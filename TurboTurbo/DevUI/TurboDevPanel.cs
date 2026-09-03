using System;
using System.Collections.Generic;
using System.Linq;
using TurboTurbo.Modeling;
using TurboTurbo.Runtime;
using TurboTurbo.WorkBench;
using UnityEngine;

namespace TurboTurbo.DevUI;

internal sealed class TurboDevPanel : MonoBehaviour
{
    private const KeyCode ToggleKey = KeyCode.F6;
    internal const float LabelWidth = 165f;

    private Rect _rect = new(20f, 20f, 360f, 120f);
    private bool _visible;
    private int _selected;
    private bool _structuralDirty;

    private EngineSimulationHost _boundHost;
    private readonly List<Section> _sections = new();
    private bool _needsShrink;

    private string _diagLine = "orchestrator: ?\nspawner: ?";
    private float _diagTimer;

    public static TurboDevPanel Create()
    {
        var go = new GameObject("TurboTurbo.DevPanel");
        DontDestroyOnLoad(go);
        go.AddComponent<TurboTooltipLayer>();
        return go.AddComponent<TurboDevPanel>();
    }

    private void Update()
    {
        if (Input.GetKeyDown(ToggleKey))
        {
            _visible = !_visible;
            if (_visible) PickDefaultTarget();
        }

        if (_visible)
        {
            RefreshDiagnostics();
        }

        if (_structuralDirty)
        {
            _structuralDirty = false;
            if (_boundHost != null)
            {
                foreach (EngineSimulationHost.ExhaustEmitters e in _boundHost.Exhausts)
                {
                    e.Smoke.Configure();
                    e.Shimmer.Configure();
                }
            }
        }
    }

    private void OnGUI()
    {
        if (!_visible)
        {
            TurboTooltipLayer.Tooltip = "";
            return;
        }

        if (_needsShrink)
        {
            // not correct, but next draw will resize the window to fit the content
            _rect.height = 10f;
            _needsShrink = false;
        }

        _rect = GUILayout.Window(GetInstanceID(), _rect, DrawWindow, "TurboTurbo Dev UI");
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

    private void MarkStructuralDirty() => _structuralDirty = true;

    private sealed class Section
    {
        public readonly string Title;
        public readonly string YamlKey;
        public readonly List<ITweakSpec> Specs = new();
        public bool Open;
        public Action OnToggle;

        public Section(string title, string yamlKey)
        {
            Title = title;
            YamlKey = yamlKey;
        }

        public int ChangedCount
        {
            get
            {
                int n = 0;
                foreach (ITweakSpec spec in Specs)
                {
                    if (spec.Changed) n++;
                }
                return n;
            }
        }

        public void Draw()
        {
            int changed = ChangedCount;

            GUILayout.BeginHorizontal();
            bool prevOpen = Open;
            Open = GUILayout.Toggle(Open, (Open ? "▾ " : "▸ ") + Title);
            if (Open != prevOpen && OnToggle != null) OnToggle();
            GUILayout.Label(changed > 0 ? $"{changed} changed" : "", GUILayout.Width(70f));
            if (changed > 0 && GUILayout.Button("reset", GUILayout.Width(46f)))
            {
                foreach (ITweakSpec spec in Specs) spec.Reset();
            }
            GUILayout.EndHorizontal();

            if (Open)
            {
                foreach (ITweakSpec spec in Specs) spec.Draw();
            }
        }
    }

    private void AddFloat(Section section, string key, string tooltip,
        float min, float max, bool structural, Func<float> get, Action<float> set)
    {
        section.Specs.Add(new FloatSpec(key, tooltip, get, set, min, max,
            structural ? (Action)MarkStructuralDirty : null));
    }

    private void AddBool(Section section, string key, string tooltip,
        bool structural, Func<bool> get, Action<bool> set)
    {
        section.Specs.Add(new BoolSpec(key, tooltip, get, set,
            structural ? (Action)MarkStructuralDirty : null));
    }

    private void AddInt(Section section, string key, string tooltip,
        int min, int max, bool structural, Func<int> get, Action<int> set)
    {
        section.Specs.Add(new IntSpec(key, tooltip, get, set, min, max,
            structural ? (Action)MarkStructuralDirty : null));
    }

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

        string self = Orchestrator.Instance == null
            ? "destroyed"
            : $"alive (id {Orchestrator.Instance.GetInstanceID()})";
        string shaders = ModAssets.ShadersValid ? "ok" : "lost";
        _diagLine = $"orchestrator: {self}; shaders: {shaders}\n{Orchestrator.Instance.DescribeDiagnostics()}";
    }

