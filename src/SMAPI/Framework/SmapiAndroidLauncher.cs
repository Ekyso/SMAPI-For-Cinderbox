#if SMAPI_FOR_ANDROID
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using StardewModdingAPI.Mobile;
using StardewModdingAPI.Framework.ModLoading.Rewriters;

namespace StardewModdingAPI.Framework;

/// <summary>Entry point for launching SMAPI on Android.</summary>
public static class SmapiAndroidLauncher
{
    /// <summary>Whether SMAPI has been initialized.</summary>
    public static bool IsInitialized { get; private set; }

    /// <summary>The SMAPI core instance.</summary>
    private static SCore? _core;

    /// <summary>Callback invoked after the Harmony bridge is initialized but before the game runs.</summary>
    public static Action? OnAfterHarmonyBridgeInitialized { get; set; }

    /// <summary>Initialize and launch SMAPI on Android.</summary>
    /// <param name="gameDlls">Directory containing game DLLs.</param>
    /// <param name="smapiInternal">Directory for SMAPI internal files.</param>
    /// <param name="stardewData">Root directory for Stardew Valley data.</param>
    /// <param name="smapiLogs">Directory for SMAPI logs.</param>
    /// <param name="saves">Directory for save files.</param>
    /// <param name="mods">Directory for mods.</param>
    /// <param name="externalRoot">External storage root.</param>
    /// <param name="useAsyncModEvents">Enable concurrent mod event pipeline.</param>
    /// <param name="modEventThreads">Number of threads for mod event pipeline (0 = auto).</param>
    /// <param name="useEventArgsPooling">Enable mod event args pooling.</param>
    /// <param name="enableEventProfiling">Enable profiler for mod event handlers.</param>
    /// <param name="eventProfilingThreshold">Profiler threshold in ms (default 4).</param>
    /// <param name="performanceLogging">Enable performance logging.</param>
    /// <param name="useOptimizedSpriteUpdates">Optimize sprite removal algorithm.</param>
    /// <param name="useOptimizedAnimalUpdates">Reuse cached buffer for animal updates.</param>
    /// <param name="useOptimizedDelayedActions">Positional removal for delayed actions.</param>
    /// <param name="useOptimizedWeatherDrawing">Hoist loop-invariant weather drawing calculations.</param>
    /// <param name="useRawFileCache">Cache decoded PNG and JSON data across invalidation cycles.</param>
    /// <remarks>This should be called instead of directly creating GameRunner. SMAPI creates its own SGameRunner and manages the game lifecycle.</remarks>
    public static void Launch(
        string gameDlls,
        string patchDeps,
        string smapiInternal,
        string stardewData,
        string smapiLogs,
        string saves,
        string mods,
        string externalRoot,
        string gameFiles = "",
        bool isMobile = false,
        bool useAsyncModEvents = true,
        int modEventThreads = 0,
        bool useEventArgsPooling = true,
        bool enableEventProfiling = false,
        int eventProfilingThreshold = 4,
        bool performanceLogging = false,
        bool useOptimizedSpriteUpdates = true,
        bool useOptimizedAnimalUpdates = false,
        bool useOptimizedDelayedActions = false,
        bool useOptimizedWeatherDrawing = false,
        bool useRawFileCache = true
    )
    {
        if (IsInitialized)
        {
            AndroidLogger.Log("[SMAPI] Already initialized, skipping launch");
            return;
        }

        try
        {
            AndroidPaths.Initialize(
                gameDlls,
                patchDeps,
                smapiInternal,
                stardewData,
                smapiLogs,
                saves,
                mods,
                externalRoot,
                gameFiles,
                isMobile
            );

            AndroidPaths.InitializeConfig(
                useAsyncModEvents,
                modEventThreads,
                useEventArgsPooling,
                enableEventProfiling,
                eventProfilingThreshold,
                performanceLogging,
                useOptimizedSpriteUpdates,
                useOptimizedAnimalUpdates,
                useOptimizedDelayedActions,
                useOptimizedWeatherDrawing,
                useRawFileCache
            );

            AndroidLogger.Log("[SMAPI] Starting SMAPI Android Launch");
            AndroidLogger.Log($"[SMAPI] Mods path: {AndroidPaths.Mods}");
            AndroidLogger.Log($"[SMAPI] Game path: {AndroidPaths.GameDllsPath}");
            AndroidLogger.Log($"[SMAPI] Data path: {AndroidPaths.StardewData}");
            AndroidLogger.Log($"[SMAPI] Logs path: {AndroidPaths.SmapiLogs}");

            // initialize Android-specific components
            AndroidPatcher.Setup();

            // apply mobile-specific patches before SCore construction
            if (isMobile)
                AndroidPatcher.ApplyMobilePatches();

            // initialize AssemblyLocationHelper for mod rewriting
            AssemblyLocationHelper.Initialize(AndroidPaths.GameDllsPath, AndroidPaths.Mods);

            // invoke callback for core patch registration
            OnAfterHarmonyBridgeInitialized?.Invoke();

            // track main thread
            AndroidMainThread.Init(Array.Empty<string>());

            // set culture
            Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;

            // extract SMAPI internal files
            ExtractInternalFiles();

            // create SMAPI core
            _core = new SCore(
                modsPath: AndroidPaths.Mods,
                writeToConsole: true,
                overrideDeveloperMode: true
            );
            IsInitialized = true;

            // initialize Android SModHooks
            AndroidSModHooks.Init();

            // verify MonoGame assembly
            VerifyMonoGameAssembly();

            // launch SMAPI
            _core.RunInteractively();
        }
        catch (Exception ex)
        {
            AndroidLogger.Log($"[SMAPI] Launch failed: {ex}");
            throw;
        }
    }

