using System;
using System.IO;

namespace TurboTurbo;

public sealed class WhineParams
{
    public double BaseHz = 1150.0;
    public double Harmonic2 = 0.5;
    public double Harmonic3 = 0.25;
    public double NoiseGain = 0.18;
    public double Seconds = 2.5;
    public int SampleRate = 44100;
}

/// <summary>
/// Procedural turbo whine synthesis (pure .NET, no engine dependencies).
/// Partials are quantized to whole cycles over the loop duration so the loop
/// is seamless; inharmonic offsets between partials produce natural beating.
/// </summary>
public static class WhineSynth
{
    public static float[] Synthesize(WhineParams p)
    {
        int n = (int)Math.Round(p.SampleRate * p.Seconds);
        int c1 = Math.Max(1, (int)Math.Round(p.BaseHz * p.Seconds));
        int c2 = 2 * c1 + 1;
        int c3 = 3 * c1 + 2;

        var samples = new float[n];
        for (int i = 0; i < n; i++)
        {
            double ph = 2.0 * Math.PI * i / n;
            double beat = 0.8 + 0.2 * Math.Sin(ph * 3.0);
            samples[i] = (float)(Math.Sin(ph * c1)
                                 + p.Harmonic2 * beat * Math.Sin(ph * c2)
                                 + p.Harmonic3 * Math.Sin(ph * c3));
        }

        var rng = new Random(1234);
        double lowState = 0.0, bandState = 0.0;
        double kLow = 1.0 - Math.Exp(-2.0 * Math.PI * (p.BaseHz * 1.4) / p.SampleRate);
        double kBand = 1.0 - Math.Exp(-2.0 * Math.PI * (p.BaseHz * 0.35) / p.SampleRate);
        int fade = Math.Max(1, (int)(p.SampleRate * 0.005));
        for (int i = 0; i < n; i++)
        {
            double white = rng.NextDouble() * 2.0 - 1.0;
            lowState += kLow * (white - lowState);
            bandState += kBand * (lowState - bandState);
            float noise = (float)((lowState - bandState) * p.NoiseGain);
            float edge = Math.Min(i, n - 1 - i) / (float)fade;
            samples[i] += noise * Clamp(edge, 0f, 1f);
        }

        float peak = 0f;
        foreach (float v in samples) peak = Math.Max(peak, Math.Abs(v));
        float gain = 0.9f / Math.Max(0.0001f, peak);
        for (int i = 0; i < n; i++) samples[i] *= gain;
        return samples;
    }

    /// <summary>
    /// Renders a spool sweep the way the in-game bench plays it: boost target
    /// follows a full cosine cycle over sweepSeconds, eased by tau, pitch
    /// mapped pitchMin..pitchMax over boost, volume = maxVolume * boost^0.7.
    /// </summary>
    public static float[] RenderSweep(float[] loop, int sampleRate, double sweepSeconds,
        double tau, double pitchMin, double pitchMax, double maxVolume)
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

            output[i] = (float)(maxVolume * Math.Pow(current, 0.7) * s);
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
