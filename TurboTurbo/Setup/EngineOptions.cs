using System;
using System.Collections.Generic;

using TurboTurbo.Modeling;
using TurboTurbo.WorkBench;

using UnityEngine;

namespace TurboTurbo.Setup;

public class EngineOptions
{
    private bool _hasTurbo = false;
    private readonly List<ExhaustBinding> _exhausts = new();
    private readonly TurboModel.Settings _turbo = new();
    private readonly ExhaustSmokeModel.Settings _smoke = new();
    private readonly SmokeParticles.Settings _smokeEmitter = new();
    private readonly ShimmerParticles.Settings _shimmer = new();
    private readonly ExhaustVelocitySettings _velocity = new();

    public EngineOptions AddTurbo()
    {
        _hasTurbo = true;
        return this;
    }

    /// <summary>Adds a new engine exhaust at the given position.</summary>
    public EngineOptions AddEngineExhaust(Func<TrainCar, Transform> transformSelector)
    {
        _exhausts.Add(new ExhaustBinding(transformSelector, null, Vector3.zero));
        return this;
    }

    /// <summary>
    /// Takes over an existing exhaust ParticleSystem: its emission is disabled so that it is effectively replaced.
    /// The optional offset, in car-local space, shifts the new emitters relative to the transform
    /// of the existing particle system.
    /// </summary>
    public EngineOptions ReplaceEngineExhaust(Func<TrainCar, ParticleSystem> psSelector, Vector3? offset = null)
    {
        _exhausts.Add(new ExhaustBinding(null, psSelector, offset ?? Vector3.zero));
        return this;
    }

    /// <summary>Tunes the turbo model from its defaults.</summary>
    public EngineOptions ConfigureTurbo(Action<TurboModel.Settings> configure)
    {
        configure(_turbo);
        return this;
    }

    /// <summary>Tunes the smoke appearance model from its defaults.</summary>
    public EngineOptions ConfigureSmoke(Action<ExhaustSmokeModel.Settings> configure)
    {
        configure(_smoke);
        return this;
    }

    /// <summary>Tunes the smoke emitter from its defaults.</summary>
    public EngineOptions ConfigureSmokeEmitter(Action<SmokeParticles.Settings> configure)
    {
        configure(_smokeEmitter);
        return this;
    }

    /// <summary>Tunes the shimmer emitter from its defaults.</summary>
    public EngineOptions ConfigureShimmerEmitter(Action<ShimmerParticles.Settings> configure)
    {
        configure(_shimmer);
        return this;
    }

    /// <summary>Tunes the exhaust flow range shared by the smoke and shimmer emitters.</summary>
    public EngineOptions ConfigureExhaustVelocity(Action<ExhaustVelocitySettings> configure)
    {
        configure(_velocity);
        return this;
    }

    internal EngineConfiguration Build()
    {
        // clone so later edits to this options object cannot leak into an already built configuration
        return new EngineConfiguration(_hasTurbo, _exhausts,
            new TurboModel.Settings(_turbo),
            new ExhaustSmokeModel.Settings(_smoke),
            new SmokeParticles.Settings(_smokeEmitter),
            new ShimmerParticles.Settings(_shimmer),
            new ExhaustVelocitySettings(_velocity));
    }
}