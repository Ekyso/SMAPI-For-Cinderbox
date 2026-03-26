using System;

namespace StardewModdingAPI.Framework.Extensions;

/// <summary>Provides internal extensions for <see cref="IModMetadata"/>.</summary>
internal static class ModMetadataExtensions
{
    /// <param name="metadata">The mod metadata to extend.</param>
    extension(IModMetadata metadata)
    {
        /// <summary>Log a message using the mod's monitor.</summary>
        /// <param name="message">The message to log.</param>
        /// <param name="level">The log severity level.</param>
        public void LogAsMod(string message, LogLevel level = LogLevel.Trace)
        {
            if (metadata.Monitor is null)
                throw new InvalidOperationException($"Can't log as mod {metadata.DisplayName}: mod is broken or a content pack. Logged message:\n[{level}] {message}");

            metadata.Monitor.Log(message, level);
        }

        /// <summary>Log a message using the mod's monitor, but only if it hasn't already been logged since the last game launch.</summary>
        /// <param name="message">The message to log.</param>
        /// <param name="level">The log severity level.</param>
        public void LogAsModOnce(string message, LogLevel level = LogLevel.Trace)
        {
            metadata.Monitor?.LogOnce(message, level);
        }
    }
}
