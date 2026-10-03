using System;
using System.Linq;

using DV.Simulation.Cars;

using HarmonyLib;

using LocoSim.Implementations;

namespace TurboTurbo.Runtime;

/// <summary>
/// Single source of truth for locating a car's diesel engine and resolving its simulation ports.
/// </summary>
internal static class DieselEngineBinder
{
    private const string ThrottleSuffix = ".THROTTLE";
    private const string RpmSuffix = ".RPM_NORMALIZED";
    private const string FuelSuffix = ".FUEL_CONSUMPTION_NORMALIZED";
    private const string EngineOnSuffix = ".ENGINE_ON";

    /// <summary>
    /// Resolves a diesel engine binding from a simulation flow.
    /// Returns an error if the flow does not hold a usable diesel engine.
    /// </summary>
    public static Result<DieselEngineBinding> TryBind(SimulationFlow flow)
    {
        if (flow == null) return new Error("no simulation flow");

        var engine = flow.OrderedSimComps.OfType<DieselEngineDirect>().FirstOrDefault();
        if (engine == null) return new Error("simulation flow has no DieselEngineDirect");

        // throttle is a port reference, the actual port hangs off an internal field
        var throttleRef = engine.GetAllPortReferences()
            .FirstOrDefault(r => r.id.EndsWith(ThrottleSuffix, StringComparison.OrdinalIgnoreCase));
        var throttlePort = throttleRef != null
            ? Traverse.Create(throttleRef).Field("port").GetValue<Port>()
            : null;

        var rpmPort = FindPort(engine, RpmSuffix);
        if (throttlePort == null) return new Error("engine has no throttle port");
        if (rpmPort == null) return new Error("engine has no RPM port");

        return new DieselEngineBinding(
            engine,
            throttlePort,
            rpmPort,
            FindPort(engine, FuelSuffix),
            FindPort(engine, EngineOnSuffix));
    }

    /// <summary>
    /// True when the car's diesel engine can be bound.
    /// </summary>
    public static bool Supports(TrainCar car)
    {
        if (car == null) return false;

        var sim = car.GetComponent<SimController>();
        if (sim == null) return false;

        return TryBind(sim.SimulationFlow).IsSuccess;
    }

    private static Port FindPort(DieselEngineDirect engine, string suffix) =>
        engine.GetAllPorts().FirstOrDefault(p => p.id.EndsWith(suffix, StringComparison.OrdinalIgnoreCase));
}
