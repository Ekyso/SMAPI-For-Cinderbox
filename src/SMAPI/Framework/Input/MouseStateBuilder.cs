using System.Collections.Generic;
using Microsoft.Xna.Framework.Input;

namespace StardewModdingAPI.Framework.Input;

/// <summary>Manages mouse state.</summary>
internal class MouseStateBuilder : IInputStateBuilder<MouseStateBuilder, MouseState>
{
    /*********
    ** Fields
    *********/
    /// <summary>The underlying mouse state.</summary>
    private MouseState? State;

    /// <summary>The current button states.</summary>
    private readonly Dictionary<SButton, ButtonState> ButtonStates = [];

    /// <summary>The mouse wheel scroll value.</summary>
    private int ScrollWheelValue;


    /*********
    ** Accessors
    *********/
    /// <summary>The X cursor position.</summary>
    public int X { get; private set; }

    /// <summary>The Y cursor position.</summary>
    public int Y { get; private set; }


    /*********
    ** Public methods
    *********/
    /// <inheritdoc />
    public void Reset(MouseState state)
    {
        this.State = state;

        var states = this.ButtonStates;
        states.Clear();
        states[SButton.MouseLeft] = state.LeftButton;
        states[SButton.MouseMiddle] = state.MiddleButton;
        states[SButton.MouseRight] = state.RightButton;
        states[SButton.MouseX1] = state.XButton1;
        states[SButton.MouseX2] = state.XButton2;

        this.X = state.X;
        this.Y = state.Y;
        this.ScrollWheelValue = state.ScrollWheelValue;
    }

    /// <summary>Override the state for a button.</summary>
    /// <param name="button">The button to override.</param>
    /// <param name="state">The new state to set.</param>
    public void OverrideButton(SButton button, SButtonState state)
    {
        ButtonState newState = state.IsDown()
            ? ButtonState.Pressed
            : ButtonState.Released;

        if (this.ButtonStates.TryGetValue(button, out ButtonState oldState) && oldState != newState)
        {
            this.State = null;
            this.ButtonStates[button] = newState;
        }
    }

    /// <inheritdoc />
    public void FillPressedButtons(HashSet<SButton> set)
    {
        foreach (var pair in this.ButtonStates)
        {
            if (pair.Value == ButtonState.Pressed)
                set.Add(pair.Key);
        }
    }

    /// <inheritdoc />
    public MouseState GetState()
    {
        return this.State ??= new MouseState(
            x: this.X,
            y: this.Y,
            scrollWheel: this.ScrollWheelValue,
            leftButton: this.ButtonStates[SButton.MouseLeft],
            middleButton: this.ButtonStates[SButton.MouseMiddle],
            rightButton: this.ButtonStates[SButton.MouseRight],
            xButton1: this.ButtonStates[SButton.MouseX1],
            xButton2: this.ButtonStates[SButton.MouseX2]
        );
    }
}
