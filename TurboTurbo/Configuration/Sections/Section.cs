using System;
using System.Collections.Generic;
using System.Text;

using UnityEngine;

namespace TurboTurbo.Configuration.Sections;

internal sealed class Section
{
    // currently, we can get away with having one action per section.
    // if we encounter more complex situations, we might have to set it per spec
    private readonly Action _onRequiresReconfigure;
    private readonly List<ITweakSpec> _specs = new();

    public readonly string Title;
    public readonly string YamlKey;
    public bool Open;
    public Action OnToggle;
    public TweakGrade MaxGrade { get; set; } = TweakGrade.Advanced;

    /// <summary>Fixed header width, or 0 to stretch across the row.</summary>
    public float HeaderWidth { get; set; }

    public Section(string title, string yamlKey, Action onRequiresReconfigure = null)
    {
        Title = title;
        YamlKey = yamlKey;
        _onRequiresReconfigure = onRequiresReconfigure;
    }

    public void AddFloat(string key, string tooltip, float min, float max, bool requiresReconfigure,
        Func<float> get, Action<float> set, TweakGrade grade = TweakGrade.Advanced)
    {
        _specs.Add(new FloatSpec(key, tooltip, get, set, min, max,
            requiresReconfigure ? _onRequiresReconfigure : null, grade));
    }

    public void AddBool(string key, string tooltip, bool requiresReconfigure,
        Func<bool> get, Action<bool> set, TweakGrade grade = TweakGrade.Advanced)
    {
        _specs.Add(new BoolSpec(key, tooltip, get, set,
            requiresReconfigure ? _onRequiresReconfigure : null, grade));
    }

    public ColorSpec AddColor(string key, string tooltip, Func<Color> get, Action<Color> set,
        TweakGrade grade = TweakGrade.Advanced)
    {
        var spec = new ColorSpec(key, tooltip, get, set, grade);
        _specs.Add(spec);
        return spec;
    }

    public void AddButton(string key, string tooltip, Action action, TweakGrade grade = TweakGrade.Advanced)
    {
        _specs.Add(new ButtonSpec(key, tooltip, action, grade));
    }

    public void AddInt(string key, string tooltip, int min, int max, bool requiresReconfigure,
        Func<int> get, Action<int> set, TweakGrade grade = TweakGrade.Advanced)
    {
        _specs.Add(new IntSpec(key, tooltip, get, set, min, max,
            requiresReconfigure ? _onRequiresReconfigure : null, grade));
    }

    public int ChangedCount
    {
        get
        {
            var n = 0;
            foreach (var spec in _specs)
            {
                if (spec.Changed) n++;
            }
            return n;
        }
    }

    public void ExportYaml(StringBuilder sb)
    {
        var lines = new List<string>();
        foreach (var spec in _specs)
        {
            var line = spec.Export();
            if (line != null) lines.Add(line);
        }
        if (lines.Count == 0) return;

        sb.Append(YamlKey).AppendLine(":");
        foreach (var line in lines)
        {
            sb.AppendLine(line);
        }
    }

    public void Draw()
    {
        var visible = new List<ITweakSpec>();
        foreach (var spec in _specs)
        {
            if (spec.Grade <= MaxGrade) visible.Add(spec);
        }
        if (visible.Count == 0) return;

        var changed = 0;
        foreach (var spec in visible)
        {
            if (spec.Changed) changed++;
        }

        GUILayout.BeginHorizontal();
        bool clicked;
        if (HeaderWidth > 0)
        {
            clicked = GUILayout.Button((Open ? "▾ " : "▸ ") + Title, Styles.SectionHeader, GUILayout.Width(HeaderWidth));
        }
        else
        {
            clicked = GUILayout.Button((Open ? "▾ " : "▸ ") + Title, Styles.SectionHeader);
        }
        if (clicked)
        {
            Open = !Open;
            if (OnToggle != null) OnToggle();
        }
        if (HeaderWidth > 0) GUILayout.FlexibleSpace();
        GUILayout.Label(changed > 0 ? $"{changed} changed" : "", GUILayout.Width(70f));
        GUI.enabled = changed > 0;
        if (GUILayout.Button("reset", GUILayout.Width(60f)))
        {
            foreach (var spec in visible) spec.Reset();
        }
        GUI.enabled = true;
        GUILayout.EndHorizontal();

        if (Open)
        {
            foreach (var spec in visible) spec.Draw();
        }
    }
}