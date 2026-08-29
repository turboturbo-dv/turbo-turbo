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

    private static bool IsTurboLoco(TrainCarType carType)
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

        if (throttlePort == null || rpmNormPort == null)
        {
            Log.LogWarning($"could not resolve engine ports on {car.carType} [{car.ID}], turbo not attached");
            return;
        }

        Turbos[flow] = new EngineTurbo(car, flow, throttlePort, rpmNormPort, engineOnPort);
        car.OnDestroyCar += OnCarDestroyed;
        Turbos[flow].AttachAudio(new TurboWhineAudio(car, TurboAudio.CreateParams()));
        Turbos[flow].TryAttachSmoke(flow);
        Log.LogInfo($"turbo model attached to {car.carType} [{car.ID}] (fuel demand port: {throttlePort.id})");
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
    private readonly SimulationFlow _flow;
    private TurboWhineAudio _whine;
    private readonly List<TurboSmokeEmitter> _smoke = new();
    private bool _smokeAttached;
    private float _smokeRetryTimer;
    private float _boost;
    private float _demand;
    private float _rpmNorm;
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

    internal EngineTurbo(TrainCar car, SimulationFlow flow, Port throttlePort, Port rpmNormPort, Port engineOnPort)
    {
        Car = car;
        _flow = flow;
        _throttlePort = throttlePort;
        _rpmNormPort = rpmNormPort;
        _engineOnPort = engineOnPort;
    }

    internal void AttachAudio(TurboWhineAudio whine) => _whine = whine;

    /// <summary>
    /// Clones the vanilla exhaust system into our emitter, and (when
    /// TurboConfig.TakeOverExhaust is on) unhooks the vanilla port readers and parks the
    /// vanilla system so our emitter is the sole exhaust. The car model may
    /// not be loaded at attach time - retried from UpdateFrame.
    /// </summary>
    internal void TryAttachSmoke(SimulationFlow flow)
    {
        if (_smokeAttached) return;
        var allPs = Car.GetComponentsInChildren<ParticleSystem>(true);
        var exhausts = allPs
            .Where(ps => ps.name == "ExhaustEngineSmoke")
            .ToList();
        if (exhausts.Count == 0) return;

        bool ownsExhaust = TurboConfig.TakeOverExhaust.Value;

        // the damaged-engine smoke system renders proven visible black -
        // borrow its material for the emitter
        var damaged = allPs.FirstOrDefault(ps => ps.name == "DamagedEngineSmoke");
        Material blackMaterial = damaged != null
            ? damaged.GetComponent<ParticleSystemRenderer>().sharedMaterial
            : null;

        foreach (ParticleSystem ps in exhausts)
        {
            _smoke.Add(new TurboSmokeEmitter(ps, blackMaterial, ownsExhaust));
        }

        if (ownsExhaust)
        {
            // unsubscribe the vanilla exhaust readers from their sim ports,
            // then stop and park the vanilla system - it stays out of the way
            foreach (ParticlesPortReadersController ctrl in Car.GetComponentsInChildren<ParticlesPortReadersController>(true))
            {
                if (ctrl.particlePortReaders == null) continue;
                foreach (var reader in ctrl.particlePortReaders
                    .Where(r => r.particlesParent != null && r.particlesParent.name == "ExhaustEngineSmoke")
                    .ToList())
                {
                    if (reader.particleUpdaters != null)
                    {
                        foreach (var updater in reader.particleUpdaters)
                        {
                            updater.Deinit(flow);
                        }
                    }
                }
            }
            foreach (ParticleSystem ps in exhausts)
            {
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                ps.gameObject.SetActive(false);
            }
            TurboModel.Log.LogInfo($"vanilla exhaust taken over on [{Car.ID}] (readers deinit'd, system parked)");
        }

        _smokeAttached = true;
        TurboModel.Log.LogInfo($"soot emitter attached on [{Car.ID}] ({_smoke.Count} exhaust stack(s), ownsExhaust={ownsExhaust})");
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
            float soot = TurboModel.SimActive ? SmokeDensity : 0f;
            foreach (TurboSmokeEmitter emitter in _smoke)
            {
                emitter.Update(soot, _rpmNorm, EngineRunning);
            }
        }

        _whine?.UpdateFromModel(_boost, _demand, _rpmNorm, frameDt);
    }

    internal void Destroy()
    {
        foreach (TurboSmokeEmitter emitter in _smoke)
        {
            emitter.Destroy();
        }
        _smoke.Clear();
        _whine?.Destroy();
        _whine = null;
    }

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

        if (TurboConfig.DebugLog.Value && SmokeDensity > 0.05f && Time.time - _lastDebugLog > 0.5f)
        {
            _lastDebugLog = Time.time;
            TurboModel.Log.LogInfo($"smoke={SmokeDensity:0.00} lambda={lambda:0.00} overfuel={Overfuel:0.00} " +
                                   $"boost={_boost:0.00} charge={charge:0.00} demand={demand:0.00} rpmNorm={rpmNorm:0.00} [{Car.ID}]");
        }

        _throttlePort.Value = effective;
    }
}

[HarmonyPatch(typeof(SimController), nameof(SimController.Initialize))]
internal static class TurboAttachPatch
{
    private static void Postfix(SimController __instance, TrainCar trainCar)
    {
        TurboModel.Attach(trainCar, __instance.simFlow);
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