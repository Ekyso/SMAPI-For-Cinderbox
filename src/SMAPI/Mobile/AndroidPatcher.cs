using System;
using System.Reflection;
using HarmonyLib;
#if SMAPI_FOR_ANDROID
using StardewModdingAPI.Mobile.Patches;
#endif

namespace StardewModdingAPI.Mobile;

internal static class AndroidPatcher
{
    private const string Tag = "AndroidPatcher";

    public static Harmony? Harmony { get; private set; }

    internal static void Setup()
    {
        LogInfo("Setup starting...");

        try
        {
            Harmony = new Harmony(nameof(AndroidPatcher));
            PatchOperatingSystemChecks();
            PatchConsoleForegroundColor();
            LogInfo("Setup complete");
        }
        catch (Exception ex)
        {
            LogError($"Setup failed: {ex}");
            throw;
        }
    }

    internal static void ApplyMobilePatches()
    {
        if (Harmony == null)
        {
            LogError("Cannot apply mobile patches because Harmony is not initialized");
            return;
        }

        try
        {
#if SMAPI_FOR_ANDROID
            MenuWithInventoryMobilePatches.Apply(Harmony);
            ShopMenuTabMobilePatches.Apply(Harmony);
            CraftingPageMobilePatches.Apply(Harmony);
#endif
        }
        catch (Exception ex)
        {
            LogError($"Failed to apply mobile facade patches: {ex}");
        }
    }

    internal static void ApplyGameInitializedPatches()
    {
        if (Harmony == null)
        {
            LogError("Cannot apply performance patches because Harmony is not initialized");
            return;
        }

        try
        {
#if SMAPI_FOR_ANDROID
            ParallelAudioLoadPatch.Apply(Harmony);
#endif
        }
        catch (Exception ex)
        {
            LogError($"Error applying performance patches: {ex}");
        }
    }

    private static void PatchOperatingSystemChecks()
    {
        PatchOperatingSystemCheck("IsAndroid", nameof(ReturnFalse_Prefix));
        PatchOperatingSystemCheck("IsLinux", nameof(ReturnTrue_Prefix));
        PatchOperatingSystemCheck("IsWindows", nameof(ReturnFalse_Prefix));
        PatchOperatingSystemCheck("IsMacOS", nameof(ReturnFalse_Prefix));
    }

    private static void PatchOperatingSystemCheck(string methodName, string prefixName)
    {
        var method = typeof(OperatingSystem).GetMethod(
            methodName,
            BindingFlags.Public | BindingFlags.Static
        );
        var prefix = typeof(AndroidPatcher).GetMethod(
            prefixName,
            BindingFlags.NonPublic | BindingFlags.Static
        );
        if (method != null && prefix != null)
            Harmony!.Patch(method, prefix: new HarmonyMethod(prefix));
    }

    private static void PatchConsoleForegroundColor()
    {
        var property = typeof(Console).GetProperty(
            "ForegroundColor",
            BindingFlags.Public | BindingFlags.Static
        );

        var getter = property?.GetGetMethod();
        if (getter != null)
        {
            Harmony!.Patch(
                getter,
                prefix: new HarmonyMethod(
                    typeof(AndroidPatcher),
                    nameof(ConsoleForegroundColor_Get_Prefix)
                )
            );
        }

        var setter = property?.GetSetMethod();
        if (setter != null)
        {
            Harmony!.Patch(
                setter,
                prefix: new HarmonyMethod(
                    typeof(AndroidPatcher),
                    nameof(ReturnFalse_VoidPrefix)
                )
            );
        }
    }

    private static bool ConsoleForegroundColor_Get_Prefix(ref ConsoleColor __result)
    {
        __result = ConsoleColor.Gray;
        return false;
    }

    private static bool ReturnFalse_VoidPrefix()
    {
        return false;
    }

    private static bool ReturnFalse_Prefix(ref bool __result)
    {
        __result = false;
        return false;
    }

    private static bool ReturnTrue_Prefix(ref bool __result)
    {
        __result = true;
        return false;
    }

    private static void LogInfo(string message)
    {
#if SMAPI_FOR_ANDROID
        global::Android.Util.Log.Info(Tag, message);
#else
        System.Diagnostics.Debug.WriteLine($"[{Tag}] {message}");
#endif
    }

    private static void LogError(string message)
    {
#if SMAPI_FOR_ANDROID
        global::Android.Util.Log.Error(Tag, message);
#else
        System.Diagnostics.Debug.WriteLine($"[{Tag}] ERROR: {message}");
#endif
    }
}
