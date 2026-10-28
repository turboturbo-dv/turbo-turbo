using System;

using UnityEngine;

namespace TurboTurbo.Modeling;

/// <summary>
/// Self-contained model of a diesel engine. This is intended to clip on to the game's DieselEngineDirect.
/// We don't modify or replace it in any way, as that would be too fragile. For our purposes, it is perfectly
/// fine to just read what the engine does, and derive what the combustion in that engine would be doing.
///
/// Which is what we do here, accounting for fuel, rpm, air charge, and various temperatures in order to
/// figure out what is happening in the engine. Outputs drive the visual effects.
/// </summary>
public sealed class CombustionModel
{
    /// <summary>Configuration constants for the combustion model. Defaults give a reasonable starting point.</summary>
    public sealed class Settings
    {
        internal const float DefaultRatedExhaustTempK = 760f;
        internal const float DefaultTauChamber = 10f;
        internal const float DefaultTauEngine = 650f;
        internal const float DefaultTauCooldownOpen = 65f;
        internal const float DefaultTauCooldownClosed = 1300f;
        internal const float DefaultChamberGainK = 220f;
        internal const float DefaultPolytropicExponent = 1.34f;
        internal const float DefaultIgnitionTempK = 580f;
        internal const float DefaultIgnitionSpread = 1.5f;
        internal const float DefaultLateBurnWork = 0.5f;
        internal const float DefaultQuenchFraction = 0.15f;
        internal const float DefaultCrankingFuelPerStroke = 0.25f;

        /// <summary>Block temperature [K] where the radiator thermostat begins to open.</summary>
        public const float ThermostatOpenK = 65f + PhysicsConstants.KelvinOffset;

        /// <summary>Block temperature [K] where the radiator thermostat is fully open.</summary>
        public const float ThermostatFullOpenK = 85f + PhysicsConstants.KelvinOffset;

        /// <summary>
        /// Rated exhaust gas temperature [K] in the exhaust manifold at full power.
        /// Note that this is a target value; transient conditions may overshoot it.
        /// </summary>
        public float RatedExhaustTempK { get; set; } = DefaultRatedExhaustTempK;
        public float TauChamber { get; set; } = DefaultTauChamber;
        public float TauEngine { get; set; } = DefaultTauEngine;
        public float TauCooldownOpen { get; set; } = DefaultTauCooldownOpen;
        public float TauCooldownClosed { get; set; } = DefaultTauCooldownClosed;
        public float ChamberGainK { get; set; } = DefaultChamberGainK;
        public float PolytropicExponent { get; set; } = DefaultPolytropicExponent;
        public float IgnitionTempK { get; set; } = DefaultIgnitionTempK;
        public float IgnitionSpread { get; set; } = DefaultIgnitionSpread;
        public float LateBurnWork { get; set; } = DefaultLateBurnWork;
        public float QuenchFraction { get; set; } = DefaultQuenchFraction;

        /// <summary>
        /// Fuel injected per stroke while the starter cranks the engine [0..1].
        /// DV injects no fuel while starting the engine. We do want to inject fuel because poor combustion neatly
        /// falls out of our model on these conditions, resulting in the diesel vapour you get on an engine start.
        /// So to account for this, we override fuel/stroke to this value while the engine is starting.
        /// </summary>
        public float CrankingFuelPerStroke { get; set; } = DefaultCrankingFuelPerStroke;

        public Settings()
        {
        }

        public Settings(Settings other)
        {
            RatedExhaustTempK = other.RatedExhaustTempK;
            TauChamber = other.TauChamber;
            TauEngine = other.TauEngine;
            TauCooldownOpen = other.TauCooldownOpen;
            TauCooldownClosed = other.TauCooldownClosed;
            ChamberGainK = other.ChamberGainK;
            PolytropicExponent = other.PolytropicExponent;
            IgnitionTempK = other.IgnitionTempK;
            IgnitionSpread = other.IgnitionSpread;
            LateBurnWork = other.LateBurnWork;
            QuenchFraction = other.QuenchFraction;
            CrankingFuelPerStroke = other.CrankingFuelPerStroke;
        }

