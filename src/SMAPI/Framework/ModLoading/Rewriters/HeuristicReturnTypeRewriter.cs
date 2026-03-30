using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
using StardewModdingAPI.Framework.ModLoading.Framework;

namespace StardewModdingAPI.Framework.ModLoading.Rewriters;

/// <summary>Automatically fix references to methods whose return type changed (e.g., List to ObservableCollection between mobile and desktop Stardew Valley).</summary>
internal class HeuristicReturnTypeRewriter : BaseInstructionHandler
{
    /// <summary>The assembly names to which to rewrite broken references.</summary>
    private readonly ISet<string> RewriteReferencesToAssemblies;

    /// <summary>Construct an instance.</summary>
    /// <param name="rewriteReferencesToAssemblies">The assembly names to which to rewrite broken references.</param>
    public HeuristicReturnTypeRewriter(ISet<string> rewriteReferencesToAssemblies)
        : base(defaultPhrase: "methods with mismatched return types")
    {
        this.RewriteReferencesToAssemblies = rewriteReferencesToAssemblies;
    }

    /// <inheritdoc />
    public override bool Handle(ModuleDefinition module, ILProcessor cil, Instruction instruction)
    {
        MethodReference? methodRef = RewriteHelper.AsMethodReference(instruction);
        if (methodRef == null || !this.ShouldValidate(methodRef.DeclaringType))
            return false;

        // skip if the reference already resolves correctly
        if (methodRef.Resolve() != null)
            return false;

        // resolve the declaring type
        TypeDefinition? type = methodRef.DeclaringType.Resolve();
        if (type == null)
            return false;

        // find a method with same name and parameters but different return type
        MethodDefinition? match = type.Methods.FirstOrDefault(m =>
            m.Name == methodRef.Name
            && m.Parameters.Count == methodRef.Parameters.Count
            && this.ParametersMatch(methodRef, m)
        );

        if (match == null)
            return false;

        // rewrite the method reference to use the actual method from the loaded assembly
        instruction.Operand = module.ImportReference(match);

        this.Phrases.Add(
            $"{methodRef.DeclaringType.Name}.{methodRef.Name} "
                + $"(return type {methodRef.ReturnType.Name} => {match.ReturnType.Name})"
        );
        return this.MarkRewritten();
    }

    /// <summary>Whether references to the given type should be validated.</summary>
    private bool ShouldValidate([NotNullWhen(true)] TypeReference? type)
    {
        return type != null && this.RewriteReferencesToAssemblies.Contains(type.Scope.Name);
    }

    /// <summary>Get whether every parameter matches between the reference and definition.</summary>
    private bool ParametersMatch(MethodReference methodRef, MethodDefinition method)
    {
        if (methodRef.Parameters.Count != method.Parameters.Count)
            return false;

        for (int i = 0; i < methodRef.Parameters.Count; i++)
        {
            if (
                !RewriteHelper.IsSameType(
                    methodRef.Parameters[i].ParameterType,
                    method.Parameters[i].ParameterType
                )
            )
                return false;
        }

        return true;
    }
}
