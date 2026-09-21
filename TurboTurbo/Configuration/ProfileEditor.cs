using System;
using System.Collections.Generic;

using TurboTurbo.Configuration.Sections;
using TurboTurbo.Modeling;
using TurboTurbo.Profiles;
using TurboTurbo.Runtime;

using UnityEngine;

namespace TurboTurbo.Configuration;

/// <summary>
/// Minimal v1 profile editor: charger kind, lambda calibration, save/discard.
/// Acts on the boarded loco; creates a scratch host when it has none.
/// </summary>
internal sealed class ProfileEditor : MonoBehaviour
{
    private static readonly Logger Log = TurboTurbo.Log.ForContext("editor");
    private static readonly string[] ChargerOptions = { "Turbo", "Atmospheric" };

    internal event Action Closed;

    private TrainCar _car;
    private EngineSimulationHost _host;
    private string _liveryId;
    private bool _isCreate;
    private Rect _windowRect = new(460f, 20f, 450f, 170f);

    private EngineSimulationHost _boundHost;
    private readonly List<Section> _sections = new();
    private bool _needsShrink;

    public void Initialize(TrainCar car, EngineSimulationHost host, string liveryId, bool isCreate)
    {
        _car = car;
        _host = host;
        _liveryId = liveryId;
        _isCreate = isCreate;
    }

    private void OnDestroy()
    {
        Closed?.Invoke();
    }

    private void CloseSelf() => Destroy(gameObject);

    private void OnGUI()
    {
        if (_car == null || _host == null)
        {
            CloseSelf();
            return;
        }

        if (_needsShrink)
        {
            var shrinkRect = _windowRect;
            shrinkRect.height = 10f;
            _windowRect = shrinkRect;
            _needsShrink = false;
        }

        _windowRect = GUILayout.Window(GetInstanceID(), _windowRect, DrawWindow, $"Profile: {_liveryId} ({_car.ID})", Styles.Window);
    }

    private void DrawWindow(int id)
    {
        TelemetryView.Draw(_host);
        DrawChargerRow();
        DrawChargerSection();
        DrawFooter();

        // GUI.tooltip is only populated during repaint; capture then, so
        // other event passes don't overwrite it.
        if (Event.current.type == EventType.Repaint)
        {
            TurboTooltipLayer.Tooltip = GUI.tooltip;
        }

        GUI.DragWindow();
    }

    private void DrawChargerRow()
    {
        var selected = _host.Profile.ChargerKind == ChargerKind.Atmospheric ? 1 : 0;
        var next = GUILayout.Toolbar(selected, ChargerOptions);
        if (next != selected) SwitchCharger(next == 1 ? ChargerKind.Atmospheric : ChargerKind.Turbo);
    }

    private void DrawChargerSection()
    {
        if (_host != _boundHost)
        {
            _boundHost = _host;
            _sections.Clear();
        }

        if (_sections.Count == 0 && _host.Bound)
        {
            _sections.Add(ChargerSection.Build(_host, null, () => _needsShrink = true));
        }

        if (_sections.Count == 0)
        {
            GUILayout.Label("waiting for engine model…");
            return;
        }

        foreach (var section in _sections)
        {
            section.Draw();
        }
    }

    private void DrawFooter()
    {
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Save"))
        {
            var error = ProfileRepository.SaveProfile(_host.CloneProfile());
            if (error != null) Log.Warn($"could not save profile '{_liveryId}': {error}");
            else
            {
                Log.Info($"saved profile '{_liveryId}'");
                CloseSelf();
            }
        }

        if (GUILayout.Button("Discard"))
        {
            if (_isCreate)
            {
                Rebind(null);
            }
            else
            {
                Rebind(ProfileRepository.TryGetProfile(_car));
            }

            CloseSelf();
        }

        GUILayout.EndHorizontal();
    }

    private void SwitchCharger(ChargerKind kind)
    {
        var profile = _host.CloneProfile();
        profile.ChargerKind = kind;

        var error = profile.Complete();
        if (error != null)
        {
            Log.Warn($"cannot switch charger for '{_liveryId}': {error}");
            return;
        }

        Rebind(profile);
    }

    private void Rebind(LocoProfile profile)
    {
        if (_host != null)
        {
            Destroy(_host);
            _host = null;
        }

        if (profile != null)
        {
            _host = _car.gameObject.AddComponent<EngineSimulationHost>();
            _host.Configure(profile);
            Orchestrator.Instance.Hosts.Add(_host);
        }
    }

}