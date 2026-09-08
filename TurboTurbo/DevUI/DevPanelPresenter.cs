using TurboTurbo.Configuration;

using UnityEngine;

namespace TurboTurbo.DevUI;

/// <summary>
/// Owns the dev panel's lifecycle: F6 toggles its existence.
/// Also tracks state across panel destruction (currently only window position).
/// </summary>
internal sealed class DevPanelPresenter : MonoBehaviour
{
    private Settings _settings;

    private Rect _panelRect = new(20f, 20f, 360f, 120f);
    private TurboDevPanel _panel;

    public static DevPanelPresenter Instance { get; private set; }

    public static DevPanelPresenter Create(Settings settings)
    {
        var go = new GameObject("TurboTurbo.DevPanelPresenter");
        DontDestroyOnLoad(go);
        var presenter = go.AddComponent<DevPanelPresenter>();
        presenter._settings = settings;
        Instance = presenter;
        return presenter;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        DevCommands.TryRegister(this);

        if (Input.GetKeyDown(_settings.ToggleDevPanelKey)) Toggle();
    }

    /// <summary>
    /// Creates or destroys the panel, returning the new state.
    /// </summary>
    internal bool Toggle()
    {
        if (_panel == null)
        {
            _panel = TurboDevPanel.Create(_panelRect);
            return true;
        }

        _panelRect = _panel.WindowRect;
        Destroy(_panel.gameObject);
        _panel = null;
        return false;
    }
}