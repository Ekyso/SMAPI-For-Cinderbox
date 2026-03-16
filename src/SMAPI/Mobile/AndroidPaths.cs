#if SMAPI_FOR_ANDROID
using System.IO;

namespace StardewModdingAPI.Mobile;

/// <summary>
/// Stores Android-specific paths and config passed from the launcher.
/// These values are set by Iridium.Android before SMAPI starts.
/// </summary>
internal static class AndroidPaths
{
    /// <summary>Whether paths have been initialized.</summary>
    public static bool IsInitialized { get; private set; }

    /// <summary>Directory containing the game DLLs (Stardew Valley.dll, etc.).</summary>
    public static string DesktopDlls { get; private set; } = string.Empty;

    /// <summary>Directory for SMAPI internal files (config, metadata, i18n).</summary>
    public static string SmapiInternal { get; private set; } = string.Empty;

    /// <summary>Root directory for Stardew Valley data.</summary>
    public static string StardewData { get; private set; } = string.Empty;

    /// <summary>Directory for SMAPI error logs.</summary>
    public static string SmapiLogs { get; private set; } = string.Empty;

    /// <summary>Directory for save files.</summary>
    public static string Saves { get; private set; } = string.Empty;

    /// <summary>Directory for mods.</summary>
    public static string Mods { get; private set; } = string.Empty;

    /// <summary>External storage root (/storage/emulated/0/StardewValley).</summary>
    public static string ExternalRoot { get; private set; } = string.Empty;

    /// <summary>Enable concurrent event pipeline for mod event processing.</summary>
    public static bool UseAsyncModEvents { get; private set; } = true;

    /// <summary>Number of threads for the mod event pipeline (0 = auto).</summary>
    public static int ModEventThreads { get; private set; } = 0;

    /// <summary>Enable object pooling for mod event args to reduce GC pressure.</summary>
    public static bool UseEventArgsPooling { get; private set; } = true;

    /// <summary>Enable performance metrics logging.</summary>
    public static bool PerformanceLogging { get; private set; } = false;

    /// <summary>Optimize sprite removal algorithm in location updates.</summary>
    public static bool UseOptimizedSpriteUpdates { get; private set; } = true;

    /// <summary>Reuse cached buffer for animal updates instead of per-frame ToArray().</summary>
    public static bool UseOptimizedAnimalUpdates { get; private set; } = false;

    /// <summary>Positional removal for delayed actions instead of Contains+Remove.</summary>
    public static bool UseOptimizedDelayedActions { get; private set; } = false;

    /// <summary>Hoist loop-invariant calculations in weather drawing.</summary>
    public static bool UseOptimizedWeatherDrawing { get; private set; } = false;

    /// <summary>Cache decoded PNG/JSON/OGG data across invalidation cycles.</summary>
    public static bool UseRawFileCache { get; private set; } = true;

    /// <summary>
    /// Initialize paths. Called by SmapiAndroidLauncher with values from Iridium.Android.
    /// </summary>
    public static void Initialize(
        string desktopDlls,
        string smapiInternal,
        string stardewData,
        string smapiLogs,
        string saves,
        string mods,
        string externalRoot)
    {
        DesktopDlls = desktopDlls;
        SmapiInternal = smapiInternal;
        StardewData = stardewData;
        SmapiLogs = smapiLogs;
        Saves = saves;
        Mods = mods;
        ExternalRoot = externalRoot;
        IsInitialized = true;
    }

    /// <summary>
    /// Initialize performance config. Called by SmapiAndroidLauncher with values from IridiumConfig.
    /// </summary>
    public static void InitializeConfig(
        bool useAsyncModEvents,
        int modEventThreads,
        bool useEventArgsPooling,
        bool performanceLogging,
        bool useOptimizedSpriteUpdates,
        bool useOptimizedAnimalUpdates,
        bool useOptimizedDelayedActions,
        bool useOptimizedWeatherDrawing,
        bool useRawFileCache)
    {
        UseAsyncModEvents = useAsyncModEvents;
        ModEventThreads = modEventThreads;
        UseEventArgsPooling = useEventArgsPooling;
        PerformanceLogging = performanceLogging;
        UseOptimizedSpriteUpdates = useOptimizedSpriteUpdates;
        UseOptimizedAnimalUpdates = useOptimizedAnimalUpdates;
        UseOptimizedDelayedActions = useOptimizedDelayedActions;
        UseOptimizedWeatherDrawing = useOptimizedWeatherDrawing;
        UseRawFileCache = useRawFileCache;
    }

    /// <summary>Get the full path for a game DLL.</summary>
    public static string GetDesktopDllPath(string dllName)
    {
        return Path.Combine(DesktopDlls, dllName);
    }
}
#endif
