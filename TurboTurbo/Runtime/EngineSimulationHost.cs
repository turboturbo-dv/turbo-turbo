using System;
using System.Collections.Generic;
using System.Linq;

using DV.Simulation.Cars;

using HarmonyLib;

using LocoSim.Implementations;

using TurboTurbo.Assets;
using TurboTurbo.Modeling;
using TurboTurbo.Setup;
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

    private Port _throttlePort;
    private Func<float> _fuelNorm;
    private Func<bool> _engineOn;

    private readonly List<ParticleSystem> _replacedExhausts = new();

    public TurboModel TurboModel { get; private set; }
    public TrainCar TrainCar { get; private set; }
    public List<ExhaustEmitters> Exhausts { get; } = new();

    /// <summary>Exhaust flow range shared by this host's smoke and shimmer emitters.</summary>
    public ExhaustVelocitySettings Velocity { get; private set; }
    public bool Bound => _simBound && TurboModel != null;
    public bool EngineOn => TurboModel != null && _engineOn();
    public float AbsSpeed => TrainCar.GetAbsSpeed();
    public string CarId => TrainCar.ID;

    private void OnDestroy()
    {
        // restore replaced exhausts so a disabled mod leaves the car stock
        foreach (var replacedPs in _replacedExhausts)
        {
            if (replacedPs == null) continue;

            var emission = replacedPs.emission;
            emission.enabled = true;
        }

        // emitters are separate GameObjects under the car root, they do not
        // die with this component
        foreach (var e in Exhausts)
        {
            if (e.Smoke != null) Destroy(e.Smoke.gameObject);
            if (e.Shimmer != null) Destroy(e.Shimmer.gameObject);
        }
        Exhausts.Clear();

        Orchestrator.Instance?.Forget(this);
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

        if (TurboModel == null) return;

        var engineOn = _engineOn();
        TurboModel.Tick(Time.deltaTime, engineOn);

        // write the torque-capped demand back to the engine's throttle port,
        // this ensures the engine's power is limited by available air
        _throttlePort.Value = TurboModel.EffectiveDemand;

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
            if (TurboModel != null)
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

        TurboModel = new TurboModel(new TurboModel.Settings(_configuration.Turbo),
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

        TrainCar = GetComponent<TrainCar>();
        Exhausts.Clear();

        Velocity = new ExhaustVelocitySettings(_configuration.Velocity);

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
                Mouth = TrainCar.transform.InverseTransformPoint(exhaust.position),
                Offset = binding.Offset,
            };
            emitters.Smoke = CreateSmokeEmitter(i, exhaust.position, binding.Offset,
                new SmokeParticles.Settings(_configuration.SmokeEmitter),
                new ExhaustSmokeModel.Settings(_configuration.Smoke));
            emitters.Shimmer = CreateShimmerEmitter(i, exhaust.position, binding.Offset,
                new ShimmerParticles.Settings(_configuration.ShimmerEmitter));
            Exhausts.Add(emitters);
        }

        _effectsBound = true;
        _log.Info($"effects bound on '{name}' ({Exhausts.Count} exhaust emitter(s))");
    }

    private Transform ResolveExhaustTransform(ExhaustBinding binding)
    {
        if (binding.ParticleSystemSelector != null)
        {
            var existingPs = binding.ParticleSystemSelector(TrainCar);
            if (existingPs == null)
            {
                return null;
            }

            var existingEmission = existingPs.emission;
            existingEmission.enabled = false;
            _replacedExhausts.Add(existingPs);
            return existingPs.transform;
        }
        else
        {
            return binding.TransformSelector(TrainCar);
        }
    }

    private SmokeParticles CreateSmokeEmitter(int index, Vector3 exhaustPosition, Vector3 offset,
        SmokeParticles.Settings tuning, ExhaustSmokeModel.Settings smokeTuning)
    {
        var go = new GameObject($"TurboTurbo.Smoke[{index}]");
        ExhaustPlacement.PlaceAt(go.transform, exhaustPosition, TrainCar.transform, offset);
        var smoke = go.AddComponent<SmokeParticles>();
        smoke.tuning = tuning;
        smoke.Model.Tuning = smokeTuning;
        smoke.velocity = Velocity;
        smoke.shader = ModAssets.SmokeShader;
        smoke.atlas = GameAssets.SmokeAtlas;
        smoke.customSimulationSpace = WorldMover.OriginShiftParent;
        smoke.Configure();
        return smoke;
    }

    private ShimmerParticles CreateShimmerEmitter(int index, Vector3 exhaustPosition, Vector3 offset,
        ShimmerParticles.Settings tuning)
    {
        var go = new GameObject($"TurboTurbo.Shimmer[{index}]");
        ExhaustPlacement.PlaceAt(go.transform, exhaustPosition, TrainCar.transform, offset);
        var shimmer = go.AddComponent<ShimmerParticles>();
        shimmer.tuning = tuning;
        shimmer.velocity = Velocity;
        shimmer.shader = ModAssets.HeatShimmerShader;
        shimmer.customSimulationSpace = WorldMover.OriginShiftParent;
        shimmer.Configure();
        return shimmer;
    }

    private void UpdateEffects(bool engineOn)
    {
        var velocity = TrainCar.GetVelocity();
        var absSpeed = TrainCar.GetAbsSpeed();
        var heat = TurboModel.ExhaustHeat;

        foreach (var e in Exhausts)
        {
            var smoke = e.Smoke;
            smoke.lambda = TurboModel.Lambda;
            smoke.rpmNorm = TurboModel.RpmNorm;
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