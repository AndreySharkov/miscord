using Microsoft.EntityFrameworkCore;
using Miscord.Data;
using Miscord.Data.Models;
using Miscord.Services;
using System;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Miscord.Tests
{
    public class ServerServiceTests
    {
        private AppDbContext GetDbContext()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;
            return new AppDbContext(options);
        }

        [Fact]
        public async Task CreateServerAsync_ShouldCreateServerWithDefaultChannels()
        {
            // Arrange
            using var context = GetDbContext();
            var service = new ServerService(context);
            var ownerId = "user-1";
            var serverName = "Test Server";

            // Act
            var serverId = await service.CreateServerAsync(serverName, ownerId, null, "friends");

            // Assert
            var server = await context.Servers.Include(s => s.Channels).FirstOrDefaultAsync(s => s.Id == serverId);
            Assert.NotNull(server);
            Assert.Equal(serverName, server.Name);
            Assert.Equal(ownerId, server.OwnerId);
            Assert.Contains(server.Channels, c => c.Name == "general");
            Assert.Contains(server.Channels, c => c.Name == "announcements");
        }

        [Fact]
        public async Task JoinServerAsync_ShouldAddMember()
        {
            // Arrange
            using var context = GetDbContext();
            var service = new ServerService(context);
            
            var ownerId = "owner";
            var serverId = await service.CreateServerAsync("Server", ownerId, null, null);
            
            var creatorId = "creator";
            var inviteToken = await service.CreateInviteAsync(serverId, creatorId, 1, 10);
            
            var newUserId = "new-user";

            // Act
            var resultId = await service.JoinServerAsync(inviteToken, newUserId);

            // Assert
            Assert.Equal(serverId, resultId);
            var isMember = await context.ServerMembers.AnyAsync(sm => sm.ServerId == serverId && sm.UserId == newUserId);
            Assert.True(isMember);
            
            var invite = await context.Invites.FirstAsync(i => i.Token == inviteToken);
            Assert.Equal(1, invite.Uses);
        }

        [Fact]
        public async Task LeaveServerAsync_ShouldTransferOwnership()
        {
            // Arrange
            using var context = GetDbContext();
            var service = new ServerService(context);
            
            var ownerId = "owner";
            var nextOwnerId = "next-owner";
            var serverId = await service.CreateServerAsync("Server", ownerId, null, null);
            
            // Add second member
            context.ServerMembers.Add(new ServerMember { ServerId = serverId, UserId = nextOwnerId, JoinedAt = DateTime.UtcNow.AddMinutes(1) });
            await context.SaveChangesAsync();

            // Act
            await service.LeaveServerAsync(serverId, ownerId);

            // Assert
            var server = await context.Servers.FindAsync(serverId);
            Assert.Equal(nextOwnerId, server.OwnerId);
            var wasMember = await context.ServerMembers.AnyAsync(sm => sm.ServerId == serverId && sm.UserId == ownerId);
            Assert.False(wasMember);
        }
    }
}
