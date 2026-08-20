using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Mono.Cecil;
using Mono.Cecil.Cil;
using StardewModdingAPI.Framework.ModLoading.Framework;

namespace StardewModdingAPI.Framework.ModLoading.Rewriters;

/// <summary>Redirects unsupported Android console-color calls made by mods.</summary>
internal sealed class ConsoleColorRewriter : BaseInstructionHandler
{
    private const string ConsoleTypeName = "System.Console";

    private readonly Dictionary<string, MethodInfo> Methods = typeof(ConsoleColorFacade)
        .GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
        .ToDictionary(method => GetKey(method.Name, method.GetParameters().Select(p => p.ParameterType.FullName!)));

    /// <summary>Construct an instance.</summary>
    public ConsoleColorRewriter()
        : base(defaultPhrase: "unsupported Android console-color call") { }

    /// <inheritdoc />
    public override bool Handle(ModuleDefinition module, ILProcessor cil, Instruction instruction)
    {
        if (
            instruction.Operand is not MethodReference method
            || method.DeclaringType.FullName != ConsoleTypeName
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
            $"Console.{method.Name} Android compatibility call"
        );
    }

    private static string GetKey(string name, IEnumerable<string> parameterTypes)
    {
        return $"{name}({string.Join(",", parameterTypes)})";
    }
}
