using System;
using System.Reflection;
using System.Text;
using HarmonyLib;
using Microsoft.Xna.Framework.Input;
#if SMAPI_FOR_ANDROID
using Android.Util;
#endif

namespace StardewModdingAPI.Mobile.Patches;

/// <summary>
/// Fixes Lookup Anything on Android. Touch input causes the mod to use cursor-based
/// lookup at the touch position instead of the facing tile. Forces ignoreCursor = true
/// when VirtualGamePad is active so the mod uses its facing-tile detection.
/// </summary>
internal static class LookupAnythingPatch
{
    private const string Tag = "LookupAnythingPatch";

    private static void LogInfo(string message)
    {
#if SMAPI_FOR_ANDROID
        Log.Info(Tag, message);
#else
        System.Diagnostics.Debug.WriteLine($"[{Tag}] {message}");
#endif
    }

    private static void LogError(string message)
    {
#if SMAPI_FOR_ANDROID
        Log.Error(Tag, message);
#else
        System.Diagnostics.Debug.WriteLine($"[{Tag}] ERROR: {message}");
#endif
    }

    /// <summary>Apply the patch if Lookup Anything is loaded.</summary>
    public static void Apply(Harmony harmony)
    {
        try
        {
            Type? modEntryType = null;
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (assembly.GetName().Name == "LookupAnything")
                {
                    modEntryType = assembly.GetType("Pathoschild.Stardew.LookupAnything.ModEntry");
                    break;
                }
            }

            if (modEntryType == null)
            {
                LogInfo("Lookup Anything not found, skipping patch");
                return;
            }

            var getSubject = modEntryType.GetMethod(
                "GetSubject",
                BindingFlags.NonPublic | BindingFlags.Instance,
                null,
                new[] { typeof(StringBuilder), typeof(bool) },
                null
            );

            if (getSubject == null)
            {
                LogError("Could not find ModEntry.GetSubject method");
                return;
            }

            var prefix = typeof(LookupAnythingPatch).GetMethod(
                nameof(GetSubject_Prefix),
                BindingFlags.Public | BindingFlags.Static
            );

            harmony.Patch(getSubject, prefix: new HarmonyMethod(prefix));
            LogInfo("Patched Lookup Anything GetSubject (VirtualGamePad → ignoreCursor)");
        }
        catch (Exception ex)
        {
            LogError($"Failed to patch Lookup Anything: {ex.Message}");
        }
    }

    /// <summary>
    /// Prefix on ModEntry.GetSubject: when VirtualGamePad is active,
    /// force ignoreCursor = true so the mod uses facing-tile lookup.
    /// </summary>
    public static void GetSubject_Prefix(ref bool ignoreCursor)
    {
        if (VirtualGamePad.IsActive)
            ignoreCursor = true;
    }
}
