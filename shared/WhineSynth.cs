using System;
using System.IO;

namespace TurboTurbo;

/// <summary>
/// Loop-playback simulator for the GameStyle audio path: renders a loop the
/// way the mod plays it (pitch-mapped resampling + volume law), plus WAV export.
/// </summary>
public static class WhineSynth
{
    /// <summary>
    /// Renders a spool sweep the way the in-game bench plays it: boost target
    /// follows a full cosine cycle over sweepSeconds, eased by tau, pitch
    /// mapped pitchMin..pitchMax over boost, volume = maxVolume * boost^0.7.
    /// Optional dipole-style shaping: steeper volume exponent and pressure
    /// coupling (volume weighted by normalized boost delta w x load).
    /// </summary>
    public static float[] RenderSweep(float[] loop, int sampleRate, double sweepSeconds,
        double tau, double pitchMin, double pitchMax, double maxVolume,
        double volumeExponent = 0.7, bool pressureCoupling = false)
    {
        int n = loop.Length;
        int total = (int)(sampleRate * sweepSeconds);
        var output = new float[total];
        double current = 0.0;
        double phase = 0.0;
        double dt = 1.0 / sampleRate;
        for (int i = 0; i < total; i++)
        {
            double t = i * dt;
            double target = 0.5 - 0.5 * Math.Cos(2.0 * Math.PI * t / sweepSeconds);
            current += (target - current) * (1.0 - Math.Exp(-dt / tau));

            double pitch = pitchMin + (pitchMax - pitchMin) * current;
            phase += pitch;
            int i0 = (int)phase % n;
            int i1 = (i0 + 1) % n;
            double frac = phase - Math.Floor(phase);
            double s = loop[i0] * (1.0 - frac) + loop[i1] * frac;

            double volume = maxVolume * Math.Pow(current, volumeExponent);
            if (pressureCoupling)
            {
                // normalized boost delta proxy: w x load (command acts as governor load)
                double delta = Math.Min(1.0, current * target);
                volume *= 0.10 + 0.90 * delta;
            }
            output[i] = (float)(volume * s);
        }
        return output;
    }

    public static void ExportWav(float[] samples, int sampleRate, string path)
    {
        string dir = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        using (var stream = new FileStream(path, FileMode.Create))
        using (var writer = new BinaryWriter(stream))
        {
            int dataBytes = samples.Length * 2;
            writer.Write(0x46464952);           // RIFF
            writer.Write(36 + dataBytes);
            writer.Write(0x45564157);           // WAVE
            writer.Write(0x20746D66);           // fmt
            writer.Write(16);
            writer.Write((short)1);             // PCM
            writer.Write((short)1);             // mono
            writer.Write(sampleRate);
            writer.Write(sampleRate * 2);
            writer.Write((short)2);             // block align
            writer.Write((short)16);            // bits
            writer.Write(0x61746164);           // data
            writer.Write(dataBytes);
            foreach (float v in samples)
            {
                writer.Write((short)(Clamp(v, -1f, 1f) * 32767f));
            }
        }
    }

    private static float Clamp(float v, float min, float max) => v < min ? min : (v > max ? max : v);
}
