using System.Reflection;

namespace StardewModdingAPI.Framework.Events;

/// <summary>Formats diagnostics for slow mod event handlers.</summary>
internal static class EventProfilingWarningFormatter
{
    /// <summary>Format a slow-handler warning with enough metadata to identify the exact registration.</summary>
    /// <param name="modDisplayName">The mod's display name.</param>
    /// <param name="modId">The mod's unique ID.</param>
    /// <param name="method">The registered delegate method.</param>
    /// <param name="eventName">The event being raised.</param>
    /// <param name="elapsedMilliseconds">The handler duration in milliseconds.</param>
    /// <param name="warningThreshold">The configured warning threshold in milliseconds.</param>
    public static string Format(
        string modDisplayName,
        string modId,
        MethodInfo method,
        string eventName,
        long elapsedMilliseconds,
        int warningThreshold
    )
    {
        string declaringType =
            method.DeclaringType?.FullName?.Replace('+', '.') ?? "<unknown type>";
        string handlerName = $"{declaringType}.{method.Name}";

        return $"The '{modDisplayName}' mod ({modId}) event handler '{handlerName}' for the {eventName} event took {elapsedMilliseconds}ms, which exceeds the {warningThreshold}ms warning threshold. This may cause performance issues or frame stutters.";
    }
}
