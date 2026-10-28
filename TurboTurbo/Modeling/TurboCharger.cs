using System;

using UnityEngine;

namespace TurboTurbo.Modeling;

/// <summary>Models turbocharging. Hot exhaust gas spins up a turbine, that drives a compressor that builds charge to
/// above atmospheric pressure levels.</summary>
public sealed class TurboCharger : ICharger
{
    /// <summary>Configuration constants. Defaults give a reasonable starting point.</summary>
    public sealed class Settings
    {
        internal const float DefaultLambdaCalibration = 1.41f;
        internal const float DefaultBoostChargeMultiplier = 1.125f;
        internal const float DefaultTauUp = 3.0f;
        internal const float DefaultTauDown = 1.5f;
        internal const float DefaultSurgeRateThreshold = 15f;
        internal const float DefaultCompressionRatio = 13f;
        internal const float DefaultIntakeNtu = 1f;

        public float LambdaCalibration { get; set; } = DefaultLambdaCalibration;

        public float BoostChargeMultiplier { get; set; } = DefaultBoostChargeMultiplier;

        public float TauUp { get; set; } = DefaultTauUp;

        public float TauDown { get; set; } = DefaultTauDown;

        /// <summary>Governor drop rate [1/s] that triggers a surge while boost is high.</summary>
        public float SurgeRateThreshold { get; set; } = DefaultSurgeRateThreshold;

        public float CompressionRatio { get; set; } = DefaultCompressionRatio;

        public float IntakeNtu { get; set; } = DefaultIntakeNtu;

        public Settings()
        {
        }

        public Settings(Settings other)
        {
            LambdaCalibration = other.LambdaCalibration;
            BoostChargeMultiplier = other.BoostChargeMultiplier;
            TauUp = other.TauUp;
            TauDown = other.TauDown;
            SurgeRateThreshold = other.SurgeRateThreshold;
            CompressionRatio = other.CompressionRatio;
            IntakeNtu = other.IntakeNtu;
        }

        public void Validate()
        {
            LambdaCalibration = Mathf.Max(0.01f, LambdaCalibration);
            BoostChargeMultiplier = Mathf.Max(0f, BoostChargeMultiplier);
            TauUp = Mathf.Max(0.01f, TauUp);
            TauDown = Mathf.Max(0.01f, TauDown);
            SurgeRateThreshold = Mathf.Max(0f, SurgeRateThreshold);
            CompressionRatio = Mathf.Max(1.01f, CompressionRatio);
            IntakeNtu = Mathf.Max(0f, IntakeNtu);
        }
    }

    private float _prevGovernor;

    public Settings Tuning { get; }

    /// <summary>Current turbo boost pressure ratio [0..1].</summary>
    public float Boost { get; private set; }

    public bool Surging { get; private set; }

    public float LambdaCalibration => Tuning.LambdaCalibration;

    public float CompressionRatio => Tuning.CompressionRatio;

    public float IntakeNtu => Tuning.IntakeNtu;

    public float CompressorEfficiency => 0.75f;

    // this is just charge here, because the pressure ratio generally tracks the charge ratio
    public float TurbinePressureRatio => Charge;

    /// <summary>
    /// Per-stroke cylinder charge.
    /// </summary>
    public float Charge => 1f + Tuning.BoostChargeMultiplier * Boost;

    public float RatedCharge => 1f + Tuning.BoostChargeMultiplier;

    public TurboCharger(Settings settings)
    {
        Tuning = settings ?? throw new ArgumentNullException(nameof(settings));
    }

    public void Tick(float delta, float rpmNorm, float governorNorm, float exhaustEnergy, bool engineOn)
    {
        var s = Tuning;

        var target = exhaustEnergy;

        var tau = target > Boost ? s.TauUp : s.TauDown;
        Boost += (target - Boost) * (1f - Mathf.Exp(-delta / tau));

        var governorRate = (_prevGovernor - governorNorm) / Mathf.Max(0.001f, delta);
        Surging = engineOn && governorRate > s.SurgeRateThreshold && Boost > 0.75f;
        _prevGovernor = governorNorm;
    }

    public ICharger Clone() => new TurboCharger(new Settings(Tuning));
}
