using UnityModManagerNet;

namespace TurboTurbo.Configuration;

/// <summary>
/// Owns the settings instance and its UMM persistence.
/// </summary>
internal static class SettingsStore
{
    private static UnityModManager.ModEntry _entry;

    public static Settings Current { get; set; }

    public static string ModId => _entry?.Info.Id;

    public static void Initialize(UnityModManager.ModEntry entry)
    {
        _entry = entry;
        Current = UnityModManager.ModSettings.Load<Settings>(entry);
        entry.OnSaveGUI = _ => Save();
    }

    public static void Save()
    {
        if (_entry != null) Current.Save(_entry);
    }
}