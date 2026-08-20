using System.Reflection;
using StardewModdingAPI.Framework.ModLoading.Framework;
using StardewValley;
using StardewValley.Menus;

namespace StardewModdingAPI.Framework.ModLoading.Rewriters.StardewValley_1_6;

/// <summary>Mobile facade for desktop <see cref="InventoryPage"/> members which are missing or unusable on mobile.</summary>
/// <remarks>This is public to support SMAPI rewriting and should never be referenced directly by mods. See remarks on <see cref="ReplaceReferencesRewriter"/> for more info.</remarks>
public class InventoryPageMobileFacade : InventoryPage, IRewriteFacade
{
    /*********
    ** Fields
    *********/
    /// <summary>Mobile moved the usable trash and organize controls into <see cref="InventoryMenu"/>. Resolve them at runtime since SMAPI compiles against the desktop game assembly, where those fields don't exist.</summary>
    private static readonly FieldInfo? InventoryTrashCanField = GetInventoryMenuField("trashCan");
    private static readonly FieldInfo? InventoryOrganizeButtonField = GetInventoryMenuField(
        "organizeButton"
    );

    /*********
    ** Properties
    *********/
    /// <summary>Get the real mobile trash control from the embedded inventory.</summary>
    public new ClickableTextureComponent? trashCan
    {
        get =>
            base.trashCan
            ?? GetInventoryMenuFieldValue(
                (InventoryPage)(object)this,
                InventoryTrashCanField
            );
        set
        {
            if (
                !TrySetInventoryMenuFieldValue(
                    (InventoryPage)(object)this,
                    InventoryTrashCanField,
                    value
                )
            )
                base.trashCan = value;
        }
    }

    /// <summary>Get the real mobile organize control from the embedded inventory.</summary>
    public new ClickableTextureComponent? organizeButton
    {
        get =>
            base.organizeButton
            ?? GetInventoryMenuFieldValue(
                (InventoryPage)(object)this,
                InventoryOrganizeButtonField
            );
        set
        {
            if (
                !TrySetInventoryMenuFieldValue(
                    (InventoryPage)(object)this,
                    InventoryOrganizeButtonField,
                    value
                )
            )
                base.organizeButton = value;
        }
    }

    /// <summary>Get the inventory page's note control. This stays null on stock mobile, where the control belongs to <see cref="GameMenu"/>.</summary>
    public new ClickableTextureComponent? junimoNoteIcon
    {
        get => base.junimoNoteIcon;
        set => base.junimoNoteIcon = value;
    }

    /*********
    ** Public methods
    *********/
    public new static bool ShouldShowJunimoNoteIcon()
    {
        // Stock mobile owns this button on GameMenu, not InventoryPage. Returning false keeps
        // desktop mods from dereferencing InventoryPage.junimoNoteIcon, which mobile leaves null.
        if (InventoryOrganizeButtonField is not null)
            return false;

        if (
            Game1.player.hasOrWillReceiveMail("canReadJunimoText")
            && !Game1.player.hasOrWillReceiveMail("JojaMember")
        )
        {
            if (Game1.MasterPlayer.hasCompletedCommunityCenter())
            {
                if (Game1.player.hasOrWillReceiveMail("hasSeenAbandonedJunimoNote"))
                    return !Game1.MasterPlayer.hasOrWillReceiveMail("ccMovieTheater");
                return false;
            }
            return true;
        }
        return false;
    }

    /*********
    ** Private methods
    *********/
    private static FieldInfo? GetInventoryMenuField(string name)
    {
        return typeof(InventoryMenu).GetField(
            name,
            BindingFlags.Public | BindingFlags.Instance
        );
    }

    private static ClickableTextureComponent? GetInventoryMenuFieldValue(
        InventoryPage page,
        FieldInfo? field
    )
    {
        return page.inventory is not null
            ? field?.GetValue(page.inventory) as ClickableTextureComponent
            : null;
    }

    private static bool TrySetInventoryMenuFieldValue(
        InventoryPage page,
        FieldInfo? field,
        ClickableTextureComponent? value
    )
    {
        if (page.inventory is null || field is null)
            return false;

        field.SetValue(page.inventory, value);
        return true;
    }

    private InventoryPageMobileFacade()
        : base(0, 0, 0, 0)
    {
        RewriteHelper.ThrowFakeConstructorCalled();
    }
}
