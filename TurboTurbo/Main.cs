using DV.ThingTypes;

using UnityEngine;

namespace TurboTurbo;

using UnityModManagerNet;

public static class Main
{
    public static void Load(UnityModManager.ModEntry entry)
    {
        Log.Init(entry.Logger);

        ModAssets.Initialize(entry.Path);

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

        Orchestrator.Create();

        DevUI.TurboDevPanel.Create();

        Log.ForContext("main").Info("TurboTurbo ready!");
    }
}