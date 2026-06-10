using Miscord.Data.Models;
using Xunit;

namespace Miscord.Tests;

public class PermissionTests
{
    [Fact]
    public void Permissions_ShouldSupportBitwiseOperations()
    {
        // Arrange
        var permissions = ServerPermissions.SendMessages | ServerPermissions.AddReactions;

        // Assert
        Assert.True(permissions.HasFlag(ServerPermissions.SendMessages));
        Assert.True(permissions.HasFlag(ServerPermissions.AddReactions));
        Assert.False(permissions.HasFlag(ServerPermissions.Administrator));
    }

    [Fact]
    public void Administrator_ShouldBeSpecificFlag()
    {
        // Arrange
        long adminFlag = (long)ServerPermissions.Administrator;

        // Assert
        Assert.Equal(1L, adminFlag);
    }
}
