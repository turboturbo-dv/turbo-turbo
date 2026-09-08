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
        public Vector3 Mouth;
        public Vector3 Offset;
        public SmokeParticles Smoke;
        public ShimmerParticles Shimmer;

        /// <summary>Repositions both emitters to Mouth + Offset.</summary>
        public void Reposition()
        {
            var local = Mouth + Offset;
            Smoke.transform.localPosition = local;
            Shimmer.transform.localPosition = local;
        }
    }
    private Logger _log;
    private bool _simBound;
    private bool _loggedNoSim;
    private bool _effectsBound;

    private EngineConfiguration _configuration;
    private SimController _simController;
    private TurboModel _turboModel;

    private Port _throttlePort;
    private Func<float> _fuelNorm;
    private Func<bool> _engineOn;

    private TrainCar _trainCar;
    private readonly List<ExhaustEmitters> _exhausts = new();

    public TurboModel TurboModel => _turboModel;
    public TrainCar TrainCar => _trainCar;
    public IReadOnlyList<ExhaustEmitters> Exhausts => _exhausts;
    public bool Bound => _simBound && _turboModel != null;
    public bool EngineOn => _turboModel != null && _engineOn();
    public float AbsSpeed => _trainCar.GetAbsSpeed();
    public string CarId => _trainCar.ID;

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

        var engineOn = _engineOn();
        _turboModel.Tick(Time.deltaTime, engineOn);

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
                     $"{_configuration.Exhausts.Count} exhaust(s))");

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
        var throttleRef = engine.GetAllPortReferences()
            .FirstOrDefault(r => r.id.EndsWith(".THROTTLE", StringComparison.OrdinalIgnoreCase));
        _throttlePort = throttleRef != null
            ? Traverse.Create(throttleRef).Field("port").GetValue<Port>()
            : null;

        var rpmPort = engine.GetAllPorts()
            .FirstOrDefault(p => p.id.EndsWith(".RPM_NORMALIZED", StringComparison.OrdinalIgnoreCase));
        var fuelPort = engine.GetAllPorts()
            .FirstOrDefault(p => p.id.EndsWith(".FUEL_CONSUMPTION_NORMALIZED", StringComparison.OrdinalIgnoreCase));
        var engineOnPort = engine.GetAllPorts()
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
    /// Spawns the smoke + shimmer emitters for each configured exhaust.
    /// </summary>
    private void TryBindEffects()
    {
        ModAssets.EnsureLoaded();
        GameAssets.EnsureLoaded();

        _trainCar = GetComponent<TrainCar>();
        _exhausts.Clear();

        var exhausts = _configuration.Exhausts;
        for (var i = 0; i < exhausts.Count; i++)
        {
            var binding = exhausts[i];

            var exhaust = ResolveExhaustTransform(binding);
            if (exhaust == null)
            {
                _log.Warn($"exhaust selector {i} resolved to null on '{name}', skipping");
                continue;
            }

            var emitters = new ExhaustEmitters
            {
                Mouth = _trainCar.transform.InverseTransformPoint(exhaust.position),
                Offset = binding.Offset,
            };
            emitters.Smoke = CreateSmokeEmitter(i, exhaust.position, binding.Offset);
            emitters.Shimmer = CreateShimmerEmitter(i, exhaust.position, binding.Offset);
            _exhausts.Add(emitters);
        }

        _effectsBound = true;
        _log.Info($"effects bound on '{name}' ({_exhausts.Count} exhaust emitter(s))");
    }

    private Transform ResolveExhaustTransform(ExhaustBinding binding)
    {
        if (binding.ParticleSystemSelector != null)
        {
            var vanillaPs = binding.ParticleSystemSelector(_trainCar);
            if (vanillaPs == null)
            {
                return null;
            }

            var vanillaEmission = vanillaPs.emission;
            vanillaEmission.enabled = false;
            return vanillaPs.transform;
        }
        else
        {
            return binding.TransformSelector(_trainCar);
        }
    }

    private SmokeParticles CreateSmokeEmitter(int index, Vector3 exhaustPosition, Vector3 offset)
    {
        var go = new GameObject($"TurboTurbo.Smoke[{index}]");
        ExhaustPlacement.PlaceAt(go.transform, exhaustPosition, _trainCar.transform, offset);
        var smoke = go.AddComponent<SmokeParticles>();
        smoke.shader = ModAssets.SmokeShader;
        smoke.atlas = GameAssets.SmokeAtlas;
        smoke.customSimulationSpace = WorldMover.OriginShiftParent;
        smoke.Configure();
        return smoke;
    }

    private ShimmerParticles CreateShimmerEmitter(int index, Vector3 exhaustPosition, Vector3 offset)
    {
        var go = new GameObject($"TurboTurbo.Shimmer[{index}]");
        ExhaustPlacement.PlaceAt(go.transform, exhaustPosition, _trainCar.transform, offset);
        var shimmer = go.AddComponent<ShimmerParticles>();
        shimmer.shader = ModAssets.HeatShimmerShader;
        shimmer.customSimulationSpace = WorldMover.OriginShiftParent;
        shimmer.Configure();
        return shimmer;
    }

    private void UpdateEffects(bool engineOn)
    {
        var velocity = _trainCar.GetVelocity();
        var absSpeed = _trainCar.GetAbsSpeed();
        var heat = _turboModel.ExhaustHeat;

        foreach (var e in _exhausts)
        {
            var smoke = e.Smoke;
            smoke.lambda = _turboModel.Lambda;
            smoke.rpmNorm = _turboModel.RpmNorm;
            smoke.heat = heat;
            smoke.engineOn = engineOn;
            smoke.locoVelocity = velocity;
            smoke.absSpeed = absSpeed;

            var shimmer = e.Shimmer;
            shimmer.enabled = engineOn;
            if (engineOn)
            {
                shimmer.SetFlow(heat);
                shimmer.locoVelocity = velocity;
                shimmer.absSpeed = absSpeed;
            }
        }
    }
}