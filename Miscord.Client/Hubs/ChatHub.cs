using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Miscord.Data;
using Miscord.Data.Models;

namespace Miscord.Client.Hubs
{
    public class ChatHub : Hub
    {
        private readonly AppDbContext _context;
        public ChatHub(AppDbContext context)
        {
            _context = context;
        }
        public async Task SendMessage(string userId, int channelId, string content)
        {
            var user = await _context.Users.FindAsync(userId);
            var displayName = user?.Nickname ?? user?.UserName ?? "Unknown";

            var message = new Message
            {
                AuthorId = userId,
                ChannelId = channelId,
                Content = content,
                Timestamp = DateTime.UtcNow
            };
            _context.Messages.Add(message);
            await _context.SaveChangesAsync();

            await Clients.Group(channelId.ToString()).SendAsync("ReceiveMessage", displayName, content);
        }
    }
}