using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Miscord.Client.Hubs;
using Miscord.Client.Models;
using Miscord.Data.Models;
using Miscord.Services;
using Miscord.Data;

namespace Miscord.Client.Controllers
{
    [Authorize]
    public class ChannelController : Controller
    {
        private readonly IChannelService _channelService;
        private readonly IHubContext<ChatHub> _chatHubContext;
        private readonly PermissionHelper _permissionHelper;
        private readonly AppDbContext _context;

        public ChannelController(IChannelService channelService, IHubContext<ChatHub> chatHubContext, PermissionHelper permissionHelper, AppDbContext context)
        {
            _channelService = channelService;
            _chatHubContext = chatHubContext;
            _permissionHelper = permissionHelper;
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetChat(int channelId)
        {
            var channel = await _channelService.GetChannelForChatAsync(channelId);
            if (channel == null) return NotFound();

            var messages = await _channelService.GetChatMessagesAsync(channelId);

            var vm = new ChatChannelViewModel
            {
                Id       = channelId,
                Name     = channel.Name,
                Messages = messages.Select(m => new ChatMessageViewModel
                {
                    Id                           = m.Id,
                    Content                      = m.Content,
                    Timestamp                    = m.Timestamp,
                    AuthorId                     = m.AuthorId,
                    AuthorDisplayName            = m.AuthorNickname ?? m.AuthorUserName,
                    AuthorHasProfilePicture      = m.AuthorHasProfilePicture,
                    HasAttachment                = m.HasAttachment,
                    AttachmentFileName           = m.AttachmentFileName,
                    AttachmentContentType        = m.AttachmentContentType,
                    ReplyToMessageId             = m.ReplyToMessageId,
                    ParentContent                = m.ParentContent,
                    ParentAuthorId               = m.ParentAuthorId,
                    ParentAuthorDisplayName      = m.ParentAuthorNickname ?? m.ParentAuthorUserName,
                    ParentAuthorHasProfilePicture = m.ParentAuthorHasProfilePicture,
                }).ToList(),
                Server   = channel.Server
            };

            return PartialView("~/Views/Server/_ChatArea.cshtml", vm);
        }

        [HttpPost]
        public async Task<IActionResult> PostMessage(
            [FromForm] string? content, // Changed to nullable string to handle form data cleanly
            [FromForm] int channelId,
            [FromForm] string userId,
            [FromForm] IFormFile? attachment,
            [FromForm] int? parentMessageId)
        {
            var channel = await _channelService.GetChannelByIdAsync(channelId);
            if (channel == null) return NotFound();
            
            if (!await _permissionHelper.HasPermission(userId, channel.ServerId, ServerPermissions.SendMessages))
                return Unauthorized("You do not have permission to send messages in this server.");

            // FIXED: Guard against completely empty submissions
            if (string.IsNullOrEmpty(content) && (attachment == null || attachment.Length == 0))
                return BadRequest("Cannot send an empty message.");

            if (content != null && content.Length > 6000)
                return BadRequest("Message is too long (max 6,000 characters).");

            byte[]? attachmentData = null;
            string? fileName = null;
            string? contentType = null;

            if (attachment != null && attachment.Length > 0)
            {
                using (var ms = new MemoryStream())
                {
                    await attachment.CopyToAsync(ms);
                    attachmentData = ms.ToArray();
                }
                fileName = attachment.FileName;
                contentType = attachment.ContentType;
            }

            // FIXED: Using null-coalescing operator (?? "") to prevent CS8604 null reference argument warnings
            var message = await _channelService.CreateMessageAsync(channelId, userId, content ?? "", attachmentData, fileName, contentType, parentMessageId);

            // --- RESTORED SIGNALR BROADCAST LOGIC ---
            string? parentMessageContent = null;
            string? parentMessageAuthor = null;
            byte[]? parentAuthorPfp = null;
            
            if (parentMessageId.HasValue)
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

            var authorUser = await _context.Users.FindAsync(userId);
            var displayName = authorUser?.Nickname ?? authorUser?.UserName ?? "Unknown";
            var pfpBase64 = authorUser?.ProfilePictureData != null ? Convert.ToBase64String(authorUser.ProfilePictureData) : null;

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
            var result = await _channelService.GetAttachmentAsync(messageId);
            if (result == null) return NotFound();

            // ETag for efficient 304 Not Modified responses
            var etag = $"\"{Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(result.Value.data)[..8])}\"";
            if (Request.Headers.IfNoneMatch.ToString() == etag)
                return StatusCode(304);

            Response.Headers["Cache-Control"] = "public, max-age=604800, immutable";
            Response.Headers["ETag"] = etag;
            return File(result.Value.data, result.Value.contentType, result.Value.fileName);
        }

        [HttpGet]
        public async Task<IActionResult> SearchMessages(int channelId, string query)
        {
            if (string.IsNullOrWhiteSpace(query)) return BadRequest();

            var messages = await _channelService.GetChatMessagesAsync(channelId);
            
            var results = messages
                .Select(m => new { 
                    Message = m, 
                    Score = FuzzySearch.GetJaroWinklerSimilarity(m.Content, query) 
                })
                .Where(r => r.Score > 0.7 || r.Message.Content.Contains(query, StringComparison.OrdinalIgnoreCase)) 
                .OrderByDescending(r => r.Score)
                .Take(10)
                .Select(r => new {
                    id = r.Message.Id,
                    content = r.Message.Content,
                    author = r.Message.AuthorNickname ?? r.Message.AuthorUserName,
                    timestamp = r.Message.Timestamp.ToString("MM/dd/yyyy h:mm tt")
                });

            return Ok(results);
        }

        [HttpPost]
        public async Task<IActionResult> ToggleReaction([FromForm] int messageId, [FromForm] string emoji)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null) return Unauthorized();

            var (count, hasReacted, channelId) = await _channelService.ToggleReactionAsync(messageId, userId, emoji);
            
            if (channelId != 0)
            {
                await _chatHubContext.Clients.Group(channelId.ToString()).SendAsync(
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
        public async Task<IActionResult> CreateCategory([FromForm] int serverId, [FromForm] string name)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null) return Unauthorized(); 

            if (!await _permissionHelper.HasPermission(userId, serverId, ServerPermissions.ManageChannels)) return Unauthorized();

            await _channelService.CreateCategoryAsync(serverId, name);
            return Ok();
        }

        [HttpPost]
        public async Task<IActionResult> CreateChannel([FromForm] int serverId, [FromForm] string name, [FromForm] int? categoryId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null) return Unauthorized(); 

            if (!await _permissionHelper.HasPermission(userId, serverId, ServerPermissions.ManageChannels)) return Unauthorized();

            await _channelService.CreateChannelAsync(serverId, name, categoryId);
            return Ok();
        }
    }
}