using System;

namespace TurboTurbo.Modeling;

/// <summary>
/// Per-sample DSP engine following a hybrid procedural design:
/// Branch A: tonal whine - sine at blade-passing frequency with boost-driven
///           wavefolding (tanh saturation, dynamically tapered near Nyquist)
///           and low-frequency pitch jitter.
/// Branch B: broadband flow - noise through a one-pole LPF tracking mass air
///           flow, plus a 2-pole state-variable band-pass resonator tracking
///           0.35 x BPF (intake duct / airbox resonance).
/// Branch C: surge flutter - 16 Hz AM on the flow layer with exponential decay,
///           triggered by rapid load rejection at high boost.
/// State layer: turbo rpm first-order lag, target = N^2 * (a + b*load), or
/// game-coupled external state (UseExternalState + BeginBuffer ramping).
/// Filter coefficients and the PRNG are precomputed/latched so the per-sample
/// loop performs no transcendental setup work.
/// </summary>
public sealed class TurboDsp
{
    private readonly TurboDspParams _p;
    private Xorshift32 _rng;

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

    // precomputed coefficients
    private readonly double _kJitter;
    private double _surgeDecay = Math.Exp(-4.0 / 48000.0);
    private double _lastDt = -1.0;
    private double _kCab;
    private double _lastCabCutoff = -1.0;
    private double _svfF;
    private double _svfLastFc = -1.0;
    private readonly double _svfQInv;

    // state-variable filter states
    private double _svfLow;
    private double _svfBand;

    public double TurboRpm => _turboRpm;
    public double Boost => _boost;

    /// <summary>
    /// When true, ProcessSample skips the internal lag physics and synthesizes
    /// from the state pushed via SetExternalState (game-coupled mode).
    /// </summary>
    public bool UseExternalState;

    public TurboDsp(TurboDspParams p)
    {
        _p = p;
        _rng = new Xorshift32((uint)p.Seed);
        _kJitter = 1.0 - Math.Exp(-2.0 * Math.PI * p.JitterHz / p.SampleRate);
        _svfQInv = 1.0 / Math.Max(0.5, p.DuctQ);
    }

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

    public double ProcessSample(double engineRpmNorm, double load, double dt)
    {
        TurboDspParams p = _p;
        double sr = p.SampleRate;

        if (dt != _lastDt)
        {
            _lastDt = dt;
            _surgeDecay = Math.Exp(-4.0 * dt);
        }

        if (!UseExternalState)
        {
            // exhaust-energy target, scaled so N=1, L=1 reaches MaxTurboRpm
            // (same proportions as Gemini's original: 25% floor at full N, no load)
            double target = engineRpmNorm * engineRpmNorm * (9000.0 + 27000.0 * Math.Max(0.0, load));
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

        // pitch jitter: xorshift noise, low-passed at JitterHz
        double white1 = _rng.NextUnit() * 2.0 - 1.0;
        _jitter += _kJitter * (white1 - _jitter);
        double freq = bpf * (1.0 + p.JitterAmount * _jitter);

        // Branch A: whine
        _phase += 2.0 * Math.PI * freq / sr;
        if (_phase >= 2.0 * Math.PI) _phase -= 2.0 * Math.PI;
        double raw = Math.Sin(_phase);

        // dynamic harmonic taper: fade the wavefold drive as the fundamental
        // approaches the fold-free budget (3rd harmonic at Nyquist/3 = fs/6),
        // so harmonics taper off naturally instead of aliasing
        double foldBudget = sr / 6.0;
        double taper = (foldBudget - bpf) / (0.35 * foldBudget);
        if (taper > 1.0) taper = 1.0;
        else if (taper < 0.0) taper = 0.0;
        double drive = 1.0 + (_boost - 1.0) * taper;
        double tonal = Math.Tanh(raw * drive) / Math.Tanh(drive);

        double w = _turboRpm / p.MaxTurboRpm;

        // aeroacoustic loading: sound power tracks boost pressure differential,
        // not shaft speed alone - right after a load drop the blade loading
        // collapses even while shaft inertia keeps w high (0.1 floor = faint
        // high-rpm overrun whistle)
        double boostDeltaNorm = Clamp01((_boost - 1.0) / 2.5);
        double pressureFactor = 0.10 + 0.90 * boostDeltaNorm;

        double whineGain = Math.Pow(w, p.WhineGainExponent) * pressureFactor * p.WhineGain;

        // Branch B: flow noise, LPF cutoff tracks mass flow
        double cutoff = 400.0 + 6000.0 * w;
        double rc = 1.0 / (2.0 * Math.PI * cutoff);
        double alpha = dt / (rc + dt);
        double white2 = _rng.NextUnit() * 2.0 - 1.0;
        _lpfState += alpha * (white2 - _lpfState);

        // intake duct resonance: 2-pole SVF band-pass tracking 0.35 x BPF
        double fc = bpf * 0.35;
        if (fc < 40.0) fc = 40.0;
        else if (fc > 0.1 * sr) fc = 0.1 * sr;
        if (Math.Abs(fc - _svfLastFc) > 25.0)
        {
            _svfF = 2.0 * Math.Sin(Math.PI * fc / sr);
            _svfLastFc = fc;
        }
        _svfLow += _svfF * _svfBand;
        double svfHigh = white2 - _svfLow - _svfQInv * _svfBand;
        _svfBand += _svfF * svfHigh;
        double duct = _svfBand * p.DuctResGain;

        // air flow tracks engine demand too - without the load term the rush
        // stays loud through load rejection while the whine decays away
        double loadTerm = 0.35 + 0.65 * Clamp01(load);
        double flowGain = w * loadTerm * p.FlowGain;

        // Branch C: surge flutter AM on flow
        double surgeMod = 1.0;
        if (_surgeEnvelope > 0.001)
        {
            _surgePhase += 2.0 * Math.PI * 16.0 / sr;
            surgeMod = 1.0 + 0.6 * Math.Sin(_surgePhase) * _surgeEnvelope;
            _surgeEnvelope *= _surgeDecay;
        }

        double output = tonal * whineGain + (_lpfState + duct) * flowGain * surgeMod;

        if (p.CabFilter)
        {
            if (Math.Abs(p.CabFilterCutoffHz - _lastCabCutoff) > 1.0)
            {
                _kCab = 1.0 - Math.Exp(-2.0 * Math.PI * p.CabFilterCutoffHz / sr);
                _lastCabCutoff = p.CabFilterCutoffHz;
            }
            _cabLp1 += _kCab * (output - _cabLp1);
            _cabLp2 += _kCab * (_cabLp1 - _cabLp2);
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