using System;
using System.Collections.Generic;

using TurboTurbo.Assets;
using TurboTurbo.Configuration;
using TurboTurbo.Configuration.Sections;
using TurboTurbo.Profiles;
using TurboTurbo.Runtime;

using UnityEngine;

namespace TurboTurbo.DevUI;

internal sealed class TurboDevPanel : MonoBehaviour
{
    private int _selected;
    private bool _requiresReconfigure;
    private readonly Logger _log = Log.ForContext("devpanel");

    private EngineSimulationHost _boundHost;
    private readonly List<Section> _sections = new();
    private bool _needsShrink;

    private string _diagLine = "orchestrator: ?\nspawner: ?";
    private float _diagTimer;

    private ColorPickerWindow _colorPicker;
    private ExhaustOffsetDebugView _debugView;
    private bool _showOffsetMarkers;

    public Rect WindowRect { get; private set; } = new(20f, 20f, 360f, 120f);

    public static TurboDevPanel Create(Rect initialRect)
    {
        var go = new GameObject("TurboTurbo.DevPanel");
        DontDestroyOnLoad(go);
        go.AddComponent<TurboTooltipLayer>();
        var panel = go.AddComponent<TurboDevPanel>();
        panel._colorPicker = go.AddComponent<ColorPickerWindow>();
        panel._debugView = go.AddComponent<ExhaustOffsetDebugView>();
        panel.WindowRect = initialRect;
        return panel;
    }

    private void OnEnable()
    {
        PickDefaultTarget();
    }

    private void Update()
    {
        RefreshDiagnostics();

        if (_requiresReconfigure)
        {
            _requiresReconfigure = false;
            if (_boundHost != null)
            {
                foreach (var e in _boundHost.Exhausts)
                {
                    e.Smoke.Configure();
                    e.Shimmer.Configure();
                }
            }
        }
    }

    private void OnGUI()
    {
        if (_needsShrink)
        {
            // not correct, but next draw will resize the window to fit the content
            var shrinkRect = WindowRect;
            shrinkRect.height = 10f;
            WindowRect = shrinkRect;
            _needsShrink = false;
        }

        WindowRect = GUILayout.Window(GetInstanceID(), WindowRect, DrawWindow, "TurboTurbo Dev UI");
    }

    private void DrawWindow(int id)
    {
        DrawTargetSelector();
        GUILayout.Space(6f);
        DrawDiagnostics();
        GUILayout.Space(6f);
        DrawTelemetry();
        GUILayout.Space(6f);
        DrawSections();
        GUILayout.Space(4f);
        DrawDumpButtons();
        GUILayout.Space(2f);
        DrawDiagnosticsButtons();

        // GUI.tooltip is only populated during repaint; capture then, so
        // other event passes don't overwrite it.
        if (Event.current.type == EventType.Repaint)
        {
            TurboTooltipLayer.Tooltip = GUI.tooltip;
        }

        GUI.DragWindow();
    }

    private void MarkRequiresReconfigure() => _requiresReconfigure = true;

    private void RefreshDiagnostics()
    {
        _diagTimer -= Time.deltaTime;
        if (_diagTimer > 0f) return;
        _diagTimer = 1f;

        if (ReferenceEquals(Orchestrator.Instance, null))
        {
            _diagLine = "orchestrator: no instance\nspawner: ?";
            return;
        }

        var self = Orchestrator.Instance == null
            ? "destroyed"
            : $"alive (id {Orchestrator.Instance.GetInstanceID()})";
        var assets = ModAssets.ShadersValid && GameAssets.SmokeAtlas != null ? "ok" : "lost";
        _diagLine = $"orchestrator: {self}; assets: {assets}\n{Orchestrator.Instance.DescribeDiagnostics()}";
    }

    private void DrawDiagnosticsButtons()
    {
        if (GUILayout.Button("inspect car (dump particle systems to log)"))
        {
            DumpParticleSystems();
        }
        if (GUILayout.Button("dump player train to log"))
        {
            PlayerTrainInspector.DumpTarget();
        }
        if (GUILayout.Button("save profile to settings"))
        {
            SaveProfile();
        }
    }

    private void SaveProfile()
    {
        var host = CurrentHost();
        if (host == null)
        {
            _log.Warn("no car targeted for inspection");
            return;
        }

        var profile = host.CloneProfile();

        var error = ProfileRepository.SaveProfile(profile);
        if (error != null)
        {
            _log.Warn($"could not save profile '{profile.LiveryId}': {error}");
            return;
        }

        _log.Info($"saved profile '{profile.LiveryId}' to settings");
    }

    private void DumpParticleSystems()
    {
        var host = CurrentHost();
        if (host == null)
        {
            _log.Warn("no car targeted for inspection");
            return;
        }

        // the host lives on the car root, so this always finds the TrainCar
        var car = host.GetComponent<TrainCar>();
        ParticleSystemInspector.Dump(car);
    }

