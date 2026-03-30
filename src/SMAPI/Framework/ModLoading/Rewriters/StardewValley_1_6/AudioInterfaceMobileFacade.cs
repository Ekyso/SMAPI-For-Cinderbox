using System;
using System.Reflection;
using Microsoft.Xna.Framework.Audio;
using StardewModdingAPI.Framework.ModLoading.Framework;
using StardewValley;

namespace StardewModdingAPI.Framework.ModLoading.Rewriters.StardewValley_1_6;

/// <summary>Mobile facade for audio interface members missing on mobile.
/// Desktop exposes Pitch/Volume/Exists etc. through ICue/ISoundBank interfaces.
/// Mobile's CueWrapper/SoundBankWrapper wrap the same XNA types that have these
/// members, but don't expose them on the interface. This facade accesses the
/// underlying XNA objects via cached reflection.</summary>
/// <remarks>This is public to support SMAPI rewriting and should never be referenced directly by mods. See remarks on <see cref="ReplaceReferencesRewriter"/> for more info.</remarks>
public class AudioInterfaceMobileFacade : IRewriteFacade
{
    /*********
    ** Fields
    *********/
    private static readonly FieldInfo? CueField = typeof(CueWrapper).GetField(
        "cue",
        BindingFlags.NonPublic | BindingFlags.Instance
    );

    private static readonly FieldInfo? SoundBankField = typeof(SoundBankWrapper).GetField(
        "soundBank",
        BindingFlags.NonPublic | BindingFlags.Instance
    );

    /*********
    ** ICue - Pitch/Volume/IsPitchBeingControlledByRPC
    ** Desktop CueWrapper exposes these from the underlying XNA Cue.
    ** Mobile CueWrapper wraps the same Cue but doesn't expose them on ICue.
    *********/
    public static float GetCuePitch(ICue cue)
    {
        if (cue is CueWrapper wrapper && CueField?.GetValue(wrapper) is Cue xnaCue)
            return xnaCue.Pitch;
        return 0f;
    }

    public static void SetCuePitch(ICue cue, float value)
    {
        if (cue is CueWrapper wrapper && CueField?.GetValue(wrapper) is Cue xnaCue)
            xnaCue.Pitch = value;
    }

    public static float GetCueVolume(ICue cue)
    {
        if (cue is CueWrapper wrapper && CueField?.GetValue(wrapper) is Cue xnaCue)
            return xnaCue.Volume;
        return 1f;
    }

    public static void SetCueVolume(ICue cue, float value)
    {
        if (cue is CueWrapper wrapper && CueField?.GetValue(wrapper) is Cue xnaCue)
            xnaCue.Volume = value;
    }

    public static bool GetCueIsPitchBeingControlledByRPC(ICue cue)
    {
        if (cue is CueWrapper wrapper && CueField?.GetValue(wrapper) is Cue xnaCue)
            return xnaCue.IsPitchBeingControlledByRPC;
        return false;
    }

    /*********
    ** ISoundBank - Exists/AddCue/GetCueDefinition
    ** Desktop SoundBankWrapper delegates to the underlying XNA SoundBank.
    ** Mobile SoundBankWrapper wraps the same SoundBank but doesn't expose these.
    *********/
    public static bool SoundBankExists(ISoundBank soundBank, string name)
    {
        if (
            soundBank is SoundBankWrapper wrapper
            && SoundBankField?.GetValue(wrapper) is SoundBank xnaBank
        )
            return xnaBank.Exists(name);
        return true;
    }

    public static void SoundBankAddCue(ISoundBank soundBank, CueDefinition definition)
    {
        if (
            soundBank is SoundBankWrapper wrapper
            && SoundBankField?.GetValue(wrapper) is SoundBank xnaBank
        )
            xnaBank.AddCue(definition);
    }

    public static CueDefinition? SoundBankGetCueDefinition(ISoundBank soundBank, string name)
    {
        if (
            soundBank is SoundBankWrapper wrapper
            && SoundBankField?.GetValue(wrapper) is SoundBank xnaBank
        )
            return xnaBank.GetCueDefinition(name);
        return null;
    }

    /*********
    ** IAudioEngine - GetCategoryIndex
    *********/
    public static int AudioEngineGetCategoryIndex(
        StardewValley.Audio.IAudioEngine engine,
        string name
    )
    {
        return engine.Engine.GetCategoryIndex(name);
    }

    /*********
    ** Private
    *********/
    private AudioInterfaceMobileFacade()
    {
        RewriteHelper.ThrowFakeConstructorCalled();
    }
}
