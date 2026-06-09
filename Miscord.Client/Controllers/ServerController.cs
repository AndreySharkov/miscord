using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Miscord.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using System.IO;
using Microsoft.AspNetCore.SignalR;
using Miscord.Client.Hubs;
using Miscord.Client.Models;
using Miscord.Data.Models;
using System.Security.Claims;
using Microsoft.Identity.Client;
using Miscord.Client.Services;


namespace Miscord.Client.Controllers
{
    public class ServerController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IHubContext<ChatHub> _chatHubContext;
        private readonly PermissionHelper _permissionHelper;

        public ServerController(AppDbContext context, IHubContext<ChatHub> chatHubContext, PermissionHelper permissionHelper)
        {
            _context = context;
            _chatHubContext = chatHubContext;
            _permissionHelper = permissionHelper;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var servers = await _context.Servers.Where(s => s.Owner != null).ToListAsync();
            return View(servers);
        }

        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            var server = await _context.Servers
                .Include(s => s.ChannelCategories.OrderBy(cc => cc.Position))
                    .ThenInclude(cc => cc.Channels.OrderBy(c => c.Position))
                .Include(s => s.Channels.Where(c => c.ChannelCategoryId == null).OrderBy(c => c.Position))
                .FirstOrDefaultAsync(s => s.Id == id);

            if (server == null)
            {
                return NotFound();
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            ViewData["ServerName"] = server.Name;
            ViewData["Channels"] = server.Channels;
            ViewData["Categories"] = server.ChannelCategories;
            ViewData["Server"] = server;
            ViewData["CurrentUserId"] = userId;

            var hasManageChannels = await _permissionHelper.HasPermission(userId, id, ServerPermissions.ManageChannels);
            var hasManageServer = await _permissionHelper.HasPermission(userId, id, ServerPermissions.ManageServer);
            var hasManageRoles = await _permissionHelper.HasPermission(userId, id, ServerPermissions.ManageRoles);
            ViewData["IsAdmin"] = hasManageChannels || hasManageServer || hasManageRoles;
            
            return View(server);
        }

        [HttpGet]
        public async Task<IActionResult> GetChannels(int id)
        {
            var server = await _context.Servers
                .Include(s => s.ChannelCategories.OrderBy(cc => cc.Position))
                    .ThenInclude(cc => cc.Channels.OrderBy(c => c.Position))
                .Include(s => s.Channels.Where(c => c.ChannelCategoryId == null).OrderBy(c => c.Position))
                .FirstOrDefaultAsync(s => s.Id == id);

            if (server == null)
            {
                return NotFound();
            }            
            
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            ViewData["CurrentUserId"] = userId;
            
            // Comprehensive admin check
            var hasManageChannels = await _permissionHelper.HasPermission(userId, id, ServerPermissions.ManageChannels);
            var hasManageServer = await _permissionHelper.HasPermission(userId, id, ServerPermissions.ManageServer);
            var hasManageRoles = await _permissionHelper.HasPermission(userId, id, ServerPermissions.ManageRoles);
            ViewData["IsAdmin"] = hasManageChannels || hasManageServer || hasManageRoles;

            return PartialView("_ChannelList", server);
        }

