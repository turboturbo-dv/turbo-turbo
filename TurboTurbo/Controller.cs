using System;
using System.Collections.Generic;

using DV;
using DV.ThingTypes;

using TurboTurbo.Profiles;
using TurboTurbo.Setup;

namespace TurboTurbo;

public static class Controller
{
    private static readonly Logger Log = TurboTurbo.Log.ForContext("controller");

    private static readonly Dictionary<string, LocoProfile> ConfigurationsByLiveryId = new();

    /// <summary>
    /// Registers a configuration for the given car type.
    /// </summary>
    public static void ConfigureEngine(TrainCarType trainCarType, Action<EngineOptions> configure)
    {
        var liveryId = ResolveLiveryId(trainCarType);
        if (liveryId == null)
        {
            Log.Warn($"no livery registered for car type {trainCarType}, configuration skipped");
            return;
        }

        ConfigureEngine(liveryId, configure);
    }

    /// <summary>
    /// Registers a configuration for the given livery ID.
    /// </summary>
    public static void ConfigureEngine(string liveryId, Action<EngineOptions> configure)
    {
        var options = new EngineOptions();
        configure(options);

        var profile = options.Build(liveryId);
        ConfigurationsByLiveryId[liveryId] = profile;

        Log.Info(
            $"configured livery '{liveryId}' with charger={profile.ChargerKind} and {profile.Exhausts.Count} exhausts");
    }

    internal static LocoProfile TryGetConfiguration(string liveryId)
    {
        ConfigurationsByLiveryId.TryGetValue(liveryId, out var configuration);
        return configuration;
    }

    private static string ResolveLiveryId(TrainCarType carType)
    {
        var liveries = Globals.G?.Types?.TrainCarType_to_v2;
        if (liveries != null && liveries.TryGetValue(carType, out var livery) && livery != null)
        {
            return livery.id;
        }

        return null;
    }
}