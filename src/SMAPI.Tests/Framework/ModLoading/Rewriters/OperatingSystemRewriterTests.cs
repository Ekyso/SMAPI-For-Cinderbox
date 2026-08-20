using System;
using System.Linq;
using System.Reflection;
using FluentAssertions;
using Mono.Cecil;
using Mono.Cecil.Cil;
using NUnit.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Framework.ModLoading.Rewriters;

namespace SMAPI.Tests.Framework.ModLoading.Rewriters;

[TestFixture]
internal class OperatingSystemRewriterTests
{
    [Test]
    public void RewritesEverySupportedOperatingSystemMethod()
    {
        MethodInfo[] facadeMethods = typeof(OperatingSystemFacade).GetMethods(
            BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly
        );

        foreach (MethodInfo facadeMethod in facadeMethods)
        {
            MethodInfo? sourceMethod = typeof(OperatingSystem).GetMethod(
                facadeMethod.Name,
                facadeMethod.GetParameters().Select(p => p.ParameterType).ToArray()
            );
            sourceMethod.Should().NotBeNull($"OperatingSystem.{facadeMethod.Name} should exist");

            using ModuleDefinition module = ModuleDefinition.CreateModule(
                $"OperatingSystem_{facadeMethod.Name}",
                ModuleKind.Dll
            );
            MethodDefinition fixture = new(
                "Call",
                Mono.Cecil.MethodAttributes.Public | Mono.Cecil.MethodAttributes.Static,
                module.TypeSystem.Void
            );
            module.Types.Add(
                new TypeDefinition(
                    "Fixture",
                    "Caller",
                    Mono.Cecil.TypeAttributes.Class,
                    module.TypeSystem.Object
                )
                {
                    Methods = { fixture },
                }
            );

            ILProcessor il = fixture.Body.GetILProcessor();
            Instruction call = il.Create(OpCodes.Call, module.ImportReference(sourceMethod!));
            il.Append(call);
            il.Append(il.Create(OpCodes.Ret));

            var rewriter = new OperatingSystemRewriter();

            rewriter.Handle(module, il, call).Should().BeTrue();
            MethodReference replacement = call.Operand.Should().BeOfType<MethodReference>().Subject;
            replacement.DeclaringType.FullName.Should().Be(typeof(OperatingSystemFacade).FullName);
            replacement.Name.Should().Be(facadeMethod.Name);
        }
    }

    [Test]
    public void IgnoresUnrelatedMethods()
    {
        using ModuleDefinition module = ModuleDefinition.CreateModule("Unrelated", ModuleKind.Dll);
        MethodInfo sourceMethod = typeof(Environment).GetMethod(nameof(Environment.GetCommandLineArgs))!;
        MethodReference sourceReference = module.ImportReference(sourceMethod);
        MethodDefinition fixture = new(
            "Call",
            Mono.Cecil.MethodAttributes.Public | Mono.Cecil.MethodAttributes.Static,
            module.TypeSystem.Void
        );
        ILProcessor il = fixture.Body.GetILProcessor();
        Instruction call = il.Create(OpCodes.Call, sourceReference);

        var rewriter = new OperatingSystemRewriter();

        rewriter.Handle(module, il, call).Should().BeFalse();
        call.Operand.Should().BeSameAs(sourceReference);
    }

    [TestCase("LINUX", true)]
    [TestCase("linux", true)]
    [TestCase("ANDROID", false)]
    [TestCase("WINDOWS", false)]
    public void IsOSPlatform_UsesLauncherSelectedDesktopPlatform(
        string platform,
        bool expected
    )
    {
        OperatingSystemFacade.IsOSPlatform(GamePlatform.Linux, platform).Should().Be(expected);
    }

    [Test]
    public void IsOSPlatformVersionAtLeast_UsesHostVersionForVirtualLinuxIdentity()
    {
        Version hostKernelVersion = new(6, 12, 3);

        OperatingSystemFacade
            .IsOSPlatformVersionAtLeast(
                GamePlatform.Linux,
                "LINUX",
                hostKernelVersion,
                major: 6,
                minor: 12,
                build: 3
            )
            .Should()
            .BeTrue();
        OperatingSystemFacade
            .IsOSPlatformVersionAtLeast(
                GamePlatform.Linux,
                "LINUX",
                hostKernelVersion,
                major: 6,
                minor: 12,
                build: 4
            )
            .Should()
            .BeFalse();
        OperatingSystemFacade
            .IsOSPlatformVersionAtLeast(
                GamePlatform.Linux,
                "ANDROID",
                hostKernelVersion,
                major: 1
            )
            .Should()
            .BeFalse();
    }

    [Test]
    public void IsOSPlatformVersionAtLeast_NormalizesMissingVersionComponents()
    {
        OperatingSystemFacade
            .IsOSPlatformVersionAtLeast(
                GamePlatform.Linux,
                "LINUX",
                new Version(6, 12),
                major: 6,
                minor: 12,
                build: 0,
                revision: 0
            )
            .Should()
            .BeTrue();
    }
}
