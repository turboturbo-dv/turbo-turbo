using System;
using System.Collections.Generic;

using UnityEngine;

namespace TurboTurbo.Configuration.Sections;

internal sealed class Section : IEditorPanel
{
    // currently, we can get away with having one action per section.
    // if we encounter more complex situations, we might have to set it per spec
    private readonly Action _onRequiresReconfigure;
    private readonly List<ITweakSpec> _specs = new();

    public readonly string Title;
    public bool Open { get; set; }
    public Action OnToggle;
    public TweakGrade MaxGrade { get; set; } = TweakGrade.Advanced;

    /// <summary>Fixed header width, or 0 to stretch across the row.</summary>
    public float HeaderWidth { get; set; }

    public Section(string title, Action onRequiresReconfigure = null)
    {
        Title = title;
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

    public void AddProgressButton(string key, string tooltip, Action action,
        Func<float> progress = null, TweakGrade grade = TweakGrade.Advanced)
    {
        _specs.Add(new ProgressButtonSpec(key, tooltip, action, grade, progress));
    }

    public void AddInt(string key, string tooltip, int min, int max, bool requiresReconfigure,
        Func<int> get, Action<int> set, TweakGrade grade = TweakGrade.Advanced)
    {
        _specs.Add(new IntSpec(key, tooltip, get, set, min, max,
            requiresReconfigure ? _onRequiresReconfigure : null, grade));
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