        public void Validate()
        {
            RatedExhaustTempK = Mathf.Max(PhysicsConstants.ReferenceAmbientK + 0.01f, RatedExhaustTempK);
            TauChamber = Mathf.Max(0.01f, TauChamber);
            TauEngine = Mathf.Max(TauChamber + 0.01f, TauEngine);
            TauCooldownOpen = Mathf.Max(0.01f, TauCooldownOpen);
            TauCooldownClosed = Mathf.Max(TauCooldownOpen, TauCooldownClosed);
            ChamberGainK = Mathf.Max(0.01f, ChamberGainK);
            PolytropicExponent = Mathf.Clamp(PolytropicExponent, 1.01f, PhysicsConstants.SpecificHeatRatio);
            IgnitionTempK = Mathf.Max(1f, IgnitionTempK);
            IgnitionSpread = Mathf.Clamp(IgnitionSpread, 0.05f, 5f);
            LateBurnWork = Mathf.Clamp01(LateBurnWork);
            QuenchFraction = Mathf.Clamp01(QuenchFraction);
            CrankingFuelPerStroke = Mathf.Clamp01(CrankingFuelPerStroke);
        }
    }

    private readonly ExhaustVelocitySettings _velocity;

    private readonly Func<float> _governorNorm;
    private readonly Func<float> _fuelNorm;
    private readonly Func<float> _rpmNorm;
    private readonly Func<float> _ambientTemperatureK;

    // the turbine extracts this fraction of the isentropic temperature drop
    private const float TurbineEfficiency = 0.78f;

    // a normal burn sends about 40% of the fuel's energy to the crankshaft and 30% to the exhaust. Work that a late
    // burn fails to produce mostly ends up in the exhaust instead, scaled by this ratio.
    private const float WorkToExhaustRatio = 0.4f / 0.3f;

    // this isnt' quite in accordance with Woschni's model, but it accounts for the fact that we're lumping
    // everything into a single node, which is already plenty of detail for our purposes
    private const float WoschniChargeExponent = 0.5f;
    private const float WoschniRelExponent = -0.2f;

    // weight of the end-of-compression temperature in the mean gas temperature
    private const float GasMeanCompressionWeight = 0.5f;

    // wall temperature drop below ThermostatFullOpenK at which the cold wall exponent drop fully applies
    private const float ColdWallSpanK = 100f;

    // how quickly the polytropic exponent falls towards 1 as the engine slows down, from heat loss and leakage
    // during the longer compression stroke
    private const float LowSpeedCompressionLoss = 0.035f;
    // drop of the polytropic exponent with fully cold combustion chamber surfaces
    internal const float ColdWallExponentDrop = 0.05f;
    // fraction of the wall-to-intake temperature difference the charge gains during induction
    private const float InductionWallHeating = 0.06f;
    // fraction of the trapped charge that is leftover exhaust gas
    private const float ResidualGasFraction = 0.04f;

    private const float WallConductanceCeiling = 0.95f;

    // ignition delay follows Wolfer's correlation: tau ~ p^-1.19 * exp(4650 / T)
    private const float IgnitionActivationK = 4650f;
    private const float IgnitionPressureExponent = 1.19f;
    // reference conditions for IgnitionTempK; engine idle
    private const float IgnitionReferenceRpm = 0.3f;
    private const float IgnitionReferencePressure = 30f;
    private const float MinIgnitionRpm = 0.01f;
    // a faster stroke leaves less time for ignition, but also brings more turbulence and higher injection pressure.
    // an exponent below linear (to 1/rpm) is defensible for our model
    private const float IgnitionSpeedExponent = 0.5f;

    public ICharger Charger { get; }

    public Settings Tuning { get; }

    /// <summary>Current charger boost pressure ratio [0..1], 0 when naturally aspirated.</summary>
    public float Boost { get; private set; }

    /// <summary>Raw clamped throttle (the game's governor output) read this tick.</summary>
    public float GovernorNorm { get; private set; }

