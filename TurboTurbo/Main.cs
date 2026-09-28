using System;

using TurboTurbo.Assets;
using TurboTurbo.Configuration;
using TurboTurbo.DevUI;
using TurboTurbo.Profiles;
using TurboTurbo.Runtime;

namespace TurboTurbo;

using UnityModManagerNet;

public static class Main
{
    public static void Load(UnityModManager.ModEntry entry)
    {
        Log.Init(new UmmLogSink(entry.Logger));
        try
        {
            LoadInternal(entry);
        }
        catch (Exception e)
        {
            // for whatever reason, UMM does not log exception details when a mod fails to start
            // so catch-log-rethrow to make sure any startup exceptions end up in the log somewhere
            var log = Log.ForContext("main");
            log.Error("TurboTurbo failed to start");
            log.Exception(e);
            throw;
        }
    }

    private static void LoadInternal(UnityModManager.ModEntry entry)
    {
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
                UnityEngine.Object.Destroy(DevPanelPresenter.Instance.gameObject);
            }
            if (ProfileEditorPresenter.Instance != null)
            {
                UnityEngine.Object.Destroy(ProfileEditorPresenter.Instance.gameObject);
            }
        }

        return true;
    }
}