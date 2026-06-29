namespace StardewModdingAPI.Web.Framework.ConfigModels;

/// <summary>The config settings for the Stardew mod dataset repo.</summary>
internal class ModDatasetConfig
{
    /*********
    ** Accessors
    *********/
    /// <summary>The URL of the zip file to download which contains the Stardew mod dataset (e.g. a GitHub branch download URL).</summary>
    public string DownloadZipUrl { get; set; } = null!;

    /// <summary>The full path to the folder into which to download the mod dataset. This should be a new folder, since anything inside it may be deleted.</summary>
    public string LocalRootPath { get; set; } = null!;
}
