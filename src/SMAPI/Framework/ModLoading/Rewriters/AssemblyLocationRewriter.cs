#if SMAPI_FOR_ANDROID
using System;
using System.Reflection;
using Mono.Cecil;
using Mono.Cecil.Cil;
using StardewModdingAPI.Framework.ModLoading.Framework;

namespace StardewModdingAPI.Framework.ModLoading.Rewriters;

/// <summary>Rewrites Assembly.Location calls to return correct paths on Android.</summary>
internal class AssemblyLocationRewriter : BaseInstructionHandler
{
    /*********
    ** Fields
    *********/
    /// <summary>Cached reference to the helper method.</summary>
    private MethodReference? CachedHelperMethod;

    /*********
    ** Public methods
    *********/
    /// <summary>Construct an instance.</summary>
    public AssemblyLocationRewriter()
        : base(defaultPhrase: "Assembly.Location (Android fix)")
    {
    }

    /// <inheritdoc />
    public override bool Handle(ModuleDefinition module, ILProcessor cil, Instruction instruction)
    {
        if (!this.IsLocationGetter(instruction))
            return false;

        var helperMethod = this.GetHelperMethodReference(module);
        if (helperMethod == null)
            return false;

        instruction.Operand = helperMethod;
        instruction.OpCode = OpCodes.Call;

        this.MarkRewritten();
        this.Phrases.Add("Assembly.Location -> AssemblyLocationHelper.GetLocation (Android)");

        return true;
    }

    /*********
    ** Private methods
    *********/
    /// <summary>Check if the instruction is a call to Assembly.get_Location().</summary>
    private bool IsLocationGetter(Instruction instruction)
    {
        if (instruction.OpCode != OpCodes.Callvirt && instruction.OpCode != OpCodes.Call)
            return false;

        if (instruction.Operand is not MethodReference methodRef)
            return false;

        return methodRef.DeclaringType?.FullName == "System.Reflection.Assembly"
               && methodRef.Name == "get_Location"
               && methodRef.Parameters.Count == 0;
    }

    /// <summary>Get a reference to the helper method.</summary>
    private MethodReference? GetHelperMethodReference(ModuleDefinition module)
    {
        if (this.CachedHelperMethod != null)
            return this.CachedHelperMethod;

        try
        {
            var helperType = typeof(AssemblyLocationHelper);
            var method = helperType.GetMethod(
                nameof(AssemblyLocationHelper.GetLocation),
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[] { typeof(Assembly) },
                null
            );

            if (method == null)
                return null;

            this.CachedHelperMethod = module.ImportReference(method);
            return this.CachedHelperMethod;
        }
        catch
        {
            return null;
        }
    }
}

/// <summary>Provides Assembly.Location functionality on Android where the property returns empty for bundled assemblies.</summary>
public static class AssemblyLocationHelper
{
    /// <summary>The path to the desktop DLLs folder on Android.</summary>
    private static string? DesktopDllsPath;

    /// <summary>The path to the mods folder on Android.</summary>
    private static string? ModsPath;

    /// <summary>Initialize the helper with the paths where assemblies can be found.</summary>
    /// <param name="desktopDllsPath">Path to the desktop DLLs folder (for game assemblies).</param>
    /// <param name="modsPath">Path to the mods folder (for mod assemblies).</param>
    public static void Initialize(string desktopDllsPath, string modsPath)
    {
        DesktopDllsPath = desktopDllsPath;
        ModsPath = modsPath;
    }

    /// <summary>Get the file path of an assembly, working around Android returning empty for bundled assemblies.</summary>
    /// <param name="assembly">The assembly to get the location for.</param>
    /// <returns>The assembly file path, or empty string if not found.</returns>
    public static string GetLocation(Assembly assembly)
    {
        if (assembly == null)
            return string.Empty;

        // try native Location property first
        var nativeLocation = assembly.Location;
        if (!string.IsNullOrEmpty(nativeLocation))
            return nativeLocation;

        var assemblyName = assembly.GetName().Name;
        if (string.IsNullOrEmpty(assemblyName))
            return string.Empty;

        var dllName = assemblyName + ".dll";

        // check desktop DLLs path
        if (!string.IsNullOrEmpty(DesktopDllsPath))
        {
            var gamePath = System.IO.Path.Combine(DesktopDllsPath, dllName);
            if (System.IO.File.Exists(gamePath))
                return gamePath;
        }

        // check mods path (skip dot-prefixed directories — those are disabled mods)
        if (!string.IsNullOrEmpty(ModsPath) && System.IO.Directory.Exists(ModsPath))
        {
            try
            {
                var match = FindDllRecursive(new System.IO.DirectoryInfo(ModsPath), dllName);
                if (match != null)
                    return match;
            }
            catch
            {
            }
        }

        // fallback
        return string.Empty;
    }

    /// <summary>Recursively search for a DLL file, skipping dot-prefixed (disabled) mod folders.</summary>
    private static string? FindDllRecursive(System.IO.DirectoryInfo dir, string dllName)
    {
        try
        {
            var file = new System.IO.FileInfo(System.IO.Path.Combine(dir.FullName, dllName));
            if (file.Exists)
                return file.FullName;

            foreach (var sub in dir.EnumerateDirectories())
            {
                if (sub.Name.StartsWith("."))
                    continue;

                var result = FindDllRecursive(sub, dllName);
                if (result != null)
                    return result;
            }
        }
        catch
        {
        }

        return null;
    }
}
#endif
