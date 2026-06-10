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
    public class ChannelServiceTests
    {
        private AppDbContext GetDbContext()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;
            return new AppDbContext(options);
        }

        [Fact]
        public async Task CreateMessageAsync_ShouldSaveToDatabase()
        {
            // Arrange
            using var context = GetDbContext();
            var service = new ChannelService(context);
            var channelId = 1;
            var userId = "user-1";
            var content = "Hello World";

            // Act
            var message = await service.CreateMessageAsync(channelId, userId, content, null, null, null, null);

            // Assert
            var savedMessage = await context.Messages.FindAsync(message.Id);
            Assert.NotNull(savedMessage);
            Assert.Equal(content, savedMessage.Content);
            Assert.Equal(userId, savedMessage.AuthorId);
            Assert.Equal(channelId, savedMessage.ChannelId);
        }

        [Fact]
        public async Task CreateChannelAsync_ShouldIncrementPosition()
        {
            // Arrange
            using var context = GetDbContext();
            var service = new ChannelService(context);
            var serverId = 1;

            // Act
            await service.CreateChannelAsync(serverId, "channel-1", null);
            await service.CreateChannelAsync(serverId, "channel-2", null);

            // Assert
            var channels = await context.Channels.Where(c => c.ServerId == serverId).OrderBy(c => c.Position).ToListAsync();
            Assert.Equal(2, channels.Count);
            Assert.Equal(0, channels[0].Position);
            Assert.Equal(1, channels[1].Position);
        }
    }
}
