#if SMAPI_FOR_ANDROID
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HarmonyLib;
using Microsoft.Xna.Framework.Audio;
using NVorbis;
using StardewValley;
using StardewValley.Audio;

namespace StardewModdingAPI.Mobile.Patches;

/// <summary>
/// Parallelizes OGG decoding during ApplyAllCueModifications. Optionally encodes to
/// IMA4 ADPCM for ~4x memory reduction, with disk caching to skip re-decoding.
/// </summary>
internal static class ParallelAudioLoadPatch
{
    private static int _invocationCount;

    private const int Ima4BlockAlignmentMono = 1024;
    private const int Ima4BlockAlignmentStereo = 2048;

    /// <summary>Cached SoundEffect objects keyed by file path to avoid re-decoding unchanged OGGs.</summary>
    private static readonly Dictionary<string, SoundEffect> _sfxCache = new(StringComparer.Ordinal);

    /// <summary>IMA4 buffer size and PCM equivalent for cached SoundEffects, keyed by file path.</summary>
    private static readonly Dictionary<string, (long Ima4Bytes, long PcmEquivBytes)> _sfxIma4Meta = new(StringComparer.Ordinal);

    private static readonly byte[] Ima4CacheMagic = { 0x49, 0x4D, 0x41, 0x34 }; // "IMA4"
    private const byte Ima4CacheVersion = 1;
    private const int Ima4CacheHeaderSize = 28;

    /// <summary>Apply the Harmony patch.</summary>
    /// <param name="harmony">The Harmony instance.</param>
    public static void Apply(Harmony harmony)
    {
        harmony.Patch(
            original: AccessTools.Method(
                typeof(AudioCueModificationManager),
                nameof(AudioCueModificationManager.ApplyAllCueModifications)
            ),
            prefix: new HarmonyMethod(
                typeof(ParallelAudioLoadPatch),
                nameof(ApplyAllCueModifications_Prefix)
            )
        );
    }

