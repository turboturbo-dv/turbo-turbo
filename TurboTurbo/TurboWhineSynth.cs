using UnityEngine;

namespace TurboTurbo;

/// <summary>
/// Unity-side wrapper over the shared whine synthesis (see shared/WhineSynth.cs).
/// </summary>
internal static class TurboWhineSynth
{
    internal static AudioClip BuildClip(WhineParams p)
    {
        float[] samples = WhineSynth.Synthesize(p);
        var clip = AudioClip.Create("TurboTurboWhine", samples.Length, 1, p.SampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }
}
