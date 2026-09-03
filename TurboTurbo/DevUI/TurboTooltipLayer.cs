using UnityEngine;

namespace TurboTurbo.DevUI;

internal sealed class TurboTooltipLayer : MonoBehaviour
{
    public static string Tooltip = "";

    private GUIStyle _style;

    private void OnGUI()
    {
        if (string.IsNullOrEmpty(Tooltip)) return;

        // draw above the main panel
        GUI.depth = -1;

        if (_style == null)
        {
            _style = new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.UpperLeft,
                wordWrap = true,
                fontSize = 11,
                border = new RectOffset(0, 0, 0, 0),
                padding = new RectOffset(6, 6, 4, 4),
            };
            _style.normal.background = Texture2D.whiteTexture;
            _style.normal.textColor = Color.white;
        }

        var content = new GUIContent(Tooltip);
        const float maxWidth = 340f;
        var width = Mathf.Min(_style.CalcSize(content).x + 12f, maxWidth);
        var height = _style.CalcHeight(content, width) + 6f;

        var mouse = Event.current.mousePosition;
        var rect = new Rect(mouse.x + 15f, mouse.y + 15f, width, height);

        // keep the tooltip on screen when hovering near a screen edge
        rect.x = Mathf.Min(rect.x, Screen.width - rect.width - 4f);
        rect.y = Mathf.Min(rect.y, Screen.height - rect.height - 4f);

        var oldBg = GUI.backgroundColor;
        GUI.backgroundColor = new Color(0.08f, 0.08f, 0.10f, 0.97f);
        GUI.Box(rect, content, _style);
        GUI.backgroundColor = oldBg;
    }
}