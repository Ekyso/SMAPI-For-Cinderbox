using System;
using System.Linq;
using System.Reflection;
using FluentAssertions;
using Mono.Cecil;
using Mono.Cecil.Cil;
using NUnit.Framework;
using StardewModdingAPI.Framework.ModLoading.Rewriters;
using StardewModdingAPI.Framework.ModLoading.Rewriters.StardewValley_1_6;
using StardewModdingAPI.Metadata;
using StardewValley.Menus;

namespace SMAPI.Tests.Framework.ModLoading.Rewriters;

[TestFixture]
internal class IClickableMenuMobileFacadeTests
{
    /*********
    ** Unit tests
    *********/
    [Test]
    public void DrawToolTip_DeclaresDesktopSignatureForRewrite()
    {
        MethodInfo gameMethod = typeof(IClickableMenu)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(p => p.Name == nameof(IClickableMenu.drawToolTip) && p.GetParameters().Length == 12);
        MethodInfo facadeMethod = typeof(IClickableMenuMobileFacade)
            .GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Single(p =>
                p.Name == nameof(IClickableMenuMobileFacade.drawToolTip)
                && p.GetParameters().Length == 12
            );

        facadeMethod.ReturnType.Should().Be(gameMethod.ReturnType);
        facadeMethod
            .GetParameters()
            .Select(p => p.ParameterType)
            .Should()
            .Equal(gameMethod.GetParameters().Select(p => p.ParameterType));
    }

    [Test]
    public void RuntimeDelegates_MatchMobileSignatures()
    {
        this.AssertDelegateParameters(
            "MobileDrawToolTipDelegate",
            [
                "Microsoft.Xna.Framework.Graphics.SpriteBatch",
                "System.String",
                "System.String",
                "StardewValley.Item",
                "System.Boolean",
                "System.Int32",
                "System.Int32",
                "System.String",
                "System.Int32",
                "StardewValley.CraftingRecipe",
                "System.Int32"
            ]
        );

        this.AssertDelegateParameters(
            "MobileDrawHoverTextDelegate",
            [
                "Microsoft.Xna.Framework.Graphics.SpriteBatch",
                "System.String",
                "Microsoft.Xna.Framework.Graphics.SpriteFont",
                "System.Int32",
                "System.Int32",
                "System.Int32",
                "System.String",
                "System.Int32",
                "System.String[]",
                "StardewValley.Item",
                "System.Int32",
                "System.String",
                "System.Int32",
                "System.Int32",
                "System.Int32",
                "System.Single",
                "StardewValley.CraftingRecipe",
                "System.Collections.Generic.IList`1<StardewValley.Item>",
                "Microsoft.Xna.Framework.Graphics.Texture2D",
                "System.Nullable`1<Microsoft.Xna.Framework.Rectangle>",
                "System.Nullable`1<Microsoft.Xna.Framework.Color>",
                "System.Nullable`1<Microsoft.Xna.Framework.Color>",
                "System.Single",
                "System.Int32",
                "System.Int32",
                "System.Int32"
            ]
        );

        this.AssertDelegateParameters(
            "MobileDrawHoverTextBuilderDelegate",
            [
                "Microsoft.Xna.Framework.Graphics.SpriteBatch",
                "System.Text.StringBuilder",
                "Microsoft.Xna.Framework.Graphics.SpriteFont",
                "System.Int32",
                "System.Int32",
                "System.Int32",
                "System.String",
                "System.Int32",
                "System.String[]",
                "StardewValley.Item",
                "System.Int32",
                "System.String",
                "System.Int32",
                "System.Int32",
                "System.Int32",
                "System.Single",
                "StardewValley.CraftingRecipe",
                "System.Collections.Generic.IList`1<StardewValley.Item>",
                "Microsoft.Xna.Framework.Graphics.Texture2D",
                "System.Nullable`1<Microsoft.Xna.Framework.Rectangle>",
                "System.Nullable`1<Microsoft.Xna.Framework.Color>",
                "System.Nullable`1<Microsoft.Xna.Framework.Color>",
                "System.Single",
                "System.Int32",
                "System.Int32",
                "System.Int32"
            ]
        );

        this.AssertDelegateParameters(
            "MobileDrawTextureBoxDelegate",
            [
                "Microsoft.Xna.Framework.Graphics.SpriteBatch",
                "Microsoft.Xna.Framework.Graphics.Texture2D",
                "Microsoft.Xna.Framework.Rectangle",
                "System.Int32",
                "System.Int32",
                "System.Int32",
                "System.Int32",
                "Microsoft.Xna.Framework.Color",
                "System.Single",
                "System.Boolean",
                "System.Single",
                "System.Boolean"
            ]
        );
    }

