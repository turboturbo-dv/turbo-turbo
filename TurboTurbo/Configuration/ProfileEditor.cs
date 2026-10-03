using System;
using System.Collections.Generic;

using TurboTurbo.Configuration.Sections;
using TurboTurbo.Modeling;
using TurboTurbo.Profiles;
using TurboTurbo.Profiles.Storage;
using TurboTurbo.Runtime;

using UnityEngine;

namespace TurboTurbo.Configuration;

internal sealed class ProfileEditor : MonoBehaviour
{
    private static readonly Logger Log = TurboTurbo.Log.ForContext("editor");
    private static readonly GUIContent[] ChargerOptions =
    {
        new GUIContent("Turbo",
            "Simulate a turbocharger on this locomotive.\n" +
            "Turbocharged engines produce a lot of soot while throttling up fast, as it takes a while for the turbo to " +
            "spin up.\n\n" +
            "Adjust the 'Turbocharger' section below to tune this behaviour."),
        new GUIContent("Atmospheric",
            "Simulate natural aspiration on this locomotive.\n" +
            "Naturally aspirated engines do not generally produce much soot, although at high RPM airflow may become " +
            "restricted, resulting in soot when running at maximum power.\n\n" +
            "Adjust the 'Atmospheric charger' section below to tune this behaviour."),
    };

    internal event Action Closed;

    private TrainCar _car;
    private EngineSimulationHost _host;
    private string _liveryId;
    private Rect _windowRect = new(460f, 20f, 450f, 170f);

    private EngineSimulationHost _boundHost;
    private readonly List<(string Key, IEditorPanel Panel)> _sections = new();
    private readonly Dictionary<string, bool> _openState = new();
    private ExhaustMarkerController _markerController;
    private ColorPickerWindow _colorPicker;
    private TurboTooltipLayer _tooltip;
    private bool _needsShrink;
    private bool _requiresReconfigure;
    private TweakGrade _grade = TweakGrade.Basic;

    public Rect WindowRect => _windowRect;

    public void Initialize(TrainCar car, EngineSimulationHost host, string liveryId)
    {
        _car = car;
        _host = host;
        _liveryId = liveryId;
        _markerController = new ExhaustMarkerController(() => _host);
        var pickerGo = new GameObject(Naming.Create("ColorPicker"));
        pickerGo.transform.SetParent(transform, worldPositionStays: false);
        _colorPicker = pickerGo.AddComponent<ColorPickerWindow>();
        _tooltip = gameObject.AddComponent<TurboTooltipLayer>();
    }

    private void CloseSelf() => Destroy(gameObject);

    private void MarkRequiresReconfigure() => _requiresReconfigure = true;

    private void Update()
    {
        _markerController?.Sync();

        if (!_requiresReconfigure) return;

        _requiresReconfigure = false;
        if (_host == null) return;

        foreach (var e in _host.Exhausts)
        {
            e.Smoke.Configure();
            e.Shimmer.Configure();
        }
    }

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

