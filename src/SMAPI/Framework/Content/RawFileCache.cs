#if SMAPI_FOR_ANDROID
using System;
using System.Collections.Concurrent;

namespace StardewModdingAPI.Framework.Content;

/// <summary>
/// A thread-safe cache for decoded raw file data (PNG textures, JSON strings).
/// Persists across invalidation cycles so the same mod files aren't re-decoded repeatedly.
/// </summary>
internal sealed class RawFileCache : IDisposable
{
    /*********
    ** Fields
    *********/
    /// <summary>The cache storing decoded data keyed by absolute file path.</summary>
    /// <remarks>
    /// Values are <see cref="IRawTextureData"/> or <see cref="string"/>.
    /// </remarks>
    private readonly ConcurrentDictionary<string, object> _cache
        = new(StringComparer.Ordinal); // case-sensitive for Android/Linux filesystem


    /*********
    ** Public methods
    *********/
    /// <summary>Try to get cached raw texture data for a PNG file.</summary>
    /// <param name="absolutePath">The absolute file path.</param>
    /// <param name="data">The cached texture data, if found.</param>
    /// <returns>Whether the data was found in cache.</returns>
    public bool TryGetRawTexture(string absolutePath, out IRawTextureData? data)
    {
        if (this._cache.TryGetValue(absolutePath, out object? value) && value is IRawTextureData texture)
        {
            data = texture;
            return true;
        }

        data = null;
        return false;
    }

    /// <summary>Try to get a cached JSON string.</summary>
    /// <param name="absolutePath">The absolute file path.</param>
    /// <param name="json">The cached JSON string, if found.</param>
    /// <returns>Whether the data was found in cache.</returns>
    public bool TryGetJsonString(string absolutePath, out string? json)
    {
        if (this._cache.TryGetValue(absolutePath, out object? value) && value is string str)
        {
            json = str;
            return true;
        }

        json = null;
        return false;
    }

    /// <summary>Store decoded data in the cache.</summary>
    /// <param name="absolutePath">The absolute file path.</param>
    /// <param name="data">The decoded data (<see cref="IRawTextureData"/> or <see cref="string"/>).</param>
    public void Set(string absolutePath, object data)
    {
        this._cache[absolutePath] = data;
    }

    /// <summary>Clear all cached data.</summary>
    public void Clear()
    {
        this._cache.Clear();
    }

    /// <summary>Clear cache and dispose resources.</summary>
    public void Dispose()
    {
        this._cache.Clear();
    }
}
#endif
