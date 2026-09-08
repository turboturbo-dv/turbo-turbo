using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using DV.Simulation.Cars;

using LocoSim.Implementations;

using UnityEngine;

namespace TurboTurbo.DevUI;

/// <summary>
/// Dumps the details needed to write a Controller.ConfigureEngine entry for the player's train.
/// </summary>
internal static class PlayerTrainInspector
{
    private static readonly Logger Log = TurboTurbo.Log.ForContext("playerTrain");

    public static void DumpTarget()
    {
        var car = PlayerManager.Car ?? PlayerManager.LastLoco;
        if (car == null)
        {
            Log.Info("no player car to dump");
            return;
        }

        Dump(car);
    }

    public static void Dump(TrainCar car)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"car: {car.name} (id={car.ID}, type={car.carType})");

        var systems = car.GetComponentsInChildren<ParticleSystem>(true);
        sb.AppendLine($"particle systems: {systems.Length}");
        foreach (var ps in systems)
        {
            var main = ps.main;
            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            var texture = renderer != null && renderer.sharedMaterial != null
                ? renderer.sharedMaterial.mainTexture
                : null;
            sb.AppendLine($"- {ps.name} path={HierarchyPath(car.transform, ps.transform)} " +
                $"localPos={ps.transform.localPosition} localRot={ps.transform.localEulerAngles} " +
                $"emission={ps.emission.enabled} texture={(texture != null ? texture.name : "none")} " +
                $"startSpeed={main.startSpeed.constantMax} startLifetime={main.startLifetime.constantMax}");
        }

        DumpSim(sb, car);

        Log.Info(sb.ToString());
    }

    private static void DumpSim(StringBuilder sb, TrainCar car)
    {
        var sim = car.GetComponent<SimController>();
        if (sim == null || sim.SimulationFlow == null)
        {
            sb.AppendLine("sim: not bound (no SimController/SimulationFlow)");
            return;
        }

        var engine = sim.SimulationFlow.OrderedSimComps.OfType<DieselEngineDirect>().FirstOrDefault();
        if (engine == null)
        {
            sb.AppendLine("sim: no DieselEngineDirect");
            return;
        }

        var throttleRef = engine.GetAllPortReferences()
            .FirstOrDefault(r => r.id.EndsWith(".THROTTLE", StringComparison.OrdinalIgnoreCase));
        sb.AppendLine($"ports: throttle={(throttleRef != null ? throttleRef.id : "MISSING")} " +
            $"rpm={FindPort(engine, ".RPM_NORMALIZED")} " +
            $"engineOn={FindPort(engine, ".ENGINE_ON")} " +
            $"fuel={FindPort(engine, ".FUEL_CONSUMPTION_NORMALIZED")}");
    }

    private static string FindPort(DieselEngineDirect engine, string suffix)
    {
        var port = engine.GetAllPorts()
            .FirstOrDefault(p => p.id.EndsWith(suffix, StringComparison.OrdinalIgnoreCase));
        return port != null ? port.id : "MISSING";
    }

    private static string HierarchyPath(Transform root, Transform transform)
    {
        var names = new List<string>();
        for (var current = transform; current != null && current != root; current = current.parent)
        {
            names.Add(current.name);
        }
        names.Reverse();
        return string.Join("/", names);
    }
}