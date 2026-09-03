using System;
using System.Collections.Generic;
using System.Linq;

using DV.Simulation.Cars;

using HarmonyLib;

using LocoSim.Implementations;

using TurboTurbo.Modeling;
using TurboTurbo.WorkBench;

using UnityEngine;

namespace TurboTurbo.Runtime;

/// <summary>
/// Per-car runtime host, attached to the car's root GameObject by the
/// Orchestrator when a matching EngineConfiguration exists, living alongside
/// DV's own per-car runtime components.
/// Waits for the car's simulation to initialize, then binds the configured
/// features to it.
/// </summary>
internal sealed class EngineSimulationHost : MonoBehaviour
{
    /// <summary>Pair of emitters sharing one exhaust position.</summary>
    internal sealed class ExhaustEmitters
    {
        public SmokeParticles Smoke;
        public ShimmerParticles Shimmer;
    }

    private EngineConfiguration _configuration;
    private Logger _log;
    private SimController _simController;
    private bool _simBound;
    private bool _loggedNoSim;

    private TurboModel _turboModel;
    private Port _throttlePort;
    private Func<float> _fuelNorm;
    private Func<bool> _engineOn;

    private TrainCar _trainCar;
    private readonly List<ExhaustEmitters> _exhausts = new();
    private bool _effectsBound;

    internal TurboModel TurboModel => _turboModel;
    internal TrainCar TrainCar => _trainCar;
    internal IReadOnlyList<ExhaustEmitters> Exhausts => _exhausts;
    internal bool Bound => _simBound && _turboModel != null;
    internal bool EngineOn => _turboModel != null && _engineOn();
    internal string CarId => _trainCar.ID;

    private void OnDestroy()
    {
        Orchestrator.Instance.Forget(this);
    }

    public EngineSimulationHost Configure(EngineConfiguration configuration)
    {
        _configuration = configuration;

        // per-car context: logs from multiple locos stay distinguishable
        var car = GetComponent<TrainCar>();
        _log = Log.ForContext(car != null ? $"host:{car.ID}" : "host");

        return this;
    }

    private void Update()
    {
        if (!_simBound)
        {
            TryBindSimulation();
            return;
        }

        if (_turboModel == null) return;

        bool engineOn = _engineOn();
        _turboModel.Tick(Time.deltaTime, _fuelNorm(), engineOn);

        // write the torque-capped demand back to the engine's throttle port,
        // this ensures the engine's power is limited by available air
        _throttlePort.Value = _turboModel.EffectiveDemand;

        if (_effectsBound)
        {
            UpdateEffects(engineOn);
        }
    }

    private void TryBindSimulation()
    {
        _simController = GetComponent<SimController>();
        if (_simController == null || _simController.SimulationFlow == null)
        {
            if (!_loggedNoSim)
            {
                _loggedNoSim = true;
                _log.Info($"no SimController (yet) on '{name}'");
            }
            return;
        }

        _simBound = true;
        _log.Info($"sim bound on '{name}' ({_configuration.HasTurbo} turbo, " +
                     $"{_configuration.ExhaustPositionSelectors.Count} exhaust selector(s))");

        if (_configuration.HasTurbo)
        {
            TryBindTurbo();

            // effects are driven from the turbo model's signals, so only bind
            // them when binding the turbo model succeeds.
            if (_turboModel != null)
            {
                TryBindEffects();
            }
        }
    }

    private void TryBindTurbo()
    {
        var flow = _simController.SimulationFlow;
        var engine = flow.OrderedSimComps.OfType<DieselEngineDirect>().FirstOrDefault();
        if (engine == null)
        {
            _log.Warn($"no DieselEngineDirect on '{name}' - turbo not bound");
            return;
        }

        // throttle is a port reference, the actual port hangs off a private field
        PortReference throttleRef = engine.GetAllPortReferences()
            .FirstOrDefault(r => r.id.EndsWith(".THROTTLE", StringComparison.OrdinalIgnoreCase));
        _throttlePort = throttleRef != null
            ? Traverse.Create(throttleRef).Field("port").GetValue<Port>()
            : null;

        Port rpmPort = engine.GetAllPorts()
            .FirstOrDefault(p => p.id.EndsWith(".RPM_NORMALIZED", StringComparison.OrdinalIgnoreCase));
        Port fuelPort = engine.GetAllPorts()
            .FirstOrDefault(p => p.id.EndsWith(".FUEL_CONSUMPTION_NORMALIZED", StringComparison.OrdinalIgnoreCase));
        Port engineOnPort = engine.GetAllPorts()
            .FirstOrDefault(p => p.id.EndsWith(".ENGINE_ON", StringComparison.OrdinalIgnoreCase));

        if (_throttlePort == null || rpmPort == null)
        {
            _log.Warn($"could not resolve throttle/rpm ports on '{name}' - turbo not bound");
            return;
        }

        // suppliers: the model reads the ports through these closures
        _fuelNorm = fuelPort != null
            ? () => Mathf.Clamp01(fuelPort.Value)
            : () => 0f;
        _engineOn = engineOnPort != null
            ? () => engineOnPort.Value > 0.5f
            : () => rpmPort.Value > 0.05f;

        _turboModel = new TurboModel(new TurboModel.Settings(),
            () => _throttlePort.Value,
            () => rpmPort.Value);

        _log.Info($"turbo bound on '{name}' (throttle: {_throttlePort.id}, " +
                     $"fuel: {(fuelPort != null ? fuelPort.id : "MISSING")})");
    }

