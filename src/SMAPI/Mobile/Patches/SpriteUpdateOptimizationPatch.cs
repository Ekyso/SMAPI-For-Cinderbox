#if SMAPI_FOR_ANDROID
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Microsoft.Xna.Framework;
using StardewValley;

namespace StardewModdingAPI.Mobile.Patches;

/// <summary>
/// Optimizes the temporarySprites removal loop in GameLocation.updateEvenIfFarmerIsntHere.
/// Uses a transpiler to replace the O(n^2) reverse-iterate-remove loop with O(n) compaction.
/// All other method logic (private field access, protected method calls) is untouched.
/// </summary>
internal static class SpriteUpdateOptimizationPatch
{
    /// <summary>Apply the sprite update optimization patch.</summary>
    public static void Apply(Harmony harmony)
    {
        try
        {
            var target = AccessTools.Method(
                typeof(GameLocation),
                "updateEvenIfFarmerIsntHere",
                new[] { typeof(GameTime), typeof(bool) }
            );

            if (target == null)
            {
                AndroidLogger.Log(
                    "[SpriteUpdateOptimizationPatch] Could not find updateEvenIfFarmerIsntHere"
                );
                return;
            }

            var transpiler = new HarmonyMethod(
                typeof(SpriteUpdateOptimizationPatch).GetMethod(
                    nameof(Transpiler),
                    BindingFlags.NonPublic | BindingFlags.Static
                )
            );

            harmony.Patch(target, transpiler: transpiler);
            AndroidLogger.Log("[SpriteUpdateOptimizationPatch] Applied sprite update optimization");
        }
        catch (Exception ex)
        {
            AndroidLogger.Log($"[SpriteUpdateOptimizationPatch] Failed to apply: {ex}");
        }
    }

    /// <summary>
    /// Transpiler that replaces the reverse-iterate-remove loop with a call to
    /// <see cref="CompactSprites"/>. Anchors on TemporaryAnimatedSpriteList.RemoveAt.
    /// </summary>
    private static IEnumerable<CodeInstruction> Transpiler(
        IEnumerable<CodeInstruction> instructions
    )
    {
        var codes = new List<CodeInstruction>(instructions);

        var temporarySpritesField = AccessTools.Field(typeof(GameLocation), "temporarySprites");
        var getCountMethod = AccessTools.PropertyGetter(
            typeof(TemporaryAnimatedSpriteList),
            "Count"
        );
        var removeAtMethod = AccessTools.Method(
            typeof(TemporaryAnimatedSpriteList),
            "RemoveAt",
            new[] { typeof(int) }
        );
        var compactMethod = AccessTools.Method(
            typeof(SpriteUpdateOptimizationPatch),
            nameof(CompactSprites)
        );

        if (
            temporarySpritesField == null
            || getCountMethod == null
            || removeAtMethod == null
            || compactMethod == null
        )
        {
            AndroidLogger.Log("[SpriteUpdateOptimizationPatch] Could not resolve required members");
            return codes;
        }

        int removeAtIdx = -1;
        for (int i = 0; i < codes.Count; i++)
        {
            if (codes[i].Calls(removeAtMethod))
            {
                removeAtIdx = i;
                break;
            }
        }

        if (removeAtIdx == -1)
        {
            AndroidLogger.Log("[SpriteUpdateOptimizationPatch] Could not find RemoveAt call in IL");
            return codes;
        }

        // scan backward for loop init: ldarg.0 → ldfld temporarySprites → get_Count → ldc.i4.1 → sub
        int loopInitStart = -1;
        for (int i = removeAtIdx; i >= 4; i--)
        {
            if (
                codes[i].opcode == OpCodes.Sub
                && IsLdcI4(codes[i - 1], 1)
                && codes[i - 2].Calls(getCountMethod)
                && codes[i - 3].LoadsField(temporarySpritesField)
                && codes[i - 4].IsLdarg(0)
            )
            {
                loopInitStart = i - 4;
                break;
            }
        }

        if (loopInitStart == -1)
        {
            AndroidLogger.Log(
                "[SpriteUpdateOptimizationPatch] Could not find loop init pattern in IL"
            );
            return codes;
        }

        // find loop end: first backward branch after RemoveAt
        int loopEnd = -1;
        for (int i = removeAtIdx + 1; i < codes.Count; i++)
        {
            if (codes[i].Branches(out Label? targetLabel) && targetLabel.HasValue)
            {
                for (int j = loopInitStart; j < i; j++)
                {
                    if (codes[j].labels.Contains(targetLabel.Value))
                    {
                        loopEnd = i;
                        break;
                    }
                }

                if (loopEnd != -1)
                    break;
            }
        }

        if (loopEnd == -1)
        {
            AndroidLogger.Log(
                "[SpriteUpdateOptimizationPatch] Could not find loop end branch in IL"
            );
            return codes;
        }

        var firstLabels = new List<Label>(codes[loopInitStart].labels);

        var replacement = new List<CodeInstruction>
        {
            new CodeInstruction(OpCodes.Ldarg_0) { labels = firstLabels },
            new CodeInstruction(OpCodes.Ldfld, temporarySpritesField),
            new CodeInstruction(OpCodes.Ldarg_1), // GameTime time
            new CodeInstruction(OpCodes.Call, compactMethod),
        };

        int removeCount = loopEnd - loopInitStart + 1;
        codes.RemoveRange(loopInitStart, removeCount);
        codes.InsertRange(loopInitStart, replacement);

        AndroidLogger.Log(
            $"[SpriteUpdateOptimizationPatch] Transpiler: replaced {removeCount} IL instructions with {replacement.Count}"
        );

        return codes;
    }

