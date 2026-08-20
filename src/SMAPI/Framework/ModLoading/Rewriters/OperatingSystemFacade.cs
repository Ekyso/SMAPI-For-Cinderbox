using System;

#pragma warning disable CS1591 // public only so rewritten mod assemblies can call these methods

namespace StardewModdingAPI.Framework.ModLoading.Rewriters;

/// <summary>Provides operating-system checks based on the game platform selected by the launcher.</summary>
/// <remarks>This is public so rewritten mod assemblies can call it. Mods should not reference it directly.</remarks>
public static class OperatingSystemFacade
{
    public static bool IsOSPlatform(string platform)
    {
        ArgumentNullException.ThrowIfNull(platform);

        return IsOSPlatform(Constants.TargetPlatform, platform);
    }

    internal static bool IsOSPlatform(GamePlatform targetPlatform, string platform)
    {
        ArgumentNullException.ThrowIfNull(platform);

        return targetPlatform switch
        {
            GamePlatform.Android => platform.Equals("ANDROID", StringComparison.OrdinalIgnoreCase),
            GamePlatform.Linux => platform.Equals("LINUX", StringComparison.OrdinalIgnoreCase),
            GamePlatform.Mac =>
                platform.Equals("OSX", StringComparison.OrdinalIgnoreCase)
                || platform.Equals("MACOS", StringComparison.OrdinalIgnoreCase),
            GamePlatform.Windows => platform.Equals("WINDOWS", StringComparison.OrdinalIgnoreCase),
            _ => false,
        };
    }

    public static bool IsOSPlatformVersionAtLeast(
        string platform,
        int major,
        int minor = 0,
        int build = 0,
        int revision = 0
    )
    {
        if (!IsOSPlatform(platform))
            return false;

        // The Android host can intentionally present the desktop game to mods as Linux. In that
        // mode the real platform predicate is Android, so use the host kernel version instead of
        // asking OperatingSystem to reject the launcher-selected Linux identity.
        return OperatingSystem.IsOSPlatform(platform)
            ? OperatingSystem.IsOSPlatformVersionAtLeast(platform, major, minor, build, revision)
            : IsOSPlatformVersionAtLeast(
                Constants.TargetPlatform,
                platform,
                Environment.OSVersion.Version,
                major,
                minor,
                build,
                revision
            );
    }

    internal static bool IsOSPlatformVersionAtLeast(
        GamePlatform targetPlatform,
        string platform,
        Version currentVersion,
        int major,
        int minor = 0,
        int build = 0,
        int revision = 0
    )
    {
        ArgumentNullException.ThrowIfNull(currentVersion);

        if (!IsOSPlatform(targetPlatform, platform))
            return false;

        Version normalizedCurrentVersion = new(
            Math.Max(0, currentVersion.Major),
            Math.Max(0, currentVersion.Minor),
            Math.Max(0, currentVersion.Build),
            Math.Max(0, currentVersion.Revision)
        );
        return normalizedCurrentVersion >= new Version(major, minor, build, revision);
    }

    public static bool IsBrowser() => false;

    public static bool IsWasi() => false;

    public static bool IsLinux() => Constants.TargetPlatform == GamePlatform.Linux;

    public static bool IsFreeBSD() => false;

    public static bool IsFreeBSDVersionAtLeast(
        int major,
        int minor = 0,
        int build = 0,
        int revision = 0
    ) => false;

    public static bool IsAndroid() => Constants.TargetPlatform == GamePlatform.Android;

    public static bool IsAndroidVersionAtLeast(
        int major,
        int minor = 0,
        int build = 0,
        int revision = 0
    ) =>
        IsAndroid() && OperatingSystem.IsAndroidVersionAtLeast(major, minor, build, revision);

    public static bool IsIOS() => false;

    public static bool IsIOSVersionAtLeast(int major, int minor = 0, int build = 0) => false;

    public static bool IsMacOS() => Constants.TargetPlatform == GamePlatform.Mac;

    public static bool IsMacOSVersionAtLeast(int major, int minor = 0, int build = 0) =>
        IsMacOS() && OperatingSystem.IsMacOSVersionAtLeast(major, minor, build);

    public static bool IsMacCatalyst() => false;

    public static bool IsMacCatalystVersionAtLeast(int major, int minor = 0, int build = 0) =>
        false;

    public static bool IsTvOS() => false;

    public static bool IsTvOSVersionAtLeast(int major, int minor = 0, int build = 0) => false;

    public static bool IsWatchOS() => false;

    public static bool IsWatchOSVersionAtLeast(int major, int minor = 0, int build = 0) => false;

    public static bool IsWindows() => Constants.TargetPlatform == GamePlatform.Windows;

    public static bool IsWindowsVersionAtLeast(
        int major,
        int minor = 0,
        int build = 0,
        int revision = 0
    ) =>
        IsWindows() && OperatingSystem.IsWindowsVersionAtLeast(major, minor, build, revision);
}

#pragma warning restore CS1591