    /// <summary>Raw clamped fuel consumption per unit time read this tick.</summary>
    public float FuelNorm { get; private set; }

    /// <summary>Normalized engine speed read this tick.</summary>
    public float RpmNorm { get; private set; }

    /// <summary>Fuel consumption per stroke, derived from <c>fuelNorm / rpm</c></summary>
    public float FuelPerStroke { get; private set; }

    /// <summary>
    /// Fuel actually injected per stroke [0..1]. Because we don't actually write-back torque to the game,
    /// DV's governor won't adjust fuel/stroke to account for our lower torque. We use a simplified simulation
    /// of what the governor would do, as we only care about the visual effects.
    /// </summary>
    public float InjectedPerStroke { get; private set; }

    /// <summary>Work produced per unit of injected fuel, relative to a clean burn [0..1].</summary>
    public float WorkEfficiency { get; private set; }

    /// <summary>Fuel that burns late in the stroke, per unit time, on the same scale as <see cref="FuelNorm"/>.</summary>
    public float LateBurnedFuel { get; private set; }

    /// <summary>Fuel that never burns, per unit time, on the same scale as <see cref="FuelNorm"/>.</summary>
    public float UnburnedFuel { get; private set; }

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

    /// <summary>
    /// Temperature [K] of the combustion chamber surfaces, these react quickly to power changes and
    /// are used to determine whether injected fuel condenses or evaporates.
    /// </summary>
    public float ChamberTempK { get; private set; }

    /// <summary>
    /// Temperature [K] of the engine block and oil.
    /// The block feeds and sinks heat to/from the chamber surfaces and gives us the slow response
    /// that prevents an idling engine from cooling too quickly.
    /// </summary>
    public float EngineTempK { get; private set; }

    /// <summary>Intake charge temperature [K] after compressor heating and manifold pickup.</summary>
    public float IntakeTempK { get; private set; }

    /// <summary>Effective polytropic exponent of the current compression stroke.</summary>
    public float CompressionExponent { get; private set; }

    /// <summary>End-of-compression gas temperature [K].</summary>
    public float CompressionTempK { get; private set; }

    /// <summary>End-of-compression pressure [bar].</summary>
    public float CompressionPressure { get; private set; }

    /// <summary>
    /// Log ratio of the time available for ignition to the ignition delay. At 0, half of the fuel ignites in time.
    /// </summary>
    public float IgnitionMargin { get; private set; }

    /// <summary>Exhaust manifold temperature [K], also used as the next tick's residual gas temperature.</summary>
    public float ManifoldTempK { get; private set; }

    /// <summary>Thermostat opening fraction [0..1]: 1 = fully open.</summary>
    public float ThermostatOpen { get; private set; }

    /// <summary>Air-limited combustion efficiency factor [0..1].</summary>
    public float BurnFractionAir { get; private set; }

    /// <summary>Fraction of the injected fuel that ignites in time [0..1]. Most of the rest still burns late.</summary>
    public float BurnFractionTemp { get; private set; }

    /// <summary>Combined combustion efficiency factor [0..1].</summary>
    public float BurnFraction { get; private set; }

    /// <summary>True on the tick where the charger reported a surge. Reset on the next tick.</summary>
    public bool SurgeThisTick { get; private set; }

    public CombustionModel(Func<float> governorNorm, Func<float> fuelNorm, Func<float> rpmNorm,
        Func<float> ambientTemperatureK, ICharger charger, Settings settings, ExhaustVelocitySettings velocity,
        float? initialTemperatureK = null)
    {
        Charger = charger ?? throw new ArgumentNullException(nameof(charger));
        Tuning = settings ?? throw new ArgumentNullException(nameof(settings));
        _velocity = velocity ?? throw new ArgumentNullException(nameof(velocity));
        _governorNorm = governorNorm ?? throw new ArgumentNullException(nameof(governorNorm));
        _fuelNorm = fuelNorm ?? throw new ArgumentNullException(nameof(fuelNorm));
        _rpmNorm = rpmNorm ?? throw new ArgumentNullException(nameof(rpmNorm));
        _ambientTemperatureK = ambientTemperatureK ?? throw new ArgumentNullException(nameof(ambientTemperatureK));

        var initial = Mathf.Max(1f, initialTemperatureK ?? _ambientTemperatureK());
        ChamberTempK = initial;
        EngineTempK = initial;
        // the first tick reads this as the residual gas temperature
        ManifoldTempK = initial;
    }

