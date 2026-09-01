using System;
using System.Collections.Generic;
using DV.ThingTypes;
using TurboTurbo.Setup;
using UnityEngine;
using UnityModManagerNet;

namespace TurboTurbo;

public static class Controller
{
    internal static readonly Dictionary<TrainCarType, EngineConfiguration> Configurations = new();

    public static void ConfigureEngine(TrainCarType trainCarType, Action<EngineOptions> configure)
    {
        var configurator = new EngineOptions();
        configure(configurator);

        var configuration = configurator.Build();

        Configurations.Add(trainCarType, configuration);
        
        Main.Log.LogInfo($"[controller] configured {trainCarType} with turbo={configuration.HasTurbo} and {configuration.ExhaustPositionSelectors.Count} exhausts");
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

internal record struct EngineConfiguration(bool HasTurbo, List<Func<TrainCar, Transform>> ExhaustPositionSelectors);