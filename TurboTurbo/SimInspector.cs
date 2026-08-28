using System.Collections.Generic;
using System.Linq;
using System.Text;
using BepInEx.Configuration;
using BepInEx.Logging;
using DV.Simulation.Cars;
using DV.Simulation.Ports;
using DV.ThingTypes;
using HarmonyLib;
using LocoSim.Definitions;
using LocoSim.Implementations;
using UnityEngine;

namespace TurboTurbo;

/// <summary>
/// Dumps the sim component graph (components, ports, connections, fuses)
/// of cars to the log and can live-sample watched ports over time.
/// </summary>
internal static class SimInspector
{
    internal static ConfigEntry<bool> Enabled;
    internal static ConfigEntry<bool> DieselOnly;
    internal static ConfigEntry<KeyboardShortcut> DumpCarKey;
    internal static ConfigEntry<KeyboardShortcut> DumpLocosKey;
    internal static ConfigEntry<KeyboardShortcut> WatchKey;
    internal static ConfigEntry<string> WatchPorts;
    internal static ConfigEntry<float> WatchInterval;

    private static ManualLogSource _log;

    private static readonly TrainCarType[] DieselTypes =
    {
        TrainCarType.LocoShunter,
        TrainCarType.LocoDiesel,
        TrainCarType.LocoDH4,
        TrainCarType.LocoDM3,
        TrainCarType.LocoDM1U,
        TrainCarType.LocoMicroshunter,
    };

    internal static void Bind(ConfigFile config)
    {
        Enabled = config.Bind("SimInspector", "Enabled", true,
            "Dump the sim component graph of cars to the log when they spawn.");
        DieselOnly = config.Bind("SimInspector", "DieselOnly", true,
            "Only dump diesel locomotives instead of every car.");
        DumpCarKey = config.Bind("SimInspector", "DumpCarKey", new KeyboardShortcut(KeyCode.F9),
            "Dump the sim graph of the car the player is currently in.");
        DumpLocosKey = config.Bind("SimInspector", "DumpLocosKey", new KeyboardShortcut(KeyCode.F8),
            "Dump the sim graph of all loaded diesel locomotives (nearest first).");
        WatchKey = config.Bind("SimInspector", "WatchKey", new KeyboardShortcut(KeyCode.F7),
            "Toggle live sampling of the current car's watched ports.");
        WatchPorts = config.Bind("SimInspector", "WatchPorts", "THROTTLE,GOAL_POWER,RPM,POWER_OUT,FUEL_CONS",
            "Comma-separated port id substrings to sample while watching.");
        WatchInterval = config.Bind("SimInspector", "WatchInterval", 0.1f,
            "Seconds between watch samples.");
        _log = BepInEx.Logging.Logger.CreateLogSource("SimInspector");
    }

    internal static void Update()
    {
        if (!Enabled.Value) return;

        if (DumpCarKey.Value.IsDown())
        {
            DumpOne(PlayerManager.Car);
        }

        if (DumpLocosKey.Value.IsDown())
        {
            DumpLoadedDieselLocos();
        }

        if (WatchKey.Value.IsDown())
        {
            ToggleWatch();
        }

        WatchTick();
    }

    internal static bool ShouldDump(TrainCar car)
    {
        if (car == null || !Enabled.Value) return false;
        return !DieselOnly.Value || DieselTypes.Contains(car.carType);
    }

    internal static void DumpOne(TrainCar car)
    {
        if (car == null)
        {
            _log.LogWarning("no car to dump (enter a loco first)");
            return;
        }
        SimController sim = car.SimController;
        if (sim == null || sim.simFlow == null)
        {
            _log.LogWarning($"{car.carType} \"{car.name}\" has no initialized sim flow");
            return;
        }
        Dump(car, sim.simFlow);
    }

    internal static void DumpLoadedDieselLocos()
    {
        Vector3 origin = Camera.main ? Camera.main.transform.position : Vector3.zero;
        var locos = Object.FindObjectsOfType<TrainCar>()
            .Where(c => DieselTypes.Contains(c.carType))
            .OrderBy(c => (c.transform.position - origin).sqrMagnitude)
            .ToList();
        _log.LogInfo($"--- dumping {locos.Count} loaded diesel locos (nearest first) ---");
        foreach (TrainCar car in locos)
        {
            DumpOne(car);
        }
    }

