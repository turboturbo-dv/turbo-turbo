using TurboTurbo.Assets;
using TurboTurbo.Configuration;
using TurboTurbo.Profiles;
using TurboTurbo.Runtime;

using UnityEngine;

namespace TurboTurbo;

using System.Linq;

using UnityModManagerNet;

public static class Main
{
    private static Settings _settings;

    public static void Load(UnityModManager.ModEntry entry)
    {
        Log.Init(new UmmLogSink(entry.Logger));
        ModAssets.Initialize(entry.Path);

        _settings = UnityModManager.ModSettings.Load<Settings>(entry);
        SettingsPanel.Initialize(_settings);

        ProfileRepository.Initialize(_settings, entry);

        var userProfiles = ProfileLoader.LoadUserProfiles(_settings.LocoProfiles);
        var modProfiles = ProfileLoader.LoadModProfiles(
            UnityModManager.modEntries.Select(e => new ProfileLoader.ModSource(e.Info.Id, e.Info.DisplayName, e.Enabled, e.Path)),
            entry.Info.Id);

        ProfileRepository.SetUserProfiles(userProfiles);
        ProfileRepository.SetSuppliedProfiles(modProfiles);

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