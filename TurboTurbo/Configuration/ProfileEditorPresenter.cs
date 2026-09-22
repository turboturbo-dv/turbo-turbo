using TurboTurbo.Runtime;

using UnityEngine;

namespace TurboTurbo.Configuration;

/// <summary>
/// Owns the profile editor's lifecycle.
/// </summary>
internal sealed class ProfileEditorPresenter : MonoBehaviour
{
    private static readonly Logger Log = TurboTurbo.Log.ForContext("editor");

    private ProfileEditor _editor;

    public static ProfileEditorPresenter Create()
    {
        var go = new GameObject("TurboTurbo.ProfileEditorPresenter");
        DontDestroyOnLoad(go);
        return go.AddComponent<ProfileEditorPresenter>();
    }

    public void Open(TrainCar car)
    {
        if (_editor != null)
        {
            Log.Info("profile editor already open");
            return;
        }

        if (car == null || !car.IsLoco || car.carLivery == null)
        {
            Log.Warn("profile editor needs a boarded locomotive");
            return;
        }

        if (Orchestrator.Instance == null)
        {
            Log.Warn("orchestrator not ready");
            return;
        }

        var host = Orchestrator.Instance.FindHost(car);
        var isCreate = host == null;
        if (isCreate)
        {
            host = ScratchHost.Create(car);
            if (host == null) return;
        }

        var go = new GameObject("TurboTurbo.ProfileEditor");
        DontDestroyOnLoad(go);
        go.AddComponent<TurboTooltipLayer>();
        var editor = go.AddComponent<ProfileEditor>();
        editor.Initialize(car, host, car.carLivery.id, isCreate);
        editor.Closed += OnEditorClosed;
        go.AddComponent<WindowBlocker>().Track(() => editor.WindowRect);
        _editor = editor;
    }

    public void Close()
    {
        if (_editor == null) return;

        var editor = _editor;
        _editor = null;
        editor.Closed -= OnEditorClosed;
        Destroy(editor.gameObject);
    }

    private void OnEditorClosed() => _editor = null;
}