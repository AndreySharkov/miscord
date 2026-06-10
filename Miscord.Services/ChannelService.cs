using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Miscord.Data;
using Miscord.Data.Models;

namespace Miscord.Services
{
    public interface IChannelService
    {
        Task<Channel?> GetChannelByIdAsync(int channelId);
        Task<Channel?> GetChannelForChatAsync(int channelId);
        Task<List<ChatMessageDto>> GetChatMessagesAsync(int channelId);
        Task CreateCategoryAsync(int serverId, string name);
        Task CreateChannelAsync(int serverId, string name, int? categoryId);
        Task<Message> CreateMessageAsync(int channelId, string userId, string content, byte[]? attachmentData, string? fileName, string? contentType, int? parentMessageId);
        Task<(byte[] data, string contentType, string fileName)?> GetAttachmentAsync(int messageId);
        Task<(int count, bool hasReacted, int channelId)> ToggleReactionAsync(int messageId, string userId, string emoji);
    }

    public class ChannelService : IChannelService
    {
        private readonly AppDbContext _context;

        public ChannelService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<Channel?> GetChannelByIdAsync(int channelId)
        {
            return await _context.Channels
                .AsNoTracking()
                .Select(c => new Channel
                {
                    Id = c.Id,
                    ServerId = c.ServerId,
                    Name = c.Name
                })
                .FirstOrDefaultAsync(c => c.Id == channelId);
        }

        public async Task<Channel?> GetChannelForChatAsync(int channelId)
        {
            return await _context.Channels
                .AsNoTracking()
                .Include(c => c.Server)
                .Select(c => new Channel
                {
                    Id = c.Id,
                    Name = c.Name,
                    ServerId = c.ServerId,
                    Server = new Server
                    {
                        Id = c.Server.Id,
                        Name = c.Server.Name,
                        OwnerId = c.Server.OwnerId
                    }
                })
                .FirstOrDefaultAsync(c => c.Id == channelId);
        }

        public async Task<List<ChatMessageDto>> GetChatMessagesAsync(int channelId)
        {
            var messages = await _context.Messages
                .AsNoTracking()
                .Where(m => m.ChannelId == channelId)
                .OrderByDescending(m => m.Timestamp)
                .Take(100)
                .Select(m => new ChatMessageDto
                {
                    Id                            = m.Id,
                    Content                       = m.Content,
                    Timestamp                     = m.Timestamp,
                    AuthorId                      = m.AuthorId,
                    AuthorNickname                = m.Author.Nickname,
                    AuthorUserName                = m.Author.UserName,
                    AuthorHasProfilePicture       = m.Author.ProfilePictureData != null,
                    HasAttachment                 = m.AttachmentData != null,
                    AttachmentFileName            = m.AttachmentFileName,
                    AttachmentContentType         = m.AttachmentContentType,
                    ReplyToMessageId              = m.ReplyToMessageId,
                    ParentContent                 = m.ParentMessage != null ? m.ParentMessage.Content : null,
                    ParentAuthorId                = m.ParentMessage != null ? m.ParentMessage.AuthorId : null,
                    ParentAuthorNickname          = m.ParentMessage != null ? m.ParentMessage.Author.Nickname : null,
                    ParentAuthorUserName          = m.ParentMessage != null ? m.ParentMessage.Author.UserName : null,
                    ParentAuthorHasProfilePicture = m.ParentMessage != null && m.ParentMessage.Author.ProfilePictureData != null,
                })
                .ToListAsync();

            messages.Reverse();
            return messages;
        }

        public async Task CreateCategoryAsync(int serverId, string name)
        {
            var category = new ChannelCategory 
            { 
                ServerId = serverId, 
                Name = name, 
                Position = await _context.ChannelCategories.CountAsync(cc => cc.ServerId == serverId) 
            };
            _context.ChannelCategories.Add(category);
            await _context.SaveChangesAsync();
        }

        public async Task CreateChannelAsync(int serverId, string name, int? categoryId)
        {
            var channel = new Channel 
            { 
                ServerId = serverId, 
                Name = name, 
                ChannelCategoryId = categoryId, 
                Position = await _context.Channels.CountAsync(c => c.ServerId == serverId && c.ChannelCategoryId == categoryId) 
            };
            _context.Channels.Add(channel);
            await _context.SaveChangesAsync();
        }

        public async Task<Message> CreateMessageAsync(int channelId, string userId, string content, byte[]? attachmentData, string? fileName, string? contentType, int? parentMessageId)
        {
            var message = new Message
            {
                AuthorId = userId,
                ChannelId = channelId,
                Content = content ?? "",
                Timestamp = DateTime.UtcNow,
                ReplyToMessageId = parentMessageId,
                AttachmentData = attachmentData,
                AttachmentFileName = fileName,
                AttachmentContentType = contentType
            };

            _context.Messages.Add(message);
            await _context.SaveChangesAsync();
            return message;
        }

        public async Task<(byte[] data, string contentType, string fileName)?> GetAttachmentAsync(int messageId)
        {
            var message = await _context.Messages
                .AsNoTracking()
                .Select(m => new { m.Id, m.AttachmentData, m.AttachmentContentType, m.AttachmentFileName })
                .FirstOrDefaultAsync(m => m.Id == messageId);

            if (message == null || message.AttachmentData == null) return null;

            return (message.AttachmentData, message.AttachmentContentType ?? "application/octet-stream", message.AttachmentFileName ?? "file");
        }

        public async Task<(int count, bool hasReacted, int channelId)> ToggleReactionAsync(int messageId, string userId, string emoji)
        {
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
            
            return (count, hasReacted, message?.ChannelId ?? 0);
        }
    }
}
