namespace StardewModdingAPI.Web.Framework.ConfigModels;

/// <summary>The config settings for the Stardew mod dataset repo.</summary>
internal class ModDatasetConfig
{
    /*********
    ** Accessors
    *********/
    /// <summary>The HTTPS URL of the mod dataset Git repo.</summary>
    public string RepoUrl { get; set; } = null!;

    /// <summary>The local path into which to clone the mod dataset repo.</summary>
    public string LocalPath { get; set; } = null!;
}
