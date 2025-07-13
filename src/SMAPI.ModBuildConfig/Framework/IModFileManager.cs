using System.Collections.Generic;

namespace StardewModdingAPI.ModBuildConfig.Framework;

/// <summary>Manages the files that are part of a mod in the release package.</summary>
internal interface IModFileManager
{
    /// <summary>Get the files in the mod package.</summary>
    public IEnumerable<BundleFile> GetFiles();
}
