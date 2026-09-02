using System;

namespace TurboTurbo.Modeling;

public static class TurboSynth
{
    /// <summary>
    /// Bench-style sweep: the same cosine command trajectory as
    /// WhineSynth.RenderSweep, driving engine rpm and load through the DSP.
    /// </summary>
    public static float[] RenderSweep(TurboDsp.Settings p, double sweepSeconds)
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
    /// Not a seamless loop. The real-time paradigm has no loop; audition only.
    /// </summary>
    public static float[] RenderSteady(TurboDsp.Settings p, double seconds, double load)
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
    /// tonal component stays as periodic as possible. Organic wobble belongs
    /// to the realtime playback layer, where it cannot break the seam.
    /// </summary>
    public static float[] RenderLoop(TurboDsp.Settings p, double steadySeconds, double load)
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

    private static TurboDsp.Settings CloneForLoop(TurboDsp.Settings p)
    {
        return new TurboDsp.Settings
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