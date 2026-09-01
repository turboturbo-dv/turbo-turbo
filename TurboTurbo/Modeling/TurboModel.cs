using System;

namespace TurboTurbo.Modeling;

/// <summary>
/// Pure mathematical model of a turbocharged diesel engine and its turbocharger.
///
/// Completely independent of the game: throttle and engine speed enter as
/// supplier functions (wired to the game's ports by the adapter), configuration
/// is fed in via <see cref="Settings"/>, and <see cref="Tick"/> advances the
/// simulation one step.
///
/// Physics summary (per tick):
/// 1. charge      - per-stroke cylinder air index; 1.0 = naturally aspirated,
///                  rises with boost (turcharged air density).
/// 2. lambda      - air-fuel ratio proxy = charge / (calibration x fuel flow).
///                  Below SmokeOnsetLambda the engine overfuels and soots.
/// 3. smoke       - 0..1 smoke density from the lambda deficit.
/// 4. torque cap  - fuel flow is capped by available air (charge), blended
///                  with engine speed via RpmTorqueExponent. Below
///                  TorqueLambdaFloor extra fuel contributes no torque.
/// 5. boost       - first-order lag towards an equilibrium set by exhaust
///                  energy (fuel x rpm mass flow). Overfueling shortens
///                  spool-up (thermal enthalpy feedback).
/// </summary>
public sealed class TurboModel
{
    /// <summary>Configuration constants. Defaults match the shipped tuning.</summary>
    public sealed class Settings
    {
        /// <summary>Per-stroke charge index of naturally-aspirated operation (zero boost).</summary>
        public float AirNAFraction { get; set; } = 0.55f;

        /// <summary>Air-to-fuel calibration constant: full boost + full rack is exactly clean at 2.5.</summary>
        public float LambdaCalibration { get; set; } = 2.5f;

        /// <summary>Boost multiplier on top of NA charge at full boost (charge = NA + (1-NA) x (1 + k x boost)).</summary>
        public float BoostChargeMultiplier { get; set; } = 2.5f;

        /// <summary>Lambda where soot formation begins (fueling above this vs air is overfueling).</summary>
        public float SmokeOnsetLambda { get; set; } = 0.85f;

        /// <summary>Lambda where smoke reaches full opacity.</summary>
        public float SmokeOpaqueLambda { get; set; } = 0.45f;

        /// <summary>0 = torque cap is pure per-stroke charge, 1 = strict airflow on top.</summary>
        public float RpmTorqueExponent { get; set; } = 0f;

        /// <summary>Exponent bounding the boost equilibrium: exhaust mass flow scales with engine speed.</summary>
        public float RpmBoostExponent { get; set; } = 1.2f;

        /// <summary>Seconds of spool-up time constant (clean combustion).</summary>
        public float TauUp { get; set; } = 3.0f;

        /// <summary>Seconds of blow-down (boost release) time constant.</summary>
        public float TauDown { get; set; } = 1.0f;

        /// <summary>Floor for the spool-up time constant (stability under heavy overfuel).</summary>
        public float MinSpoolTau { get; set; } = 0.5f;

        /// <summary>Thermal enthalpy feedback strength: overfueling shortens spool-up time.</summary>
        public float ThermalK { get; set; } = 0.8f;

        /// <summary>Lambda below which extra fuel contributes no torque.</summary>
        public float TorqueLambdaFloor { get; set; } = 0.7f;
    }

    private readonly Settings _settings;
    private readonly Func<float> _throttle;
    private readonly Func<float> _rpmNorm;

    private float _boost;
    private float _prevDemand;

    /// <summary>Current turbo boost pressure ratio [0..1].</summary>
    public float Boost { get; private set; }

    /// <summary>Raw clamped throttle demand read this tick, before the
    /// torque cap. Downstream exhaust behaviour (wet stacking) keys off
    /// this, not EffectiveDemand.</summary>
    public float Demand { get; private set; }

    /// <summary>Clamped normalized engine speed read this tick.</summary>
    public float RpmNorm { get; private set; }

