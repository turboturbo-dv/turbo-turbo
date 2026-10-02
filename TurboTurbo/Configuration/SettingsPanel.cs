using System.Collections.Generic;

using TurboTurbo.Configuration.Sections;
using TurboTurbo.Profiles;
using TurboTurbo.Profiles.Storage;
using TurboTurbo.Runtime;

using UnityEngine;

using UnityModManagerNet;

namespace TurboTurbo.Configuration;

/// <summary>
/// Renders the mod settings inside the UMM window's settings tab.
/// </summary>
internal static class SettingsPanel
{
    private static readonly List<Section> Sections = new();
    private static bool _targetOpen;
    private static GUIStyle _toggle;
    private static TurboTooltipLayer _tooltip;
    private static readonly ProfileOverview Profiles = new();

    private const float TargetWidth = 220f;
    private const float KeybindWidth = 110f;

    // UMM indents GUI.skin.toggle by ~10px; drop that so our toggle aligns with the labels.
    private static GUIStyle Toggle => _toggle ??= new GUIStyle(GUI.skin.toggle) { margin = new RectOffset(0, 0, 0, 0) };

    internal static void Initialize()
    {
        var go = new GameObject(Naming.Create("TooltipLayer"));
        Object.DontDestroyOnLoad(go);
        _tooltip = go.AddComponent<TurboTooltipLayer>();
    }

    internal static void Draw(UnityModManager.ModEntry entry)
    {
        foreach (var section in Sections)
        {
            section.Draw();
        }
        GUILayout.BeginHorizontal();
        GUILayout.Label(
            new GUIContent("Open profile editor",
                "Opens the profile editor for the locomotive you are currently driving."),
            GUILayout.Width(Styles.LabelWidth));
        UnityModManager.UI.DrawKeybindingSmart(
            SettingsStore.Current.OpenProfileEditor, "Open profile editor", Styles.KeybindButton, GUILayout.Width(KeybindWidth));
        GUILayout.EndHorizontal();

        DrawAuthoring(entry);

        GUILayout.Space(8f);
        Profiles.Draw();

        // GUI.tooltip is only populated during repaint; capture then, so
        // other event passes don't overwrite it.
        if (_tooltip != null && Event.current.type == EventType.Repaint)
        {
            _tooltip.Tooltip = GUI.tooltip;
        }
    }

    private static void DrawAuthoring(UnityModManager.ModEntry entry)
    {
        var settings = SettingsStore.Current;

        GUILayout.BeginHorizontal();
        GUILayout.Label(
            new GUIContent("Vehicle author mode",
                "For vehicle mod developers. With author mode enabled, any vehicle profiles that you create are saved " +
                "directly to a TurboConfig.xml file in the mod directory (next to its info.json).\n\n" +
                "When you've finished creating the profile(s), distribute the TurboConfig.xml file together with the other " +
                "files in your mod's directory. Users who have TurboTurbo installed alongside your mod will " +
                "automatically have your profile applied.\n\n" +
                "Note that all user profiles are temporarily disabled while vehicle author mode is active."),
            GUILayout.Width(Styles.LabelWidth));
        var mode = GUILayout.Toggle(settings.AuthoringMode, GUIContent.none, Toggle, GUILayout.Width(16f));
        GUILayout.EndHorizontal();
        if (mode != settings.AuthoringMode)
        {
            settings.AuthoringMode = mode;
            _targetOpen = false;
            settings.Save(entry);
            Orchestrator.Instance?.ReloadAllHosts();
        }
        if (!settings.AuthoringMode) return;

        var mods = ModRegistry.EligibleTargets(entry.Info.Id);

        var current = mods.Find(m => m.Info.Id == settings.AuthoringTargetModId);
        var label = current != null ? current.Info.DisplayName : "(none)";

        GUILayout.BeginVertical(Styles.AuthoringBox);

        GUILayout.Label("Save profiles to:");
        GUILayout.BeginVertical(GUILayout.Width(TargetWidth));
        if (GUILayout.Button(label + "  ▾")) _targetOpen = !_targetOpen;
        if (_targetOpen) DrawTargetList(entry, mods);
        GUILayout.EndVertical();

        if (!string.IsNullOrEmpty(settings.AuthoringTargetModId))
        {
            var file = current != null
                ? System.IO.Path.Combine(current.Path, TurboConfigCodec.FileName)
                : "(target mod unavailable)";
            GUILayout.Label($"Profiles will be saved to {file}", Styles.WrappedLabel);
        }

        GUILayout.EndVertical();
    }

    private static void DrawTargetList(UnityModManager.ModEntry entry, List<UnityModManager.ModEntry> mods)
    {
        if (mods.Count == 0)
        {
            GUILayout.Label("no eligible mods");
            return;
        }

        if (GUILayout.Button("(none)")) SelectTarget(entry, "");

        foreach (var mod in mods)
        {
            var marker = mod.Info.Id == SettingsStore.Current.AuthoringTargetModId ? "* " : "  ";
            if (GUILayout.Button(marker + mod.Info.DisplayName)) SelectTarget(entry, mod.Info.Id);
        }
    }

    private static void SelectTarget(UnityModManager.ModEntry entry, string modId)
    {
        SettingsStore.Current.AuthoringTargetModId = modId;
        _targetOpen = false;
        SettingsStore.Current.Save(entry);
    }
}
