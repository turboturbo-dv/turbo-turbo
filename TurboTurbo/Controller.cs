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

        Log.Info(
            $"configured car type {trainCarType} with charger={configuration.ChargerKind} and {configuration.Exhausts.Count} exhausts");
    }

    public static void ConfigureEngine(string liveryId, Action<EngineOptions> configure)
    {
        var configurator = new EngineOptions();
        configure(configurator);

        var configuration = configurator.Build();

        ConfigurationsByLiveryId.Add(liveryId, configuration);

        Log.Info(
            $"configured livery id '{liveryId}' with charger={configuration.ChargerKind} and {configuration.Exhausts.Count} exhausts");
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
    List<ExhaustBinding> Exhausts,
    CombustionModel.Settings Combustion,
    ChargerKind ChargerKind,
    TurboCharger.Settings TurboCharger,
    AtmosphericCharger.Settings Atmospheric,
    ExhaustSmokeModel.Settings Smoke,
    SmokeParticles.Settings SmokeEmitter,
    ShimmerParticles.Settings ShimmerEmitter,
    ExhaustVelocitySettings Velocity)
{
    public ICharger BuildCharger()
    {
        return ChargerKind == ChargerKind.Atmospheric
            ? new AtmosphericCharger(new AtmosphericCharger.Settings(Atmospheric))
            : new TurboCharger(new TurboCharger.Settings(TurboCharger));
    }
}

internal record struct ExhaustBinding(
    Func<TrainCar, Transform> TransformSelector,
    Func<TrainCar, ParticleSystem> ParticleSystemSelector,
    Vector3 Offset);