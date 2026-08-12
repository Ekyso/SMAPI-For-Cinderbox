using NUnit.Framework;
using StardewModdingAPI.Framework.Events;

namespace SMAPI.Tests.Core;

[TestFixture]
internal class EventProfilingTimerTests
{
    [TestCase(0, 4, 1_000, ExpectedResult = 4)]
    [TestCase(10_000, 10_001, 1_000, ExpectedResult = 1)]
    [TestCase(20, 25, 2_500, ExpectedResult = 2)]
    public long GetElapsedMilliseconds_ConvertsStopwatchTicks(
        long startTimestamp,
        long endTimestamp,
        long frequency
    )
    {
        return EventProfilingTimer.GetElapsedMilliseconds(
            startTimestamp,
            endTimestamp,
            frequency
        );
    }
}
