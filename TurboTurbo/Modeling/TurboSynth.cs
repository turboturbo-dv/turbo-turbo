using System;

namespace TurboTurbo.Modeling;

public static class TurboSynth
{
    /// <summary>
    /// Bench-style sweep: the same cosine command trajectory as
    /// WhineSynth.RenderSweep, driving engine rpm and load through the DSP.
    /// </summary>
    public static float[] RenderSweep(TurboDspParams p, double sweepSeconds)
    {
        int total = (int)(p.SampleRate * sweepSeconds);
        var output = new float[total];
        var dsp = new TurboDsp(p);
        double dt = 1.0 / p.SampleRate;
        double peak = 0.0;
        for (int i = 0; i < total; i++)
        {
            double t = i * dt;
            double command = 0.5 - 0.5 * Math.Cos(2.0 * Math.PI * t / sweepSeconds);
            double rpmNorm = p.IdleEngineRpmNorm + (1.0 - p.IdleEngineRpmNorm) * command;
            double s = dsp.ProcessSample(rpmNorm, command, dt);
            output[i] = (float)s;
            double a = Math.Abs(s);
            if (a > peak) peak = a;
        }
        Normalize(output, peak);
        return output;
    }

    /// <summary>
    /// Holds a steady operating point (spooling up from rest first).
    /// Not a seamless loop - the real-time paradigm has no loop; audition only.
    /// </summary>
    public static float[] RenderSteady(TurboDspParams p, double seconds, double load)
    {
        int total = (int)(p.SampleRate * seconds);
        var output = new float[total];
        var dsp = new TurboDsp(p);
        double dt = 1.0 / p.SampleRate;
        double rpmNorm = p.IdleEngineRpmNorm + (1.0 - p.IdleEngineRpmNorm) * load;
        double peak = 0.0;
        for (int i = 0; i < total; i++)
        {
            double s = dsp.ProcessSample(rpmNorm, load, dt);
            output[i] = (float)s;
            double a = Math.Abs(s);
            if (a > peak) peak = a;
        }
        Normalize(output, peak);
        return output;
    }

    /// <summary>
    /// Renders a seamless loop of the steady-state operating point at the
    /// given load: spools up, discards the transient, then wraps the buffer
    /// with an equal-power crossfade. Jitter is zeroed for the render so the
    /// tonal component stays as periodic as possible - organic wobble belongs
    /// to the realtime playback layer, where it cannot break the seam.
    /// </summary>
    public static float[] RenderLoop(TurboDspParams p, double steadySeconds, double load)
    {
        var pj = CloneForLoop(p);
        double spoolSeconds = 4.0 * pj.TauSpool + 1.0;
        int total = (int)(pj.SampleRate * (spoolSeconds + steadySeconds));
        var dsp = new TurboDsp(pj);
        double dt = 1.0 / pj.SampleRate;
        double rpmNorm = pj.IdleEngineRpmNorm + (1.0 - pj.IdleEngineRpmNorm) * load;

        int skip = (int)(pj.SampleRate * spoolSeconds);
        int n = total - skip;
        var kept = new float[n];
        for (int i = 0; i < total; i++)
        {
            double s = dsp.ProcessSample(rpmNorm, load, dt);
            if (i >= skip) kept[i - skip] = (float)s;
        }

        // equal-power wrap crossfade (long, so residual tonal phase mismatch
        // smears into a gentle swell instead of a click)
        int xf = Math.Min(n / 4, (int)(pj.SampleRate * 0.2));
        var output = new float[n - xf];
        for (int i = 0; i < output.Length; i++)
        {
            if (i < xf)
            {
                double th = Math.PI * i / (2.0 * xf);
                output[i] = (float)(kept[i] * Math.Sin(th) + kept[n - xf + i] * Math.Cos(th));
            }
            else
            {
                output[i] = kept[i];
            }
        }

        float peak = 0f;
        foreach (float v in output) peak = Math.Max(peak, Math.Abs(v));
        if (peak > 1e-9)
        {
            float gain = (float)(0.9 / peak);
            for (int i = 0; i < output.Length; i++) output[i] *= gain;
        }
        return output;
    }

    private static TurboDspParams CloneForLoop(TurboDspParams p)
    {
        return new TurboDspParams
        {
            SampleRate = p.SampleRate,
            BladeCount = p.BladeCount,
            MaxTurboRpm = p.MaxTurboRpm,
            BpfScale = p.BpfScale,
            IdleEngineRpmNorm = p.IdleEngineRpmNorm,
            TauSpool = p.TauSpool,
            TauDump = p.TauDump,
            WhineGain = p.WhineGain,
            WhineGainExponent = p.WhineGainExponent,
            FlowGain = p.FlowGain,
            DuctResGain = p.DuctResGain,
            DuctQ = p.DuctQ,
            JitterAmount = 0.0,
            JitterHz = p.JitterHz,
            SurgeRateThreshold = p.SurgeRateThreshold,
            CabFilter = p.CabFilter,
            CabFilterCutoffHz = p.CabFilterCutoffHz,
            Seed = p.Seed,
        };
    }

    private static void Normalize(float[] samples, double peak)
    {
        if (peak < 1e-9) return;
        float gain = (float)(0.9 / peak);
        for (int i = 0; i < samples.Length; i++) samples[i] *= gain;
    }
}

public sealed class TurboDspParams
{
    public int SampleRate = 44100;
    public double BladeCount = 12.0;
    /// <summary>
    /// Supposedly a realistic peak shaft speed for large-frame turbos.
    /// At 36000 RPM with 12 blades the physical BPF tops out at 7.2 kHz,
    /// which sounds nice without needing any pitch scaling.
    /// </summary>
    public double MaxTurboRpm = 36000.0;
    /// <summary>1:1 physical blade-passing frequency scaling.</summary>
    public double BpfScale = 1.0;
    public double IdleEngineRpmNorm = 0.332; // TODO: measured idle? verify
    public double TauSpool = 1.8;
    public double TauDump = 1.2;
    public double WhineGain = 0.4;
    /// <summary>Exponent of the whine gain curve (gain = (w/max)^exponent).
    /// Models dipole aeroacoustic scaling (U^4-U^6) so the whistle stays
    /// submerged at low shaft speed and emerges sharply at high power.</summary>
    public double WhineGainExponent = 3.5;
    public double FlowGain = 0.6;
    /// <summary>Gain of the resonant intake-duct band-pass (Branch B).</summary>
    public double DuctResGain = 0.5;
    /// <summary>Resonance (Q) of the intake-duct band-pass.</summary>
    public double DuctQ = 2.0;
    public double JitterHz = 10.0;
    public double JitterAmount = 0.008;
    /// <summary>Load rejection rate (per second) that triggers surge flutter.</summary>
    public double SurgeRateThreshold = -0.35;
    /// <summary>Listener-position filter: 2-pole (12 dB/oct) low-pass modeling
    /// the muffled engine-bay/cab sound when the listener is inside the cab.</summary>
    public bool CabFilter = false;
    public double CabFilterCutoffHz = 2000.0;
    public int Seed = 1234;
}