using System.Linq;
using System.Text;
using UnityEngine;

namespace TurboTurbo;

/// <summary>
/// Diagnostic pass: the vanilla ExhaustEngineSmoke is left ENTIRELY untouched
/// (its own ParticlesPortReaders drive it), and everything about it is dumped
/// to the log at attach. Heat intensity still feeds the shimmer.
/// </summary>
internal sealed class TurboSmokeEmitter
{
    private readonly ParticleSystem _vanilla;

    private const float StackOffset = 0.05f;
    private const float HeatDecayTime = 3f;
    private float _heat;
    private float _nextLiveLog;
    private bool _loggedRunning;

    internal TrainCar Car { get; set; }

    /// <summary>Heat shimmer strength [0..1], fed to HeatShimmer every frame.</summary>
    internal float HeatIntensity { get; private set; }

    /// <summary>World position the shimmer hovers above (exhaust stack exit).</summary>
    internal Vector3 HeatOrigin => _vanilla.transform.position + Vector3.up * StackOffset;

    internal TurboSmokeEmitter(ParticleSystem vanilla, Material blackMaterial, bool ownsExhaust)
    {
        _vanilla = vanilla;
        DumpVanilla(vanilla);
    }

    internal void Update(Color smokeColor, float smokeDensity, float rpmNorm, float fuelNorm, bool engineOn)
    {
        // heat: raw fuel-based with asymmetric inertia (keeps the shimmer alive)
        float heatTarget = engineOn ? Mathf.Clamp01(0.15f + 0.85f * fuelNorm) : 0f;
        if (heatTarget > _heat)
        {
            _heat = heatTarget;
        }
        else
        {
            float decay = 1f - Mathf.Exp(-Time.deltaTime / HeatDecayTime);
            _heat = Mathf.Lerp(_heat, heatTarget, decay);
            if (_heat < 0.01f) _heat = 0f;
        }
        HeatIntensity = _heat;

        // periodic live state of the untouched vanilla emitter
        if (Time.time >= _nextLiveLog && engineOn)
        {
            _nextLiveLog = Time.time + 2f;
            TurboModel.Log.LogInfo(
                $"[exhaust-dump] {_vanilla.name}: playing={_vanilla.isPlaying} emitting={_vanilla.isEmitting} " +
                $"particles={_vanilla.particleCount} rate={_vanilla.emission.rateOverTime.constant:0.##} " +
                $"fuelNorm={fuelNorm:0.000}");
        }

        if (!_loggedRunning && _vanilla.isEmitting)
        {
            _loggedRunning = true;
            TurboModel.Log.LogInfo($"[exhaust-dump] {_vanilla.name} is EMITTING (vanilla port readers driving it)");
        }
    }

    internal void Destroy()
    {
        HeatShimmer.Unregister(this);
    }

    // ------------------------------------------------------------------
    // dump
    // ------------------------------------------------------------------

    private static void DumpVanilla(ParticleSystem ps)
    {
        var sb = new StringBuilder();
        sb.AppendLine("[exhaust-dump] ==================================================");
        sb.AppendLine($"[exhaust-dump] full dump of '{ps.name}' (and particle children)");
        Describe(ps, sb, 0);
        TurboModel.Log.LogInfo(sb.ToString());
    }

