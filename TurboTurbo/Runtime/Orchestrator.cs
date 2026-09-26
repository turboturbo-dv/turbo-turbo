using System.Collections.Generic;

using TurboTurbo.Profiles;

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
    private CarSpawner _hookedSpawner;
    private bool _loggedSpawnerLost;

    public List<EngineSimulationHost> Hosts { get; } = [];

    public bool Enabled { get; private set; } = true;


    public EngineSimulationHost FindHost(TrainCar car)
    {
        foreach (var host in Hosts)
        {
            if (host != null && host.gameObject == car.gameObject) return host;
        }

        return null;
    }

    public static Orchestrator Instance { get; private set; }

    public static Orchestrator Create()
    {
        // the game has SingletonBehaviour, which is probably what we want, but this works fine
        var go = new GameObject(Naming.Create("Orchestrator"));
        DontDestroyOnLoad(go);
        var orchestrator = go.AddComponent<Orchestrator>();
        Instance = orchestrator;
        return orchestrator;
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

    /// <summary>Reloads the profiles for every spawned car of the given livery.</summary>
    public void ReloadHostsForLivery(string liveryId)
    {
        if (!Enabled) return;

        var spawner = CarSpawner.Instance;
        if (spawner == null) return;

        foreach (var car in spawner.AllCars)
        {
            if (car != null && car.carLivery != null && car.carLivery?.id == liveryId) ReloadHost(car);
        }
    }

    /// <summary> Drops whatever host a car may have and reloads it from the repository. </summary>
    public void ReloadHost(TrainCar car)
    {
        if (car == null || car.carLivery == null || !Enabled) return;

        foreach (var host in Hosts.ToArray())
        {
            if (host != null && host.gameObject == car.gameObject)
            {
                Hosts.Remove(host);
                Destroy(host);
            }
        }

        Track(car);
    }

    /// <summary> Forgets about a host, without explicitly deleting it. </summary>
    public void Forget(EngineSimulationHost host)
    {
        var car = host.TrainCar;
        if (Hosts.Remove(host))
        {
            _log.Info($"forgot about {car.LogIdentifier()}");
        }
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

    private void Track(TrainCar car)
    {
        if (!Enabled) return;

        // this is a fresh instance, so edits to a host stay local to that host
        var matchingConfiguration = ProfileRepository.TryGetProfile(car);

        if (matchingConfiguration == null)
        {
            if (car.IsLoco)
            {
                _log.Info(
                    $"{car.LogIdentifier()} not configured, skipping");
            }
            return;
        }

        // ensures revived cars don't receive another host
        if (car.TryGetComponent<EngineSimulationHost>(out _))
        {
            _log.Info(
                $"{car.LogIdentifier()} already has a simulation host, skipping");
            return;
        }

        Attach(car, matchingConfiguration);
    }

    private void Attach(TrainCar car, LocoProfile matchingConfiguration)
    {
        _log.Info($"attaching simulation host to {car.LogIdentifier()}");

        // host is a component of the car so it dies along with it if the car is fully removed
        var host = car.gameObject.AddComponent<EngineSimulationHost>();
        host.Configure(matchingConfiguration);
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