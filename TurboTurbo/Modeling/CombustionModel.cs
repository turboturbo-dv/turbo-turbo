using System;

using UnityEngine;

namespace TurboTurbo.Modeling;

/// <summary>
/// Self-contained model of a diesel engine: fueling and overfueling
/// driven by the air charge supplied by its <see cref="ICharger"/>.
/// </summary>
public sealed class CombustionModel
{
    /// <summary>Configuration constants for the exhaust-state quantities. Defaults give a reasonable starting point.</summary>
    public sealed class Settings
    {
        internal const float DefaultRatedExhaustTempK = 760f;
        internal const float DefaultTauCylinder = 10f;
        internal const float DefaultTauEngine = 55f;
        internal const float DefaultTauCooldownOpen = 65f;
        internal const float DefaultTauCooldownClosed = 1200f;
        internal const float DefaultCylinderGainK = 60f;
        internal const float DefaultColdWallFloorK = 260f;
        internal const float DefaultWarmWallTargetK = 370f;
        internal const float DefaultMinBurnFractionAtCold = 0.85f;

        /// <summary>Block temperature [K] where the radiator thermostat begins to open.</summary>
        public const float ThermostatOpenK = 65f + PhysicsConstants.KelvinOffset;

        /// <summary>Block temperature [K] where the radiator thermostat is fully open.</summary>
        public const float ThermostatFullOpenK = 85f + PhysicsConstants.KelvinOffset;

        /// <summary>
        /// Rated exhaust gas temperature [K] at the exhaust mouth at full power. Note that this is a target value;
        /// transient conditions may overshoot it.
        /// </summary>
        public float RatedExhaustTempK { get; set; } = DefaultRatedExhaustTempK;
        public float TauCylinder { get; set; } = DefaultTauCylinder;
        public float TauEngine { get; set; } = DefaultTauEngine;
        public float TauCooldownOpen { get; set; } = DefaultTauCooldownOpen;
        public float TauCooldownClosed { get; set; } = DefaultTauCooldownClosed;
        public float CylinderGainK { get; set; } = DefaultCylinderGainK;
        public float ColdWallFloorK { get; set; } = DefaultColdWallFloorK;
        public float WarmWallTargetK { get; set; } = DefaultWarmWallTargetK;

        /// <summary>Floor on the temperature efficiency factor at the coldest extreme (0..1].</summary>
        public float MinBurnFractionAtCold { get; set; } = DefaultMinBurnFractionAtCold;

        public Settings()
        {
        }

        public Settings(Settings other)
        {
            RatedExhaustTempK = other.RatedExhaustTempK;
            TauCylinder = other.TauCylinder;
            TauEngine = other.TauEngine;
            TauCooldownOpen = other.TauCooldownOpen;
            TauCooldownClosed = other.TauCooldownClosed;
            CylinderGainK = other.CylinderGainK;
            ColdWallFloorK = other.ColdWallFloorK;
            WarmWallTargetK = other.WarmWallTargetK;
            MinBurnFractionAtCold = other.MinBurnFractionAtCold;
        }

        public void Validate()
        {
            RatedExhaustTempK = Mathf.Max(PhysicsConstants.ReferenceAmbientK + 0.01f, RatedExhaustTempK);
            TauCylinder = Mathf.Max(0.01f, TauCylinder);
            TauEngine = Mathf.Max(TauCylinder + 0.01f, TauEngine);
            TauCooldownOpen = Mathf.Max(0.01f, TauCooldownOpen);
            TauCooldownClosed = Mathf.Max(TauCooldownOpen, TauCooldownClosed);
            CylinderGainK = Mathf.Max(0.01f, CylinderGainK);
            ColdWallFloorK = Mathf.Max(0f, ColdWallFloorK);
            WarmWallTargetK = Mathf.Max(ColdWallFloorK + 0.01f, WarmWallTargetK);
            MinBurnFractionAtCold = Mathf.Clamp(MinBurnFractionAtCold, 0.01f, 1f);
        }
    }

    private readonly ExhaustVelocitySettings _velocity;

    private readonly Func<float> _governorNorm;
    private readonly Func<float> _fuelNorm;
    private readonly Func<float> _rpmNorm;
    private readonly Func<float> _ambientTemperatureK;

    // isentropic expansion exponent for the turbine drop
    private const float IsentropicExponent =
        (PhysicsConstants.SpecificHeatRatio - 1f) / PhysicsConstants.SpecificHeatRatio;

    public ICharger Charger { get; }

    public Settings Tuning { get; }

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

    /// <summary>Temperature [K] of the cylinder walls.</summary>
    public float CylinderTempK { get; private set; }

    /// <summary>Temperature [K] of the engine block and oil.</summary>
    public float EngineTempK { get; private set; }

    /// <summary>Thermostat opening fraction [0..1]: 1 = fully open.</summary>
    public float ThermostatOpen { get; private set; }

    /// <summary>Air-limited combustion efficiency factor [0..1].</summary>
    public float BurnFractionAir { get; private set; }

    /// <summary>Temperature-limited combustion efficiency factor [0..1].</summary>
    public float BurnFractionTemp { get; private set; }

    /// <summary>Combined combustion efficiency factor [0..1].</summary>
    public float BurnFraction { get; private set; }

    /// <summary>True on the tick where the charger reported a surge. Reset on the next tick.</summary>
    public bool SurgeThisTick { get; private set; }

