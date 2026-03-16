using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using StardewValley;
#if SMAPI_FOR_ANDROID
using Android.Util;
#endif

namespace StardewModdingAPI.Mobile.Patches;

/// <summary>
/// Fixes MobilePhone mod UI positioning on Android. The mod uses Game1.viewport for
/// layout, which gives wrong dimensions with zoom scaling. Transpiles PhoneUtils
/// methods to use Game1.uiViewport instead.
/// </summary>
internal static class MobilePhonePatch
{
    private const string Tag = "MobilePhonePatch";

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

    /// <summary>Apply the patches if MobilePhone mod is loaded.</summary>
    public static void Apply(Harmony harmony)
    {
        try
        {
            Type? phoneUtilsType = null;
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (assembly.GetName().Name == "MobilePhone")
                {
                    phoneUtilsType = assembly.GetType("MobilePhone.PhoneUtils");
                    break;
                }
            }

            if (phoneUtilsType == null)
            {
                LogInfo("MobilePhone mod not found, skipping patch");
                return;
            }

            var transpiler = new HarmonyMethod(
                typeof(MobilePhonePatch).GetMethod(nameof(ViewportToUiViewport_Transpiler),
                    BindingFlags.Public | BindingFlags.Static));

            string[] methodNames = {
                "GetPhonePosition",
                "GetPhoneIconPosition",
                "GetOpenSurroundingPosition",
                "GetScreenPosition",
                "CheckIconOffScreen"
            };

            foreach (var methodName in methodNames)
            {
                var method = phoneUtilsType.GetMethod(methodName,
                    BindingFlags.Public | BindingFlags.Static);
                if (method != null)
                {
                    harmony.Patch(method, transpiler: transpiler);
                    LogInfo($"Patched PhoneUtils.{methodName} (viewport → uiViewport)");
                }
                else
                {
                    LogError($"Could not find PhoneUtils.{methodName}");
                }
            }
        }
        catch (Exception ex)
        {
            LogError($"Failed to patch MobilePhone: {ex.Message}");
        }
    }

    /// <summary>Transpiler that replaces Game1.viewport references with Game1.uiViewport.</summary>
    public static IEnumerable<CodeInstruction> ViewportToUiViewport_Transpiler(
        IEnumerable<CodeInstruction> instructions)
    {
        var viewportField = typeof(Game1).GetField("viewport",
            BindingFlags.Public | BindingFlags.Static);
        var uiViewportField = typeof(Game1).GetField("uiViewport",
            BindingFlags.Public | BindingFlags.Static);

        if (viewportField == null || uiViewportField == null)
        {
            LogError("Could not find Game1.viewport or Game1.uiViewport fields");
            foreach (var instr in instructions)
                yield return instr;
            yield break;
        }

        int count = 0;
        foreach (var instr in instructions)
        {
            if (instr.operand is FieldInfo field && field == viewportField)
            {
                yield return new CodeInstruction(instr.opcode, uiViewportField)
                    .MoveLabelsFrom(instr)
                    .MoveBlocksFrom(instr);
                count++;
            }
            else
            {
                yield return instr;
            }
        }

        if (count > 0)
            LogInfo($"  Replaced {count} viewport reference(s)");
    }
}
