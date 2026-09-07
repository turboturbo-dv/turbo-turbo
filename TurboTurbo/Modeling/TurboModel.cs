using System;

using UnityEngine;

namespace TurboTurbo.Modeling;

/// <summary>
/// Self-contained model of a turbocharged diesel engine and its turbocharger.
/// </summary>
public sealed class TurboModel
{
    /// <summary>Configuration constants. Defaults give a reasonable starting point.</summary>
    public sealed class Settings
    {
        public float LambdaCalibration { get; set; } = 1.65f;
        public float BoostChargeMultiplier { get; set; } = 1.125f;
        public float RpmTorqueExponent { get; set; } = 0f;
        public float RpmBoostExponent { get; set; } = 1.2f;
        public float TauUp { get; set; } = 3.0f;
        public float TauDown { get; set; } = 1.0f;
        public float MinSpoolTau { get; set; } = 0.5f;
        public float ThermalK { get; set; } = 0.8f;
        public float TorqueLambdaFloor { get; set; } = 0.7f;

        /// <summary>Demand drop rate [1/s] that triggers a surge while boost is high.</summary>
        public float SurgeRateThreshold { get; set; } = 15f;
    }

    private readonly Settings _settings;
    private readonly Func<float> _throttle;
    private readonly Func<float> _rpmNorm;

    internal Settings Tuning => _settings;

    private float _prevDemand;

    /// <summary>Current turbo boost pressure ratio [0..1].</summary>
    public float Boost { get; private set; }

    /// <summary>Raw clamped throttle demand read this tick, before the torque cap.</summary>
    public float Demand { get; private set; }

    /// <summary>Normalized engine speed read this tick.</summary>
    public float RpmNorm { get; private set; }

    /// <summary>
    /// Exhaust-gas energy proxy [0..1], where 1 is max energy flow (full fuel consumption at full RPM).
    /// </summary>
    public float ExhaustHeat { get; private set; }

    /// <summary>
    /// Per-stroke cylinder charge. 1.0 = naturally aspirated; boost raises it further, up to 1 + BoostChargeMultiplier
    /// </summary>
    public float Charge { get; private set; }

    /// <summary>
    /// Air-fuel ratio proxy. Calibrated so that 1 means perfectly mixed. Below 1 is rich, above 1 is lean.
    /// </summary>
    public float Lambda { get; private set; }

    /// <summary>Overfueling amount [0..1]: portion of fuel consumption that has no air left to burn.</summary>
    public float Overfuel { get; private set; }

    /// <summary>Throttle demand after the available-air torque cap is applied.
    /// The adapter writes this back to the engine's throttle port since we have no way to control torque (yet).</summary>
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
    public void Tick(float delta, bool engineOn)
    {
        var s = _settings;

        var demand = Mathf.Clamp01(_throttle());
        var rpmNorm = Mathf.Clamp01(_rpmNorm());
        Demand = demand;
        RpmNorm = rpmNorm;

        // gate all combustion on the engine's own running state, the port may not read 0
        var fuelDemand = engineOn ? demand : 0f;

        Charge = 1f + s.BoostChargeMultiplier * Boost;

        Lambda = Charge / (s.LambdaCalibration * Mathf.Max(0.01f, fuelDemand));

        // Cap torque by usable air charge. Extra fuel below TorqueLambdaFloor still
        // produces work rather than instant torque loss to keep lugging engines from stalling.
        var rpmFactor = Mathf.Pow(rpmNorm, s.RpmTorqueExponent);
        var fuelMaxTorque = rpmFactor * Charge
                            / (s.LambdaCalibration * s.TorqueLambdaFloor);
        EffectiveDemand = Mathf.Min(fuelDemand, fuelMaxTorque);

        Overfuel = Mathf.Max(0f, fuelDemand - Charge / s.LambdaCalibration);

        // Exhaust heat is the proxy for boost target
        var rpmMassFlow = Mathf.Pow(rpmNorm, s.RpmBoostExponent);
        var target = fuelDemand * rpmMassFlow;
        ExhaustHeat = target;

        var tau = fuelDemand > Boost
            ? Mathf.Max(s.MinSpoolTau, s.TauUp / (1f + s.ThermalK * Overfuel))
            : s.TauDown;
        Boost += (target - Boost) * (1f - Mathf.Exp(-delta / tau));

        var demandRate = (_prevDemand - demand) / Mathf.Max(0.001f, delta);
        SurgeThisTick = engineOn && demandRate > s.SurgeRateThreshold && Boost > 0.75f;
        _prevDemand = demand;
    }
}