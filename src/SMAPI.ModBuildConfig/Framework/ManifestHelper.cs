using System;
using System.IO;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using StardewModdingAPI.Toolkit.Framework;
using StardewModdingAPI.Toolkit.Serialization;
using StardewModdingAPI.Toolkit.Serialization.Models;

namespace StardewModdingAPI.ModBuildConfig.Framework;

/// <summary>Provides utilities for reading and validating mod manifest files.</summary>
internal class ManifestHelper
{
    /// <summary>Load a mod manifest from a file path.</summary>
    /// <param name="manifestPath">The file path to read.</param>
    /// <param name="projectVersion">The version number for the project assembly.</param>
    /// <param name="manifest">The manifest, if loaded successfully.</param>
    /// <param name="overrideManifestJson">The manifest JSON with tokens like <c>%ProjectVersion%</c> replaced, if loaded successfully different from the original file.</param>
    /// <param name="error">A human-readable error indicating why the manifest couldn't be loaded, if applicable.</param>
    /// <returns>Returns whether the manifest was loaded successfully.</returns>
    public static bool TryLoadManifest(string manifestPath, string projectVersion, out IManifest manifest, out string overrideManifestJson, out string error)
    {
        JsonHelper jsonHelper = new();

        // check manifest exists
        if (!File.Exists(manifestPath))
        {
            error = "The mod's manifest.json file doesn't exist.";
            manifest = null;
            overrideManifestJson = null;
            return false;
        }

        // get raw JSON & replace tokens
        string manifestJson;
        {
            string rawJson = File.ReadAllText(manifestPath);

            manifestJson = Regex.Replace(rawJson, "%ProjectVersion%", projectVersion, RegexOptions.IgnoreCase);
            overrideManifestJson = manifestJson != rawJson
                ? manifestJson
                : null;
        }

        // parse JSON
        try
        {
            manifest = jsonHelper.Deserialize<Manifest>(manifestJson);
        }
        catch (JsonReaderException ex)
        {
            // log the inner exception, otherwise the message will be generic
            Exception exToShow = ex.InnerException ?? ex;
            error = $"file has invalid JSON ({exToShow.Message})";
            manifest = null;
            return false;
        }

        // validate manifest fields
        if (!ManifestHelper.TryValidateFields(manifest, out error))
            return false;

        // valid
        error = null;
        return true;
    }

    /// <inheritdoc cref="ManifestValidator.TryValidateFields" />
    public static bool TryValidateFields(IManifest manifest, out string error)
    {
        return ManifestValidator.TryValidateFields(manifest, out error);
    }
}
