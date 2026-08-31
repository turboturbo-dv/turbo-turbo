using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using BepInEx.Logging;
using DV.Simulation.Cars;
using DV.Simulation.Controllers;
using DV.ThingTypes;
using HarmonyLib;
using LocoSim.Implementations;
using UnityEngine;

namespace TurboTurbo;

/// <summary>
/// Runtime turbocharger model for DieselEngineDirect locos.
/// Boost is a first-order lag on exhaust energy (fuel demand x rpm); the fuel
/// demand the engine sees is capped by available boost, fading in above idle.
/// </summary>
internal static class TurboModel
{
    internal static ManualLogSource Log;


    internal static bool SimActive => TurboConfig.SimEnabled.Value;

    private static readonly Dictionary<SimulationFlow, EngineTurbo> Turbos = new();

    /// <summary>Console access for state dumps.</summary>
    internal static System.Collections.Generic.IEnumerable<EngineTurbo> AllTurbos => Turbos.Values;

    internal static void HandleUpdate()
    {
        if (TurboConfig.SimToggleKey.Value.IsDown())
        {
            TurboConfig.SimEnabled.Value = !TurboConfig.SimEnabled.Value;
            string state = TurboConfig.SimEnabled.Value ? "ENABLED" : "DISABLED (vanilla engine behaviour)";
            Log.LogInfo($"turbo simulation {state}");
            Debug.Log($"[TurboTurbo] turbo simulation {state}");
        }

        foreach (EngineTurbo turbo in Turbos.Values)
        {
            turbo.UpdateFrame(Time.deltaTime);
        }
    }

    internal static bool IsTurboLoco(TrainCarType carType)
    {
        foreach (string name in TurboConfig.TurboLocos.Value.Split(','))
        {
            string trimmed = name.Trim();
            if (trimmed.Length == 0) continue;
            if (Enum.TryParse(trimmed, true, out TrainCarType type) && type == carType) return true;
        }
        return false;
    }

    internal static void Attach(TrainCar car, SimulationFlow flow)
    {
        if (car == null || flow == null) return;
        if (Turbos.ContainsKey(flow)) return;
        if (!IsTurboLoco(car.carType)) return;

        DieselEngineDirect engine = flow.OrderedSimComps.OfType<DieselEngineDirect>().FirstOrDefault();
        if (engine == null) return;

        PortReference throttleRef = engine.GetAllPortReferences()
            .FirstOrDefault(r => r.id.EndsWith(".THROTTLE", StringComparison.OrdinalIgnoreCase));
        Port throttlePort = throttleRef != null
            ? Traverse.Create(throttleRef).Field("port").GetValue<Port>()
            : null;
        Port rpmNormPort = engine.GetAllPorts()
            .FirstOrDefault(p => p.id.EndsWith(".RPM_NORMALIZED", StringComparison.OrdinalIgnoreCase));
        Port engineOnPort = engine.GetAllPorts()
            .FirstOrDefault(p => p.id.EndsWith(".ENGINE_ON", StringComparison.OrdinalIgnoreCase));
        Port fuelPort = engine.GetAllPorts()
            .FirstOrDefault(p => p.id.EndsWith(".FUEL_CONSUMPTION_NORMALIZED", StringComparison.OrdinalIgnoreCase));

        if (throttlePort == null || rpmNormPort == null)
        {
            Log.LogWarning($"could not resolve engine ports on {car.carType} [{car.ID}], turbo not attached");
            return;
        }

        Turbos[flow] = new EngineTurbo(car, flow, throttlePort, rpmNormPort, engineOnPort, fuelPort);
        car.OnDestroyCar += OnCarDestroyed;
        Turbos[flow].AttachAudio(new TurboWhineAudio(car, TurboAudio.CreateParams()));
        Turbos[flow].TryAttachSmoke(flow);
        Log.LogInfo($"turbo model attached to {car.carType} [{car.ID}] (fuel demand port: {throttlePort.id}, " +
                    $"fuel consumption port: {(fuelPort != null ? fuelPort.id : "MISSING")})");
    }

    private static void OnCarDestroyed(TrainCar car)
    {
        car.OnDestroyCar -= OnCarDestroyed;
        var match = Turbos.FirstOrDefault(t => t.Value.Car == car);
        if (match.Key != null)
        {
            match.Value.Destroy();
            Turbos.Remove(match.Key);
        }
    }

    internal static void TickFlow(SimulationFlow flow, float delta)
    {
        if (Turbos.TryGetValue(flow, out EngineTurbo turbo))
        {
            turbo.Tick(delta);
        }
    }
}

internal sealed class EngineTurbo
{
    internal TrainCar Car { get; }

