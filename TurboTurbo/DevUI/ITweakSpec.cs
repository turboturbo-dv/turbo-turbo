using System;
using System.Collections.Generic;
using System.Globalization;

using UnityEngine;

namespace TurboTurbo.DevUI;

// float equality comp is actually fine here, since we only need to check for exact matches
// ReSharper disable CompareOfFloatsByEqualityOperator

internal interface ITweakSpec
{
    string Key { get; }
    bool Changed { get; }
    void Reset();
    void Draw();

    /// <summary>
    /// Exports the value of this spec as YAML string, null if unchanged
    /// </summary>
    string Export();
}

internal abstract class SpecBase<T>
{
    private readonly string _key;
    protected readonly Func<T> _get;
    protected readonly Action<T> _set;
    protected readonly T _initial;
    private readonly Action _onRequiresReconfigure;
    protected readonly bool _requiresReconfigure;
    protected readonly GUIContent _label;

    protected SpecBase(string key, string tooltip, Func<T> get, Action<T> set, Action onRequiresReconfigure)
    {
        _key = key;
        _label = new GUIContent(key, tooltip);
        _get = get;
        _set = set;
        _initial = get();
        _onRequiresReconfigure = onRequiresReconfigure;
        _requiresReconfigure = onRequiresReconfigure != null;
    }

    public string Key => _key;

    public bool Changed => !EqualityComparer<T>.Default.Equals(_get(), _initial);

    protected void Commit(T value)
    {
        _set(value);
        if (_requiresReconfigure) _onRequiresReconfigure();
    }

    public void Reset()
    {
        if (!Changed) return;
        Commit(_initial);
    }
}

internal sealed class IntSpec : SpecBase<int>, ITweakSpec
{
    private readonly int _min;
    private readonly int _max;
    private string _editText;

    public IntSpec(string key, string tooltip, Func<int> get, Action<int> set,
        int min, int max, Action onRequiresReconfigure)
        : base(key, tooltip, get, set, onRequiresReconfigure)
    {
        _min = min;
        _max = max;
    }

    public void Draw()
    {
        int current = _get();
        GUILayout.BeginHorizontal();
        GUILayout.Label(_label, GUILayout.Width(TurboDevPanel.LabelWidth));
        int sliderV = Mathf.RoundToInt(GUILayout.HorizontalSlider(current, _min, _max));
        if (sliderV != current)
        {
            Commit(sliderV);
            _editText = null;
        }

        string shown = _editText ?? current.ToString(CultureInfo.InvariantCulture);
        string typed = GUILayout.TextField(shown, GUILayout.Width(48f));
        if (typed != shown)
        {
            _editText = typed;
            if (int.TryParse(typed, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed))
            {
                int clamped = Mathf.Clamp(parsed, _min, _max);
                if (clamped != current) Commit(clamped);
            }
        }

        GUILayout.EndHorizontal();
    }

    public string Export() => Changed ? $"  {Key}: {_get()}" : null;
}

internal sealed class FloatSpec : SpecBase<float>, ITweakSpec
{
    private readonly float _min;
    private readonly float _max;
    private string _editText;

    public FloatSpec(string key, string tooltip, Func<float> get, Action<float> set,
        float min, float max, Action onRequiresReconfigure)
        : base(key, tooltip, get, set, onRequiresReconfigure)
    {
        _min = min;
        _max = max;
    }

    public void Draw()
    {
        float current = _get();
        GUILayout.BeginHorizontal();
        GUILayout.Label(_label, GUILayout.Width(TurboDevPanel.LabelWidth));
        float sliderV = GUILayout.HorizontalSlider(current, _min, _max);
        if (sliderV != current)
        {
            Commit(sliderV);
            _editText = null;
        }

        string shown = _editText ?? Format(current);
        string typed = GUILayout.TextField(shown, GUILayout.Width(48f));
        if (typed != shown)
        {
            _editText = typed;
            if (float.TryParse(typed, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed))
            {
                float clamped = Mathf.Clamp(parsed, _min, _max);
                if (clamped != current) Commit(clamped);
            }
        }

        GUILayout.EndHorizontal();
    }

    public string Export() => Changed ? $"  {Key}: {Format(_get())}" : null;

    private static string Format(float v) => v.ToString("0.#####", CultureInfo.InvariantCulture);
}

internal sealed class BoolSpec : SpecBase<bool>, ITweakSpec
{
    public BoolSpec(string key, string tooltip, Func<bool> get, Action<bool> set, Action onRequiresReconfigure)
        : base(key, tooltip, get, set, onRequiresReconfigure)
    {
    }

    public void Draw()
    {
        bool value = GUILayout.Toggle(_get(), _label);
        if (value != _get()) Commit(value);
    }

    public string Export() => Changed ? $"  {Key}: {_get().ToString().ToLowerInvariant()}" : null;
}