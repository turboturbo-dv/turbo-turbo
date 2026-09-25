using System;
using System.Collections.Generic;

using TurboTurbo.Modeling;
using TurboTurbo.Profiles;
using TurboTurbo.WorkBench;

using UnityEngine;

namespace TurboTurbo.Setup;

public class EngineOptions
{
    private readonly List<LocoExhaust> _exhausts = new();
    private readonly TurboCharger.Settings _turboCharger = new();
    private readonly AtmosphericCharger.Settings _atmospheric = new();
    private readonly ExhaustSmokeModel.Settings _smoke = new();
    private readonly SmokeParticles.Settings _smokeEmitter = new();
    private readonly ShimmerParticles.Settings _shimmer = new();
    private readonly ExhaustVelocitySettings _velocity = new();

    private ChargerKind _chargerKind = ChargerKind.Turbo;

    /// <summary>
    /// Adds an exhaust at the position determined by <paramref name="offset"/>.
    /// </summary>
    public EngineOptions AddEngineExhaust(Vector3 offset = default)
    {
        _exhausts.Add(new LocoExhaust { Kind = ExhaustKind.Independent, Offset = offset });
        return this;
    }

    /// <summary>
    /// Takes over an existing exhaust ParticleSystem found by exact name: its emission
    /// is disabled so that it is effectively replaced. Supplying <paramref name="offset"/>
    /// will adjust the position of the replacement relative to the replaced particle system.
    /// </summary>
    public EngineOptions ReplaceEngineExhaust(string particleSystemName, Vector3 offset = default)
    {
        _exhausts.Add(new LocoExhaust { Kind = ExhaustKind.Replacement, Name = particleSystemName, Offset = offset });
        return this;
    }

    /// <summary>Switches the engine to a naturally aspirated charger.</summary>
    public EngineOptions UseAtmosphericCharger(Action<AtmosphericCharger.Settings> configure)
    {
        _chargerKind = ChargerKind.Atmospheric;
        configure(_atmospheric);
        return this;
    }

    /// <summary>Tunes the turbocharger from its defaults.</summary>
    public EngineOptions ConfigureTurboCharger(Action<TurboCharger.Settings> configure)
    {
        configure(_turboCharger);
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

    /// <summary>Tunes the exhaust velocity shared by the smoke and shimmer emitters.</summary>
    public EngineOptions ConfigureExhaustVelocity(Action<ExhaustVelocitySettings> configure)
    {
        configure(_velocity);
        return this;
    }

    /// <summary>
    /// Builds the assembled configuration into a complete, valid <see cref="LocoProfile"/>,
    /// or null when the configuration is invalid.
    /// </summary>
    internal LocoProfile TryBuild(string liveryId)
    {
        var profile = new LocoProfile
        {
            LiveryId = liveryId,
            ChargerKind = _chargerKind,
            Exhausts = _exhausts,
            TurboCharger = _turboCharger,
            Atmospheric = _atmospheric,
            Smoke = _smoke,
            SmokeEmitter = _smokeEmitter,
            ShimmerEmitter = _shimmer,
            Velocity = _velocity,
        };

        var error = profile.Complete();
        if (error == null) return profile;

        Log.ForContext("options").Error($"invalid engine configuration for '{liveryId}': {error}");
        return null;
    }
}