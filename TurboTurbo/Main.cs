using System.Linq;
using DV.ThingTypes;
using UnityEngine;

namespace TurboTurbo;

using UnityModManagerNet;

public static class Main
{
    private static UnityModManager.ModEntry _modEntry;
    internal static Logger Log;

    public static void Load(UnityModManager.ModEntry entry)
    {
        _modEntry = entry;
        Log = new Logger(_modEntry.Logger);

        ModAssets.Load(entry.Path, Log);

        Controller.ConfigureEngine(TrainCarType.LocoDiesel, options => options
            .AddTurbo()
            // DE6: vanilla ExhaustEngineSmoke transform sits below the visible
            // stack mouth; offset verified in TurboTurboOld (0.05 HeatOrigin
            // + 0.20 emitter placement)
            .WithExhaustSpawnOffset(0.3f)
            .AddEngineExhaust(c =>
                c.GetFirstComponentInChildren<ParticleSystem>(true, ps => ps.name == "ExhaustEngineSmoke")?.transform)
            .AddTractionMotorVent(c =>
                c.GetFirstComponentInChildren<ParticleSystem>(true, ps => ps.name == "HighTempEngineSmoke")?.transform)
            .AddTractionMotorVent(c =>
                c.GetFirstComponentInChildren<ParticleSystem>(true, ps => ps.name == "DamagedEngineSmoke")?.transform));

        Orchestrator.Create(Log);

        Log.LogInfo("TurboTurbo ready!");
    }
}
