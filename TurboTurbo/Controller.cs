using System;
using System.Collections.Generic;

using DV.ThingTypes;

using TurboTurbo.Modeling;
using TurboTurbo.Setup;
using TurboTurbo.WorkBench;

using UnityEngine;

namespace TurboTurbo;

public static class Controller
{
    private static readonly Logger Log = TurboTurbo.Log.ForContext("controller");

    private static readonly Dictionary<TrainCarType, EngineConfiguration> ConfigurationsByCarType = new();
    private static readonly Dictionary<string, EngineConfiguration> ConfigurationsByLiveryId = new();

    public static void ConfigureEngine(TrainCarType trainCarType, Action<EngineOptions> configure)
    {
        var configurator = new EngineOptions();
        configure(configurator);

        var configuration = configurator.Build();

        ConfigurationsByCarType.Add(trainCarType, configuration);

        Log.Info($"configured car type {trainCarType} with turbo={configuration.HasTurbo} and {configuration.Exhausts.Count} exhausts");
    }

    public static void ConfigureEngine(string liveryId, Action<EngineOptions> configure)
    {
        var configurator = new EngineOptions();
        configure(configurator);

        var configuration = configurator.Build();

        ConfigurationsByLiveryId.Add(liveryId, configuration);

        Log.Info($"configured livery id '{liveryId}' with turbo={configuration.HasTurbo} and {configuration.Exhausts.Count} exhausts");
    }

    internal static EngineConfiguration? TryGetConfiguration(TrainCar car)
    {
        EngineConfiguration config;
        if (ConfigurationsByCarType.TryGetValue(car.carType, out config))
        {
            return config;
        }
        if (ConfigurationsByLiveryId.TryGetValue(car.carLivery.id, out config))
        {
            return config;
        }

        return null;
    }
}

internal record struct EngineConfiguration(
    bool HasTurbo,
    List<ExhaustBinding> Exhausts,
    TurboModel.Settings Turbo,
    ExhaustSmokeModel.Settings Smoke,
    SmokeParticles.Settings SmokeEmitter,
    ShimmerParticles.Settings ShimmerEmitter,
    ExhaustVelocitySettings Velocity);

internal record struct ExhaustBinding(
    Func<TrainCar, Transform> TransformSelector,
    Func<TrainCar, ParticleSystem> ParticleSystemSelector,
    Vector3 Offset);