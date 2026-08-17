using FluentAssertions;
using NUnit.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Framework;

namespace SMAPI.Tests.Core;

[TestFixture]
internal class GamePlatformIdentityTests
{
    [TestCase(false, GamePlatform.Linux)]
    [TestCase(true, GamePlatform.Android)]
    public void Resolve_UsesActiveGameForAndroidLaunches(
        bool isMobileGame,
        GamePlatform expectedTargetPlatform
    )
    {
        GamePlatformIdentity identity = GamePlatformIdentity.Resolve(
            detectedPlatform: GamePlatform.Linux,
            usesAndroidLauncher: true,
            isMobileGame: isMobileGame
        );

        identity.TargetPlatform.Should().Be(expectedTargetPlatform);
        identity.ModCompatibilityPlatform.Should().Be(GamePlatform.Linux);
    }

    [TestCase(GamePlatform.Linux)]
    [TestCase(GamePlatform.Mac)]
    [TestCase(GamePlatform.Windows)]
    public void Resolve_PreservesDetectedPlatformOutsideAndroidLauncher(
        GamePlatform detectedPlatform
    )
    {
        GamePlatformIdentity identity = GamePlatformIdentity.Resolve(
            detectedPlatform,
            usesAndroidLauncher: false,
            isMobileGame: true
        );

        identity.TargetPlatform.Should().Be(detectedPlatform);
        identity.ModCompatibilityPlatform.Should().Be(detectedPlatform);
    }
}
