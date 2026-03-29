using System;
using System.Collections.Generic;
using StardewModdingAPI.Framework.Input;

namespace StardewModdingAPI.Events;

/// <summary>Event arguments when any buttons were pressed or released.</summary>
public class ButtonsChangedEventArgs : EventArgs
{
    /*********
    ** Accessors
    *********/
    /// <summary>The current cursor position.</summary>
    public ICursorPosition Cursor { get; }

    /// <summary>The buttons which were pressed since the previous tick.</summary>
    public IEnumerable<SButton> Pressed { get; }

    /// <summary>The buttons which were held since the previous tick.</summary>
    public IEnumerable<SButton> Held { get; }

    /// <summary>The buttons which were released since the previous tick.</summary>
    public IEnumerable<SButton> Released { get; }


    /*********
    ** Public methods
    *********/
    /// <summary>Construct an instance.</summary>
    /// <param name="cursor">The cursor position.</param>
    /// <param name="inputState">The game's current input state.</param>
    internal ButtonsChangedEventArgs(ICursorPosition cursor, SInputState inputState)
    {
        var pressed = new List<SButton>();
        var held = new List<SButton>();
        var released = new List<SButton>();

        foreach ((SButton button, SButtonState state) in inputState.GetActiveButtonStates())
        {
            switch (state)
            {
                case SButtonState.Pressed:
                    pressed.Add(button);
                    break;

                case SButtonState.Held:
                    held.Add(button);
                    break;

                case SButtonState.Released:
                    released.Add(button);
                    break;
            }
        }

        this.Cursor = cursor;
        this.Pressed = pressed;
        this.Held = held;
        this.Released = released;
    }
}
