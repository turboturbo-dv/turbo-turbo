using System;

using UnityEngine;

namespace TurboTurbo.Modeling;

/// <summary>
/// Exhaust stack fouling ("wet stacking") model. Unburned fuel condenses on cold
/// exhaust surfaces and boils back off once they are hot, with the surface's
/// thermal mass supplying the hysteresis.
/// </summary>
public sealed class StackModel
{
    /// <summary>Configuration for the stack model. Defaults give a reasonable starting point.</summary>
    public sealed class Settings
    {
        internal const float DefaultFillTime = 120f;
        internal const float DefaultClearTime = 60f;
        internal const float DefaultTauWall = 35f;
        internal const float DefaultFlowWarmBias = 0.25f;
        internal const float DefaultCaptureColdK = 420f;
        internal const float DefaultCaptureHotK = 520f;
        internal const float DefaultClearStartK = 540f;
        internal const float DefaultClearFullK = 660f;
        internal const float DefaultContactIdle = 0.7f;
        internal const float DefaultContactFullFlow = 0.15f;
        internal const float DefaultStickMax = 0.9f;
        internal const float DefaultReferenceUnburned = 0.0045f;

        /// <summary>Cold-idle time [s] to fill the stack from empty to full.</summary>
        public float FillTime { get; set; } = DefaultFillTime;

        /// <summary>Time [s] for a full stack to drain to 5% at full wall temperature.</summary>
        public float ClearTime { get; set; } = DefaultClearTime;

        /// <summary>Exhaust wall thermal inertia [s].</summary>
        public float TauWall { get; set; } = DefaultTauWall;

        /// <summary>Warm-up speed-up at zero flow, so idle warms the wall more slowly than a hard pull.</summary>
        public float FlowWarmBias { get; set; } = DefaultFlowWarmBias;

        /// <summary>Wall temperature [K] at and below which deposition is fully active.</summary>
        public float CaptureColdK { get; set; } = DefaultCaptureColdK;

        /// <summary>Wall temperature [K] at and above which no further deposition occurs.</summary>
        public float CaptureHotK { get; set; } = DefaultCaptureHotK;

        /// <summary>Wall temperature [K] at which boil-off begins.</summary>
        public float ClearStartK { get; set; } = DefaultClearStartK;

        /// <summary>Wall temperature [K] at which boil-off is fully active.</summary>
        public float ClearFullK { get; set; } = DefaultClearFullK;

        /// <summary>Wall-contact fraction at zero flow.</summary>
        public float ContactIdle { get; set; } = DefaultContactIdle;

        /// <summary>Wall-contact fraction at full flow.</summary>
        public float ContactFullFlow { get; set; } = DefaultContactFullFlow;

        /// <summary>Cap on the deposited fraction, leaving an escaping floor.</summary>
        public float StickMax { get; set; } = DefaultStickMax;

        /// <summary>Unburned fuel rate [1/s] at the reference cold idle, used to calibrate <see cref="FillTime"/>.</summary>
        public float ReferenceUnburned { get; set; } = DefaultReferenceUnburned;

        public Settings()
        {
        }

        public Settings(Settings other)
        {
            FillTime = other.FillTime;
            ClearTime = other.ClearTime;
            TauWall = other.TauWall;
            FlowWarmBias = other.FlowWarmBias;
            CaptureColdK = other.CaptureColdK;
            CaptureHotK = other.CaptureHotK;
            ClearStartK = other.ClearStartK;
            ClearFullK = other.ClearFullK;
            ContactIdle = other.ContactIdle;
            ContactFullFlow = other.ContactFullFlow;
            StickMax = other.StickMax;
            ReferenceUnburned = other.ReferenceUnburned;
        }

