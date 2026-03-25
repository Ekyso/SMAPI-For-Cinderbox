using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework.Input;

namespace StardewModdingAPI.Framework.Input;

/// <summary>Manages keyboard state.</summary>
internal class KeyboardStateBuilder : IInputStateBuilder<KeyboardStateBuilder, KeyboardState>
{
    /*********
    ** Fields
    *********/
    /// <summary>The underlying keyboard state.</summary>
    /// <remarks>This value is null if it needs to be regenerated for overrides. Most code should call <see cref="GetState"/> instead.</remarks>
    private KeyboardState? State;

    /// <summary>The pressed buttons.</summary>
    private readonly HashSet<Keys> PressedButtons = [];


    /*********
    ** Public methods
    *********/
    /// <inheritdoc />
    public void Reset(KeyboardState state)
    {
        this.State = state;

        // reset tracked values
        this.PressedButtons.Clear();
        foreach (Keys button in state.GetPressedKeys())
            this.PressedButtons.Add(button);
    }

    /// <summary>Override the state for a key.</summary>
    /// <param name="key">The key to override.</param>
    /// <param name="state">The new state to set.</param>
    public void OverrideButton(Keys key, SButtonState state)
    {
        bool changed = state.IsDown()
            ? this.PressedButtons.Add(key)
            : this.PressedButtons.Remove(key);

        if (changed)
            this.State = null;
    }

    /// <inheritdoc />
    public void FillPressedButtons(HashSet<SButton> set)
    {
        foreach (Keys key in this.PressedButtons)
            set.Add(key.ToSButton());
    }

    /// <inheritdoc />
    public KeyboardState GetState()
    {
        return this.State ??= new KeyboardState(this.PressedButtons.ToArray());
    }
}
