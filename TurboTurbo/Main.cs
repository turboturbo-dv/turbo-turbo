using DV.ThingTypes;

using TurboTurbo.Assets;
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

        StockConfiguration.Apply();

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