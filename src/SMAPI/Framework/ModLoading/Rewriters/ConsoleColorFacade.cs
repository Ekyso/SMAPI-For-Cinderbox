using System;

#pragma warning disable CS1591 // public only so rewritten mod assemblies can call these members

namespace StardewModdingAPI.Framework.ModLoading.Rewriters;

/// <summary>Provides safe no-op console colors for rewritten mods running in the Android host.</summary>
/// <remarks>This is public so rewritten mod assemblies can call it. Mods should not reference it directly.</remarks>
public static class ConsoleColorFacade
{
    public static ConsoleColor ForegroundColor
    {
        get => ConsoleColor.Gray;
        set { }
    }

    public static ConsoleColor BackgroundColor
    {
        get => ConsoleColor.Black;
        set { }
    }

    public static void ResetColor() { }
}

#pragma warning restore CS1591
