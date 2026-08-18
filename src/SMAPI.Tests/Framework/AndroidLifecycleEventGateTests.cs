using FluentAssertions;
using NUnit.Framework;
using StardewModdingAPI.Framework;

namespace SMAPI.Tests.Framework;

[TestFixture]
internal class AndroidLifecycleEventGateTests
{
    [Test]
    public void InitialTick_DoesNotExposePublicLifecycleEventsBeforeModsInitialize()
    {
        AndroidLifecycleEventGate.CanRaiseGameLaunched(
            isGameLaunched: false,
            areAllModsInitialized: false
        ).Should().BeFalse();
        AndroidLifecycleEventGate.CanRaisePublicUpdateEvents(isGameLaunched: false).Should().BeFalse();
    }

    [Test]
    public void NextTick_RaisesGameLaunchedBeforePublicUpdateEventsAfterModsInitialize()
    {
        AndroidLifecycleEventGate.CanRaiseGameLaunched(
            isGameLaunched: false,
            areAllModsInitialized: true
        ).Should().BeTrue();

        AndroidLifecycleEventGate.CanRaisePublicUpdateEvents(isGameLaunched: true).Should().BeTrue();
    }

    [Test]
    public void LaterTicks_DoNotRaiseGameLaunchedAgain()
    {
        AndroidLifecycleEventGate.CanRaiseGameLaunched(
            isGameLaunched: true,
            areAllModsInitialized: true
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
