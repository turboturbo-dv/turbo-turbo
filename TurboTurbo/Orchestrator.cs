using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace TurboTurbo;

/// <summary>
/// Reacts to cars appearing in and leaving the world, and maintains the list
/// of tracked cars. Feature attachment (turbo, smoke) reads this list.
/// </summary>
internal sealed class Orchestrator : MonoBehaviour
{
    private bool _hooked;

    private CarSpawner Spawner { get; set; }

    public static Orchestrator Create(Logger log)
    {
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
        // CarSpawner lives in the game scene - wait for it to exist (the mod
        // loads before the game world does)
        var spawner = CarSpawner.Instance;
        if (spawner == null) return;
        
        Log.LogInfo($"[orchestrator] hooked {spawner}");

        spawner.CarSpawned += OnCarSpawned;
        spawner.CarAboutToBeDeleted += OnCarAboutToBeDeleted;
        _hooked = true;

        // snapshot cars that already exist (if scene was loaded before we hooked)
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
        // No teardown: pooled cars are only deactivated (CarSpawner parks them
        // under its transform), and the host's bindings (SimController /
        // SimulationFlow) survive the pool cycle - on revival Track() skips
        // re-attachment and the host simply resumes. Revisit if hosts ever
        // hold resources that do not survive pooling (game event
        // subscriptions, audio sources, ...). Fires before pooling, so this
        // is the right hook if teardown becomes necessary.
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

        // pooled cars fire CarSpawned again on revival - never attach twice
        if (car.TryGetComponent<Runtime.EngineSimulationHost>(out _))
        {
            Log?.LogInfo($"[orchestrator] '{car.name}' ({car.carType}, id={car.ID}) already has a simulation host - skipping");
            return;
        }

        Log?.LogInfo($"[orchestrator] attaching simulation host to '{car.name}' ({car.carType}, id={car.ID})");

        // the host lives on the car root, alongside SimController and DV's own
        // per-car runtime components (CarDamageModel, CarDebtController, ...);
        // it dies with the car and deactivates/reactivates with pool cycles
        car.gameObject.AddComponent<Runtime.EngineSimulationHost>()
                      .Configure(matchingConfiguration.Value, Log);
    }
}