        _windowRect = GUILayout.Window(GetInstanceID(), _windowRect, DrawWindow, $"Profile: {_liveryId} ({_car.DisplayId()})", Styles.Window);
    }

    private void DrawWindow(int id)
    {
        TelemetryView.Draw(_host);
        DrawIntro();
        DrawGradeRow();
        Styles.Separator();
        DrawChargerRow();
        DrawSections();
        DrawFooter();

        // GUI.tooltip is only populated during repaint; capture then, so
        // other event passes don't overwrite it.
        if (Event.current.type == EventType.Repaint)
        {
            _tooltip.Tooltip = GUI.tooltip;
        }

        GUI.DragWindow();
    }

    private void DrawChargerRow()
    {
        GUILayout.Label("Select a charger model to use.", Styles.WrappedLabel);

        var selected = _host.Profile.ChargerKind == ChargerKind.Atmospheric ? 1 : 0;
        var next = GUILayout.Toolbar(selected, ChargerOptions);
        if (next != selected) SwitchCharger(next == 1 ? ChargerKind.Atmospheric : ChargerKind.Turbo);
    }

    private void DrawIntro()
    {
        GUILayout.Label($"Adjust the engine parameters below to change how the exhaust effect behaves.", Styles.WrappedLabel);
    }

    private void DrawGradeRow()
    {
        var advanced = GUILayout.Toggle(_grade == TweakGrade.Advanced,
            new GUIContent(" Advanced mode",
                "Show advanced tuning parameters.\n" +
                "You do not need to change any advanced parameters if you just want to make a profile work.\n\n" +
                "Adjusting these parameters can have unintended effects and may cause this profile to override " +
                "future model enhancements, so it is recommended to only change them when you cannot achieve the " +
                "desired effect any other way.")); var next = advanced ? TweakGrade.Advanced : TweakGrade.Basic;
        if (next == _grade) return;

        _grade = next;
        SnapshotOpenState();
        _markerController?.Clear();
        _colorPicker?.Close();
        _sections.Clear();
        _needsShrink = true;
    }

    private void DrawSections()
    {
        if (_host != _boundHost)
        {
            SnapshotOpenState();
            _colorPicker?.Close();
            _boundHost = _host;
            _sections.Clear();
        }

        if (_sections.Count == 0 && _host.Bound)
        {
            BuildSections();
            RestoreOpenState();
        }

        if (_sections.Count == 0)
        {
            GUILayout.Label("waiting for engine model…");
            return;
        }

        foreach (var (_, panel) in _sections)
        {
            panel.Draw();
        }
    }

    private void BuildSections()
    {
        AddPanel("exhausts", new ExhaustsPanel(
            () => _host,
            () => Rebind(_host.Profile),
            () => _needsShrink = true,
            _markerController.SetTarget));
        AddPanel("charger", ChargerSection.Build(_host, MarkRequiresReconfigure, () => _needsShrink = true));
        AddPanel("smoke-model", SmokeModelSection.Build(_host, MarkRequiresReconfigure, () => _needsShrink = true));
        AddPanel("colors", ColorsSection.Build(_host, () => _needsShrink = true, OpenColorPicker));
        AddPanel("velocity", VelocitySection.Build(_host, () => _needsShrink = true));
        AddPanel("smoke-emitter", SmokeEmitterSection.Build(_host, MarkRequiresReconfigure, () => _needsShrink = true));
        AddPanel("shimmer-emitter", ShimmerEmitterSection.Build(_host, MarkRequiresReconfigure, () => _needsShrink = true));
    }

    private void AddPanel(string key, IEditorPanel panel)
    {
        if (panel == null) return;
        if (panel is Section section)
        {
            section.MaxGrade = _grade;
            section.HeaderWidth = (_windowRect.width - GUI.skin.window.padding.horizontal) * 0.4f;
        }
        _sections.Add((key, panel));
    }

    private void SnapshotOpenState()
    {
        foreach (var (key, panel) in _sections) _openState[key] = panel.Open;
    }

    private void RestoreOpenState()
    {
        foreach (var (key, panel) in _sections)
        {
            if (_openState.TryGetValue(key, out var open)) panel.Open = open;
        }
    }

    private void OpenColorPicker(ColorSpec spec)
    {
        const float gap = 6f;
        _colorPicker.Open(spec, new Vector2(_windowRect.xMax + gap, spec.SwatchScreenRect.y));
    }

    private void DrawFooter()
    {
        GUILayout.BeginHorizontal();
        if (SettingsStore.Current.IsAuthoring)
        {
            if (GUILayout.Button($"Save to '{AuthoringService.TargetDisplayName}'")) SaveToAuthoringMod();
            if (GUILayout.Button(new GUIContent("copy XML",
                "Copy this profile to the clipboard as a <LocoProfile> XML fragment, ready to paste into a mod's " +
                "TurboConfig.xml.")))
            {
                CopyXml();
            }
        }
        else if (GUILayout.Button("Save"))
        {
            var error = ProfileService.User.Save(_host.Profile.Clone());
            if (error != null) Log.Warn($"could not save profile '{_liveryId}': {error}");
            else
            {
                Log.Info($"saved profile '{_liveryId}'");
                CloseSelf();
            }
        }

        if (GUILayout.Button("Discard"))
        {
            CloseSelf();
        }

        GUILayout.EndHorizontal();
    }

    private void SaveToAuthoringMod()
    {
        var profile = _host.Profile.Clone();
        var error = AuthoringService.Write(profile);
        if (error != null)
        {
            Log.Warn($"could not save to mod: {error}");
            CopyXml();
            return;
        }

        AuthoringService.ReloadTarget();
        Log.Info($"saved profile '{_liveryId}' to '{AuthoringService.TargetDisplayName}'");
        CloseSelf();
    }

    private void CopyXml()
    {
        GUIUtility.systemCopyBuffer = ModProfileWriter.SerializeFragment(_host.Profile.Clone());
        Log.Info("copied profile XML to the clipboard");
    }

    private void SwitchCharger(ChargerKind kind)
    {
        var profile = _host.Profile.Clone();
        profile.ChargerKind = kind;

        var error = profile.Normalize();
        if (error != null)
        {
            Log.Warn($"cannot switch charger for '{_liveryId}': {error}");
            return;
        }

        Rebind(profile);
    }

    private void Rebind(LocoProfile profile)
    {
        _markerController?.Clear();
        _colorPicker?.Close();

        _host = HostFactory.Replace(_host, _car, profile);
    }

    private void OnDestroy()
    {
        Closed?.Invoke();

        _markerController?.Clear();
        _colorPicker?.Close();

        var orchestrator = Orchestrator.Instance;
        if (_host != null)
        {
            Destroy(_host);
            _host = null;
        }

        if (orchestrator != null && orchestrator.Enabled && _car != null)
        {
            orchestrator.ReloadHost(_car);
        }
    }
}