    private readonly Port _throttlePort;
    private readonly Port _rpmNormPort;
    private readonly Port _engineOnPort;
    private readonly Port _fuelPort;
    private readonly SimulationFlow _flow;
    private TurboWhineAudio _whine;
    private readonly List<Inspectors.ParticleSystemInspector> _smoke = new();
    private bool _smokeAttached;
    private float _smokeRetryTimer;
    private float _boost;
    private float _demand;
    private float _rpmNorm;
    private float _fuelNorm;
    private float _prevDemand;
    private float _lastDebugLog;

    /// <summary>Smoke density [0..1] - phase 2: drives exhaust particles.</summary>
    internal float SmokeDensity { get; private set; }

    /// <summary>Whether the engine is currently combusting (gates exhaust).</summary>
    internal bool EngineRunning { get; private set; }

    /// <summary>Air-fuel ratio proxy (calibrated: ~1.0 = edge of clean full load).</summary>
    internal float Lambda { get; private set; }

    /// <summary>Overfueling amount [0..1] - fuel beyond available air.</summary>
    internal float Overfuel { get; private set; }

    internal EngineTurbo(TrainCar car, SimulationFlow flow, Port throttlePort, Port rpmNormPort, Port engineOnPort, Port fuelPort)
    {
        Car = car;
        _flow = flow;
        _throttlePort = throttlePort;
        _rpmNormPort = rpmNormPort;
        _engineOnPort = engineOnPort;
        _fuelPort = fuelPort;
    }

    internal void AttachAudio(TurboWhineAudio whine) => _whine = whine;

    /// <summary>
    /// Attaches the ParticleSystemInspector to the vanilla exhaust system.
    /// The vanilla exhaust is left fully untouched (its own port readers
    /// drive it) - we are observing its behavior in-game.
    /// </summary>
    internal void TryAttachSmoke(SimulationFlow flow)
    {
        if (_smokeAttached) return;
        var allPs = Car.GetComponentsInChildren<ParticleSystem>(true);
        var exhausts = allPs
            .Where(ps => ps.name == "ExhaustEngineSmoke")
            .ToList();
        if (exhausts.Count == 0) return;

        foreach (ParticleSystem ps in exhausts)
        {
            var inspector = new Inspectors.ParticleSystemInspector(ps) { Car = Car };
            _smoke.Add(inspector);
            HeatShimmer.Register(inspector);
        }

        _smokeAttached = true;
        TurboModel.Log.LogInfo($"particle inspector attached on [{Car.ID}] ({_smoke.Count} exhaust stack(s))");
    }

    internal void UpdateFrame(float frameDt)
    {
        if (!_smokeAttached)
        {
            if (TurboConfig.SmokeEnabled.Value)
            {
                _smokeRetryTimer += frameDt;
            if (_smokeRetryTimer > 2f)
            {
                _smokeRetryTimer = 0f;
                TryAttachSmoke(_flow);
            }
            }
        }
        else
        {
            Color smokeColor = TurboModel.SimActive ? SmokeColor : Color.clear;
            float smokeDensity = TurboModel.SimActive ? SmokeModelDensity : 0f;
            foreach (Inspectors.ParticleSystemInspector emitter in _smoke)
            {
                emitter.Update(_fuelNorm, EngineRunning);
            }
        }

        _whine?.UpdateFromModel(_boost, _demand, _rpmNorm, frameDt);
    }

    internal void Destroy()
    {
        foreach (Inspectors.ParticleSystemInspector emitter in _smoke)
        {
            emitter.Destroy();
        }
        _smoke.Clear();
        _whine?.Destroy();
        _whine = null;
    }

    internal string DumpState()
    {
        var smokeText = string.Join(", ", _smoke.Select(e => $"heat={e.HeatIntensity:0.000}"));
        return $"[{Car.ID}] engineOn={EngineRunning} fuelNorm={_fuelNorm:0.000} demand={_demand:0.000} " +
               $"rpmNorm={_rpmNorm:0.000} boost={_boost:0.000} lambda={Lambda:0.000} overfuel={Overfuel:0.000} " +
               $"smoke={SmokeDensity:0.000} {smokeText}";
    }

    private readonly ExhaustSmokeModel _smokeModel = new();

    /// <summary>Smoke appearance from the exhaust model (authoritative).</summary>
    internal Color SmokeColor { get; private set; } = Color.clear;

    /// <summary>Smoke density 0..1 from the exhaust model (authoritative).</summary>
    internal float SmokeModelDensity { get; private set; }

