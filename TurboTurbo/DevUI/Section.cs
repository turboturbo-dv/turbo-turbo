using System;
using System.Collections.Generic;
using System.Text;

using UnityEngine;

namespace TurboTurbo.DevUI;

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

    public Section(string title, string yamlKey, Action onRequiresReconfigure = null)
    {
        Title = title;
        YamlKey = yamlKey;
        _onRequiresReconfigure = onRequiresReconfigure;
    }

    public void AddFloat(string key, string tooltip, float min, float max, bool requiresReconfigure,
        Func<float> get, Action<float> set)
    {
        _specs.Add(new FloatSpec(key, tooltip, get, set, min, max,
            requiresReconfigure ? _onRequiresReconfigure : null));
    }

    public void AddBool(string key, string tooltip, bool requiresReconfigure,
        Func<bool> get, Action<bool> set)
    {
        _specs.Add(new BoolSpec(key, tooltip, get, set,
            requiresReconfigure ? _onRequiresReconfigure : null));
    }

    public void AddKey(string key, string tooltip, Func<KeyCode> get, Action<KeyCode> set)
    {
        _specs.Add(new KeyCodeSpec(key, tooltip, get, set));
    }

    public ColorSpec AddColor(string key, string tooltip, Func<Color> get, Action<Color> set)
    {
        var spec = new ColorSpec(key, tooltip, get, set);
        _specs.Add(spec);
        return spec;
    }

    public void AddInt(string key, string tooltip, int min, int max, bool requiresReconfigure,
        Func<int> get, Action<int> set)
    {
        _specs.Add(new IntSpec(key, tooltip, get, set, min, max,
            requiresReconfigure ? _onRequiresReconfigure : null));
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
        var changed = ChangedCount;

        GUILayout.BeginHorizontal();
        var prevOpen = Open;
        Open = GUILayout.Toggle(Open, (Open ? "▾ " : "▸ ") + Title);
        if (Open != prevOpen && OnToggle != null) OnToggle();
        GUILayout.Label(changed > 0 ? $"{changed} changed" : "", GUILayout.Width(70f));
        if (changed > 0 && GUILayout.Button("reset", GUILayout.Width(46f)))
        {
            foreach (var spec in _specs) spec.Reset();
        }
        GUILayout.EndHorizontal();

        if (Open)
        {
            foreach (var spec in _specs) spec.Draw();
        }
    }
}