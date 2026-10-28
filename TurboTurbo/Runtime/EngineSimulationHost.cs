using System.Collections.Generic;

using DV.Simulation.Cars;

using TurboTurbo.Assets;
using TurboTurbo.Configuration;
using TurboTurbo.Effects;
using TurboTurbo.Modeling;
using TurboTurbo.Profiles;
using TurboTurbo.Runtime.Environment;

using UnityEngine;

namespace TurboTurbo.Runtime;

/// <summary>
/// Host for all runtime behaviour on a single locomotive. It is meant to be attached
/// to a locomotive's root game object. The host will automatically attach its simulation
/// and effects once the car is live.
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
        public LocoExhaust Source;

        /// <summary>Car-local position both emitters are placed at.</summary>
        public Vector3 LocalPosition => Mouth + Offset;

        /// <summary>Places both emitters at <see cref="LocalPosition"/>.</summary>
        public void Reposition()
        {
            Smoke.transform.localPosition = LocalPosition;
            Shimmer.transform.localPosition = LocalPosition;
        }
    }

    private const float RunningEngineTemperatureK = 80f + PhysicsConstants.KelvinOffset;

    private static WindFrame _windFrame;
    private static WindFrame SharedWindFrame
    {
        get
        {
            if (_windFrame == null) _windFrame = WindFrame.Create(WorldMover.OriginShiftParent);
            return _windFrame;
        }
    }

    private Logger _log;
    private bool _simBound;
    private bool _loggedNoSim;
    private bool _tracksAmbient;

    private SimController _simController;
    private DieselEngineBinding _engine;

    private readonly List<ParticleSystem> _replacedExhausts = new();

    public CombustionModel CombustionModel { get; private set; }
    public StackModel Stack { get; private set; }

    public TrainCar TrainCar { get; private set; }
    public List<ExhaustEmitters> Exhausts { get; } = new();

    /// <summary>Exhaust flow range shared by this host's smoke and shimmer emitters.</summary>
    public ExhaustVelocitySettings Velocity { get; private set; }

    /// <summary>The profile this host is running.</summary>
    public LocoProfile Profile { get; private set; }

    public bool Bound => _simBound && CombustionModel != null;

    /// <summary>True once the exhaust emitters have been built from the profile.</summary>
    public bool EffectsBound { get; private set; }

    public bool EngineOn => CombustionModel != null && _engine.EngineRunning;

    public float AbsSpeed => TrainCar.GetAbsSpeed();

    public string CarId => TrainCar.DisplayId();

    public EngineSimulationHost Configure(LocoProfile configuration)
    {
        Profile = configuration;

        var car = GetComponent<TrainCar>();
        TrainCar = car;
        _log = Log.ForContext(car != null ? $"host:{car.LogIdentifier()}" : "host");

        return this;
    }

    private void Update()
    {
        if (!_simBound)
        {
            TryBindSimulation();
            return;
        }

        if (CombustionModel == null) return;

        var engineOn = _engine.EngineRunning;
        var cranking = !engineOn && _engine.Cranking;

        // the model fuels the engine while cranking, so the exhaust behaves as if it is running
        var firing = engineOn || cranking;

        var ambient = AmbientTemperature.Kelvin;

        // if the engine has never fired, make it follow the ambient temperature directly, without thermal lag.
        // this is required mainly because on load, Seasons initialises after locomotives have already spawned,
        // so they tend to start out on the wrong temperature. it also smooths out some wrinkles in a few other
        // edge cases.
        if (firing) _tracksAmbient = false;
        if (_tracksAmbient)
        {
            ApplyTemperaturesK(ambient);
        }

        CombustionModel.Tick(Time.deltaTime, engineOn, cranking);

        var unburned = CombustionModel.UnburnedFuel;
        var flowNorm = CombustionModel.MassFlow / Mathf.Max(1e-4f, CombustionModel.Charger.RatedCharge);
        Stack.Tick(Time.deltaTime, firing, unburned, flowNorm, CombustionModel.GasTemperature, ambient);

        // TODO: properly attach the combustion model to the simulation graph.
        // then we can apply the combustion model's torque limit, but that
        // will also involve tuning the engine torque curves, which has gameplay
        // implications. For now, keep it purely visual.
        // When that lands, the model should expose the limited value already in
        // port units (writable straight to the governor port), so the adapter
        // does not have to invert the game's fuel-per-stroke mapping itself.

        if (EffectsBound)
        {
            UpdateEffects(firing);
        }
    }

    /// <summary>Sets all engine component temperatures to the given value.</summary>
    public void SetTemperaturesK(float kelvin)
    {
        if (!Bound) return;
        _tracksAmbient = false;
        ApplyTemperaturesK(kelvin);
    }

    private void ApplyTemperaturesK(float kelvin)
    {
        CombustionModel.SetTemperatureK(kelvin);
        Stack.SetTemperatureK(kelvin);
    }

    private void TryBindSimulation()
    {
        _simController = GetComponent<SimController>();
        if (_simController == null || _simController.SimulationFlow == null)
        {
            if (!_loggedNoSim)
            {
                _loggedNoSim = true;
                _log.Info("no SimController (yet)");
            }
            return;
        }

        _simBound = true;
        _log.Info($"sim bound ({Profile.ChargerKind} charger, " +
                     $"{Profile.Exhausts.Count} exhausts)");

        TryBindCombustion();

        // effects are driven from the engine model's signals, so only bind
        // them when binding the model succeeds.
        if (CombustionModel != null)
        {
            TryBindEffects();
        }
    }

    private void TryBindCombustion()
    {
        var flow = _simController.SimulationFlow;
        var result = DieselEngineBinder.TryBind(flow);
        if (!result.IsSuccess)
        {
            _log.Warn($"engine model not bound: {result.Error}");
            return;
        }

        _engine = result.Value;

        // binding to a locomotive happens infrequently, and it is important that the models are initialised
        // to the correct temperature, so the cost of calling Refresh() is acceptable here.
        // we want to avoid reading a stale temperature here, especially if it happens just after a save (re)loads,
        // which could result in the stale temperature being wildly off and the engine block spawning way too hot or cold.
        AmbientTemperature.Refresh();
        if (AmbientTemperature.Source == AmbientSource.None)
        {
            _log.Warn("failed to read temperature from seasons or builtin, falling back to 20C");
        }

        // a loco whose engine is already running must not come up stone cold
        float? initialTemperatureK = _engine.EngineRunning ? RunningEngineTemperatureK : null;

        // suppliers: the model reads the ports through these closures
        CombustionModel = new CombustionModel(
            () => _engine.ThrottleValue,
            () => _engine.FuelNormalized,
            () => _engine.RpmNormalized,
            () => AmbientTemperature.Kelvin,
            Profile.BuildCharger(),
            Profile.Combustion,
            Profile.Velocity,
            initialTemperatureK);

        Stack = new StackModel(Profile.Stack, AmbientTemperature.Kelvin);
        _tracksAmbient = !_engine.EngineRunning;

        _log.Info($"combustion bound (throttle: {_engine.ThrottlePort.id}, " +
                     $"fuel: {DieselEngineBinding.DescribePort(_engine.FuelPort)})");
    }

    /// <summary>
    /// Spawns the smoke + shimmer emitters for each configured exhaust.
    /// </summary>
    private void TryBindEffects()
    {
        ModAssets.EnsureLoaded();
        GameAssets.EnsureLoaded();

        Exhausts.Clear();

        Velocity = Profile.Velocity;

        var profileExhausts = Profile.Exhausts;
        var exhaustShare = 1f / profileExhausts.Count;
        for (var i = 0; i < profileExhausts.Count; i++)
        {
            var profileExhaust = profileExhausts[i];

            var exhaustTransform = ResolveExhaustTransform(profileExhaust);
            if (exhaustTransform == null)
            {
                _log.Warn($"exhaust {i} ('{profileExhaust.Path}') resolved to null, skipping");
                continue;
            }

            var emitters = new ExhaustEmitters
            {
                Mouth = TrainCar.transform.InverseTransformPoint(exhaustTransform.position),
                Offset = profileExhaust.Offset,
                Source = profileExhaust,
                Smoke = CreateSmokeEmitter(i, Profile.SmokeEmitter, Profile.Smoke, exhaustShare),
                Shimmer = CreateShimmerEmitter(i, Profile.ShimmerEmitter)
            };
            emitters.Reposition();
            Exhausts.Add(emitters);
        }

        ConfigureEmitters();

        EffectsBound = true;
        _log.Info($"effects bound ({Exhausts.Count} exhaust emitters)");
    }

    private Transform ResolveExhaustTransform(LocoExhaust exhaust)
    {
        if (exhaust.Kind == ExhaustKind.Independent)
        {
            return TrainCar.transform;
        }

        var existingPs = ExhaustTargets.Resolve(TrainCar, exhaust.Path);
        if (existingPs == null)
        {
            return null;
        }

        var existingEmission = existingPs.emission;
        existingEmission.enabled = false;
        _replacedExhausts.Add(existingPs);
        return existingPs.transform;
    }

    private SmokeParticles CreateSmokeEmitter(int index, SmokeParticles.Settings tuning,
        ExhaustSmokeModel.Settings smokeTuning, float exhaustShare)
    {
        var go = new GameObject(Naming.Create($"Smoke[{index}]"));
        ExhaustPlacement.AttachTo(go.transform, TrainCar.transform);
        var smoke = go.AddComponent<SmokeParticles>();
        smoke.tuning = tuning;
        smoke.Model.Tuning = smokeTuning;
        smoke.Model.ExhaustShare = exhaustShare;
        smoke.velocity = Velocity;
        smoke.shader = ModAssets.SmokeShader;
        smoke.atlas = GameAssets.SmokeAtlas;
        smoke.windFrame = SharedWindFrame;
        return smoke;
    }

    /// <summary>
    /// (re)configures every emitter from its current tuning and the user settings. Called once
    /// by the host after binding. If you make any changes to particle emitters afterwards, you
    /// must call this again.
    /// </summary>
    public void ConfigureEmitters()
    {
        var settings = SettingsStore.Current;
        SmokeParticles.Lifetime = settings.SmokeLifetime;
        SmokeParticles.IdleEmissionRate = settings.SmokeIdleEmissionRate;
        SmokeParticles.FullEmissionRate = settings.SmokeFullEmissionRate;

        foreach (var e in Exhausts)
        {
            e.Smoke.Configure();
            e.Shimmer.Configure();
        }
    }

    private ShimmerParticles CreateShimmerEmitter(int index, ShimmerParticles.Settings tuning)
    {
        var go = new GameObject(Naming.Create($"Shimmer[{index}]"));
        ExhaustPlacement.AttachTo(go.transform, TrainCar.transform);
        var shimmer = go.AddComponent<ShimmerParticles>();
        shimmer.tuning = tuning;
        shimmer.velocity = Velocity;
        shimmer.shader = ModAssets.HeatShimmerShader;
        shimmer.windFrame = SharedWindFrame;
        return shimmer;
    }

    private void UpdateEffects(bool firing)
    {
        var signals = new ExhaustSignalSet
        {
            Lambda = CombustionModel.Lambda,
            RpmNorm = CombustionModel.RpmNorm,
            ExhaustEnergy = CombustionModel.ExhaustEnergy,
            Vapour = Stack.Vapour,
            MassFlow = CombustionModel.MassFlow,
            GasDensity = CombustionModel.GasDensity,
            EngineOn = firing,
            LocoVelocity = TrainCar.GetVelocity(),
        };

        SharedWindFrame.wind = AmbientTemperature.Wind;

        foreach (var e in Exhausts)
        {
            signals.Apply(e.Smoke, e.Shimmer);
        }
    }

    private void OnDestroy()
    {
        // a successor host may have already claimed these, so restore only
        // the ones nobody else owns. Full teardown still restores exactly
        // once, via the last destroyed host.
        foreach (var replacedPs in _replacedExhausts)
        {
            if (replacedPs == null || IsClaimedByAnotherHost(replacedPs)) continue;

            var emission = replacedPs.emission;
            emission.enabled = true;
        }

        // emitters are separate GameObjects under the car root, they do not die with this component
        foreach (var e in Exhausts)
        {
            if (e.Smoke != null) Destroy(e.Smoke.gameObject);
            if (e.Shimmer != null) Destroy(e.Shimmer.gameObject);
        }
        Exhausts.Clear();

        if (Orchestrator.Instance != null)
        {
            Orchestrator.Instance.Forget(this);
        }
    }

    private bool IsClaimedByAnotherHost(ParticleSystem ps)
    {
        var orchestrator = Orchestrator.Instance;
        if (orchestrator == null) return false;

        foreach (var host in orchestrator.Hosts)
        {
            if (host == null || host == this || host.TrainCar != TrainCar) continue;
            if (host._replacedExhausts.Contains(ps)) return true;
        }

        return false;
    }
}
