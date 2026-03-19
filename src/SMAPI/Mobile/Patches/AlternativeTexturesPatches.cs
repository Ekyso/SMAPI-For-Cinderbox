#if SMAPI_FOR_ANDROID
using System;
using System.Reflection;
using Android.Util;
using HarmonyLib;
using Microsoft.Xna.Framework;
using StardewValley.Menus;

namespace StardewModdingAPI.Mobile.Patches;

/// <summary>
/// Fixes AlternativeTextures search boxes on Android. The mod's menus pre-select
/// the search box in their constructors, which prevents the Android soft keyboard
/// from activating on tap. This deselects on construction and re-selects on tap.
/// </summary>
internal static class AlternativeTexturesPatches
{
    private const string Tag = "AltTexturesPatches";

    private static FieldInfo _catalogueSearchBox = null!;
    private static FieldInfo _paintBucketSearchBox = null!;

    public static void Apply(Harmony harmony)
    {
        try
        {
            Assembly? assembly = null;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (asm.GetName().Name == "AlternativeTextures")
                {
                    assembly = asm;
                    break;
                }
            }

            if (assembly == null)
            {
                Log.Info(Tag, "AlternativeTextures not found, skipping patches");
                return;
            }

            PatchCatalogueMenu(harmony, assembly);
            PatchPaintBucketMenu(harmony, assembly);
        }
        catch (Exception ex)
        {
            Log.Error(Tag, $"Failed to patch AlternativeTextures: {ex.Message}");
        }
    }

    private static void PatchCatalogueMenu(Harmony harmony, Assembly assembly)
    {
        try
        {
            var menuType = assembly.GetType("AlternativeTextures.Framework.UI.CatalogueMenu");
            if (menuType == null)
            {
                Log.Info(Tag, "CatalogueMenu type not found, skipping");
                return;
            }

            _catalogueSearchBox = menuType.GetField(
                "_searchBox",
                BindingFlags.NonPublic | BindingFlags.Instance
            )!;
            if (_catalogueSearchBox == null)
            {
                Log.Error(Tag, "CatalogueMenu._searchBox field not found");
                return;
            }

            var ctor = menuType.GetConstructors()[0];
            harmony.Patch(ctor,
                postfix: new HarmonyMethod(typeof(AlternativeTexturesPatches),
                    nameof(CatalogueMenu_Ctor_Postfix)));

            var clickMethod = menuType.GetMethod(
                "receiveLeftClick",
                BindingFlags.Public | BindingFlags.Instance,
                null,
                new[] { typeof(int), typeof(int), typeof(bool) },
                null
            );
            if (clickMethod != null)
            {
                harmony.Patch(clickMethod,
                    postfix: new HarmonyMethod(typeof(AlternativeTexturesPatches),
                        nameof(CatalogueMenu_ReceiveLeftClick_Postfix)));
            }

            Log.Info(Tag, "Patched CatalogueMenu (constructor + receiveLeftClick)");
        }
        catch (Exception ex)
        {
            Log.Error(Tag, $"Failed to patch CatalogueMenu: {ex.Message}");
        }
    }

    private static void PatchPaintBucketMenu(Harmony harmony, Assembly assembly)
    {
        try
        {
            var menuType = assembly.GetType("AlternativeTextures.Framework.UI.PaintBucketMenu");
            if (menuType == null)
            {
                Log.Info(Tag, "PaintBucketMenu type not found, skipping");
                return;
            }

            _paintBucketSearchBox = menuType.GetField(
                "_searchBox",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
            )!;
            if (_paintBucketSearchBox == null)
            {
                Log.Error(Tag, "PaintBucketMenu._searchBox field not found");
                return;
            }

            var ctor = menuType.GetConstructors()[0];
            harmony.Patch(ctor,
                postfix: new HarmonyMethod(typeof(AlternativeTexturesPatches),
                    nameof(PaintBucketMenu_Ctor_Postfix)));

            var clickMethod = menuType.GetMethod(
                "receiveLeftClick",
                BindingFlags.Public | BindingFlags.Instance,
                null,
                new[] { typeof(int), typeof(int), typeof(bool) },
                null
            );
            if (clickMethod != null)
            {
                harmony.Patch(clickMethod,
                    postfix: new HarmonyMethod(typeof(AlternativeTexturesPatches),
                        nameof(PaintBucketMenu_ReceiveLeftClick_Postfix)));
            }

            Log.Info(Tag, "Patched PaintBucketMenu (constructor + receiveLeftClick, covers SprayCanMenu)");
        }
        catch (Exception ex)
        {
            Log.Error(Tag, $"Failed to patch PaintBucketMenu: {ex.Message}");
        }
    }

    private static void CatalogueMenu_Ctor_Postfix(object __instance)
    {
        try
        {
            if (_catalogueSearchBox?.GetValue(__instance) is TextBox searchBox)
                searchBox.Selected = false;
        }
        catch (Exception ex)
        {
            Log.Error(Tag, $"CatalogueMenu ctor postfix error: {ex.Message}");
        }
    }

    private static void PaintBucketMenu_Ctor_Postfix(object __instance)
    {
        try
        {
            if (_paintBucketSearchBox?.GetValue(__instance) is TextBox searchBox)
                searchBox.Selected = false;
        }
        catch (Exception ex)
        {
            Log.Error(Tag, $"PaintBucketMenu ctor postfix error: {ex.Message}");
        }
    }

    /// <summary>Select the search box on tap so the Android soft keyboard activates.</summary>
    private static void CatalogueMenu_ReceiveLeftClick_Postfix(object __instance, int x, int y)
    {
        try
        {
            if (_catalogueSearchBox?.GetValue(__instance) is TextBox searchBox)
                SelectIfClicked(searchBox, x, y);
        }
        catch (Exception ex)
        {
            Log.Error(Tag, $"CatalogueMenu click postfix error: {ex.Message}");
        }
    }

    private static void PaintBucketMenu_ReceiveLeftClick_Postfix(object __instance, int x, int y)
    {
        try
        {
            if (_paintBucketSearchBox?.GetValue(__instance) is TextBox searchBox)
                SelectIfClicked(searchBox, x, y);
        }
        catch (Exception ex)
        {
            Log.Error(Tag, $"PaintBucketMenu click postfix error: {ex.Message}");
        }
    }

    private static void SelectIfClicked(TextBox searchBox, int x, int y)
    {
        var bounds = new Rectangle(searchBox.X, searchBox.Y, searchBox.Width, searchBox.Height);
        if (bounds.Contains(x, y))
            searchBox.Selected = true;
        else if (searchBox.Selected)
            searchBox.Selected = false;
    }
}
#endif