    /// <summary>
    /// O(n) in-place compaction that replaces the O(n^2) reverse-iterate-remove loop.
    /// Handles sprite pooling, list mutation during update(), and preserves newly added sprites.
    /// </summary>
    public static void CompactSprites(TemporaryAnimatedSpriteList sprites, GameTime time)
    {
        int originalCount = sprites.Count;
        int writeIndex = 0;

        for (int i = 0; i < originalCount; i++)
        {
            if (i >= sprites.Count)
                break;

            var sprite = sprites[i];

            if (sprite == null || !sprite.update(time))
            {
                if (writeIndex != i)
                    sprites[writeIndex] = sprite;
                writeIndex++;
            }
            else if (sprite.Pooled)
            {
                sprite.Pool();
            }
        }

        // preserve sprites added during iteration (appended beyond originalCount)
        if (sprites.Count > originalCount)
        {
            for (int i = originalCount; i < sprites.Count; i++)
            {
                sprites[writeIndex] = sprites[i];
                writeIndex++;
            }
        }

        // trim via inner List<T> to bypass TemporaryAnimatedSpriteList.RemoveAt pooling logic
        var innerList = sprites.AnimatedSprites;
        if (writeIndex < innerList.Count)
            innerList.RemoveRange(writeIndex, innerList.Count - writeIndex);
    }

    /// <summary>Check if an IL instruction loads the given integer constant.</summary>
    private static bool IsLdcI4(CodeInstruction instruction, int value)
    {
        if (instruction.opcode == OpCodes.Ldc_I4 && instruction.operand is int v)
            return v == value;
        if (instruction.opcode == OpCodes.Ldc_I4_S && instruction.operand is sbyte s)
            return s == value;

        return value switch
        {
            0 => instruction.opcode == OpCodes.Ldc_I4_0,
            1 => instruction.opcode == OpCodes.Ldc_I4_1,
            2 => instruction.opcode == OpCodes.Ldc_I4_2,
            3 => instruction.opcode == OpCodes.Ldc_I4_3,
            4 => instruction.opcode == OpCodes.Ldc_I4_4,
            5 => instruction.opcode == OpCodes.Ldc_I4_5,
            6 => instruction.opcode == OpCodes.Ldc_I4_6,
            7 => instruction.opcode == OpCodes.Ldc_I4_7,
            8 => instruction.opcode == OpCodes.Ldc_I4_8,
            -1 => instruction.opcode == OpCodes.Ldc_I4_M1,
            _ => false,
        };
    }
}
#endif
