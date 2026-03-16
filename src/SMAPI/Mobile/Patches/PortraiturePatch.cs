using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Microsoft.Xna.Framework.Graphics;
#if SMAPI_FOR_ANDROID
using Android.Util;
#endif

namespace StardewModdingAPI.Mobile.Patches;

/// <summary>
/// Caches Portraiture mod's HDP portrait textures to avoid re-creating GPU textures
/// every frame. Also invalidates cache when the user cycles portrait folders.
/// </summary>
internal static class PortraiturePatch
{
    private const string Tag = "PortraiturePatch";

    /// <summary>Cache of portrait textures keyed by NPC name.</summary>
    private static readonly Dictionary<string, Texture2D> PortraitCache = new();

    /// <summary>Type of ScaledTexture2D from Portraiture mod, resolved at patch time.</summary>
    private static Type? _scaledTexture2DType;

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

    /// <summary>Apply the patch if Portraiture mod is loaded.</summary>
    public static void Apply(Harmony harmony)
    {
        try
        {
            Type? textureLoaderType = null;
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (assembly.GetName().Name == "Portraiture")
                {
                    textureLoaderType = assembly.GetType("Portraiture.TextureLoader");
                    _scaledTexture2DType = assembly.GetType("Portraiture.ScaledTexture2D");
                    break;
                }
            }

            if (textureLoaderType == null)
            {
                LogInfo("Portraiture not found, skipping patch");
                return;
            }

            if (_scaledTexture2DType == null)
            {
                LogError("Found Portraiture but could not find ScaledTexture2D type");
                return;
            }

            var getPortrait = textureLoaderType.GetMethod(
                "getPortrait",
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[] { typeof(StardewValley.NPC), typeof(Texture2D) },
                null
            );

            if (getPortrait == null)
            {
                LogError("Could not find TextureLoader.getPortrait method");
                return;
            }

            harmony.Patch(
                getPortrait,
                prefix: new HarmonyMethod(typeof(PortraiturePatch), nameof(GetPortrait_Prefix)),
                postfix: new HarmonyMethod(typeof(PortraiturePatch), nameof(GetPortrait_Postfix))
            );
            LogInfo("Patched TextureLoader.getPortrait (HDP portrait caching)");

            var nextFolder = textureLoaderType.GetMethod(
                "nextFolder",
                BindingFlags.Public | BindingFlags.Static | BindingFlags.NonPublic,
                null,
                Type.EmptyTypes,
                null
            );

            if (nextFolder != null)
            {
                harmony.Patch(
                    nextFolder,
                    postfix: new HarmonyMethod(typeof(PortraiturePatch), nameof(NextFolder_Postfix))
                );
                LogInfo("Patched TextureLoader.nextFolder (cache invalidation)");
            }
            else
            {
                LogError("Could not find TextureLoader.nextFolder - cache won't invalidate on folder change");
            }
        }
        catch (Exception ex)
        {
            LogError($"Failed to patch Portraiture: {ex.Message}");
        }
    }

    /// <summary>
    /// Prefix on TextureLoader.getPortrait: if we have a cached texture for this NPC,
    /// return it directly and skip the original method (avoids per-frame GetData calls).
    /// </summary>
    public static bool GetPortrait_Prefix(StardewValley.NPC npc, ref Texture2D __result)
    {
        if (npc?.Name == null)
            return true; // run original

        if (PortraitCache.TryGetValue(npc.Name, out var cached) && cached != null && !cached.IsDisposed)
        {
            __result = cached;
            return false; // skip original
        }

        return true; // run original
    }

    /// <summary>
    /// Postfix on TextureLoader.getPortrait: if the original returned a ScaledTexture2D,
    /// cache it so future calls don't re-create the texture.
    /// </summary>
    public static void GetPortrait_Postfix(StardewValley.NPC npc, Texture2D __result)
    {
        if (npc?.Name == null || __result == null)
            return;

        if (_scaledTexture2DType != null && _scaledTexture2DType.IsInstanceOfType(__result))
        {
            PortraitCache[npc.Name] = __result;
        }
    }

    /// <summary>
    /// Postfix on TextureLoader.nextFolder: clear the portrait cache when the user
    /// cycles to a different portrait folder (different portrait pack).
    /// </summary>
    public static void NextFolder_Postfix()
    {
        PortraitCache.Clear();
        LogInfo("Portrait cache cleared (folder changed)");
    }

    /// <summary>Clear the portrait cache on return to title screen.</summary>
    public static void ClearCache()
    {
        if (PortraitCache.Count > 0)
        {
            LogInfo($"Portrait cache cleared on title return ({PortraitCache.Count} entries)");
            PortraitCache.Clear();
        }
    }
}
