using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Mono.Cecil;

namespace StardewModdingAPI.Framework.ModLoading;

/// <summary>A minimal assembly definition resolver which resolves references to known assemblies.</summary>
internal class AssemblyDefinitionResolver : IAssemblyResolver
{
    /*********
    ** Fields
    *********/
    /// <summary>The underlying assembly resolver.</summary>
    private readonly DefaultAssemblyResolverWrapper Resolver = new();

    /// <summary>The known assemblies.</summary>
    private readonly IDictionary<string, AssemblyDefinition> Lookup = new Dictionary<string, AssemblyDefinition>();

    /// <summary>The directory paths to search for assemblies.</summary>
    private readonly HashSet<string> SearchPaths = [];

    /// <summary>Assemblies that were already checked in the AppDomain and not found on disk.</summary>
    private readonly HashSet<string> RuntimeResolveFailed = [];


    /*********
    ** Public methods
    *********/
    /// <summary>Construct an instance.</summary>
    public AssemblyDefinitionResolver()
    {
        foreach (string path in this.Resolver.GetSearchDirectories())
            this.SearchPaths.Add(path);
    }

    /// <summary>Add known assemblies to the resolver.</summary>
    /// <param name="assemblies">The known assemblies.</param>
    public void Add(params AssemblyDefinition[] assemblies)
    {
        foreach (AssemblyDefinition assembly in assemblies)
            this.AddWithExplicitNames(assembly, assembly.Name.Name, assembly.Name.FullName);
    }

    /// <summary>Add a known assembly to the resolver with the given names. This overrides the assembly names that would normally be assigned.</summary>
    /// <param name="assembly">The assembly to add.</param>
    /// <param name="names">The assembly names for which it should be returned.</param>
    public void AddWithExplicitNames(AssemblyDefinition assembly, params string[] names)
    {
        this.Resolver.AddAssembly(assembly);
        foreach (string name in names)
            this.Lookup[name] = assembly;
    }

    /// <summary>Resolve an assembly reference.</summary>
    /// <param name="name">The assembly name.</param>
    /// <exception cref="AssemblyResolutionException">The assembly can't be resolved.</exception>
    public AssemblyDefinition Resolve(AssemblyNameReference name)
    {
        return this.ResolveName(name.Name)
            ?? this.TryResolveFromSearchPaths(name)
            ?? this.TryResolveFromRuntime(name)
            ?? throw new AssemblyResolutionException(name);
    }

    /// <summary>Resolve an assembly reference.</summary>
    /// <param name="name">The assembly name.</param>
    /// <param name="parameters">The assembly reader parameters.</param>
    /// <exception cref="AssemblyResolutionException">The assembly can't be resolved.</exception>
    public AssemblyDefinition Resolve(AssemblyNameReference name, ReaderParameters parameters)
    {
        return this.ResolveName(name.Name)
            ?? this.TryResolveFromSearchPaths(name, parameters)
            ?? this.TryResolveFromRuntime(name)
            ?? throw new AssemblyResolutionException(name);
    }

    /// <summary>Add a directory path to search for assemblies, if it's non-null and not already added.</summary>
    /// <param name="path">The path to search.</param>
    /// <returns>Returns whether the path was successfully added.</returns>
    public bool TryAddSearchDirectory(string? path)
    {
        if (path is not null && this.SearchPaths.Add(path))
        {
            this.Resolver.AddSearchDirectory(path);
            return true;
        }

        return false;
    }

    /// <summary>Remove a directory path to search for assemblies, if it's non-null.</summary>
    /// <param name="path">The path to remove.</param>
    /// <returns>Returns whether the path was in the list and removed.</returns>
    public bool RemoveSearchDirectory(string? path)
    {
        if (path is not null && this.SearchPaths.Remove(path))
        {
            this.Resolver.RemoveSearchDirectory(path);
            return true;
        }

        return false;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        this.Resolver.Dispose();
    }


