using DV.ThingTypes;

using TurboTurbo.Configuration;
using TurboTurbo.Runtime;

using UnityEngine;

namespace TurboTurbo;

using UnityModManagerNet;

public static class Main
{
    private static Settings _settings;

    public static void Load(UnityModManager.ModEntry entry)
    {
        Log.Init(entry.Logger);
        ModAssets.Initialize(entry.Path);

        _settings = UnityModManager.ModSettings.Load<Settings>(entry);
        SettingsPanel.Initialize(_settings);

        Controller.ConfigureEngine(TrainCarType.LocoDiesel, options => options
            .AddTurbo()
            .ReplaceEngineExhaust(
                c => c.GetFirstComponentInChildren<ParticleSystem>(true, ps => ps.name == "ExhaustEngineSmoke"),
                new Vector3(0f, 0.2f, -0.05f)));

        Controller.ConfigureEngine(TrainCarType.LocoDH4, options => options
            .AddTurbo()
            .ReplaceEngineExhaust(
                c => c.GetFirstComponentInChildren<ParticleSystem>(true, ps => ps.name == "ExhaustEngineSmoke"),
                new Vector3(0f, 0f, -0.05f))
            .ConfigureTurbo(t => t.LambdaCalibration = 1.7f)
            .ConfigureSmoke(s =>
            {
                s.WetStackMistStrength = 0.95f;
                s.OilRpmExponent = 2.1f;
            })
            .ConfigureExhaustVelocity(v =>
            {
                v.Idle = 3.05f;
                v.FullLoad = 13f;
            })
            .ConfigureSmokeEmitter(e =>
            {
                e.startSizeMin = 0.4f;
                e.startSizeMax = 0.6f;
            }));

        Orchestrator.Create();

        DevUI.DevPanelPresenter.Create(_settings);

        entry.OnToggle = OnToggle;
        entry.OnGUI = SettingsPanel.Draw;
        entry.OnSaveGUI = saveEntry => _settings.Save(saveEntry);

        Log.ForContext("main").Info("TurboTurbo ready!");
    }

    private static bool OnToggle(UnityModManager.ModEntry entry, bool isOn)
    {
        Orchestrator.Instance.SetActive(isOn);

        if (isOn)
        {
            if (DevUI.DevPanelPresenter.Instance == null)
            {
                DevUI.DevPanelPresenter.Create(_settings);
            }
        }
        else if (DevUI.DevPanelPresenter.Instance != null)
        {
            Object.Destroy(DevUI.DevPanelPresenter.Instance.gameObject);
        }

        return true;
    }
}