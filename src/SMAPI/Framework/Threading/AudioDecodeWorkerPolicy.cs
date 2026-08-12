using System;

namespace StardewModdingAPI.Framework.Threading;

internal static class AudioDecodeWorkerPolicy
{
    public static int CalculateWorkerCount(int processorCount, bool limitForMemory)
    {
        if (limitForMemory)
            return 1;

        return Math.Clamp(processorCount / 2, 1, 4);
    }
}
