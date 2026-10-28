using UnityEngine;

namespace TurboTurbo.Configuration;

/// <summary>Clickable header of a collapsible block.</summary>
internal static class Foldout
{
    /// <summary>
    /// Draws the header; returns whether it was clicked. Note that state tracking is a responsibility of the caller.
    /// </summary>
    public static bool Header(bool open, GUIContent title, params GUILayoutOption[] options)
    {
        var content = new GUIContent((open ? "▾ " : "▸ ") + title.text, title.tooltip);
        return GUILayout.Button(content, Styles.SectionHeader, options);
    }
}
