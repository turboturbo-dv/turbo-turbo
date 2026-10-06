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
        internal const float DefaultRpmBoostExponent = 1.2f;
        internal const float DefaultTauUp = 3.0f;
        internal const float DefaultTauDown = 1.5f;
        internal const float DefaultMinSpoolTau = 0.5f;
        internal const float DefaultThermalK = 0.8f;
        internal const float DefaultSurgeRateThreshold = 15f;

        public float LambdaCalibration { get; set; } = DefaultLambdaCalibration;

        public float BoostChargeMultiplier { get; set; } = DefaultBoostChargeMultiplier;

        public float RpmBoostExponent { get; set; } = DefaultRpmBoostExponent;

        public float TauUp { get; set; } = DefaultTauUp;

        public float TauDown { get; set; } = DefaultTauDown;

        public float MinSpoolTau { get; set; } = DefaultMinSpoolTau;

        public float ThermalK { get; set; } = DefaultThermalK;

        /// <summary>Governor drop rate [1/s] that triggers a surge while boost is high.</summary>
        public float SurgeRateThreshold { get; set; } = DefaultSurgeRateThreshold;

        public Settings()
        {
        }

        public Settings(Settings other)
        {
            LambdaCalibration = other.LambdaCalibration;
            BoostChargeMultiplier = other.BoostChargeMultiplier;
            RpmBoostExponent = other.RpmBoostExponent;
            TauUp = other.TauUp;
            TauDown = other.TauDown;
            MinSpoolTau = other.MinSpoolTau;
            ThermalK = other.ThermalK;
            SurgeRateThreshold = other.SurgeRateThreshold;
        }
    }

    private float _prevGovernor;

    public Settings Tuning { get; }

    /// <summary>Current turbo boost pressure ratio [0..1].</summary>
    public float Boost { get; private set; }

    public bool Surging { get; private set; }

    public float LambdaCalibration => Tuning.LambdaCalibration;

    /// <summary>
    /// Per-stroke cylinder charge.
    /// </summary>
    public float Charge => 1f + Tuning.BoostChargeMultiplier * Boost;

    public float ChargeAtFullPower => 1f + Tuning.BoostChargeMultiplier;

    public TurboCharger(Settings settings)
    {
        Tuning = settings ?? throw new ArgumentNullException(nameof(settings));
    }

    public void Tick(float delta, float fuelPerStroke, float overfuel, float rpmNorm, float governorNorm, bool engineOn)
    {
        var s = Tuning;

        var target = fuelPerStroke * Mathf.Pow(rpmNorm, s.RpmBoostExponent);

        var tau = fuelPerStroke > Boost
            ? Mathf.Max(s.MinSpoolTau, s.TauUp / (1f + s.ThermalK * overfuel))
            : s.TauDown;
        Boost += (target - Boost) * (1f - Mathf.Exp(-delta / tau));

        var governorRate = (_prevGovernor - governorNorm) / Mathf.Max(0.001f, delta);
        Surging = engineOn && governorRate > s.SurgeRateThreshold && Boost > 0.75f;
        _prevGovernor = governorNorm;
    }

    public ICharger Clone() => new TurboCharger(new Settings(Tuning));
}
