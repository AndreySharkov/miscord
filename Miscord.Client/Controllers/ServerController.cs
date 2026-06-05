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


namespace Miscord.Client.Controllers
{
    public class ServerController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IHubContext<ChatHub> _chatHubContext;

        public ServerController(AppDbContext context, IHubContext<ChatHub> chatHubContext)
        {
            _context = context;
            _chatHubContext = chatHubContext;
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
            var server = await _context.Servers.Include(s => s.Channels).FirstOrDefaultAsync(s => s.Id == id);
            if (server == null)
            {
                return NotFound();
            }
            ViewData["ServerName"] = server.Name;
            ViewData["Channels"] = server.Channels;
            return View(server);
        }

        [HttpGet]
        public async Task<IActionResult> GetChannels(int id)
        {
            var channels = await _context.Servers.Include(s => s.Channels).FirstOrDefaultAsync(s => s.Id == id);
            if (channels == null)
            {
                return NotFound();
            }            
            
            return PartialView("_ChannelList", channels.Channels);
        }

        [HttpGet]
        public async Task<IActionResult> GetChat(int channelId)
        {
            // Project to DTO — never loads AttachmentData (the binary blob that caused the slowness).
            // AsNoTracking skips change-tracker overhead since this is a read-only endpoint.
            var channelName = await _context.Channels
                .AsNoTracking()
                .Where(c => c.Id == channelId)
                .Select(c => c.Name)
                .FirstOrDefaultAsync();

            if (channelName == null)
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

                    // Only metadata — AttachmentData byte[] is never fetched
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

            // Chronological order for the UI
            messages.Reverse();

            var vm = new ChatChannelViewModel
            {
                Id       = channelId,
                Name     = channelName,
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

            // Broadcast to SignalR group
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

            // Attachments are immutable — cache them in the browser for 7 days
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

            // Profile pictures rarely change — cache for 1 hour
            Response.Headers["Cache-Control"] = "public, max-age=3600";
            return File(user.ProfilePictureData, "image/png");
        }

        [HttpPost]
        public async Task<IActionResult> ToggleReaction(
            [FromForm] int messageId,
            [FromForm] string emoji)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null)
            {
                return Unauthorized();
            }

            var existing = await _context.Reactions.FirstOrDefaultAsync(r => r.MessageId == messageId && r.UserId == userId && r.Emoji == emoji);
            bool hasReacted;
            if (existing != null)            {
                _context.Reactions.Remove(existing);
                hasReacted = false;
            }
            else
            {
                var reaction = new Reaction
                {
                    MessageId = messageId,
                    UserId = userId,
                    Emoji = emoji
                };
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
        public async Task<IActionResult> CreateServer(
            [FromForm] string ServerName,
            [FromForm] string? serverType,
            [FromForm] IFormFile? serverIcon
        )
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null)
            {
                return Unauthorized();
            }


            var server = new Server
            {
                Name = ServerName,
                OwnerId = userId
            };
            if (serverIcon != null && serverIcon.Length > 0)
            {
                using (var ms = new MemoryStream())
                {
                    await serverIcon.CopyToAsync(ms);
                    server.IconData = ms.ToArray();
                }
            }
            _context.Servers.Add(server);
            await _context.SaveChangesAsync();

            var channel = new Channel
            {
                Name = "general",
                ServerId = server.Id
            };
            _context.Channels.Add(channel);
            var channel2 = new Channel
            {
                Name = "announcements",
                ServerId = server.Id
            };
            _context.Channels.Add(channel2);
            switch(serverType)
            {
                case "gaming":
                    var channel3 = new Channel
                    {
                        Name = "mladost-1",
                        ServerId = server.Id
                    };
                    _context.Channels.Add(channel3);           
                    break;
                case "school":
                    var channel4 = new Channel
                    {
                        Name = "homework-help",
                        ServerId = server.Id
                    };
                    _context.Channels.Add(channel4);
                    
                    break;
                case "friends":
                        var channel5 = new Channel
                        {
                            Name = "memes",
                            ServerId = server.Id
                        };
                        _context.Channels.Add(channel5);
                    
                    break;
                case "art":
                    var channel6 = new Channel
                    {
                        Name = "art-showcase",
                        ServerId = server.Id
                    };
                    _context.Channels.Add(channel6);
                    
                    break;
                case "own":                    
                    break;
                default:
                    
                    break;
            }
            
            
            await _context.SaveChangesAsync();

            return Ok(new { serverId = server.Id });
        }
        [HttpGet]
        public async Task<IActionResult> GetServerIcon(int serverId)
        {
                var server = await _context.Servers
                    .AsNoTracking()
                    .Select(s => new { s.Id, s.IconData })
                    .FirstOrDefaultAsync(s => s.Id == serverId);

                if (server == null || server.IconData == null)
                {
                    return NotFound();
                }

                Response.Headers["Cache-Control"] = "public, max-age=3600";
            return File(server.IconData, "image/png");
        }
            
    }
}