using System;
using System.Reflection;
using FluentAssertions;
using NUnit.Framework;
using StardewModdingAPI.Framework.Events;

namespace SMAPI.Tests.Framework.Events;

[TestFixture]
internal class EventProfilingWarningFormatterTests
{
    [Test]
    public void Format_IncludesExactHandlerAndProfilingContext()
    {
        MethodInfo method = typeof(EventProfilingWarningFormatterTests).GetMethod(
            nameof(OnRenderingHud),
            BindingFlags.NonPublic | BindingFlags.Static
        )!;

        string warning = EventProfilingWarningFormatter.Format(
            modDisplayName: "UI Info Suite 2 Redux",
            modId: "Annosz.UiInfoSuite2Redux",
            method,
            eventName: "Display.RenderingHud",
            elapsedMilliseconds: 12,
            warningThreshold: 4
        );

        warning
            .Should()
            .Be(
                "The 'UI Info Suite 2 Redux' mod (Annosz.UiInfoSuite2Redux) event handler 'SMAPI.Tests.Framework.Events.EventProfilingWarningFormatterTests.OnRenderingHud' for the Display.RenderingHud event took 12ms, which exceeds the 4ms warning threshold. This may cause performance issues or frame stutters."
            );
    }

    private static void OnRenderingHud(object? sender, EventArgs args) { }
}
