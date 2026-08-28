using System.Linq;
using BepInEx.Configuration;
using BepInEx.Logging;
using DV.Simulation.Cars;
using DV.ThingTypes;
using HarmonyLib;
using LocoSim.Definitions;
using LocoSim.Implementations;

namespace TurboTurbo;

/// <summary>
/// Dumps the sim component graph (components, ports, connections, fuses)
/// of spawned cars to the log, to map per-loco port IDs for the turbo model.
/// </summary>
internal static class SimInspector
{
    internal static ConfigEntry<bool> Enabled;
    internal static ConfigEntry<bool> DieselOnly;

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
        _log = Logger.CreateLogSource("SimInspector");
    }

    internal static bool ShouldDump(TrainCar car)
    {
        if (car == null || !Enabled.Value) return false;
        return !DieselOnly.Value || DieselTypes.Contains(car.carType);
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
        }

        if (flow.AllFuses.Count > 0)
        {
            string fuses = string.Join(", ", flow.AllFuses.Select(f => $"{f.id}={(f.State ? "on" : "off")}"));
            _log.LogInfo($"fuses: {fuses}");
        }
    }
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