    public CombustionModel(Func<float> governorNorm, Func<float> fuelNorm, Func<float> rpmNorm,
        Func<float> ambientTemperatureK, ICharger charger, Settings settings, ExhaustVelocitySettings velocity)
    {
        Charger = charger ?? throw new ArgumentNullException(nameof(charger));
        Tuning = settings ?? throw new ArgumentNullException(nameof(settings));
        _velocity = velocity ?? throw new ArgumentNullException(nameof(velocity));
        _governorNorm = governorNorm ?? throw new ArgumentNullException(nameof(governorNorm));
        _fuelNorm = fuelNorm ?? throw new ArgumentNullException(nameof(fuelNorm));
        _rpmNorm = rpmNorm ?? throw new ArgumentNullException(nameof(rpmNorm));
        _ambientTemperatureK = ambientTemperatureK ?? throw new ArgumentNullException(nameof(ambientTemperatureK));

        var ambient = Mathf.Max(1f, _ambientTemperatureK());
        CylinderTempK = ambient;
        EngineTempK = ambient;
    }

    /// <summary>
    /// Advances the simulation by <paramref name="delta"/> seconds
    /// </summary>
    public void Tick(float delta, bool engineOn)
    {
        var governor = Mathf.Clamp01(_governorNorm());
        var fuel = Mathf.Clamp01(_fuelNorm());
        var rpm = Mathf.Clamp01(_rpmNorm());

        var s = Tuning;

        var fuelPerStroke = fuel / Mathf.Max(0.01f, rpm);

        var ambient = Mathf.Max(1f, _ambientTemperatureK());
        var lastExhaustEnergy = ExhaustEnergy;

        GovernorNorm = governor;
        FuelNorm = fuel;
        RpmNorm = rpm;
        FuelPerStroke = fuelPerStroke;
        Charge = Charger.Charge;

        var calibration = Charger.LambdaCalibration;
        Lambda = Charge / (calibration * Mathf.Max(0.01f, fuelPerStroke));

        Overfuel = Mathf.Max(0f, fuelPerStroke - Charge / calibration);

        // combustion heat warms up the cylinders, the block cools the cylinders
        var cylinderTarget = EngineTempK + s.CylinderGainK * lastExhaustEnergy;
        CylinderTempK += (cylinderTarget - CylinderTempK) * (1f - Mathf.Exp(-delta / s.TauCylinder));

        // the thermostat governs engine block cooling rate
        ThermostatOpen = Smoothstep(Settings.ThermostatOpenK, Settings.ThermostatFullOpenK, EngineTempK);
        var cooldownTau = Mathf.Lerp(s.TauCooldownClosed, s.TauCooldownOpen, ThermostatOpen);

        // hot cylinders warm up the block, heat flows out of the block into the environment
        var engineTau = (s.TauEngine * cooldownTau) / (s.TauEngine + cooldownTau);
        var engineTarget = (CylinderTempK * cooldownTau + ambient * s.TauEngine)
                           / (s.TauEngine + cooldownTau);
        EngineTempK += (engineTarget - EngineTempK) * (1f - Mathf.Exp(-delta / engineTau));

        // heavy overfueling reduces combustion efficiency.
        // simplified model for now; in reality, burn fraction already starts dropping before lambda reaches 1, as
        // non-perfect mixing causes local rich pockets to start to form in the cylinder even while global lambda is still lean
        BurnFractionAir = Mathf.Min(1f, Lambda);
        // cold cylinder walls also reduce combustion efficiency
        BurnFractionTemp = Mathf.Lerp(s.MinBurnFractionAtCold, 1f,
            Smoothstep(s.ColdWallFloorK, s.WarmWallTargetK, CylinderTempK));
        BurnFraction = BurnFractionAir * BurnFractionTemp; 

        MassFlow = Charge * rpm; // fuel mass is so small relative to air mass as to be negligible

        // dynamically derive temperature gain based on rated exhaust temperature at load.
        // this makes it easier to alter temperature behaviour of the engine: just say what the rated exhaust gas
        // temperature at exhaust mouth should be, and the rest follows.
        var tempGainK = (s.RatedExhaustTempK - PhysicsConstants.ReferenceAmbientK) * Charger.ChargeAtFullPower;

        var manifoldTemperature = ambient + tempGainK * fuelPerStroke * BurnFraction / Charge;
        var turbinePressureRatio = Mathf.Max(1f, Charge);
        TurbineTemperatureDropK = manifoldTemperature *
                                  (1f - Mathf.Pow(turbinePressureRatio, -IsentropicExponent));

        GasTemperature = manifoldTemperature - TurbineTemperatureDropK;
        GasDensity = PhysicsConstants.ReferenceAirDensity * ambient / Mathf.Max(1f, GasTemperature);
        ExhaustEnergy = MassFlow * (manifoldTemperature - ambient) / tempGainK;
        ExhaustVelocity = _velocity.Calculate(MassFlow, GasDensity);

        Charger.Tick(delta, Overfuel, rpm, governor, ExhaustEnergy, engineOn);

        Boost = Charger.Boost;
        SurgeThisTick = Charger.Surging;
    }

    private static float Smoothstep(float edge0, float edge1, float x)
    {
        var t = Mathf.Clamp01(Mathf.InverseLerp(edge0, edge1, x));
        return t * t * (3f - 2f * t);
    }
}
