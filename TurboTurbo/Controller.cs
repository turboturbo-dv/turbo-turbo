using System;
using System.Collections.Generic;

using DV.ThingTypes;

using TurboTurbo.Setup;

using UnityEngine;

namespace TurboTurbo;

public static class Controller
{
    private static readonly Logger Log = TurboTurbo.Log.ForContext("controller");

    internal static readonly Dictionary<TrainCarType, EngineConfiguration> Configurations = new();

    public static void ConfigureEngine(TrainCarType trainCarType, Action<EngineOptions> configure)
    {
        var configurator = new EngineOptions();
        configure(configurator);

        var configuration = configurator.Build();

        Configurations.Add(trainCarType, configuration);

        Log.Info($"configured {trainCarType} with turbo={configuration.HasTurbo} and {configuration.ExhaustPositionSelectors.Count} exhausts");
    }

    internal static EngineConfiguration? TryGetConfiguration(TrainCar car)
    {
        if (Configurations.TryGetValue(car.carType, out var config))
        {
            return config;
        }

        return null;
    }
}

internal record struct EngineConfiguration(bool HasTurbo, float ExhaustSpawnOffset, List<Func<TrainCar, Transform>> ExhaustPositionSelectors);