using System;
using System.Linq;
using System.Reflection;
using FluentAssertions;
using Mono.Cecil;
using Mono.Cecil.Cil;
using NUnit.Framework;
using StardewModdingAPI.Framework.ModLoading.Rewriters;

namespace SMAPI.Tests.Framework.ModLoading.Rewriters;

[TestFixture]
internal class ConsoleColorRewriterTests
{
    [Test]
    public void RewritesAllConsoleColorMembers()
    {
        MethodInfo[] facadeMethods = typeof(ConsoleColorFacade).GetMethods(
            BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly
        );

        foreach (MethodInfo facadeMethod in facadeMethods)
        {
            MethodInfo? sourceMethod = typeof(Console).GetMethod(
                facadeMethod.Name,
                facadeMethod.GetParameters().Select(p => p.ParameterType).ToArray()
            );
            sourceMethod.Should().NotBeNull($"Console.{facadeMethod.Name} should exist");

            using ModuleDefinition module = ModuleDefinition.CreateModule(
                $"Console_{facadeMethod.Name}",
                ModuleKind.Dll
            );
            MethodDefinition fixture = new(
                "Call",
                Mono.Cecil.MethodAttributes.Public | Mono.Cecil.MethodAttributes.Static,
                module.TypeSystem.Void
            );
            TypeDefinition fixtureType = new(
                "Fixture",
                "Caller",
                Mono.Cecil.TypeAttributes.Class,
                module.TypeSystem.Object
            );
            fixtureType.Methods.Add(fixture);
            module.Types.Add(fixtureType);

            ILProcessor il = fixture.Body.GetILProcessor();
            Instruction call = il.Create(OpCodes.Call, module.ImportReference(sourceMethod!));

            var rewriter = new ConsoleColorRewriter();

            rewriter.Handle(module, il, call).Should().BeTrue();
            MethodReference replacement = call.Operand.Should().BeOfType<MethodReference>().Subject;
            replacement.DeclaringType.FullName.Should().Be(typeof(ConsoleColorFacade).FullName);
            replacement.Name.Should().Be(facadeMethod.Name);
        }
    }

    [Test]
    public void LeavesOrdinaryConsoleWritesUnchanged()
    {
        using ModuleDefinition module = ModuleDefinition.CreateModule("ConsoleWrite", ModuleKind.Dll);
        MethodInfo sourceMethod = typeof(Console).GetMethod(nameof(Console.WriteLine), [typeof(string)])!;
        MethodReference sourceReference = module.ImportReference(sourceMethod);
        MethodDefinition fixture = new(
            "Call",
            Mono.Cecil.MethodAttributes.Public | Mono.Cecil.MethodAttributes.Static,
            module.TypeSystem.Void
        );
        ILProcessor il = fixture.Body.GetILProcessor();
        Instruction call = il.Create(OpCodes.Call, sourceReference);

        var rewriter = new ConsoleColorRewriter();

        rewriter.Handle(module, il, call).Should().BeFalse();
        call.Operand.Should().BeSameAs(sourceReference);
    }
}
