using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Miscord.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualBasic;
using Microsoft.AspNetCore.Http;
using System.IO;
using Microsoft.AspNetCore.SignalR;
using Miscord.Client.Hubs;
using Miscord.Data.Models;
using System.Security.Claims;


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
            var channel = await _context.Channels
                .Include(c => c.Messages.Where(m => !m.IsDeleted).OrderBy(m => m.Timestamp))
                    .ThenInclude(m => m.Author)
                .Include(c => c.Messages.Where(m => !m.IsDeleted).OrderBy(m => m.Timestamp))
                    .ThenInclude(m => m.ParentMessage)
                        .ThenInclude(pm => pm.Author)
                .FirstOrDefaultAsync(c => c.Id == channelId);
            if (channel == null)
            {
                return NotFound();
            }
            return PartialView("_ChatArea", channel);
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
                parentMessageContent,
                parentMessageAuthor,
                parentAuthorPfp
            );

            return Ok(new { messageId = message.Id });
        }

        [HttpGet]
        public async Task<IActionResult> GetAttachment(int messageId)
        {
            var message = await _context.Messages.FindAsync(messageId);
            if (message == null || message.AttachmentData == null)
            {
                return NotFound();
            }
            return File(message.AttachmentData, message.AttachmentContentType ?? "application/octet-stream", message.AttachmentFileName);
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
    }
}