    private void DrawSections()
    {
        var host = CurrentHost();
        if (host != _boundHost)
        {
            _boundHost = host;
            BuildSections(host);
            _debugView.SetHost(host);
        }

        if (_sections.Count == 0)
        {
            GUILayout.Label("no target");
            return;
        }

        foreach (var section in _sections)
        {
            section.Draw();
        }
    }

    private EngineSimulationHost CurrentHost()
    {
        var hosts = Orchestrator.Instance.Hosts;
        return hosts.Count == 0 ? null : hosts[_selected];
    }

    private void BuildSections(EngineSimulationHost host)
    {
        _sections.Clear();
        _colorPicker.Close();
        if (host == null) return;

        _sections.Add(ChargerSection.Build(host, MarkRequiresReconfigure, () => _needsShrink = true));
        AddSection(SmokeModelSection.Build(host, MarkRequiresReconfigure, () => _needsShrink = true));
        _sections.Add(VelocitySection.Build(host, () => _needsShrink = true));
        AddSection(ColorsSection.Build(host, () => _needsShrink = true,
            spec => spec.RequestEdit += () => _colorPicker.Open(spec)));
        AddSection(SmokeEmitterSection.Build(host, MarkRequiresReconfigure, () => _needsShrink = true));
        AddSection(ShimmerEmitterSection.Build(host, MarkRequiresReconfigure, () => _needsShrink = true));
        AddSection(PlacementSection.Build(host, () => _needsShrink = true,
            () => _showOffsetMarkers, v => { _showOffsetMarkers = v; _debugView.SetVisible(v); }));
    }

    private void AddSection(Section section)
    {
        if (section != null) _sections.Add(section);
    }

    private void DrawDumpButtons()
    {
        var total = 0;
        foreach (var section in _sections)
        {
            total += section.ChangedCount;
        }

        GUILayout.BeginHorizontal();
        GUI.enabled = total > 0;
        if (GUILayout.Button($"copy changes ({total})"))
        {
            GUIUtility.systemCopyBuffer = BuildYaml();
        }
        GUI.enabled = true;
        GUILayout.EndHorizontal();
    }

    private string BuildYaml()
    {
        // poor man's YAML. only really intended to give you a simple, clipboardable representation of changed settings
        var sb = new System.Text.StringBuilder();
        foreach (var section in _sections)
        {
            section.ExportYaml(sb);
        }
        return sb.ToString();
    }

    private void PickDefaultTarget()
    {
        var hosts = Orchestrator.Instance.Hosts;
        _selected = 0;
        for (var i = 0; i < hosts.Count; i++)
        {
            if (hosts[i].TrainCar == PlayerManager.Car)
            {
                _selected = i;
                break;
            }
        }
    }

    private void DrawTargetSelector()
    {
        var hosts = Orchestrator.Instance.Hosts;
        if (hosts.Count == 0)
        {
            GUILayout.Label("no loco tracked");
            return;
        }

        if (_selected >= hosts.Count) _selected = hosts.Count - 1;

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("<", GUILayout.Width(26f)))
        {
            _selected = (_selected + hosts.Count - 1) % hosts.Count;
        }
        GUILayout.Label($"{_selected + 1}/{hosts.Count}   {hosts[_selected].CarId}");
        if (GUILayout.Button(">", GUILayout.Width(26f)))
        {
            _selected = (_selected + 1) % hosts.Count;
        }
        GUILayout.EndHorizontal();
    }

    private void DrawDiagnostics()
    {
        GUILayout.BeginVertical(GUI.skin.box);
        GUILayout.Label(_diagLine);
        GUILayout.EndVertical();
    }

    private void DrawTelemetry()
    {
        var hosts = Orchestrator.Instance.Hosts;
        if (hosts.Count == 0) return;

        var host = hosts[_selected];

        GUILayout.BeginVertical(GUI.skin.box);

        if (!host.Bound)
        {
            GUILayout.Label($"{host.CarId}: sim not bound yet");
            GUILayout.EndVertical();
            return;
        }

        var m = host.CombustionModel;
        GUILayout.Label($"{host.CarId}   engineOn: {host.EngineOn}");
        GUILayout.Label($"throttle {m.Demand:0.000}   fuel/t {m.FuelNorm:0.000}   fuel/s {m.FuelPerStroke:0.000}");
        GUILayout.Label($"charge {m.Charge:0.000}   effDemand {m.EffectiveDemand:0.000}");
        GUILayout.Label($"lambda {m.Lambda:0.000}   rpm {m.RpmNorm:0.000}");
        GUILayout.Label($"exhaustHeat {m.ExhaustHeat:0.000}   boost {m.Boost:0.000}");
        GUILayout.Label($"absSpeed {host.AbsSpeed:0.0} m/s ({host.AbsSpeed * 3.6f:0.0} km/h)");

        for (var i = 0; i < host.Exhausts.Count; i++)
        {
            var e = host.Exhausts[i];
            GUILayout.Label($"exhaust {i}: smoke {e.Smoke.ParticleCount} p, " +
                            $"shimmer {e.Shimmer.ParticleCount} p, " +
                            $"wetStack {e.Smoke.Model.WetStackAccumulator:0.00}");
        }

        GUILayout.EndVertical();
    }
}