    /// <summary>
    /// Advances the simulation by <paramref name="delta"/> seconds
    /// </summary>
    public void Tick(float delta, bool engineOn, bool cranking = false)
    {
        var governor = Mathf.Clamp01(_governorNorm());
        var fuel = Mathf.Clamp01(_fuelNorm());
        var rpm = Mathf.Clamp01(_rpmNorm());

        var s = Tuning;

        // the game only starts fueling once the engine has caught, but a real engine is fueled while cranking.
        // max() keeps the handover seamless in case there is an overlap
        if (cranking) fuel = Mathf.Max(fuel, s.CrankingFuelPerStroke * rpm);

        var fuelPerStroke = fuel / Mathf.Max(0.01f, rpm);

        var ambient = Mathf.Max(1f, _ambientTemperatureK());

        GovernorNorm = governor;
        FuelNorm = fuel;
        RpmNorm = rpm;
        FuelPerStroke = fuelPerStroke;
        Charge = Charger.Charge;
        MassFlow = Charge * rpm; // fuel mass is so small relative to air mass as to be negligible

        var ratedCharge = Charger.RatedCharge;
        // mass flow peaks at rated speed for both chargers, even though charge drops slightly
        // with RPM on natural aspiration
        var flowNorm = MassFlow / ratedCharge;

        // the intake warms up inflowing air though compression and/or heat exchange
        IntakeTempK = IntakeTemperature(ambient, EngineTempK, Charge, flowNorm);
        // intake air mixes with residual gases and exchanges heat with the combustion chamber
        var trappedTemperature = TrappedTemperature(IntakeTempK, ChamberTempK, ManifoldTempK);
        // the mixture is then compressed, heating it up to the ignition point
        CompressionExponent = EffectiveExponent(rpm, ChamberTempK);
        CompressionTempK = CompressionTemperature(trappedTemperature, CompressionExponent);
        CompressionPressure = Charge * Mathf.Pow(Charger.CompressionRatio, CompressionExponent);

        // fuel only burns if it ignites before expansion cools the charge too far
        IgnitionMargin = IgnitionMarginAt(rpm);
        // this should more realistically be a logistic curve, but smoothstep removes the tails, which
        // is easier for us to deal with right now as we have no good way to render slight fuel inclusions
        // in our current model
        BurnFractionTemp = MathExt.Smoothstep(-s.IgnitionSpread, s.IgnitionSpread, IgnitionMargin);

        // fuel that misses the ignition window mostly burns late in the expansion stroke, producing little work and
        // sending its heat into the exhaust. Only the part quenched against the chamber surfaces never burns.
        var missed = 1f - BurnFractionTemp;
        var lateShare = missed * (1f - s.QuenchFraction);
        WorkEfficiency = BurnFractionTemp + lateShare * s.LateBurnWork;

        // this is the bit that DV's governor would fix for us if we were to feed limited torque back into the game.
        // since we currently don't do this, we will pretend that more fuel is injected if work efficiency falls, so
        // that the effects match what the engine should be doing.
        var injectedPerStroke = cranking
            ? fuelPerStroke
            : Mathf.Max(fuelPerStroke, Mathf.Min(1f, fuelPerStroke / Mathf.Max(0.01f, WorkEfficiency)));
        var lateBurnedPerStroke = injectedPerStroke * lateShare;
        var burnedPerStroke = injectedPerStroke * BurnFractionTemp + lateBurnedPerStroke;
        InjectedPerStroke = injectedPerStroke;
        LateBurnedFuel = lateBurnedPerStroke * rpm;
        UnburnedFuel = injectedPerStroke * missed * s.QuenchFraction * rpm;

        // all burned fuel consumes air, whether it burns in time or late
        var calibration = Charger.LambdaCalibration;
        Lambda = Charge / (calibration * Mathf.Max(0.01f, burnedPerStroke));
        Overfuel = Mathf.Max(0f, burnedPerStroke - Charge / calibration);

        // heavy overfueling reduces combustion efficiency.
        // simplified model for now; in reality, burn fraction already starts dropping before lambda reaches 1, as
        // non-perfect mixing causes local rich pockets to start to form in the cylinder even while global lambda is
        // still lean. we do not have a way to model this yet.
        BurnFractionAir = Mathf.Min(1f, Lambda);
        BurnFraction = BurnFractionAir * BurnFractionTemp;

        // dynamically derive temperature gain based on rated exhaust temperature at load.
        // this makes it easier to alter temperature behaviour of the engine: just say what the rated exhaust gas
        // temperature at exhaust manifold should be, and the rest follows.
        var tempGainK = (s.RatedExhaustTempK - PhysicsConstants.ReferenceAmbientK) * ratedCharge;
        // late-burned fuel also sends the work it fails to produce into the exhaust
        var exhaustHeatPerStroke = burnedPerStroke + lateBurnedPerStroke * (1f - s.LateBurnWork) * WorkToExhaustRatio;
        var combustionRiseK = tempGainK * exhaustHeatPerStroke * BurnFractionAir / Charge;

        // the wall sits at a conductance-weighted mean of the cycle-mean gas temperature and the block
        var gasMeanK = GasMeanTemperature(trappedTemperature, CompressionTempK, combustionRiseK);
        // there is really no need to recompute this every frame, but that requires invalidation when settings change.
        // in practice, it's just a handful of ops * locos / frame, so it's not a big deal to recompute
        var wallRatio = WallConductanceRatio(ratedCharge);
        var wallConductance = rpm > 0f
            ? wallRatio * Mathf.Pow(flowNorm, WoschniChargeExponent)
            : 0f;
        var chamberTarget = (wallConductance * gasMeanK + EngineTempK) / (wallConductance + 1f);
        ChamberTempK += (chamberTarget - ChamberTempK) * (1f - Mathf.Exp(-delta / s.TauChamber));

        // the thermostat governs engine block cooling rate
        ThermostatOpen = MathExt.Smoothstep(Settings.ThermostatOpenK, Settings.ThermostatFullOpenK, EngineTempK);
        var cooldownTau = Mathf.Lerp(s.TauCooldownClosed, s.TauCooldownOpen, ThermostatOpen);

        // the hot chamber warms up the block; heat flows out of the block into the environment
        var engineTau = (s.TauEngine * cooldownTau) / (s.TauEngine + cooldownTau);
        var engineTarget = (ChamberTempK * cooldownTau + ambient * s.TauEngine)
                           / (s.TauEngine + cooldownTau);
        EngineTempK += (engineTarget - EngineTempK) * (1f - Mathf.Exp(-delta / engineTau));

        // hot exhaust gas drives the turbine
        ManifoldTempK = ambient + combustionRiseK;

        // exhaust gas has a slightly lower isentropic exponent, but for our model, air is fine
        var isentropicDrop =
            1f - Mathf.Pow(Charger.TurbinePressureRatio, -PhysicsConstants.IsentropicTemperatureExponent);
        TurbineTemperatureDropK = ManifoldTempK * TurbineEfficiency * isentropicDrop;

        GasTemperature = ManifoldTempK - TurbineTemperatureDropK;
        GasDensity = PhysicsConstants.ReferenceAirDensity * ambient / Mathf.Max(1f, GasTemperature);
        ExhaustEnergy = MassFlow * (ManifoldTempK - ambient) / tempGainK;
        ExhaustVelocity = _velocity.Calculate(MassFlow, GasDensity);

        // charger always lags 1 tick behind the combustion model, as it depends on exhaust
        Charger.Tick(delta, rpm, governor, ExhaustEnergy, engineOn);

        Boost = Charger.Boost;
        SurgeThisTick = Charger.Surging;
    }

