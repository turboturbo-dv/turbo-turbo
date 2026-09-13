using System.Collections.Generic;

using TurboTurbo.DevUI;

using UnityEngine;

using UnityModManagerNet;

namespace TurboTurbo.Configuration;

/// <summary>
/// Renders the mod settings inside the UMM window's settings tab.
/// </summary>
internal static class SettingsPanel
{
    private static readonly List<Section> Sections = new();
    private static Settings _settings;

    internal static void Initialize(Settings settings)
    {
        _settings = settings;
    }

    internal static void Draw(UnityModManager.ModEntry entry)
    {
        foreach (var section in Sections)
        {
            section.Draw();
        }
        GUILayout.BeginHorizontal();
        GUILayout.Label("Toggle dev panel");
        UnityModManager.UI.DrawKeybindingSmart(_settings.ToggleDevPanel, "Toggle dev panel");
        GUILayout.EndHorizontal();
    }
}