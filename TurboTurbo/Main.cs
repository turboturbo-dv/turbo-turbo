using TurboTurbo.Assets;
using TurboTurbo.Configuration;
using TurboTurbo.DevUI;
using TurboTurbo.Profiles;
using TurboTurbo.Runtime;

using UnityEngine;

namespace TurboTurbo;

using UnityModManagerNet;

public static class Main
{
    public static void Load(UnityModManager.ModEntry entry)
    {
        Log.Init(new UmmLogSink(entry.Logger));
        ModAssets.Initialize(entry.Path);

        SettingsStore.Initialize(entry);
        SettingsPanel.Initialize();
        ProfileRepository.Initialize();

        StockConfiguration.Apply();

        Orchestrator.Create();

        DevPanelPresenter.Create();
        ProfileEditorPresenter.Create();

        entry.OnToggle = OnToggle;
        entry.OnGUI = SettingsPanel.Draw;

        Log.ForContext("main").Info("TurboTurbo ready!");
    }

    private static bool OnToggle(UnityModManager.ModEntry entry, bool isOn)
    {
        Orchestrator.Instance.SetActive(isOn);

        if (isOn)
        {
            if (DevPanelPresenter.Instance == null)
            {
                DevPanelPresenter.Create();
            }
            if (ProfileEditorPresenter.Instance == null)
            {
                ProfileEditorPresenter.Create();
            }
        }
        else
        {
            if (DevPanelPresenter.Instance != null)
            {
                Object.Destroy(DevPanelPresenter.Instance.gameObject);
            }
            if (ProfileEditorPresenter.Instance != null)
            {
                Object.Destroy(ProfileEditorPresenter.Instance.gameObject);
            }
        }

        return true;
    }
}