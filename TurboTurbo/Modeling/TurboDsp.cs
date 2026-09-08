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
    private readonly Settings _p;
    private Xorshift32 _rng;

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

    public double TurboRpm { get; private set; }
    public double Boost { get; private set; } = 1.0;

    /// <summary>
    /// When true, ProcessSample skips the internal lag physics and synthesizes
    /// from the state pushed via SetExternalState (game-coupled mode).
    /// </summary>
    public bool UseExternalState;

    public TurboDsp(Settings p)
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
        _rampFromRpm = TurboRpm;
        _rampFromBoost = Boost;
        _rampSamples = Math.Max(1, sampleCount);
        _rampPos = 0;
    }

    public double ProcessSample(double engineRpmNorm, double load, double dt)
    {
        var p = _p;
        double sr = p.SampleRate;

        _lastDt = dt;
        _surgeDecay = Math.Exp(-4.0 * dt);

        if (!UseExternalState)
        {
            // exhaust-energy target, scaled so N=1, L=1 reaches MaxTurboRpm
            var target = engineRpmNorm * engineRpmNorm * (9000.0 + 27000.0 * Math.Max(0.0, load));
            var tau = target > TurboRpm ? p.TauSpool : p.TauDump;
            TurboRpm += (target - TurboRpm) * Math.Min(1.0, dt / tau);
            Boost = 1.0 + 2.5 * (TurboRpm / p.MaxTurboRpm) * Math.Max(0.0, load);
        }
        else
        {
            _rampPos = Math.Min(_rampPos + 1, _rampSamples);
            var t = (double)_rampPos / _rampSamples;
            TurboRpm = _rampFromRpm + (_targetTurboRpm - _rampFromRpm) * t;
            Boost = _rampFromBoost + (_targetBoost - _rampFromBoost) * t;
        }

        if (_hasPrevLoad && (load - _prevLoad) / dt < p.SurgeRateThreshold && Boost > 2.0 && _surgeEnvelope <= 0.001)
        {
            _surgeEnvelope = 1.0;
            _surgePhase = 0.0;
        }

        _prevLoad = load;
        _hasPrevLoad = true;

        var bpf = (TurboRpm / 60.0) * p.BladeCount * p.BpfScale;
        if (bpf > 0.45 * sr) bpf = 0.45 * sr;

        // pitch jitter: xorshift noise, low-passed at JitterHz
        var white1 = _rng.NextUnit() * 2.0 - 1.0;
        _jitter += _kJitter * (white1 - _jitter);
        var freq = bpf * (1.0 + p.JitterAmount * _jitter);

        // Branch A: whine
        _phase += 2.0 * Math.PI * freq / sr;
        if (_phase >= 2.0 * Math.PI) _phase -= 2.0 * Math.PI;
        var raw = Math.Sin(_phase);

        // dynamic harmonic taper: fade the wavefold drive as the fundamental
        // approaches the fold-free budget (3rd harmonic at Nyquist/3 = fs/6),
        // so harmonics taper off naturally instead of aliasing
        var foldBudget = sr / 6.0;
        var taper = (foldBudget - bpf) / (0.35 * foldBudget);
        if (taper > 1.0) taper = 1.0;
        else if (taper < 0.0) taper = 0.0;
        var drive = 1.0 + (Boost - 1.0) * taper;
        var tonal = Math.Tanh(raw * drive) / Math.Tanh(drive);

        var w = TurboRpm / p.MaxTurboRpm;

        // acoustic loading: sound power tracks boost pressure differential,
        // not shaft speed alone. Right after a load drop the blade loading
        // collapses even while shaft inertia keeps w high (0.1 floor = faint
        // high-rpm overrun whistle)
        var boostDeltaNorm = Clamp01((Boost - 1.0) / 2.5);
        var pressureFactor = 0.10 + 0.90 * boostDeltaNorm;

        var whineGain = Math.Pow(w, p.WhineGainExponent) * pressureFactor * p.WhineGain;

        // Branch B: flow noise, LPF cutoff tracks mass flow
        var cutoff = 400.0 + 6000.0 * w;
        var rc = 1.0 / (2.0 * Math.PI * cutoff);
        var alpha = dt / (rc + dt);
        var white2 = _rng.NextUnit() * 2.0 - 1.0;
        _lpfState += alpha * (white2 - _lpfState);

        // intake duct resonance: 2-pole SVF band-pass tracking 0.35 x BPF
        var fc = bpf * 0.35;
        if (fc < 40.0) fc = 40.0;
        else if (fc > 0.1 * sr) fc = 0.1 * sr;
        if (Math.Abs(fc - _svfLastFc) > 25.0)
        {
            _svfF = 2.0 * Math.Sin(Math.PI * fc / sr);
            _svfLastFc = fc;
        }

        _svfLow += _svfF * _svfBand;
        var svfHigh = white2 - _svfLow - _svfQInv * _svfBand;
        _svfBand += _svfF * svfHigh;
        var duct = _svfBand * p.DuctResGain;

        // air flow tracks engine demand too, without the load term the rush
        // stays loud through load rejection while the whine decays away
        var loadTerm = 0.35 + 0.65 * Clamp01(load);
        var flowGain = w * loadTerm * p.FlowGain;

        // Branch C: surge flutter AM on flow
        var surgeMod = 1.0;
        if (_surgeEnvelope > 0.001)
        {
            _surgePhase += 2.0 * Math.PI * 16.0 / sr;
            surgeMod = 1.0 + 0.6 * Math.Sin(_surgePhase) * _surgeEnvelope;
            _surgeEnvelope *= _surgeDecay;
        }

        var output = tonal * whineGain + (_lpfState + duct) * flowGain * surgeMod;

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


    // TODO: several of these are targeted at the DE6, which is reasonable as a default but we need to support other options.
    // to configure settings for other engines we should expose a flow via the Controller
    public sealed class Settings
    {
        // today's fish is trout a la creme, enjoy your meal
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

        public double IdleEngineRpmNorm = 0.332;
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
}