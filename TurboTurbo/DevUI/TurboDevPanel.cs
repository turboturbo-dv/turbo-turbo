using TurboTurbo.Assets;
using TurboTurbo.Configuration;
using TurboTurbo.Runtime;
using TurboTurbo.WorkBench;

using UnityEngine;

namespace TurboTurbo.DevUI;

internal sealed class TurboDevPanel : MonoBehaviour
{
    private int _selected;
    private readonly Logger _log = Log.ForContext("devpanel");

    private string _diagLine = "orchestrator: ?\nspawner: ?";
    private float _diagTimer;

    private float _densityFalloff = SmokeParticles.DensityFalloff;
    private float _softParticlesFade = SmokeParticles.SoftParticlesFade;
    private float _fadeInSeconds = SmokeParticles.FadeInSeconds;

    private TurboTooltipLayer _tooltip;

    public Rect WindowRect { get; private set; } = new(20f, 20f, 540f, 180f);

    public static TurboDevPanel Create(Rect initialRect)
    {
        var go = new GameObject(Naming.Create("DevPanel"));
        DontDestroyOnLoad(go);
        var panel = go.AddComponent<TurboDevPanel>();
        panel._tooltip = go.AddComponent<TurboTooltipLayer>();
        panel.WindowRect = initialRect;
        go.AddComponent<WindowBlocker>().Track(() => panel.WindowRect);
        return panel;
    }

    private void OnEnable()
    {
        PickDefaultTarget();
    }

    private void Update()
    {
        RefreshDiagnostics();
    }

    private void OnGUI()
    {
        WindowRect = GUILayout.Window(GetInstanceID(), WindowRect, DrawWindow, "TurboTurbo Dev UI");
    }

    private void DrawWindow(int id)
    {
        DrawTargetSelector();
        GUILayout.Space(6f);
        DrawDiagnostics();
        GUILayout.Space(6f);
        DrawTelemetry();
        GUILayout.Space(4f);
        DrawDensityFalloff();
        GUILayout.Space(4f);
        DrawSoftParticlesFade();
        GUILayout.Space(4f);
        DrawFadeInSeconds();
        GUILayout.Space(4f);
        DrawDiagnosticsButtons();

        // GUI.tooltip is only populated during repaint; capture then, so
        // other event passes don't overwrite it.
        if (Event.current.type == EventType.Repaint)
        {
            _tooltip.Tooltip = GUI.tooltip;
        }

        GUI.DragWindow();
    }

