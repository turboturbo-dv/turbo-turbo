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
/// Per-car runtime host: attached to the car's root GameObject by the
/// Orchestrator when a matching EngineConfiguration exists, living alongside
/// DV's own per-car runtime components (SimController, CarDamageModel, ...).
/// Waits for the car's simulation to initialize, then binds the configured
/// features to it.
///
/// Survives car pool cycles deactivated and resumes on revival; the sim
/// bindings stay valid because SimController/SimulationFlow are never
/// rebuilt for pooled cars.
/// </summary>
internal sealed class EngineSimulationHost : MonoBehaviour
{
    /// <summary>Pair of emitters sharing one exhaust position.</summary>
    private sealed class ExhaustEmitters
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

    public EngineSimulationHost Configure(EngineConfiguration configuration, Logger log)
    {
        _configuration = configuration;
        _log = log;
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

        // write the torque-capped demand back to the engine's throttle port;
        // the upstream throttle chain re-propagates the player's lever before
        // we read again, so this only caps the engine, it never sticks
        _throttlePort.Value = _turboModel.EffectiveDemand;

        if (_effectsBound)
        {
            UpdateEffects(engineOn);
        }
    }

    /// <summary>
    /// The car's SimulationFlow only exists after SimController.Initialize has
    /// run - retry until it does, then bind the configured features.
    /// </summary>
    private void TryBindSimulation()
    {
        // the host lives on the car root, so plain GetComponent finds the
        // car's SimController (same lookup TrainCar.SimController does)
        _simController = GetComponent<SimController>();
        if (_simController == null || _simController.SimulationFlow == null)
        {
            if (!_loggedNoSim)
            {
                _loggedNoSim = true;
                _log.LogInfo($"[host] no SimController (yet) on '{name}'");
            }
            return;
        }

        _simBound = true;
        _log.LogInfo($"[host] sim bound on '{name}' ({_configuration.HasTurbo} turbo, " +
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
            _log.LogWarning($"[host] no DieselEngineDirect on '{name}' - turbo not bound");
            return;
        }

        // THROTTLE is a PortReference - the actual Port hangs off a private field
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
            _log.LogWarning($"[host] could not resolve throttle/rpm ports on '{name}' - turbo not bound");
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

        _log.LogInfo($"[host] turbo bound on '{name}' (throttle: {_throttlePort.id}, " +
                     $"fuel: {(fuelPort != null ? fuelPort.id : "MISSING")})");
    }

    // ------------------------------------------------------------------
    // exhaust effects
    // ------------------------------------------------------------------

    /// <summary>
    /// Spawns the smoke + shimmer emitters on each configured exhaust
    /// transform and takes over the vanilla exhaust smoke. Runs once; the
    /// emitters live under the car root and survive pool cycles.
    /// </summary>
    private void TryBindEffects()
    {
        _trainCar = GetComponent<TrainCar>();
        _exhausts.Clear();

        for (int i = 0; i < _configuration.ExhaustPositionSelectors.Count; i++)
        {
            Transform exhaust = _configuration.ExhaustPositionSelectors[i](_trainCar);
            if (exhaust == null)
            {
                _log.LogWarning($"[host] exhaust selector {i} resolved to null on '{name}' - skipping");
                continue;
            }

            // vanilla takeover: our emitters replace the vanilla smoke; the
            // vanilla PS stays in place as the position/orientation donor
            ParticleSystem vanillaPs = exhaust.GetComponent<ParticleSystem>();
            if (vanillaPs != null)
            {
                var vanillaEmission = vanillaPs.emission;
                vanillaEmission.enabled = false;
            }
            else
            {
                _log.LogWarning($"[host] exhaust selector {i} ('{exhaust.name}') has no ParticleSystem on '{name}'");
            }

            Texture atlas = vanillaPs != null
                ? vanillaPs.GetComponent<ParticleSystemRenderer>()?.sharedMaterial?.mainTexture
                : null;

            _exhausts.Add(new ExhaustEmitters
            {
                Smoke = CreateSmokeEmitter(i, exhaust, atlas),
                Shimmer = CreateShimmerEmitter(i, exhaust),
            });
        }

        _effectsBound = true;
        _log.LogInfo($"[host] effects bound on '{name}' ({_exhausts.Count} exhaust emitter(s))");
    }

    private SmokeParticles CreateSmokeEmitter(int index, Transform exhaust, Texture atlas)
    {
        var go = new GameObject($"TurboTurbo.Smoke[{index}]");
        PlaceAtExhaust(go.transform, exhaust);
        var smoke = go.AddComponent<SmokeParticles>();
        smoke.shaderOverride = ModAssets.SmokeShader;
        smoke.atlasOverride = atlas;
        return smoke;
    }

    private ShimmerParticles CreateShimmerEmitter(int index, Transform exhaust)
    {
        var go = new GameObject($"TurboTurbo.Shimmer[{index}]");
        PlaceAtExhaust(go.transform, exhaust);
        var shimmer = go.AddComponent<ShimmerParticles>();
        shimmer.shaderOverride = ModAssets.HeatShimmerShader;
        return shimmer;
    }

    private void PlaceAtExhaust(Transform t, Transform exhaust)
    {
        // shared car-local placement (see ExhaustPlacement for the
        // rationale); offset is the car-type's ExhaustSpawnOffset
        ExhaustPlacement.PlaceAt(t, exhaust.position, _trainCar.transform,
            _configuration.ExhaustSpawnOffset);
    }

    private void UpdateEffects(bool engineOn)
    {
        Vector3 velocity = _trainCar != null ? _trainCar.GetVelocity() : Vector3.zero;
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

            // disabling the component stops emission while the ParticleSystem
            // keeps simulating, so lingering shimmer particles finish their
            // decay envelope instead of popping out of existence
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
