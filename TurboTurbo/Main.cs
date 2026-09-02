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
            // TODO: Need to tune this
            .WithExhaustSpawnOffset(0.3f)
            .AddEngineExhaust(c =>
                c.GetFirstComponentInChildren<ParticleSystem>(true, ps => ps.name == "ExhaustEngineSmoke")?.transform)
            .AddTractionMotorVent(c =>
                c.GetFirstComponentInChildren<ParticleSystem>(true, ps => ps.name == "HighTempEngineSmoke")?.transform)
            .AddTractionMotorVent(c =>
                c.GetFirstComponentInChildren<ParticleSystem>(true, ps => ps.name == "DamagedEngineSmoke")?.transform));

        Orchestrator.Create(Log);

        DevUI.TurboDevPanel.Create();

        Log.LogInfo("TurboTurbo ready!");
    }
}
