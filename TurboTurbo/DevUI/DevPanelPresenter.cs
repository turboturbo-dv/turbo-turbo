using UnityEngine;

namespace TurboTurbo.DevUI;

/// <summary>
/// Owns the dev panel's lifecycle: F6 toggles its existence. The window position
/// is the only piece of UI state that survives a close/open cycle.
/// </summary>
internal sealed class DevPanelPresenter : MonoBehaviour
{
    private const KeyCode ToggleKey = KeyCode.F6;

    private Rect _panelRect = new(20f, 20f, 360f, 120f);
    private TurboDevPanel _panel;

    public static DevPanelPresenter Create()
    {
        var go = new GameObject("TurboTurbo.DevPanelPresenter");
        DontDestroyOnLoad(go);
        return go.AddComponent<DevPanelPresenter>();
    }

    private void Update()
    {
        if (!Input.GetKeyDown(ToggleKey)) return;

        if (_panel == null)
        {
            _panel = TurboDevPanel.Create(_panelRect);
        }
        else
        {
            _panelRect = _panel.WindowRect;
            Destroy(_panel.gameObject);
            _panel = null;
        }
    }
}