    /*********
    ** Private methods
    *********/
    /// <summary>Resolve a known assembly definition based on its short or full name.</summary>
    /// <param name="name">The assembly's short or full name.</param>
    private AssemblyDefinition? ResolveName(string name)
    {
        return this.Lookup.TryGetValue(name, out AssemblyDefinition? match)
            ? match
            : null;
    }

    /// <summary>Try to resolve an assembly from the search directory paths.</summary>
    /// <param name="name">The assembly name reference.</param>
    /// <param name="parameters">Optional reader parameters.</param>
    private AssemblyDefinition? TryResolveFromSearchPaths(AssemblyNameReference name, ReaderParameters? parameters = null)
    {
        try
        {
            return parameters != null
                ? this.Resolver.Resolve(name, parameters)
                : this.Resolver.Resolve(name);
        }
        catch (AssemblyResolutionException)
        {
            return null;
        }
    }

    /// <summary>Try to resolve an assembly from the runtime by checking loaded AppDomain assemblies.</summary>
    /// <param name="name">The assembly name reference.</param>
    private AssemblyDefinition? TryResolveFromRuntime(AssemblyNameReference name)
    {
        if (this.RuntimeResolveFailed.Contains(name.Name))
            return null;

        try
        {
            Assembly? loaded = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == name.Name);

            if (loaded == null)
            {
                System.Console.WriteLine($"[AssemblyResolver] '{name.Name}' not found in AppDomain");
                this.RuntimeResolveFailed.Add(name.Name);
                return null;
            }

#pragma warning disable IL3000 // Assembly.Location may return empty on Android for APK-embedded assemblies
            string location = loaded.Location;
#pragma warning restore IL3000

            System.Console.WriteLine($"[AssemblyResolver] '{name.Name}' found in AppDomain, Location='{(string.IsNullOrEmpty(location) ? "<empty>" : location)}'");

            if (string.IsNullOrEmpty(location) || !File.Exists(location))
            {
#if SMAPI_FOR_ANDROID
                // Create a stub definition for APK-embedded assemblies with no file location.
                System.Console.WriteLine($"[AssemblyResolver] '{name.Name}' has no file location (APK-embedded), creating stub for Cecil");
                var stubDefinition = AssemblyDefinition.CreateAssembly(
                    new AssemblyNameDefinition(name.Name, name.Version),
                    name.Name,
                    ModuleKind.Dll
                );
                this.Lookup[name.Name] = stubDefinition;
                this.Resolver.AddAssembly(stubDefinition);
                return stubDefinition;
#else
                System.Console.WriteLine($"[AssemblyResolver] '{name.Name}' has no valid file location, cannot resolve for Cecil");
                this.RuntimeResolveFailed.Add(name.Name);
                return null;
#endif
            }

            // read with deferred mode for metadata-only resolution
            var definition = AssemblyDefinition.ReadAssembly(
                location,
                new ReaderParameters(ReadingMode.Deferred)
                {
                    AssemblyResolver = this,
                    InMemory = true,
                }
            );

            // cache for future lookups
            this.Lookup[name.Name] = definition;
            this.Resolver.AddAssembly(definition);
            System.Console.WriteLine($"[AssemblyResolver] Resolved '{name.Name}' from runtime: {location}");

            return definition;
        }
        catch (Exception ex)
        {
            System.Console.WriteLine($"[AssemblyResolver] Failed to resolve '{name.Name}' from runtime: {ex.Message}");
            this.RuntimeResolveFailed.Add(name.Name);
            return null;
        }
    }

    /// <summary>An internal wrapper around <see cref="DefaultAssemblyResolver"/> to allow access to its protected methods.</summary>
    private class DefaultAssemblyResolverWrapper : DefaultAssemblyResolver
    {
        /// <summary>Add an assembly to the resolver.</summary>
        /// <param name="assembly">The assembly to add.</param>
        public void AddAssembly(AssemblyDefinition assembly)
        {
            this.RegisterAssembly(assembly);
        }
    }
}
