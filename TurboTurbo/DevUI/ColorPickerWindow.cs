using UnityEngine;

namespace TurboTurbo.DevUI;

internal sealed class ColorPickerWindow : MonoBehaviour
{
    private Rect _rect = new(400f, 20f, 240f, 180f);
    private ColorSpec _spec;
    private string _hexEdit;

    public void Open(ColorSpec spec)
    {
        _spec = spec;
        _hexEdit = null;
    }

    public void Close() => _spec = null;

    private void OnGUI()
    {
        if (_spec == null) return;

        _rect = GUILayout.Window(GetInstanceID(), _rect, DrawWindow, _spec.Key);
    }

    private void DrawWindow(int id)
    {
        var c = _spec.Value;

        var rect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none,
            GUILayout.Height(48f), GUILayout.ExpandWidth(true));
        var prev = GUI.color;
        GUI.color = c;
        GUI.DrawTexture(rect, Texture2D.whiteTexture, ScaleMode.StretchToFill);
        GUI.color = prev;

        var before = c;
        c.r = LabeledSlider("R", c.r);
        c.g = LabeledSlider("G", c.g);
        c.b = LabeledSlider("B", c.b);
        c.a = LabeledSlider("A", c.a);
        if (c != before)
        {
            _spec.SetValue(c);
            _hexEdit = null;
        }

        GUILayout.BeginHorizontal();
        GUILayout.Label("hex", GUILayout.Width(40f));
        var shown = _hexEdit ?? $"#{ColorUtility.ToHtmlStringRGBA(c)}";
        var typed = GUILayout.TextField(shown);
        if (typed != shown)
        {
            _hexEdit = typed;
            if (ColorUtility.TryParseHtmlString(typed, out var parsed)) _spec.SetValue(parsed);
        }
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("close")) Close();
        if (GUILayout.Button("copy hex")) GUIUtility.systemCopyBuffer = $"#{ColorUtility.ToHtmlStringRGBA(c)}";
        GUILayout.EndHorizontal();

        var e = Event.current;
        if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape)
        {
            e.Use();
            Close();
        }

        GUI.DragWindow();
    }

    private static float LabeledSlider(string label, float value)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label(label, GUILayout.Width(24f));
        var v = GUILayout.HorizontalSlider(value, 0f, 1f);
        GUILayout.Label(v.ToString("0.00"), GUILayout.Width(40f));
        GUILayout.EndHorizontal();
        return v;
    }
}