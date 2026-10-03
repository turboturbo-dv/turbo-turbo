using System.Collections.Generic;
using System.Text;

using DV.Simulation.Cars;

using TurboTurbo.Runtime;

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
        sb.AppendLine($"car: {car.name} (id={car.DisplayId()}, type={car.carType})");

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

        var result = DieselEngineBinder.TryBind(sim.SimulationFlow);
        if (!result.IsSuccess)
        {
            sb.AppendLine($"sim: {result.Error}");
            return;
        }

        sb.AppendLine($"ports: {result.Value.Describe()}");
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
