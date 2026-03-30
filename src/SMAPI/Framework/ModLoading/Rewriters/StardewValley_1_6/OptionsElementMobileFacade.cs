using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI.Framework.ModLoading.Framework;
using StardewValley;
using StardewValley.BellsAndWhistles;
using StardewValley.Menus;

namespace StardewModdingAPI.Framework.ModLoading.Rewriters.StardewValley_1_6;

/// <summary>Mobile facade for <see cref="OptionsElement.draw"/> which has an extra
/// <c>IClickableMenu context</c> param on desktop for text wrapping.</summary>
/// <remarks>This is public to support SMAPI rewriting and should never be referenced directly by mods. See remarks on <see cref="ReplaceReferencesRewriter"/> for more info.</remarks>
public class OptionsElementMobileFacade : OptionsElement, IRewriteFacade
{
    /*********
    ** Fields - desktop has labelOffset, mobile doesn't
    *********/
    public new Vector2 labelOffset = Vector2.Zero;

    /*********
    ** Public methods
    *********/
    public new void draw(SpriteBatch b, int slotX, int slotY, IClickableMenu? context = null)
    {
        DrawLabel(this, b, slotX, slotY, Vector2.Zero, context);
    }

    /// <summary>Shared label draw logic implementing desktop's OptionsElement.draw behavior
    /// using public APIs. Called by both OptionsElement and OptionsDropDown facades.</summary>
    internal static void DrawLabel(
        OptionsElement element,
        SpriteBatch b,
        int slotX,
        int slotY,
        Vector2 labelOffset,
        IClickableMenu? context
    )
    {
        Color textColor = element.greyedOut ? (Game1.textColor * 0.33f) : Game1.textColor;

        if (element.style == Style.OptionLabel)
        {
            Utility.drawTextWithShadow(
                b,
                element.label,
                Game1.dialogueFont,
                new Vector2(
                    slotX + element.bounds.X + (int)labelOffset.X,
                    slotY + element.bounds.Y + (int)labelOffset.Y + 12
                ),
                textColor,
                1f,
                0.1f
            );
            return;
        }

        if (element.whichOption == -1)
        {
            SpriteText.drawString(
                b,
                element.label,
                slotX + element.bounds.X + (int)labelOffset.X,
                slotY
                    + element.bounds.Y
                    + (int)labelOffset.Y
                    + 56
                    - SpriteText.getHeightOfString(element.label),
                999,
                -1,
                999,
                1f,
                0.1f
            );
            return;
        }

        int labelX = slotX + element.bounds.X + element.bounds.Width + 8 + (int)labelOffset.X;
        int labelY = slotY + element.bounds.Y + (int)labelOffset.Y;
        string text = element.label;
        SpriteFont font = Game1.dialogueFont;

        if (context != null)
        {
            int availableRight = context.width - 64 + context.xPositionOnScreen;
            if (font.MeasureString(element.label).X + labelX > availableRight)
            {
                int wrapWidth = availableRight - labelX;
                font = Game1.smallFont;
                text = Game1.parseText(element.label, font, wrapWidth);
                labelY -= (int)((font.MeasureString(text).Y - font.MeasureString("T").Y) / 2f);
            }
        }

        Utility.drawTextWithShadow(b, text, font, new Vector2(labelX, labelY), textColor, 1f, 0.1f);
    }

    /*********
    ** Private methods
    *********/
    private OptionsElementMobileFacade()
        : base("")
    {
        RewriteHelper.ThrowFakeConstructorCalled();
    }
}