    /// <summary>Instantaneous exhaust-gas energy proxy [0..1]: the
    /// equilibrium target the boost lag chases (fuel demand x rpm mass
    /// flow). Exhaust temperature follows combustion immediately - only
    /// the turbo lags - so plume effects key off this, not Boost.</summary>
    public float ExhaustHeat { get; private set; }

    /// <summary>Per-stroke cylinder charge index (1.0 = naturally aspirated).</summary>
    public float Charge { get; private set; }

    /// <summary>Air-fuel ratio proxy (calibrated: ~1.0 = edge of clean full load).</summary>
    public float Lambda { get; private set; }

    /// <summary>Overfueling amount [0..1] - fuel beyond available air.</summary>
    public float Overfuel { get; private set; }

    /// <summary>Smoke density [0..1] from the lambda deficit.</summary>
    public float SmokeDensity { get; private set; }

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
    /// Advances the simulation one tick.
    /// </summary>
    /// <param name="delta">Simulation time step [s].</param>
    /// <param name="fuelNorm">Normalized fuel consumption [0..1] this tick.</param>
    /// <param name="engineOn">Whether the engine is currently combusting.</param>
    public void Tick(float delta, float fuelNorm, bool engineOn)
    {
        float demand = Clamp(_throttle(), 0f, 1f);
        float rpmNorm = Clamp(_rpmNorm(), 0f, 1f);
        Demand = demand;
        RpmNorm = rpmNorm;

        // gate all combustion on the engine's own running state: the layshaft
        // port reads ~1.0 with the engine shut down
        float fuelDemand = engineOn ? demand : 0f;

        // 1. per-stroke cylinder charge index: 1.0 = naturally aspirated,
        //    full boost = NA + (1-NA) x (1 + BoostChargeMultiplier)
        var s = _settings;
        Charge = s.AirNAFraction + (1f - s.AirNAFraction) * (1f + s.BoostChargeMultiplier * _boost);

        // 2. per-stroke air-fuel ratio proxy
        Lambda = Charge / (s.LambdaCalibration * Math.Max(0.01f, fuelDemand));

        // 3. smoke density from the lambda deficit
        SmokeDensity = fuelDemand < 0.02f
            ? 0f
            : Clamp((s.SmokeOnsetLambda - Lambda) / (s.SmokeOnsetLambda - s.SmokeOpaqueLambda), 0f, 1f);

        // 4. torque cap: per-stroke charge sets usable work, blended with
        //    engine speed via RpmTorqueExponent. Between SmokeOnsetLambda and
        //    TorqueLambdaFloor the engine still pulls hard - it just smokes -
        //    which keeps a lugging engine from stalling.
        float rpmFactor = Clamp(rpmNorm, 0f, 1f);
        rpmFactor = (float)Math.Pow(rpmFactor, s.RpmTorqueExponent);
        float fuelMaxTorque = rpmFactor * Charge
                              / (s.LambdaCalibration * s.TorqueLambdaFloor);
        EffectiveDemand = Math.Min(fuelDemand, fuelMaxTorque);

        // 5. overfueling: fuel beyond available air
        Overfuel = Math.Max(0f, fuelDemand - Charge / s.LambdaCalibration);

        // 6. boost: first-order lag towards an equilibrium ceiling set by
        //    exhaust mass flow (fuel x rpm). Spool mode keys off demand (not
        //    target) so a lug-driven ceiling drop eases boost down with
        //    turbine inertia instead of blowing it off. Thermal enthalpy
        //    feedback: overfueling shortens spool-up time.
        float rpmMassFlow = (float)Math.Pow(Clamp(rpmNorm, 0f, 1f), s.RpmBoostExponent);
        float target = Clamp(fuelDemand, 0f, 1f) * rpmMassFlow;
        ExhaustHeat = target;
        float tau = fuelDemand > _boost
            ? Math.Max(s.MinSpoolTau, s.TauUp / (1f + s.ThermalK * Overfuel))
            : s.TauDown;
        _boost += (target - _boost) * (1f - (float)Math.Exp(-delta / tau));
        Boost = _boost;

        // 7. surge detection: sharp demand drop while boost is high
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