    /// <summary>Instantly sets the block and combustion chamber to the given temperature.</summary>
    public void SetTemperatureK(float kelvin)
    {
        var k = Mathf.Max(1f, kelvin);
        ChamberTempK = k;
        EngineTempK = k;
    }

    private float IntakeTemperature(float ambientK, float blockK, float charge, float flowNorm)
    {
        var s = Tuning;
        // air heats up from compression
        var pressureRatio = Mathf.Max(1f, charge);
        
        // a perfectly efficient compressor would heat air by a factor of ratio^exponent
        var isentropicRise = Mathf.Pow(pressureRatio, PhysicsConstants.IsentropicTemperatureExponent) - 1f;
        var compressorK = ambientK * (1f + isentropicRise / Charger.CompressorEfficiency);

        // and exchanges heat with the manifold
        var flow = Mathf.Max(1e-3f, flowNorm);
        var effectiveness = 1f - Mathf.Exp(-Charger.IntakeNtu * Mathf.Pow(flow, WoschniRelExponent));

        return compressorK + effectiveness * (blockK - compressorK);
    }

    private static float TrappedTemperature(float intakeK, float wallK, float residualK)
    {
        // air exchanges heat with the chamber surfaces
        var warmed = intakeK + InductionWallHeating * (wallK - intakeK);
        // and mixes with the residual gases
        return (1f - ResidualGasFraction) * warmed + ResidualGasFraction * residualK;
    }

