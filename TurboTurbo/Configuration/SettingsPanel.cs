using System.Collections.Generic;

using TurboTurbo.DevUI;

using UnityModManagerNet;

namespace TurboTurbo.Configuration;

/// <summary>
/// Renders the mod settings inside the UMM window's settings tab.
/// </summary>
internal static class SettingsPanel
{
    private static readonly List<Section> Sections = new();

    internal static void Initialize(Settings settings)
    {
        var general = new Section("general", "general");
        general.AddKey("Toggle dev panel", "Key that toggles the dev panel. None = unbound.",
            () => settings.ToggleDevPanelKey, v => settings.ToggleDevPanelKey = v);
        Sections.Add(general);
    }

    internal static void Draw(UnityModManager.ModEntry entry)
    {
        foreach (var section in Sections)
        {
            section.Draw();
        }
    }
}