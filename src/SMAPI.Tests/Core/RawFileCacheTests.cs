using System;
using System.IO;
using FluentAssertions;
using NUnit.Framework;
using StardewModdingAPI.Framework.Content;

namespace SMAPI.Tests.Core;

[TestFixture]
internal class RawFileCacheTests
{
    [Test]
    public void GetOrLoad_ReusesDataWhileSourceIsUnchanged()
    {
        string path = CreateTempFile("first");
        try
        {
            using var cache = new RawFileCache();
            int loads = 0;

            string first = cache.GetOrLoad(path, Load);
            string second = cache.GetOrLoad(path, Load);

            first.Should().Be("first");
            second.Should().Be("first");
            loads.Should().Be(1);

            string Load()
            {
                loads++;
                return File.ReadAllText(path);
            }
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public void GetOrLoad_ReloadsDataWhenLengthChangesWithSameTimestamp()
    {
        string path = CreateTempFile("first");
        try
        {
            using var cache = new RawFileCache();
            int loads = 0;
            DateTime timestamp = File.GetLastWriteTimeUtc(path);

            cache.GetOrLoad(path, Load).Should().Be("first");
            File.WriteAllText(path, "second-value");
            File.SetLastWriteTimeUtc(path, timestamp);
            cache.GetOrLoad(path, Load).Should().Be("second-value");

            loads.Should().Be(2);

            string Load()
            {
                loads++;
                return File.ReadAllText(path);
            }
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public void GetOrLoad_ReloadsDataWhenTimestampChangesWithSameLength()
    {
        string path = CreateTempFile("first");
        try
        {
            using var cache = new RawFileCache();
            int loads = 0;

            cache.GetOrLoad(path, Load).Should().Be("first");
            File.WriteAllText(path, "other");
            File.SetLastWriteTimeUtc(path, new DateTime(2030, 1, 2, 3, 4, 5, DateTimeKind.Utc));
            cache.GetOrLoad(path, Load).Should().Be("other");

            loads.Should().Be(2);

            string Load()
            {
                loads++;
                return File.ReadAllText(path);
            }
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public void GetOrLoad_DoesNotCacheAFileThatChangesDuringLoad()
    {
        string path = CreateTempFile("first");
        try
        {
            using var cache = new RawFileCache();
            int loads = 0;

            string result = cache.GetOrLoad(path, () =>
            {
                loads++;
                string value = File.ReadAllText(path);
                File.WriteAllText(path, value + "x");
                return value;
            });

            result.Should().Be("firstx");
            loads.Should().Be(2);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static string CreateTempFile(string contents)
    {
        string path = Path.Combine(Path.GetTempPath(), $"smapi-raw-{Guid.NewGuid():N}.dat");
        File.WriteAllText(path, contents);
        return path;
    }
}
