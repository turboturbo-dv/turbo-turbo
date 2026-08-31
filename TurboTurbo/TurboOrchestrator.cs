using System.Collections.Generic;
using System.Linq;
using DV.Simulation.Cars;
using UnityEngine;

namespace TurboTurbo;

/// <summary>
/// Watches for diesel locomotives appearing in the world (spawned or
/// scene-loaded) and attaches the TurboModel to each one. Detached turbo
/// models are cleaned up when their car is destroyed.
/// </summary>
internal sealed class TurboOrchestrator : MonoBehaviour
{
    private const float ScanInterval = 2f;

    private readonly HashSet<TrainCar> _attached = new();
    private float _nextScan;

    private TurboOrchestrator() { }

    public static TurboOrchestrator Create()
    {
        var go = new GameObject("TurboTurbo.Orchestrator");
        Object.DontDestroyOnLoad(go);
        return go.AddComponent<TurboOrchestrator>();
    }

    private void OnEnable()
    {
        CarSpawner.Instance.CarSpawned += OnCarSpawned;
        CarSpawner.Instance.CarAboutToBeDeleted += OnCarAboutToBeDeleted;
    }

    private void OnDisable()
    {
        if (CarSpawner.Instance != null)
        {
            CarSpawner.Instance.CarSpawned -= OnCarSpawned;
            CarSpawner.Instance.CarAboutToBeDeleted -= OnCarAboutToBeDeleted;
        }
    }

    private void OnCarSpawned(TrainCar car)
    {
        TryAttach(car);
    }

    private void OnCarAboutToBeDeleted(TrainCar car)
    {
        _attached.Remove(car);
    }

    private void Update()
    {
        // periodic sweep: catches cars that were scene-loaded without a
        // CarSpawned event, and retries cars whose SimController wasn't
        // initialized yet at spawn time
        if (Time.time < _nextScan) return;
        _nextScan = Time.time + ScanInterval;

        foreach (var car in Object.FindObjectsOfType<TrainCar>())
        {
            TryAttach(car);
        }
    }

    private void TryAttach(TrainCar car)
    {
        if (car == null || _attached.Contains(car)) return;
        if (!TurboModel.IsTurboLoco(car.carType)) return;

        var simController = car.GetComponent<SimController>();
        var flow = simController != null ? simController.simFlow : null;
        if (flow == null) return; // sim not initialized yet; the sweep retries

        TurboModel.Attach(car, flow);
        _attached.Add(car);
    }
}
