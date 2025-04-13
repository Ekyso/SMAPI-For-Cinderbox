using System;
using System.IO;
using System.Text;

namespace StardewModdingAPI.ModBuildConfig.Framework;

/// <summary>A file that should be deployed or zipped as part of a mod.</summary>
internal class BundleFile
{
    /*********
    ** Accessors
    *********/
    /// <summary>The name of the manifest file.</summary>
    public const string ManifestFileName = "manifest.json";

    /// <summary>The file's relative path within the mod.</summary>
    public string RelativePath { get; }

    /// <summary>The file to copy from.</summary>
    public FileInfo File { get; }

    /// <summary>If set, deploy this content instead of copying the original file.</summary>
    public string OverrideContent { get; }

    /*********
    ** Public methods
    *********/
    /// <summary>Construct an instance.</summary>
    /// <param name="relativePath">The file's relative path within the mod.</param>
    /// <param name="file">The file to copy from.</param>
    /// <param name="overrideContent">If set, deploy this content instead of copying the original file.</param>
    public BundleFile(string relativePath, FileInfo file, string overrideContent = null)
    {
        this.RelativePath = relativePath;
        this.File = file;
        this.OverrideContent = overrideContent;
    }

    /// <summary>Get whether this entry is for the mod's <samp>manifest.json</samp> file.</summary>
    public bool IsModManifest()
    {
        return BundleFile.IsModManifest(this.RelativePath);
    }

    /// <summary>Copy the file into a destination folder.</summary>
    /// <param name="folderPath">The folder path to use as the base for the <see cref="RelativePath"/>.</param>
    public void CopyToFolder(string folderPath)
    {
        string toPath = Path.Combine(folderPath, this.RelativePath);

        Directory.CreateDirectory(Path.GetDirectoryName(toPath)!);

        if (this.OverrideContent != null)
            System.IO.File.WriteAllText(toPath, this.OverrideContent, Encoding.UTF8);
        else
            this.File.CopyTo(toPath, overwrite: true);
    }

    /// <summary>Copy the file's contents into a stream.</summary>
    /// <param name="stream">The stream into which to write the file's contents.</param>
    public void CopyToStream(Stream stream)
    {
        using Stream fromStream = this.OverrideContent != null
            ? new MemoryStream(Encoding.UTF8.GetBytes(this.OverrideContent))
            : this.File.OpenRead();

        fromStream.CopyTo(stream);
    }

    /// <summary>Get whether a relative path is for the mod's <samp>manifest.json</samp> file.</summary>
    /// <param name="relativePath">The relative path within the mod folder within the mod's folder.</param>
    public static bool IsModManifest(string relativePath)
    {
        return string.Equals(relativePath, BundleFile.ManifestFileName, StringComparison.OrdinalIgnoreCase);
    }
}
