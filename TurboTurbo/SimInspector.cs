using System.Collections.Generic;
using System.Linq;
using System.Text;
using BepInEx.Configuration;
using BepInEx.Logging;
using DV.Simulation.Cars;
using DV.Simulation.Controllers;
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
    internal static ManualLogSource Log;

    private static readonly TrainCarType[] DieselTypes =
    {
        TrainCarType.LocoShunter,
        TrainCarType.LocoDiesel,
        TrainCarType.LocoDH4,
        TrainCarType.LocoDM3,
        TrainCarType.LocoDM1U,
        TrainCarType.LocoMicroshunter,
    };

    internal static void Update()
    {
        if (!TurboConfig.InspectorEnabled.Value) return;

        if (TurboConfig.InspectorDumpCarKey.Value.IsDown())
        {
            DumpOne(PlayerManager.Car);
        }

        if (TurboConfig.InspectorDumpLocosKey.Value.IsDown())
        {
            DumpLoadedDieselLocos();
        }

        if (TurboConfig.InspectorWatchKey.Value.IsDown())
        {
            ToggleWatch();
        }

        WatchTick();
    }

    internal static bool ShouldDump(TrainCar car)
    {
        if (car == null || !TurboConfig.InspectorEnabled.Value) return false;
        return !TurboConfig.InspectorDieselOnly.Value || DieselTypes.Contains(car.carType);
    }

    internal static void DumpOne(TrainCar car)
    {
        if (car == null)
        {
            Log.LogWarning("no car to dump (enter a loco first)");
            return;
        }
        SimController sim = car.SimController;
        if (sim == null || sim.simFlow == null)
        {
            Log.LogWarning($"{car.carType} \"{car.name}\" has no initialized sim flow");
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
        Log.LogInfo($"--- dumping {locos.Count} loaded diesel locos (nearest first) ---");
        foreach (TrainCar car in locos)
        {
            DumpOne(car);
        }
    }

    internal static void Dump(TrainCar car, SimulationFlow flow)
    {
        var comps = flow.OrderedSimComps;
        Log.LogInfo($"=== {car.carType} \"{car.name}\" ID={car.ID} | {comps.Count} components, {flow.AllPorts.Count} ports, {flow.AllFuses.Count} fuses ===");

        for (int i = 0; i < comps.Count; i++)
        {
            SimComponent comp = comps[i];
            Log.LogInfo($"[{i,2}] {comp.GetType().Name} \"{comp.id}\"");
            foreach (Port port in comp.GetAllPorts())
            {
                string line = $"      {port.type,-12} {port.id} = {port.Value:0.####}";
                if (port.IsConnectedPort && (port.type == PortType.OUT || port.type == PortType.FORWARD_OUT))
                {
                    Port target = Traverse.Create(port).Field("connectedPort").GetValue<Port>();
                    if (target != null) line += $"  -> {target.id}";
                }
                Log.LogInfo(line);
            }
            List<PortReference> refs = comp.GetAllPortReferences();
            if (refs.Count > 0)
            {
                string targets = string.Join(", ", refs.Select(r =>
                {
                    Port p = Traverse.Create(r).Field("port").GetValue<Port>();
                    return p != null ? $"{r.id} -> {p.id}" : $"{r.id} (unbound)";
                }));
                Log.LogInfo($"      refs: {targets}");
            }
        }

        if (flow.AllFuses.Count > 0)
        {
            string fuses = string.Join(", ", flow.AllFuses.Select(f => $"{f.id}={(f.State ? "on" : "off")}"));
            Log.LogInfo($"fuses: {fuses}");
        }

        foreach (LayeredAudio la in car.GetComponentsInChildren<LayeredAudio>(true))
        {
            int layerCount = la.layers != null ? la.layers.Length : 0;
            var groups = new System.Collections.Generic.List<string>();
            if (la.layers != null)
            {
                foreach (var l in la.layers)
                {
                    groups.Add(l?.source != null
                        ? (l.source.outputAudioMixerGroup != null ? l.source.outputAudioMixerGroup.name : "master")
                        : "no-src");
                }
            }
            Log.LogInfo($"layeredAudio: '{la.name}' layers={layerCount} groups=[{string.Join(",", groups)}]");
        }
        foreach (LayeredAudioPortReader reader in car.GetComponentsInChildren<LayeredAudioPortReader>(true))
        {
            LayeredAudio la = reader.GetComponent<LayeredAudio>();
            string layers = la != null && la.layers != null
                ? string.Join("+", la.layers.Select(l => l.name))
                : "?";
            Log.LogInfo($"audio: [{reader.updateType}] port={reader.portId} mult={reader.valueMultiplier:0.###} off={reader.valueOffset:0.###} layers=[{layers}] on '{(la ? la.name : "?")}'");
        }
        foreach (AudioClipPortReader reader in car.GetComponentsInChildren<AudioClipPortReader>(true))
        {
            string clips = reader.clips != null ? string.Join(",", reader.clips.Select(c => c ? c.name : "?")) : "";
            Log.LogInfo($"oneshot: [{reader.playType} @{reader.playAudioThreshold:0.###}] port={reader.portId} clips=[{clips}]");
        }

        foreach (ParticlesPortReadersController ctrl in car.GetComponentsInChildren<ParticlesPortReadersController>(true))
        {
            Log.LogInfo($"particlesController '{ctrl.name}' readers={ctrl.particlePortReaders?.Count ?? 0} colorReaders={ctrl.particleColorPortReaders?.Count ?? 0}");
            if (ctrl.particlePortReaders != null)
            {
                foreach (var r in ctrl.particlePortReaders)
                {
                    string parent = r.particlesParent ? r.particlesParent.name : "?";
                    string updaters = r.particleUpdaters != null
                        ? string.Join(" | ", r.particleUpdaters.Select(u =>
                        {
                            string props = u.propertiesToUpdate != null
                                ? string.Join("+", u.propertiesToUpdate.Select(p =>
                                    $"{p.propertyType}:{(p.propertyChangeCurve != null ? $"{p.propertyChangeCurve.keys.Length}keys[{p.propertyChangeCurve.keys.FirstOrDefault().time:0.##}->{p.propertyChangeCurve.keys.LastOrDefault().time:0.##}]" : "no-curve")}"))
                                : "";
                            return $"port={u.portId} mod(m={u.inputModifier.valueMultiplier:0.##},o={u.inputModifier.valueOffset:0.##}) {props}";
                        }))
                        : "";
                    Log.LogInfo($"  reader parent='{parent}' {updaters}");
                    if (r.particlesParent != null)
                    {
                        foreach (ParticleSystem ps in r.particlesParent.GetComponentsInChildren<ParticleSystem>(true))
                        {
                            var main = ps.main;
                            var rend = ps.GetComponent<ParticleSystemRenderer>();
                            string mat = rend.sharedMaterial ? rend.sharedMaterial.name : "?";
                            Log.LogInfo($"    ps '{ps.name}' max={main.maxParticles} size={main.startSize.constant:0.##} life={main.startLifetime.constant:0.##} color={main.startColor.color} mat='{mat}'");
                        }
                    }
                }
            }
            if (ctrl.particleColorPortReaders != null)
            {
                foreach (var r in ctrl.particleColorPortReaders)
                {
                    Log.LogInfo($"  colorReader parent='{(r.particlesParent ? r.particlesParent.name : "?")}' port={r.portId} min={r.startColorMin} max={r.startColorMax}");
                }
            }
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
            Log.LogWarning("watch: no car to watch (enter a loco first)");
            return;
        }
        SimController sim = car.SimController;
        if (sim == null || sim.simFlow == null)
        {
            Log.LogWarning($"watch: {car.carType} \"{car.name}\" has no initialized sim flow");
            return;
        }

        string[] filters = TurboConfig.InspectorWatchPorts.Value.Split(',')
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
            Log.LogWarning($"watch: no ports match [{TurboConfig.InspectorWatchPorts.Value}] on {car.carType}");
            return;
        }

        _watchedCar = car;
        _watchedFlow = sim.simFlow;
        _watchedPorts = ports;
        _watchTimer = 0f;
        _watchTime = 0f;
        Log.LogInfo($"watch: started on {car.carType} \"{car.name}\" | {ports.Count} ports: {string.Join(", ", ports.Select(p => p.Key))}");
    }

    private static void StopWatch()
    {
        if (_watchedCar != null)
        {
            Log.LogInfo($"watch: stopped after {_watchTime:0.0}s");
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
        if (_watchTimer < TurboConfig.InspectorWatchInterval.Value) return;
        _watchTimer -= TurboConfig.InspectorWatchInterval.Value;
        _watchTime += TurboConfig.InspectorWatchInterval.Value;

        var sb = new StringBuilder(256);
        sb.Append($"watch t={_watchTime:0.0}s");
        foreach (KeyValuePair<string, Port> kvp in _watchedPorts)
        {
            sb.Append($"  {kvp.Key}={kvp.Value.Value:0.###}");
        }
        Log.LogInfo(sb.ToString());
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