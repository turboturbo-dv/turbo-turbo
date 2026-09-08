using System.Collections.Generic;

using UnityEngine;

namespace TurboTurbo.Runtime;

/// <summary>
/// Orchestrates the creation of <see cref="Runtime.EngineSimulationHost"/>s on cars that match the mod's configuration,
/// as configured on the <see cref="Controller"/>. Tracks the lifecycle of hosts and as well as the
/// <see cref="CarSpawner"/> to ensure that all spawned cars that match a configuration entry receive the correct
/// simulation host.
/// </summary>
internal sealed class Orchestrator : MonoBehaviour
{
    private readonly Logger _log = Log.ForContext("orchestrator");

    public List<Runtime.EngineSimulationHost> Hosts { get; } = [];

    public bool Enabled { get; private set; } = true;

    public void Forget(Runtime.EngineSimulationHost host)
    {
        var car = host.TrainCar;
        _log.Info($"forgetting about '{car.name}' ({car.carType}, id={car.ID})");
        Hosts.Remove(host);
    }

    private CarSpawner _hookedSpawner;
    private bool _loggedSpawnerLost;

    public static Orchestrator Instance { get; private set; }

    public static Orchestrator Create()
    {
        // the game has SingletonBehaviour, which is probably what we want, but this works fine
        var go = new GameObject("TurboTurbo.Orchestrator");
        DontDestroyOnLoad(go);
        var orchestrator = go.AddComponent<Orchestrator>();
        Instance = orchestrator;
        return orchestrator;
    }

    private void Update()
    {
        EnsureSpawnerHooked();
    }

    /// <summary>
    /// Self-healing spawner hook. The spawner is destroyed during a reload, this method registers when that happens and
    /// ensures the new spawner is hooked as soon as it appears.
    /// </summary>
    private void EnsureSpawnerHooked()
    {
        if (_hookedSpawner != null) return;

        if (!ReferenceEquals(_hookedSpawner, null) && !_loggedSpawnerLost)
        {
            _loggedSpawnerLost = true;
            _log.Info("car spawner lost, waiting for a new one");
        }

        var spawner = CarSpawner.Instance;
        if (spawner == null) return;

        _loggedSpawnerLost = false;
        Hook(spawner);
    }

    private void Hook(CarSpawner spawner)
    {
        _log.Info($"hooked {spawner}");

        spawner.CarSpawned += OnCarSpawned;
        spawner.CarAboutToBeDeleted += OnCarAboutToBeDeleted;
        _hookedSpawner = spawner;

        // if the spawner already has cars before we discover it, track those too.
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

    public void SetActive(bool isOn)
    {
        if (Enabled == isOn) return;

        Enabled = isOn;
        _log.Info($"{(isOn ? "enabled" : "disabled")}");

        if (isOn)
        {
            AttachToExistingCars();
        }
        else
        {
            TeardownHosts();
        }
    }

    private void AttachToExistingCars()
    {
        var spawner = CarSpawner.Instance;
        if (spawner == null)
        {
            _log.Info("no car spawner (yet), re-attach deferred to spawn events");
            return;
        }

        foreach (var car in spawner.AllCars)
        {
            Track(car);
        }
    }

    private void TeardownHosts()
    {
        // host OnDestroy restores vanilla exhausts and removes our emitters
        foreach (var host in Hosts.ToArray())
        {
            Destroy(host);
        }
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

        _log.Info($"about to be deleted '{car.name}' ({car.carType}, id={car.ID})");
    }

    private void Track(TrainCar car)
    {
        if (!Enabled) return;

        var matchingConfiguration = Controller.TryGetConfiguration(car);

        if (matchingConfiguration == null)
        {
            if (car.IsLoco)
            {
                _log.Info(
                    $"'{car.name}' ({car.carType}, id={car.ID} liv={car.carLivery.name} livid={car.carLivery.id})) not configured, skipping");
            }
            return;
        }

        // ensures revived cars don't receive another host
        if (car.TryGetComponent<Runtime.EngineSimulationHost>(out _))
        {
            _log.Info(
                $"'{car.name}' ({car.carType}, id={car.ID}, already has a simulation host, skipping");
            return;
        }

        _log.Info($"attaching simulation host to '{car.name}' ({car.carType}, id={car.ID})");

        // host is a component of the car so it dies along with it if the car is fully removed
        var host = car.gameObject.AddComponent<Runtime.EngineSimulationHost>();
        host.Configure(matchingConfiguration.Value);
        Hosts.Add(host);
    }

    /// <summary>
    /// Generate diagnostics, used in the <see cref="DevUI.TurboDevPanel"/>
    /// </summary>
    /// <returns></returns>
    public string DescribeDiagnostics()
    {
        var current = CarSpawner.Instance;
        var currentId = current != null ? current.GetInstanceID().ToString() : "none";
        string spawner;
        if (ReferenceEquals(_hookedSpawner, null))
        {
            spawner = $"not hooked (current spawner: {currentId})";
        }
        else
        {
            var alive = _hookedSpawner != null;
            var hookedId = alive ? _hookedSpawner.GetInstanceID().ToString() : "<destroyed>";
            var verdict = alive && current != null && current == _hookedSpawner ? "ok" : "mismatch";
            spawner = $"hooked to {hookedId}, current {currentId} ({verdict})";
        }

        return $"enabled: {Enabled}\nhosts: {Hosts.Count}\nspawner: {spawner}";
    }
}