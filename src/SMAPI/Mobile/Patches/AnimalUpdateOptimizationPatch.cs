#if SMAPI_FOR_ANDROID
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Netcode;
using StardewValley;
using StardewValley.Buildings;
using StardewValley.Network;

namespace StardewModdingAPI.Mobile.Patches;

/// <summary>
/// Replaces the per-frame ToArray() allocation in the animal update loop with a
/// reusable thread-static buffer.
/// </summary>
internal static class AnimalUpdateOptimizationPatch
{
    /// <summary>Apply the animal update optimization patch.</summary>
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
                    "[AnimalUpdateOptimizationPatch] Could not find updateEvenIfFarmerIsntHere"
                );
                return;
            }

            var transpiler = new HarmonyMethod(
                typeof(AnimalUpdateOptimizationPatch).GetMethod(
                    nameof(Transpiler),
                    BindingFlags.NonPublic | BindingFlags.Static
                )
            );

            harmony.Patch(target, transpiler: transpiler);
            AndroidLogger.Log("[AnimalUpdateOptimizationPatch] Applied animal update optimization");
        }
        catch (Exception ex)
        {
            AndroidLogger.Log($"[AnimalUpdateOptimizationPatch] Failed to apply: {ex}");
        }
    }

    /// <summary>
    /// Transpiler that replaces the ToArray + update loop with a call to
    /// <see cref="UpdateAnimalsNotCurrentLocation"/>. Anchors on Enumerable.ToArray&lt;FarmAnimal&gt;.
    /// </summary>
    private static IEnumerable<CodeInstruction> Transpiler(
        IEnumerable<CodeInstruction> instructions
    )
    {
        var codes = new List<CodeInstruction>(instructions);

        var animalsField = AccessTools.Field(typeof(GameLocation), "animals");
        var toArrayMethod = typeof(Enumerable).GetMethod("ToArray")?.MakeGenericMethod(typeof(FarmAnimal));
        var updateWhenNotMethod = AccessTools.Method(
            typeof(FarmAnimal),
            "updateWhenNotCurrentLocation",
            new[] { typeof(Building), typeof(GameTime), typeof(GameLocation) }
        );
        var helperMethod = AccessTools.Method(
            typeof(AnimalUpdateOptimizationPatch),
            nameof(UpdateAnimalsNotCurrentLocation)
        );

        if (animalsField == null || toArrayMethod == null || updateWhenNotMethod == null || helperMethod == null)
        {
            AndroidLogger.Log(
                "[AnimalUpdateOptimizationPatch] Could not resolve required members: "
                + $"animals={animalsField != null}, ToArray={toArrayMethod != null}, "
                + $"updateWhenNot={updateWhenNotMethod != null}, helper={helperMethod != null}"
            );
            return codes;
        }

        int toArrayIdx = -1;
        for (int i = 0; i < codes.Count; i++)
        {
            if (codes[i].Calls(toArrayMethod))
            {
                toArrayIdx = i;
                break;
            }
        }

        if (toArrayIdx == -1)
        {
            AndroidLogger.Log("[AnimalUpdateOptimizationPatch] Could not find ToArray<FarmAnimal> call in IL");
            return codes;
        }

        // scan backward from ToArray for ldarg.0 → ldfld animals (block start)
        int blockStart = -1;
        for (int i = toArrayIdx - 1; i >= 1; i--)
        {
            if (codes[i].LoadsField(animalsField) && codes[i - 1].IsLdarg(0))
            {
                blockStart = i - 1;
                break;
            }
        }

        if (blockStart == -1)
        {
            AndroidLogger.Log("[AnimalUpdateOptimizationPatch] Could not find ldfld animals pattern before ToArray");
            return codes;
        }

        // find the containingBuilding local (stloc after ldfld ParentBuilding)
        var parentBuildingField = AccessTools.Field(typeof(GameLocation), "ParentBuilding");
        int containingBuildingLocalIdx = -1;
        for (int i = blockStart - 1; i >= 2; i--)
        {
            if (codes[i].IsStloc()
                && codes[i - 1].LoadsField(parentBuildingField)
                && codes[i - 2].IsLdarg(0))
            {
                containingBuildingLocalIdx = GetLocalIndex(codes[i]);
                break;
            }
        }

        if (containingBuildingLocalIdx == -1)
        {
            AndroidLogger.Log("[AnimalUpdateOptimizationPatch] Could not find containingBuilding local store");
            return codes;
        }

        // find loop end: backward branch after updateWhenNotCurrentLocation
        int updateCallIdx = -1;
        for (int i = toArrayIdx + 1; i < codes.Count; i++)
        {
            if (codes[i].Calls(updateWhenNotMethod))
            {
                updateCallIdx = i;
                break;
            }
        }

        if (updateCallIdx == -1)
        {
            AndroidLogger.Log("[AnimalUpdateOptimizationPatch] Could not find updateWhenNotCurrentLocation call");
            return codes;
        }

        int loopEnd = -1;
        for (int i = updateCallIdx + 1; i < codes.Count; i++)
        {
            if (codes[i].Branches(out Label? targetLabel) && targetLabel.HasValue)
            {
                // Check if it branches backward (target is before current position)
                for (int j = blockStart; j < i; j++)
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
            AndroidLogger.Log("[AnimalUpdateOptimizationPatch] Could not find loop end branch");
            return codes;
        }

        var firstLabels = new List<Label>(codes[blockStart].labels);

        var replacement = new List<CodeInstruction>
        {
            new CodeInstruction(OpCodes.Ldarg_0) { labels = firstLabels },
            new CodeInstruction(OpCodes.Ldfld, animalsField),
            EmitLdloc(containingBuildingLocalIdx),
            new CodeInstruction(OpCodes.Ldarg_1),
            new CodeInstruction(OpCodes.Ldarg_0),
            new CodeInstruction(OpCodes.Call, helperMethod),
        };

        int removeCount = loopEnd - blockStart + 1;
        codes.RemoveRange(blockStart, removeCount);
        codes.InsertRange(blockStart, replacement);

        AndroidLogger.Log(
            $"[AnimalUpdateOptimizationPatch] Transpiler: replaced {removeCount} IL instructions with {replacement.Count}"
        );

        return codes;
    }

    /// <summary>Reusable buffer for animal references. Grows as needed, never shrinks.</summary>
    [ThreadStatic]
    private static FarmAnimal[]? _animalBuffer;

    /// <summary>
    /// Update animals that are not in the current location, using a cached buffer
    /// instead of allocating a new array via ToArray() each frame.
    /// </summary>
    public static void UpdateAnimalsNotCurrentLocation(
        NetLongDictionary<FarmAnimal, NetRef<FarmAnimal>> animals,
        Building containingBuilding,
        GameTime time,
        GameLocation environment)
    {
        int count = animals.Length;
        if (count == 0)
            return;

        if (_animalBuffer == null || _animalBuffer.Length < count)
            _animalBuffer = new FarmAnimal[Math.Max(count, 16)];

        int idx = 0;
        foreach (var animal in animals.Values)
        {
            if (idx >= _animalBuffer.Length)
                break;
            _animalBuffer[idx++] = animal;
        }

        for (int i = 0; i < idx; i++)
            _animalBuffer[i].updateWhenNotCurrentLocation(containingBuilding, time, environment);

        Array.Clear(_animalBuffer, 0, idx);
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