    [Test]
    public void DrawTextureBox_DeclaresDesktopSignatureForRewrite()
    {
        MethodInfo gameMethod = typeof(IClickableMenu)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(p =>
                p.Name == nameof(IClickableMenu.drawTextureBox)
                && p.GetParameters().Length == 11
                && p.GetParameters()[1].ParameterType.FullName
                    == "Microsoft.Xna.Framework.Graphics.Texture2D"
            );
        MethodInfo facadeMethod = typeof(IClickableMenuMobileFacade)
            .GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Single(p =>
                p.Name == nameof(IClickableMenuMobileFacade.drawTextureBox)
                && p.GetParameters().Length == 11
            );

        facadeMethod.ReturnType.Should().Be(gameMethod.ReturnType);
        facadeMethod
            .GetParameters()
            .Select(p => p.ParameterType)
            .Should()
            .Equal(gameMethod.GetParameters().Select(p => p.ParameterType));
    }

    [Test]
    public void Facade_DeclaresCurrentAndLegacyHoverSignatures()
    {
        string[] currentGameSignatures = typeof(IClickableMenu)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(p => p.Name == nameof(IClickableMenu.drawHoverText) && p.GetParameters().Length == 25)
            .Select(this.FormatSignature)
            .Order()
            .ToArray();
        string[] currentFacadeSignatures = typeof(IClickableMenuMobileFacade)
            .GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Where(p => p.Name == nameof(IClickableMenu.drawHoverText) && p.GetParameters().Length == 25)
            .Select(this.FormatSignature)
            .Order()
            .ToArray();
        string[] legacySignatures = typeof(IClickableMenuFacade)
            .GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Where(p =>
                (p.Name == nameof(IClickableMenu.drawHoverText) && p.GetParameters().Length == 18)
                || (p.Name == nameof(IClickableMenu.drawToolTip) && p.GetParameters().Length == 11)
            )
            .Select(this.FormatSignature)
            .Order()
            .ToArray();
        string[] legacyMobileSignatures = typeof(IClickableMenuMobileFacade)
            .GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Where(p =>
                (p.Name == nameof(IClickableMenu.drawHoverText) && p.GetParameters().Length == 18)
                || (p.Name == nameof(IClickableMenu.drawToolTip) && p.GetParameters().Length == 11)
            )
            .Select(this.FormatSignature)
            .Order()
            .ToArray();

        currentGameSignatures.Should().HaveCount(2);
        currentFacadeSignatures.Should().Equal(currentGameSignatures);
        legacySignatures.Should().HaveCount(3);
        legacyMobileSignatures.Should().Equal(legacySignatures);
    }

    [Test]
    public void FacadeMethods_DoNotCallGameMenuMethodsDirectly()
    {
        using AssemblyDefinition assembly = AssemblyDefinition.ReadAssembly(
            typeof(IClickableMenuMobileFacade).Assembly.Location
        );
        TypeDefinition[] facadeTypes =
        [
            assembly.MainModule.GetType(typeof(IClickableMenuMobileFacade).FullName!),
            assembly.MainModule.GetType(typeof(OptionsDropDownMobileFacade).FullName!)
        ];

        MethodReference[] directCalls = facadeTypes
            .SelectMany(type => type.Methods)
            .SelectMany(p => p.Body.Instructions)
            .Select(p => p.Operand)
            .OfType<MethodReference>()
            .Where(p =>
                p.DeclaringType.FullName == typeof(IClickableMenu).FullName
                && (p.Name == "drawToolTip" || p.Name == "drawHoverText" || p.Name == "drawTextureBox")
            )
            .ToArray();

        directCalls
            .Should()
            .BeEmpty(
                "the Android runtime doesn't contain the desktop menu signatures SMAPI compiles against"
            );
    }

