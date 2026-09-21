using UnityEngine;

namespace TurboTurbo.Configuration;

internal static class Styles
{
    public const float LabelWidth = 165f;

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