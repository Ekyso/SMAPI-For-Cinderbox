using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Mono.Cecil;
using Mono.Cecil.Cil;
using StardewModdingAPI.Framework.ModLoading.Framework;

namespace StardewModdingAPI.Framework.ModLoading.Rewriters;

/// <summary>Automatically fix references to fields that have been replaced by a property or const field.</summary>
internal class HeuristicFieldRewriter : BaseInstructionHandler
{
    /*********
    ** Fields
    *********/
    /// <summary>The assembly names to which to rewrite broken references.</summary>
    private readonly ISet<string> RewriteReferencesToAssemblies;

    /*********
    ** Public methods
    *********/
    /// <summary>Construct an instance.</summary>
    /// <param name="rewriteReferencesToAssemblies">The assembly names to which to rewrite broken references.</param>
    public HeuristicFieldRewriter(ISet<string> rewriteReferencesToAssemblies)
        : base(defaultPhrase: "field changed to property") // ignored since we specify phrases
    {
        this.RewriteReferencesToAssemblies = rewriteReferencesToAssemblies;
    }

    /// <inheritdoc />
    public override bool Handle(ModuleDefinition module, ILProcessor cil, Instruction instruction)
    {
        // get field ref
        FieldReference? fieldRef = RewriteHelper.AsFieldReference(instruction);
        if (fieldRef == null || !this.ShouldValidate(fieldRef.DeclaringType))
            return false;

        // skip if not broken
        FieldDefinition? fieldDefinition = fieldRef.Resolve();
        if (
            fieldDefinition?.HasConstant == false
            && RewriteHelper.HasSameNamespaceAndName(
                fieldRef.DeclaringType,
                fieldDefinition.DeclaringType
            )
        )
        {
            // field exists but type changed (e.g. IList on desktop, List on mobile)
            if (!RewriteHelper.LooksLikeSameType(fieldRef.FieldType, fieldDefinition.FieldType))
                return this.TryRewriteFieldType(module, instruction, fieldRef, fieldDefinition);

            return false;
        }

        // Resolve() can fail when the field type differs between platforms.
        // Look up the field by name on the declaring type as a fallback.
        if (fieldDefinition == null)
        {
            FieldDefinition? targetField = fieldRef
                .DeclaringType.Resolve()
                ?.Fields.FirstOrDefault(f => f.Name == fieldRef.Name);
            if (
                targetField != null
                && !RewriteHelper.LooksLikeSameType(fieldRef.FieldType, targetField.FieldType)
            )
                return this.TryRewriteFieldType(module, instruction, fieldRef, targetField);
        }

        // rewrite if possible
        TypeDefinition? declaringType = fieldRef.DeclaringType.Resolve();
        bool isRead = instruction.OpCode == OpCodes.Ldsfld || instruction.OpCode == OpCodes.Ldfld;
        return this.TryRewriteToProperty(module, instruction, fieldRef, declaringType, isRead)
            || this.TryRewriteToConstField(instruction, fieldDefinition)
            || this.TryRewriteToInheritedField(module, instruction, fieldRef, fieldDefinition);
    }

    /*********
    ** Private methods
    *********/
    /// <summary>Whether references to the given type should be validated.</summary>
    /// <param name="type">The type reference.</param>
    private bool ShouldValidate([NotNullWhen(true)] TypeReference? type)
    {
        return type != null && this.RewriteReferencesToAssemblies.Contains(type.Scope.Name);
    }

    /// <summary>Try rewriting the field into a matching property.</summary>
    /// <param name="module">The assembly module containing the instruction.</param>
    /// <param name="instruction">The CIL instruction to rewrite.</param>
    /// <param name="fieldRef">The field reference.</param>
    /// <param name="declaringType">The type on which the field was defined.</param>
    /// <param name="isRead">Whether the field is being read; else it's being written to.</param>
    private bool TryRewriteToProperty(
        ModuleDefinition module,
        Instruction instruction,
        FieldReference fieldRef,
        TypeDefinition? declaringType,
        bool isRead
    )
    {
        // get equivalent property
        MethodDefinition? accessor = null;
        if (declaringType is not null)
        {
            PropertyDefinition? targetProperty = null;
            foreach (PropertyDefinition property in declaringType.Properties)
            {
                if (property.Name == fieldRef.Name)
                {
                    targetProperty = property;
                    break;
                }
            }

            accessor = isRead ? targetProperty?.GetMethod : targetProperty?.SetMethod;
        }
        if (accessor is null)
            return false;

        // rewrite field to property
        instruction.OpCode = OpCodes.Call;
        instruction.Operand = module.ImportReference(accessor);

        this.Phrases.Add($"{fieldRef.DeclaringType.Name}.{fieldRef.Name} (field => property)");
        return this.MarkRewritten();
    }

