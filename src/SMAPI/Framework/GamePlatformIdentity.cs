namespace StardewModdingAPI.Framework;

/// <summary>Resolves the platform identities used for the active game and mod compatibility.</summary>
internal readonly record struct GamePlatformIdentity(
    GamePlatform TargetPlatform,
    GamePlatform ModCompatibilityPlatform
)
{
    /// <summary>Resolve the platform identities for the current launch mode.</summary>
    /// <param name="detectedPlatform">The platform detected from the host environment.</param>
    /// <param name="usesAndroidLauncher">Whether the game is being started through the Android launcher.</param>
    /// <param name="isMobileGame">Whether the launcher selected the mobile Stardew Valley build.</param>
    public static GamePlatformIdentity Resolve(
        GamePlatform detectedPlatform,
        bool usesAndroidLauncher,
        bool isMobileGame
    )
    {
        if (!usesAndroidLauncher)
            return new(detectedPlatform, detectedPlatform);

        return new(
            TargetPlatform: isMobileGame ? GamePlatform.Android : GamePlatform.Linux,
            ModCompatibilityPlatform: GamePlatform.Linux
        );
    }
}
