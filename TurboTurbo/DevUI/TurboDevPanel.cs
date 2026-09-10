using System;
using System.Collections.Generic;

using TurboTurbo.Assets;
using TurboTurbo.Modeling;
using TurboTurbo.Runtime;
using TurboTurbo.WorkBench;

using UnityEngine;

namespace TurboTurbo.DevUI;

internal sealed class TurboDevPanel : MonoBehaviour
{
    public const float LabelWidth = 165f;

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

        BuildTurboSection(host);
        BuildSmokeModelSection(host);
        BuildVelocitySection(host);
        BuildColorsSection(host);
        BuildEmitterSections(host);
        BuildPlacementSection(host);
    }

    private void BuildVelocitySection(EngineSimulationHost host)
    {
        var section = new Section("exhaust velocity", "exhaustVelocity") { OnToggle = () => _needsShrink = true };
        section.AddFloat("idleVelocity", "Exhaust plume speed [m/s] at idle heat.",
            0f, 5f, false, () => host.Velocity.Idle, v => host.Velocity.Idle = v);
        section.AddFloat("fullLoadVelocity", "Exhaust plume speed [m/s] at full heat.",
            0f, 20f, false, () => host.Velocity.FullLoad, v => host.Velocity.FullLoad = v);
        _sections.Add(section);
    }

    private void BuildColorsSection(EngineSimulationHost host)
    {
        var models = new List<ExhaustSmokeModel>();
        foreach (var e in host.Exhausts)
        {
            models.Add(e.Smoke.Model);
        }

        if (models.Count == 0) return;

        var first = models[0];
        var section = new Section("smoke colors", "smokeColors") { OnToggle = () => _needsShrink = true };
        WireColor(section, "colorIdleHaze", "Haze tint at idle and low load.",
            () => first.Tuning.ColorIdleHaze, v => { foreach (var m in models) m.Tuning.ColorIdleHaze = v; });
        WireColor(section, "colorCleanBurn", "Clean burn tint.",
            () => first.Tuning.ColorCleanBurn, v => { foreach (var m in models) m.Tuning.ColorCleanBurn = v; });
        WireColor(section, "colorHeavySoot", "Soot tint.",
            () => first.Tuning.ColorHeavySoot, v => { foreach (var m in models) m.Tuning.ColorHeavySoot = v; });
        WireColor(section, "colorWetStack", "Wet stack burn tint.",
            () => first.Tuning.ColorWetStack, v => { foreach (var m in models) m.Tuning.ColorWetStack = v; });
        WireColor(section, "colorOilBurn", "Oil burn tint.",
            () => first.Tuning.ColorOilBurn, v => { foreach (var m in models) m.Tuning.ColorOilBurn = v; });
        _sections.Add(section);
    }

    private void WireColor(Section section, string key, string tooltip, Func<Color> get, Action<Color> set)
    {
        var spec = section.AddColor(key, tooltip, get, set);
        spec.RequestEdit += () => _colorPicker.Open(spec);
    }

    private void BuildTurboSection(EngineSimulationHost host)
    {
        var model = host.CombustionModel;
        var aspirated = model.Charger is AtmosphericCharger;
        var section = new Section(aspirated ? "aspirated engine" : "turbo model", aspirated ? "aspirated" : "turbo", MarkRequiresReconfigure) { Open = true, OnToggle = () => _needsShrink = true };
        var s = model.Tuning;
        section.AddFloat("rpmTorqueExponent",
            "Scales max torque capacity with engine speed. 0 = torque cap depends purely on cylinder charge density; 1 = torque cap scales linearly with RPM.",
            0f, 3f, false, () => s.RpmTorqueExponent, v => s.RpmTorqueExponent = v);
        section.AddFloat("torqueLambdaFloor",
            "Lambda below which extra fuel contributes no torque.",
            0.3f, 1f, false, () => s.TorqueLambdaFloor, v => s.TorqueLambdaFloor = v);
        if (model.Charger is TurboCharger turbo)
        {
            var t = turbo.Tuning;
            section.AddFloat("lambdaCalibration",
                "Global air-to-fuel scaling factor. Higher values lower lambda across all operating points, making the engine run richer.",
                1f, 4f, false, () => t.LambdaCalibration, v => t.LambdaCalibration = v);
            section.AddFloat("boostChargeMultiplier",
                "Charge gain per unit boost. Charge = 1 + BoostChargeMultiplier * Boost.",
                0f, 5f, false, () => t.BoostChargeMultiplier, v => t.BoostChargeMultiplier = v);
            section.AddFloat("rpmBoostExponent",
                "RPM penalty exponent on target boost equilibrium (Target = Demand * RPM^exponent). Higher values restrict turbo spooling at low engine RPM.",
                0f, 3f, false, () => t.RpmBoostExponent, v => t.RpmBoostExponent = v);
            section.AddFloat("tauUp",
                "Spool-up time constant in seconds.",
                0.25f, 8f, false, () => t.TauUp, v => t.TauUp = v);
            section.AddFloat("tauDown",
                "Blow-down time constant in seconds.",
                0.1f, 4f, false, () => t.TauDown, v => t.TauDown = v);
            section.AddFloat("minSpoolTau",
                "Floor for the spool-up time constant (stability under heavy overfuel).",
                0.1f, 2f, false, () => t.MinSpoolTau, v => t.MinSpoolTau = v);
            section.AddFloat("thermalK",
                "Thermal enthalpy feedback strength: overfueling shortens spool-up time.",
                0f, 3f, false, () => t.ThermalK, v => t.ThermalK = v);
            section.AddFloat("surgeRateThreshold",
                "Demand drop rate [1/s] that triggers a surge while boost is above 0.75.",
                0f, 60f, false, () => t.SurgeRateThreshold, v => t.SurgeRateThreshold = v);
        }
        else if (model.Charger is AtmosphericCharger na)
        {
            var a = na.Tuning;
            section.AddFloat("etaPeak",
                "Peak volumetric efficiency: charge density at zero RPM before choke losses.",
                0.5f, 1f, false, () => a.EtaPeak, v => { a.EtaPeak = v; a.Validate(); });
            section.AddFloat("chokeK",
                "High-RPM breathing loss factor. Higher values cause more choke loss overall.",
                0f, 1f, false, () => a.ChokeK, v => { a.ChokeK = v; a.Validate(); });
            section.AddFloat("chokeBeta",
                "High-RPM breathing loss exponent. Shapes the choke loss curve. Values above 1 shift choke loss to the top RPM range.",
                0f, 4f, false, () => a.ChokeBeta, v => { a.ChokeBeta = v; a.Validate(); });
            section.AddFloat("lambdaCalibration",
                "Global air-to-fuel scaling factor. Higher values lower lambda across all operating points, making the engine run richer.",
                0.2f, 1.5f, false, () => a.LambdaCalibration, v => a.LambdaCalibration = v);
        }
        _sections.Add(section);
    }

    private void BuildSmokeModelSection(EngineSimulationHost host)
    {
        var models = new List<ExhaustSmokeModel>();
        foreach (var e in host.Exhausts)
        {
            models.Add(e.Smoke.Model);
        }

        if (models.Count == 0) return;

        var section = new Section("smoke model", "smokeModel", MarkRequiresReconfigure) { OnToggle = () => _needsShrink = true };
        var first = models[0];

        void Add(string key, string tooltip, float min, float max,
            Func<ExhaustSmokeModel, float> get, Action<ExhaustSmokeModel, float> set)
        {
            section.AddFloat(key, tooltip, min, max, false,
                () => get(first), v =>
                {
                    foreach (var m in models)
                    {
                        set(m, v);
                        m.Tuning.Validate();
                    }
                });
        }

        Add("hazeAlpha", "Idle haze opacity.", 0f, 0.5f,
            m => m.Tuning.HazeAlpha, (m, v) => m.Tuning.HazeAlpha = v);
        Add("cleanBurnHeat", "Heat at which the idle haze is fully gone.", 0.05f, 1f,
            m => m.Tuning.CleanBurnHeat, (m, v) => m.Tuning.CleanBurnHeat = v);
        Add("cleanExhaustLambda", "Lambda at which the exhaust is fully clean.", 1f, 4f,
            m => m.Tuning.CleanExhaustLambda, (m, v) => m.Tuning.CleanExhaustLambda = v);
        Add("cleanExhaustAlpha", "Opacity of clean exhaust.", 0f, 0.2f,
            m => m.Tuning.CleanExhaustAlpha, (m, v) => m.Tuning.CleanExhaustAlpha = v);
        Add("sootOnsetLambda", "Lambda where soot starts forming.", 0.3f, 1.5f,
            m => m.Tuning.SootOnsetLambda, (m, v) => m.Tuning.SootOnsetLambda = v);
        Add("sootOpaqueLambda", "Lambda where soot reaches maximum opacity.", 0.1f, 1f,
            m => m.Tuning.SootOpaqueLambda, (m, v) => m.Tuning.SootOpaqueLambda = v);
        Add("sootCurveExponent", "Gamma shaping the soot ladder over the lambda deficit.", 0.5f, 3f,
            m => m.Tuning.SootCurveExponent, (m, v) => m.Tuning.SootCurveExponent = v);
        Add("sootMaxAlpha", "Opacity contribution of fully developed soot.", 0f, 1f,
            m => m.Tuning.SootMaxAlpha, (m, v) => m.Tuning.SootMaxAlpha = v);
        Add("wetStackFillHeat", "Heat below which wet stacking starts to occur.", 0f, 1f,
            m => m.Tuning.WetStackFillHeat, (m, v) => m.Tuning.WetStackFillHeat = v);
        Add("wetStackReleaseHeat", "Heat above which the wet stack starts to release.", 0f, 1f,
            m => m.Tuning.WetStackReleaseHeat, (m, v) => m.Tuning.WetStackReleaseHeat = v);
        Add("wetStackFillRate", "Accumulator fill rate [1/s] at zero heat.", 0f, 0.1f,
            m => m.Tuning.WetStackFillRate, (m, v) => m.Tuning.WetStackFillRate = v);
        Add("wetStackReleaseRate", "Release rate [1/s] at full heat.", 0f, 3f,
            m => m.Tuning.WetStackReleaseRate, (m, v) => m.Tuning.WetStackReleaseRate = v);
        Add("wetStackMistStrength", "How strongly the release rate converts into visible mist.", 0f, 5f,
            m => m.Tuning.WetStackMistStrength, (m, v) => m.Tuning.WetStackMistStrength = v);
        Add("wetStackMaxAlpha", "Opacity contribution of the wet-stack mist.", 0f, 1f,
            m => m.Tuning.WetStackMaxAlpha, (m, v) => m.Tuning.WetStackMaxAlpha = v);
        Add("oilTintStrength", "Max blend toward the oil-burn color, reached at high rpm.", 0f, 1f,
            m => m.Tuning.OilTintStrength, (m, v) => m.Tuning.OilTintStrength = v);
        Add("oilRpmExponent", "RPM exponent on the oil tint. Higher keeps oil coloration out of the low RPM range.", 0.1f, 5f,
            m => m.Tuning.OilRpmExponent, (m, v) => m.Tuning.OilRpmExponent = v);
        section.AddButton("fillWetStack", "Fill the wet-stack accumulator to 1.",
            () => { foreach (var m in models) m.FillWetStack(); });
        _sections.Add(section);
    }

    private void BuildEmitterSections(EngineSimulationHost host)
    {
        var smokes = new List<SmokeParticles>();
        var shimmers = new List<ShimmerParticles>();
        foreach (var e in host.Exhausts)
        {
            smokes.Add(e.Smoke);
            shimmers.Add(e.Shimmer);
        }

        if (smokes.Count > 0)
        {
            var section = new Section("smoke emitter", "smokeEmitter", MarkRequiresReconfigure) { OnToggle = () => _needsShrink = true };
            var f = smokes[0];
            section.AddFloat("lifetime", "Particle lifetime in seconds.",
                0.5f, 6f, false, () => f.tuning.lifetime, v => { foreach (var s in smokes) s.tuning.lifetime = v; });
            section.AddFloat("startSizeMin", "Particle size range at emission [m].",
                0.1f, 3f, false, () => f.tuning.startSizeMin, v => { foreach (var s in smokes) s.tuning.startSizeMin = v; });
            section.AddFloat("startSizeMax", "Particle size range at emission [m].",
                0.1f, 3f, false, () => f.tuning.startSizeMax, v => { foreach (var s in smokes) s.tuning.startSizeMax = v; });
            section.AddFloat("sizeOverLifetimeStart", "Growth factor at emission.",
                0.1f, 3f, true, () => f.tuning.sizeOverLifetimeStart, v => { foreach (var s in smokes) s.tuning.sizeOverLifetimeStart = v; });
            section.AddFloat("sizeOverLifetimeEnd", "Growth factor at end of lifetime.",
                1f, 10f, true, () => f.tuning.sizeOverLifetimeEnd, v => { foreach (var s in smokes) s.tuning.sizeOverLifetimeEnd = v; });
            section.AddFloat("buoyancy", "Constant upward drift [m/s].",
                -1f, 2f, true, () => f.tuning.buoyancy, v => { foreach (var s in smokes) s.tuning.buoyancy = v; });
            section.AddFloat("drag", "Air resistance decaying the inherited train velocity.",
                0f, 3f, true, () => f.tuning.drag, v => { foreach (var s in smokes) s.tuning.drag = v; });
            section.AddFloat("angularVelocityMax", "Max random spin speed [deg/s], sign-randomized per particle.",
                0f, 90f, false, () => f.tuning.angularVelocityMax, v => { foreach (var s in smokes) s.tuning.angularVelocityMax = v; });
            section.AddFloat("idleEmissionRate", "Emission rate [p/s] at idle heat.",
                0f, 60f, false, () => f.tuning.idleEmissionRate, v => { foreach (var s in smokes) s.tuning.idleEmissionRate = v; });
            section.AddFloat("fullEmissionRate", "Emission rate [p/s] at full heat.",
                0f, 150f, false, () => f.tuning.fullEmissionRate, v => { foreach (var s in smokes) s.tuning.fullEmissionRate = v; });
            section.AddFloat("speedNormMax", "Speed [m/s] at which speed-based dispersion reaches full strength.",
                1f, 30f, false, () => f.tuning.speedNormMax, v => { foreach (var s in smokes) s.tuning.speedNormMax = v; });
            section.AddFloat("speedLifetimeScale", "Particle lifetime multiplier at full dispersion.",
                0f, 1f, false, () => f.tuning.speedLifetimeScale, v => { foreach (var s in smokes) s.tuning.speedLifetimeScale = v; });
            section.AddFloat("speedJitter", "Extra emission jitter [m/s] at full dispersion.",
                0f, 2f, false, () => f.tuning.speedJitter, v => { foreach (var s in smokes) s.tuning.speedJitter = v; });
            section.AddFloat("turbulenceStrength", "Turbulence noise field strength at full dispersion speed (zero at standstill).",
                0f, 3f, false, () => f.tuning.turbulenceStrength, v => { foreach (var s in smokes) s.tuning.turbulenceStrength = v; });
            section.AddFloat("turbulenceFrequency", "Turbulence noise field frequency (lower = larger cells).",
                0.05f, 2f, true, () => f.tuning.turbulenceFrequency, v => { foreach (var s in smokes) s.tuning.turbulenceFrequency = v; });
            section.AddFloat("turbulenceScrollSpeed", "Turbulence noise field scroll speed.",
                0f, 3f, true, () => f.tuning.turbulenceScrollSpeed, v => { foreach (var s in smokes) s.tuning.turbulenceScrollSpeed = v; });
            _sections.Add(section);
        }

        if (shimmers.Count > 0)
        {
            var section = new Section("shimmer emitter", "shimmerEmitter", MarkRequiresReconfigure) { OnToggle = () => _needsShrink = true };
            var f = shimmers[0];
            section.AddFloat("idleRate", "Emission rate [p/s] at zero heat.",
                0f, 20f, false, () => f.tuning.idleRate, v => { foreach (var s in shimmers) s.tuning.idleRate = v; });
            section.AddFloat("fullRate", "Emission rate [p/s] at full heat.",
                0f, 40f, false, () => f.tuning.fullRate, v => { foreach (var s in shimmers) s.tuning.fullRate = v; });
            section.AddFloat("yOffset", "Vertical offset [m] of the emission point relative to the exhaust.",
                -2f, 2f, false, () => f.tuning.yOffset, v => { foreach (var s in shimmers) s.tuning.yOffset = v; });
            section.AddFloat("lifetime", "Particle lifetime in seconds.",
                0.5f, 6f, true, () => f.tuning.lifetime, v => { foreach (var s in shimmers) s.tuning.lifetime = v; });
            section.AddFloat("startSizeMin", "Billboard size range at emission [m].",
                0.1f, 3f, false, () => f.tuning.startSizeMin, v => { foreach (var s in shimmers) s.tuning.startSizeMin = v; });
            section.AddFloat("startSizeMax", "Billboard size range at emission [m].",
                0.1f, 3f, false, () => f.tuning.startSizeMax, v => { foreach (var s in shimmers) s.tuning.startSizeMax = v; });
            section.AddFloat("sizeOverLifetimeStart", "Growth factor at emission.",
                0.1f, 3f, true, () => f.tuning.sizeOverLifetimeStart, v => { foreach (var s in shimmers) s.tuning.sizeOverLifetimeStart = v; });
            section.AddFloat("sizeOverLifetimeEnd", "Growth factor at end of lifetime.",
                1f, 10f, true, () => f.tuning.sizeOverLifetimeEnd, v => { foreach (var s in shimmers) s.tuning.sizeOverLifetimeEnd = v; });
            section.AddFloat("gravity", "Gravity modifier (negative = buoyant).",
                -1f, 0.5f, true, () => f.tuning.gravity, v => { foreach (var s in shimmers) s.tuning.gravity = v; });
            section.AddFloat("drag", "Air resistance decaying the inherited train velocity.",
                0f, 3f, true, () => f.tuning.drag, v => { foreach (var s in shimmers) s.tuning.drag = v; });
            section.AddFloat("buoyancy", "Constant upward drift [m/s].",
                0f, 2f, true, () => f.tuning.buoyancy, v => { foreach (var s in shimmers) s.tuning.buoyancy = v; });
            section.AddFloat("strength", "Max shimmer displacement at full heat.",
                0f, 0.05f, false, () => f.tuning.strength, v => { foreach (var s in shimmers) s.tuning.strength = v; });
            section.AddFloat("baseStrength", "Displacement multiplier at zero heat (lerps to 1 at full heat).",
                0f, 1f, false, () => f.tuning.baseStrength, v => { foreach (var s in shimmers) s.tuning.baseStrength = v; });
            section.AddFloat("freq", "Noise frequency of the shimmer field.",
                1f, 20f, false, () => f.tuning.freq, v => { foreach (var s in shimmers) s.tuning.freq = v; });
            section.AddFloat("idleRadius", "Displacement radius at zero heat.",
                0.2f, 1f, false, () => f.tuning.idleRadius, v => { foreach (var s in shimmers) s.tuning.idleRadius = v; });
            section.AddFloat("fullRadius", "Displacement radius at full heat.",
                0.2f, 1f, false, () => f.tuning.fullRadius, v => { foreach (var s in shimmers) s.tuning.fullRadius = v; });
            section.AddFloat("idleAnimSpeed", "Noise scroll speed at zero heat.",
                0f, 3f, false, () => f.tuning.idleAnimSpeed, v => { foreach (var s in shimmers) s.tuning.idleAnimSpeed = v; });
            section.AddFloat("fullAnimSpeed", "Noise scroll speed at full heat.",
                0f, 5f, false, () => f.tuning.fullAnimSpeed, v => { foreach (var s in shimmers) s.tuning.fullAnimSpeed = v; });
            section.AddFloat("speedMultiplier", "Multiplier on the noise scroll speed.",
                0f, 4f, false, () => f.tuning.speedMultiplier, v => { foreach (var s in shimmers) s.tuning.speedMultiplier = v; });
            section.AddFloat("shimmerHoldTime", "Fraction of the particle's lifetime held at full strength before the decay function takes over.",
                0f, 1f, true, () => f.tuning.shimmerHoldTime, v => { foreach (var s in shimmers) s.tuning.shimmerHoldTime = v; });
            section.AddFloat("decayK", "Rational decay tuning constant. Larger k gives a steeper initial drop after the hold time passes.",
                0f, 8f, true, () => f.tuning.decayK, v => { foreach (var s in shimmers) s.tuning.decayK = v; });
            section.AddFloat("speedNormMax", "Speed [m/s] at which speed-based dispersion reaches full strength.",
                1f, 30f, false, () => f.tuning.speedNormMax, v => { foreach (var s in shimmers) s.tuning.speedNormMax = v; });
            section.AddFloat("speedLifetimeScale", "Particle lifetime multiplier at full dispersion.",
                0f, 1f, false, () => f.tuning.speedLifetimeScale, v => { foreach (var s in shimmers) s.tuning.speedLifetimeScale = v; });
            section.AddFloat("speedJitter", "Extra emission jitter [m/s] at full dispersion.",
                0f, 2f, false, () => f.tuning.speedJitter, v => { foreach (var s in shimmers) s.tuning.speedJitter = v; });
            section.AddBool("outline", "Debug: outline the shimmer billboards.",
                false, () => f.outline, v => { foreach (var s in shimmers) s.outline = v; });
            section.AddInt("debug", "Shader debug mode (0=off, 1=mask, 2=offset+mask).",
                0, 2, false, () => f.debug, v => { foreach (var s in shimmers) s.debug = v; });
            section.AddInt("renderQueue", "Material render queue (3000 = smoke, 3010 = after the smoke).",
                2000, 4000, true, () => f.renderQueue, v => { foreach (var s in shimmers) s.renderQueue = v; });
            _sections.Add(section);
        }
    }

    private void BuildPlacementSection(EngineSimulationHost host)
    {
        var exhausts = host.Exhausts;
        if (exhausts.Count == 0) return;

        var section = new Section("exhaust placement", "exhaustPlacement") { OnToggle = () => _needsShrink = true };
        for (var i = 0; i < exhausts.Count; i++)
        {
            var e = exhausts[i];
            section.AddFloat($"exhaust{i}OffsetX", "Exhaust placement offset [m], lateral.",
                -1f, 1f, false,
                () => e.Offset.x,
                v => { e.Offset.x = v; e.Reposition(); });
            section.AddFloat($"exhaust{i}OffsetY", "Exhaust placement offset [m], vertical.",
                -1f, 2f, false,
                () => e.Offset.y,
                v => { e.Offset.y = v; e.Reposition(); });
            section.AddFloat($"exhaust{i}OffsetZ", "Exhaust placement offset [m], fore/aft.",
                -1f, 1f, false,
                () => e.Offset.z,
                v => { e.Offset.z = v; e.Reposition(); });
        }
        section.AddBool("showMarkers", "Debug: show axis crosses at the modded emitter positions.",
            false, () => _showOffsetMarkers, v => { _showOffsetMarkers = v; _debugView.SetVisible(v); });
        _sections.Add(section);
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