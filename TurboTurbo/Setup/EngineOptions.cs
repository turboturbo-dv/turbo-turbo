using System;
using System.Collections.Generic;
using System.Linq;

using TurboTurbo.Modeling;
using TurboTurbo.Profiles;
using TurboTurbo.WorkBench;

using UnityEngine;

namespace TurboTurbo.Setup;

public class EngineOptions
{
    private readonly List<LocoExhaust> _exhausts = new();
    private CombustionModel.Settings _combustion = new();
    private TurboCharger.Settings _turboCharger = new();
    private AtmosphericCharger.Settings _atmospheric = new();
    private ExhaustSmokeModel.Settings _smoke = new();
    private SmokeParticles.Settings _smokeEmitter = new();
    private ShimmerParticles.Settings _shimmer = new();
    private ExhaustVelocitySettings _velocity = new();

    private ChargerKind _chargerKind = ChargerKind.Turbo;

    /// <summary>Adds an exhaust that hangs off the car's own transform, at the given car-local offset.</summary>
    public EngineOptions AddEngineExhaust(Vector3 offset = default)
    {
        _exhausts.Add(new LocoExhaust { Kind = ExhaustKind.Independent, Offset = offset });
        return this;
    }

    /// <summary>
    /// Takes over an existing exhaust ParticleSystem found by exact name: its emission
    /// is disabled so that it is effectively replaced. The offset, in car-local space,
    /// shifts the new emitters relative to the transform of the existing particle system.
    /// </summary>
    public EngineOptions ReplaceEngineExhaust(string particleSystemName, Vector3 offset = default)
    {
        _exhausts.Add(new LocoExhaust { Kind = ExhaustKind.Replacement, Name = particleSystemName, Offset = offset });
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
        if (profile.Exhausts != null)
        {
            foreach (var exhaust in profile.Exhausts)
            {
                _exhausts.Add(new LocoExhaust { Kind = exhaust.Kind, Name = exhaust.Name, Offset = exhaust.Offset });
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

    internal LocoProfile Build(string liveryId)
    {
        return new LocoProfile
        {
            LiveryId = liveryId,
            ChargerKind = _chargerKind,
            Exhausts = _exhausts.Select(e => new LocoExhaust { Kind = e.Kind, Name = e.Name, Offset = e.Offset }).ToList(),
            Combustion = new CombustionModel.Settings(_combustion),
            TurboCharger = new TurboCharger.Settings(_turboCharger),
            Atmospheric = new AtmosphericCharger.Settings(_atmospheric),
            Smoke = new ExhaustSmokeModel.Settings(_smoke),
            SmokeEmitter = new SmokeParticles.Settings(_smokeEmitter),
            ShimmerEmitter = new ShimmerParticles.Settings(_shimmer),
            Velocity = new ExhaustVelocitySettings(_velocity),
        };
    }
}