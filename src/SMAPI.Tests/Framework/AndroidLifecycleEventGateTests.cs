using FluentAssertions;
using NUnit.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Framework;

namespace SMAPI.Tests.Framework;

[TestFixture]
internal class AndroidLifecycleEventGateTests
{
    [Test]
    public void DesktopGame_DoesNotWaitForMobileIncrementalLoad()
    {
        AndroidLifecycleEventGate.IsInitialContentLoaded(
            GamePlatform.Linux,
            finishedIncrementalLoad: false,
            hasPlayerTeam: false
        ).Should().BeTrue();
    }

    [TestCase(false, false)]
    [TestCase(true, true)]
    public void MobileGame_UsesIncrementalLoadCompletion(bool isFinished, bool expected)
    {
        AndroidLifecycleEventGate.IsInitialContentLoaded(
            GamePlatform.Android,
            finishedIncrementalLoad: isFinished,
            hasPlayerTeam: true
        ).Should().Be(expected);
    }

    [Test]
    public void MobileGame_FallsBackToPlayerStateWhenCompletionFlagIsUnavailable()
    {
        AndroidLifecycleEventGate.IsInitialContentLoaded(
            GamePlatform.Android,
            finishedIncrementalLoad: null,
            hasPlayerTeam: true
        ).Should().BeTrue();
    }

    [Test]
    public void InitialTick_DoesNotExposePublicLifecycleEventsBeforeModsInitialize()
    {
        AndroidLifecycleEventGate.CanRaiseGameLaunched(
            isGameLaunched: false,
            areAllModsInitialized: false,
            isInitialContentLoaded: false
        ).Should().BeFalse();
        AndroidLifecycleEventGate.CanRaisePublicUpdateEvents(isGameLaunched: false).Should().BeFalse();
    }

    [Test]
    public void ModsInitialized_DoesNotExposePublicLifecycleBeforeInitialContentLoadCompletes()
    {
        AndroidLifecycleEventGate.CanRaiseGameLaunched(
            isGameLaunched: false,
            areAllModsInitialized: true,
            isInitialContentLoaded: false
        ).Should().BeFalse();
        AndroidLifecycleEventGate.CanRaisePublicUpdateEvents(isGameLaunched: false).Should().BeFalse();
    }

    [Test]
    public void InitialContentLoaded_RaisesGameLaunchedBeforePublicUpdateEvents()
    {
        AndroidLifecycleEventGate.CanRaiseGameLaunched(
            isGameLaunched: false,
            areAllModsInitialized: true,
            isInitialContentLoaded: true
        ).Should().BeTrue();

        AndroidLifecycleEventGate.CanRaisePublicUpdateEvents(isGameLaunched: true).Should().BeTrue();
    }

    [Test]
    public void LaterTicks_DoNotRaiseGameLaunchedAgain()
    {
        AndroidLifecycleEventGate.CanRaiseGameLaunched(
            isGameLaunched: true,
            areAllModsInitialized: true,
            isInitialContentLoaded: true
        ).Should().BeFalse();
        AndroidLifecycleEventGate.CanRaisePublicUpdateEvents(isGameLaunched: true).Should().BeTrue();
    }

    [Test]
    public void GetPublicTicks_StartsAtZeroWhenTheFirstPublicTickIsDeferred()
    {
        AndroidLifecycleEventGate.GetPublicTicks(ticksElapsed: 1, firstPublicTick: 1).Should().Be(0);
        AndroidLifecycleEventGate.GetPublicTicks(ticksElapsed: 2, firstPublicTick: 1).Should().Be(1);
    }
}
