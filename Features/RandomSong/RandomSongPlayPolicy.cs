namespace QoLiTea.Features.RandomSong;

/// <summary>
/// After jukebox land: auto-start stage vs open stock loadout ("play menu").
/// </summary>
public static class RandomSongPlayPolicy
{
    /// <summary>
    /// When <paramref name="openLoadoutInstead"/> is true, land on loadout; otherwise start immediately.
    /// </summary>
    public static bool ShouldOpenLoadout(bool openLoadoutInstead) => openLoadoutInstead;

    public static bool ShouldAutoStart(bool openLoadoutInstead) => !openLoadoutInstead;
}