    /// <summary>
    /// Spawns the smoke + shimmer emitters on each configured exhaust
    /// transform and takes over the vanilla exhaust smoke. Runs once, the
    /// emitters live under the car root and /should/ survive pool cycles.
    /// </summary>
    private void TryBindEffects()
    {
        ModAssets.EnsureLoaded();

        _trainCar = GetComponent<TrainCar>();
        _exhausts.Clear();

        for (int i = 0; i < _configuration.ExhaustPositionSelectors.Count; i++)
        {
            Transform exhaust = _configuration.ExhaustPositionSelectors[i](_trainCar);
            if (exhaust == null)
            {
                _log.Warn($"exhaust selector {i} resolved to null on '{name}' - skipping");
                continue;
            }

            ParticleSystem vanillaPs = exhaust.GetComponent<ParticleSystem>();
            if (vanillaPs != null)
            {
                var vanillaEmission = vanillaPs.emission;
                vanillaEmission.enabled = false;
            }
            else
            {
                _log.Warn($"exhaust selector {i} ('{exhaust.name}') has no ParticleSystem on '{name}'");
            }

            Texture atlas = vanillaPs != null
                ? vanillaPs.GetComponent<ParticleSystemRenderer>()?.sharedMaterial?.mainTexture
                : null;

            Transform simSpace = WorldMover.OriginShiftParent;
            _exhausts.Add(new ExhaustEmitters
            {
                Smoke = CreateSmokeEmitter(i, exhaust, atlas, simSpace),
                Shimmer = CreateShimmerEmitter(i, exhaust, simSpace),
            });
        }

        _effectsBound = true;
        _log.Info($"effects bound on '{name}' ({_exhausts.Count} exhaust emitter(s))");
    }

    private SmokeParticles CreateSmokeEmitter(int index, Transform exhaust, Texture atlas, Transform simSpace)
    {
        var go = new GameObject($"TurboTurbo.Smoke[{index}]");
        PlaceAtExhaust(go.transform, exhaust);
        var smoke = go.AddComponent<SmokeParticles>();
        smoke.shader = ModAssets.SmokeShader;
        smoke.atlas = atlas;
        smoke.customSimulationSpace = simSpace;
        smoke.Configure();
        return smoke;
    }

    private ShimmerParticles CreateShimmerEmitter(int index, Transform exhaust, Transform simSpace)
    {
        var go = new GameObject($"TurboTurbo.Shimmer[{index}]");
        PlaceAtExhaust(go.transform, exhaust);
        var shimmer = go.AddComponent<ShimmerParticles>();
        shimmer.shader = ModAssets.HeatShimmerShader;
        shimmer.customSimulationSpace = simSpace;
        shimmer.Configure();
        return shimmer;
    }

    private void PlaceAtExhaust(Transform t, Transform exhaust)
    {
        // TODO: this needs some work
        ExhaustPlacement.PlaceAt(t, exhaust.position, _trainCar.transform,
            _configuration.ExhaustSpawnOffset);
    }

    private void UpdateEffects(bool engineOn)
    {
        Vector3 velocity = _trainCar.GetVelocity();
        float heat = _turboModel.ExhaustHeat;

        foreach (ExhaustEmitters e in _exhausts)
        {
            SmokeParticles smoke = e.Smoke;
            smoke.lambda = _turboModel.Lambda;
            smoke.demand = _turboModel.Demand;
            smoke.rpmNorm = _turboModel.RpmNorm;
            smoke.heat = heat;
            smoke.engineOn = engineOn;
            smoke.locoVelocity = velocity;

            ShimmerParticles shimmer = e.Shimmer;
            shimmer.enabled = engineOn;
            if (engineOn)
            {
                shimmer.SetFlow(heat);
                shimmer.locoVelocity = velocity;
            }
        }
    }
}