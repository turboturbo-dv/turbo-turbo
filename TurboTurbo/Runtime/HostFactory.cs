using TurboTurbo.Profiles;

using UnityEngine;

namespace TurboTurbo.Runtime;

/// <summary>Attaches and tracks <see cref="EngineSimulationHost"/>s on cars.</summary>
internal static class HostFactory
{
    /// <summary>Attaches a configured host to <paramref name="car"/> and tracks it.</summary>
    public static EngineSimulationHost Create(TrainCar car, LocoProfile profile)
    {
        var host = car.gameObject.AddComponent<EngineSimulationHost>();
        host.Configure(profile);
        Orchestrator.Instance.Hosts.Add(host);
        return host;
    }

    /// <summary>Destroys <paramref name="existing"/> (if any), then attaches a configured host.</summary>
    public static EngineSimulationHost Replace(EngineSimulationHost existing, TrainCar car, LocoProfile profile)
    {
        if (existing != null) Object.Destroy(existing);
        return Create(car, profile);
    }
}
