using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Mono.Cecil;
using Mono.Cecil.Cil;
using StardewModdingAPI.Framework.ModLoading.Framework;

namespace StardewModdingAPI.Framework.ModLoading.Rewriters;

/// <summary>Redirects a mod's operating-system checks to the selected game platform.</summary>
internal sealed class OperatingSystemRewriter : BaseInstructionHandler
{
    private const string OperatingSystemTypeName = "System.OperatingSystem";

    /// <summary>Facade methods indexed by their method name and parameter types.</summary>
    private readonly Dictionary<string, MethodInfo> Methods = typeof(OperatingSystemFacade)
        .GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
        .ToDictionary(method => GetKey(method.Name, method.GetParameters().Select(p => p.ParameterType.FullName!)));

    /// <summary>Construct an instance.</summary>
    public OperatingSystemRewriter()
        : base(defaultPhrase: "operating-system platform check") { }

    /// <inheritdoc />
    public override bool Handle(ModuleDefinition module, ILProcessor cil, Instruction instruction)
    {
        if (
            instruction.Operand is not MethodReference method
            || method.DeclaringType.FullName != OperatingSystemTypeName
        )
        {
            return false;
        }

        string key = GetKey(method.Name, method.Parameters.Select(p => p.ParameterType.FullName));
        if (!this.Methods.TryGetValue(key, out MethodInfo? replacement))
            return false;

        instruction.Operand = module.ImportReference(replacement);
        if (instruction.OpCode == OpCodes.Callvirt)
            instruction.OpCode = OpCodes.Call;

        return this.MarkFlag(
            InstructionHandleResult.Rewritten,
            $"OperatingSystem.{method.Name} platform check"
        );
    }

    /// <summary>Get a stable lookup key for a method signature.</summary>
    private static string GetKey(string name, IEnumerable<string> parameterTypes)
    {
        return $"{name}({string.Join(",", parameterTypes)})";
    }
}
