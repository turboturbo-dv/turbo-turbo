using System;

namespace TurboTurbo;

public sealed class GeminiParams
{
    public int SampleRate = 44100;
    public double BladeCount = 12.0;
    public double MaxTurboRpm = 80000.0;
    /// <summary>
    /// Scales the physical blade-passing frequency into an alias-safe audible
    /// band. At full speed (80k rpm, 12 blades) the literal BPF is 16 kHz;
    /// wavefolding harmonics at 3x would cross Nyquist and fold back down as
    /// ghost tones. With scale 0.22 the fundamental tops out ~3.5 kHz and the
    /// 5th wavefold harmonic (~17.6 kHz) stays below Nyquist.
    /// </summary>
    public double BpfScale = 0.22;
    public double IdleEngineRpmNorm = 0.332;   // DE6 measured idle
    public double TauSpool = 1.8;
    public double TauDump = 1.2;
    public double WhineGain = 0.4;
    /// <summary>Exponent of the whine gain curve (gain = (w/max)^exponent).
    /// 2.0 is the physical default; lower values make partial-spool audible.</summary>
    public double WhineGainExponent = 2.0;
    public double FlowGain = 0.6;
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
    private double _targetTurboRpm;
    private double _targetBoost = 1.0;
    private double _rampFromRpm;
    private double _rampFromBoost = 1.0;
    private int _rampSamples = 1;
    private int _rampPos;
    private double _phase;
    private double _jitter;
    private double _lpfState;
    private double _surgeEnvelope;
    private double _surgePhase;
    private double _cabLp1;
    private double _cabLp2;
    private double _prevLoad;
    private bool _hasPrevLoad;

    public double TurboRpm => _turboRpm;
    public double Boost => _boost;

    /// <summary>
    /// When true, ProcessSample skips the internal lag physics and synthesizes
    /// from the state pushed via SetExternalState (game-coupled mode).
    /// </summary>
    public bool UseExternalState;

    /// <summary>
    /// Pushes game-derived turbo state targets: shaft speed [rpm] and boost
    /// ratio [1..]. Values ramp linearly across each buffer (see BeginBuffer)
    /// so the whine glides continuously instead of zipper-stepping.
    /// </summary>
    public void SetExternalState(double turboRpm, double boostPressure)
    {
        _targetTurboRpm = Math.Max(0.0, turboRpm);
        _targetBoost = Math.Max(1.0, boostPressure);
    }

    /// <summary>
    /// Anchors a linear parameter ramp across the upcoming buffer. Call once
    /// per PCM buffer before the per-sample loop.
    /// </summary>
    public void BeginBuffer(int sampleCount)
    {
        _rampFromRpm = _turboRpm;
        _rampFromBoost = _boost;
        _rampSamples = Math.Max(1, sampleCount);
        _rampPos = 0;
    }

    public GeminiTurboDsp(GeminiParams p)
    {
        _p = p;
        _rng = new Random(p.Seed);
    }

    public double ProcessSample(double engineRpmNorm, double load, double dt)
    {
        GeminiParams p = _p;
        double sr = p.SampleRate;

        if (!UseExternalState)
        {
            double target = engineRpmNorm * engineRpmNorm * (20000.0 + 60000.0 * Math.Max(0.0, load));
            double tau = target > _turboRpm ? p.TauSpool : p.TauDump;
            _turboRpm += (target - _turboRpm) * Math.Min(1.0, dt / tau);
            _boost = 1.0 + 2.5 * (_turboRpm / p.MaxTurboRpm) * Math.Max(0.0, load);
        }
        else
        {
            _rampPos = Math.Min(_rampPos + 1, _rampSamples);
            double t = (double)_rampPos / _rampSamples;
            _turboRpm = _rampFromRpm + (_targetTurboRpm - _rampFromRpm) * t;
            _boost = _rampFromBoost + (_targetBoost - _rampFromBoost) * t;
        }

        if (_hasPrevLoad && (load - _prevLoad) / dt < p.SurgeRateThreshold && _boost > 2.0 && _surgeEnvelope <= 0.001)
        {
            _surgeEnvelope = 1.0;
            _surgePhase = 0.0;
        }
        _prevLoad = load;
        _hasPrevLoad = true;

        double bpf = (_turboRpm / 60.0) * p.BladeCount * p.BpfScale;
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
        double whineGain = Math.Pow(w, p.WhineGainExponent) * p.WhineGain;

        double cutoff = 400.0 + 6000.0 * w;
        double rc = 1.0 / (2.0 * Math.PI * cutoff);
        double alpha = dt / (rc + dt);
        double white2 = _rng.NextDouble() * 2.0 - 1.0;
        _lpfState += alpha * (white2 - _lpfState);
        // air flow tracks engine demand too - without the load term the rush
        // stays loud through load rejection while the whine decays away
        double loadTerm = 0.35 + 0.65 * Clamp01(load);
        double flowGain = w * loadTerm * p.FlowGain;

        double surgeMod = 1.0;
        if (_surgeEnvelope > 0.001)
        {
            _surgePhase += 2.0 * Math.PI * 16.0 / sr;
            surgeMod = 1.0 + 0.6 * Math.Sin(_surgePhase) * _surgeEnvelope;
            _surgeEnvelope *= Math.Exp(-4.0 * dt);
        }

        double output = tonal * whineGain + _lpfState * flowGain * surgeMod;

        if (p.CabFilter)
        {
            double kCab = 1.0 - Math.Exp(-2.0 * Math.PI * p.CabFilterCutoffHz / sr);
            _cabLp1 += kCab * (output - _cabLp1);
            _cabLp2 += kCab * (_cabLp1 - _cabLp2);
            output = _cabLp2;
        }

        return output;
    }

    public void TriggerSurge()
    {
        _surgeEnvelope = 1.0;
        _surgePhase = 0.0;
    }

    private static double Clamp01(double v) => v < 0.0 ? 0.0 : (v > 1.0 ? 1.0 : v);
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

    /// <summary>
    /// Renders a seamless loop of the steady-state operating point at the
    /// given load: spools up, discards the transient, then folds the buffer
    /// tail into the head (standard loop-equalize) so the wrap is continuous.
    /// </summary>
    public static float[] RenderLoop(GeminiParams p, double steadySeconds, double load)
    {
        double spoolSeconds = 4.0 * p.TauSpool + 1.0;
        int total = (int)(p.SampleRate * (spoolSeconds + steadySeconds));
        var dsp = new GeminiTurboDsp(p);
        double dt = 1.0 / p.SampleRate;
        double rpmNorm = p.IdleEngineRpmNorm + (1.0 - p.IdleEngineRpmNorm) * load;

        int skip = (int)(p.SampleRate * spoolSeconds);
        int n = total - skip;
        var kept = new float[n];
        for (int i = 0; i < total; i++)
        {
            double s = dsp.ProcessSample(rpmNorm, load, dt);
            if (i >= skip) kept[i - skip] = (float)s;
        }

        int xf = Math.Min(n / 4, (int)(p.SampleRate * 0.05));
        var output = new float[n - xf];
        for (int i = 0; i < output.Length; i++)
        {
            if (i < xf)
            {
                double w = (double)i / xf;
                output[i] = (float)(kept[i] * w + kept[n - xf + i] * (1.0 - w));
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

    private static void Normalize(float[] samples, double peak)
    {
        if (peak < 1e-9) return;
        float gain = (float)(0.9 / peak);
        for (int i = 0; i < samples.Length; i++) samples[i] *= gain;
    }
}
