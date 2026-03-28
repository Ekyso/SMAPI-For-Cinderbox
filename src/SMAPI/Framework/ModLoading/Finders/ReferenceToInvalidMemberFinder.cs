using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Mono.Cecil;
using Mono.Cecil.Cil;
using StardewModdingAPI.Framework.ModLoading.Framework;

namespace StardewModdingAPI.Framework.ModLoading.Finders;

/// <summary>Finds references to a field, property, or method which either doesn't exist or returns a different type than the code expects.</summary>
/// <remarks>This implementation is purely heuristic. It should never return a false positive, but won't detect all cases.</remarks>
internal class ReferenceToInvalidMemberFinder : BaseInstructionHandler
{
    /*********
    ** Fields
    *********/
    /// <summary>The assembly names to which to heuristically detect broken references.</summary>
    private readonly ISet<string> ValidateReferencesToAssemblies;

    /// <summary>Whether to include more technical details about broken mods in the TRACE logs. This is mainly useful for creating compatibility rewriters.</summary>
    private readonly bool LogTechnicalDetailsForBrokenMods;


    /*********
    ** Public methods
    *********/
    /// <summary>Construct an instance.</summary>
    /// <param name="validateReferencesToAssemblies">The assembly names to which to heuristically detect broken references.</param>
    /// <param name="logTechnicalDetailsForBrokenMods">Whether to include more technical details about broken mods in the TRACE logs. This is mainly useful for creating compatibility rewriters.</param>
    public ReferenceToInvalidMemberFinder(ISet<string> validateReferencesToAssemblies, bool logTechnicalDetailsForBrokenMods)
        : base(defaultPhrase: "")
    {
        this.ValidateReferencesToAssemblies = validateReferencesToAssemblies;
        this.LogTechnicalDetailsForBrokenMods = logTechnicalDetailsForBrokenMods;
    }

    /// <inheritdoc />
    public override bool Handle(ModuleDefinition module, ILProcessor cil, Instruction instruction)
    {
        // field reference
        FieldReference? fieldRef = RewriteHelper.AsFieldReference(instruction);
        if (fieldRef != null && this.ShouldValidate(fieldRef.DeclaringType))
        {
            // resolve field
            FieldDefinition? targetField = null;
            if (fieldRef.DeclaringType.Resolve() is { } declaringType)
            {
                foreach (FieldDefinition field in declaringType.Fields)
                {
                    if (field.Name == fieldRef.Name)
                    {
                        targetField = field;
                        break;
                    }
                }
            }

            // detect wrong return type
            if (targetField != null && !RewriteHelper.LooksLikeSameType(fieldRef.FieldType, targetField.FieldType))
                this.MarkFlag(InstructionHandleResult.NotCompatible, $"reference to {this.GetMemberDisplayName(fieldRef)} (field returns {this.GetFriendlyTypeName(targetField.FieldType)}, not {this.GetFriendlyTypeName(fieldRef.FieldType)})");

            // detect missing
            else if (targetField == null || targetField.HasConstant || !RewriteHelper.HasSameNamespaceAndName(fieldRef.DeclaringType, targetField.DeclaringType))
                this.MarkFlag(InstructionHandleResult.NotCompatible, $"reference to {this.GetMemberDisplayName(fieldRef)} (no such field)");

            return false;
        }

        // method reference
        MethodReference? methodRef = RewriteHelper.AsMethodReference(instruction);
        if (methodRef != null && !this.IsUnsupported(methodRef) && this.ShouldValidate(methodRef.DeclaringType))
        {
            MethodDefinition? methodDef = methodRef.Resolve();

            // wrong return type
            if (methodDef != null)
            {
                if (methodRef.DeclaringType.Resolve() is { } declaringType)
                {
                    bool foundName = false;
                    bool foundMatch = true;

                    foreach (MethodDefinition method in declaringType.Methods)
                    {
                        if (method.Name != methodRef.Name)
                            continue;

                        foundName = true;
                        foundMatch = RewriteHelper.LooksLikeSameType(method.ReturnType, methodDef.ReturnType);
                        if (foundMatch)
                            break;
                    }

                    if (foundName && !foundMatch)
                        this.MarkFlag(InstructionHandleResult.NotCompatible, $"reference to {this.GetMemberDisplayName(methodDef)} (no such method returns {this.GetFriendlyTypeName(methodDef.ReturnType)})");
                }
            }

            // missing
            else if (methodDef is null)
            {
                string typeName;
                if (this.IsProperty(methodRef))
                    typeName = "property";
                else if (methodRef.Name == ".ctor")
                    typeName = "constructor";
                else
                    typeName = "method";

                this.MarkFlag(InstructionHandleResult.NotCompatible, $"reference to {this.GetMemberDisplayName(methodRef)} (no such {typeName})");
            }
        }

        return false;
    }


    /*********
    ** Private methods
    *********/
    /// <summary>Whether references to the given type should be validated.</summary>
    /// <param name="type">The type reference.</param>
    private bool ShouldValidate([NotNullWhen(true)] TypeReference? type)
    {
        return type != null && this.ValidateReferencesToAssemblies.Contains(type.Scope.Name);
    }

    /// <summary>Get whether a method reference is a special case that's not currently supported (e.g. array methods).</summary>
    /// <param name="method">The method reference.</param>
    private bool IsUnsupported(MethodReference method)
    {
        return
            method.DeclaringType.Name.Contains("["); // array methods
    }

    /// <summary>Get the member name to show in logged messages.</summary>
    /// <param name="memberRef">The member reference.</param>
    private string GetMemberDisplayName(MemberReference memberRef)
    {
        if (this.LogTechnicalDetailsForBrokenMods)
            return memberRef.FullName;

        string name = memberRef.Name;
        if (memberRef is PropertyReference)
            name = name[4..]; // remove `get_` or `set_` prefix

        return $"{memberRef.DeclaringType.FullName}.{name}";
    }

    /// <summary>Get a shorter type name for display.</summary>
    /// <param name="type">The type reference.</param>
    private string GetFriendlyTypeName(TypeReference type)
    {
        // most common built-in types
        switch (type.FullName)
        {
            case "System.Boolean":
                return "bool";
            case "System.Int32":
                return "int";
            case "System.String":
                return "string";
        }

        // most common unambiguous namespaces
        foreach (string @namespace in new[] { "Microsoft.Xna.Framework", "Netcode", "System", "System.Collections.Generic" })
        {
            if (type.Namespace == @namespace)
                return type.Name;
        }

        return type.FullName;
    }

    /// <summary>Get whether a method reference is a property getter or setter.</summary>
    /// <param name="method">The method reference.</param>
    private bool IsProperty(MethodReference method)
    {
        return method.Name.StartsWith("get_") || method.Name.StartsWith("set_");
    }
}
