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
/// Replaces the Contains + Remove pattern in the delayed action loop with positional
/// removal to avoid two O(n) scans per completed action.
/// </summary>
internal static class DelayedActionOptimizationPatch
{
    /// <summary>Apply the delayed action optimization patch.</summary>
    public static void Apply(Harmony harmony)
    {
        try
        {
            var target = AccessTools.Method(
                typeof(Game1),
                "UpdateOther",
                new[] { typeof(GameTime) }
            );

            if (target == null)
            {
                AndroidLogger.Log(
                    "[DelayedActionOptimizationPatch] Could not find Game1.UpdateOther"
                );
                return;
            }

            var transpiler = new HarmonyMethod(
                typeof(DelayedActionOptimizationPatch).GetMethod(
                    nameof(Transpiler),
                    BindingFlags.NonPublic | BindingFlags.Static
                )
            );

            harmony.Patch(target, transpiler: transpiler);
            AndroidLogger.Log("[DelayedActionOptimizationPatch] Applied delayed action optimization");
        }
        catch (Exception ex)
        {
            AndroidLogger.Log($"[DelayedActionOptimizationPatch] Failed to apply: {ex}");
        }
    }

    /// <summary>
    /// Transpiler that replaces the Contains + Remove block with a single
    /// <see cref="RemoveCompletedAction"/> call.
    /// </summary>
    private static IEnumerable<CodeInstruction> Transpiler(
        IEnumerable<CodeInstruction> instructions
    )
    {
        var codes = new List<CodeInstruction>(instructions);

        var delayedActionsField = AccessTools.Field(typeof(Game1), "delayedActions");
        var containsMethod = typeof(ICollection<DelayedAction>).GetMethod("Contains");
        var removeMethod = typeof(ICollection<DelayedAction>).GetMethod("Remove");
        var helperMethod = AccessTools.Method(
            typeof(DelayedActionOptimizationPatch),
            nameof(RemoveCompletedAction)
        );

        if (delayedActionsField == null || containsMethod == null || removeMethod == null || helperMethod == null)
        {
            AndroidLogger.Log(
                "[DelayedActionOptimizationPatch] Could not resolve required members: "
                + $"delayedActions={delayedActionsField != null}, Contains={containsMethod != null}, "
                + $"Remove={removeMethod != null}, helper={helperMethod != null}"
            );
            return codes;
        }

        int containsIdx = -1;
        for (int i = 0; i < codes.Count; i++)
        {
            if (codes[i].Calls(containsMethod))
            {
                for (int j = i - 1; j >= Math.Max(0, i - 3); j--)
                {
                    if (codes[j].LoadsField(delayedActionsField))
                    {
                        containsIdx = i;
                        break;
                    }
                }
                if (containsIdx != -1)
                    break;
            }
        }

        if (containsIdx == -1)
        {
            AndroidLogger.Log("[DelayedActionOptimizationPatch] Could not find Contains call on delayedActions");
            return codes;
        }

        int removeIdx = -1;
        for (int i = containsIdx + 1; i < Math.Min(containsIdx + 10, codes.Count); i++)
        {
            if (codes[i].Calls(removeMethod))
            {
                removeIdx = i;
                break;
            }
        }

        if (removeIdx == -1)
        {
            AndroidLogger.Log("[DelayedActionOptimizationPatch] Could not find Remove call after Contains");
            return codes;
        }

        int blockStart = -1;
        for (int i = containsIdx - 1; i >= Math.Max(0, containsIdx - 3); i--)
        {
            if (codes[i].LoadsField(delayedActionsField))
            {
                blockStart = i;
                break;
            }
        }

        if (blockStart == -1)
        {
            AndroidLogger.Log("[DelayedActionOptimizationPatch] Could not find block start (ldsfld delayedActions)");
            return codes;
        }

        int blockEnd = removeIdx;
        if (removeIdx + 1 < codes.Count && codes[removeIdx + 1].opcode == OpCodes.Pop)
        {
            blockEnd = removeIdx + 1;
        }

        Label? skipLabel = null;
        int brfalseIdx = -1;
        for (int i = containsIdx + 1; i < removeIdx; i++)
        {
            if (codes[i].opcode == OpCodes.Brfalse || codes[i].opcode == OpCodes.Brfalse_S)
            {
                brfalseIdx = i;
                skipLabel = (Label)codes[i].operand;
                break;
            }
        }

        int actionLocalIdx = -1;
        for (int i = blockStart + 1; i < containsIdx; i++)
        {
            if (IsLdloc(codes[i]))
            {
                actionLocalIdx = GetLocalIndex(codes[i]);
                break;
            }
        }

        if (actionLocalIdx == -1)
        {
            AndroidLogger.Log("[DelayedActionOptimizationPatch] Could not find action local variable");
            return codes;
        }

        // find loop counter local by matching the ldsfld delayedActions → ldloc(i) pattern
        int loopCounterLocalIdx = -1;
        for (int i = blockStart - 1; i >= Math.Max(0, blockStart - 15); i--)
        {
            if (codes[i].LoadsField(delayedActionsField) && i + 1 < codes.Count && IsLdloc(codes[i + 1]))
            {
                loopCounterLocalIdx = GetLocalIndex(codes[i + 1]);
                break;
            }
        }

        if (loopCounterLocalIdx == -1)
        {
            AndroidLogger.Log("[DelayedActionOptimizationPatch] Could not find loop counter local variable");
            return codes;
        }

        var firstLabels = new List<Label>(codes[blockStart].labels);

        var replacement = new List<CodeInstruction>
        {
            new CodeInstruction(OpCodes.Ldsfld, delayedActionsField) { labels = firstLabels },
            EmitLdloc(actionLocalIdx),
            EmitLdloc(loopCounterLocalIdx),
            new CodeInstruction(OpCodes.Call, helperMethod),
        };

        if (skipLabel.HasValue && blockEnd + 1 < codes.Count)
        {
            codes[blockEnd + 1].labels.Add(skipLabel.Value);
        }

        int removeCount = blockEnd - blockStart + 1;
        codes.RemoveRange(blockStart, removeCount);
        codes.InsertRange(blockStart, replacement);

        AndroidLogger.Log(
            $"[DelayedActionOptimizationPatch] Transpiler: replaced {removeCount} IL instructions with {replacement.Count}"
        );

        return codes;
    }

