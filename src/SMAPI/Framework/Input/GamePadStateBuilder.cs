using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace StardewModdingAPI.Framework.Input;

/// <summary>Manages controller state.</summary>
internal class GamePadStateBuilder : IInputStateBuilder<GamePadStateBuilder, GamePadState>
{
    /*********
    ** Fields
    *********/
    /// <summary>The maximum direction to ignore for the left thumbstick.</summary>
    private const float LeftThumbstickDeadZone = 0.2f;

    /// <summary>The maximum direction to ignore for the right thumbstick.</summary>
    private const float RightThumbstickDeadZone = 0.9f;

    /// <summary>The underlying controller state.</summary>
    /// <remarks>This value is null if it needs to be regenerated for overrides. Most code should call <see cref="GetState"/> instead.</remarks>
    private GamePadState? State;

    /// <summary>The pressed buttons.</summary>
    private readonly HashSet<Buttons> PressedButtons = [];

    /// <summary>The left trigger value.</summary>
    private float LeftTrigger;

    /// <summary>The right trigger value.</summary>
    private float RightTrigger;

    /// <summary>The left thumbstick position.</summary>
    private Vector2 LeftStickPos;

    /// <summary>The right thumbstick position.</summary>
    private Vector2 RightStickPos;


    /*********
    ** Public methods
    *********/
    /// <inheritdoc />
    public void Reset(GamePadState state)
    {
        this.State = state;

        // reset tracked values
        this.PressedButtons.Clear();
        if (state.IsConnected)
        {
            GamePadDPad pad = state.DPad;
            GamePadButtons buttons = state.Buttons;
            GamePadTriggers triggers = state.Triggers;
            GamePadThumbSticks sticks = state.ThumbSticks;

            HashSet<Buttons> pressed = this.PressedButtons;
            AddIfPressed(pressed, Buttons.DPadUp, pad.Up);
            AddIfPressed(pressed, Buttons.DPadDown, pad.Down);
            AddIfPressed(pressed, Buttons.DPadLeft, pad.Left);
            AddIfPressed(pressed, Buttons.DPadRight, pad.Right);
            AddIfPressed(pressed, Buttons.A, buttons.A);
            AddIfPressed(pressed, Buttons.B, buttons.B);
            AddIfPressed(pressed, Buttons.X, buttons.X);
            AddIfPressed(pressed, Buttons.Y, buttons.Y);
            AddIfPressed(pressed, Buttons.LeftStick, buttons.LeftStick);
            AddIfPressed(pressed, Buttons.RightStick, buttons.RightStick);
            AddIfPressed(pressed, Buttons.LeftShoulder, buttons.LeftShoulder);
            AddIfPressed(pressed, Buttons.RightShoulder, buttons.RightShoulder);
            AddIfPressed(pressed, Buttons.Back, buttons.Back);
            AddIfPressed(pressed, Buttons.Start, buttons.Start);
            AddIfPressed(pressed, Buttons.BigButton, buttons.BigButton);

            this.LeftTrigger = triggers.Left;
            this.RightTrigger = triggers.Right;
            this.LeftStickPos = sticks.Left;
            this.RightStickPos = sticks.Right;
        }
        else
        {
            this.LeftTrigger = 0;
            this.RightTrigger = 0;
            this.LeftStickPos = Vector2.Zero;
            this.RightStickPos = Vector2.Zero;
        }

        return;
        static void AddIfPressed(HashSet<Buttons> pressed, Buttons button, ButtonState state)
        {
            if (state == ButtonState.Pressed)
                pressed.Add(button);
        }
    }

