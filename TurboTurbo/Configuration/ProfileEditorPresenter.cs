using TurboTurbo.Profiles;
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

    public static ProfileEditorPresenter Instance { get; private set; }

    public static ProfileEditorPresenter Create()
    {
        var go = new GameObject(Naming.Create("ProfileEditorPresenter"));
        DontDestroyOnLoad(go);
        var presenter = go.AddComponent<ProfileEditorPresenter>();
        Instance = presenter;
        return presenter;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        Close();
    }

    private void Update()
    {
        if (!SettingsStore.Current.OpenProfileEditor.Down()) return;

        var car = PlayerManager.Car;
        if (car == null || !car.IsLoco || car.carLivery == null)
        {
            Log.Info("open-profile-editor hotkey pressed with no boarded locomotive, ignoring");
            return;
        }

        Open(car);
    }

    public void Open(TrainCar car)
    {
        if (_editor != null)
        {
            return;
        }

        if (car == null || !car.IsLoco || car.carLivery == null)
        {
            Log.Warn($"cannot open profile editor for {car.LogIdentifier()}: it does not look like a valid locomotive");
            return;
        }

        if (!DieselEngineBinder.Supports(car))
        {
            Log.Warn($"cannot open profile editor for {car.LogIdentifier()}: no diesel engine could be detected");
            return;
        }

        if (Orchestrator.Instance == null)
        {
            Log.Warn("cannot open profile editor: the orchestrator is not running");
            return;
        }

        if (SettingsStore.Current.IsAuthoring)
        {
            // switching the authoring target does not reload hosts, so the car
            // may still be running the previous target's profile
            Orchestrator.Instance.ReloadHost(car);
        }

        var host = Orchestrator.Instance.FindHost(car);
        if (host == null)
        {
            var result = ScratchHost.Create(car);
            if (!result.IsSuccess)
            {
                Log.Warn($"cannot create a profile for {car.LogIdentifier()}: {result.Error}");
                return;
            }

            host = result.Value;
        }

        var go = new GameObject(Naming.Create("ProfileEditor"));
        DontDestroyOnLoad(go);
        var editor = go.AddComponent<ProfileEditor>();
        editor.Initialize(car, host, car.carLivery.id);
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
