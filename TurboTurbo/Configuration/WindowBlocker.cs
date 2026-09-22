using System;

using UnityEngine;
using UnityEngine.UI;

namespace TurboTurbo.Configuration;

/// <summary>
/// Transparent uGUI shield fitted to an IMGUI window. Stops clicks on the window
/// from also reaching game UI behind it.
/// </summary>
internal sealed class WindowBlocker : MonoBehaviour
{
    private Func<Rect> _windowRect;
    private RectTransform _blocker;

    public void Track(Func<Rect> windowRect)
    {
        _windowRect = windowRect;
    }

    private void Awake()
    {
        var canvas = new GameObject("Blocker Canvas", typeof(Canvas), typeof(GraphicRaycaster));
        canvas.transform.SetParent(transform, worldPositionStays: false);
        var canvasComponent = canvas.GetComponent<Canvas>();
        canvasComponent.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasComponent.sortingOrder = 32767;

        var image = new GameObject("Blocker", typeof(Image));
        image.transform.SetParent(canvas.transform, worldPositionStays: false);
        var imageComponent = image.GetComponent<Image>();
        imageComponent.color = new Color(0f, 0f, 0f, 0f);
        imageComponent.raycastTarget = true;
        _blocker = image.GetComponent<RectTransform>();
        _blocker.anchorMin = new Vector2(0f, 1f);
        _blocker.anchorMax = new Vector2(0f, 1f);
    }

    private void Update()
    {
        if (_windowRect == null || _blocker == null) return;

        var rect = _windowRect();
        _blocker.offsetMin = new Vector2(rect.x, -(rect.y + rect.height));
        _blocker.offsetMax = new Vector2(rect.x + rect.width, -rect.y);
    }
}