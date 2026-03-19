#if SMAPI_FOR_ANDROID
using System;
using System.Reflection;
using Android.Util;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.Menus;

namespace StardewModdingAPI.Mobile.Patches;

/// <summary>
/// Fixes FashionSense SearchMenu and NameMenu on Android.
/// SearchMenu: deselects search box on construction so tap-to-type works with the soft keyboard.
/// NameMenu: re-evaluates name validity when text changes via soft keyboard (bypasses receiveKeyPress),
/// and handles clicking OK with empty text.
/// </summary>
internal static class FashionSensePatches
{
    private const string Tag = "FashionSensePatches";

    private static FieldInfo _searchBoxField = null!;

    private static MethodInfo _evaluateNameMethod = null!;
    private static FieldInfo _isNewNameValidField = null!;
    private static FieldInfo _nameTextBoxField = null!;
    private static FieldInfo _doneNamingButtonField = null!;
    private static FieldInfo _callbackMenuField = null!;

    public static void Apply(Harmony harmony)
    {
        try
        {
            Assembly? fashionSenseAssembly = null;
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (assembly.GetName().Name == "FashionSense")
                {
                    fashionSenseAssembly = assembly;
                    break;
                }
            }

            if (fashionSenseAssembly == null)
            {
                Log.Info(Tag, "FashionSense not found, skipping patches");
                return;
            }

            ApplySearchMenuPatches(harmony, fashionSenseAssembly);
            ApplyNameMenuPatches(harmony, fashionSenseAssembly);
        }
        catch (Exception ex)
        {
            Log.Error(Tag, $"Failed to patch FashionSense: {ex.Message}");
        }
    }

    #region SearchMenu

    private static void ApplySearchMenuPatches(Harmony harmony, Assembly assembly)
    {
        try
        {
            var searchMenuType = assembly.GetType("FashionSense.Framework.UI.SearchMenu");
            if (searchMenuType == null)
            {
                Log.Info(Tag, "SearchMenu type not found, skipping");
                return;
            }

            _searchBoxField = searchMenuType.GetField(
                "_searchBox",
                BindingFlags.NonPublic | BindingFlags.Instance
            )!;
            if (_searchBoxField == null)
            {
                Log.Error(Tag, "SearchMenu._searchBox field not found");
                return;
            }

            var ctor = searchMenuType.GetConstructors()[0];
            harmony.Patch(ctor,
                postfix: new HarmonyMethod(typeof(FashionSensePatches),
                    nameof(SearchMenu_Ctor_Postfix)));

            var clickMethod = searchMenuType.GetMethod(
                "receiveLeftClick",
                BindingFlags.Public | BindingFlags.Instance,
                null,
                new[] { typeof(int), typeof(int), typeof(bool) },
                null
            );
            if (clickMethod != null)
            {
                harmony.Patch(clickMethod,
                    postfix: new HarmonyMethod(typeof(FashionSensePatches),
                        nameof(SearchMenu_ReceiveLeftClick_Postfix)));
            }

            Log.Info(Tag, "Patched SearchMenu (constructor + receiveLeftClick)");
        }
        catch (Exception ex)
        {
            Log.Error(Tag, $"Failed to patch SearchMenu: {ex.Message}");
        }
    }

    /// <summary>Deselect the search box after construction so tap-to-select works.</summary>
    private static void SearchMenu_Ctor_Postfix(object __instance)
    {
        try
        {
            if (_searchBoxField?.GetValue(__instance) is TextBox searchBox)
                searchBox.Selected = false;
        }
        catch (Exception ex)
        {
            Log.Error(Tag, $"SearchMenu ctor postfix error: {ex.Message}");
        }
    }

    /// <summary>Select the search box on tap so the Android soft keyboard activates.</summary>
    private static void SearchMenu_ReceiveLeftClick_Postfix(object __instance, int x, int y)
    {
        try
        {
            if (_searchBoxField?.GetValue(__instance) is TextBox searchBox)
                SelectIfClicked(searchBox, x, y);
        }
        catch (Exception ex)
        {
            Log.Error(Tag, $"SearchMenu click postfix error: {ex.Message}");
        }
    }

    private static void SelectIfClicked(TextBox textBox, int x, int y)
    {
        var bounds = new Rectangle(textBox.X, textBox.Y, textBox.Width, textBox.Height);
        if (bounds.Contains(x, y))
            textBox.Selected = true;
        else if (textBox.Selected)
            textBox.Selected = false;
    }

    #endregion

    #region NameMenu

    private static void ApplyNameMenuPatches(Harmony harmony, Assembly assembly)
    {
        try
        {
            var nameMenuType = assembly.GetType("FashionSense.Framework.UI.NameMenu");
            if (nameMenuType == null)
            {
                Log.Info(Tag, "NameMenu type not found, skipping");
                return;
            }

            _evaluateNameMethod = nameMenuType.GetMethod(
                "EvaluateName",
                BindingFlags.Public | BindingFlags.Instance
            )!;
            _isNewNameValidField = nameMenuType.GetField(
                "_isNewNameValid",
                BindingFlags.NonPublic | BindingFlags.Instance
            )!;
            _nameTextBoxField = nameMenuType.GetField(
                "_textBox",
                BindingFlags.NonPublic | BindingFlags.Instance
            )!;
            _doneNamingButtonField = nameMenuType.GetField(
                "_doneNamingButton",
                BindingFlags.NonPublic | BindingFlags.Instance
            )!;
            _callbackMenuField = nameMenuType.GetField(
                "_callbackMenu",
                BindingFlags.NonPublic | BindingFlags.Instance
            )!;

            if (_evaluateNameMethod == null || _nameTextBoxField == null ||
                _doneNamingButtonField == null || _callbackMenuField == null)
            {
                Log.Error(Tag, "Could not find required fields/methods on NameMenu");
                return;
            }

            var drawMethod = nameMenuType.GetMethod(
                "draw",
                BindingFlags.Public | BindingFlags.Instance,
                null,
                new[] { typeof(SpriteBatch) },
                null
            );
            if (drawMethod != null)
            {
                harmony.Patch(drawMethod,
                    prefix: new HarmonyMethod(typeof(FashionSensePatches),
                        nameof(NameMenu_Draw_Prefix)));
            }

            var clickMethod = nameMenuType.GetMethod(
                "receiveLeftClick",
                BindingFlags.Public | BindingFlags.Instance,
                null,
                new[] { typeof(int), typeof(int), typeof(bool) },
                null
            );
            if (clickMethod != null)
            {
                harmony.Patch(clickMethod,
                    prefix: new HarmonyMethod(typeof(FashionSensePatches),
                        nameof(NameMenu_ReceiveLeftClick_Prefix)));
            }

            Log.Info(Tag, "Patched NameMenu (EvaluateName on draw + click, empty-text close)");
        }
        catch (Exception ex)
        {
            Log.Error(Tag, $"Failed to patch NameMenu: {ex.Message}");
        }
    }

    /// <summary>Keep name validation in sync with soft keyboard text before drawing.</summary>
    private static void NameMenu_Draw_Prefix(object __instance)
    {
        try
        {
            _isNewNameValidField?.SetValue(__instance, true);
        }
        catch (Exception ex)
        {
            Log.Error(Tag, $"NameMenu draw prefix error: {ex.Message}");
        }
    }

    /// <summary>Re-evaluate name validity before click handling, and close on empty OK tap.</summary>
    private static bool NameMenu_ReceiveLeftClick_Prefix(object __instance, int x, int y)
    {
        try
        {
            var textBox = _nameTextBoxField?.GetValue(__instance) as TextBox;
            var doneButton = _doneNamingButtonField?.GetValue(__instance) as ClickableTextureComponent;

            if (textBox == null || doneButton == null)
                return true;

            if (doneButton.containsPoint(x, y) && string.IsNullOrEmpty(textBox.Text))
            {
                var callbackMenu = _callbackMenuField?.GetValue(__instance) as IClickableMenu;
                if (callbackMenu != null)
                    Game1.activeClickableMenu = callbackMenu;
                Game1.playSound("smallSelect");
                return false;
            }

            _evaluateNameMethod?.Invoke(__instance, null);

            return true;
        }
        catch (Exception ex)
        {
            Log.Error(Tag, $"NameMenu click prefix error: {ex.Message}");
            return true;
        }
    }

    #endregion
}
#endif
