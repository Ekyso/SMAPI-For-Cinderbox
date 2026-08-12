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
    /// <summary>The cache storing decoded data and its source-file stamp by absolute path.</summary>
    private readonly ConcurrentDictionary<string, CacheEntry> _cache
        = new(StringComparer.Ordinal); // case-sensitive for Android/Linux filesystem


    /*********
    ** Public methods
    *********/
    /// <summary>Get cached data when the source is unchanged, or load a stable copy from disk.</summary>
    /// <typeparam name="T">The decoded data type.</typeparam>
    /// <param name="absolutePath">The absolute source file path.</param>
    /// <param name="loader">Load and decode the source file.</param>
    public T GetOrLoad<T>(string absolutePath, Func<T> loader)
        where T : class
    {
        if (
            this._cache.TryGetValue(absolutePath, out CacheEntry? cached)
            && cached != null
            && cached.Data is T value
            && SourceFileStamp.TryRead(absolutePath, out SourceFileStamp currentStamp)
            && currentStamp == cached.SourceStamp
        )
        {
            return value;
        }

        this._cache.TryRemove(absolutePath, out _);

        T loaded = null!;
        for (int attempt = 0; attempt < 2; attempt++)
        {
            bool hadStamp = SourceFileStamp.TryRead(
                absolutePath,
                out SourceFileStamp beforeLoad
            );
            loaded = loader();

            if (
                hadStamp
                && SourceFileStamp.TryRead(absolutePath, out SourceFileStamp afterLoad)
                && beforeLoad == afterLoad
            )
            {
                this._cache[absolutePath] = new CacheEntry(loaded, afterLoad);
                return loaded;
            }
        }

        return loaded;
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

    private sealed record CacheEntry(object Data, SourceFileStamp SourceStamp);
}
