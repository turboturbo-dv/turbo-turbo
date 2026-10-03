using System;

using UnityEngine;
using UnityEngine.UI;

namespace TurboTurbo.Configuration;

/// <summary>
/// A click-through blocker that can be applied to IMGUI windows. Because the
/// game has a separate event stack, blocking clicks on IMGUI itself does
/// not prevent click-through. This blocker 
/// </summary>
internal sealed class WindowBlocker : MonoBehaviour
{
    private Func<Rect> _windowRect;
    private GameObject _canvas;
    private RectTransform _blocker;

    public void Track(Func<Rect> windowRect)
    {
        _windowRect = windowRect;
    }

    /// <summary>Enables or disables click through blocking.</summary>
    public void SetBlocking(bool on)
    {
        if (_canvas != null) _canvas.SetActive(on);
    }

    private void Awake()
    {
        var canvas = new GameObject("Blocker Canvas", typeof(Canvas), typeof(GraphicRaycaster));
        canvas.transform.SetParent(transform, worldPositionStays: false);
        var canvasComponent = canvas.GetComponent<Canvas>();
        canvasComponent.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasComponent.sortingOrder = 32767;
        _canvas = canvas;

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
        if (_windowRect == null || _blocker == null || _canvas == null || !_canvas.activeSelf) return;

        var rect = _windowRect();
        _blocker.offsetMin = new Vector2(rect.x, -(rect.y + rect.height));
        _blocker.offsetMax = new Vector2(rect.x + rect.width, -rect.y);
    }
}