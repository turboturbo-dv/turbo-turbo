using TurboTurbo.Assets;
using TurboTurbo.Configuration;
using TurboTurbo.Profiles;
using TurboTurbo.Runtime;

using UnityEngine;

namespace TurboTurbo;

using UnityModManagerNet;

public static class Main
{
    internal static Configuration.ProfileEditorPresenter EditPresenter { get; private set; }

    public static void Load(UnityModManager.ModEntry entry)
    {
        Log.Init(new UmmLogSink(entry.Logger));
        ModAssets.Initialize(entry.Path);

        SettingsStore.Initialize(entry);
        SettingsPanel.Initialize();
        ProfileRepository.Initialize();

        StockConfiguration.Apply();

        Orchestrator.Create();

        DevUI.DevPanelPresenter.Create();
        EditPresenter = Configuration.ProfileEditorPresenter.Create();

        entry.OnToggle = OnToggle;
        entry.OnGUI = SettingsPanel.Draw;

        Log.ForContext("main").Info("TurboTurbo ready!");
    }

    private static bool OnToggle(UnityModManager.ModEntry entry, bool isOn)
    {
        Orchestrator.Instance.SetActive(isOn);

        if (isOn)
        {
            if (DevUI.DevPanelPresenter.Instance == null)
            {
                DevUI.DevPanelPresenter.Create();
            }
        }
        else
        {
            if (DevUI.DevPanelPresenter.Instance != null)
            {
                Object.Destroy(DevUI.DevPanelPresenter.Instance.gameObject);
            }
            EditPresenter.Close();
        }

        return true;
    }
}