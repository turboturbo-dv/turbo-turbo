using System.Collections.Generic;
using UnityEngine;

namespace TurboTurbo;

internal sealed class Orchestrator : MonoBehaviour
{
    private bool _hooked;

    private static readonly List<Runtime.EngineSimulationHost> _hosts = new();

    internal static IReadOnlyList<Runtime.EngineSimulationHost> Hosts => _hosts;

    internal static void Forget(Runtime.EngineSimulationHost host)
    {
        var car = host.TrainCar;
        Main.Log.LogInfo($"[orchestrator] forgetting about '{car.name}' ({car.carType}, id={car.ID})");
        _hosts.Remove(host);
    }

    private CarSpawner Spawner { get; set; }

    public static Orchestrator Create(Logger log)
    {
        // the game has SingletonBehaviour, which is probably what we want, but this works fine
        var go = new GameObject("TurboTurbo.Orchestrator");
        DontDestroyOnLoad(go);
        var orchestrator = go.AddComponent<Orchestrator>();
        orchestrator.Log = log;
        return orchestrator;
    }

    private Logger Log { get; set; }

    private void Update()
    {
        if (!_hooked)
        {
            Hook();
        }
    }

    private void Hook()
    {
        // CarSpawner lives in the game scene, wait for it to exist
        var spawner = CarSpawner.Instance;
        if (spawner == null) return;
        
        Log.LogInfo($"[orchestrator] hooked {spawner}");

        spawner.CarSpawned += OnCarSpawned;
        spawner.CarAboutToBeDeleted += OnCarAboutToBeDeleted;
        _hooked = true;

        // this probably doesn't happen, but if the spawner already has cars before we discover it, we should track those too.
        // slight race condition here, but track is idempotent so that's fine
        foreach (var car in spawner.AllCars)
        {
            Track(car);
        }
    }

    private void OnCarSpawned(TrainCar car)
    {
        Track(car);
    }

    private void OnCarAboutToBeDeleted(TrainCar car)
    {
        // we really don't need to do anything on delete, if the car is revived from the pool
        // the host should just come back to life with it. Still log a bit in case we run into weird issues here.
        var matchingConfiguration = Controller.TryGetConfiguration(car);

        if (matchingConfiguration == null)
        {
            return;
        }

        Log?.LogInfo($"[orchestrator] about to be deleted '{car.name}' ({car.carType}, id={car.ID})");
    }

    private void Track(TrainCar car)
    {
        var matchingConfiguration = Controller.TryGetConfiguration(car);

        if (matchingConfiguration == null)
        {
            return;
        }

        // ensures revived cars don't receive another host
        if (car.TryGetComponent<Runtime.EngineSimulationHost>(out _))
        {
            Log?.LogInfo($"[orchestrator] '{car.name}' ({car.carType}, id={car.ID}) already has a simulation host - skipping");
            return;
        }

        Log?.LogInfo($"[orchestrator] attaching simulation host to '{car.name}' ({car.carType}, id={car.ID})");

        // host is a component of the car so it dies along with it if the car is fully removed
        var host = car.gameObject.AddComponent<Runtime.EngineSimulationHost>();
        host.Configure(matchingConfiguration.Value, Log);
        _hosts.Add(host);
    }
}