    /// <summary>Try rewriting the field into a matching const field.</summary>
    /// <param name="instruction">The CIL instruction to rewrite.</param>
    /// <param name="field">The field definition.</param>
    private bool TryRewriteToConstField(Instruction instruction, FieldDefinition? field)
    {
        // must have been a static field read, and the new field must be const
        if (instruction.OpCode != OpCodes.Ldsfld || field?.HasConstant != true)
            return false;

        // get opcode for value type
        Instruction? loadInstruction = RewriteHelper.GetLoadValueInstruction(field.Constant);
        if (loadInstruction == null)
            return false;

        // rewrite to constant
        instruction.OpCode = loadInstruction.OpCode;
        instruction.Operand = loadInstruction.Operand;

        this.Phrases.Add($"{field.DeclaringType.Name}.{field.Name} (field => const)");
        return this.MarkRewritten();
    }

    /// <summary>Try rewriting a field reference whose type changed but is still compatible (e.g. IList → List).</summary>
    /// <param name="module">The assembly module containing the instruction.</param>
    /// <param name="instruction">The CIL instruction to rewrite.</param>
    /// <param name="fieldRef">The field reference from the mod.</param>
    /// <param name="fieldDefinition">The actual field definition at runtime.</param>
    private bool TryRewriteFieldType(
        ModuleDefinition module,
        Instruction instruction,
        FieldReference fieldRef,
        FieldDefinition fieldDefinition
    )
    {
        instruction.Operand = module.ImportReference(fieldDefinition);

        this.Phrases.Add(
            $"{fieldRef.DeclaringType.Name}.{fieldRef.Name} ({this.GetFriendlyTypeName(fieldRef.FieldType)} => {this.GetFriendlyTypeName(fieldDefinition.FieldType)})"
        );
        return this.MarkRewritten();
    }

    /// <summary>Get a shorter type name for display.</summary>
    /// <param name="type">The type reference.</param>
    private string GetFriendlyTypeName(TypeReference type)
    {
        if (type is GenericInstanceType generic)
            return $"{generic.Name}<{string.Join(", ", generic.GenericArguments.Select(a => a.Name))}>";
        return type.Name;
    }

    /// <summary>Try rewriting the field into a matching inherited field.</summary>
    /// <param name="module">The assembly module containing the instruction.</param>
    /// <param name="instruction">The CIL instruction to rewrite.</param>
    /// <param name="fieldRef">The field reference.</param>
    /// <param name="fieldDefinition">The actual field resolved by Cecil.</param>
    private bool TryRewriteToInheritedField(
        ModuleDefinition module,
        Instruction instruction,
        FieldReference fieldRef,
        FieldDefinition? fieldDefinition
    )
    {
        // skip if not resolvable
        if (fieldDefinition == null)
            return false;

        // skip if no rewrite needed
        if (
            RewriteHelper.HasSameNamespaceAndName(
                fieldRef.DeclaringType,
                fieldDefinition.DeclaringType
            )
        )
            return false;

        // skip if static (it's less intuitive that rewriting should happen)
        if (instruction.OpCode != OpCodes.Ldfld)
            return false;

        // rewrite reference
        instruction.Operand = module.ImportReference(fieldDefinition);
        fieldRef.FieldType = fieldDefinition.FieldType;

        this.Phrases.Add(
            $"{fieldRef.DeclaringType.Name}.{fieldRef.Name} -> {fieldDefinition.DeclaringType.Name}.{fieldRef.Name} (field now inherited)"
        );
        return this.MarkRewritten();
    }
}
