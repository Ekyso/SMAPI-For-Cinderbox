using System;
using System.IO;
using System.Xml.Serialization;
using StardewModdingAPI.Framework.ModLoading.Framework;
using StardewValley;
using StardewValley.Quests;
using StardewValley.SaveMigrations;
using StardewValley.TerrainFeatures;

namespace StardewModdingAPI.Framework.ModLoading.Rewriters.StardewValley_1_6;

/// <summary>Mobile facade for <see cref="SaveGame"/> fields missing on mobile.
/// Desktop has public static XmlSerializer fields and ensureFolderStructureExists.
/// Mobile uses SaveSerializer instead and doesn't expose these.</summary>
/// <remarks>This is public to support SMAPI rewriting and should never be referenced directly by mods. See remarks on <see cref="ReplaceReferencesRewriter"/> for more info.</remarks>
public class SaveGameMobileFacade : SaveGame, IRewriteFacade
{
    /*********
    ** Fields - backing storage for serializer properties
    *********/
    private static readonly XmlSerializer _serializer = new(
        typeof(SaveGame),
        new Type[]
        {
            typeof(Character),
            typeof(GameLocation),
            typeof(Item),
            typeof(Quest),
            typeof(TerrainFeature),
        }
    );

    private static readonly XmlSerializer _farmerSerializer = new(
        typeof(Farmer),
        new Type[] { typeof(Item) }
    );

    private static readonly XmlSerializer _locationSerializer = new(
        typeof(GameLocation),
        new Type[] { typeof(Character), typeof(Item), typeof(TerrainFeature) }
    );

    private static readonly XmlSerializer _descriptionElementSerializer = new(
        typeof(DescriptionElement),
        new Type[] { typeof(Character), typeof(Item) }
    );

    private static readonly XmlSerializer _legacyDescriptionElementSerializer = new(
        typeof(SaveMigrator_1_6.LegacyDescriptionElement),
        new Type[] { typeof(DescriptionElement), typeof(Character), typeof(Item) }
    );

    /*********
    ** Properties - MapFacade rewrites field references to property get/set calls
    *********/
    public static new XmlSerializer serializer
    {
        get => _serializer;
        set { }
    }

    public static new XmlSerializer farmerSerializer
    {
        get => _farmerSerializer;
        set { }
    }

    public static new XmlSerializer locationSerializer
    {
        get => _locationSerializer;
        set { }
    }

    public static new XmlSerializer descriptionElementSerializer
    {
        get => _descriptionElementSerializer;
        set { }
    }

    public static new XmlSerializer legacyDescriptionElementSerializer
    {
        get => _legacyDescriptionElementSerializer;
        set { }
    }

    /*********
    ** Methods
    *********/
    public new static void ensureFolderStructureExists()
    {
        string path =
            SaveGame.FilterFileName(Game1.GetSaveGameName()) + "_" + Game1.uniqueIDForThisGame;
        Directory.CreateDirectory(Path.Combine(StardewValley.Program.GetSavesFolder(), path));
    }

    /*********
    ** Private
    *********/
    private SaveGameMobileFacade()
    {
        RewriteHelper.ThrowFakeConstructorCalled();
    }
}
