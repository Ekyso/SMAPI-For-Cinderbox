#if SMAPI_FOR_ANDROID
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using Android.App;
using Android.Content;
using Android.Util;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.Menus;

namespace StardewModdingAPI.Mobile.Patches;

/// <summary>
/// Adds touch-friendly scrollbar, drag scrolling, and Android browser links
/// to UnofficialModUpdateMenu's update menu.
/// </summary>
internal static class UnofficialModUpdateMenuPatch
{
    private const string Tag = "UnofficialModUpdateMenuPatch";

    // Scrollbar dimensions (touch-friendly)
    private const int ThumbWidth = 48;
    private const int ThumbHeight = 64;
    private const int TrackWidth = 48;

    // Touch scrolling state
    private static int _lastTouchY;
    private static bool _isDragging;
    private const int DragThreshold = 10;
    private const int ScrollPixelsPerEntry = 48;

    // Scrollbar drag state
    private static bool _isScrollbarDragging;

    // Cached reflection
    private static Type _updateMenuType;
    private static FieldInfo _statusesField;
    private static FieldInfo _componentsField;
    private static FieldInfo _smapiComponentField;
    private static FieldInfo _smapiTextField;
    private static FieldInfo _originalStatusesField;
    private static FieldInfo _displayIndexField;
    private static FieldInfo _numDisplayableModsField;
    private static MethodInfo _scrollMethod;
    private static MethodInfo _updateComponentsMethod;

    public static void Apply(Harmony harmony)
    {
        try
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (assembly.GetName().Name == "ModUpdateMenu")
                {
                    _updateMenuType = assembly.GetType("ModUpdateMenu.Menus.UpdateMenu");
                    break;
                }
            }

            if (_updateMenuType == null)
            {
                Log.Info(Tag, "UnofficialModUpdateMenu not found, skipping patches");
                return;
            }

            var flags = BindingFlags.NonPublic | BindingFlags.Instance;
            _statusesField = _updateMenuType.GetField("statuses", flags);
            _componentsField = _updateMenuType.GetField("components", flags);
            _smapiComponentField = _updateMenuType.GetField("SMAPIComponent", flags);
            _smapiTextField = _updateMenuType.GetField("_SMAPIText", flags);
            _originalStatusesField = _updateMenuType.GetField("originalStatuses", flags);
            _displayIndexField = _updateMenuType.GetField("displayIndex", flags);
            _numDisplayableModsField = _updateMenuType.GetField("numDisplayableMods", flags);
            _scrollMethod = _updateMenuType.GetMethod("receiveScrollWheelAction",
                BindingFlags.Public | BindingFlags.Instance, null, new[] { typeof(int) }, null);
            _updateComponentsMethod = _updateMenuType.GetMethod("UpdateComponents", flags);

            var patchType = typeof(UnofficialModUpdateMenuPatch);

            var draw = _updateMenuType.GetMethod("draw",
                BindingFlags.Public | BindingFlags.Instance, null,
                new[] { typeof(SpriteBatch) }, null);
            if (draw != null)
            {
                harmony.Patch(draw,
                    transpiler: new HarmonyMethod(patchType, nameof(Draw_Transpiler)),
                    postfix: new HarmonyMethod(patchType, nameof(Draw_Postfix)));
                Log.Info(Tag, "Patched draw (scrollbar replacement)");
            }

            var receiveLeftClick = _updateMenuType.GetMethod("receiveLeftClick",
                BindingFlags.Public | BindingFlags.Instance, null,
                new[] { typeof(int), typeof(int), typeof(bool) }, null);
            if (receiveLeftClick != null)
            {
                harmony.Patch(receiveLeftClick,
                    prefix: new HarmonyMethod(patchType, nameof(ReceiveLeftClick_Prefix)));
                Log.Info(Tag, "Patched receiveLeftClick");
            }

            var performHoverAction = _updateMenuType.GetMethod("performHoverAction",
                BindingFlags.Public | BindingFlags.Instance, null,
                new[] { typeof(int), typeof(int) }, null);
            if (performHoverAction != null)
            {
                harmony.Patch(performHoverAction,
                    prefix: new HarmonyMethod(patchType, nameof(PerformHoverAction_Prefix)));
                Log.Info(Tag, "Patched performHoverAction");
            }

