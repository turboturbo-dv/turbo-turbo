using System;

namespace WhineBench;

/// <summary>
/// Simulates how the in-game GameStyle audio path plays a loop: boost target
/// follows a full cosine cycle over sweepSeconds, eased by tau, pitch mapped
/// pitchMin..pitchMax over boost, volume = maxVolume * boost^volumeExponent,
/// optionally weighted by the normalized boost delta (w x load).
/// </summary>
internal static class SweepPlayer
{
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
}