    /// <summary>Replaces sequential cue modification with parallel OGG decoding and sequential SoundEffect creation.</summary>
    private static bool ApplyAllCueModifications_Prefix(AudioCueModificationManager __instance)
    {
        int invocation = Interlocked.Increment(ref _invocationCount);
        var totalSw = Stopwatch.StartNew();
        string tag = $"AudioLoad #{invocation}";

        try
        {
            Game1.log.Info($"{tag}: ApplyAllCueModifications start");

            var keys = __instance.cueModificationData.Keys.ToList();

            // Phase 1: Collect all unique paths and categorize
            var oggPathsNonStreamed = new HashSet<string>(StringComparer.Ordinal);

            foreach (var key in keys)
            {
                if (!__instance.cueModificationData.TryGetValue(key, out var data))
                    continue;

                if (data.FilePaths == null)
                    continue;

                foreach (var filePath in data.FilePaths)
                {
                    string path = __instance.GetFilePath(filePath);
                    bool isOgg = Path.GetExtension(path).Equals(".ogg", StringComparison.OrdinalIgnoreCase);

                    if (isOgg && !data.StreamedVorbis)
                        oggPathsNonStreamed.Add(path);
                }
            }

            bool useIma4 = false;
            try { useIma4 = OpenALSoundController.Instance.SupportsIma4; }
            catch { }

            var oggPathsToProcess = new HashSet<string>(StringComparer.Ordinal);
            foreach (var path in oggPathsNonStreamed)
            {
                if (!_sfxCache.TryGetValue(path, out var cached) || cached.IsDisposed)
                    oggPathsToProcess.Add(path);
            }

            var decodedAudio = new ConcurrentDictionary<string, DecodedAudio?>(StringComparer.Ordinal);

            if (oggPathsToProcess.Count > 0)
            {
                int parallelism = Math.Clamp(Environment.ProcessorCount / 2, 2, 4);

                Parallel.ForEach(
                    oggPathsToProcess,
                    new ParallelOptions
                    {
                        MaxDegreeOfParallelism = parallelism,
                    },
                    path =>
                    {
                        decodedAudio[path] = DecodeOggAudio(path, useIma4);
                    }
                );
            }

            foreach (var key in keys)
            {
                try
                {
                    if (!__instance.cueModificationData.TryGetValue(key, out var data))
                        continue;

                    bool isMod = false;
                    int catIdx = Game1.audioEngine.GetCategoryIndex("Default");

                    CueDefinition cueDef;
                    if (Game1.soundBank.Exists(data.Id))
                    {
                        cueDef = Game1.soundBank.GetCueDefinition(data.Id);
                        isMod = true;
                    }
                    else
                    {
                        cueDef = new CueDefinition();
                        cueDef.name = data.Id;
                    }

                    if (data.Category != null)
                        catIdx = Game1.audioEngine.GetCategoryIndex(data.Category);

                    if (data.FilePaths != null)
                    {
                        var effects = new SoundEffect[data.FilePaths.Count];
                        int invalidSounds = 0;

                        for (int i = 0; i < data.FilePaths.Count; i++)
                        {
                            string path = __instance.GetFilePath(data.FilePaths[i]);
                            bool vorbis = Path.GetExtension(path)
                                .Equals(".ogg", StringComparison.OrdinalIgnoreCase);

                            try
                            {
                                SoundEffect sfx;
                                if (vorbis && !data.StreamedVorbis && _sfxCache.TryGetValue(path, out var cachedSfx) && !cachedSfx.IsDisposed)
                                {
                                    sfx = cachedSfx;
                                }
                                else if (vorbis && data.StreamedVorbis)
                                {
                                    sfx = new OggStreamSoundEffect(path);
                                }
                                else if (
                                    vorbis
                                    && decodedAudio.TryGetValue(path, out var audio)
                                    && audio != null
                                )
                                {
                                    if (audio.IsIma4)
                                        sfx = new SoundEffect(audio.Buffer, audio.SampleRate, audio.Channels, audio.BlockAlignment, audio.TotalPcmSamples);
                                    else
                                        sfx = new SoundEffect(audio.Buffer, audio.SampleRate, audio.Channels);
                                    _sfxCache[path] = sfx;
                                    if (audio.IsIma4)
                                        _sfxIma4Meta[path] = (audio.Buffer.Length, (long)audio.TotalPcmSamples * (int)audio.Channels * 2);
                                }
                                else
                                {
                                    using var stream = new FileStream(path, FileMode.Open);
                                    sfx = SoundEffect.FromStream(stream, vorbis);
                                    if (vorbis)
                                        _sfxCache[path] = sfx;
                                }

                                effects[i - invalidSounds] = sfx;
                            }
                            catch (Exception ex)
                            {
                                Game1.log.Error("Error loading sound: " + path, ex);
                                invalidSounds++;
                            }
                        }

                        if (invalidSounds > 0)
                            Array.Resize(ref effects, effects.Length - invalidSounds);

                        cueDef.SetSound(effects, catIdx, data.Looped, data.UseReverb);
                        if (isMod)
                            cueDef.OnModified?.Invoke();
                    }

                    Game1.soundBank.AddCue(cueDef);
                }
                catch (NoAudioHardwareException)
                {
                    Game1.log.Warn(
                        $"Can't apply modifications for audio cue '{key}' because there's no audio hardware available."
                    );
                }
            }

            // dispose stale SoundEffects so OpenAL buffers don't accumulate across invocations
            foreach (var effect in SoundEffect.EffectsToRemove)
            {
                if (effect.ShouldBeRemoved())
                    effect.Dispose();
            }
            SoundEffect.EffectsToRemove.Clear();

            var keysToRemove = new List<string>();
            foreach (var kvp in _sfxCache)
            {
                if (kvp.Value.IsDisposed)
                    keysToRemove.Add(kvp.Key);
            }
            foreach (var k in keysToRemove)
            {
                _sfxCache.Remove(k);
                _sfxIma4Meta.Remove(k);
            }

            decodedAudio.Clear();
            GC.Collect(2, GCCollectionMode.Forced);
            GC.WaitForPendingFinalizers();

            totalSw.Stop();
            Game1.log.Info($"{tag}: ApplyAllCueModifications completed in {totalSw.ElapsedMilliseconds}ms");
        }
        catch (Exception ex)
        {
            Game1.log.Error($"{tag}: Parallel audio loading failed after {totalSw.ElapsedMilliseconds}ms, falling back to sequential", ex);
            return true; // fall through to original sequential method
        }

        return false; // skip original
    }

    /// <summary>Decoded/encoded audio data ready for SoundEffect creation.</summary>
    private record DecodedAudio(
        byte[] Buffer, int SampleRate, AudioChannels Channels,
        bool IsIma4, int BlockAlignment, int TotalPcmSamples, bool WasCacheHit);

