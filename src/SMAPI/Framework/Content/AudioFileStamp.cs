using System.IO;

namespace StardewModdingAPI.Framework.Content;

internal readonly record struct AudioFileStamp(long Length, long LastWriteTimeUtcTicks)
{
    public static bool TryRead(string path, out AudioFileStamp stamp)
    {
        try
        {
            var file = new FileInfo(path);
            if (!file.Exists)
            {
                stamp = default;
                return false;
            }

            stamp = new AudioFileStamp(file.Length, file.LastWriteTimeUtc.Ticks);
            return true;
        }
        catch
        {
            stamp = default;
            return false;
        }
    }
}
