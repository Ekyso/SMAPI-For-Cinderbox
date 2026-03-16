using System;
using System.IO;
using System.Reflection;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Pdb;
using Mono.Collections.Generic;
#if SMAPI_FOR_ANDROID
using Android.Util;
#endif

namespace StardewModdingAPI.Framework.ModLoading.Symbols;

/// <summary>Reads symbol data for an assembly.</summary>
internal class SymbolReader : ISymbolReader
{
    /*********
    ** Fields
    *********/
    /// <summary>The module for which to read symbols.</summary>
    private readonly ModuleDefinition Module;

    /// <summary>The symbol file stream.</summary>
    private readonly Stream Stream;

    /// <summary>The underlying symbol reader.</summary>
    private ISymbolReader Reader;


    /*********
    ** Public methods
    *********/
    /// <summary>Construct an instance.</summary>
    /// <param name="module">The module for which to read symbols.</param>
    /// <param name="stream">The symbol file stream.</param>
    public SymbolReader(ModuleDefinition module, Stream stream)
    {
        this.Module = module;
        this.Stream = stream;
#if SMAPI_FOR_ANDROID
        // Pre-load Cecil symbol assemblies so DefaultSymbolReaderProvider can resolve them
        // (Assembly.Load on Mono/Android can create assemblies invisible to the native JIT).
        SymbolReader.EnsureCecilSymbolAssembliesLoaded();
        this.Reader = new DefaultSymbolReaderProvider(throwIfNoSymbol: true).GetSymbolReader(module, stream);
#else
        this.Reader = new NativePdbReaderProvider().GetSymbolReader(module, stream);
#endif
    }

    /// <summary>Get the symbol writer provider for the assembly.</summary>
    public ISymbolWriterProvider GetWriterProvider()
    {
        return new PortablePdbWriterProvider();
    }

    /// <summary>Process a debug header in the symbol file.</summary>
    /// <param name="header">The debug header.</param>
    public bool ProcessDebugHeader(ImageDebugHeader header)
    {
        try
        {
            return this.Reader.ProcessDebugHeader(header);
        }
        catch
        {
            this.Reader.Dispose();
            this.Stream.Position = 0;
#if SMAPI_FOR_ANDROID
            throw;
#else
            this.Reader = new PortablePdbReaderProvider().GetSymbolReader(this.Module, this.Stream);
            return this.Reader.ProcessDebugHeader(header);
#endif
        }
    }

    /// <summary>Read the method debug information for a method in the assembly.</summary>
    /// <param name="method">The method definition.</param>
    public MethodDebugInformation Read(MethodDefinition method)
    {
        return this.Reader.Read(method);
    }

    /// <summary>Read the method debug information for a method in the assembly.</summary>
    /// <param name="provider">The debug info provider.</param>
    public Collection<CustomDebugInformation> Read(ICustomDebugInformationProvider provider)
    {
        return this.Reader.Read(provider);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        this.Reader.Dispose();
    }

#if SMAPI_FOR_ANDROID
    /*********
    ** Private methods
    *********/
    /// <summary>Whether Cecil symbol assemblies have been pre-loaded.</summary>
    private static bool CecilSymbolAssembliesLoaded;

    /// <summary>Pre-load Mono.Cecil.Pdb.dll and Mono.Cecil.Mdb.dll before DefaultSymbolReaderProvider resolves them dynamically.</summary>
    private static void EnsureCecilSymbolAssembliesLoaded()
    {
        if (CecilSymbolAssembliesLoaded)
            return;
        CecilSymbolAssembliesLoaded = true;

        const string tag = "SymbolReader";

        string? cecilLocation = typeof(ModuleDefinition).Assembly.Location;
        Log.Info(tag, $"Cecil assembly location: '{cecilLocation ?? "(null)"}'");
        if (string.IsNullOrEmpty(cecilLocation))
            return;

        string? dir = Path.GetDirectoryName(cecilLocation);
        if (string.IsNullOrEmpty(dir))
            return;

        foreach (var name in new[] { "Mono.Cecil.Pdb.dll", "Mono.Cecil.Mdb.dll" })
        {
            string path = Path.Combine(dir, name);
            if (File.Exists(path))
            {
                try
                {
                    var asm = Assembly.LoadFrom(path);
                    Log.Info(tag, $"Pre-loaded {name} from '{path}' -> {asm.FullName}");
                }
                catch (Exception ex)
                {
                    Log.Warn(tag, $"Failed to pre-load {name}: {ex.Message}");
                }
            }
            else
            {
                Log.Warn(tag, $"{name} not found at '{path}'");
            }
        }
    }
#endif
}
