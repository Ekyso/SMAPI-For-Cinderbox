using System.Diagnostics;

namespace StardewModdingAPI.Framework.Events;

internal static class EventProfilingTimer
{
    public static long GetElapsedMilliseconds(long startTimestamp)
    {
        return GetElapsedMilliseconds(
            startTimestamp,
            Stopwatch.GetTimestamp(),
            Stopwatch.Frequency
        );
    }

    internal static long GetElapsedMilliseconds(
        long startTimestamp,
        long endTimestamp,
        long frequency
    )
    {
        return (long)((endTimestamp - startTimestamp) * 1000d / frequency);
    }
}