            var releaseLeftClick = typeof(IClickableMenu).GetMethod("releaseLeftClick",
                BindingFlags.Public | BindingFlags.Instance, null,
                new[] { typeof(int), typeof(int) }, null);
            if (releaseLeftClick != null)
            {
                harmony.Patch(releaseLeftClick,
                    prefix: new HarmonyMethod(patchType, nameof(ReleaseLeftClick_Prefix)));
                Log.Info(Tag, "Patched releaseLeftClick");
            }

            var leftClickHeld = typeof(IClickableMenu).GetMethod("leftClickHeld",
                BindingFlags.Public | BindingFlags.Instance, null,
                new[] { typeof(int), typeof(int) }, null);
            if (leftClickHeld != null)
            {
                harmony.Patch(leftClickHeld,
                    prefix: new HarmonyMethod(patchType, nameof(LeftClickHeld_Prefix)));
                Log.Info(Tag, "Patched leftClickHeld");
            }

            Log.Info(Tag, "All UpdateMenu patches applied");
        }
        catch (Exception ex)
        {
            Log.Error(Tag, $"Failed to apply patches: {ex.Message}");
            Log.Error(Tag, ex.StackTrace ?? "");
        }
    }

    #region Scrollbar geometry helpers

    private static void GetScrollMetrics(object instance, out int maxScroll, out int displayIndex, out int numDisplayable)
    {
        var originalStatuses = _originalStatusesField?.GetValue(instance) as System.Collections.IList;
        numDisplayable = (int)(_numDisplayableModsField?.GetValue(instance) ?? 1);
        displayIndex = (int)(_displayIndexField?.GetValue(instance) ?? 0);
        maxScroll = Math.Max(1, (originalStatuses?.Count ?? 0) - numDisplayable);
    }

    private static Rectangle GetTrackBounds(object instance)
    {
        var menu = (IClickableMenu)instance;
        var topLeft = Utility.getTopLeftPositionForCenteringOnScreen(menu.width, menu.height - 100);
        int trackX = (int)topLeft.X + menu.width;
        int trackY = (int)topLeft.Y + 16;
        int trackHeight = menu.height - 200 + 16;
        return new Rectangle(trackX, trackY, TrackWidth, trackHeight);
    }

    private static Rectangle GetThumbBounds(object instance)
    {
        GetScrollMetrics(instance, out int maxScroll, out int displayIndex, out _);
        var track = GetTrackBounds(instance);
        int thumbTravel = track.Height - ThumbHeight;
        int thumbY = track.Y + (int)((float)displayIndex / maxScroll * thumbTravel);
        return new Rectangle(track.X, thumbY, ThumbWidth, ThumbHeight);
    }

    private static void SetDisplayIndexFromY(object instance, int y)
    {
        GetScrollMetrics(instance, out int maxScroll, out _, out _);
        var track = GetTrackBounds(instance);
        int thumbTravel = track.Height - ThumbHeight;
        float ratio = (float)(y - track.Y) / Math.Max(1, thumbTravel);
        int newIndex = Math.Clamp((int)(ratio * maxScroll), 0, maxScroll);

        _displayIndexField?.SetValue(instance, newIndex);
        _updateComponentsMethod?.Invoke(instance, null);
    }

    #endregion

    #region Patches

    /// <summary>Handle scrollbar, content drag, and mod link clicks.</summary>
    public static bool ReceiveLeftClick_Prefix(object __instance, int x, int y)
    {
        var thumb = GetThumbBounds(__instance);
        var track = GetTrackBounds(__instance);

        if (thumb.Contains(x, y))
        {
            _isScrollbarDragging = true;
            _isDragging = false;
            _lastTouchY = 0;
            return false;
        }

        if (track.Contains(x, y))
        {
            SetDisplayIndexFromY(__instance, y - ThumbHeight / 2);
            _isScrollbarDragging = true;
            return false;
        }

        _lastTouchY = y;
        _isDragging = false;
        _isScrollbarDragging = false;

        var smapiText = _smapiTextField?.GetValue(__instance) as string;
        var smapiComponent = _smapiComponentField?.GetValue(__instance) as ClickableComponent;
        if (smapiText != null && smapiComponent != null && smapiComponent.containsPoint(x, y))
        {
            ConfirmAndOpenUrl("https://smapi.io");
            return false;
        }

        var components = _componentsField?.GetValue(__instance) as IList<ClickableComponent>;
        var statuses = _statusesField?.GetValue(__instance) as System.Collections.IList;

        if (components != null && components.Count > 0 && statuses != null)
        {
            for (int j = 3; j < components.Count; j++)
            {
                if (components[j].containsPoint(x, y))
                {
                    int offset = j % 3;
                    if (offset == 2)
                    {
                        int statusIdx = (j - 3) / 3;
                        if (statusIdx >= 0 && statusIdx < statuses.Count)
                        {
                            var status = statuses[statusIdx];
                            var urlProp = status?.GetType().GetProperty("UpdateURL",
                                BindingFlags.Public | BindingFlags.Instance);
                            var url = urlProp?.GetValue(status) as string;
                            if (!string.IsNullOrEmpty(url))
                            {
                                ConfirmAndOpenUrl(url);
                                Game1.playSound("bigSelect");
                            }
                            else
                            {
                                Game1.playSound("toyPiano");
                            }
                        }
                        return false;
                    }
                    break;
                }
            }
        }

        return true;
    }

    /// <summary>
    /// Handle held drag for scrollbar thumb.
    /// Scoped to UpdateMenu instances only.
    /// </summary>
    public static bool LeftClickHeld_Prefix(object __instance, int x, int y)
    {
        if (_updateMenuType == null || !_updateMenuType.IsInstanceOfType(__instance))
            return true;

        if (_isScrollbarDragging)
        {
            SetDisplayIndexFromY(__instance, y - ThumbHeight / 2);
            return false;
        }

        return true;
    }

    /// <summary>
    /// Track drag movement for touch scrolling on content area.
    /// </summary>
    public static bool PerformHoverAction_Prefix(object __instance, int x, int y)
    {
        if (_isScrollbarDragging)
            return false;

        if (_lastTouchY != 0)
        {
            int deltaY = _lastTouchY - y;
            if (Math.Abs(deltaY) > DragThreshold)
                _isDragging = true;

            if (_isDragging && Math.Abs(deltaY) >= ScrollPixelsPerEntry)
            {
                int direction = deltaY > 0 ? -1 : 1;
                _scrollMethod?.Invoke(__instance, new object[] { direction });
                _lastTouchY = y;
            }
        }

        if (_isDragging)
            return false;

        return true;
    }

    /// <summary>
    /// End all drag tracking on release.
    /// Scoped to UpdateMenu instances only.
    /// </summary>
    public static bool ReleaseLeftClick_Prefix(object __instance, int x, int y)
    {
        if (_updateMenuType == null || !_updateMenuType.IsInstanceOfType(__instance))
            return true;

        bool wasDragging = _isDragging || _isScrollbarDragging;
        _isDragging = false;
        _isScrollbarDragging = false;
        _lastTouchY = 0;

        if (wasDragging)
            return false;

        return true;
    }

    /// <summary>Hide the original scrollbar so Draw_Postfix can draw a replacement.</summary>
    public static IEnumerable<CodeInstruction> Draw_Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var codes = new List<CodeInstruction>(instructions);
        bool patched = false;

        for (int i = 0; i < codes.Count - 6; i++)
        {
            if (IsLdcI4(codes[i], 325) && IsLdcI4(codes[i + 1], 448) &&
                IsLdcI4(codes[i + 2], 5) && IsLdcI4(codes[i + 3], 17))
            {
                for (int j = i + 4; j < Math.Min(i + 20, codes.Count - 1); j++)
                {
                    if (IsLdcI4(codes[j], 16) && IsLdcI4(codes[j + 1], 32))
                    {
                        codes[j] = new CodeInstruction(OpCodes.Ldc_I4_0);
                        codes[j + 1] = new CodeInstruction(OpCodes.Ldc_I4_0);
                        patched = true;
                        Log.Info(Tag, "Transpiler: zeroed original scrollbar dimensions");
                        break;
                    }
                }
                break;
            }
        }

        if (!patched)
            Log.Error(Tag, "Transpiler: failed to find scrollbar dimensions in draw()");

        return codes;
    }

    /// <summary>Draw a touch-friendly scrollbar with track and draggable thumb.</summary>
    public static void Draw_Postfix(object __instance, SpriteBatch b)
    {
        var originalStatuses = _originalStatusesField?.GetValue(__instance) as System.Collections.IList;
        if (originalStatuses == null || originalStatuses.Count == 0)
            return;

        int numDisplayable = (int)(_numDisplayableModsField?.GetValue(__instance) ?? 1);
        if (originalStatuses.Count <= numDisplayable)
            return;

        var track = GetTrackBounds(__instance);
        var thumb = GetThumbBounds(__instance);

        IClickableMenu.drawTextureBox(b, Game1.mouseCursors,
            new Rectangle(403, 383, 6, 6),
            track.X, track.Y, track.Width, track.Height,
            Color.White, 4f, false);

        IClickableMenu.drawTextureBox(b, Game1.mouseCursors,
            new Rectangle(435, 463, 6, 10),
            thumb.X, thumb.Y, thumb.Width, thumb.Height,
            Color.White, 4f, false);
    }

    #endregion

    #region Helpers

    private static bool IsLdcI4(CodeInstruction instr, int value)
    {
        if (instr.opcode == OpCodes.Ldc_I4 && instr.operand is int i && i == value)
            return true;
        if (instr.opcode == OpCodes.Ldc_I4_S && instr.operand is sbyte sb && sb == value)
            return true;
        if (instr.opcode == OpCodes.Ldc_I4_S && instr.operand is int si && si == value)
            return true;
        if (value >= 0 && value <= 8)
        {
            var expected = value switch
            {
                0 => OpCodes.Ldc_I4_0, 1 => OpCodes.Ldc_I4_1,
                2 => OpCodes.Ldc_I4_2, 3 => OpCodes.Ldc_I4_3,
                4 => OpCodes.Ldc_I4_4, 5 => OpCodes.Ldc_I4_5,
                6 => OpCodes.Ldc_I4_6, 7 => OpCodes.Ldc_I4_7,
                8 => OpCodes.Ldc_I4_8, _ => OpCodes.Nop
            };
            if (instr.opcode == expected)
                return true;
        }
        return false;
    }

    private static void ConfirmAndOpenUrl(string url)
    {
        try
        {
            var activity = SMAPIActivityTool.MainActivity
                ?? (Activity)Microsoft.Xna.Framework.Game.Activity;
            if (activity == null)
            {
                OpenUrlOnAndroid(url);
                return;
            }

            activity.RunOnUiThread(() =>
            {
                new AlertDialog.Builder(activity)
                    .SetTitle("Open Link")
                    .SetMessage(url)
                    .SetPositiveButton("Open", (s, e) => OpenUrlOnAndroid(url))
                    .SetNegativeButton("Cancel", (s, e) => { })
                    .Show();
            });
        }
        catch (Exception ex)
        {
            Log.Error(Tag, $"Failed to show URL confirmation: {ex.Message}");
            OpenUrlOnAndroid(url);
        }
    }

    private static void OpenUrlOnAndroid(string url)
    {
        try
        {
            var uri = global::Android.Net.Uri.Parse(url);
            var intent = new Intent(Intent.ActionView, uri);
            intent.AddFlags(ActivityFlags.NewTask);
            Application.Context.StartActivity(intent);
            Log.Info(Tag, $"Opened URL: {url}");
        }
        catch (Exception ex)
        {
            Log.Error(Tag, $"Failed to open URL '{url}': {ex.Message}");
        }
    }

    #endregion
}
#endif
