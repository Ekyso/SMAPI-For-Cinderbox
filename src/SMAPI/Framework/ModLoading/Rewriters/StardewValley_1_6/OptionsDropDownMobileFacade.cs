using System.Reflection;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI.Framework.ModLoading.Framework;
using StardewValley;
using StardewValley.Menus;

namespace StardewModdingAPI.Framework.ModLoading.Rewriters.StardewValley_1_6;

/// <summary>Mobile facade for <see cref="OptionsDropDown.draw"/> which has an extra
/// <c>IClickableMenu context</c> param on desktop for text wrapping.</summary>
/// <remarks>This is public to support SMAPI rewriting and should never be referenced directly by mods. See remarks on <see cref="ReplaceReferencesRewriter"/> for more info.</remarks>
public class OptionsDropDownMobileFacade : OptionsDropDown, IRewriteFacade
{
    /*********
    ** Fields
    *********/
    // Mobile: public bool dropDownOpen. Desktop: private bool clicked.
    // At runtime on mobile, dropDownOpen exists. Access via reflection since we compile against desktop.
    private static readonly FieldInfo? DropDownOpenField =
        typeof(OptionsDropDown).GetField(
            "dropDownOpen",
            BindingFlags.Public | BindingFlags.Instance
        )
        ?? typeof(OptionsDropDown).GetField(
            "clicked",
            BindingFlags.NonPublic | BindingFlags.Instance
        );

    /*********
    ** Public methods
    *********/
    public new void draw(SpriteBatch b, int slotX, int slotY, IClickableMenu? context = null)
    {
        recentSlotY = slotY;

        // Draw label with desktop text wrapping behavior
        OptionsElementMobileFacade.DrawLabel(this, b, slotX, slotY, Vector2.Zero, context);

        float alpha = greyedOut ? 0.33f : 1f;
        bool isExpanded = DropDownOpenField?.GetValue(this) is true;

        if (isExpanded)
        {
            IClickableMenuMobileFacade.drawTextureBox(
                b,
                Game1.mouseCursors,
                dropDownBGSource,
                slotX + dropDownBounds.X,
                slotY + dropDownBounds.Y,
                dropDownBounds.Width,
                dropDownBounds.Height,
                Color.White * alpha,
                4f,
                drawShadow: false,
                0.97f
            );

            for (int i = 0; i < dropDownDisplayOptions.Count; i++)
            {
                if (i == selectedOption)
                {
                    b.Draw(
                        Game1.staminaRect,
                        new Rectangle(
                            slotX + dropDownBounds.X,
                            slotY + dropDownBounds.Y + i * bounds.Height,
                            dropDownBounds.Width,
                            bounds.Height
                        ),
                        new Rectangle(0, 0, 1, 1),
                        Color.Wheat,
                        0f,
                        Vector2.Zero,
                        SpriteEffects.None,
                        0.975f
                    );
                }

                b.DrawString(
                    Game1.smallFont,
                    dropDownDisplayOptions[i],
                    new Vector2(
                        slotX + dropDownBounds.X + 4,
                        slotY + dropDownBounds.Y + 8 + bounds.Height * i
                    ),
                    Game1.textColor * alpha,
                    0f,
                    Vector2.Zero,
                    1f,
                    SpriteEffects.None,
                    0.98f
                );
            }

            b.Draw(
                Game1.mouseCursors,
                new Vector2(slotX + bounds.X + bounds.Width - 48, slotY + bounds.Y),
                dropDownButtonSource,
                Color.Wheat * alpha,
                0f,
                Vector2.Zero,
                4f,
                SpriteEffects.None,
                0.981f
            );
        }
        else
        {
            IClickableMenuMobileFacade.drawTextureBox(
                b,
                Game1.mouseCursors,
                dropDownBGSource,
                slotX + bounds.X,
                slotY + bounds.Y,
                bounds.Width - 48,
                bounds.Height,
                Color.White * alpha,
                4f,
                drawShadow: false
            );

            b.DrawString(
                Game1.smallFont,
                (selectedOption < dropDownDisplayOptions.Count && selectedOption >= 0)
                    ? dropDownDisplayOptions[selectedOption]
                    : "",
                new Vector2(slotX + bounds.X + 4, slotY + bounds.Y + 8),
                Game1.textColor * alpha,
                0f,
                Vector2.Zero,
                1f,
                SpriteEffects.None,
                0.88f
            );

            b.Draw(
                Game1.mouseCursors,
                new Vector2(slotX + bounds.X + bounds.Width - 48, slotY + bounds.Y),
                dropDownButtonSource,
                Color.White * alpha,
                0f,
                Vector2.Zero,
                4f,
                SpriteEffects.None,
                0.88f
            );
        }
    }

    /*********
    ** Private methods
    *********/
    private OptionsDropDownMobileFacade()
        : base("", 0)
    {
        RewriteHelper.ThrowFakeConstructorCalled();
    }
}