    private static void Describe(ParticleSystem ps, StringBuilder sb, int depth)
    {
        var pad = new string(' ', depth * 2 + 2);

        sb.AppendLine($"{pad}[PS] '{ps.name}' goActive={ps.gameObject.activeInHierarchy} " +
                      $"emitting={ps.isEmitting} particles={ps.particleCount}");

        var main = ps.main;
        sb.AppendLine($"{pad}  main: duration={main.duration} prewarm={main.prewarm} " +
                      $"playOnAwake={main.playOnAwake} simSpace={main.simulationSpace} maxParticles={main.maxParticles} " +
                      $"randomSeed set via GO");
        sb.AppendLine($"{pad}  main.startLifetime: {DescribeCurve(main.startLifetime)}");
        sb.AppendLine($"{pad}  main.startSpeed:    {DescribeCurve(main.startSpeed)}");
        sb.AppendLine($"{pad}  main.startSize:     {DescribeCurve(main.startSize)}");
        sb.AppendLine($"{pad}  main.startColor:    {DescribeGradient(main.startColor)}");
        sb.AppendLine($"{pad}  main.gravityModifier: {DescribeCurve(main.gravityModifier)}");

        var em = ps.emission;
        sb.AppendLine($"{pad}  emission: enabled={em.enabled} rateOverTime: {DescribeCurve(em.rateOverTime)} " +
                      $"rateOverDistance: {DescribeCurve(em.rateOverDistance)} bursts={em.burstCount}");
        if (em.rateOverTime.mode == ParticleSystemCurveMode.Curve)
        {
            var keys = string.Join(" ", em.rateOverTime.curveMax.keys.Select(k => $"({k.time:0.###},{k.value:0.###})"));
            sb.AppendLine($"{pad}  emission.rateOverTime keys: {keys}");
        }

        var shape = ps.shape;
        sb.AppendLine($"{pad}  shape: enabled={shape.enabled} type={shape.shapeType} angle={shape.angle} radius={shape.radius}");

        var sol = ps.sizeOverLifetime;
        sb.AppendLine($"{pad}  sizeOverLifetime: enabled={sol.enabled} mode={sol.size.mode} " +
                      $"sepAxes={sol.separateAxes} curve@0={sol.size.curveMax.Evaluate(0f):0.###} " +
                      $"@0.5={sol.size.curveMax.Evaluate(0.5f):0.###} @1={sol.size.curveMax.Evaluate(1f):0.###}");

        var col = ps.colorOverLifetime;
        sb.AppendLine($"{pad}  colorOverLifetime: enabled={col.enabled} mode={col.color.mode} " +
                      $"alpha@0={SampleAlpha(col.color, 0f):0.###} @0.25={SampleAlpha(col.color, 0.25f):0.###} " +
                      $"@0.5={SampleAlpha(col.color, 0.5f):0.###} @0.75={SampleAlpha(col.color, 0.75f):0.###} @1={SampleAlpha(col.color, 1f):0.###}");
        if (col.enabled && (col.color.mode == ParticleSystemGradientMode.Gradient || col.color.mode == ParticleSystemGradientMode.TwoGradients))
        {
            var g = col.color.mode == ParticleSystemGradientMode.Gradient ? col.color.gradient : col.color.gradientMax;
            var alphaKeys = string.Join(" ", g.alphaKeys.Select(k => $"({k.time:0.###},{k.alpha:0.###})"));
            var colorKeys = string.Join(" ", g.colorKeys.Select(k => $"({k.time:0.###},{k.color.r:0.##},{k.color.g:0.##},{k.color.b:0.##})"));
            sb.AppendLine($"{pad}  CoL.alphaKeys: {alphaKeys}");
            sb.AppendLine($"{pad}  CoL.colorKeys: {colorKeys}");
        }

        var tsa = ps.textureSheetAnimation;
        sb.AppendLine($"{pad}  TSA: enabled={tsa.enabled} mode={tsa.mode} tiles={tsa.numTilesX}x{tsa.numTilesY} " +
                      $"cycleCount={tsa.cycleCount} frameOverTime mode={tsa.frameOverTime.mode} " +
                      $"startFrame mode={tsa.startFrame.mode} min={tsa.startFrame.constantMin:0.###} max={tsa.startFrame.constantMax:0.###}");
        if (tsa.enabled && tsa.frameOverTime.mode == ParticleSystemCurveMode.Curve)
        {
            var keys = tsa.frameOverTime.curveMax.keys;
            var keyText = string.Join(" ", keys.Select(k => $"({k.time:0.###},{k.value:0.###})"));
            sb.AppendLine($"{pad}  TSA.frameOverTime keys: {keyText}");
        }

        var vol = ps.velocityOverLifetime;
        sb.AppendLine($"{pad}  velocityOverLifetime: enabled={vol.enabled} space={vol.space} " +
                      $"x={DescribeCurve(vol.x)} y={DescribeCurve(vol.y)} z={DescribeCurve(vol.z)}");

        var lvol = ps.limitVelocityOverLifetime;
        sb.AppendLine($"{pad}  limitVelocity: enabled={lvol.enabled} limit: {DescribeCurve(lvol.limit)} " +
                      $"dampen={lvol.dampen:0.###} drag={lvol.drag:0.###}");

        var inh = ps.inheritVelocity;
        sb.AppendLine($"{pad}  inheritVelocity: enabled={inh.enabled} mode={inh.mode} curve: {DescribeCurve(inh.curve)}");

        sb.AppendLine($"{pad}  noise: enabled={ps.noise.enabled} subEmitters={ps.subEmitters.subEmittersCount} " +
                      $"trails: enabled={ps.trails.enabled} lights: enabled={ps.lights.enabled} trigger: enabled={ps.trigger.enabled}");

        var rend = ps.GetComponent<ParticleSystemRenderer>();
        if (rend != null)
        {
            sb.AppendLine($"{pad}  renderer: renderMode={rend.renderMode} sortMode={rend.sortMode} " +
                          $"fudge={rend.sortingFudge:0.###} lengthScale={rend.lengthScale:0.###} velocityScale={rend.velocityScale:0.###}");
            var mat = rend.sharedMaterial;
            if (mat != null)
            {
                var tex = mat.mainTexture;
                sb.AppendLine($"{pad}  renderer.material: '{mat.name}' shader='{mat.shader?.name}' " +
                              $"tex='{(tex != null ? tex.name : "NULL")}' " +
                              $"({(tex != null ? $"{tex.width}x{tex.height}" : "0x0")}) queue={mat.renderQueue}");
                sb.AppendLine($"{pad}  renderer._Color={mat.color}");
            }
            else
            {
                sb.AppendLine($"{pad}  renderer.material: NULL");
            }
            var streams = new System.Collections.Generic.List<ParticleSystemVertexStream>();
            rend.GetActiveVertexStreams(streams);
            sb.AppendLine($"{pad}  renderer.vertexStreams=[{string.Join(", ", streams)}]");
        }

        foreach (Transform child in ps.transform)
        {
            var cps = child.GetComponent<ParticleSystem>();
            if (cps != null)
            {
                Describe(cps, sb, depth + 1);
            }
        }
    }