    /// <summary>Extract embedded SMAPI internal files to the smapi-internal folder.</summary>
    private static void ExtractInternalFiles()
    {
        var internalPath = AndroidPaths.SmapiInternal;
        var i18nPath = Path.Combine(internalPath, "i18n");

        Directory.CreateDirectory(internalPath);
        Directory.CreateDirectory(i18nPath);

        var assembly = Assembly.GetExecutingAssembly();
        var allResources = assembly.GetManifestResourceNames();

        ExtractResource(assembly, "SMAPI.config.json", Path.Combine(internalPath, "config.json"));
        ExtractResource(
            assembly,
            "SMAPI.metadata.json",
            Path.Combine(internalPath, "metadata.json")
        );

        int i18nCount = 0;
        foreach (var resourceName in allResources)
        {
            if (resourceName.StartsWith("i18n.") && resourceName.EndsWith(".json"))
            {
                var fileName = resourceName.Substring(5);
                ExtractResource(assembly, resourceName, Path.Combine(i18nPath, fileName));
                i18nCount++;
            }
        }
        AndroidLogger.Log($"[SMAPI] Extracted {i18nCount} i18n files to {internalPath}");
    }

    /// <summary>Extract an embedded resource to a file.</summary>
    private static void ExtractResource(
        Assembly mainAssembly,
        string resourceName,
        string targetPath
    )
    {
        try
        {
            Stream? stream = mainAssembly.GetManifestResourceStream(resourceName);

            if (stream == null)
            {
                string[] satelliteNames = { "StardewModdingAPI.resources", "SMAPI.resources" };
                foreach (var satName in satelliteNames)
                {
                    try
                    {
                        var satelliteAsm = Assembly.Load(satName);
                        stream = satelliteAsm.GetManifestResourceStream(resourceName);
                        if (stream != null)
                            break;
                    }
                    catch { }
                }

                if (stream == null)
                {
                    foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                    {
                        try
                        {
                            foreach (var r in asm.GetManifestResourceNames())
                            {
                                if (r == resourceName || r.EndsWith(resourceName))
                                {
                                    stream = asm.GetManifestResourceStream(r);
                                    if (stream != null)
                                        break;
                                }
                            }
                            if (stream != null)
                                break;
                        }
                        catch { }
                    }
                }
            }

            if (stream == null)
            {
                AndroidLogger.Log($"[SMAPI] Warning: resource '{resourceName}' not found");
                return;
            }

            using var fileStream = File.Create(targetPath);
            stream.CopyTo(fileStream);
            stream.Dispose();
        }
        catch (Exception ex)
        {
            AndroidLogger.Log($"[SMAPI] Error extracting {resourceName}: {ex.Message}");
        }
    }

    /// <summary>Verify that the correct (Android) MonoGame.Framework is loaded at runtime.</summary>
    private static void VerifyMonoGameAssembly()
    {
        try
        {
            var monoGameAsm = AppDomain
                .CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == "MonoGame.Framework");

            if (monoGameAsm == null)
            {
                AndroidLogger.Log("[SMAPI] WARNING: MonoGame.Framework not yet loaded");
                return;
            }

            bool isAndroid =
                monoGameAsm.GetType("Microsoft.Xna.Framework.AndroidGameWindow") != null
                || monoGameAsm.GetType("Microsoft.Xna.Framework.AndroidGameActivity") != null;
            bool isDesktop = monoGameAsm.GetType("Microsoft.Xna.Framework.SdlGameWindow") != null;

            if (isAndroid && !isDesktop)
                AndroidLogger.Log("[SMAPI] MonoGame.Framework: Android variant loaded");
            else if (isDesktop)
                AndroidLogger.Log(
                    "[SMAPI] ERROR: Desktop MonoGame.Framework loaded - this will cause runtime failures"
                );
            else
                AndroidLogger.Log("[SMAPI] WARNING: Could not determine MonoGame platform variant");
        }
        catch (Exception ex)
        {
            AndroidLogger.Log($"[SMAPI] Error verifying MonoGame assembly: {ex.Message}");
        }
    }

    /// <summary>Dispose SMAPI resources.</summary>
    public static void Dispose()
    {
        if (_core != null)
        {
            _core.Dispose();
            _core = null;
            IsInitialized = false;
            AndroidLogger.Log("[SMAPI] Disposed");
        }
    }
}
#endif
