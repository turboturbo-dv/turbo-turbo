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
    internal static ConfigEntry<float> BoostFloor;
    internal static ConfigEntry<float> SpoolStartRpmNorm;
    internal static ConfigEntry<float> SpoolFullRpmNorm;
    internal static ConfigEntry<float> DemandRef;
    internal static ConfigEntry<bool> DebugLog;

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
            "Seconds of spool-up time constant.");
        TauDown = config.Bind("Turbo", "TauDown", 1.0f,
            "Seconds of blow-down (boost release) time constant.");
        BoostFloor = config.Bind("Turbo", "BoostFloor", 0.55f,
            "Fuel/power cap at zero boost (1 = no limiting, 0.55 = 55% fuel when unspooled).");
        SpoolStartRpmNorm = config.Bind("Turbo", "SpoolStartRpmNorm", 0.35f,
            "Normalized engine rpm where the turbo starts to spool.");
        SpoolFullRpmNorm = config.Bind("Turbo", "SpoolFullRpmNorm", 0.65f,
            "Normalized engine rpm where the turbo can reach full boost.");
        DemandRef = config.Bind("Turbo", "DemandRef", 0.2f,
            "Governor demand at which fuel limiting reaches full strength (limiting fades in below this to keep idle stable).");
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

        if (throttlePort == null || rpmNormPort == null)
        {
            Log.LogWarning($"could not resolve engine ports on {car.carType} [{car.ID}], turbo not attached");
            return;
        }

        Turbos[flow] = new EngineTurbo(car, throttlePort, rpmNormPort);
        car.OnDestroyCar += OnCarDestroyed;
        Turbos[flow].AttachAudio(new TurboWhineAudio(car, TurboAudio.CreateParams()));
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
    private TurboWhineAudio _whine;
    private float _boost;
    private float _demand;
    private float _rpmNorm;
    private float _prevDemand;
    private float _lastDebugLog;

    internal EngineTurbo(TrainCar car, Port throttlePort, Port rpmNormPort)
    {
        Car = car;
        _throttlePort = throttlePort;
        _rpmNormPort = rpmNormPort;
    }

    internal void AttachAudio(TurboWhineAudio whine) => _whine = whine;

    internal void UpdateFrame(float frameDt)
    {
        _whine?.UpdateFromModel(_boost, _demand, _rpmNorm, frameDt);
    }

    internal void Destroy()
    {
        _whine?.Destroy();
        _whine = null;
    }

    internal void Tick(float delta)
    {
        float demand = _throttlePort.Value;
        float rpmNorm = _rpmNormPort.Value;
        _demand = demand;
        _rpmNorm = rpmNorm;

        float gate = Mathf.InverseLerp(TurboModel.SpoolStartRpmNorm.Value, TurboModel.SpoolFullRpmNorm.Value, rpmNorm);
        float target = Mathf.Clamp01(demand) * gate;
        float tau = target > _boost ? TurboModel.TauUp.Value : TurboModel.TauDown.Value;
        tau = Mathf.Max(0.01f, tau);
        _boost += (target - _boost) * (1f - Mathf.Exp(-delta / tau));

        if (!TurboModel.SimActive) return;

        if (_prevDemand - demand > 0.3f && _boost > 0.75f)
        {
            _whine?.TriggerSurge();
        }
        _prevDemand = demand;

        float boostCap = TurboModel.BoostFloor.Value + (1f - TurboModel.BoostFloor.Value) * _boost;
        float capWeight = Mathf.Clamp01(demand / Mathf.Max(0.001f, TurboModel.DemandRef.Value));
        float effective = demand * Mathf.Lerp(1f, boostCap, capWeight);

        if (TurboModel.DebugLog.Value && demand - effective > 0.05f && Time.time - _lastDebugLog > 0.5f)
        {
            _lastDebugLog = Time.time;
            TurboModel.Log.LogInfo($"overfuel={demand - effective:0.00} boost={_boost:0.00} demand={demand:0.00} rpmNorm={rpmNorm:0.00} [{Car.ID}]");
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
