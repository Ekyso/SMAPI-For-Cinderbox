using System;
using Microsoft.Xna.Framework.Input;

namespace StardewModdingAPI.Framework.ModLoading.Rewriters.StardewValley_1_6.Internal;

/// <summary>Standalone replacement for desktop's <c>StardewValley.KeyEventArgs</c>.
/// Desktop uses Win32 hooks for keyboard input; mobile has no keyboard.
/// This stub provides the type so mods can reference it without crashing.</summary>
public class KeyEventArgs : EventArgs
{
    private readonly Keys keyCode;

    public Keys KeyCode => keyCode;

    public KeyEventArgs(Keys keyCode)
    {
        this.keyCode = keyCode;
    }
}

/// <summary>Standalone replacement for desktop's <c>StardewValley.KeyEventHandler</c>.</summary>
public delegate void KeyEventHandler(object? sender, KeyEventArgs e);

/// <summary>Standalone replacement for desktop's <c>StardewValley.KeyboardInput</c>.
/// Provides events that mods can subscribe to. On mobile these never fire
/// since there's no Win32 keyboard hook - which is correct for a touch device.</summary>
public static class KeyboardInput
{
    public static event KeyEventHandler? KeyDown;
    public static event KeyEventHandler? KeyUp;
}
