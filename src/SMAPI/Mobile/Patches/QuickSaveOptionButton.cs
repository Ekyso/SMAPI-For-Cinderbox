#if SMAPI_FOR_ANDROID
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.Menus;

namespace StardewModdingAPI.Mobile.Patches;

/// <summary>
/// Options menu button for QuickSave/QuickLoad.
/// Quick Load shows a cached SDV date string (including time) to its right.
/// </summary>
public class QuickSaveOptionButton : OptionsElement
{
    private readonly bool _isSaveButton;
    private static readonly Rectangle ButtonSource = new Rectangle(432, 439, 9, 9);

    public QuickSaveOptionButton(bool isSaveButton)
        : base(isSaveButton ? "Quick Save" : "Quick Load")
    {
        _isSaveButton = isSaveButton;
        whichOption = isSaveButton ? 99100 : 99101;

        int textWidth = (int)Game1.dialogueFont.MeasureString(label).X + 64;
        bounds = new Rectangle(32, 0, textWidth, 68);
    }

    public override void receiveLeftClick(int x, int y)
    {
        if (greyedOut || !bounds.Contains(x, y))
            return;

        Game1.playSound("bigSelect");

        if (_isSaveButton)
            QuickSavePatch.InvokeSave();
        else
            QuickSavePatch.InvokeLoad();
    }

    public override void draw(
        SpriteBatch b,
        int slotX,
        int slotY,
        IClickableMenu? context = null
    )
    {
        greyedOut = !_isSaveButton && !QuickSavePatch.QuicksaveExists;

        float textAlpha = greyedOut ? 0.33f : 1f;
        float drawLayer = 0.8f - (float)(slotY + bounds.Y) * 1E-06f;

        IClickableMenu.drawTextureBox(
            b,
            Game1.mouseCursors,
            ButtonSource,
            slotX + bounds.X,
            slotY + bounds.Y,
            bounds.Width,
            bounds.Height,
            Color.White,
            4f,
            drawShadow: true,
            drawLayer
        );

        Vector2 textCenter = Game1.dialogueFont.MeasureString(label) / 2f;
        textCenter.X = (int)(textCenter.X / 4f) * 4;
        textCenter.Y = (int)(textCenter.Y / 4f) * 4;
        Utility.drawTextWithShadow(
            b,
            label,
            Game1.dialogueFont,
            new Vector2(slotX + bounds.Center.X, slotY + bounds.Center.Y) - textCenter,
            Game1.textColor * textAlpha,
            1f,
            drawLayer + 1E-06f,
            -1,
            -1,
            0f
        );

        if (!_isSaveButton)
        {
            string dateStr = QuickSavePatch.QuicksaveExists
                ? (QuickSavePatch.QuicksaveDateString ?? "Quicksave date unknown")
                : "No quicksave";

            Utility.drawTextWithShadow(
                b,
                dateStr,
                Game1.smallFont,
                new Vector2(
                    slotX + bounds.X + bounds.Width + 16,
                    slotY
                        + bounds.Y
                        + (bounds.Height - Game1.smallFont.MeasureString(dateStr).Y) / 2f
                ),
                Game1.textColor,
                1f,
                drawLayer + 1E-06f,
                -1,
                -1,
                0f
            );
        }
    }
}
#endif