    [Test]
    public void Rewriter_MapsUnresolvedDesktopTooltipToMobileFacade()
    {
        using ModuleDefinition module = ModuleDefinition.CreateModule(
            "MobileTooltipRewriteFixture",
            ModuleKind.Dll
        );
        TypeDefinition fixtureType = new(
            "Fixture",
            "Caller",
            Mono.Cecil.TypeAttributes.Class,
            module.TypeSystem.Object
        );
        MethodDefinition fixtureMethod = new(
            "CallTooltip",
            Mono.Cecil.MethodAttributes.Public | Mono.Cecil.MethodAttributes.Static,
            module.TypeSystem.Void
        );
        fixtureType.Methods.Add(fixtureMethod);
        module.Types.Add(fixtureType);

        TypeReference itemType = this.CreateTypeReference(module, "StardewValley", "Item");
        TypeReference listType = this.CreateTypeReference(
            module,
            "System.Collections.Generic",
            "IList`1"
        );
        GenericInstanceType itemListType = new(listType);
        itemListType.GenericArguments.Add(itemType);

        MethodReference desktopTooltip = new(
            "drawToolTip",
            module.TypeSystem.Void,
            this.CreateTypeReference(module, "StardewValley.Menus", "IClickableMenu")
        )
        {
            HasThis = false,
        };
        TypeReference[] parameterTypes =
        [
            this.CreateTypeReference(
                module,
                "Microsoft.Xna.Framework.Graphics",
                "SpriteBatch"
            ),
            module.TypeSystem.String,
            module.TypeSystem.String,
            itemType,
            module.TypeSystem.Boolean,
            module.TypeSystem.Int32,
            module.TypeSystem.Int32,
            module.TypeSystem.String,
            module.TypeSystem.Int32,
            this.CreateTypeReference(module, "StardewValley", "CraftingRecipe"),
            module.TypeSystem.Int32,
            itemListType
        ];
        foreach (TypeReference parameterType in parameterTypes)
            desktopTooltip.Parameters.Add(new ParameterDefinition(parameterType));

        ILProcessor il = fixtureMethod.Body.GetILProcessor();
        Instruction call = il.Create(OpCodes.Call, desktopTooltip);
        il.Append(call);
        il.Append(il.Create(OpCodes.Ret));

        ReplaceReferencesRewriter rewriter = new ReplaceReferencesRewriter()
            .MapFacade<IClickableMenu, IClickableMenuMobileFacade>();

        rewriter.Handle(module, il, call).Should().BeTrue();
        MethodReference rewritten = call.Operand.Should().BeOfType<MethodReference>().Subject;
        rewritten.DeclaringType.FullName.Should().Be(typeof(IClickableMenuMobileFacade).FullName);
        rewritten.Parameters.Should().HaveCount(12);
    }

