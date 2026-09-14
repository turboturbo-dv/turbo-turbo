using System;
using System.Collections.Generic;

using TurboTurbo.Modeling;
using TurboTurbo.Profiles;
using TurboTurbo.WorkBench;

using UnityEngine;

namespace TurboTurbo.Setup;

public class EngineOptions
{
    private readonly List<ExhaustBinding> _exhausts = new();
    private CombustionModel.Settings _combustion = new();
    private TurboCharger.Settings _turboCharger = new();
    private AtmosphericCharger.Settings _atmospheric = new();
    private ExhaustSmokeModel.Settings _smoke = new();
    private SmokeParticles.Settings _smokeEmitter = new();
    private ShimmerParticles.Settings _shimmer = new();
    private ExhaustVelocitySettings _velocity = new();

    private ChargerKind _chargerKind = ChargerKind.Turbo;

    /// <summary>Adds a new engine exhaust at the given position.</summary>
    public EngineOptions AddEngineExhaust(Func<TrainCar, Transform> transformSelector, Vector3? offset = null)
    {
        _exhausts.Add(new ExhaustBinding(transformSelector, null, offset ?? Vector3.zero));
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

    /// <summary>Tunes the combustion model from its defaults.</summary>
    public EngineOptions ConfigureCombustion(Action<CombustionModel.Settings> configure)
    {
        configure(_combustion);
        return this;
    }

    /// <summary>Tunes the turbocharger from its defaults.</summary>
    public EngineOptions ConfigureTurboCharger(Action<TurboCharger.Settings> configure)
    {
        configure(_turboCharger);
        return this;
    }

    /// <summary>Switches the engine to natural aspiration with a choke-model charger.</summary>
    public EngineOptions UseAtmosphericCharger(Action<AtmosphericCharger.Settings> configure)
    {
        _chargerKind = ChargerKind.Atmospheric;
        configure(_atmospheric);
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

    /// <summary>
    /// Populates this options object from a loco profile. Null settings blocks keep
    /// their defaults; present smoke/atmospheric blocks are normalized via Validate,
    /// mirroring the dev panel edit path.
    /// </summary>
    internal void ApplyLocoProfile(LocoProfile profile)
    {
        foreach (var exhaust in profile.Exhausts)
        {
            if (exhaust.Kind == ExhaustKind.Replacement)
            {
                ReplaceEngineExhaust(
                    c => c.GetFirstComponentInChildren<ParticleSystem>(true, ps => ps.name == exhaust.Name),
                    exhaust.Offset);
            }
            else
            {
                AddEngineExhaust(c => c.transform, exhaust.Offset);
            }
        }

        if (profile.Combustion != null) _combustion = new CombustionModel.Settings(profile.Combustion);
        if (profile.ChargerKind == ChargerKind.Atmospheric)
        {
            _chargerKind = ChargerKind.Atmospheric;
            if (profile.Atmospheric != null)
            {
                _atmospheric = new AtmosphericCharger.Settings(profile.Atmospheric);
                _atmospheric.Validate();
            }
        }
        else if (profile.TurboCharger != null)
        {
            _turboCharger = new TurboCharger.Settings(profile.TurboCharger);
        }
        if (profile.Smoke != null)
        {
            _smoke = new ExhaustSmokeModel.Settings(profile.Smoke);
            _smoke.Validate();
        }
        if (profile.SmokeEmitter != null) _smokeEmitter = new SmokeParticles.Settings(profile.SmokeEmitter);
        if (profile.ShimmerEmitter != null) _shimmer = new ShimmerParticles.Settings(profile.ShimmerEmitter);
        if (profile.Velocity != null) _velocity = new ExhaustVelocitySettings(profile.Velocity);
    }

    internal EngineConfiguration Build()
    {
        // clone so later edits to this options object cannot leak into an already built configuration
        return new EngineConfiguration(_exhausts,
            new CombustionModel.Settings(_combustion),
            _chargerKind,
            new TurboCharger.Settings(_turboCharger),
            new AtmosphericCharger.Settings(_atmospheric),
            new ExhaustSmokeModel.Settings(_smoke),
            new SmokeParticles.Settings(_smokeEmitter),
            new ShimmerParticles.Settings(_shimmer),
            new ExhaustVelocitySettings(_velocity));
    }
}