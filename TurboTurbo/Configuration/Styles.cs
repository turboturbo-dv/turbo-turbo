using System.Collections.Generic;

using UnityEngine;

namespace TurboTurbo.Configuration;

internal static class Styles
{
    public const float LabelWidth = 165f;

    public const float DefaultWindowWidth = 450f;

    public const int ProfileOverviewRowMargin = 4;

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

    private static readonly Color TitleIdle = new Color32(0xC8, 0xC8, 0xC8, 0xFF);

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

    private static GUIStyle _overviewBox;

    public static GUIStyle OverviewBox
    {
        get
        {
            if (_overviewBox == null)
            {
                _overviewBox = new GUIStyle(GUI.skin.box);
                _overviewBox.normal.background = FlatBackground(0x2C);
            }

            return _overviewBox;
        }
    }

    private static GUIStyle _authoringBox;

    public static GUIStyle AuthoringBox
    {
        get
        {
            if (_authoringBox == null)
            {
                _authoringBox = new GUIStyle(GUI.skin.box);
                _authoringBox.normal.background = FlatBackground(0x2A);
                _authoringBox.padding = new RectOffset(8, 8, 6, 6);
            }

            return _authoringBox;
        }
    }

    private static GUIStyle _boldLabel;

    public static GUIStyle BoldLabel
    {
        get
        {
            if (_boldLabel == null)
            {
                _boldLabel = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold };
                _boldLabel.margin = new RectOffset(ProfileOverviewRowMargin, ProfileOverviewRowMargin, 0, 0);
            }

            return _boldLabel;
        }
    }

    private static GUIStyle _rowLabel;

    public static GUIStyle RowLabel
    {
        get
        {
            if (_rowLabel == null)
            {
                _rowLabel = new GUIStyle(GUI.skin.label);
                _rowLabel.margin = new RectOffset(ProfileOverviewRowMargin, ProfileOverviewRowMargin, 0, 0);
            }

            return _rowLabel;
        }
    }

    private static GUIStyle _emptySlot;

    public static GUIStyle EmptySlot
    {
        get
        {
            if (_emptySlot == null)
            {
                _emptySlot = new GUIStyle(GUI.skin.label);
                _emptySlot.margin = new RectOffset(ProfileOverviewRowMargin, ProfileOverviewRowMargin, 0, 0);
            }

            return _emptySlot;
        }
    }

    private static GUIStyle _actionButton;

    public static GUIStyle ActionButton
    {
        get
        {
            if (_actionButton == null)
            {
                _actionButton = new GUIStyle(GUI.skin.button);
                _actionButton.margin = new RectOffset(ProfileOverviewRowMargin, ProfileOverviewRowMargin, 0, 0);
            }

            return _actionButton;
        }
    }

    private static GUIStyle _keybindButton;

    public static GUIStyle KeybindButton
    {
        get
        {
            if (_keybindButton == null)
            {
                _keybindButton = new GUIStyle(GUI.skin.button);
                _keybindButton.margin = new RectOffset(0, 0, 0, 0);
            }

            return _keybindButton;
        }
    }

    private static Texture2D FlatBackground(byte shade) => Solid(new Color32(shade, shade, shade, 0xFF));

    private static Texture2D Solid(Color32 color)
    {
        var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        texture.hideFlags = HideFlags.HideAndDontSave;
        texture.SetPixel(0, 0, color);
        texture.Apply();
        return texture;
    }

    private const int PillHeight = 18;
    private const int PillRadius = 9;
    private const int PillWidth = 30;
    private const float PillDotX = 10f;
    private const float PillDotRadius = 3f;

    private static readonly Dictionary<(Color32 Fill, Color32 Dot, Color32 Text), GUIStyle> PillStyles = new();

    public static GUIStyle Pill(Color32 fill, Color32 dot, Color32 text)
    {
        var key = (fill, dot, text);
        if (PillStyles.TryGetValue(key, out var cached)) return cached;

        var style = new GUIStyle(GUI.skin.label)
        {
            normal = { background = PillTexture(fill, dot), textColor = text },
            border = new RectOffset(18, 9, 0, 0),
            padding = new RectOffset(20, 8, 0, 0),
            margin = new RectOffset(ProfileOverviewRowMargin, ProfileOverviewRowMargin, 0, 0),
            alignment = TextAnchor.MiddleLeft,
            fontSize = 13,
            fixedHeight = PillHeight,
        };

        PillStyles[key] = style;
        return style;
    }

    private static Texture2D PillTexture(Color32 fill, Color32 dot)
    {
        var texture = NewTexture(PillWidth, PillHeight);
        var fillColor = (Color)fill;
        var dotColor = (Color)dot;
        var centerY = (PillHeight - 1) / 2f;
        var bodyX = (PillWidth - 1) / 2f - PillRadius;
        var bodyY = (PillHeight - 1) / 2f - PillRadius;

        for (var y = 0; y < PillHeight; y++)
        {
            for (var x = 0; x < PillWidth; x++)
            {
                var qx = Mathf.Max(Mathf.Abs(x - (PillWidth - 1) / 2f) - bodyX, 0f);
                var qy = Mathf.Max(Mathf.Abs(y - centerY) - bodyY, 0f);
                var alpha = Mathf.Clamp01(0.5f - (Mathf.Sqrt(qx * qx + qy * qy) - PillRadius));

                var color = fillColor;
                var ddx = x - PillDotX;
                var ddy = y - centerY;
                var dotAlpha = Mathf.Clamp01(PillDotRadius - Mathf.Sqrt(ddx * ddx + ddy * ddy) + 0.5f);
                if (dotAlpha > 0f) color = Color.Lerp(color, dotColor, dotAlpha);

                texture.SetPixel(x, y, new Color(color.r, color.g, color.b, alpha));
            }
        }

        texture.Apply();
        return texture;
    }

    private static Texture2D NewTexture(int width, int height)
    {
        return new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            hideFlags = HideFlags.HideAndDontSave,
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
        };
    }

    private static GUIStyle _hline;

    public static void HLine()
    {
        if (_hline == null)
        {
            _hline = new GUIStyle
            {
                normal = { background = Solid(new Color32(0x3A, 0x3A, 0x3A, 0xFF)) },
                border = new RectOffset(0, 0, 0, 0),
                padding = new RectOffset(0, 0, 0, 0),
                margin = new RectOffset(0, 0, 0, 0),
            };
        }

        GUILayout.Space(2f);
        GUILayout.Box(GUIContent.none, _hline, GUILayout.Height(1f), GUILayout.ExpandWidth(true));
        GUILayout.Space(2f);
    }

    private static GUIStyle _enabledToggle;

    public static GUIStyle EnabledToggle
    {
        get
        {
            if (_enabledToggle == null)
            {
                _enabledToggle = new GUIStyle(GUI.skin.toggle);
                _enabledToggle.margin = new RectOffset(ProfileOverviewRowMargin, ProfileOverviewRowMargin, 0, 0);
            }

            return _enabledToggle;
        }
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

                // Unity highlights the window title with the onNormal state on hover
                _windowStyle.normal.textColor = TitleIdle;
                _windowStyle.focused.textColor = TitleIdle;
                _windowStyle.onFocused.textColor = TitleIdle;
                _windowStyle.hover.textColor = Color.white;
                _windowStyle.active.textColor = Color.white;
                _windowStyle.onNormal.textColor = Color.white;
                _windowStyle.onHover.textColor = Color.white;
                _windowStyle.onActive.textColor = Color.white;
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
