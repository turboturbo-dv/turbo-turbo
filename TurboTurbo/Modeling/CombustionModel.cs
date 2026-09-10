using System;

using UnityEngine;

namespace TurboTurbo.Modeling;

/// <summary>
/// Self-contained model of a diesel engine: fueling, torque cap and overfueling
/// driven by the air charge supplied by its <see cref="ICharger"/>.
/// </summary>
public sealed class CombustionModel
{
    /// <summary>Configuration constants. Defaults give a reasonable starting point.</summary>
    public sealed class Settings
    {
        public float RpmTorqueExponent { get; set; } = 0f;
        public float TorqueLambdaFloor { get; set; } = 0.7f;

        public Settings()
        {
        }

        public Settings(Settings other)
        {
            RpmTorqueExponent = other.RpmTorqueExponent;
            TorqueLambdaFloor = other.TorqueLambdaFloor;
        }
    }

    private readonly Func<float> _throttle;
    private readonly Func<float> _fuelNorm;
    private readonly Func<float> _rpmNorm;

    public Settings Tuning { get; }
    public ICharger Charger { get; }

    /// <summary>Current charger boost pressure ratio [0..1], 0 when naturally aspirated.</summary>
    public float Boost { get; private set; }

    /// <summary>Raw clamped throttle demand read this tick, before the torque cap.</summary>
    public float Demand { get; private set; }

    /// <summary>Raw clamped fuel consumption read this tick.</summary>
    public float FuelNorm { get; private set; }

    /// <summary>Normalized engine speed read this tick.</summary>
    public float RpmNorm { get; private set; }

    /// <summary>RPM-independent fuel consumption (per stroke).</summary>
    public float FuelPerStroke { get; set; }

    /// <summary>
    /// Exhaust-gas energy proxy [0..1], where 1 is max energy flow (full fuel consumption at full RPM).
    /// </summary>
    public float ExhaustHeat { get; private set; }

    /// <summary>Per-stroke cylinder charge supplied by the charger.</summary>
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

    /// <summary>True on the tick where the charger reported a surge. Reset on the next tick.</summary>
    public bool SurgeThisTick { get; private set; }

    public CombustionModel(Settings settings, Func<float> throttle, Func<float> fuelNorm, Func<float> rpmNorm, ICharger charger)
    {
        Tuning = settings ?? throw new ArgumentNullException(nameof(settings));
        Charger = charger ?? throw new ArgumentNullException(nameof(charger));
        _throttle = throttle ?? throw new ArgumentNullException(nameof(throttle));
        _fuelNorm = fuelNorm ?? throw new ArgumentNullException(nameof(fuelNorm));
        _rpmNorm = rpmNorm ?? throw new ArgumentNullException(nameof(rpmNorm));
    }

    /// <summary>
    /// Advances the simulation by <paramref name="delta"/> seconds
    /// </summary>
    public void Tick(float delta, bool engineOn)
    {
        var s = Tuning;

        var throttle = Mathf.Clamp01(_throttle());
        var fuel = Mathf.Clamp01(_fuelNorm());
        var rpm = Mathf.Clamp01(_rpmNorm());

        // no clamp01 yet, validate how well it behaves
        var fuelPerStroke = fuel / Mathf.Max(0.01f, rpm);

        Demand = throttle;
        FuelNorm = fuel;
        RpmNorm = rpm;
        FuelPerStroke = fuelPerStroke;
        Charge = Charger.Charge;

        var calibration = Charger.LambdaCalibration;
        Lambda = Charge / (calibration * Mathf.Max(0.01f, fuelPerStroke));

        // Cap torque by usable air charge. Extra fuel below TorqueLambdaFloor still
        // produces work rather than instant torque loss to keep lugging engines from stalling.
        var rpmFactor = Mathf.Pow(rpm, s.RpmTorqueExponent);
        var fuelMaxTorque = rpmFactor * Charge
                            / (calibration * s.TorqueLambdaFloor);
        EffectiveDemand = Mathf.Min(fuelPerStroke, fuelMaxTorque);

        Overfuel = Mathf.Max(0f, fuelPerStroke - Charge / calibration);

        Charger.Tick(delta, fuelPerStroke, Overfuel, rpm, throttle, engineOn);

        ExhaustHeat = Charger.ExhaustHeat;
        Boost = Charger.Boost;
        SurgeThisTick = Charger.Surging;
    }
}