        [HttpGet]
        public async Task<IActionResult> GetChat(int channelId)
        {
            var channel = await _context.Channels
                .AsNoTracking()
                .Where(c => c.Id == channelId)
                .Select(c => new { c.Name, c.ServerId })
                .FirstOrDefaultAsync();

            if (channel == null)
            {
                return NotFound();
            }

            var messages = await _context.Messages
                .AsNoTracking()
                .Where(m => m.ChannelId == channelId)
                .OrderByDescending(m => m.Timestamp)
                .Take(100)
                .Select(m => new ChatMessageViewModel
                {
                    Id                         = m.Id,
                    Content                    = m.Content,
                    Timestamp                  = m.Timestamp,
                    AuthorId                   = m.AuthorId,
                    AuthorDisplayName          = m.Author.Nickname ?? m.Author.UserName,
                    AuthorHasProfilePicture    = m.Author.ProfilePictureData != null,
                    HasAttachment              = m.AttachmentData != null,
                    AttachmentFileName         = m.AttachmentFileName,
                    AttachmentContentType      = m.AttachmentContentType,
                    ReplyToMessageId           = m.ReplyToMessageId,
                    ParentContent              = m.ParentMessage != null ? m.ParentMessage.Content : null,
                    ParentAuthorId             = m.ParentMessage != null ? m.ParentMessage.AuthorId : null,
                    ParentAuthorDisplayName    = m.ParentMessage != null
                                                    ? (m.ParentMessage.Author.Nickname ?? m.ParentMessage.Author.UserName)
                                                    : null,
                    ParentAuthorHasProfilePicture = m.ParentMessage != null && m.ParentMessage.Author.ProfilePictureData != null,
                })
                .ToListAsync();

            messages.Reverse();

            var vm = new ChatChannelViewModel
            {
                Id       = channelId,
                Name     = channel.Name,
                Messages = messages,
            };

            return PartialView("_ChatArea", vm);
        }

        [HttpPost]
        public async Task<IActionResult> PostMessage(
            [FromForm] string content,
            [FromForm] int channelId,
            [FromForm] string userId,
            [FromForm] IFormFile? attachment,
            [FromForm] int? parentMessageId)
        {
            if (string.IsNullOrWhiteSpace(content) && attachment == null)
            {
                return BadRequest("Message content or attachment is required.");
            }

            var channel = await _context.Channels.FindAsync(channelId);
            if (channel == null) return NotFound();
            
            if (!await _permissionHelper.HasPermission(userId, channel.ServerId, ServerPermissions.SendMessages))
                return Unauthorized("You do not have permission to send messages in this server.");

            if (content != null && content.Length > 6000)
            {
                return BadRequest("Message is too long (max 6,000 characters).");
            }

            var message = new Message
            {
                AuthorId = userId,
                ChannelId = channelId,
                Content = content ?? "",
                Timestamp = DateTime.UtcNow,
                ReplyToMessageId = parentMessageId
            };

            if (attachment != null && attachment.Length > 0)
            {
                using (var ms = new MemoryStream())
                {
                    await attachment.CopyToAsync(ms);
                    message.AttachmentData = ms.ToArray();
                }
                message.AttachmentFileName = attachment.FileName;
                message.AttachmentContentType = attachment.ContentType;
            }

            _context.Messages.Add(message);
            await _context.SaveChangesAsync();

            string? parentMessageContent = null;
            string? parentMessageAuthor = null;
            byte[]? parentAuthorPfp = null;
            if(parentMessageId.HasValue)
            {
                var parentMessage = await _context.Messages
                    .Include(m => m.Author)
                    .FirstOrDefaultAsync(m => m.Id == parentMessageId.Value);
                if (parentMessage != null)
                {
                    parentMessageContent = parentMessage.Content;
                    parentMessageAuthor = parentMessage.Author.Nickname ?? parentMessage.Author.UserName;
                    parentAuthorPfp = parentMessage.Author.ProfilePictureData;
                }
            }

            var user = await _context.Users.FindAsync(userId);
            var displayName = user?.Nickname ?? user?.UserName ?? "Unknown";
            var pfpBase64 = user?.ProfilePictureData != null ? Convert.ToBase64String(user.ProfilePictureData) : null;

            await _chatHubContext.Clients.Group(channelId.ToString()).SendAsync(
                "ReceiveMessage",
                displayName,
                message.Content,
                channelId,
                pfpBase64,
                message.Id,
                message.AttachmentFileName,
                message.AttachmentContentType,
                parentMessageId,
                parentMessageContent,
                parentMessageAuthor + "|" + (parentAuthorPfp != null ? Convert.ToBase64String(parentAuthorPfp) : "") + "|" + userId
            );

            return Ok(new { messageId = message.Id });
        }