    internal void Tick(float delta)
    {
        float demand = _throttlePort.Value;
        float rpmNorm = _rpmNormPort.Value;

        // the layshaft port reads ~1.0 with the engine shut down - gate all
        // combustion effects on the engine's own running state
        bool engineOn = _engineOnPort != null ? _engineOnPort.Value > 0.5f : rpmNorm > 0.05f;
        EngineRunning = engineOn;
        float fuelDemand = engineOn ? demand : 0f;

        _demand = fuelDemand;
        _rpmNorm = rpmNorm;
        _fuelNorm = _fuelPort != null ? Mathf.Clamp01(_fuelPort.Value) : 0f;

        // per-stroke cylinder charge index: 1.0 = naturally aspirated,
        // 2.125 = full boost. This is the combustion-relevant air quantity.
        float charge = TurboConfig.AirNAFraction.Value
                       + (1f - TurboConfig.AirNAFraction.Value) * (1f + 2.5f * _boost);

        // per-stroke air-fuel ratio proxy and smoke density
        float lambda = charge / (TurboConfig.LambdaCalibration.Value * Mathf.Max(0.01f, fuelDemand));
        Lambda = lambda;
        SmokeDensity = fuelDemand < 0.02f
            ? 0f
            : Mathf.Clamp01((TurboConfig.SmokeOnsetLambda.Value - lambda)
                / (TurboConfig.SmokeOnsetLambda.Value - TurboConfig.SmokeOpaqueLambda.Value));

        // exhaust smoke model: color + density for the particle system
        _smokeModel.sootOnsetLambda = TurboConfig.SmokeOnsetLambda.Value;
        _smokeModel.sootOpaqueLambda = TurboConfig.SmokeOpaqueLambda.Value;
        _smokeModel.Update(lambda, fuelDemand, rpmNorm, engineOn, delta);
        SmokeColor = _smokeModel.Color;
        SmokeModelDensity = _smokeModel.Density;

        // torque cap: per-stroke charge sets usable work, blended with engine
        // speed via TurboConfig.RpmTorqueExponent (0 = pure per-stroke, 1 = strict airflow).
        // Between TurboConfig.SmokeOnsetLambda and TurboConfig.TorqueLambdaFloor the engine still pulls
        // hard - it just smokes - which keeps a lugging engine from stalling.
        float rpmFactor = Mathf.Pow(rpmNorm, TurboConfig.RpmTorqueExponent.Value);
        float fuelMaxTorque = rpmFactor * charge
            / (TurboConfig.LambdaCalibration.Value * TurboConfig.TorqueLambdaFloor.Value);
        float effective = Mathf.Min(demand, fuelMaxTorque);

        // thermal enthalpy feedback: overfueling shortens spool-up time
        Overfuel = Mathf.Max(0f, fuelDemand - charge / TurboConfig.LambdaCalibration.Value);

        // boost equilibrium ceiling: exhaust mass flow scales with engine
        // speed, so even a pinned rack at low rpm cannot reach rated boost.
        // Spool mode keys off demand (not target) so a lug-driven ceiling drop
        // eases boost down with turbine inertia instead of blowing it off.
        float rpmMassFlow = Mathf.Pow(Mathf.Clamp01(rpmNorm), TurboConfig.RpmBoostExponent.Value);
        float target = Mathf.Clamp01(fuelDemand) * rpmMassFlow;
        float tau = fuelDemand > _boost
            ? Mathf.Max(TurboConfig.MinSpoolTau.Value,
                TurboConfig.TauUp.Value / (1f + TurboConfig.ThermalK.Value * Overfuel))
            : TurboConfig.TauDown.Value;
        _boost += (target - _boost) * (1f - Mathf.Exp(-delta / tau));

        if (!TurboModel.SimActive) return;

        if (engineOn && _prevDemand - demand > 0.3f && _boost > 0.75f)
        {
            _whine?.TriggerSurge();
        }
        _prevDemand = demand;

        if (TurboConfig.DebugLog.Value && Time.time - _lastDebugLog > 0.5f)
        {
            _lastDebugLog = Time.time;
            TurboModel.Log.LogInfo($"fuelNorm={_fuelNorm:0.00} smoke={SmokeDensity:0.00} lambda={lambda:0.00} overfuel={Overfuel:0.00} " +
                                   $"boost={_boost:0.00} charge={charge:0.00} demand={demand:0.00} rpmNorm={rpmNorm:0.00} [{Car.ID}]");
        }

        _throttlePort.Value = effective;
    }
}

[HarmonyPatch(typeof(SimulationFlow), nameof(SimulationFlow.Tick))]
internal static class TurboTickPatch
{
    private static void Postfix(SimulationFlow __instance, float delta)
    {
        TurboModel.TickFlow(__instance, delta);
    }
}