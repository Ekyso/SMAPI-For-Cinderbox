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
    /// <remarks>This value is null if it needs to be regenerated for overrides. Most code should call <see cref="GetState"/> instead.</remarks>
    private MouseState? State;

    /// <summary>The pressed buttons.</summary>
    private readonly HashSet<SButton> PressedButtons = [];

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

        // reset tracked buttons
        this.PressedButtons.Clear();
        AddIfPressed(this.PressedButtons, SButton.MouseLeft, state.LeftButton);
        AddIfPressed(this.PressedButtons, SButton.MouseMiddle, state.MiddleButton);
        AddIfPressed(this.PressedButtons, SButton.MouseRight, state.RightButton);
        AddIfPressed(this.PressedButtons, SButton.MouseX1, state.XButton1);
        AddIfPressed(this.PressedButtons, SButton.MouseX2, state.XButton2);

        this.X = state.X;
        this.Y = state.Y;
        this.ScrollWheelValue = state.ScrollWheelValue;

        return;
        static void AddIfPressed(HashSet<SButton> pressed, SButton button, ButtonState state)
        {
            if (state == ButtonState.Pressed)
                pressed.Add(button);
        }
    }

    /// <summary>Override the state for a button.</summary>
    /// <param name="button">The button to override.</param>
    /// <param name="state">The new state to set.</param>
    public void OverrideButton(SButton button, SButtonState state)
    {
        bool changed = state.IsDown()
            ? this.PressedButtons.Add(button)
            : this.PressedButtons.Remove(button);

        if (changed)
            this.State = null;
    }

    /// <inheritdoc />
    public void FillPressedButtons(HashSet<SButton> set)
    {
        foreach (SButton button in this.PressedButtons)
            set.Add(button);
    }

    /// <inheritdoc />
    public MouseState GetState()
    {
        return this.State ??= new MouseState(
            x: this.X,
            y: this.Y,
            scrollWheel: this.ScrollWheelValue,
            leftButton: GetButtonState(this.PressedButtons, SButton.MouseLeft),
            middleButton: GetButtonState(this.PressedButtons, SButton.MouseMiddle),
            rightButton: GetButtonState(this.PressedButtons, SButton.MouseRight),
            xButton1: GetButtonState(this.PressedButtons, SButton.MouseX1),
            xButton2: GetButtonState(this.PressedButtons, SButton.MouseX2)
        );

        static ButtonState GetButtonState(HashSet<SButton> pressed, SButton button)
        {
            return pressed.Contains(button)
                ? ButtonState.Pressed
                : ButtonState.Released;
        }
    }
}
