using System.Globalization;

using UnityEngine;

namespace TurboTurbo.Configuration;

internal sealed class ColorPickerWindow : MonoBehaviour
{
    private Rect _rect = new(400f, 20f, 240f, 180f);
    private ColorSpec _spec;
    private string _hexEdit;
    private readonly string[] _channelEdit = new string[4];
    private WindowBlocker _blocker;

    private void Awake()
    {
        _blocker = gameObject.AddComponent<WindowBlocker>();
        _blocker.Track(() => _rect);
        _blocker.SetBlocking(false);
    }

    public void Open(ColorSpec spec)
    {
        _spec = spec;
        _hexEdit = null;
        for (var i = 0; i < _channelEdit.Length; i++) _channelEdit[i] = null;
        _blocker.SetBlocking(true);
    }

    public void Close()
    {
        _spec = null;
        _blocker.SetBlocking(false);
    }

    private void OnGUI()
    {
        if (_spec == null) return;

        _rect = GUILayout.Window(GetInstanceID(), _rect, DrawWindow, _spec.Key, Styles.Window);
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
        c.r = ChannelSlider(0, "R", c.r);
        c.g = ChannelSlider(1, "G", c.g);
        c.b = ChannelSlider(2, "B", c.b);
        c.a = ChannelSlider(3, "A", c.a);
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
        if (GUILayout.Button("Close")) Close();
        if (GUILayout.Button("Copy hex")) GUIUtility.systemCopyBuffer = $"#{ColorUtility.ToHtmlStringRGBA(c)}";
        GUILayout.EndHorizontal();

        var e = Event.current;
        if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape)
        {
            e.Use();
            Close();
        }

        GUI.DragWindow();
    }

    private float ChannelSlider(int index, string label, float value)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label(label, GUILayout.Width(24f));
        var v = GUILayout.HorizontalSlider(value, 0f, 1f);
        if (v != value) _channelEdit[index] = null;

        var shown = _channelEdit[index]
                    ?? Mathf.RoundToInt(Mathf.Clamp01(v) * 255f).ToString(CultureInfo.InvariantCulture);
        var typed = GUILayout.TextField(shown, GUILayout.Width(30f));
        if (typed != shown)
        {
            _channelEdit[index] = typed;
            if (int.TryParse(typed, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
            {
                v = Mathf.Clamp(parsed, 0, 255) / 255f;
            }
        }
        GUILayout.EndHorizontal();
        return v;
    }
}