    /// <summary>Override the state for a button.</summary>
    /// <param name="button">The button to override.</param>
    /// <param name="state">The new state to set.</param>
    public void OverrideButton(Buttons button, SButtonState state)
    {
        bool isDown = state.IsDown();
        bool changed;

        switch (button)
        {
            // left thumbstick
            case Buttons.LeftThumbstickUp:
                changed = Set(ref this.LeftStickPos.Y, isDown ? 1 : 0);
                break;
            case Buttons.LeftThumbstickDown:
                changed = Set(ref this.LeftStickPos.Y, isDown ? -1 : 0);
                break;
            case Buttons.LeftThumbstickLeft:
                changed = Set(ref this.LeftStickPos.X, isDown ? -1 : 0);
                break;
            case Buttons.LeftThumbstickRight:
                changed = Set(ref this.LeftStickPos.X, isDown ? 1 : 0);
                break;

            // right thumbstick
            case Buttons.RightThumbstickUp:
                changed = Set(ref this.RightStickPos.Y, isDown ? 1 : 0);
                break;
            case Buttons.RightThumbstickDown:
                changed = Set(ref this.RightStickPos.Y, isDown ? -1 : 0);
                break;
            case Buttons.RightThumbstickLeft:
                changed = Set(ref this.RightStickPos.X, isDown ? -1 : 0);
                break;
            case Buttons.RightThumbstickRight:
                changed = Set(ref this.RightStickPos.X, isDown ? 1 : 0);
                break;

            // triggers
            case Buttons.LeftTrigger:
                changed = Set(ref this.LeftTrigger, isDown ? 1 : 0);
                break;
            case Buttons.RightTrigger:
                changed = Set(ref this.RightTrigger, isDown ? 1 : 0);
                break;

            // buttons
            default:
                changed = isDown
                    ? this.PressedButtons.Add(button)
                    : this.PressedButtons.Remove(button);
                break;
        }

        if (changed)
            this.State = null;

        return;
        [SuppressMessage("ReSharper", "CompareOfFloatsByEqualityOperator", Justification = "Floating points not an issue for the specific values we're checking.")]
        static bool Set(ref float field, int newValue)
        {
            if (field != newValue)
            {
                field = newValue;
                return true;
            }

            return false;
        }
    }

    /// <inheritdoc />
    public void FillPressedButtons(HashSet<SButton> set)
    {
        // buttons
        foreach (Buttons button in this.PressedButtons)
            set.Add(button.ToSButton());

        // triggers
        if (this.LeftTrigger > 0.2f)
            set.Add(SButton.LeftTrigger);
        if (this.RightTrigger > 0.2f)
            set.Add(SButton.RightTrigger);

        // left thumbstick direction
        if (this.LeftStickPos.Y > GamePadStateBuilder.LeftThumbstickDeadZone)
            set.Add(SButton.LeftThumbstickUp);
        if (this.LeftStickPos.Y < -GamePadStateBuilder.LeftThumbstickDeadZone)
            set.Add(SButton.LeftThumbstickDown);
        if (this.LeftStickPos.X > GamePadStateBuilder.LeftThumbstickDeadZone)
            set.Add(SButton.LeftThumbstickRight);
        if (this.LeftStickPos.X < -GamePadStateBuilder.LeftThumbstickDeadZone)
            set.Add(SButton.LeftThumbstickLeft);

        // right thumbstick direction
        if (this.RightStickPos.Length() > GamePadStateBuilder.RightThumbstickDeadZone)
        {
            if (this.RightStickPos.Y > 0)
                set.Add(SButton.RightThumbstickUp);
            if (this.RightStickPos.Y < 0)
                set.Add(SButton.RightThumbstickDown);
            if (this.RightStickPos.X > 0)
                set.Add(SButton.RightThumbstickRight);
            if (this.RightStickPos.X < 0)
                set.Add(SButton.RightThumbstickLeft);
        }
    }

    /// <inheritdoc />
    public GamePadState GetState()
    {
        return this.State ??= new GamePadState(
            leftThumbStick: this.LeftStickPos,
            rightThumbStick: this.RightStickPos,
            leftTrigger: this.LeftTrigger,
            rightTrigger: this.RightTrigger,
            buttons: this.PressedButtons.Count > 0
                ? this.PressedButtons.ToArray()
                : []
        );
    }
}
