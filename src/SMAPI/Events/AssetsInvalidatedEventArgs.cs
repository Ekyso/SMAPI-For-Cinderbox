using System;
using System.Collections.Generic;

namespace StardewModdingAPI.Events;

/// <summary>Event arguments for an <see cref="IContentEvents.AssetsInvalidated"/> event.</summary>
public class AssetsInvalidatedEventArgs : EventArgs
{
    /*********
    ** Accessors
    *********/
    /// <summary>The asset names that were invalidated.</summary>
    public IReadOnlySet<IAssetName> Names { get; }

    /// <summary>The <see cref="Names"/> with any locale codes stripped.</summary>
    /// <remarks>For example, if <see cref="Names"/> contains a locale like <c>Data/Bundles.fr-FR</c>, this will have the name without locale like <c>Data/Bundles</c>. If the name has no locale, this field is equivalent.</remarks>
    public IReadOnlySet<IAssetName> NamesWithoutLocale { get; }


    /*********
    ** Public methods
    *********/
    /// <summary>Construct an instance.</summary>
    /// <param name="assetNames">The asset names that were invalidated.</param>
    internal AssetsInvalidatedEventArgs(ICollection<IAssetName> assetNames)
    {
        HashSet<IAssetName> names = new(assetNames);

        HashSet<IAssetName> namesWithoutLocale = [];
        foreach (IAssetName name in names)
            namesWithoutLocale.Add(name.GetBaseAssetName());

        this.Names = names;
        this.NamesWithoutLocale = namesWithoutLocale;
    }
}
