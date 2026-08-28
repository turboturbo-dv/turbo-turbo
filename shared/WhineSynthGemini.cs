using System;

namespace TurboTurbo;

public sealed class GeminiParams
{
    public int SampleRate = 44100;
    public double BladeCount = 12.0;
    public double MaxTurboRpm = 80000.0;
    public double IdleEngineRpmNorm = 0.332;   // DE6 measured idle
    public double TauSpool = 1.8;
    public double TauDump = 1.2;
    public double WhineGain = 0.4;
    public double FlowGain = 0.6;
    public double JitterHz = 10.0;
    public double JitterAmount = 0.008;
    public int Seed = 1234;
}

/// <summary>
/// Per-sample DSP engine following Gemini's hybrid procedural design:
/// Branch A: tonal whine - sine at blade-passing frequency with boost-driven
///           wavefolding (tanh saturation) and low-frequency pitch jitter.
/// Branch B: broadband flow - white noise through a one-pole LPF whose cutoff
///           tracks mass air flow.
/// Branch C: surge flutter - 16 Hz AM on the flow layer with exponential decay,
///           triggered by rapid load rejection at high boost.
/// State layer: turbo rpm first-order lag, target = N^2 * (a + b*load).
/// </summary>
public sealed class GeminiTurboDsp
{
    private readonly GeminiParams _p;
    private readonly Random _rng;

    private double _turboRpm;
    private double _boost = 1.0;
    private double _phase;
    private double _jitter;
    private double _lpfState;
    private double _surgeEnvelope;
    private double _surgePhase;
    private double _prevLoad;
    private bool _hasPrevLoad;

    public double TurboRpm => _turboRpm;
    public double Boost => _boost;

    public GeminiTurboDsp(GeminiParams p)
    {
        _p = p;
        _rng = new Random(p.Seed);
    }

    public double ProcessSample(double engineRpmNorm, double load, double dt)
    {
        GeminiParams p = _p;
        double sr = p.SampleRate;

        double target = engineRpmNorm * engineRpmNorm * (20000.0 + 60000.0 * Math.Max(0.0, load));
        double tau = target > _turboRpm ? p.TauSpool : p.TauDump;
        _turboRpm += (target - _turboRpm) * Math.Min(1.0, dt / tau);
        _boost = 1.0 + 2.5 * (_turboRpm / p.MaxTurboRpm) * Math.Max(0.0, load);

        if (_hasPrevLoad && load - _prevLoad < -0.35 && _boost > 2.0 && _surgeEnvelope <= 0.001)
        {
            _surgeEnvelope = 1.0;
            _surgePhase = 0.0;
        }
        _prevLoad = load;
        _hasPrevLoad = true;

        double bpf = (_turboRpm / 60.0) * p.BladeCount;
        if (bpf > 0.45 * sr) bpf = 0.45 * sr;

        double white1 = _rng.NextDouble() * 2.0 - 1.0;
        double kJitter = 1.0 - Math.Exp(-2.0 * Math.PI * p.JitterHz / sr);
        _jitter += kJitter * (white1 - _jitter);
        double freq = bpf * (1.0 + p.JitterAmount * _jitter);

        _phase += 2.0 * Math.PI * freq / sr;
        if (_phase >= 2.0 * Math.PI) _phase -= 2.0 * Math.PI;
        double raw = Math.Sin(_phase);
        double tonal = Math.Tanh(raw * _boost) / Math.Tanh(_boost);
        double w = _turboRpm / p.MaxTurboRpm;
        double whineGain = w * w * p.WhineGain;

        double cutoff = 400.0 + 6000.0 * w;
        double rc = 1.0 / (2.0 * Math.PI * cutoff);
        double alpha = dt / (rc + dt);
        double white2 = _rng.NextDouble() * 2.0 - 1.0;
        _lpfState += alpha * (white2 - _lpfState);
        double flowGain = w * p.FlowGain;

        double surgeMod = 1.0;
        if (_surgeEnvelope > 0.001)
        {
            _surgePhase += 2.0 * Math.PI * 16.0 / sr;
            surgeMod = 1.0 + 0.6 * Math.Sin(_surgePhase) * _surgeEnvelope;
            _surgeEnvelope *= Math.Exp(-4.0 * dt);
        }

        return tonal * whineGain + _lpfState * flowGain * surgeMod;
    }

    public void TriggerSurge()
    {
        _surgeEnvelope = 1.0;
        _surgePhase = 0.0;
    }
}

public static class WhineSynthGemini
{
    /// <summary>
    /// Bench-style sweep: the same cosine command trajectory as
    /// WhineSynth.RenderSweep, driving engine rpm and load through the DSP.
    /// </summary>
    public static float[] RenderSweep(GeminiParams p, double sweepSeconds)
    {
        int total = (int)(p.SampleRate * sweepSeconds);
        var output = new float[total];
        var dsp = new GeminiTurboDsp(p);
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
    public static float[] RenderSteady(GeminiParams p, double seconds, double load)
    {
        int total = (int)(p.SampleRate * seconds);
        var output = new float[total];
        var dsp = new GeminiTurboDsp(p);
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

    private static void Normalize(float[] samples, double peak)
    {
        if (peak < 1e-9) return;
        float gain = (float)(0.9 / peak);
        for (int i = 0; i < samples.Length; i++) samples[i] *= gain;
    }
}
