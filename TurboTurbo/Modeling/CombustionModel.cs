using System;
using System.ComponentModel;
using System.Xml.Serialization;

using UnityEngine;

namespace TurboTurbo.Modeling;

/// <summary>
/// Self-contained model of a diesel engine: fueling and overfueling
/// driven by the air charge supplied by its <see cref="ICharger"/>.
/// </summary>
public sealed class CombustionModel
{
    /// <summary>Configuration constants for the exhaust-state quantities. Defaults give a reasonable starting point.</summary>
    [XmlType("CombustionSettings")]
    public sealed class Settings
    {
        internal const float DefaultRatedExhaustTempK = 760f;

        /// <summary>
        /// Rated exhaust gas temperature [K] at the exhaust mouth at full power. Note that this is a target value;
        /// transient conditions may overshoot it.
        /// </summary>
        [DefaultValue(DefaultRatedExhaustTempK)]
        public float RatedExhaustTempK { get; set; } = DefaultRatedExhaustTempK;

        public Settings()
        {
        }

        public Settings(Settings other)
        {
            RatedExhaustTempK = other.RatedExhaustTempK;
        }

        public void Validate()
        {
            RatedExhaustTempK = Mathf.Max(PhysicsConstants.ReferenceAmbientK + 0.01f, RatedExhaustTempK);
        }
    }

    private readonly Settings _tuning;
    private readonly ExhaustVelocitySettings _velocity;

    private readonly Func<float> _governorNorm;
    private readonly Func<float> _fuelNorm;
    private readonly Func<float> _rpmNorm;
    private readonly Func<float> _ambientTemperatureK;

    // isentropic expansion exponent for the turbine drop
    private const float IsentropicExponent =
        (PhysicsConstants.SpecificHeatRatio - 1f) / PhysicsConstants.SpecificHeatRatio;

    public ICharger Charger { get; }

    public Settings Tuning => _tuning;

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

    /// <summary>Per-stroke cylinder charge supplied by the charger.</summary>
    public float Charge { get; private set; }

    /// <summary>
    /// Air-fuel ratio proxy. Calibrated so that 1 means perfectly mixed. Below 1 is rich, above 1 is lean.
    /// </summary>
    public float Lambda { get; private set; }

    /// <summary>Overfueling amount [0..1]: portion of fuel consumption that has no air left to burn.</summary>
    public float Overfuel { get; private set; }

    /// <summary>Exhaust mass-flow proxy (per-stroke charge times engine speed).</summary>
    public float MassFlow { get; private set; }

    /// <summary>Exhaust gas temperature [K] at the exhaust mouth.</summary>
    public float GasTemperature { get; private set; }

    /// <summary>Exhaust gas density [kg/m^3] at the exhaust mouth.</summary>
    public float GasDensity { get; private set; }

    /// <summary>Temperature the turbine extracts before the exhaust reaches the mouth [K].</summary>
    public float TurbineTemperatureDropK { get; private set; }

    /// <summary>
    /// Normalized exhaust heat-release rate [0..1]: mass flow weighted by combustion efficiency.
    /// </summary>
    public float ExhaustEnergy { get; private set; }

    /// <summary>Exhaust plume speed [m/s] at the exhaust mouth.</summary>
    public float ExhaustVelocity { get; private set; }

    /// <summary>True on the tick where the charger reported a surge. Reset on the next tick.</summary>
    public bool SurgeThisTick { get; private set; }

    public CombustionModel(Func<float> governorNorm, Func<float> fuelNorm, Func<float> rpmNorm,
        Func<float> ambientTemperatureK, ICharger charger, Settings settings, ExhaustVelocitySettings velocity)
    {
        Charger = charger ?? throw new ArgumentNullException(nameof(charger));
        _tuning = settings ?? throw new ArgumentNullException(nameof(settings));
        _velocity = velocity ?? throw new ArgumentNullException(nameof(velocity));
        _governorNorm = governorNorm ?? throw new ArgumentNullException(nameof(governorNorm));
        _fuelNorm = fuelNorm ?? throw new ArgumentNullException(nameof(fuelNorm));
        _rpmNorm = rpmNorm ?? throw new ArgumentNullException(nameof(rpmNorm));
        _ambientTemperatureK = ambientTemperatureK ?? throw new ArgumentNullException(nameof(ambientTemperatureK));
    }

    /// <summary>
    /// Advances the simulation by <paramref name="delta"/> seconds
    /// </summary>
    public void Tick(float delta, bool engineOn)
    {
        var governor = Mathf.Clamp01(_governorNorm());
        var fuel = Mathf.Clamp01(_fuelNorm());
        var rpm = Mathf.Clamp01(_rpmNorm());

        var s = _tuning;

        var fuelPerStroke = fuel / Mathf.Max(0.01f, rpm);

        GovernorNorm = governor;
        FuelNorm = fuel;
        RpmNorm = rpm;
        FuelPerStroke = fuelPerStroke;
        Charge = Charger.Charge;

        var calibration = Charger.LambdaCalibration;
        Lambda = Charge / (calibration * Mathf.Max(0.01f, fuelPerStroke));

        Overfuel = Mathf.Max(0f, fuelPerStroke - Charge / calibration);

        // simplified model for now; in reality, burn fraction already starts dropping before lambda reaches 1, as
        // non-perfect mixing causes local rich pockets to start to form in the cylinder even while global lambda is still lean
        var burnFraction = Mathf.Min(1f, Lambda);
        var ambient = Mathf.Max(1f, _ambientTemperatureK());

        MassFlow = Charge * rpm; // fuel mass is so small relative to air mass as to be negligible

        // dynamically derive temperature gain based on rated exhaust temperature at load.
        // this makes it easier to alter temperature behaviour of the engine: just say what the rated exhaust gas
        // temperature at exhaust mouth should be, and the rest follows.
        var tempGainK = (s.RatedExhaustTempK - PhysicsConstants.ReferenceAmbientK) * Charger.ChargeAtFullPower;

        var manifoldTemperature = ambient + tempGainK * fuelPerStroke * burnFraction / Charge;
        var turbinePressureRatio = Mathf.Max(1f, Charge);
        TurbineTemperatureDropK = manifoldTemperature *
                                  (1f - Mathf.Pow(turbinePressureRatio, -IsentropicExponent));

        GasTemperature = manifoldTemperature - TurbineTemperatureDropK;
        GasDensity = PhysicsConstants.ReferenceAirDensity * ambient / Mathf.Max(1f, GasTemperature);
        ExhaustEnergy = MassFlow * (manifoldTemperature - ambient) / tempGainK;
        ExhaustVelocity = _velocity.Calculate(MassFlow, GasDensity);

        Charger.Tick(delta, fuelPerStroke, Overfuel, rpm, governor, engineOn);

        Boost = Charger.Boost;
        SurgeThisTick = Charger.Surging;
    }
}
