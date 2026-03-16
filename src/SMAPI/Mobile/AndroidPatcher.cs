using System;
using System.Reflection;
using HarmonyLib;
#if SMAPI_FOR_ANDROID
using Android.Util;
using StardewModdingAPI.Mobile.Patches;
#endif

namespace StardewModdingAPI.Mobile;

/// <summary>
/// Manages Harmony for Android runtime.
/// </summary>
internal static class AndroidPatcher
{
    private const string Tag = "AndroidPatcher";

    public static Harmony? Harmony { get; private set; }

    /// <summary>Log a message to Android logcat or debug output.</summary>
    private static void LogInfo(string message)
    {
#if SMAPI_FOR_ANDROID
        Log.Info(Tag, message);
#else
        System.Diagnostics.Debug.WriteLine($"[{Tag}] {message}");
#endif
    }

    /// <summary>Log an error to Android logcat or debug output.</summary>
    private static void LogError(string message)
    {
#if SMAPI_FOR_ANDROID
        Log.Error(Tag, message);
#else
        System.Diagnostics.Debug.WriteLine($"[{Tag}] ERROR: {message}");
#endif
    }

    /// <summary>Initialize the Android patcher. Called at program entry point.</summary>
    internal static void Setup()
    {
        LogInfo("Setup starting...");

        try
        {
            Harmony = new Harmony(nameof(AndroidPatcher));

            // report as Linux so PC mods use the correct code paths
            PatchOperatingSystemChecks();

            // prevent Console.ForegroundColor from throwing on Android
            PatchConsoleForegroundColor();

            LogInfo("Setup complete");
        }
        catch (Exception ex)
        {
            LogError($"Setup failed: {ex}");
            throw;
        }
    }

    /// <summary>
    /// Patch OperatingSystem platform checks for PC compatibility.
    /// Without this, mods detect Android and try to patch mobile-only methods
    /// like IClickableMenu.drawMobileToolTip which don't exist in the PC DLLs.
    /// </summary>
    private static void PatchOperatingSystemChecks()
    {
        var isAndroidMethod = typeof(OperatingSystem).GetMethod(
            "IsAndroid",
            BindingFlags.Public | BindingFlags.Static
        );
        if (isAndroidMethod != null)
        {
            var prefix = typeof(AndroidPatcher).GetMethod(
                nameof(ReturnFalse_Prefix),
                BindingFlags.NonPublic | BindingFlags.Static
            );
            Harmony!.Patch(isAndroidMethod, prefix: new HarmonyMethod(prefix));
        }

        var isLinuxMethod = typeof(OperatingSystem).GetMethod(
            "IsLinux",
            BindingFlags.Public | BindingFlags.Static
        );
        if (isLinuxMethod != null)
        {
            var prefix = typeof(AndroidPatcher).GetMethod(
                nameof(ReturnTrue_Prefix),
                BindingFlags.NonPublic | BindingFlags.Static
            );
            Harmony!.Patch(isLinuxMethod, prefix: new HarmonyMethod(prefix));
        }

        var isWindowsMethod = typeof(OperatingSystem).GetMethod(
            "IsWindows",
            BindingFlags.Public | BindingFlags.Static
        );
        if (isWindowsMethod != null)
        {
            var prefix = typeof(AndroidPatcher).GetMethod(
                nameof(ReturnFalse_Prefix),
                BindingFlags.NonPublic | BindingFlags.Static
            );
            Harmony!.Patch(isWindowsMethod, prefix: new HarmonyMethod(prefix));
        }

        var isMacOSMethod = typeof(OperatingSystem).GetMethod(
            "IsMacOS",
            BindingFlags.Public | BindingFlags.Static
        );
        if (isMacOSMethod != null)
        {
            var prefix = typeof(AndroidPatcher).GetMethod(
                nameof(ReturnFalse_Prefix),
                BindingFlags.NonPublic | BindingFlags.Static
            );
            Harmony!.Patch(isMacOSMethod, prefix: new HarmonyMethod(prefix));
        }
    }

    /// <summary>
    /// Apply performance patches after the game is loaded.
    /// Called from SCore.InitializePerformanceFeatures once game DLLs are available.
    /// </summary>
    internal static void ApplyPerformancePatches(
        bool useOptimizedSpriteUpdates,
        bool useOptimizedAnimalUpdates,
        bool useOptimizedDelayedActions,
        bool useOptimizedWeatherDrawing)
    {
        if (Harmony == null)
        {
            LogError("Cannot apply performance patches - Harmony not initialized");
            return;
        }

        try
        {
            if (useOptimizedSpriteUpdates)
            {
                SpriteUpdateOptimizationPatch.Apply(Harmony);
            }

            if (useOptimizedAnimalUpdates)
            {
                AnimalUpdateOptimizationPatch.Apply(Harmony);
            }

            if (useOptimizedDelayedActions)
            {
                DelayedActionOptimizationPatch.Apply(Harmony);
            }

            if (useOptimizedWeatherDrawing)
            {
                WeatherDrawOptimizationPatch.Apply(Harmony);
            }

#if SMAPI_FOR_ANDROID
            ParallelAudioLoadPatch.Apply(Harmony);
            ModDataSafeAccessPatch.Apply(Harmony);
#endif
        }
        catch (Exception ex)
        {
            LogError($"Error applying performance patches: {ex}");
        }
    }

    /// <summary>
    /// Patch Console.ForegroundColor getter/setter to no-op on Android.
    /// The getter returns ConsoleColor.Gray; the setter does nothing.
    /// </summary>
    private static void PatchConsoleForegroundColor()
    {
        var consoleType = typeof(Console);

        var getter = consoleType.GetProperty("ForegroundColor", BindingFlags.Public | BindingFlags.Static)?.GetGetMethod();
        if (getter != null)
        {
            var prefix = typeof(AndroidPatcher).GetMethod(nameof(ConsoleForegroundColor_Get_Prefix), BindingFlags.NonPublic | BindingFlags.Static);
            Harmony!.Patch(getter, prefix: new HarmonyMethod(prefix));
        }

        var setter = consoleType.GetProperty("ForegroundColor", BindingFlags.Public | BindingFlags.Static)?.GetSetMethod();
        if (setter != null)
        {
            var prefix = typeof(AndroidPatcher).GetMethod(nameof(ReturnFalse_VoidPrefix), BindingFlags.NonPublic | BindingFlags.Static);
            Harmony!.Patch(setter, prefix: new HarmonyMethod(prefix));
        }
    }

    /// <summary>Prefix for Console.ForegroundColor getter - return ConsoleColor.Gray.</summary>
    private static bool ConsoleForegroundColor_Get_Prefix(ref ConsoleColor __result)
    {
        __result = ConsoleColor.Gray;
        return false;
    }

    /// <summary>Prefix that skips original void method.</summary>
    private static bool ReturnFalse_VoidPrefix() => false;

    /// <summary>Prefix that returns false and skips original method.</summary>
    private static bool ReturnFalse_Prefix(ref bool __result)
    {
        __result = false;
        return false;
    }

    /// <summary>Prefix that returns true and skips original method.</summary>
    private static bool ReturnTrue_Prefix(ref bool __result)
    {
        __result = true;
        return false;
    }
}
