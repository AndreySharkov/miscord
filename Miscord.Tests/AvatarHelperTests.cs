using Miscord.Services;
using Xunit;

namespace Miscord.Tests;

public class AvatarHelperTests
{
    [Fact]
    public void GetUserColor_ShouldReturnColorString()
    {
        // Arrange
        string username = "testuser";

        // Act
        string result = AvatarHelper.GetUserColor(username);

        // Assert
        Assert.NotNull(result);
        Assert.StartsWith("#", result);
        Assert.True(result.Length == 7);
    }

    [Theory]
    [InlineData("alice")]
    [InlineData("bob")]
    [InlineData("charlie")]
    public void GetUserColor_ShouldBeConsistentForSameUsername(string username)
    {
        // Act
        string color1 = AvatarHelper.GetUserColor(username);
        string color2 = AvatarHelper.GetUserColor(username);

        // Assert
        Assert.Equal(color1, color2);
    }

    [Fact]
    public void GetUserColor_ShouldHandleNullUsername()
    {
        // Act
        string result = AvatarHelper.GetUserColor(null!);

        // Assert
        Assert.NotNull(result);
        Assert.StartsWith("#", result);
    }
}