    private static string DescribeCurve(ParticleSystem.MinMaxCurve curve)
    {
        return curve.mode switch
        {
            ParticleSystemCurveMode.Constant => $"constant {curve.constant:0.###}",
            ParticleSystemCurveMode.Curve => $"curve keys={curve.curve.keys.Length} " +
                $"[@0={curve.curve.Evaluate(0f):0.###} @0.5={curve.curve.Evaluate(0.5f):0.###} @1={curve.curve.Evaluate(1f):0.###}]",
            ParticleSystemCurveMode.TwoConstants => $"twoConstants min={curve.constantMin:0.###} max={curve.constantMax:0.###}",
            ParticleSystemCurveMode.TwoCurves => $"twoCurves keys={curve.curveMin.keys.Length}/{curve.curveMax.keys.Length} " +
                $"min[@1={curve.curveMin.Evaluate(1f):0.###}] max[@1={curve.curveMax.Evaluate(1f):0.###}]",
            _ => curve.mode.ToString(),
        };
    }

    private static string DescribeGradient(ParticleSystem.MinMaxGradient gradient)
    {
        return gradient.mode switch
        {
            ParticleSystemGradientMode.Color => $"color {gradient.color}",
            ParticleSystemGradientMode.Gradient => $"gradient a[@0={gradient.gradient.Evaluate(0f).a:0.###} @0.5={gradient.gradient.Evaluate(0.5f).a:0.###} @1={gradient.gradient.Evaluate(1f).a:0.###}]",
            ParticleSystemGradientMode.TwoColors => $"twoColors min={gradient.colorMin} max={gradient.colorMax}",
            ParticleSystemGradientMode.TwoGradients => $"twoGradients",
            _ => gradient.mode.ToString(),
        };
    }

    private static float SampleAlpha(ParticleSystem.MinMaxGradient gradient, float t)
    {
        return gradient.mode switch
        {
            ParticleSystemGradientMode.Color => gradient.color.a,
            ParticleSystemGradientMode.Gradient => gradient.gradient.Evaluate(t).a,
            ParticleSystemGradientMode.TwoColors => Mathf.Lerp(gradient.colorMin.a, gradient.colorMax.a, t),
            ParticleSystemGradientMode.TwoGradients => Mathf.Lerp(gradient.gradientMin.Evaluate(t).a, gradient.gradientMax.Evaluate(t).a, t),
            _ => 0f,
        };
    }
}
