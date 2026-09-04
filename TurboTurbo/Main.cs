using DV.ThingTypes;

using TurboTurbo.Configuration;

using UnityEngine;

namespace TurboTurbo;

using UnityModManagerNet;

public static class Main
{
    public static void Load(UnityModManager.ModEntry entry)
    {
        Log.Init(entry.Logger);

        ModAssets.Initialize(entry.Path);

        var settings = UnityModManager.ModSettings.Load<Settings>(entry);
        SettingsPanel.Initialize(settings);

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

        DevUI.DevPanelPresenter.Create(settings);

        entry.OnGUI = SettingsPanel.Draw;
        entry.OnSaveGUI = saveEntry => settings.Save(saveEntry);

        Log.ForContext("main").Info("TurboTurbo ready!");
    }
}