        public void Validate()
        {
            FillTime = Mathf.Max(0.01f, FillTime);
            ClearTime = Mathf.Max(0.01f, ClearTime);
            TauWall = Mathf.Max(0.01f, TauWall);
            FlowWarmBias = Mathf.Max(0f, FlowWarmBias);
            CaptureColdK = Mathf.Max(0f, CaptureColdK);
            CaptureHotK = Mathf.Max(CaptureColdK + 0.01f, CaptureHotK);
            ClearStartK = Mathf.Max(CaptureHotK, ClearStartK);
            ClearFullK = Mathf.Max(ClearStartK + 0.01f, ClearFullK);
            ContactIdle = Mathf.Clamp01(ContactIdle);
            ContactFullFlow = Mathf.Clamp(ContactFullFlow, 0f, ContactIdle);
            StickMax = Mathf.Clamp(StickMax, 0.01f, 1f);
            ReferenceUnburned = Mathf.Max(1e-5f, ReferenceUnburned);
        }
    }

    private readonly Settings _s;

    /// <summary>Temperature [K] of the lumped post-combustion exhaust metal.</summary>
    public float ExhaustWallTempK { get; private set; }

    /// <summary>Condensed fuel deposit level [0..1].</summary>
    public float WetStack { get; private set; }

    /// <summary>Normalized [0..1] white-vapour driver for the exhaust appearance.</summary>
    public float Vapour { get; private set; }

    /// <summary>Deposit fill rate [1/s].</summary>
    public float DepositRate { get; private set; }

    /// <summary>Unburned fuel that passed through without depositing [1/s].</summary>
    public float SlipRate { get; private set; }

    /// <summary>Boil-off rate [1/s].</summary>
    public float EvaporateRate { get; private set; }

    /// <summary>Deposit that did not fit, for telemetry only [1/s].</summary>
    public float Overflow { get; private set; }

    public Settings Tuning => _s;

    public StackModel(Settings settings, float ambientK)
    {
        _s = settings ?? throw new ArgumentNullException(nameof(settings));
        ExhaustWallTempK = Mathf.Max(1f, ambientK);
    }

    /// <summary>Fills the stack immediately (editor test button).</summary>
    public void Fill() => WetStack = 1f;

    /// <summary>
    /// Advances the simulation by <paramref name="delta"/> seconds.
    /// </summary>
    public void Tick(float delta, bool engineOn, float unburned, float flowNorm, float gasTempK, float ambientK)
    {
        var s = _s;
        var ambient = Mathf.Max(1f, ambientK);
        var flow = Mathf.Clamp01(flowNorm);

        // the wall chases the gas while running and ambient when off, faster with more flow
        var target = engineOn ? gasTempK : ambient;
        var wallTau = s.TauWall / (s.FlowWarmBias + flow);
        ExhaustWallTempK += (target - ExhaustWallTempK) * (1f - Mathf.Exp(-delta / wallTau));

        // cold walls and slow flow both raise the deposited share; StickMax leaves an escaping floor
        var unburnedRate = engineOn ? Mathf.Max(0f, unburned) : 0f;
        var coldness = 1f - Smoothstep(s.CaptureColdK, s.CaptureHotK, ExhaustWallTempK);
        var contact = Mathf.Lerp(s.ContactFullFlow, s.ContactIdle, 1f - flow);
        var stick = Mathf.Min(coldness * contact, s.StickMax);
        var deposit = stick * unburnedRate;
        var slip = (1f - stick) * unburnedRate;

        var hotness = Smoothstep(s.ClearStartK, s.ClearFullK, ExhaustWallTempK);
        var clearTau = s.ClearTime / 3f;
        var cleared = WetStack * hotness * (1f - Mathf.Exp(-delta / clearTau));

        var referenceStick = Mathf.Min(s.ContactIdle, s.StickMax);
        var fillGain = 1f / (s.FillTime * s.ReferenceUnburned * referenceStick);

        var next = WetStack + (fillGain * deposit * delta) - cleared;
        Overflow = next > 1f ? (next - 1f) / delta : 0f;
        WetStack = Mathf.Clamp01(next);

        DepositRate = fillGain * deposit;
        SlipRate = slip;
        EvaporateRate = cleared / delta;

        var slipNorm = slip / Mathf.Max(1e-4f, s.ReferenceUnburned);
        Vapour = Mathf.Clamp01(slipNorm + (WetStack * hotness));
    }

    private static float Smoothstep(float edge0, float edge1, float x)
    {
        var t = Mathf.Clamp01(Mathf.InverseLerp(edge0, edge1, x));
        return t * t * (3f - 2f * t);
    }
}
