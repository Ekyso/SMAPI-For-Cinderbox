using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
#if SMAPI_FOR_ANDROID
using Android.Util;
#endif

namespace StardewModdingAPI.Mobile.Patches;

/// <summary>
/// Fixes SpaceCore's AnimatedSpriteDrawExtrasPatch3 transpiler for NPC.draw.
/// Applies SpaceCore's intended modifications when its drawCount bug prevents them,
/// and fixes a Nullable&lt;Rectangle&gt; mismatch that causes garbled sprites on Mono.
/// </summary>
internal static class SpaceCorePatch3Fix
{
    private const string Tag = "SpaceCorePatch3Fix";

    private static MethodInfo? _getExtraValues;

    private static void LogInfo(string message)
    {
#if SMAPI_FOR_ANDROID
        Log.Info(Tag, message);
#else
        System.Diagnostics.Debug.WriteLine($"[{Tag}] {message}");
#endif
    }

    internal static void Apply(Harmony harmony)
    {
        Type? patchType = null;
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (asm.GetName().Name == "SpaceCore")
            {
                patchType = asm.GetType("SpaceCore.Patches.AnimatedSpriteDrawExtrasPatch3");
                break;
            }
        }

        if (patchType == null)
        {
            LogInfo("SpaceCore not found, skipping");
            return;
        }

        _getExtraValues = patchType.GetMethod(
            "getExtraValues",
            BindingFlags.Public | BindingFlags.Static
        );
        if (_getExtraValues == null)
        {
            LogInfo("getExtraValues not found, skipping");
            return;
        }

        var target = typeof(NPC).GetMethod(
            "draw",
            new Type[] { typeof(SpriteBatch), typeof(float) }
        );
        if (target == null)
        {
            LogInfo("NPC.draw not found, skipping");
            return;
        }

