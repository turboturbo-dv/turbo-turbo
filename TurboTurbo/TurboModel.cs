using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using BepInEx.Logging;
using DV.Simulation.Cars;
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

    private static ConfigEntry<bool> _enabled;
    internal static ConfigEntry<KeyboardShortcut> ToggleKey;
    private static ConfigEntry<string> _turboLocos;
    internal static ConfigEntry<float> TauUp;
    internal static ConfigEntry<float> TauDown;
    internal static ConfigEntry<float> AirNAFraction;
    internal static ConfigEntry<float> LambdaCalibration;
    internal static ConfigEntry<float> RpmTorqueExponent;
    internal static ConfigEntry<float> SmokeOnsetLambda;
    internal static ConfigEntry<float> SmokeOpaqueLambda;
    internal static ConfigEntry<float> TorqueLambdaFloor;
    internal static ConfigEntry<float> ThermalK;
    internal static ConfigEntry<float> MinSpoolTau;
    internal static ConfigEntry<bool> DebugLog;
    internal static ConfigEntry<bool> SmokeEnabled;
    internal static ConfigEntry<float> SmokeMaxRate;
    internal static ConfigEntry<float> SmokeParticleAlpha;
    internal static ConfigEntry<float> SmokeSizeMult;
    internal static ConfigEntry<bool> WhiteTestPuffs;

    internal static bool SimActive => _enabled.Value;

    private static readonly Dictionary<SimulationFlow, EngineTurbo> Turbos = new();

    internal static void Bind(ConfigFile config)
    {
        _enabled = config.Bind("Turbo", "Enabled", true,
            "Master switch for the turbo simulation. Can also be toggled in-game with ToggleKey.");
        ToggleKey = config.Bind("Turbo", "ToggleKey", new KeyboardShortcut(KeyCode.F6),
            "Hotkey to toggle the turbo simulation on/off while playing (A/B compare).");
        _turboLocos = config.Bind("Turbo", "TurboLocos", "LocoDiesel",
            "Comma-separated TrainCarType names whose engines are turbocharged (e.g. LocoDiesel,LocoDM3).");
        TauUp = config.Bind("Turbo", "TauUp", 3.0f,
            "Seconds of spool-up time constant (clean combustion).");
        TauDown = config.Bind("Turbo", "TauDown", 1.0f,
            "Seconds of blow-down (boost release) time constant. Active only when demand drops below boost.");
        AirNAFraction = config.Bind("Turbo", "AirNAFraction", 0.55f,
            "Per-stroke charge index of naturally-aspirated operation (zero boost vs max boost).");
        LambdaCalibration = config.Bind("Turbo", "LambdaCalibration", 2.5f,
            "Air-to-fuel calibration constant for the lambda proxy (2.5 = full boost, full rack is exactly clean).");
        RpmTorqueExponent = config.Bind("Turbo", "RpmTorqueExponent", 0.4f,
            "RPM blending in the torque cap: 0 = pure per-stroke charge, 1 = strict airflow. Low values reduce low-rpm torque restriction.");
        SmokeOnsetLambda = config.Bind("Turbo", "SmokeOnsetLambda", 0.85f,
            "Lambda where soot formation begins (fueling above this vs air is overfueling).");
        SmokeOpaqueLambda = config.Bind("Turbo", "SmokeOpaqueLambda", 0.45f,
            "Lambda where smoke reaches full opacity.");
        TorqueLambdaFloor = config.Bind("Turbo", "TorqueLambdaFloor", 0.7f,
            "Lambda below which extra fuel contributes no torque. Sets both the NA torque fraction at zero boost and how long the oxygen cap binds during spool (higher = wider power surge window, weaker lugging).");
        ThermalK = config.Bind("Turbo", "ThermalK", 0.8f,
            "Thermal enthalpy feedback strength: overfueling shortens spool-up time.");
        MinSpoolTau = config.Bind("Turbo", "MinSpoolTau", 0.5f,
            "Floor for the spool-up time constant (stability under heavy overfuel).");
        SmokeEnabled = config.Bind("TurboSmoke", "Enabled", true,
            "Emit a black soot plume from the exhaust, driven by the smoke density signal.");
        SmokeMaxRate = config.Bind("TurboSmoke", "MaxRate", 120f,
            "Soot particle emission rate [particles/s] at full smoke density.");
        SmokeParticleAlpha = config.Bind("TurboSmoke", "ParticleAlpha", 0.45f,
            "Peak opacity per soot particle. Lower = more translucent individual puffs.");
        SmokeSizeMult = config.Bind("TurboSmoke", "SizeMult", 0.9f,
            "Soot particle size relative to the vanilla exhaust particles.");
        WhiteTestPuffs = config.Bind("TurboSmoke", "WhiteTestPuffs", false,
            "Render constant white test puffs instead of soot (render-path diagnostics).");
        DebugLog = config.Bind("Turbo", "DebugLog", false,
            "Log overfuel/boost values while driving.");
        Log = BepInEx.Logging.Logger.CreateLogSource("TurboModel");
    }

    internal static void HandleUpdate()
    {
        if (ToggleKey.Value.IsDown())
        {
            _enabled.Value = !_enabled.Value;
            string state = _enabled.Value ? "ENABLED" : "DISABLED (vanilla engine behaviour)";
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
        foreach (string name in _turboLocos.Value.Split(','))
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

        Turbos[flow] = new EngineTurbo(car, throttlePort, rpmNormPort, engineOnPort);
        car.OnDestroyCar += OnCarDestroyed;
        Turbos[flow].AttachAudio(new TurboWhineAudio(car, TurboAudio.CreateParams()));
        Turbos[flow].TryAttachSmoke();
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

    /// <summary>Air-fuel ratio proxy (calibrated: ~1.0 = edge of clean full load).</summary>
    internal float Lambda { get; private set; }

    /// <summary>Overfueling amount [0..1] - fuel beyond available air.</summary>
    internal float Overfuel { get; private set; }

    internal EngineTurbo(TrainCar car, Port throttlePort, Port rpmNormPort, Port engineOnPort)
    {
        Car = car;
        _throttlePort = throttlePort;
        _rpmNormPort = rpmNormPort;
        _engineOnPort = engineOnPort;
    }

    internal void AttachAudio(TurboWhineAudio whine) => _whine = whine;

    /// <summary>
    /// Clones the vanilla exhaust system into a soot emitter. The car model
    /// may not be loaded at attach time - retried from UpdateFrame.
    /// </summary>
    internal void TryAttachSmoke()
    {
        if (_smokeAttached) return;
        var allPs = Car.GetComponentsInChildren<ParticleSystem>(true);
        var exhausts = allPs
            .Where(ps => ps.name == "ExhaustEngineSmoke")
            .ToList();
        if (exhausts.Count == 0) return;

        // the damaged-engine smoke system renders proven visible black -
        // borrow its material for the soot emitter
        var damaged = allPs.FirstOrDefault(ps => ps.name == "DamagedEngineSmoke");
        Material blackMaterial = damaged != null
            ? damaged.GetComponent<ParticleSystemRenderer>().sharedMaterial
            : null;

        foreach (ParticleSystem ps in exhausts)
        {
            _smoke.Add(new TurboSmokeEmitter(ps, blackMaterial));
        }
        _smokeAttached = true;
        TurboModel.Log.LogInfo($"soot emitter attached on [{Car.ID}] ({_smoke.Count} exhaust stack(s))");
    }

    internal void UpdateFrame(float frameDt)
    {
        if (!_smokeAttached)
        {
            if (TurboModel.SmokeEnabled.Value)
            {
                _smokeRetryTimer += frameDt;
                if (_smokeRetryTimer > 2f)
                {
                    _smokeRetryTimer = 0f;
                    TryAttachSmoke();
                }
            }
        }
        else
        {
            float smoke = TurboModel.SimActive ? SmokeDensity : 0f;
            bool testMode = TurboModel.WhiteTestPuffs.Value;
            foreach (TurboSmokeEmitter emitter in _smoke)
            {
                emitter.Update(smoke, testMode);
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
        float fuelDemand = engineOn ? demand : 0f;

        _demand = fuelDemand;
        _rpmNorm = rpmNorm;

        // per-stroke cylinder charge index: 1.0 = naturally aspirated,
        // 2.125 = full boost. This is the combustion-relevant air quantity.
        float charge = TurboModel.AirNAFraction.Value
                       + (1f - TurboModel.AirNAFraction.Value) * (1f + 2.5f * _boost);

        // per-stroke air-fuel ratio proxy and smoke density
        float lambda = charge / (TurboModel.LambdaCalibration.Value * Mathf.Max(0.01f, fuelDemand));
        Lambda = lambda;
        SmokeDensity = fuelDemand < 0.02f
            ? 0f
            : Mathf.Clamp01((TurboModel.SmokeOnsetLambda.Value - lambda)
                / (TurboModel.SmokeOnsetLambda.Value - TurboModel.SmokeOpaqueLambda.Value));

        // torque cap: per-stroke charge sets usable work, blended with engine
        // speed via RpmTorqueExponent (0 = pure per-stroke, 1 = strict airflow).
        // Between SmokeOnsetLambda and TorqueLambdaFloor the engine still pulls
        // hard - it just smokes - which keeps a lugging engine from stalling.
        float rpmFactor = Mathf.Pow(rpmNorm, TurboModel.RpmTorqueExponent.Value);
        float fuelMaxTorque = rpmFactor * charge
            / (TurboModel.LambdaCalibration.Value * TurboModel.TorqueLambdaFloor.Value);
        float effective = Mathf.Min(demand, fuelMaxTorque);

        // thermal enthalpy feedback: overfueling shortens spool-up time
        Overfuel = Mathf.Max(0f, fuelDemand - charge / TurboModel.LambdaCalibration.Value);
        float target = fuelDemand;
        float tau = target > _boost
            ? Mathf.Max(TurboModel.MinSpoolTau.Value,
                TurboModel.TauUp.Value / (1f + TurboModel.ThermalK.Value * Overfuel))
            : TurboModel.TauDown.Value;
        _boost += (target - _boost) * (1f - Mathf.Exp(-delta / tau));

        if (!TurboModel.SimActive) return;

        if (engineOn && _prevDemand - demand > 0.3f && _boost > 0.75f)
        {
            _whine?.TriggerSurge();
        }
        _prevDemand = demand;

        if (TurboModel.DebugLog.Value && SmokeDensity > 0.05f && Time.time - _lastDebugLog > 0.5f)
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
