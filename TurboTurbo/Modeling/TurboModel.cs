using System;

namespace TurboTurbo.Modeling;

/// <summary>
/// Self-contained model of a turbocharged diesel engine and its turbocharger.
/// </summary>
public sealed class TurboModel
{
    /// <summary>Configuration constants. Defaults give a reasonable starting point.</summary>
    public sealed class Settings
    {
        public float AirNAFraction { get; set; } = 0.55f;
        public float LambdaCalibration { get; set; } = 2.5f;
        public float BoostChargeMultiplier { get; set; } = 2.5f;
        public float RpmTorqueExponent { get; set; } = 0f;
        public float RpmBoostExponent { get; set; } = 1.2f;
        public float TauUp { get; set; } = 3.0f;
        public float TauDown { get; set; } = 1.0f;
        public float MinSpoolTau { get; set; } = 0.5f;
        public float ThermalK { get; set; } = 0.8f;
        public float TorqueLambdaFloor { get; set; } = 0.7f;
    }

    private readonly Settings _settings;
    private readonly Func<float> _throttle;
    private readonly Func<float> _rpmNorm;

    internal Settings Tuning => _settings;

    private float _boost;
    private float _prevDemand;

    /// <summary>Current turbo boost pressure ratio [0..1].</summary>
    public float Boost { get; private set; }

    /// <summary>Raw clamped throttle demand read this tick, before the torque cap.</summary>
    public float Demand { get; private set; }

    /// <summary>Clamped normalized engine speed read this tick.</summary>
    public float RpmNorm { get; private set; }

    /// <summary>
    /// Exhaust-gas energy proxy [0..1]. The boost lag chases this,
    /// but exhaust temperature follows combustion immediately.
    /// </summary>
    public float ExhaustHeat { get; private set; }

    /// <summary>Per-stroke cylinder charge index (1.0 = naturally aspirated).</summary>
    public float Charge { get; private set; }

    /// <summary>Air-fuel ratio proxy (calibrated: ~1.0 = edge of clean full load).</summary>
    public float Lambda { get; private set; }

    /// <summary>Overfueling amount [0..1]: fuel beyond available air.</summary>
    public float Overfuel { get; private set; }

    /// <summary>Throttle demand after the available-air torque cap is applied.
    /// The adapter writes this back to the engine's throttle port.</summary>
    public float EffectiveDemand { get; private set; }

    /// <summary>True on the tick where a turbo surge was detected (sharp demand
    /// drop while boost is high). Reset on the next tick.</summary>
    public bool SurgeThisTick { get; private set; }

    public TurboModel(Settings settings, Func<float> throttle, Func<float> rpmNorm)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _throttle = throttle ?? throw new ArgumentNullException(nameof(throttle));
        _rpmNorm = rpmNorm ?? throw new ArgumentNullException(nameof(rpmNorm));
    }

    /// <summary>
    /// Advances the simulation by <paramref name="delta"/> seconds
    /// </summary>
    public void Tick(float delta, float fuelNorm, bool engineOn)
    {
        var s = _settings;

        var demand = Clamp(_throttle(), 0f, 1f);
        var rpmNorm = Clamp(_rpmNorm(), 0f, 1f);
        Demand = demand;
        RpmNorm = rpmNorm;

        // gate all combustion on the engine's own running state, the port may not read 0
        var fuelDemand = engineOn ? demand : 0f;

        // 1.0 = naturally aspirated
        // full boost = NA + (1-NA) x (1 + BoostChargeMultiplier)
        Charge = s.AirNAFraction + (1f - s.AirNAFraction) * (1f + s.BoostChargeMultiplier * _boost);

        Lambda = Charge / (s.LambdaCalibration * Math.Max(0.01f, fuelDemand));

        // Capping torque by usable air charge. Extra fuel below TorqueLambdaFloor still 
        // produces work rather than instant torque loss to keep lugging engines from stalling.
        var rpmFactor = Clamp(rpmNorm, 0f, 1f);
        rpmFactor = (float)Math.Pow(rpmFactor, s.RpmTorqueExponent);
        var fuelMaxTorque = rpmFactor * Charge
                            / (s.LambdaCalibration * s.TorqueLambdaFloor);
        EffectiveDemand = Math.Min(fuelDemand, fuelMaxTorque);

        Overfuel = Math.Max(0f, fuelDemand - Charge / s.LambdaCalibration);

        // Boost lag chases equilibrium set by exhaust mass flow. Spooling checks fuelDemand 
        // directly so lug-driven target drops decay with turbine inertia.
        var rpmMassFlow = (float)Math.Pow(Clamp(rpmNorm, 0f, 1f), s.RpmBoostExponent);
        var target = Clamp(fuelDemand, 0f, 1f) * rpmMassFlow;
        ExhaustHeat = target;
        var tau = fuelDemand > _boost
            ? Math.Max(s.MinSpoolTau, s.TauUp / (1f + s.ThermalK * Overfuel))
            : s.TauDown;
        _boost += (target - _boost) * (1f - (float)Math.Exp(-delta / tau));
        Boost = _boost;

        SurgeThisTick = engineOn && _prevDemand - demand > 0.3f && _boost > 0.75f;
        _prevDemand = demand;
    }

    private static float Clamp(float value, float min, float max)
    {
        if (value < min) return min;
        if (value > max) return max;
        return value;
    }
}