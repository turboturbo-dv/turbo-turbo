using System;

using UnityEngine;

namespace TurboTurbo.Modeling;

/// <summary>
/// Self-contained model of a diesel engine: fueling and overfueling
/// driven by the air charge supplied by its <see cref="ICharger"/>.
/// </summary>
public sealed class CombustionModel
{
    private readonly Func<float> _governorNorm;
    private readonly Func<float> _fuelNorm;
    private readonly Func<float> _rpmNorm;

    public ICharger Charger { get; }

    /// <summary>Current charger boost pressure ratio [0..1], 0 when naturally aspirated.</summary>
    public float Boost { get; private set; }

    /// <summary>Raw clamped governor (rack) command read this tick.</summary>
    public float GovernorNorm { get; private set; }

    /// <summary>Raw clamped fuel consumption per unit time read this tick.</summary>
    public float FuelNorm { get; private set; }

    /// <summary>Normalized engine speed read this tick.</summary>
    public float RpmNorm { get; private set; }

    /// <summary>Fuel consumption per engine stroke, derived from <see cref="FuelNorm"/> and <see cref="RpmNorm"/>.</summary>
    public float FuelPerStroke { get; private set; }

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

    /// <summary>True on the tick where the charger reported a surge. Reset on the next tick.</summary>
    public bool SurgeThisTick { get; private set; }

    public CombustionModel(Func<float> governorNorm, Func<float> fuelNorm, Func<float> rpmNorm, ICharger charger)
    {
        Charger = charger ?? throw new ArgumentNullException(nameof(charger));
        _governorNorm = governorNorm ?? throw new ArgumentNullException(nameof(governorNorm));
        _fuelNorm = fuelNorm ?? throw new ArgumentNullException(nameof(fuelNorm));
        _rpmNorm = rpmNorm ?? throw new ArgumentNullException(nameof(rpmNorm));
    }

    /// <summary>
    /// Advances the simulation by <paramref name="delta"/> seconds
    /// </summary>
    public void Tick(float delta, bool engineOn)
    {
        var governor = Mathf.Clamp01(_governorNorm());
        var fuel = Mathf.Clamp01(_fuelNorm());
        var rpm = Mathf.Clamp01(_rpmNorm());

        var fuelPerStroke = fuel / Mathf.Max(0.01f, rpm);

        GovernorNorm = governor;
        FuelNorm = fuel;
        RpmNorm = rpm;
        FuelPerStroke = fuelPerStroke;
        Charge = Charger.Charge;

        var calibration = Charger.LambdaCalibration;
        Lambda = Charge / (calibration * Mathf.Max(0.01f, fuelPerStroke));

        Overfuel = Mathf.Max(0f, fuelPerStroke - Charge / calibration);

        Charger.Tick(delta, fuelPerStroke, Overfuel, rpm, governor, engineOn);

        ExhaustHeat = Charger.ExhaustHeat;
        Boost = Charger.Boost;
        SurgeThisTick = Charger.Surging;
    }
}