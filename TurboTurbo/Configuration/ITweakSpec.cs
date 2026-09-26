using System;
using System.Collections.Generic;
using System.Globalization;

using UnityEngine;

namespace TurboTurbo.Configuration;

// float equality comp is actually fine here, since we only need to check for exact matches
// ReSharper disable CompareOfFloatsByEqualityOperator

internal interface ITweakSpec
{
    string Key { get; }
    bool Changed { get; }
    TweakGrade Grade { get; }
    void Reset();
    void Draw();
}

internal abstract class SpecBase<T>
{
    protected readonly Func<T> _get;
    protected readonly Action<T> _set;
    protected readonly T _initial;
    private readonly Action _onRequiresReconfigure;
    protected readonly bool _requiresReconfigure;
    protected readonly GUIContent _label;

    public string Key { get; }

    public bool Changed => !EqualityComparer<T>.Default.Equals(_get(), _initial);

    public TweakGrade Grade { get; }

    protected SpecBase(string key, string tooltip, Func<T> get, Action<T> set, Action onRequiresReconfigure, TweakGrade grade)
    {
        Key = key;
        Grade = grade;
        _label = new GUIContent(key, tooltip);
        _get = get;
        _set = set;
        _initial = get();
        _onRequiresReconfigure = onRequiresReconfigure;
        _requiresReconfigure = onRequiresReconfigure != null;
    }

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
        int min, int max, Action onRequiresReconfigure, TweakGrade grade)
        : base(key, tooltip, get, set, onRequiresReconfigure, grade)
    {
        _min = min;
        _max = max;
    }

    public void Draw()
    {
        var current = _get();
        GUILayout.BeginHorizontal();
        GUILayout.Label(_label, GUILayout.Width(Styles.LabelWidth));
        var sliderV = Mathf.RoundToInt(GUILayout.HorizontalSlider(current, _min, _max));
        if (sliderV != current)
        {
            Commit(sliderV);
            _editText = null;
        }

        var shown = _editText ?? current.ToString(CultureInfo.InvariantCulture);
        var typed = GUILayout.TextField(shown, GUILayout.Width(60f));
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

}

internal sealed class FloatSpec : SpecBase<float>, ITweakSpec
{
    private readonly float _min;
    private readonly float _max;
    private string _editText;

    public FloatSpec(string key, string tooltip, Func<float> get, Action<float> set,
        float min, float max, Action onRequiresReconfigure, TweakGrade grade)
        : base(key, tooltip, get, set, onRequiresReconfigure, grade)
    {
        _min = min;
        _max = max;
    }

    public void Draw()
    {
        var current = _get();
        GUILayout.BeginHorizontal();
        GUILayout.Label(_label, GUILayout.Width(Styles.LabelWidth));
        var sliderV = GUILayout.HorizontalSlider(current, _min, _max);
        if (sliderV != current)
        {
            Commit(sliderV);
            _editText = null;
        }

        var shown = _editText ?? Format(current);
        var typed = GUILayout.TextField(shown, GUILayout.Width(60f));
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

    private static string Format(float v) => v.ToString("0.#####", CultureInfo.InvariantCulture);
}

internal sealed class BoolSpec : SpecBase<bool>, ITweakSpec
{
    public BoolSpec(string key, string tooltip, Func<bool> get, Action<bool> set, Action onRequiresReconfigure, TweakGrade grade)
        : base(key, tooltip, get, set, onRequiresReconfigure, grade)
    {
    }

    public void Draw()
    {
        var value = GUILayout.Toggle(_get(), _label);
        if (value != _get()) Commit(value);
    }

}

internal sealed class ColorSpec : SpecBase<Color>, ITweakSpec
{
    /// <summary>Raised when the swatch is clicked; the panel opens its shared picker for this spec.</summary>
    public event Action RequestEdit;

    public ColorSpec(string key, string tooltip, Func<Color> get, Action<Color> set, TweakGrade grade)
        : base(key, tooltip, get, set, null, grade)
    {
    }

    public Color Value => _get();

    public void SetValue(Color value) => Commit(value);

    public void Draw()
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label(_label, GUILayout.Width(Styles.LabelWidth));
        var prev = GUI.backgroundColor;
        GUI.backgroundColor = _get();
        if (GUILayout.Button(GUIContent.none, GUILayout.Width(120f), GUILayout.Height(18f))) RequestEdit?.Invoke();
        GUI.backgroundColor = prev;
        GUILayout.EndHorizontal();
    }

}

internal sealed class ProgressButtonSpec : ITweakSpec
{
    private readonly string _tooltip;
    private readonly Action _action;
    private readonly Func<float> _progress;

    public ProgressButtonSpec(string key, string tooltip, Action action, TweakGrade grade, Func<float> progress)
    {
        Key = key;
        Grade = grade;
        _tooltip = tooltip;
        _action = action;
        _progress = progress;
    }

    public string Key { get; }

    public TweakGrade Grade { get; }

    public bool Changed => false;

    public void Reset()
    {
    }

    public void Draw()
    {
        var button = GUI.skin.button;
        var height = button.CalcHeight(new GUIContent(Key), Styles.LabelWidth);
        var rect = GUILayoutUtility.GetRect(Styles.LabelWidth, height);

        if (GUI.Button(rect, GUIContent.none, button)) _action();

        var progress = _progress != null ? Mathf.Clamp01(_progress()) : 0f;
        if (progress > 0f)
        {
            var border = button.border;
            var insetX = (float)Mathf.Max(border.left, border.right);
            var insetY = (float)Mathf.Max(border.top, border.bottom);
            if (insetX <= 0f) insetX = 3f;
            if (insetY <= 0f) insetY = 3f;

            var inner = new Rect(rect.x + insetX, rect.y + insetY,
                rect.width - insetX * 2f, rect.height - insetY * 2f);

            var prev = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, 0.7f);
            GUI.DrawTexture(new Rect(inner.x, inner.y, inner.width * progress, inner.height),
                Styles.ProgressFill, ScaleMode.StretchToFill);
            GUI.color = prev;
        }

        var label = _progress != null ? $"{Key}  {Mathf.RoundToInt(progress * 100f)}%" : Key;
        GUI.Label(rect, new GUIContent(label, _tooltip), Styles.ProgressLabel);
    }
}