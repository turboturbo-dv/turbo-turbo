using UnityEngine;

namespace TurboTurbo.Configuration;

internal static class Styles
{
    public const float LabelWidth = 165f;

    private static GUIStyle _separator;

    public static void Separator()
    {
        if (_separator == null)
        {
            _separator = new GUIStyle
            {
                normal = { background = FlatBackground(0x5A) },
                border = new RectOffset(0, 0, 0, 0),
                padding = new RectOffset(0, 0, 0, 0),
                margin = new RectOffset(0, 0, 0, 0),
            };
        }

        GUILayout.Space(4f);
        GUILayout.Box(GUIContent.none, _separator, GUILayout.Height(1f), GUILayout.ExpandWidth(true));
        GUILayout.Space(4f);
    }

    private static GUIStyle _wrappedLabel;

    public static GUIStyle WrappedLabel
    {
        get
        {
            if (_wrappedLabel == null)
            {
                _wrappedLabel = new GUIStyle(GUI.skin.label) { wordWrap = true };
            }

            return _wrappedLabel;
        }
    }

    private static GUIStyle _sectionHeader;

    public static GUIStyle SectionHeader
    {
        get
        {
            if (_sectionHeader == null)
            {
                _sectionHeader = new GUIStyle(GUI.skin.button) { alignment = TextAnchor.MiddleLeft };
            }

            return _sectionHeader;
        }
    }

    public static readonly Color TooltipBackground = new Color32(0x1F, 0x1F, 0x1F, 0xF7);

    private static GUIStyle _telemetryBox;

    public static GUIStyle TelemetryBox
    {
        get
        {
            if (_telemetryBox == null)
            {
                _telemetryBox = new GUIStyle(GUI.skin.box);
                _telemetryBox.normal.background = FlatBackground(0x28);
            }

            return _telemetryBox;
        }
    }

    private static Texture2D FlatBackground(byte shade)
    {
        var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        texture.hideFlags = HideFlags.HideAndDontSave;
        texture.SetPixel(0, 0, new Color32(shade, shade, shade, 0xFF));
        texture.Apply();
        return texture;
    }

    private static Texture2D _progressFill;

    public static Texture2D ProgressFill
    {
        get
        {
            if (_progressFill == null) _progressFill = FlatBackground(0x60);
            return _progressFill;
        }
    }

    private static GUIStyle _progressLabel;

    public static GUIStyle ProgressLabel
    {
        get
        {
            if (_progressLabel == null)
            {
                _progressLabel = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter };
                _progressLabel.normal.textColor = GUI.skin.button.normal.textColor;
            }

            return _progressLabel;
        }
    }

    private const byte BackgroundShade = 0x32;

    private static Texture2D _background;
    private static GUIStyle _windowStyle;

    public static GUIStyle Window
    {
        get
        {
            if (_windowStyle == null)
            {
                _windowStyle = new GUIStyle(GUI.skin.window);
                _background = OpaqueBackground(_windowStyle.normal.background);
                _windowStyle.normal.background = _background;
                _windowStyle.hover.background = _background;
                _windowStyle.active.background = _background;
                _windowStyle.focused.background = _background;
                _windowStyle.onNormal.background = _background;
                _windowStyle.onHover.background = _background;
                _windowStyle.onActive.background = _background;
                _windowStyle.onFocused.background = _background;
            }

            return _windowStyle;
        }
    }

    private static Texture2D OpaqueBackground(Texture2D source)
    {
        var background = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        background.hideFlags = HideFlags.HideAndDontSave;

        try
        {
            var tinted = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
            tinted.hideFlags = HideFlags.HideAndDontSave;
            var pixels = source.GetPixels32();
            for (var i = 0; i < pixels.Length; i++)
            {
                var p = pixels[i];
                pixels[i] = new Color32(
                    (byte)(p.r * BackgroundShade / 255),
                    (byte)(p.g * BackgroundShade / 255),
                    (byte)(p.b * BackgroundShade / 255),
                    0xFF);
            }

            tinted.SetPixels32(pixels);
            tinted.Apply();
            Object.Destroy(background);
            return tinted;
        }
        catch
        {
            background.SetPixel(0, 0, new Color32(BackgroundShade, BackgroundShade, BackgroundShade, 0xFF));
            background.Apply();
            return background;
        }
    }
}