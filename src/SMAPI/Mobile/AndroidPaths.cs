#if SMAPI_FOR_ANDROID
using System;
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

    /// <summary>Whether running mobile Stardew Valley (vs desktop).</summary>
    public static bool IsMobile { get; private set; }

    /// <summary>Directory containing the game DLLs (Stardew Valley.dll, etc.).</summary>
    private static string _gameDllsPath = string.Empty;
    public static string GameDllsPath
    {
        get
        {
            ThrowIfNotInitialized();
            return _gameDllsPath;
        }
        private set => _gameDllsPath = value;
    }

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

    /// <summary>Game files directory (DLLs + Content). May be internal storage for mobile.</summary>
    public static string GameFiles { get; private set; } = string.Empty;

    /// <summary>Directory containing patch dependencies (BCL facades, reference assemblies) for Mono.Cecil type resolution.</summary>
    public static string PatchDeps { get; private set; } = string.Empty;

    /// <summary>Enable concurrent event pipeline for mod event processing.</summary>
    public static bool UseAsyncModEvents { get; private set; } = true;

    /// <summary>Number of threads for the mod event pipeline (0 = auto).</summary>
    public static int ModEventThreads { get; private set; } = 0;

    /// <summary>Enable object pooling for mod event args to reduce GC pressure.</summary>
    public static bool UseEventArgsPooling { get; private set; } = true;

    /// <summary>Enable profiler for mod event handlers.</summary>
    public static bool EnableEventProfiling { get; private set; } = false;

    /// <summary>Profiler threshold in ms, warnings are logged for handlers taking longer.</summary>
    public static int EventProfilingThreshold { get; private set; } = 4;

    /// <summary>Enable performance metrics logging.</summary>
    public static bool PerformanceLogging { get; private set; } = false;

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
        string gameDlls,
        string patchDeps,
        string smapiInternal,
        string stardewData,
        string smapiLogs,
        string saves,
        string mods,
        string externalRoot,
        string gameFiles,
        bool isMobile = false
    )
    {
        if (string.IsNullOrEmpty(gameDlls))
            throw new ArgumentException("gameDlls path is required", nameof(gameDlls));
        if (string.IsNullOrEmpty(smapiInternal))
            throw new ArgumentException("smapiInternal path is required", nameof(smapiInternal));

        GameDllsPath = gameDlls;
        PatchDeps = patchDeps ?? "";
        SmapiInternal = smapiInternal;
        StardewData = stardewData ?? "";
        SmapiLogs = smapiLogs ?? "";
        Saves = saves ?? "";
        Mods = mods ?? "";
        ExternalRoot = externalRoot ?? "";
        GameFiles = gameFiles ?? "";
        IsMobile = isMobile;
        IsInitialized = true;
    }

    internal static void ThrowIfNotInitialized()
    {
        if (!IsInitialized)
            throw new InvalidOperationException(
                "AndroidPaths.Initialize() must be called before accessing path properties."
            );
    }

    /// <summary>
    /// Initialize performance config. Called by SmapiAndroidLauncher with values from IridiumConfig.
    /// </summary>
    public static void InitializeConfig(
        bool useAsyncModEvents,
        int modEventThreads,
        bool useEventArgsPooling,
        bool enableEventProfiling,
        int eventProfilingThreshold,
        bool performanceLogging,
        bool useOptimizedAnimalUpdates,
        bool useOptimizedDelayedActions,
        bool useOptimizedWeatherDrawing,
        bool useRawFileCache
    )
    {
        UseAsyncModEvents = useAsyncModEvents;
        ModEventThreads = modEventThreads;
        UseEventArgsPooling = useEventArgsPooling;
        EnableEventProfiling = enableEventProfiling;
        EventProfilingThreshold = eventProfilingThreshold;
        PerformanceLogging = performanceLogging;
        UseOptimizedAnimalUpdates = useOptimizedAnimalUpdates;
        UseOptimizedDelayedActions = useOptimizedDelayedActions;
        UseOptimizedWeatherDrawing = useOptimizedWeatherDrawing;
        UseRawFileCache = useRawFileCache;
    }
}
#endif
