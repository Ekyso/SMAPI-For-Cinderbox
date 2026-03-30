using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI.Framework.ModLoading.Framework;
using StardewValley;
using StardewValley.Menus;

namespace StardewModdingAPI.Framework.ModLoading.Rewriters.StardewValley_1_6;

/// <summary>Mobile facade for <see cref="SocialPage.drawNPCSlotHeart"/>
/// which exists on desktop but not mobile.</summary>
/// <remarks>This is public to support SMAPI rewriting and should never be referenced directly by mods. See remarks on <see cref="ReplaceReferencesRewriter"/> for more info.</remarks>
public class SocialPageMobileFacade : SocialPage, IRewriteFacade
{
    /*********
    ** Public methods
    *********/
    public void drawNPCSlotHeart(
        SpriteBatch b,
        int npcIndex,
        SocialPage.SocialEntry entry,
        int hearts,
        bool isDating,
        bool isCurrentSpouse
    )
    {
        bool locked = entry.IsDatable && !isDating && !isCurrentSpouse && hearts >= 8;
        int srcX = (hearts < entry.HeartLevel || locked) ? 211 : 218;
        Color color = (hearts < 10 && locked) ? (Color.Black * 0.35f) : Color.White;

        if (hearts < 10)
        {
            b.Draw(
                Game1.mouseCursors,
                new Vector2(
                    base.xPositionOnScreen + 320 - 4 + hearts * 32,
                    base.sprites[npcIndex].bounds.Y + 64 - 28
                ),
                new Rectangle(srcX, 428, 7, 6),
                color,
                0f,
                Vector2.Zero,
                4f,
                SpriteEffects.None,
                0.88f
            );
        }
        else
        {
            b.Draw(
                Game1.mouseCursors,
                new Vector2(
                    base.xPositionOnScreen + 320 - 4 + (hearts - 10) * 32,
                    base.sprites[npcIndex].bounds.Y + 64
                ),
                new Rectangle(srcX, 428, 7, 6),
                color,
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
    private SocialPageMobileFacade()
        : base(0, 0, 0, 0)
    {
        RewriteHelper.ThrowFakeConstructorCalled();
    }
}
