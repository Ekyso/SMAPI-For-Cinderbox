using System.Collections.Generic;

namespace StardewModdingAPI.Framework.Extensions;

/// <summary>Provides internal extensions for <see cref="IMonitor"/>.</summary>
internal static class MonitorExtensions
{
    /// <param name="monitor">The monitor to extend.</param>
    extension(IMonitor monitor)
    {
        /// <summary>Log a message for the player or developer the first time it occurs.</summary>
        /// <param name="hash">The hash of logged messages.</param>
        /// <param name="message">The message to log.</param>
        /// <param name="level">The log severity level.</param>
        public void LogOnce(HashSet<string> hash, string message, LogLevel level = LogLevel.Trace)
        {
            if (!hash.Contains(message))
            {
                monitor.Log(message, level);
                hash.Add(message);
            }
        }
    }
}
