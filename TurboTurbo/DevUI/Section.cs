using System;
using System.Collections.Generic;
using System.Text;

using UnityEngine;

namespace TurboTurbo.DevUI;

internal sealed class Section
{
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
            int n = 0;
            foreach (ITweakSpec spec in _specs)
            {
                if (spec.Changed) n++;
            }
            return n;
        }
    }

    public void ExportYaml(StringBuilder sb)
    {
        var lines = new List<string>();
        foreach (ITweakSpec spec in _specs)
        {
            string line = spec.Export();
            if (line != null) lines.Add(line);
        }
        if (lines.Count == 0) return;

        sb.Append(YamlKey).AppendLine(":");
        foreach (string line in lines)
        {
            sb.AppendLine(line);
        }
    }

    public void Draw()
    {
        int changed = ChangedCount;

        GUILayout.BeginHorizontal();
        bool prevOpen = Open;
        Open = GUILayout.Toggle(Open, (Open ? "▾ " : "▸ ") + Title);
        if (Open != prevOpen && OnToggle != null) OnToggle();
        GUILayout.Label(changed > 0 ? $"{changed} changed" : "", GUILayout.Width(70f));
        if (changed > 0 && GUILayout.Button("reset", GUILayout.Width(46f)))
        {
            foreach (ITweakSpec spec in _specs) spec.Reset();
        }
        GUILayout.EndHorizontal();

        if (Open)
        {
            foreach (ITweakSpec spec in _specs) spec.Draw();
        }
    }
}