    /// <summary>Decode an OGG file to PCM or IMA4 audio data, using disk cache when available.</summary>
    private static DecodedAudio? DecodeOggAudio(string absolutePath, bool useIma4)
    {
        try
        {
            if (useIma4)
            {
                var cached = TryLoadIma4Cache(absolutePath);
                if (cached != null)
                    return cached;
            }

            using var fileStream = File.OpenRead(absolutePath);
            using var vorbisReader = new VorbisReader(fileStream);

            int totalInterleavedSamples = (int)(vorbisReader.TotalSamples * vorbisReader.Channels);
            float[] floatBuffer = new float[totalInterleavedSamples];
            int samplesRead = vorbisReader.ReadSamples(floatBuffer, 0, floatBuffer.Length);

            byte[] pcmBuffer = new byte[samplesRead * 2];
            for (int i = 0; i < samplesRead; i++)
            {
                int val = (int)(32767f * floatBuffer[i]);
                if (val > 32767)
                    val = 32767;
                else if (val < -32768)
                    val = -32768;
                short s = (short)val;
                pcmBuffer[i * 2] = (byte)(s & 0xFF);
                pcmBuffer[i * 2 + 1] = (byte)((s >> 8) & 0xFF);
            }

            int channelCount = vorbisReader.Channels;
            var channels = channelCount == 2 ? AudioChannels.Stereo : AudioChannels.Mono;
            int sampleRate = vorbisReader.SampleRate;
            int totalPcmSamplesPerChannel = samplesRead / channelCount;

            if (!useIma4)
                return new DecodedAudio(pcmBuffer, sampleRate, channels, false, 0, totalPcmSamplesPerChannel, false);

            int blockAlignment = channelCount == 2 ? Ima4BlockAlignmentStereo : Ima4BlockAlignmentMono;
            byte[] ima4Buffer = AudioLoader.ConvertPcmToIma4(pcmBuffer, 0, pcmBuffer.Length, channelCount, blockAlignment);

            TryWriteIma4Cache(absolutePath, ima4Buffer, channelCount, blockAlignment, sampleRate, totalPcmSamplesPerChannel);

            return new DecodedAudio(ima4Buffer, sampleRate, channels, true, blockAlignment, totalPcmSamplesPerChannel, false);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Try to load a valid IMA4 cache file for the given OGG path.
    /// Returns null if cache doesn't exist, is stale, or is corrupted.
    /// </summary>
    private static DecodedAudio? TryLoadIma4Cache(string oggPath)
    {
        try
        {
            string cachePath = Path.ChangeExtension(oggPath, ".ima4");
            if (!File.Exists(cachePath))
                return null;

            long oggLastModifiedTicks = File.GetLastWriteTimeUtc(oggPath).Ticks;
            byte[] cacheData = File.ReadAllBytes(cachePath);

            if (cacheData.Length < Ima4CacheHeaderSize)
                return null;

            if (cacheData[0] != Ima4CacheMagic[0] || cacheData[1] != Ima4CacheMagic[1] ||
                cacheData[2] != Ima4CacheMagic[2] || cacheData[3] != Ima4CacheMagic[3])
                return null;

            if (cacheData[4] != Ima4CacheVersion)
                return null;

            int channels = cacheData[5];
            if (channels != 1 && channels != 2)
                return null;

            int blockAlignment = BitConverter.ToUInt16(cacheData, 6);
            int sampleRate = BitConverter.ToInt32(cacheData, 8);
            int totalPcmSamples = BitConverter.ToInt32(cacheData, 12);
            int ima4DataLength = BitConverter.ToInt32(cacheData, 16);
            long storedOggTicks = BitConverter.ToInt64(cacheData, 20);

            if (storedOggTicks != oggLastModifiedTicks)
                return null;

            if (cacheData.Length != Ima4CacheHeaderSize + ima4DataLength)
                return null;

            byte[] ima4Buffer = new byte[ima4DataLength];
            Buffer.BlockCopy(cacheData, Ima4CacheHeaderSize, ima4Buffer, 0, ima4DataLength);

            var audioChannels = channels == 2 ? AudioChannels.Stereo : AudioChannels.Mono;
            return new DecodedAudio(ima4Buffer, sampleRate, audioChannels, true, blockAlignment, totalPcmSamples, true);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Write IMA4 encoded data to a disk cache file. Atomic write via temp file + rename.
    /// Silently fails if the directory is read-only or any other error occurs.
    /// </summary>
    private static void TryWriteIma4Cache(string oggPath, byte[] ima4Buffer, int channels, int blockAlignment, int sampleRate, int totalPcmSamples)
    {
        try
        {
            long oggLastModifiedTicks = File.GetLastWriteTimeUtc(oggPath).Ticks;
            string cachePath = Path.ChangeExtension(oggPath, ".ima4");
            string tmpPath = cachePath + ".tmp";

            byte[] header = new byte[Ima4CacheHeaderSize];
            header[0] = Ima4CacheMagic[0];
            header[1] = Ima4CacheMagic[1];
            header[2] = Ima4CacheMagic[2];
            header[3] = Ima4CacheMagic[3];
            header[4] = Ima4CacheVersion;
            header[5] = (byte)channels;
            BitConverter.TryWriteBytes(header.AsSpan(6), (ushort)blockAlignment);
            BitConverter.TryWriteBytes(header.AsSpan(8), sampleRate);
            BitConverter.TryWriteBytes(header.AsSpan(12), totalPcmSamples);
            BitConverter.TryWriteBytes(header.AsSpan(16), ima4Buffer.Length);
            BitConverter.TryWriteBytes(header.AsSpan(20), oggLastModifiedTicks);

            using (var fs = new FileStream(tmpPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                fs.Write(header, 0, header.Length);
                fs.Write(ima4Buffer, 0, ima4Buffer.Length);
            }

            File.Move(tmpPath, cachePath, overwrite: true);
        }
        catch (Exception ex)
        {
            Game1.log.Warn($"IMA4 cache write failed for {oggPath}: {ex.Message}");
        }
    }
}
#endif
