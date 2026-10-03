using LocoSim.Implementations;

using UnityEngine;

namespace TurboTurbo.Runtime;

/// <summary>
/// A resolved diesel engine and the simulation ports TurboTurbo reads from it.
/// </summary>
internal sealed class DieselEngineBinding
{
    public DieselEngineBinding(
        DieselEngineDirect engine,
        Port throttlePort,
        Port rpmPort,
        Port fuelPort,
        Port engineOnPort)
    {
        Engine = engine;
        ThrottlePort = throttlePort;
        RpmPort = rpmPort;
        FuelPort = fuelPort;
        EngineOnPort = engineOnPort;
    }

    public DieselEngineDirect Engine { get; }

    public Port ThrottlePort { get; }

    public Port RpmPort { get; }

    public Port FuelPort { get; }

    public Port EngineOnPort { get; }

    public float ThrottleValue => ThrottlePort.Value;

    public float RpmNormalized => RpmPort.Value;

    public float FuelNormalized => FuelPort != null ? Mathf.Clamp01(FuelPort.Value) : 0f;

    public bool EngineRunning => EngineOnPort != null ? EngineOnPort.Value > 0.5f : RpmPort.Value > 0.05f;

    public static string DescribePort(Port port) => port != null ? port.id : "<missing>";

    /// <summary>Port dump for diagnostics.</summary>
    public string Describe() =>
        $"throttle={DescribePort(ThrottlePort)} rpm={DescribePort(RpmPort)} " +
        $"engineOn={DescribePort(EngineOnPort)} fuel={DescribePort(FuelPort)}";
}
