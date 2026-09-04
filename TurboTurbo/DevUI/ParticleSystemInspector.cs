using System.Collections.Generic;
using System.Linq;
using System.Text;

using UnityEngine;

namespace TurboTurbo.DevUI;

internal static class ParticleSystemInspector
{
    private static readonly Logger Log = TurboTurbo.Log.ForContext("ps-dump");

    public static void Dump(TrainCar car)
    {
        var roots = car.GetComponentsInChildren<ParticleSystem>(true)
            .Where(ps => ps.transform.parent == null || ps.transform.parent.GetComponent<ParticleSystem>() == null)
            .ToList();

        Log.Info($"=== car '{car.ID}' ({car.carType}): {roots.Count} particle system root(s) ===");
        foreach (var root in roots)
        {
            Log.Info(Describe(root));
        }
    }

    public static string Describe(ParticleSystem root)
    {
        var sb = new StringBuilder();
        Describe(root, root.transform.name, 0, sb);
        return sb.ToString().TrimEnd();
    }

    private static bool IsOurs(ParticleSystem ps) => ps.transform.name.StartsWith("TurboTurbo.");

    private static void Describe(ParticleSystem ps, string path, int depth, StringBuilder sb)
    {
        var pad = new string(' ', depth * 2 + 2);

        sb.AppendLine($"{pad}[PS] '{path}'{(IsOurs(ps) ? " (ours)" : "")} " +
                      $"goActive={ps.gameObject.activeInHierarchy} emitting={ps.isEmitting} particles={ps.particleCount}");
        sb.AppendLine($"{pad}  transform: world={ps.transform.position} local={ps.transform.localPosition} rot={ps.transform.eulerAngles}");

        var main = ps.main;
        var seed = ps.useAutoRandomSeed
            ? "autoRandomSeed=yes"
            : $"autoRandomSeed=no seed={ps.randomSeed}";
        var simSpaceText = main.simulationSpace == ParticleSystemSimulationSpace.Custom
            ? $"simSpace=Custom space='{(main.customSimulationSpace != null ? main.customSimulationSpace.name : "NULL")}' " +
              $"spaceWorld={(main.customSimulationSpace != null ? main.customSimulationSpace.position.ToString() : "n/a")}"
            : $"simSpace={main.simulationSpace}";
        sb.AppendLine($"{pad}  main: duration={main.duration} prewarm={main.prewarm} playOnAwake={main.playOnAwake} " +
                      $"{simSpaceText} maxParticles={main.maxParticles} {seed}");
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
        sb.AppendLine($"{pad}  limitVelocity: enabled={lvol.enabled} space={lvol.space} sepAxes={lvol.separateAxes} " +
                      $"limit: {DescribeCurve(lvol.limit)} dampen={lvol.dampen:0.###} drag: {DescribeCurve(lvol.drag)} " +
                      $"multDragBySize={lvol.multiplyDragByParticleSize} multDragByVel={lvol.multiplyDragByParticleVelocity}");

        var inh = ps.inheritVelocity;
        sb.AppendLine($"{pad}  inheritVelocity: enabled={inh.enabled} mode={inh.mode} " +
                      $"curveMult={inh.curveMultiplier:0.###} curve: {DescribeCurve(inh.curve)}");

        var noise = ps.noise;
        sb.AppendLine($"{pad}  noise: enabled={noise.enabled} quality={noise.quality} " +
                      $"frequency={noise.frequency:0.###} damping={noise.damping} sepAxes={noise.separateAxes}");
        if (noise.enabled)
        {
            sb.AppendLine($"{pad}  noise.strength: {DescribeCurve(noise.strength)}");
            if (noise.separateAxes)
            {
                sb.AppendLine($"{pad}  noise.strengthY: {DescribeCurve(noise.strengthY)}");
            }
            sb.AppendLine($"{pad}  noise.scrollSpeed: {DescribeCurve(noise.scrollSpeed)}");
            sb.AppendLine($"{pad}  noise.remapEnabled={noise.remapEnabled}");
            if (noise.remapEnabled)
            {
                sb.AppendLine($"{pad}  noise.remap: {DescribeCurve(noise.remap)}");
                if (noise.separateAxes)
                {
                    sb.AppendLine($"{pad}  noise.remapY: {DescribeCurve(noise.remapY)}");
                }
            }
        }

        sb.AppendLine($"{pad}  misc: subEmitters={ps.subEmitters.subEmittersCount} " +
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
            var streams = new List<ParticleSystemVertexStream>();
            rend.GetActiveVertexStreams(streams);
            sb.AppendLine($"{pad}  renderer.vertexStreams=[{string.Join(", ", streams)}]");
        }

        foreach (Transform child in ps.transform)
        {
            var cps = child.GetComponent<ParticleSystem>();
            if (cps != null)
            {
                Describe(cps, $"{path}/{child.name}", depth + 1, sb);
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