    private void RefreshDiagnostics()
    {
        _diagTimer -= Time.deltaTime;
        if (_diagTimer > 0f) return;
        _diagTimer = 1f;

        var orchestrator = Orchestrator.Instance;
        if (orchestrator == null)
        {
            _diagLine = "orchestrator: no instance\nspawner: ?";
            return;
        }

        var assets = ModAssets.ShadersValid && GameAssets.SmokeAtlas != null ? "ok" : "lost";
        _diagLine = $"orchestrator: alive (id {orchestrator.GetInstanceID()}); assets: {assets}\n" +
                    orchestrator.DescribeDiagnostics();
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

    private EngineSimulationHost CurrentHost()
    {
        var hosts = Orchestrator.Instance.Hosts;
        return hosts.Count == 0 ? null : hosts[_selected];
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
        GUILayout.Label($"governor {m.GovernorNorm:0.000}   fuel/time {m.FuelNorm:0.000}   fuel/stroke {m.FuelPerStroke:0.000}");
        GUILayout.Label($"charge {m.Charge:0.000}   boost {m.Boost:0.000}");
        GUILayout.Label($"lambda {m.Lambda:0.000}   rpm {m.RpmNorm:0.000}");
        GUILayout.Label($"exhaustEnergy {m.ExhaustEnergy:0.000}   massFlow {m.MassFlow:0.000}");
        GUILayout.Label($"EGT (manif) {m.GasTemperature + m.TurbineTemperatureDropK - PhysicsConstants.KelvinOffset:0.0} C");
        GUILayout.Label($"turbineDrop {m.TurbineTemperatureDropK:0.0} C");
        GUILayout.Label($"EGT (mouth) {m.GasTemperature - PhysicsConstants.KelvinOffset:0.0} C   gasDensity {m.GasDensity:0.000}");
        GUILayout.Label($"burn air {m.BurnFractionAir:0.000}   burn temp {m.BurnFractionTemp:0.000}   burn {m.BurnFraction:0.000}");
        GUILayout.Label($"cylTemp {m.CylinderTempK - PhysicsConstants.KelvinOffset:0.0} C   blockTemp {m.EngineTempK - PhysicsConstants.KelvinOffset:0.0} C   thermostat {m.ThermostatOpen:0.00}");
        GUILayout.Label($"exhaustVelocity {m.ExhaustVelocity:0.00} m/s");
        GUILayout.Label($"absSpeed {host.AbsSpeed:0.0} m/s ({host.AbsSpeed * PhysicsConstants.MpsToKmh:0.0} km/h)");

        var stack = host.Stack;
        GUILayout.Label($"stackWall {stack.ExhaustWallTempK - PhysicsConstants.KelvinOffset:0.0} C   " +
                        $"wetStack {stack.WetStack:0.00}   vapour {stack.Vapour:0.00}");
        GUILayout.Label($"deposit {stack.DepositRate:0.000}/s   slip {stack.SlipRate:0.000}/s   " +
                        $"evap {stack.EvaporateRate:0.000}/s   overflow {stack.Overflow:0.000}/s");

        for (var i = 0; i < host.Exhausts.Count; i++)
        {
            var e = host.Exhausts[i];
            GUILayout.Label($"exhaust {i}: smoke {e.Smoke.ParticleCount} p, " +
                            $"shimmer {e.Shimmer.ParticleCount} p");
        }

        GUILayout.EndVertical();
    }

    private void DrawDensityFalloff()
    {
        GUILayout.BeginVertical(GUI.skin.box);
        GUILayout.BeginHorizontal();
        GUILayout.Label(new GUIContent($"Density falloff: {_densityFalloff:0.00}",
                "Exponent of the size-based density falloff."),
            GUILayout.Width(170f));
        var next = GUILayout.HorizontalSlider(_densityFalloff, 0.5f, 4f);
        GUILayout.EndHorizontal();
        if (!Mathf.Approximately(next, _densityFalloff))
        {
            _densityFalloff = next;
            SmokeParticles.DensityFalloff = next;
            ApplyDensityFalloff();
        }
        GUILayout.EndVertical();
    }

    private void ApplyDensityFalloff()
    {
        var orchestrator = Orchestrator.Instance;
        if (orchestrator == null) return;

        foreach (var host in orchestrator.Hosts)
        {
            foreach (var e in host.Exhausts)
            {
                e.Smoke?.ApplyDensityFalloff();
            }
        }
    }

    private void DrawSoftParticlesFade()
    {
        GUILayout.BeginVertical(GUI.skin.box);
        GUILayout.BeginHorizontal();
        GUILayout.Label(new GUIContent($"Soft particles: {_softParticlesFade:0.00} m",
                "Distance over which smoke fades out as it approaches opaque geometry. 0 disables."),
            GUILayout.Width(170f));
        var next = GUILayout.HorizontalSlider(_softParticlesFade, 0f, 1f);
        GUILayout.EndHorizontal();
        if (!Mathf.Approximately(next, _softParticlesFade))
        {
            _softParticlesFade = next;
            SmokeParticles.SoftParticlesFade = next;
            ApplySoftParticlesFade();
        }
        GUILayout.EndVertical();
    }

    private void ApplySoftParticlesFade()
    {
        var orchestrator = Orchestrator.Instance;
        if (orchestrator == null) return;

        foreach (var host in orchestrator.Hosts)
        {
            foreach (var e in host.Exhausts)
            {
                e.Smoke?.ApplySoftParticlesFade();
            }
        }
    }

    private void DrawFadeInSeconds()
    {
        GUILayout.BeginVertical(GUI.skin.box);
        GUILayout.BeginHorizontal();
        GUILayout.Label(new GUIContent($"Fade-in: {_fadeInSeconds:0.00} s",
                "Absolute time for a newly spawned smoke particle to reach full opacity, independent of lifetime."),
            GUILayout.Width(170f));
        var next = GUILayout.HorizontalSlider(_fadeInSeconds, 0f, 0.25f);
        GUILayout.EndHorizontal();
        if (!Mathf.Approximately(next, _fadeInSeconds))
        {
            _fadeInSeconds = next;
            SmokeParticles.FadeInSeconds = next;
            ApplyFadeInSeconds();
        }
        GUILayout.EndVertical();
    }

    private void ApplyFadeInSeconds()
    {
        var orchestrator = Orchestrator.Instance;
        if (orchestrator == null) return;

        foreach (var host in orchestrator.Hosts)
        {
            foreach (var e in host.Exhausts)
            {
                e.Smoke?.ApplyFadeInSeconds();
            }
        }
    }
}
