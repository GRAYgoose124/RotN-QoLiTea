using QoLiTea.Features.RandomSong;
using Xunit;

namespace QoLiTea.Tests;

public class RandomSongPlayPolicyTests
{
    [Fact]
    public void OpenLoadout_and_autoStart_are_opposites()
    {
        Assert.True(RandomSongPlayPolicy.ShouldOpenLoadout(openLoadoutInstead: true));
        Assert.False(RandomSongPlayPolicy.ShouldAutoStart(openLoadoutInstead: true));
        Assert.False(RandomSongPlayPolicy.ShouldOpenLoadout(openLoadoutInstead: false));
        Assert.True(RandomSongPlayPolicy.ShouldAutoStart(openLoadoutInstead: false));
    }
}