        var transpiler = typeof(SpaceCorePatch3Fix).GetMethod(
            nameof(Transpiler),
            BindingFlags.NonPublic | BindingFlags.Static
        );
        harmony.Patch(
            target,
            transpiler: new HarmonyMethod(transpiler) { priority = Priority.Last }
        );
        LogInfo("Registered transpiler for NPC.draw");
    }

    static IEnumerable<CodeInstruction> Transpiler(
        IEnumerable<CodeInstruction> instructions,
        ILGenerator generator
    )
    {
        if (_getExtraValues == null)
            return instructions;

        var colorGetWhite = typeof(Color).GetMethod(
            "get_White",
            BindingFlags.Public | BindingFlags.Static
        );
        var vec2Multiply = typeof(Vector2).GetMethod(
            "op_Multiply",
            BindingFlags.Public | BindingFlags.Static,
            null,
            new Type[] { typeof(float), typeof(Vector2) },
            null
        );
        var spriteBatchDrawVec2 = typeof(SpriteBatch).GetMethod(
            "Draw",
            BindingFlags.Public | BindingFlags.Instance,
            null,
            new Type[]
            {
                typeof(Texture2D),
                typeof(Vector2),
                typeof(Rectangle),
                typeof(Color),
                typeof(float),
                typeof(Vector2),
                typeof(Vector2),
                typeof(SpriteEffects),
                typeof(float),
            },
            null
        );

        if (spriteBatchDrawVec2 == null)
        {
            LogInfo("SpriteBatch.Draw Vec2 overload not found, aborting");
            return instructions;
        }

        var orig = new List<CodeInstruction>(instructions);
        int whiteFound = 0;
        int scaleFound = 0;
        int vecFound = 0;
        int drawFound = 0;

        for (int i = 0; i < orig.Count; ++i)
        {
            if (orig[i].opcode == OpCodes.Call && orig[i].operand.Equals(colorGetWhite))
                ++whiteFound;

            if (i > 0 && orig[i - 1].opcode == OpCodes.Ldc_I4_4 && orig[i].opcode == OpCodes.Mul)
                ++scaleFound;

            if (
                i > 0
                && orig[i - 1].opcode == OpCodes.Ldc_R4
                && orig[i - 1].operand.Equals(4f)
                && orig[i].opcode == OpCodes.Mul
            )
                ++vecFound;

            if (
                orig[i].opcode == OpCodes.Callvirt
                && orig[i].operand is MethodInfo mi
                && mi.Name == "Draw"
            )
                ++drawFound;
        }

        bool spaceCoreWasNoOp =
            whiteFound >= 3 && scaleFound >= 3 && vecFound >= 2 && drawFound >= 3;

        List<CodeInstruction> ret;

        if (spaceCoreWasNoOp)
        {
            LogInfo(
                $"Patterns matched (white={whiteFound}, scale={scaleFound}, vec={vecFound}, draw={drawFound}). Applying SpaceCore fixes."
            );

            LocalBuilder scale = generator.DeclareLocal(typeof(Vector2));
            LocalBuilder scaleX = generator.DeclareLocal(typeof(float));
            LocalBuilder scaleY = generator.DeclareLocal(typeof(float));
            LocalBuilder gradColor = generator.DeclareLocal(typeof(Color));

            ret = new List<CodeInstruction>()
            {
                new(OpCodes.Ldarg_0),
                new(OpCodes.Ldloca, scale),
                new(OpCodes.Ldloca, scaleX),
                new(OpCodes.Ldloca, scaleY),
                new(OpCodes.Ldloca, gradColor),
                new(OpCodes.Call, _getExtraValues),
            };

            int scaleCount = 0;
            int whiteCount = 0;
            int whiteSkip = 0; // skip pattern: apply, skip, apply
            int vecCount = 0;
            int vecSkip = 1; // skip pattern: skip, apply
            int drawCount = 0;
            int drawSkip = 2; // skip pattern: skip, skip, apply

            for (int i = 0; i < orig.Count; ++i)
            {
                if (
                    whiteCount < 3
                    && orig[i].opcode == OpCodes.Call
                    && orig[i].operand.Equals(colorGetWhite)
                )
                {
                    ++whiteCount;
                    if (whiteSkip > 0)
                    {
                        --whiteSkip;
                        ret.Add(orig[i]);
                    }
                    else
                    {
                        whiteSkip = 1;
                        ret.Add(new CodeInstruction(OpCodes.Ldloc, gradColor));
                    }
                    continue;
                }

                if (
                    drawCount < 3
                    && orig[i].opcode == OpCodes.Callvirt
                    && orig[i].operand is MethodInfo drawMethod
                    && drawMethod.Name == "Draw"
                )
                {
                    ++drawCount;
                    if (drawSkip > 0)
                    {
                        --drawSkip;
                        ret.Add(orig[i]);
                    }
                    else
                    {
                        ret.Add(new CodeInstruction(OpCodes.Callvirt, spriteBatchDrawVec2));
                    }
                    continue;
                }

                ret.Add(orig[i]);

                if (
                    i > 0
                    && scaleCount < 3
                    && orig[i - 1].opcode == OpCodes.Ldc_I4_4
                    && orig[i].opcode == OpCodes.Mul
                )
                {
                    ++scaleCount;
                    ret.AddRange(
                        new List<CodeInstruction>()
                        {
                            new(OpCodes.Ldloc, (scaleCount == 2 ? scaleX : scaleY)),
                            new(OpCodes.Mul),
                        }
                    );
                }

                if (
                    i > 0
                    && vecCount < 2
                    && orig[i - 1].opcode == OpCodes.Ldc_R4
                    && orig[i - 1].operand.Equals(4f)
                    && orig[i].opcode == OpCodes.Mul
                )
                {
                    ++vecCount;
                    if (vecSkip > 0)
                    {
                        --vecSkip;
                    }
                    else
                    {
                        ret.AddRange(
                            new List<CodeInstruction>()
                            {
                                new(OpCodes.Ldloc, scale),
                                new(OpCodes.Call, vec2Multiply),
                            }
                        );
                    }
                }
            }

            LogInfo("Applied SpaceCore modifications");
        }
        else
        {
            LogInfo(
                $"Patterns not matched (white={whiteFound}, scale={scaleFound}, vec={vecFound}, draw={drawFound}). SpaceCore already applied fixes."
            );
            ret = orig;
        }


        return ret;
    }
}