    internal static void Dump(TrainCar car, SimulationFlow flow)
    {
        var comps = flow.OrderedSimComps;
        _log.LogInfo($"=== {car.carType} \"{car.name}\" ID={car.ID} | {comps.Count} components, {flow.AllPorts.Count} ports, {flow.AllFuses.Count} fuses ===");

        for (int i = 0; i < comps.Count; i++)
        {
            SimComponent comp = comps[i];
            _log.LogInfo($"[{i,2}] {comp.GetType().Name} \"{comp.id}\"");
            foreach (Port port in comp.GetAllPorts())
            {
                string line = $"      {port.type,-12} {port.id} = {port.Value:0.####}";
                if (port.IsConnectedPort && (port.type == PortType.OUT || port.type == PortType.FORWARD_OUT))
                {
                    Port target = Traverse.Create(port).Field("connectedPort").GetValue<Port>();
                    if (target != null) line += $"  -> {target.id}";
                }
                _log.LogInfo(line);
            }
            List<PortReference> refs = comp.GetAllPortReferences();
            if (refs.Count > 0)
            {
                string targets = string.Join(", ", refs.Select(r =>
                {
                    Port p = Traverse.Create(r).Field("port").GetValue<Port>();
                    return p != null ? $"{r.id} -> {p.id}" : $"{r.id} (unbound)";
                }));
                _log.LogInfo($"      refs: {targets}");
            }
        }

        if (flow.AllFuses.Count > 0)
        {
            string fuses = string.Join(", ", flow.AllFuses.Select(f => $"{f.id}={(f.State ? "on" : "off")}"));
            _log.LogInfo($"fuses: {fuses}");
        }

        foreach (LayeredAudioPortReader reader in car.GetComponentsInChildren<LayeredAudioPortReader>(true))
        {
            LayeredAudio la = reader.GetComponent<LayeredAudio>();
            string layers = la != null && la.layers != null
                ? string.Join("+", la.layers.Select(l => l.name))
                : "?";
            _log.LogInfo($"audio: [{reader.updateType}] port={reader.portId} mult={reader.valueMultiplier:0.###} off={reader.valueOffset:0.###} layers=[{layers}] on '{(la ? la.name : "?")}'");
        }
        foreach (AudioClipPortReader reader in car.GetComponentsInChildren<AudioClipPortReader>(true))
        {
            string clips = reader.clips != null ? string.Join(",", reader.clips.Select(c => c ? c.name : "?")) : "";
            _log.LogInfo($"oneshot: [{reader.playType} @{reader.playAudioThreshold:0.###}] port={reader.portId} clips=[{clips}]");
        }
    }

    #region live watch

    private static TrainCar _watchedCar;
    private static SimulationFlow _watchedFlow;
    private static List<KeyValuePair<string, Port>> _watchedPorts;
    private static float _watchTimer;
    private static float _watchTime;

    private static void ToggleWatch()
    {
        if (_watchedCar != null)
        {
            StopWatch();
            return;
        }
        StartWatch(PlayerManager.Car);
    }

    private static void StartWatch(TrainCar car)
    {
        if (car == null)
        {
            _log.LogWarning("watch: no car to watch (enter a loco first)");
            return;
        }
        SimController sim = car.SimController;
        if (sim == null || sim.simFlow == null)
        {
            _log.LogWarning($"watch: {car.carType} \"{car.name}\" has no initialized sim flow");
            return;
        }

        string[] filters = WatchPorts.Value.Split(',')
            .Select(s => s.Trim())
            .Where(s => s.Length > 0)
            .ToArray();

        var nameCounts = new Dictionary<string, int>();
        var ports = new List<KeyValuePair<string, Port>>();
        foreach (Port port in sim.simFlow.AllPorts)
        {
            if (!filters.Any(f => port.id.IndexOf(f, System.StringComparison.OrdinalIgnoreCase) >= 0)) continue;
            string id = port.id.Substring(port.id.LastIndexOf('.') + 1);
            string unique = id;
            int n = 1;
            while (nameCounts.ContainsKey(unique))
            {
                n++;
                unique = $"{id}#{n}";
            }
            nameCounts[unique] = 1;
            ports.Add(new KeyValuePair<string, Port>(unique, port));
        }

        if (ports.Count == 0)
        {
            _log.LogWarning($"watch: no ports match [{WatchPorts.Value}] on {car.carType}");
            return;
        }

        _watchedCar = car;
        _watchedFlow = sim.simFlow;
        _watchedPorts = ports;
        _watchTimer = 0f;
        _watchTime = 0f;
        _log.LogInfo($"watch: started on {car.carType} \"{car.name}\" | {ports.Count} ports: {string.Join(", ", ports.Select(p => p.Key))}");
    }

    private static void StopWatch()
    {
        if (_watchedCar != null)
        {
            _log.LogInfo($"watch: stopped after {_watchTime:0.0}s");
        }
        _watchedCar = null;
        _watchedFlow = null;
        _watchedPorts = null;
    }

    private static void WatchTick()
    {
        if (_watchedCar == null) return;

        TrainCar playerCar = PlayerManager.Car;
        if (playerCar != null && playerCar != _watchedCar)
        {
            // player switched cars - follow them
            StopWatch();
            StartWatch(playerCar);
            return;
        }

        _watchTimer += Time.deltaTime;
        if (_watchTimer < WatchInterval.Value) return;
        _watchTimer -= WatchInterval.Value;
        _watchTime += WatchInterval.Value;

        var sb = new StringBuilder(256);
        sb.Append($"watch t={_watchTime:0.0}s");
        foreach (KeyValuePair<string, Port> kvp in _watchedPorts)
        {
            sb.Append($"  {kvp.Key}={kvp.Value.Value:0.###}");
        }
        _log.LogInfo(sb.ToString());
    }

    #endregion
}

[HarmonyPatch(typeof(SimController), nameof(SimController.Initialize))]
internal static class SimInspectorPatches
{
    private static void Postfix(SimController __instance, TrainCar trainCar)
    {
        if (!SimInspector.ShouldDump(trainCar)) return;
        SimInspector.Dump(trainCar, __instance.simFlow);
    }
}