    [Test]
    public void Rewriter_MapsUnresolvedDesktopTextureBoxToMobileFacade()
    {
        using ModuleDefinition module = ModuleDefinition.CreateModule(
            "MobileTextureBoxRewriteFixture",
            ModuleKind.Dll
        );
        TypeDefinition fixtureType = new(
            "Fixture",
            "Caller",
            Mono.Cecil.TypeAttributes.Class,
            module.TypeSystem.Object
        );
        MethodDefinition fixtureMethod = new(
            "CallTextureBox",
            Mono.Cecil.MethodAttributes.Public | Mono.Cecil.MethodAttributes.Static,
            module.TypeSystem.Void
        );
        fixtureType.Methods.Add(fixtureMethod);
        module.Types.Add(fixtureType);

        MethodReference desktopTextureBox = new(
            "drawTextureBox",
            module.TypeSystem.Void,
            this.CreateTypeReference(module, "StardewValley.Menus", "IClickableMenu")
        )
        {
            HasThis = false,
        };
        TypeReference[] parameterTypes =
        [
            this.CreateTypeReference(module, "Microsoft.Xna.Framework.Graphics", "SpriteBatch"),
            this.CreateTypeReference(module, "Microsoft.Xna.Framework.Graphics", "Texture2D"),
            this.CreateTypeReference(module, "Microsoft.Xna.Framework", "Rectangle"),
            module.TypeSystem.Int32,
            module.TypeSystem.Int32,
            module.TypeSystem.Int32,
            module.TypeSystem.Int32,
            this.CreateTypeReference(module, "Microsoft.Xna.Framework", "Color"),
            module.TypeSystem.Single,
            module.TypeSystem.Boolean,
            module.TypeSystem.Single
        ];
        foreach (TypeReference parameterType in parameterTypes)
            desktopTextureBox.Parameters.Add(new ParameterDefinition(parameterType));

        ILProcessor il = fixtureMethod.Body.GetILProcessor();
        Instruction call = il.Create(OpCodes.Call, desktopTextureBox);
        il.Append(call);
        il.Append(il.Create(OpCodes.Ret));

        ReplaceReferencesRewriter rewriter = new ReplaceReferencesRewriter()
            .MapFacade<IClickableMenu, IClickableMenuMobileFacade>();

        rewriter.Handle(module, il, call).Should().BeTrue();
        MethodReference rewritten = call.Operand.Should().BeOfType<MethodReference>().Subject;
        rewritten.DeclaringType.FullName.Should().Be(typeof(IClickableMenuMobileFacade).FullName);
        rewritten.Parameters.Should().HaveCount(11);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void ProductionHandlers_ConstructWithoutDuplicateMappings(bool isMobile)
    {
        Action construct = () =>
            new InstructionMetadata()
                .GetHandlers(
                    paranoidMode: false,
                    rewriteMods: true,
                    logTechnicalDetailsForBrokenMods: false,
                    activeGameIsMobile: isMobile
                )
                .ToArray();

        construct.Should().NotThrow();
    }

    [Test]
    public void Rewriter_ChangesCallvirtToCallForStaticHelper()
    {
        using ModuleDefinition module = ModuleDefinition.CreateModule(
            "InstanceToStaticRewriteFixture",
            ModuleKind.Dll
        );
        TypeDefinition fixtureType = new(
            "Fixture",
            "Caller",
            Mono.Cecil.TypeAttributes.Class,
            module.TypeSystem.Object
        );
        MethodDefinition fixtureMethod = new(
            "CallInstanceMethod",
            Mono.Cecil.MethodAttributes.Public | Mono.Cecil.MethodAttributes.Static,
            module.TypeSystem.Void
        );
        fixtureType.Methods.Add(fixtureMethod);
        module.Types.Add(fixtureType);

        MethodReference sourceMethod = new(
            "Run",
            module.TypeSystem.Void,
            this.CreateTypeReference(module, "Fixture", "Instance")
        )
        {
            HasThis = true,
        };
        ILProcessor il = fixtureMethod.Body.GetILProcessor();
        Instruction call = il.Create(OpCodes.Callvirt, sourceMethod);
        il.Append(call);
        il.Append(il.Create(OpCodes.Ret));

        ReplaceReferencesRewriter rewriter = new ReplaceReferencesRewriter().MapMethod(
            "System.Void Fixture.Instance::Run()",
            typeof(StaticRewriteTarget),
            nameof(StaticRewriteTarget.Run),
            [typeof(object)]
        );

        rewriter.Handle(module, il, call).Should().BeTrue();
        call.OpCode.Should().Be(OpCodes.Call);
        MethodReference rewritten = call.Operand.Should().BeOfType<MethodReference>().Subject;
        rewritten.Resolve().IsStatic.Should().BeTrue();
    }

    [Test]
    public void Rewriter_DoesNotMutateInstanceMethodGroupMappedToStaticHelper()
    {
        using ModuleDefinition module = ModuleDefinition.CreateModule(
            "InstanceMethodGroupRewriteFixture",
            ModuleKind.Dll
        );
        MethodDefinition fixtureMethod = this.CreateFixtureMethod(module, "CreateDelegate");
        MethodReference sourceMethod = this.CreateUnresolvedInstanceMethod(module);
        ILProcessor il = fixtureMethod.Body.GetILProcessor();
        Instruction loadFunction = il.Create(OpCodes.Ldvirtftn, sourceMethod);
        il.Append(loadFunction);
        il.Append(il.Create(OpCodes.Ret));

        ReplaceReferencesRewriter rewriter = this.CreateInstanceToStaticRewriter();

        rewriter.Handle(module, il, loadFunction).Should().BeFalse();
        loadFunction.OpCode.Should().Be(OpCodes.Ldvirtftn);
        loadFunction.Operand.Should().BeSameAs(sourceMethod);
    }

    [Test]
    public void Rewriter_DoesNotMutateConstrainedInstanceCallMappedToStaticHelper()
    {
        using ModuleDefinition module = ModuleDefinition.CreateModule(
            "ConstrainedInstanceRewriteFixture",
            ModuleKind.Dll
        );
        MethodDefinition fixtureMethod = this.CreateFixtureMethod(module, "CallConstrained");
        MethodReference sourceMethod = this.CreateUnresolvedInstanceMethod(module);
        ILProcessor il = fixtureMethod.Body.GetILProcessor();
        il.Append(
            il.Create(
                OpCodes.Constrained,
                this.CreateTypeReference(module, "Fixture", "Instance")
            )
        );
        Instruction call = il.Create(OpCodes.Callvirt, sourceMethod);
        il.Append(call);
        il.Append(il.Create(OpCodes.Ret));

        ReplaceReferencesRewriter rewriter = this.CreateInstanceToStaticRewriter();

        rewriter.Handle(module, il, call).Should().BeFalse();
        call.OpCode.Should().Be(OpCodes.Callvirt);
        call.Operand.Should().BeSameAs(sourceMethod);
    }

    [Test]
    public void GetRequiredStaticDelegate_ResolvesExactSignature()
    {
        Action<int> callback =
            IClickableMenuMobileFacade.GetRequiredStaticDelegate<Action<int>>(
                typeof(DelegateTarget),
                nameof(DelegateTarget.SetValue)
            );

        callback(42);

        DelegateTarget.Value.Should().Be(42);
    }

    /*********
    ** Private methods
    *********/
    private void AssertDelegateParameters(string name, string[] expected)
    {
        Type? delegateType = typeof(IClickableMenuMobileFacade).GetNestedType(
            name,
            BindingFlags.NonPublic
        );

        delegateType.Should().NotBeNull();
        delegateType!
            .GetMethod("Invoke")!
            .GetParameters()
            .Select(p => this.FormatType(p.ParameterType))
            .Should()
            .Equal(expected);
    }

    private string FormatType(Type type)
    {
        if (!type.IsGenericType)
            return type.FullName!;

        return $"{type.GetGenericTypeDefinition().FullName}<{string.Join(",", type.GetGenericArguments().Select(this.FormatType))}>";
    }

    private string FormatSignature(MethodInfo method)
    {
        return $"{method.Name}({string.Join(",", method.GetParameters().Select(p => this.FormatType(p.ParameterType)))})";
    }

    private TypeReference CreateTypeReference(
        ModuleDefinition module,
        string @namespace,
        string name
    )
    {
        return new TypeReference(@namespace, name, module, module);
    }

    private MethodDefinition CreateFixtureMethod(ModuleDefinition module, string name)
    {
        TypeDefinition fixtureType = new(
            "Fixture",
            $"{name}Caller",
            Mono.Cecil.TypeAttributes.Class,
            module.TypeSystem.Object
        );
        MethodDefinition fixtureMethod = new(
            name,
            Mono.Cecil.MethodAttributes.Public | Mono.Cecil.MethodAttributes.Static,
            module.TypeSystem.Void
        );
        fixtureType.Methods.Add(fixtureMethod);
        module.Types.Add(fixtureType);
        return fixtureMethod;
    }

    private MethodReference CreateUnresolvedInstanceMethod(ModuleDefinition module)
    {
        return new MethodReference(
            "Run",
            module.TypeSystem.Void,
            this.CreateTypeReference(module, "Fixture", "Instance")
        )
        {
            HasThis = true,
        };
    }

    private ReplaceReferencesRewriter CreateInstanceToStaticRewriter()
    {
        return new ReplaceReferencesRewriter().MapMethod(
            "System.Void Fixture.Instance::Run()",
            typeof(StaticRewriteTarget),
            nameof(StaticRewriteTarget.Run),
            [typeof(object)]
        );
    }

    /*********
    ** Private types
    *********/
    public static class DelegateTarget
    {
        public static int Value { get; private set; }

        public static void SetValue(int value)
        {
            Value = value;
        }
    }

    public static class StaticRewriteTarget
    {
        public static void Run(object instance) { }
    }
}
