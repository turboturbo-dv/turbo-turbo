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
        var current = _get();
        GUILayout.BeginHorizontal();
        GUILayout.Label(_label, GUILayout.Width(TurboDevPanel.LabelWidth));
        var sliderV = Mathf.RoundToInt(GUILayout.HorizontalSlider(current, _min, _max));
        if (sliderV != current)
        {
            Commit(sliderV);
            _editText = null;
        }

        var shown = _editText ?? current.ToString(CultureInfo.InvariantCulture);
        var typed = GUILayout.TextField(shown, GUILayout.Width(48f));
        if (typed != shown)
        {
            _editText = typed;
            if (int.TryParse(typed, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
            {
                var clamped = Mathf.Clamp(parsed, _min, _max);
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
        var current = _get();
        GUILayout.BeginHorizontal();
        GUILayout.Label(_label, GUILayout.Width(TurboDevPanel.LabelWidth));
        var sliderV = GUILayout.HorizontalSlider(current, _min, _max);
        if (sliderV != current)
        {
            Commit(sliderV);
            _editText = null;
        }

        var shown = _editText ?? Format(current);
        var typed = GUILayout.TextField(shown, GUILayout.Width(48f));
        if (typed != shown)
        {
            _editText = typed;
            if (float.TryParse(typed, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
            {
                var clamped = Mathf.Clamp(parsed, _min, _max);
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
        var value = GUILayout.Toggle(_get(), _label);
        if (value != _get()) Commit(value);
    }

    public string Export() => Changed ? $"  {Key}: {_get().ToString().ToLowerInvariant()}" : null;
}

internal sealed class ColorSpec : SpecBase<Color>, ITweakSpec
{
    /// <summary>Raised when the swatch is clicked; the panel opens its shared picker for this spec.</summary>
    public event Action RequestEdit;

    public ColorSpec(string key, string tooltip, Func<Color> get, Action<Color> set)
        : base(key, tooltip, get, set, null)
    {
    }

    public Color Value => _get();

    public void SetValue(Color value) => Commit(value);

    public void Draw()
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label(_label, GUILayout.Width(TurboDevPanel.LabelWidth));
        var prev = GUI.backgroundColor;
        GUI.backgroundColor = _get();
        if (GUILayout.Button(GUIContent.none, GUILayout.Width(120f), GUILayout.Height(18f))) RequestEdit?.Invoke();
        GUI.backgroundColor = prev;
        GUILayout.EndHorizontal();
    }

    public string Export() => Changed
        ? $"  {Key}: #{ColorUtility.ToHtmlStringRGBA(_get())}"
        : null;
}

internal sealed class ButtonSpec : ITweakSpec
{
    private readonly string _key;
    private readonly string _tooltip;
    private readonly Action _action;

    public ButtonSpec(string key, string tooltip, Action action)
    {
        _key = key;
        _tooltip = tooltip;
        _action = action;
    }

    public string Key => _key;

    public bool Changed => false;

    public void Reset()
    {
    }

    public void Draw()
    {
        if (GUILayout.Button(new GUIContent(_key, _tooltip))) _action();
    }

    public string Export() => null;
}

internal sealed class KeyCodeSpec : SpecBase<KeyCode>, ITweakSpec
{
    private bool _capturing;

    public KeyCodeSpec(string key, string tooltip, Func<KeyCode> get, Action<KeyCode> set)
        : base(key, tooltip, get, set, null)
    {
    }

    public void Draw()
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label(_label, GUILayout.Width(TurboDevPanel.LabelWidth));

        var buttonText = _capturing ? "press a key..."
            : _get() == KeyCode.None ? "(none)"
            : _get().ToString();
        if (GUILayout.Button(buttonText, GUILayout.Width(120f))) _capturing = true;

        GUILayout.EndHorizontal();

        if (!_capturing) return;

        var e = Event.current;
        if (e == null || e.type != EventType.KeyDown) return;
        e.Use();

        _capturing = false;
        // escape clears the binding instead of claiming escape itself
        Commit(e.keyCode == KeyCode.Escape ? KeyCode.None : e.keyCode);
    }

    public string Export() => Changed ? $"  {Key}: {_get()}" : null;
}