        [HttpGet]
        public async Task<IActionResult> GetAttachment(int messageId)
        {
            var message = await _context.Messages
                .AsNoTracking()
                .Select(m => new { m.Id, m.AttachmentData, m.AttachmentContentType, m.AttachmentFileName })
                .FirstOrDefaultAsync(m => m.Id == messageId);

            if (message == null || message.AttachmentData == null)
            {
                return NotFound();
            }

            Response.Headers["Cache-Control"] = "public, max-age=604800, immutable";
            return File(message.AttachmentData, message.AttachmentContentType ?? "application/octet-stream", message.AttachmentFileName);
        }

        [HttpGet]
        public async Task<IActionResult> GetProfilePicture(string userId)
        {
            var user = await _context.Users
                .AsNoTracking()
                .Select(u => new { u.Id, u.ProfilePictureData })
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null || user.ProfilePictureData == null)
            {
                return NotFound();
            }

            Response.Headers["Cache-Control"] = "public, max-age=3600";
            return File(user.ProfilePictureData, "image/png");
        }

        [HttpPost]
        public async Task<IActionResult> ToggleReaction(
            [FromForm] int messageId,
            [FromForm] string emoji)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null) return Unauthorized();

            var existing = await _context.Reactions.FirstOrDefaultAsync(r => r.MessageId == messageId && r.UserId == userId && r.Emoji == emoji);
            bool hasReacted;
            if (existing != null) {
                _context.Reactions.Remove(existing);
                hasReacted = false;
            } else {
                var reaction = new Reaction { MessageId = messageId, UserId = userId, Emoji = emoji };
                _context.Reactions.Add(reaction);
                hasReacted = true;
            }
            await _context.SaveChangesAsync();
            
            var count = await _context.Reactions.CountAsync(r => r.MessageId == messageId && r.Emoji == emoji);
            var message = await _context.Messages.FindAsync(messageId);
            if (message != null)
            {
                await _chatHubContext.Clients.Group(message.ChannelId.ToString()).SendAsync(
                    "ReactionToggled",
                    messageId,
                    emoji,
                    count,
                    hasReacted
                );
            }

            return Ok();
        }

        [HttpPost]
        public async Task<IActionResult> CreateServer(
            [FromForm] string ServerName,
            [FromForm] string? serverType,
            [FromForm] IFormFile? serverIcon
        )
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null) return Unauthorized();

            var server = new Server { Name = ServerName, OwnerId = userId };
            if (serverIcon != null && serverIcon.Length > 0)
            {
                using (var ms = new MemoryStream()) {
                    await serverIcon.CopyToAsync(ms);
                    server.IconData = ms.ToArray();
                }
            }
            _context.Servers.Add(server);
            await _context.SaveChangesAsync();

            // Default Channels
            _context.Channels.Add(new Channel { Name = "general", ServerId = server.Id });
            _context.Channels.Add(new Channel { Name = "announcements", ServerId = server.Id });
            
            if (serverType == "gaming") _context.Channels.Add(new Channel { Name = "lobby", ServerId = server.Id });
            else if (serverType == "school") _context.Channels.Add(new Channel { Name = "homework-help", ServerId = server.Id });
            
            // Add creator as member
            var member = new ServerMember { ServerId = server.Id, UserId = userId, JoinedAt = DateTime.UtcNow };
            _context.ServerMembers.Add(member);

            // Add Default Roles
            var adminRole = new ServerRole
            {
                Name = "Admin",
                ServerId = server.Id,
                Position = 0,
                Color = "#e74c3c", // Red-ish
                Permissions = (long)ServerPermissions.Administrator
            };
            var memberRole = new ServerRole
            {
                Name = "Member",
                ServerId = server.Id,
                Position = 1,
                Color = "#9b59b6", // Purple-ish
                Permissions = (long)(ServerPermissions.SendMessages | ServerPermissions.AddReactions | ServerPermissions.ReadMessageHistory | ServerPermissions.CreateInvite | ServerPermissions.ChangeNickname)
            };

            _context.ServerRoles.Add(adminRole);
            _context.ServerRoles.Add(memberRole);
            await _context.SaveChangesAsync();

            // Assign Admin role to creator
            _context.ServerMemberRoles.Add(new ServerMemberRole { ServerMemberId = member.Id, ServerRoleId = adminRole.Id });

            await _context.SaveChangesAsync();

            return Ok(new { serverId = server.Id });
        }

        [HttpPost]
        public async Task<IActionResult> UpdateServer(
            [FromForm] int serverId,
            [FromForm] string serverName,
            [FromForm] IFormFile? serverIcon
        )
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var server = await _context.Servers.FindAsync(serverId);
            if (server == null) return NotFound();
            
            if (!await _permissionHelper.HasPermission(userId, serverId, ServerPermissions.ManageServer))
                return Unauthorized();

            server.Name = serverName;
            if (serverIcon != null && serverIcon.Length > 0)
            {
                using (var ms = new MemoryStream()) {
                    await serverIcon.CopyToAsync(ms);
                    server.IconData = ms.ToArray();
                }
            }
            await _context.SaveChangesAsync();

            return Ok();
        }

        [HttpGet]
        public async Task<IActionResult> GetServerIcon(int serverId)
        {
            var server = await _context.Servers.AsNoTracking().Select(s => new { s.Id, s.IconData }).FirstOrDefaultAsync(s => s.Id == serverId);
            if (server == null || server.IconData == null) return NotFound();
            Response.Headers["Cache-Control"] = "public, max-age=3600";
            return File(server.IconData, "image/png");
        }

        [HttpPost]
        public async Task<IActionResult> CreateCategory([FromForm] int serverId, [FromForm] string name)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!await _permissionHelper.HasPermission(userId, serverId, ServerPermissions.ManageChannels)) return Unauthorized();

            var category = new ChannelCategory { ServerId = serverId, Name = name, Position = await _context.ChannelCategories.CountAsync(cc => cc.ServerId == serverId) };
            _context.ChannelCategories.Add(category);
            await _context.SaveChangesAsync();
            return Ok();
        }

        [HttpPost]
        public async Task<IActionResult> CreateChannel([FromForm] int serverId, [FromForm] string name, [FromForm] int? categoryId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!await _permissionHelper.HasPermission(userId, serverId, ServerPermissions.ManageChannels)) return Unauthorized();

            var channel = new Channel { ServerId = serverId, Name = name, ChannelCategoryId = categoryId, Position = await _context.Channels.CountAsync(c => c.ServerId == serverId && c.ChannelCategoryId == categoryId) };
            _context.Channels.Add(channel);
            await _context.SaveChangesAsync();
            return Ok();
        }

        [HttpGet]
        public async Task<IActionResult> GetRoles(int serverId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!await _permissionHelper.HasPermission(userId, serverId, ServerPermissions.ManageRoles)) return Unauthorized();

            var roles = await _context.ServerRoles.Where(r => r.ServerId == serverId).OrderBy(r => r.Position).ToListAsync();
            return Ok(roles);
        }

        [HttpPost]
        public async Task<IActionResult> CreateRole([FromForm] int serverId, [FromForm] string name)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!await _permissionHelper.HasPermission(userId, serverId, ServerPermissions.ManageRoles)) return Unauthorized();

            var role = new ServerRole {
                ServerId = serverId,
                Name = name,
                Position = await _context.ServerRoles.CountAsync(r => r.ServerId == serverId),
                Permissions = (long)(ServerPermissions.SendMessages | ServerPermissions.AddReactions | ServerPermissions.ReadMessageHistory)
            };
            _context.ServerRoles.Add(role);
            await _context.SaveChangesAsync();
            return Ok(role);
        }

        [HttpPost]
        public async Task<IActionResult> UpdateRole([FromForm] int serverId, [FromForm] int roleId, [FromForm] string name, [FromForm] string? color, [FromForm] long permissions)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!await _permissionHelper.HasPermission(userId, serverId, ServerPermissions.ManageRoles)) return Unauthorized();

            var role = await _context.ServerRoles.FindAsync(roleId);
            if (role == null || role.ServerId != serverId) return NotFound();

            role.Name = name;
            role.Color = color;
            role.Permissions = permissions;
            await _context.SaveChangesAsync();
            return Ok();
        }

        [HttpPost]
        public async Task<IActionResult> DeleteRole(int serverId, int roleId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!await _permissionHelper.HasPermission(userId, serverId, ServerPermissions.ManageRoles)) return Unauthorized();

            var role = await _context.ServerRoles.FindAsync(roleId);
            if (role == null || role.ServerId != serverId) return NotFound();

            _context.ServerRoles.Remove(role);
            await _context.SaveChangesAsync();
            return Ok();
        }

        [HttpGet]
        public async Task<IActionResult> GetMembers(int serverId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!await _permissionHelper.HasPermission(userId, serverId, ServerPermissions.ManageRoles))
                return Unauthorized();

            var members = await _context.ServerMembers
                .Include(sm => sm.User)
                .Include(sm => sm.MemberRoles)
                    .ThenInclude(mr => mr.ServerRole)
                .Where(sm => sm.ServerId == serverId)
                .Select(sm => new {
                    sm.UserId,
                    DisplayName = sm.Nickname ?? sm.User.Nickname ?? sm.User.UserName,
                    sm.User.UserName,
                    HasPfp = sm.User.ProfilePictureData != null,
                    Roles = sm.MemberRoles.Select(mr => new { mr.ServerRole.Id, mr.ServerRole.Name, mr.ServerRole.Color })
                })
                .ToListAsync();

            return Ok(members);
        }

        [HttpPost]
        public async Task<IActionResult> UpdateMemberRoles([FromForm] int serverId, [FromForm] string userId, [FromForm] string roleIds)
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!await _permissionHelper.HasPermission(currentUserId, serverId, ServerPermissions.ManageRoles))
                return Unauthorized();

            var member = await _context.ServerMembers
                .Include(sm => sm.MemberRoles)
                .FirstOrDefaultAsync(sm => sm.ServerId == serverId && sm.UserId == userId);

            if (member == null) return NotFound();

            _context.ServerMemberRoles.RemoveRange(member.MemberRoles);

            if (!string.IsNullOrEmpty(roleIds))
            {
                var ids = roleIds.Split(',').Select(int.Parse).ToList();
                foreach (var rid in ids)
                {
                    _context.ServerMemberRoles.Add(new ServerMemberRole { ServerMemberId = member.Id, ServerRoleId = rid });
                }
            }

            await _context.SaveChangesAsync();
            return Ok();
        }

        [HttpPost]
        public async Task<IActionResult> DeleteServer(int serverId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var server = await _context.Servers.FindAsync(serverId);
            if (server == null) return NotFound();

            if (server.OwnerId != userId) return Unauthorized("Only the owner can delete the server.");

            server.IsDeleted = true;
            await _context.SaveChangesAsync();
            return Ok();
        }

        [HttpGet]
        public async Task<IActionResult> Join(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null) return RedirectToAction("Login", "Account", new { returnUrl = Url.Action("Join", "Server", new { id = id }) });

            var server = await _context.Servers.FindAsync(id);
            if (server == null) return NotFound();

            var existingMember = await _context.ServerMembers.FirstOrDefaultAsync(sm => sm.ServerId == id && sm.UserId == userId);
            if (existingMember == null)
            {
                _context.ServerMembers.Add(new ServerMember { ServerId = id, UserId = userId, JoinedAt = DateTime.UtcNow });
                await _context.SaveChangesAsync();
            }

            return RedirectToAction("Details", new { id = id });
        }
    }
}
