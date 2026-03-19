#if SMAPI_FOR_ANDROID
using System;
using System.Reflection;
using Android.Util;
using HarmonyLib;
using StardewValley.Mods;

namespace StardewModdingAPI.Mobile.Patches;

/// <summary>
/// Patches ModDataDictionary indexer to return null for missing keys instead of throwing.
/// Many mods read modData[key] without ContainsKey checks, causing crashes on Android.
/// </summary>
internal static class ModDataSafeAccessPatch
{
    private const string Tag = "ModDataSafeAccessPatch";

    private static void LogInfo(string message)
    {
        Log.Info(Tag, message);
    }

    private static void LogError(string message)
    {
        Log.Error(Tag, message);
    }

    public static void Apply(Harmony harmony)
    {
        try
        {
            var getItem = typeof(ModDataDictionary).GetMethod("get_Item", new[] { typeof(string) });
            if (getItem == null)
            {
                LogError("Could not find ModDataDictionary.get_Item method");
                return;
            }

            // Harmony requires patching the declaring type, not an inherited reference
            if (getItem.DeclaringType != typeof(ModDataDictionary))
            {
                var declaringType = getItem.DeclaringType;
                LogInfo($"get_Item declared on {declaringType?.Name}, resolving from declaring type");
                getItem = declaringType!.GetMethod("get_Item", new[] { typeof(string) });
                if (getItem == null)
                {
                    LogError("Could not resolve get_Item from declaring type");
                    return;
                }
            }

            var prefix = typeof(ModDataSafeAccessPatch).GetMethod(
                nameof(GetItem_Prefix),
                BindingFlags.NonPublic | BindingFlags.Static
            );

            harmony.Patch(getItem, prefix: new HarmonyMethod(prefix));
            LogInfo("Patched ModDataDictionary.get_Item (return null for missing keys)");
        }
        catch (Exception ex)
        {
            LogError($"Failed to patch ModDataDictionary: {ex.Message}");
        }
    }

    /// <summary>
    /// Prefix on ModDataDictionary's inherited get_Item: return null for missing keys
    /// instead of throwing KeyNotFoundException.
    /// </summary>
    private static bool GetItem_Prefix(object __instance, string key, ref string __result)
    {
        if (__instance is ModDataDictionary modData && !modData.ContainsKey(key))
        {
            __result = null!;
            return false;
        }
        return true;
    }
}
#endif
