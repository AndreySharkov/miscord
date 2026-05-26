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
            Console.WriteLine($"SendMessage invoked: User={userId}, Channel={channelId}, Content={content}");
            var user = await _context.Users.FindAsync(userId);
            var displayName = user?.Nickname ?? user?.UserName ?? "Unknown";
            var pfpBase64 = user?.ProfilePictureData != null ? Convert.ToBase64String(user.ProfilePictureData) : null;

            var message = new Message
            {
                AuthorId = userId,
                ChannelId = channelId,
                Content = content,
                Timestamp = DateTime.UtcNow
            };
            _context.Messages.Add(message);
            await _context.SaveChangesAsync();

            Console.WriteLine($"Broadcasting to Group={channelId}");
            await Clients.Group(channelId.ToString()).SendAsync("ReceiveMessage", displayName, content, channelId, pfpBase64);
        }

        public async Task JoinChannel(int channelId)
        {
            Console.WriteLine($"User {Context.ConnectionId} joining Group={channelId}");
            await Groups.AddToGroupAsync(Context.ConnectionId, channelId.ToString());
        }
    }
}