using System;
using System.Collections.Generic;

using TurboTurbo.Configuration.Sections;
using TurboTurbo.Modeling;
using TurboTurbo.Profiles;
using TurboTurbo.Runtime;

using UnityEngine;

namespace TurboTurbo.Configuration;

internal sealed class ProfileEditor : MonoBehaviour
{
    private static readonly Logger Log = TurboTurbo.Log.ForContext("editor");
    private static readonly string[] ChargerOptions = { "Turbo", "Atmospheric" };

    internal event Action Closed;

    private TrainCar _car;
    private EngineSimulationHost _host;
    private string _liveryId;
    private Rect _windowRect = new(460f, 20f, 450f, 170f);

    private EngineSimulationHost _boundHost;
    private readonly List<IEditorPanel> _sections = new();
    private bool _needsShrink;
    private bool _requiresReconfigure;
    private TweakGrade _grade = TweakGrade.Basic;

    public Rect WindowRect => _windowRect;

    public void Initialize(TrainCar car, EngineSimulationHost host, string liveryId)
    {
        _car = car;
        _host = host;
        _liveryId = liveryId;
    }

    private void CloseSelf() => Destroy(gameObject);

    private void MarkRequiresReconfigure() => _requiresReconfigure = true;

    private void Update()
    {
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

        _windowRect = GUILayout.Window(GetInstanceID(), _windowRect, DrawWindow, $"Profile: {_liveryId} ({_car.ID})", Styles.Window);
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
            TurboTooltipLayer.Tooltip = GUI.tooltip;
        }

        GUI.DragWindow();
    }

    private void DrawChargerRow()
    {
        GUILayout.Label("Select a charger model to use. Note that the atmospheric model is also applicable to supercharged / roots-blown engines.", Styles.WrappedLabel);

        var selected = _host.Profile.ChargerKind == ChargerKind.Atmospheric ? 1 : 0;
        var next = GUILayout.Toolbar(selected, ChargerOptions);
        if (next != selected) SwitchCharger(next == 1 ? ChargerKind.Atmospheric : ChargerKind.Turbo);
    }

    private void DrawIntro()
    {
        GUILayout.Label("Adjust the engine parameters below. Defaults for a new profile are taken from the DE6 tuning.", Styles.WrappedLabel);
    }

    private void DrawGradeRow()
    {
        var advanced = GUILayout.Toggle(_grade == TweakGrade.Advanced, "advanced mode");
        var next = advanced ? TweakGrade.Advanced : TweakGrade.Basic;
        if (next == _grade) return;

        _grade = next;
        _sections.Clear();
        _needsShrink = true;
    }

    private void DrawSections()
    {
        if (_host != _boundHost)
        {
            _boundHost = _host;
            _sections.Clear();
        }

        if (_sections.Count == 0 && _host.Bound)
        {
            BuildSections();
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

    private void BuildSections()
    {
        var host = _host;
        AddSection(ChargerSection.Build(host, MarkRequiresReconfigure, () => _needsShrink = true));
        AddSection(SmokeModelSection.Build(host, MarkRequiresReconfigure, () => _needsShrink = true));
        AddSection(VelocitySection.Build(host, () => _needsShrink = true));
        AddSection(SmokeEmitterSection.Build(host, MarkRequiresReconfigure, () => _needsShrink = true));
        AddSection(ShimmerEmitterSection.Build(host, MarkRequiresReconfigure, () => _needsShrink = true));
    }

    private void AddSection(IEditorPanel panel)
    {
        if (panel == null) return;
        if (panel is Section section)
        {
            section.MaxGrade = _grade;
            section.HeaderWidth = (_windowRect.width - GUI.skin.window.padding.horizontal) * 0.4f;
        }
        _sections.Add(panel);
    }

    private void DrawFooter()
    {
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Save"))
        {
            var error = ProfileRepository.SaveProfile(_host.Profile.Clone());
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

    private void SwitchCharger(ChargerKind kind)
    {
        var profile = _host.Profile.Clone();
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

        _host = _car.gameObject.AddComponent<EngineSimulationHost>();
        _host.Configure(profile);
        Orchestrator.Instance.Hosts.Add(_host);
    }

    private void OnDestroy()
    {
        Closed?.Invoke();

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