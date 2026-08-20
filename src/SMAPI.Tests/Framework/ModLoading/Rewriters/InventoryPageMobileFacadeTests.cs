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
internal class InventoryPageMobileFacadeTests
{
    [TestCase(nameof(InventoryPage.organizeButton))]
    [TestCase(nameof(InventoryPage.trashCan))]
    [TestCase(nameof(InventoryPage.junimoNoteIcon))]
    public void Rewriter_MapsResolvedMobileCompatibilityFields(string fieldName)
    {
        using ModuleDefinition module = ModuleDefinition.CreateModule(
            $"InventoryPage_{fieldName}",
            ModuleKind.Dll
        );
        FieldInfo sourceField = typeof(InventoryPage).GetField(fieldName)!;
        Instruction read = Instruction.Create(
            OpCodes.Ldfld,
            module.ImportReference(sourceField)
        );
        MethodDefinition fixture = this.CreateFixtureMethod(module);
        ILProcessor il = fixture.Body.GetILProcessor();
        il.Append(read);

        ReplaceReferencesRewriter rewriter = new ReplaceReferencesRewriter()
            .MapFacade<InventoryPage, InventoryPageMobileFacade>(
                rewriteResolvedMembers: true
            );

        rewriter.Handle(module, il, read).Should().BeTrue();
        read.OpCode.Should().Be(OpCodes.Call);
        MethodReference replacement = read.Operand.Should().BeOfType<MethodReference>().Subject;
        replacement.DeclaringType.FullName.Should().Be(typeof(InventoryPageMobileFacade).FullName);
        replacement.Name.Should().Be($"get_{fieldName}");
    }

    [Test]
    public void Rewriter_MapsResolvedCompatibilityFieldWrites()
    {
        using ModuleDefinition module = ModuleDefinition.CreateModule(
            "InventoryPage_FieldWrite",
            ModuleKind.Dll
        );
        FieldInfo sourceField = typeof(InventoryPage).GetField(
            nameof(InventoryPage.organizeButton)
        )!;
        Instruction write = Instruction.Create(
            OpCodes.Stfld,
            module.ImportReference(sourceField)
        );
        MethodDefinition fixture = this.CreateFixtureMethod(module);
        ILProcessor il = fixture.Body.GetILProcessor();
        il.Append(write);

        ReplaceReferencesRewriter rewriter = new ReplaceReferencesRewriter()
            .MapFacade<InventoryPage, InventoryPageMobileFacade>(
                rewriteResolvedMembers: true
            );

        rewriter.Handle(module, il, write).Should().BeTrue();
        write.OpCode.Should().Be(OpCodes.Call);
        MethodReference replacement = write.Operand.Should().BeOfType<MethodReference>().Subject;
        replacement.Name.Should().Be("set_organizeButton");
    }

    [Test]
    public void Rewriter_LeavesResolvedMembersAloneByDefault()
    {
        using ModuleDefinition module = ModuleDefinition.CreateModule(
            "InventoryPage_DefaultRewrite",
            ModuleKind.Dll
        );
        FieldReference sourceField = module.ImportReference(
            typeof(InventoryPage).GetField(nameof(InventoryPage.organizeButton))!
        );
        Instruction read = Instruction.Create(OpCodes.Ldfld, sourceField);
        MethodDefinition fixture = this.CreateFixtureMethod(module);
        ILProcessor il = fixture.Body.GetILProcessor();
        il.Append(read);

        ReplaceReferencesRewriter rewriter = new ReplaceReferencesRewriter()
            .MapFacade<InventoryPage, InventoryPageMobileFacade>();

        rewriter.Handle(module, il, read).Should().BeFalse();
        read.Operand.Should().BeSameAs(sourceField);
    }

    [TestCase(true, true)]
    [TestCase(false, false)]
    public void ProductionHandlers_RewriteCompatibilityFieldsOnlyForMobile(
        bool isMobile,
        bool expectedRewrite
    )
    {
        using ModuleDefinition module = ModuleDefinition.CreateModule(
            $"InventoryPage_Production_{isMobile}",
            ModuleKind.Dll
        );
        FieldReference sourceField = module.ImportReference(
            typeof(InventoryPage).GetField(nameof(InventoryPage.organizeButton))!
        );
        Instruction read = Instruction.Create(OpCodes.Ldfld, sourceField);
        MethodDefinition fixture = this.CreateFixtureMethod(module);
        ILProcessor il = fixture.Body.GetILProcessor();
        il.Append(read);

        bool wasRewritten = new InstructionMetadata()
            .GetHandlers(
                paranoidMode: false,
                rewriteMods: true,
                logTechnicalDetailsForBrokenMods: false,
                activeGameIsMobile: isMobile
            )
            .Any(handler => handler.Handle(module, il, read));

        wasRewritten.Should().Be(expectedRewrite);
        if (expectedRewrite)
        {
            MethodReference replacement = read
                .Operand.Should()
                .BeOfType<MethodReference>()
                .Subject;
            replacement.DeclaringType.FullName.Should().Be(
                typeof(InventoryPageMobileFacade).FullName
            );
        }
        else
            read.Operand.Should().BeSameAs(sourceField);
    }

    private MethodDefinition CreateFixtureMethod(ModuleDefinition module)
    {
        TypeDefinition fixtureType = new(
            "Fixture",
            "Caller",
            Mono.Cecil.TypeAttributes.Class,
            module.TypeSystem.Object
        );
        MethodDefinition fixtureMethod = new(
            "AccessField",
            Mono.Cecil.MethodAttributes.Public | Mono.Cecil.MethodAttributes.Static,
            module.TypeSystem.Void
        );
        fixtureType.Methods.Add(fixtureMethod);
        module.Types.Add(fixtureType);
        return fixtureMethod;
    }
}