    private void DrawDiagnosticsButtons()
    {
        if (GUILayout.Button("inspect car (dump particle systems to log)"))
        {
            DumpParticleSystems();
        }
    }

    private void DumpParticleSystems()
    {
        EngineSimulationHost host = CurrentHost();
        if (host == null)
        {
            Main.Log.LogInfo("[ps-dump] no car targeted");
            return;
        }

        // the host lives on the car root, so this always finds the TrainCar
        TrainCar car = host.GetComponent<TrainCar>();

        // roots only: Describe() already recurses into child systems
        var roots = car.GetComponentsInChildren<ParticleSystem>(true)
            .Where(ps => ps.transform.parent == null || ps.transform.parent.GetComponent<ParticleSystem>() == null)
            .ToList();

        Main.Log.LogInfo($"[ps-dump] === car '{car.ID}' ({car.carType}): {roots.Count} particle system root(s) ===");
        foreach (ParticleSystem root in roots)
        {
            Main.Log.LogInfo(ParticleSystemInspector.Describe(root));
        }
    }

    private void DrawSections()
    {
        EngineSimulationHost host = CurrentHost();
        if (host != _boundHost)
        {
            _boundHost = host;
            BuildSections(host);
        }

        if (_sections.Count == 0)
        {
            GUILayout.Label("no target");
            return;
        }

        foreach (Section section in _sections)
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
        if (host == null) return;

        BuildTurboSection(host);
        BuildSmokeModelSections(host);
        BuildEmitterSections(host);
    }

    private void BuildTurboSection(EngineSimulationHost host)
    {
        var section = new Section("turbo model", "turbo") { Open = true, OnToggle = () => _needsShrink = true };
        TurboModel.Settings s = host.TurboModel.Tuning;
        AddFloat(section, "airNAFraction",
            "Per-stroke charge index of naturally-aspirated operation (zero boost).",
            0f, 1f, false, () => s.AirNAFraction, v => s.AirNAFraction = v);
        AddFloat(section, "lambdaCalibration",
            "Air-to-fuel calibration constant: full boost + full rack is exactly clean at 2.5.",
            1f, 4f, false, () => s.LambdaCalibration, v => s.LambdaCalibration = v);
        AddFloat(section, "boostChargeMultiplier",
            "Boost multiplier on top of NA charge at full boost (charge = NA + (1-NA) x (1 + k x boost)).",
            0f, 5f, false, () => s.BoostChargeMultiplier, v => s.BoostChargeMultiplier = v);
        AddFloat(section, "rpmTorqueExponent",
            "0 = torque cap is pure per-stroke charge, 1 = strict airflow on top.",
            0f, 3f, false, () => s.RpmTorqueExponent, v => s.RpmTorqueExponent = v);
        AddFloat(section, "rpmBoostExponent",
            "Exponent bounding the boost equilibrium: exhaust mass flow scales with engine speed.",
            0f, 3f, false, () => s.RpmBoostExponent, v => s.RpmBoostExponent = v);
        AddFloat(section, "tauUp",
            "Spool-up time constant in seconds (clean combustion).",
            0.25f, 8f, false, () => s.TauUp, v => s.TauUp = v);
        AddFloat(section, "tauDown",
            "Blow-down (boost release) time constant in seconds.",
            0.1f, 4f, false, () => s.TauDown, v => s.TauDown = v);
        AddFloat(section, "minSpoolTau",
            "Floor for the spool-up time constant (stability under heavy overfuel).",
            0.1f, 2f, false, () => s.MinSpoolTau, v => s.MinSpoolTau = v);
        AddFloat(section, "thermalK",
            "Thermal enthalpy feedback strength: overfueling shortens spool-up time.",
            0f, 3f, false, () => s.ThermalK, v => s.ThermalK = v);
        AddFloat(section, "torqueLambdaFloor",
            "Lambda below which extra fuel contributes no torque.",
            0.3f, 1f, false, () => s.TorqueLambdaFloor, v => s.TorqueLambdaFloor = v);
        _sections.Add(section);
    }

