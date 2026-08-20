using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Mono.Cecil;
using Mono.Cecil.Cil;
using StardewModdingAPI.Framework.ModLoading.Framework;

namespace StardewModdingAPI.Framework.ModLoading.Rewriters;

/// <summary>Rewrites references to types or type members so they point to a different type or member.</summary>
/// <remarks>
///   <para>
///     When a type or member was moved/renamed as-is in the game code, you can map it directly using methods like
///     <see cref="MapField"/>.
///   </para>
///
///   <para>
///     Otherwise you can use <see cref="MapFacade"/> to create a 'facade' type. This is a type which subclasses
///     the original type (if needed), and provides any number of members with the old signatures which internally
///     call the new members.
///   </para>
///
///   <para>Member mappings are only used in cases where the reference to the original member can't be resolved.</para>
///
///   <para>
///     To auto-map members to a facade type:
///     <list type="bullet">
///       <item>
///         <strong>Field</strong>: add a public property with the old name and type. Field reads/writes will be
///         auto-mapped to the property's get/set methods, if present. If a get/set method isn't present, that
///         reference will be kept as-is (so you can rewrite only reads or only writes if needed).
///       </item>
///       <item>
///         <strong>Property</strong> or <strong>Method</strong>: add a public property/method with the old name
///         and signature.
///       </item>
///       <item>
///         <strong>Constructor:</strong> add a public constructor with the old signature, <em>OR</em> a public
///         static method named 'Constructor' with the old signature which returns an instance of the intended
///         type. The latter avoids returning a facade instance to the calling code, which can cause errors (e.g.
///         when trying to write it to the save file).</item>
///     </list>
///   </para>
///
///   <para>
///     When adding a facade for a type with a required constructor, you'll need a constructor on the facade type.
///     This should be private and will never be called (unless you want to rewrite references to the original
///     constructors per the above). You can call <see cref="RewriteHelper.ThrowFakeConstructorCalled"/> in the
///     private constructor to enforce that facades aren't constructed manually.
///   </para>
/// </remarks>
internal class ReplaceReferencesRewriter : BaseInstructionHandler
{
    /*********
    ** Fields
    *********/
    /// <summary>The new types to reference, indexed by the old type's full name.</summary>
    private readonly Dictionary<string, Type> TypeMap = new();

    /// <summary>The new members to reference, indexed by the old member's full name.</summary>
    private readonly Dictionary<string, MemberInfo> MemberMap = new();

    /// <summary>The mapped members which should be rewritten even when they exist in the runtime assembly.</summary>
    private readonly HashSet<string> RewriteResolvedMembers = new();

    /*********
    ** Public methods
    *********/
    /// <summary>Construct an instance.</summary>
    public ReplaceReferencesRewriter()
        : base(defaultPhrase: "type or member reference") { } // overridden by this.Phrases when a reference is replaced

    /****
    ** Management
    ****/
    /// <summary>Rewrite type references to point to another type.</summary>
    /// <param name="fromFullName">The full type name, like <c>Microsoft.Xna.Framework.Vector2</c>.</param>
    /// <param name="toType">The new type to reference.</param>
    public ReplaceReferencesRewriter MapType(string fromFullName, Type toType)
    {
        // validate parameters
        if (string.IsNullOrWhiteSpace(fromFullName))
            throw new ArgumentException(
                "Can't replace a type given an empty name.",
                nameof(fromFullName)
            );
        if (toType is null)
            throw new ArgumentException(
                "Can't replace a type given a null target type.",
                nameof(toType)
            );

        // add mapping
        if (!this.TypeMap.TryAdd(fromFullName, toType))
            throw new InvalidOperationException($"The '{fromFullName}' type is already mapped.");

        return this;
    }

    /// <summary>Rewrite field references to point to another field with the same field type (not necessarily on the same parent).</summary>
    /// <param name="fromFullName">The full field name, like <c>Microsoft.Xna.Framework.Vector2 StardewValley.Character::Tile</c>.</param>
    /// <param name="toType">The new type which will have the field.</param>
    /// <param name="toName">The new field name to reference.</param>
    public ReplaceReferencesRewriter MapField(string fromFullName, Type toType, string toName)
    {
        // validate parameters
        if (string.IsNullOrWhiteSpace(fromFullName))
            throw new ArgumentException(
                "Can't replace a field given an empty name.",
                nameof(fromFullName)
            );
        if (toType is null)
            throw new ArgumentException(
                "Can't replace a field given a null target type.",
                nameof(toType)
            );
        if (string.IsNullOrWhiteSpace(toName))
            throw new ArgumentException(
                "Can't replace a field given an empty target name.",
                nameof(toName)
            );

        // get field
        FieldInfo? toField;
        try
        {
            toField = toType.GetField(toName);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Required field {toType.FullName}::{toName} could not be loaded.",
                ex
            );
        }
        if (toField is null)
            throw new InvalidOperationException(
                $"Required field {toType.FullName}::{toName} could not be found."
            );