    /// <summary>Remove a completed action by index if it hasn't moved, otherwise fall back to linear search.</summary>
    public static void RemoveCompletedAction(IList<DelayedAction> list, DelayedAction action, int index)
    {
        if (index >= 0 && index < list.Count && ReferenceEquals(list[index], action))
        {
            list.RemoveAt(index);
            return;
        }

        if (list is List<DelayedAction> concrete)
        {
            concrete.Remove(action);
        }
        else
        {
            for (int i = list.Count - 1; i >= 0; i--)
            {
                if (ReferenceEquals(list[i], action))
                {
                    list.RemoveAt(i);
                    return;
                }
            }
        }
    }

    /// <summary>Check if an instruction is a ldloc variant.</summary>
    private static bool IsLdloc(CodeInstruction instruction)
    {
        return instruction.opcode == OpCodes.Ldloc_0
            || instruction.opcode == OpCodes.Ldloc_1
            || instruction.opcode == OpCodes.Ldloc_2
            || instruction.opcode == OpCodes.Ldloc_3
            || instruction.opcode == OpCodes.Ldloc_S
            || instruction.opcode == OpCodes.Ldloc;
    }

    /// <summary>Get the local variable index from a stloc/ldloc instruction.</summary>
    private static int GetLocalIndex(CodeInstruction instruction)
    {
        if (instruction.opcode == OpCodes.Stloc_0 || instruction.opcode == OpCodes.Ldloc_0) return 0;
        if (instruction.opcode == OpCodes.Stloc_1 || instruction.opcode == OpCodes.Ldloc_1) return 1;
        if (instruction.opcode == OpCodes.Stloc_2 || instruction.opcode == OpCodes.Ldloc_2) return 2;
        if (instruction.opcode == OpCodes.Stloc_3 || instruction.opcode == OpCodes.Ldloc_3) return 3;
        if (instruction.opcode == OpCodes.Stloc_S || instruction.opcode == OpCodes.Ldloc_S)
            return instruction.operand is LocalBuilder lb ? lb.LocalIndex : Convert.ToInt32(instruction.operand);
        if (instruction.opcode == OpCodes.Stloc || instruction.opcode == OpCodes.Ldloc)
            return instruction.operand is LocalBuilder lb ? lb.LocalIndex : Convert.ToInt32(instruction.operand);
        return -1;
    }

    /// <summary>Emit the appropriate ldloc instruction for a given local index.</summary>
    private static CodeInstruction EmitLdloc(int index)
    {
        return index switch
        {
            0 => new CodeInstruction(OpCodes.Ldloc_0),
            1 => new CodeInstruction(OpCodes.Ldloc_1),
            2 => new CodeInstruction(OpCodes.Ldloc_2),
            3 => new CodeInstruction(OpCodes.Ldloc_3),
            _ when index <= 255 => new CodeInstruction(OpCodes.Ldloc_S, (byte)index),
            _ => new CodeInstruction(OpCodes.Ldloc, index),
        };
    }
}
#endif