    private float EffectiveExponent(float rpm, float wallK)
    {
        var s = Tuning;
        // a slower stroke gives the charge more time to lose heat to the walls and leak past the rings, so the
        // exponent falls towards 1 as the engine slows to a standstill
        var k = LowSpeedCompressionLoss;
        var speedFactor = (1f + k) / (1f + k / Mathf.Max(1e-3f, rpm));
        // cold walls take more heat out of the charge
        var coldness = Mathf.Clamp01((Settings.ThermostatFullOpenK - wallK) / ColdWallSpanK);
        var exponent = 1f + (s.PolytropicExponent - 1f) * speedFactor - ColdWallExponentDrop * coldness;
        return Mathf.Max(1f, exponent);
    }

    private float IgnitionMarginAt(float rpm)
    {
        // log of (time available / ignition delay), relative to the reference conditions
        return IgnitionActivationK * (1f / Tuning.IgnitionTempK - 1f / Mathf.Max(1f, CompressionTempK))
               - IgnitionSpeedExponent * Mathf.Log(Mathf.Max(MinIgnitionRpm, rpm) / IgnitionReferenceRpm)
               + IgnitionPressureExponent * Mathf.Log(Mathf.Max(0.01f, CompressionPressure) / IgnitionReferencePressure);
    }

    private float CompressionTemperature(float trappedK, float exponent)
        => trappedK * Mathf.Pow(Charger.CompressionRatio, exponent - 1f);

    private static float GasMeanTemperature(float trappedK, float compressionK, float riseK)
        => trappedK + GasMeanCompressionWeight * (compressionK - trappedK) + riseK;

    private float WallConductanceRatio(float chargeFull)
    {
        var s = Tuning;
        var blockK = Settings.ThermostatFullOpenK;
        var wallK = blockK + s.ChamberGainK;
        var riseK = s.RatedExhaustTempK - PhysicsConstants.ReferenceAmbientK;
        var intakeK = IntakeTemperature(PhysicsConstants.ReferenceAmbientK, blockK, chargeFull, 1f);
        var trappedK = TrappedTemperature(intakeK, wallK, s.RatedExhaustTempK);
        var compressionK = CompressionTemperature(trappedK, EffectiveExponent(1f, wallK));
        var gasMeanK = GasMeanTemperature(trappedK, compressionK, riseK);
        var x = Mathf.Clamp(s.ChamberGainK / Mathf.Max(0.01f, gasMeanK - blockK), 0f, WallConductanceCeiling);
        return x / (1f - x);
    }
}