        // add mapping
        return this.MapMember(fromFullName, toField, "field");
    }

    /// <summary>Rewrite field references to point to another field with the field and parent type.</summary>
    /// <param name="type">The type which has the old and new fields.</param>
    /// <param name="fromName">The field name.</param>
    /// <param name="toName">The new field name to reference.</param>
    public ReplaceReferencesRewriter MapFieldName(Type type, string fromName, string toName)
    {
        // validate parameters
        if (type is null)
            throw new ArgumentException(
                "Can't replace a field given a null target type.",
                nameof(type)
            );
        if (string.IsNullOrWhiteSpace(fromName))
            throw new ArgumentException(
                "Can't replace a field given an empty name.",
                nameof(fromName)
            );
        if (string.IsNullOrWhiteSpace(toName))
            throw new ArgumentException(
                "Can't replace a field given an empty target name.",
                nameof(toName)
            );

        // get field
        FieldInfo? toField;
        try
        {
            toField = type.GetField(toName);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Required field {type.FullName}::{toName} could not be loaded.",
                ex
            );
        }
        if (toField is null)
            throw new InvalidOperationException(
                $"Required field {type.FullName}::{toName} could not be found."
            );

        // add mapping
        string fromFullName =
            $"{this.FormatCecilType(toField.FieldType)} {this.FormatCecilType(type)}::{fromName}";
        return this.MapMember(fromFullName, toField, "field");
    }

    /// <summary>Rewrite field references to point to a property with the same return type.</summary>
    /// <param name="fromFullName">The full field name, like <c>Microsoft.Xna.Framework.Vector2 StardewValley.Character::Tile</c>.</param>
    /// <param name="toType">The new type which will have the field.</param>
    /// <param name="toName">The new field name to reference.</param>
    public ReplaceReferencesRewriter MapFieldToProperty(
        string fromFullName,
        Type toType,
        string toName
    )
    {
        // validate parameters
        if (string.IsNullOrWhiteSpace(fromFullName))
            throw new ArgumentException(
                "Can't replace a field given an empty name.",
                nameof(fromFullName)
            );
        if (toType is null)
            throw new ArgumentException(
                "Can't replace a field given a null target type.",
                nameof(toType)
            );
        if (string.IsNullOrWhiteSpace(toName))
            throw new ArgumentException(
                "Can't replace a field given an empty target name.",
                nameof(toType)
            );

        // get field
        PropertyInfo? toProperty;
        try
        {
            toProperty = toType.GetProperty(toName);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Required property {toType.FullName}::{toName} could not be loaded.",
                ex
            );
        }
        if (toProperty is null)
            throw new InvalidOperationException(
                $"Required property {toType.FullName}::{toName} could not be found."
            );

        // add mapping
        return this.MapMember(fromFullName, toProperty, "field-to-property");
    }

    /// <summary>Rewrite method references to point to another method with the same signature.</summary>
    /// <param name="fromFullName">The full method name, like <c>Microsoft.Xna.Framework.Vector2 StardewValley.Character::getTileLocation()</c>.</param>
    /// <param name="toType">The new type which will have the method.</param>
    /// <param name="toName">The new method name to reference.</param>
    /// <param name="parameterTypes">The method's parameter types to disambiguate between overloads, if needed.</param>
    public ReplaceReferencesRewriter MapMethod(
        string fromFullName,
        Type toType,
        string toName,
        Type[]? parameterTypes = null
    )
    {
        // validate parameters
        if (string.IsNullOrWhiteSpace(fromFullName))
            throw new ArgumentException(
                "Can't replace a field given an empty name.",
                nameof(fromFullName)
            );
        if (toType is null)
            throw new ArgumentException(
                "Can't replace a field given a null target type.",
                nameof(toType)
            );
        if (string.IsNullOrWhiteSpace(toName))
            throw new ArgumentException(
                "Can't replace a field given an empty target name.",
                nameof(toType)
            );

        // get method
        MethodInfo? method;
        try
        {
            method = parameterTypes is not null
                ? toType.GetMethod(toName, parameterTypes)
                : toType.GetMethod(toName);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Required method {toType.FullName}::{toName} could not be loaded.",
                ex
            );
        }
        if (method is null)
            throw new InvalidOperationException(
                $"Required method {toType.FullName}::{toName} could not be found."
            );

        // add mapping
        return this.MapMember(fromFullName, method, "method");
    }

    /// <summary>Rewrite field, property, constructor, and method references to point to a matching equivalent on another class.</summary>
    /// <typeparam name="TFromType">The type to which references should be rewritten.</typeparam>
    /// <typeparam name="TFacade">The facade type to which to point matching references.</typeparam>
    /// <param name="mapDefaultConstructor">If the facade has a public constructor with no parameters, whether to rewrite references to empty constructors to use that one. (This is needed because .NET has no way to distinguish between an implicit and explicit constructor.)</param>
    /// <param name="rewriteResolvedMembers">Whether mapped members should be rewritten even if they exist in the runtime assembly. This is intended for platform members which resolve successfully but don't provide usable values.</param>
    public ReplaceReferencesRewriter MapFacade<TFromType, TFacade>(
        bool mapDefaultConstructor = false,
        bool rewriteResolvedMembers = false
    )
        where TFacade : TFromType, IRewriteFacade
    {
        if (typeof(IRewriteFacade).IsAssignableFrom(typeof(TFromType)))
            throw new InvalidOperationException("Can't rewrite a rewrite facade.");

        return this.MapFacade(
            typeof(TFromType).FullName!,
            typeof(TFacade),
            mapDefaultConstructor,
            rewriteResolvedMembers
        );
    }

    /// <summary>Rewrite field, property, constructor, and method references to point to a matching equivalent on another class.</summary>
    /// <param name="fromTypeName">The full name of the type to which references should be rewritten.</param>
    /// <param name="toType">The facade type to which to point matching references.</param>
    /// <param name="mapDefaultConstructor">If the facade has a public constructor with no parameters, whether to rewrite references to empty constructors to use that one. (This is needed because .NET has no way to distinguish between an implicit and explicit constructor.)</param>
    /// <param name="rewriteResolvedMembers">Whether mapped members should be rewritten even if they exist in the runtime assembly. This is intended for platform members which resolve successfully but don't provide usable values.</param>
    public ReplaceReferencesRewriter MapFacade(
        string fromTypeName,
        Type toType,
        bool mapDefaultConstructor = false,
        bool rewriteResolvedMembers = false
    )
    {
        // properties
        foreach (
            PropertyInfo property in toType.GetProperties(
                BindingFlags.Public
                    | BindingFlags.Instance
                    | BindingFlags.Static
                    | BindingFlags.DeclaredOnly
            )
        )
        {
            string propertyType = this.FormatCecilType(property.PropertyType);

            // add getter
            MethodInfo? get = property.GetMethod;
            if (get is not null)
                this.MapMember(
                    $"{propertyType} {fromTypeName}::get_{property.Name}()",
                    get,
                    "method",
                    rewriteResolvedMembers
                );

            // add setter
            MethodInfo? set = property.SetMethod;
            if (set is not null)
                this.MapMember(
                    $"System.Void {fromTypeName}::set_{property.Name}({propertyType})",
                    set,
                    "method",
                    rewriteResolvedMembers
                );

            // add field => property
            this.MapMember(
                $"{propertyType} {fromTypeName}::{property.Name}",
                property,
                "field-to-property",
                rewriteResolvedMembers
            );
        }

        // methods
        foreach (
            MethodInfo method in toType.GetMethods(
                BindingFlags.Public
                    | BindingFlags.Instance
                    | BindingFlags.Static
                    | BindingFlags.DeclaredOnly
            )
        )
        {
            if (method.Name.StartsWith("get_") || method.Name.StartsWith("set_"))
                continue; // handled via properties above

            // map method
            {
                string fromFullName =
                    $"{this.FormatCecilType(method.ReturnType)} {fromTypeName}::{method.Name}({this.FormatCecilParameterList(method.GetParameters())})";
                this.MapMember(fromFullName, method, "method", rewriteResolvedMembers);
            }

            // map constructor to static methods
            if (method is { IsStatic: true, Name: "Constructor" })
            {
                string fromFullName =
                    $"System.Void {fromTypeName}::.ctor({this.FormatCecilParameterList(method.GetParameters())})";
                this.MapMember(fromFullName, method, "method", rewriteResolvedMembers);
            }
        }

        // constructors
        ConstructorInfo[] constructors = toType.GetConstructors(
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly
        );
        foreach (ConstructorInfo constructor in constructors)
        {
            ParameterInfo[] parameters = constructor.GetParameters();
            string fromFullName =
                $"System.Void {fromTypeName}::.ctor({this.FormatCecilParameterList(parameters)})";

            if (!mapDefaultConstructor && parameters.Length == 0)
                continue;

            this.MapMember(
                fromFullName,
                constructor,
                "constructor",
                rewriteResolvedMembers
            );
        }

        return this;
    }

    /// <summary>Conditionally apply additional mappings.</summary>
    /// <param name="condition">Whether to apply the mappings.</param>
    /// <param name="configure">The mappings to apply if the condition is true.</param>
    public ReplaceReferencesRewriter When(
        bool condition,
        Action<ReplaceReferencesRewriter> configure
    )
    {
        if (condition)
            configure(this);
        return this;
    }

    /****
    ** Handlers
    ****/
    /// <inheritdoc />
    public override bool Handle(
        ModuleDefinition module,
        TypeReference type,
        Action<TypeReference> replaceWith
    )
    {
        if (this.TypeMap.TryGetValue(type.FullName, out Type? newType))
        {
            replaceWith(module.ImportReference(newType));

            this.Phrases.Add($"{type.FullName} type");
            return this.MarkRewritten();
        }

        return false;
    }

    /// <inheritdoc />
    public override bool Handle(ModuleDefinition module, ILProcessor cil, Instruction instruction)
    {
        if (instruction.Operand is not MemberReference fromMember)
            return false;

        // get target member
        string mappedMemberName = fromMember.FullName;
        if (!this.MemberMap.TryGetValue(mappedMemberName, out MemberInfo? mappedToMethod))
        {
            // If this is a generic type, there's two cases where the above might not match:
            //   1. we mapped an open generic type like "Netcode.NetFieldBase`2::op_Implicit" without specific
            //      generic types;
            //   2. or due to Cecil's odd generic type handling, which can result in type names like
            //      "Netcode.NetFieldBase`2<!0,!1>".
            //
            // In either case, we can check for a mapping registered using the simple generic name like
            // "Netcode.NetFieldBase`2" (without type args) by using `GetElementType().FullName` instead.
            if (fromMember.DeclaringType is not GenericInstanceType)
                return false;
            mappedMemberName =
                $"{fromMember.DeclaringType.GetElementType().FullName}::{fromMember.Name}";
            if (!this.MemberMap.TryGetValue(mappedMemberName, out mappedToMethod))
                return false;
        }

        // apply options
        if (
            !this.RewriteResolvedMembers.Contains(mappedMemberName)
            && fromMember.Resolve() is not null
        )
            return false;

        // apply
        switch (mappedToMethod)
        {
            // constructor
            case ConstructorInfo toConstructor:
                instruction.Operand = module.ImportReference(toConstructor);
                return this.OnRewritten(fromMember, "constructor");

            // method
            case MethodInfo toMethod:
                // resolve generic method to a specific implementation
                if (
                    toMethod.DeclaringType?.IsGenericTypeDefinition is true
                    && fromMember.DeclaringType is GenericInstanceType generic
                )
                {
                    Type[] genericArgTypes = new Type[generic.GenericArguments.Count];
                    for (int i = 0; i < generic.GenericArguments.Count; i++)
                    {
                        Type? type = RewriteHelper.GetCSharpType(generic.GenericArguments[i]);
                        if (type is null)
                            return false;

                        genericArgTypes[i] = type;
                    }

                    MethodInfo? newMethod = toMethod
                        .DeclaringType.MakeGenericType(genericArgTypes!)
                        ?.GetMethod(toMethod.Name);
                    if (newMethod is null)
                        return false;

                    toMethod = newMethod;
                }

                // An instance method-group or constrained interface call has a different stack
                // contract from the static helper facades used by some mappings. Leave those
                // unresolved instead of emitting invalid IL; they need a dedicated, proven
                // transform if a concrete mod requires them.
                bool mapsInstanceToStatic =
                    toMethod.IsStatic && fromMember is MethodReference { HasThis: true };
                if (
                    mapsInstanceToStatic
                    && (
                        instruction.OpCode == OpCodes.Ldvirtftn
                        || (
                            instruction.OpCode == OpCodes.Callvirt
                            && instruction.Previous?.OpCode == OpCodes.Constrained
                        )
                    )
                )
                {
                    return false;
                }

                // rewrite
                instruction.Operand = module.ImportReference(toMethod);

                if (instruction.OpCode == OpCodes.Newobj) // rewriting constructor to static method
                    instruction.OpCode = OpCodes.Call;
                else if (instruction.OpCode == OpCodes.Callvirt && toMethod.IsStatic)
                    instruction.OpCode = OpCodes.Call;

                return this.OnRewritten(fromMember, "method");

            // field
            case FieldInfo toField:
                instruction.Operand = module.ImportReference(toField);
                return this.OnRewritten(fromMember, "field");

            // field to property
            // (property-to-property is handled as a method)
            case PropertyInfo toProperty:
                {
                    MethodInfo? toPropMethod = null;
                    if (instruction.OpCode == OpCodes.Ldfld || instruction.OpCode == OpCodes.Ldsfld)
                        toPropMethod = toProperty.GetMethod;
                    else if (
                        instruction.OpCode == OpCodes.Stfld
                        || instruction.OpCode == OpCodes.Stsfld
                    )
                        toPropMethod = toProperty.SetMethod;

                    if (toPropMethod is not null)
                    {
                        instruction.OpCode = OpCodes.Call;
                        instruction.Operand = module.ImportReference(toPropMethod);
                        return this.OnRewritten(fromMember, "field");
                    }
                }
                break;
        }

        return false;
    }

    /*********
    ** Private methods
    *********/
    /// <summary>Add a member to replace.</summary>
    /// <param name="fromFullName">The full member name, like <c>Microsoft.Xna.Framework.Vector2 StardewValley.Character::getTileLocation()</c>.</param>
    /// <param name="toMember">The new member to reference.</param>
    /// <param name="typeLabel">A human-readable label for the reference type, like 'field' or 'method'.</param>
    /// <param name="rewriteResolved">Whether to rewrite the member even if it resolves in the runtime assembly.</param>
    private ReplaceReferencesRewriter MapMember(
        string fromFullName,
        MemberInfo toMember,
        string typeLabel,
        bool rewriteResolved = false
    )
    {
        // validate parameters
        if (string.IsNullOrWhiteSpace(fromFullName))
            throw new ArgumentException(
                $"Can't replace a {typeLabel} given an empty name.",
                nameof(fromFullName)
            );
        if (toMember is null)
            throw new InvalidOperationException(
                $"The replacement {typeLabel} for '{fromFullName}' can't be null."
            );

        // add mapping
        if (!this.MemberMap.TryAdd(fromFullName, toMember))
            throw new InvalidOperationException(
                $"The '{fromFullName}' {typeLabel} is already mapped."
            );

        if (rewriteResolved)
            this.RewriteResolvedMembers.Add(fromFullName);

        return this;
    }

    /// <summary>Update the rewriter state after an reference is replaced.</summary>
    /// <param name="fromMember">The previous member reference.</param>
    /// <param name="typeLabel">A human-readable label for the reference type, like 'field' or 'method'.</param>
    public bool OnRewritten(MemberReference fromMember, string typeLabel)
    {
        this.Phrases.Add($"{fromMember.DeclaringType!.Name}.{fromMember.Name} {typeLabel}");
        return this.MarkRewritten();
    }

    /// <summary>Get a formatted type name in the Cecil full-method-name format.</summary>
    /// <param name="type">The type to format.</param>
    private string FormatCecilType(Type type)
    {
        return RewriteHelper.GetFullCecilName(type);
    }

    /// <summary>Get a formatted parameter list in the Cecil full-method-name format.</summary>
    /// <param name="parameters">The parameter list to format.</param>
    private string FormatCecilParameterList(ParameterInfo[] parameters)
    {
        var paramTypes = parameters.Select(p => RewriteHelper.GetFullCecilName(p.ParameterType));
        return string.Join(",", paramTypes);
    }
}