    private void BuildSmokeModelSections(EngineSimulationHost host)
    {
        var models = new List<ExhaustSmokeModel>();
        foreach (EngineSimulationHost.ExhaustEmitters e in host.Exhausts)
        {
            models.Add(e.Smoke.Model);
        }

        if (models.Count > 0)
        {
            var section = new Section("smoke model (loco)", "smokeModel") { OnToggle = () => _needsShrink = true };
            ExhaustSmokeModel first = models[0];
            AddFloat(section, "sootOnsetLambda",
                "Lambda where soot starts forming.",
                0.3f, 1.5f, false,
                () => first.SootOnsetLambda, v => { foreach (ExhaustSmokeModel m in models) m.SootOnsetLambda = v; });
            AddFloat(section, "sootOpaqueLambda",
                "Lambda where soot reaches maximum opacity.",
                0.1f, 1f, false,
                () => first.SootOpaqueLambda, v => { foreach (ExhaustSmokeModel m in models) m.SootOpaqueLambda = v; });
            _sections.Add(section);
        }

        var global = new Section("smoke model (global)", "smokeModelGlobal") { OnToggle = () => _needsShrink = true };
        AddFloat(global, "wetStackIdleDemand",
            "Demand below which unburned fuel accumulates (wet stacking); also where the burn-off ramp begins.",
            0f, 0.5f, false, () => ExhaustSmokeModel.WetStackIdleDemand, v => ExhaustSmokeModel.WetStackIdleDemand = v);
        AddFloat(global, "wetStackFillRate",
            "Accumulator fill rate [1/s] while idling.",
            0f, 0.1f, false, () => ExhaustSmokeModel.WetStackFillRate, v => ExhaustSmokeModel.WetStackFillRate = v);
        AddFloat(global, "wetStackBurnThreshold",
            "The accumulator must exceed this before burn-off becomes visible.",
            0f, 0.5f, false, () => ExhaustSmokeModel.WetStackBurnThreshold, v => ExhaustSmokeModel.WetStackBurnThreshold = v);
        AddFloat(global, "wetStackBurnDemand",
            "Demand above which the wet-stack burn produces white smoke.",
            0f, 0.6f, false, () => ExhaustSmokeModel.WetStackBurnDemand, v => ExhaustSmokeModel.WetStackBurnDemand = v);
        AddFloat(global, "wetStackBurnRate",
            "Burn-off rate [1/s] per unit demand.",
            0f, 3f, false, () => ExhaustSmokeModel.WetStackBurnRate, v => ExhaustSmokeModel.WetStackBurnRate = v);
        AddFloat(global, "wetStackBurnRampDemand",
            "Demand at which the burn-off ramp reaches full strength (ramps up from wetStackIdleDemand).",
            0.2f, 1f, false, () => ExhaustSmokeModel.WetStackBurnRampDemand, v => ExhaustSmokeModel.WetStackBurnRampDemand = v);
        AddFloat(global, "oilBlowbyTintStrength",
            "Max blend toward the oil-burn color, reached at full rpm.",
            0f, 1f, false, () => ExhaustSmokeModel.OilBlowbyTintStrength, v => ExhaustSmokeModel.OilBlowbyTintStrength = v);
        AddFloat(global, "sootCurveExponent",
            "Gamma shaping the soot ladder over the lambda deficit.",
            0.5f, 3f, false, () => ExhaustSmokeModel.SootCurveExponent, v => ExhaustSmokeModel.SootCurveExponent = v);
        AddFloat(global, "alphaFloor",
            "Smoke opacity floor (clean haze).",
            0f, 0.5f, false, () => ExhaustSmokeModel.AlphaFloor, v => ExhaustSmokeModel.AlphaFloor = v);
        AddFloat(global, "alphaCeiling",
            "Smoke opacity ceiling (soot).",
            0.5f, 1f, false, () => ExhaustSmokeModel.AlphaCeiling, v => ExhaustSmokeModel.AlphaCeiling = v);
        AddFloat(global, "wetStackAlphaScale",
            "How strongly the wet-stack burn pushes opacity towards the ceiling.",
            0f, 1f, false, () => ExhaustSmokeModel.WetStackAlphaScale, v => ExhaustSmokeModel.WetStackAlphaScale = v);
        _sections.Add(global);
    }

