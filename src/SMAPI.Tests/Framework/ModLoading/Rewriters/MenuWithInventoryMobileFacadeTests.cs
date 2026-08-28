using System;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using FluentAssertions;
using Mono.Cecil;
using NUnit.Framework;
using StardewModdingAPI.Framework.ModLoading.Rewriters.StardewValley_1_6;
using StardewValley.Menus;

namespace SMAPI.Tests.Framework.ModLoading.Rewriters;

[TestFixture]
internal class MenuWithInventoryMobileFacadeTests
{
    [Test]
    public void RuntimeFacadeMethods_DoNotCallPlatformSpecificMenuMembers()
    {
        using AssemblyDefinition assembly = AssemblyDefinition.ReadAssembly(
            typeof(MenuWithInventoryMobileFacade).Assembly.Location
        );
        string[] facadeTypeNames =
        [
            typeof(MenuWithInventoryMobileFacade).FullName!,
            typeof(ItemGrabMenuMobileFacade).FullName!,
            typeof(MobileFacadeRuntimeBridge).FullName!,
            typeof(InventoryMenuMobileFacade).FullName!,
            typeof(CraftingPageMobileFacade).FullName!,
            typeof(CharacterCustomizationMobileFacade).FullName!,
            typeof(GameMenuMobileFacade).FullName!,
            typeof(ShopMenuFacade).FullName!,
            typeof(ShopMenuMobileFacade).FullName!,
        ];

        string[] directCalls = facadeTypeNames
            .Select(assembly.MainModule.GetType)
            .SelectMany(type => type.Methods)
            .Where(method => method.HasBody && method.Name != ".ctor")
            .SelectMany(caller =>
                caller.Body.Instructions
                    .Select(instruction => instruction.Operand)
                    .OfType<MethodReference>()
                    .Where(IsPlatformSpecificMenuCall)
                    .Select(called => $"{caller.FullName} -> {called.FullName}")
            )
            .ToArray();

        directCalls
            .Should()
            .BeEmpty(
                "runtime facade methods must resolve platform-specific menu members through reflection"
            );
    }

    [Test]
    public void HeldItemAccessors_WorkWithDesktopProperty()
    {
        var menu = (MenuWithInventory)
            RuntimeHelpers.GetUninitializedObject(typeof(MenuWithInventory));

        MenuWithInventoryMobileFacade.SetHeldItem(menu, null);

        MenuWithInventoryMobileFacade.GetHeldItem(menu).Should().BeNull();
    }

    [Test]
    public void TryToAddItem_UsesDesktopOverload()
    {
        var inventory = (InventoryMenu)
            RuntimeHelpers.GetUninitializedObject(typeof(InventoryMenu));

        InventoryMenuMobileFacade.TryToAddItem(inventory, null).Should().BeNull();
    }

    [Test]
    public void DesktopMenuConstructors_AreResolved()
    {
        (Type FacadeType, string FieldName)[] fields =
        [
            (typeof(InventoryMenuMobileFacade), "DesktopConstructor"),
            (typeof(MenuWithInventoryMobileFacade), "DesktopConstructor"),
            (typeof(GameMenuMobileFacade), "DesktopBooleanConstructor"),
            (typeof(GameMenuMobileFacade), "DesktopTabConstructor"),
            (typeof(CraftingPageMobileFacade), "DesktopConstructor"),
            (typeof(CharacterCustomizationMobileFacade), "DesktopConstructor"),
            (typeof(ShopMenuMobileFacade), "DesktopListConstructor"),
            (typeof(ShopMenuMobileFacade), "DesktopDictionaryConstructor"),
        ];

        foreach ((Type facadeType, string fieldName) in fields)
        {
            FieldInfo? field = facadeType.GetField(
                fieldName,
                BindingFlags.NonPublic | BindingFlags.Static
            );
            field.Should().NotBeNull();
            field!
                .GetValue(null)
                .Should()
                .NotBeNull($"{facadeType.Name}.{fieldName} should match the desktop menu ABI");
        }
    }

    private static bool IsPlatformSpecificMenuCall(MethodReference method)
    {
        string declaringType = method.DeclaringType.FullName;
        if (declaringType == typeof(MenuWithInventory).FullName)
        {
            return method.Name == ".ctor"
                || method.Name == "get_heldItem"
                || method.Name == "set_heldItem";
        }

        if (declaringType == typeof(InventoryMenu).FullName)
        {
            return method.Name == ".ctor"
                || (method.Name == nameof(InventoryMenu.tryToAddItem) && method.Parameters.Count == 2);
        }

        if (declaringType == typeof(GameMenu).FullName)
            return method.Name == ".ctor";

        if (
            (declaringType == typeof(CraftingPage).FullName
                || declaringType == typeof(CharacterCustomization).FullName)
            && method.Name == ".ctor"
        )
        {
            return true;
        }

        if (declaringType != typeof(ShopMenu).FullName || method.Name != ".ctor")
            return false;

        string secondParameter = method.Parameters.ElementAtOrDefault(1)?.ParameterType.FullName ?? "";
        return secondParameter.StartsWith("System.Collections.Generic.List`1")
            || secondParameter.StartsWith("System.Collections.Generic.Dictionary`2");
    }
}
