#if SMAPI_FOR_ANDROID
using System;
using System.IO;

namespace StardewModdingAPI.Mobile;

/// <summary>Reads process memory metrics from /proc/self/status and GC for diagnostic logging.</summary>
internal static class MemoryDiagnostics
{
    /// <summary>Get a formatted memory snapshot string suitable for logging.</summary>
    /// <param name="label">A label identifying when the snapshot was taken.</param>
    public static string Snapshot(string label)
    {
        var (vmRss, vmSize, rssAnon, vmSwap) = ReadProcMemory();
        long gcManaged = GC.GetTotalMemory(false);
        return $"{label}: GC={gcManaged / 1048576}MB, RSS={vmRss / 1024}MB, " +
               $"RSSAnon={rssAnon / 1024}MB, VmSize={vmSize / 1024}MB, VmSwap={vmSwap / 1024}MB";
    }

    /// <summary>Read key memory metrics from /proc/self/status.</summary>
    /// <returns>Tuple of (VmRSS, VmSize, RssAnon, VmSwap) in KB. -1 if unavailable.</returns>
    public static (long vmRssKB, long vmSizeKB, long rssAnonKB, long vmSwapKB) ReadProcMemory()
    {
        long vmRss = -1, vmSize = -1, rssAnon = -1, vmSwap = -1;
        try
        {
            foreach (var line in File.ReadLines("/proc/self/status"))
            {
                if (line.StartsWith("VmRSS:"))
                    vmRss = ParseKB(line);
                else if (line.StartsWith("VmSize:"))
                    vmSize = ParseKB(line);
                else if (line.StartsWith("RssAnon:"))
                    rssAnon = ParseKB(line);
                else if (line.StartsWith("VmSwap:"))
                    vmSwap = ParseKB(line);
            }
        }
        catch { }
        return (vmRss, vmSize, rssAnon, vmSwap);
    }

    private static long ParseKB(string line)
    {
        var parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 2 && long.TryParse(parts[1], out long val))
            return val;
        return -1;
    }
}
#endif