    private void BuildEmitterSections(EngineSimulationHost host)
    {
        var smokes = new List<SmokeParticles>();
        var shimmers = new List<ShimmerParticles>();
        foreach (EngineSimulationHost.ExhaustEmitters e in host.Exhausts)
        {
            smokes.Add(e.Smoke);
            shimmers.Add(e.Shimmer);
        }

        if (smokes.Count > 0)
        {
            var section = new Section("smoke emitter", "smokeEmitter") { OnToggle = () => _needsShrink = true };
            SmokeParticles f = smokes[0];
            AddFloat(section, "lifetime", "Particle lifetime in seconds.",
                0.5f, 6f, false, () => f.lifetime, v => { foreach (SmokeParticles s in smokes) s.lifetime = v; });
            AddFloat(section, "startSizeMin", "Particle size range at emission [m].",
                0.1f, 3f, false, () => f.startSizeMin, v => { foreach (SmokeParticles s in smokes) s.startSizeMin = v; });
            AddFloat(section, "startSizeMax", "Particle size range at emission [m].",
                0.1f, 3f, false, () => f.startSizeMax, v => { foreach (SmokeParticles s in smokes) s.startSizeMax = v; });
            AddFloat(section, "sizeOverLifetimeStart", "Growth factor at emission.",
                0.1f, 3f, true, () => f.sizeOverLifetimeStart, v => { foreach (SmokeParticles s in smokes) s.sizeOverLifetimeStart = v; });
            AddFloat(section, "sizeOverLifetimeEnd", "Growth factor at end of lifetime.",
                1f, 10f, true, () => f.sizeOverLifetimeEnd, v => { foreach (SmokeParticles s in smokes) s.sizeOverLifetimeEnd = v; });
            AddFloat(section, "buoyancy", "Constant upward drift [m/s].",
                -1f, 2f, true, () => f.buoyancy, v => { foreach (SmokeParticles s in smokes) s.buoyancy = v; });
            AddFloat(section, "drag", "Air resistance decaying the inherited train velocity.",
                0f, 3f, true, () => f.drag, v => { foreach (SmokeParticles s in smokes) s.drag = v; });
            AddFloat(section, "angularVelocityMax", "Max random spin speed [deg/s], sign-randomized per particle.",
                0f, 90f, false, () => f.angularVelocityMax, v => { foreach (SmokeParticles s in smokes) s.angularVelocityMax = v; });
            AddFloat(section, "cleanRate", "Base emission rate [p/s] scaled by rpm.",
                0f, 60f, false, () => f.cleanRate, v => { foreach (SmokeParticles s in smokes) s.cleanRate = v; });
            AddFloat(section, "maxRate", "Extra emission rate [p/s] at full soot density.",
                0f, 150f, false, () => f.maxRate, v => { foreach (SmokeParticles s in smokes) s.maxRate = v; });
            _sections.Add(section);
        }

        if (shimmers.Count > 0)
        {
            var section = new Section("shimmer emitter", "shimmerEmitter") { OnToggle = () => _needsShrink = true };
            ShimmerParticles f = shimmers[0];
            AddFloat(section, "idleRate", "Emission rate [p/s] at zero heat.",
                0f, 20f, false, () => f.idleRate, v => { foreach (ShimmerParticles s in shimmers) s.idleRate = v; });
            AddFloat(section, "fullRate", "Emission rate [p/s] at full heat.",
                0f, 40f, false, () => f.fullRate, v => { foreach (ShimmerParticles s in shimmers) s.fullRate = v; });
            AddFloat(section, "lifetime", "Particle lifetime in seconds.",
                0.5f, 6f, true, () => f.lifetime, v => { foreach (ShimmerParticles s in shimmers) s.lifetime = v; });
            AddFloat(section, "startSizeMin", "Billboard size range at emission [m].",
                0.1f, 3f, false, () => f.startSizeMin, v => { foreach (ShimmerParticles s in shimmers) s.startSizeMin = v; });
            AddFloat(section, "startSizeMax", "Billboard size range at emission [m].",
                0.1f, 3f, false, () => f.startSizeMax, v => { foreach (ShimmerParticles s in shimmers) s.startSizeMax = v; });
            AddFloat(section, "sizeOverLifetimeStart", "Growth factor at emission.",
                0.1f, 3f, true, () => f.sizeOverLifetimeStart, v => { foreach (ShimmerParticles s in shimmers) s.sizeOverLifetimeStart = v; });
            AddFloat(section, "sizeOverLifetimeEnd", "Growth factor at end of lifetime.",
                1f, 10f, true, () => f.sizeOverLifetimeEnd, v => { foreach (ShimmerParticles s in shimmers) s.sizeOverLifetimeEnd = v; });
            AddFloat(section, "gravity", "Gravity modifier (negative = buoyant).",
                -1f, 0.5f, true, () => f.gravity, v => { foreach (ShimmerParticles s in shimmers) s.gravity = v; });
            AddFloat(section, "drag", "Air resistance decaying the inherited train velocity.",
                0f, 3f, true, () => f.drag, v => { foreach (ShimmerParticles s in shimmers) s.drag = v; });
            AddFloat(section, "buoyancy", "Constant upward drift [m/s].",
                0f, 2f, true, () => f.buoyancy, v => { foreach (ShimmerParticles s in shimmers) s.buoyancy = v; });
            AddFloat(section, "strength", "Max shimmer displacement at full heat.",
                0f, 0.05f, false, () => f.strength, v => { foreach (ShimmerParticles s in shimmers) s.strength = v; });
            AddFloat(section, "baseStrength", "Displacement multiplier at zero heat (lerps to 1 at full heat).",
                0f, 1f, false, () => f.baseStrength, v => { foreach (ShimmerParticles s in shimmers) s.baseStrength = v; });
            AddFloat(section, "freq", "Noise frequency of the shimmer field.",
                1f, 20f, false, () => f.freq, v => { foreach (ShimmerParticles s in shimmers) s.freq = v; });
            AddFloat(section, "idleRadius", "Displacement radius at zero heat.",
                0.2f, 2f, false, () => f.idleRadius, v => { foreach (ShimmerParticles s in shimmers) s.idleRadius = v; });
            AddFloat(section, "fullRadius", "Displacement radius at full heat.",
                0.2f, 2f, false, () => f.fullRadius, v => { foreach (ShimmerParticles s in shimmers) s.fullRadius = v; });
            AddFloat(section, "idleAnimSpeed", "Noise scroll speed at zero heat.",
                0f, 3f, false, () => f.idleAnimSpeed, v => { foreach (ShimmerParticles s in shimmers) s.idleAnimSpeed = v; });
            AddFloat(section, "fullAnimSpeed", "Noise scroll speed at full heat.",
                0f, 5f, false, () => f.fullAnimSpeed, v => { foreach (ShimmerParticles s in shimmers) s.fullAnimSpeed = v; });
            AddFloat(section, "speedMultiplier", "Multiplier on the noise scroll speed.",
                0f, 4f, false, () => f.speedMultiplier, v => { foreach (ShimmerParticles s in shimmers) s.speedMultiplier = v; });
            AddFloat(section, "shimmerHoldTime", "Fraction of the particle's lifetime held at full strength; decays linearly to zero at death.",
                0f, 1f, true, () => f.shimmerHoldTime, v => { foreach (ShimmerParticles s in shimmers) s.shimmerHoldTime = v; });
            AddBool(section, "outline", "Debug: outline the shimmer billboards.",
                false, () => f.outline, v => { foreach (ShimmerParticles s in shimmers) s.outline = v; });
            AddBool(section, "useShimmerShader", "Use the heat shimmer shader (off = opaque fallback material).",
                true, () => f.useShimmerShader, v => { foreach (ShimmerParticles s in shimmers) s.useShimmerShader = v; });
            AddInt(section, "debug", "Shader debug mode.",
                0, 5, false, () => f.debug, v => { foreach (ShimmerParticles s in shimmers) s.debug = v; });
            AddInt(section, "renderQueue", "Material render queue (3000 = smoke, 3010 = after the smoke).",
                2000, 4000, true, () => f.renderQueue, v => { foreach (ShimmerParticles s in shimmers) s.renderQueue = v; });
            _sections.Add(section);
        }
    }

    private void DrawDumpButtons()
    {
        int total = 0;
        foreach (Section section in _sections)
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
        foreach (Section section in _sections)
        {
            var lines = new List<string>();
            foreach (ITweakSpec spec in section.Specs)
            {
                string line = spec.Export();
                if (line != null) lines.Add(line);
            }
            if (lines.Count == 0) continue;

            sb.Append(section.YamlKey).AppendLine(":");
            foreach (string line in lines)
            {
                sb.AppendLine(line);
            }
        }
        return sb.ToString();
    }

    private void PickDefaultTarget()
    {
        var hosts = Orchestrator.Instance.Hosts;
        _selected = 0;
        for (int i = 0; i < hosts.Count; i++)
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

        EngineSimulationHost host = hosts[_selected];

        GUILayout.BeginVertical(GUI.skin.box);

        if (!host.Bound)
        {
            GUILayout.Label($"{host.CarId}: sim not bound yet");
            GUILayout.EndVertical();
            return;
        }

        TurboModel m = host.TurboModel;
        GUILayout.Label($"{host.CarId}   engineOn: {host.EngineOn}");
        GUILayout.Label($"boost {m.Boost:0.000}   charge {m.Charge:0.000}   effDemand {m.EffectiveDemand:0.000}");
        GUILayout.Label($"lambda {m.Lambda:0.000}   demand {m.Demand:0.000}   rpm {m.RpmNorm:0.000}");
        GUILayout.Label($"exhaustHeat {m.ExhaustHeat:0.000}   surge {m.SurgeThisTick}");

        for (int i = 0; i < host.Exhausts.Count; i++)
        {
            EngineSimulationHost.ExhaustEmitters e = host.Exhausts[i];
            GUILayout.Label($"exhaust {i}: smoke {e.Smoke.ParticleCount} p, " +
                            $"shimmer {e.Shimmer.ParticleCount} p, " +
                            $"wetStack {e.Smoke.WetStackAccumulator:0.00}");
        }

        GUILayout.EndVertical();
    }
}