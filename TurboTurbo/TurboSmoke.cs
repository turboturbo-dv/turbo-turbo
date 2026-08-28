using System.Linq;
using UnityEngine;

namespace TurboTurbo;

/// <summary>
/// A dedicated soot emitter cloned from the vanilla exhaust particle system,
/// driven per-frame by the turbo model's smoke density signal. Following the
/// game's own pattern (DamagedEngineSmoke): black plume as a separate system,
/// leaving the vanilla port-driven exhaust untouched.
/// </summary>
internal sealed class TurboSmokeEmitter
{
    private readonly ParticleSystem _vanilla;
    private readonly ParticleSystem _soot;
    private readonly float _baseSize;
    private readonly Material _ownedMaterial;
    private readonly bool _darkBlendUsed;
    private bool _loggedEmit;

    private static bool _shaderListLogged;

    /// <summary>
    /// DV/SmokeShader is additive: dark tints are invisible. For true black
    /// soot we need an alpha-blended or multiplicative shader - hunt for one
    /// shipped in the build, copying the smoke texture onto it.
    /// </summary>
    /// <summary>
    /// The DieselSmoke texture is DV/SmokeShader-internal (noise-like, near
    /// uniform alpha) - useless as a sprite mask on standard shaders. Build a
    /// soft radial puff mask instead: dense core, feathered edge, slight grain.
    /// </summary>
    private static Texture2D CreatePuffTexture()
    {
        const int size = 128;
        var tex = new Texture2D(size, size, TextureFormat.ARGB32, false) { name = "TurboTurbo.SootTex" };
        var colors = new Color[size * size];
        var rng = new System.Random(7);
        var center = new Vector2(size / 2f - 0.5f, size / 2f - 0.5f);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x - center.x) / (size / 2f);
                float dy = (y - center.y) / (size / 2f);
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float falloff = Mathf.Pow(Mathf.Clamp01(1f - d), 1.6f);
                float grain = 0.85f + 0.15f * (float)rng.NextDouble();
                colors[y * size + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(falloff * grain));
            }
        }
        tex.SetPixels(colors);
        tex.Apply();
        return tex;
    }

    private static Material CreateDarkBlendMaterial(Material source)
    {
        if (!_shaderListLogged)
        {
            _shaderListLogged = true;
            var names = Resources.FindObjectsOfTypeAll<Shader>()
                .Select(s => s.name)
                .Where(n => !string.IsNullOrEmpty(n))
                .Where(n => System.Text.RegularExpressions.Regex.IsMatch(n,
                    @"parti|alpha|multiply|blend|smoke|soft", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                .Distinct()
                .OrderBy(n => n)
                .ToList();
            TurboModel.Log.LogInfo($"shader candidates: {string.Join(", ", names)}");
        }

        foreach (string shaderName in new[]
        {
            "Particles/Alpha Blended",
            "Legacy Shaders/Particles/Alpha Blended",
            "Mobile/Particles/Alpha Blended",
            "Particles/Multiply (Double)",
            "Legacy Shaders/Particles/Multiply",
            "Particles/Standard Unlit",
        })
        {
            Shader shader = Shader.Find(shaderName);
            if (shader == null) continue;

            var material = new Material(shader) { name = $"TurboTurbo.SootMat({shaderName})" };
            material.mainTexture = CreatePuffTexture();
            TurboModel.Log.LogInfo($"soot material: using shader '{shaderName}' with procedural puff texture");
            return material;
        }

        TurboModel.Log.LogWarning("soot material: no alpha-blended particle shader found in build");
        return null;
    }

    internal TurboSmokeEmitter(ParticleSystem vanilla, Material blackMaterial, float sizeMult)
    {
        _vanilla = vanilla;

        var go = Object.Instantiate(vanilla.gameObject, vanilla.transform.parent);
        go.name = "TurboTurbo.Soot";
        foreach (var mb in go.GetComponentsInChildren<MonoBehaviour>(true))
        {
            Object.Destroy(mb);
        }

        // the vanilla exhaust GO is parked inactive while the engine is off
        // (TurnOffPS) - a clone made at spawn would stay invisible forever
        go.SetActive(true);

        _soot = go.GetComponent<ParticleSystem>();

        // use the game's proven visible-black material when available
        // (the exhaust material may blend too faintly for a dark tint)
        var rend = _soot.GetComponent<ParticleSystemRenderer>();
        Material sourceMat = blackMaterial;
        if (sourceMat == null)
        {
            var vanillaRend = vanilla.GetComponent<ParticleSystemRenderer>();
            sourceMat = vanillaRend.sharedMaterial;
        }

        Material darkBlend = CreateDarkBlendMaterial(sourceMat);
        if (darkBlend != null)
        {
            _ownedMaterial = darkBlend;
            _darkBlendUsed = true;
            rend.material = darkBlend; // instance material, owned by us
        }
        else if (sourceMat != null)
        {
            // fallback: vanilla-blend material with a vanilla-visible gray -
            // dense plume instead of true black until we find a dark shader
            rend.sharedMaterial = sourceMat;
        }

        var main = _soot.main;
        main.startColor = new Color(0.05f, 0.05f, 0.05f, 1f);
        _baseSize = main.startSize.constant * sizeMult;
        main.startSize = new ParticleSystem.MinMaxCurve(0.75f * _baseSize, 1.35f * _baseSize);

        // soft edges: fast fade-in, long opaque plateau, smooth fade-out -
        // solid smoke without popping, and size irregularity between puffs
        var col = _soot.colorOverLifetime;
        var gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(Color.black, 0f), new GradientColorKey(Color.black, 1f) },
            new[]
            {
                new GradientAlphaKey(0.4f, 0f),
                new GradientAlphaKey(0.95f, 0.12f),
                new GradientAlphaKey(0.95f, 0.6f),
                new GradientAlphaKey(0f, 1f),
            });
        col.enabled = true;
        col.color = new ParticleSystem.MinMaxGradient(gradient);

        // the vanilla exhaust flipbooks through a sprite atlas - our single
        // puff texture would get sampled per-tile, producing hard squares
        var tsa = _soot.textureSheetAnimation;
        if (tsa.enabled)
        {
            tsa.enabled = false;
            TurboModel.Log.LogInfo($"soot: disabled flipbook animation (was {tsa.numTilesX}x{tsa.numTilesY} tiles)");
        }
        _soot.Play();

        var tex = rend.sharedMaterial != null && rend.sharedMaterial.HasProperty("_MainTex")
            ? rend.sharedMaterial.mainTexture
            : null;
        TurboModel.Log.LogInfo($"soot clone: goActive={go.activeSelf} playing={_soot.isPlaying} " +
                               $"mat={(rend.sharedMaterial ? rend.sharedMaterial.name : "?")} " +
                               $"tex={(tex ? tex.name : "null")} " +
                               $"vanillaPlaying={vanilla.isPlaying}");
    }

    internal void Update(float smokeDensity, bool testMode)
    {
        var em = _soot.emission;
        var main = _soot.main;
        if (testMode)
        {
            // F6 with sim off: unmissable white puffs to verify the render path
            em.rateOverTime = 20f;
            main.startColor = new Color(1f, 1f, 1f, 1f);
            main.startSize = _baseSize * 1.5f;
        }
        else
        {
            em.rateOverTime = smokeDensity * TurboModel.SmokeMaxRate.Value;
            main.startColor = _darkBlendUsed
                ? new Color(0.05f, 0.05f, 0.05f, 1f)       // anthracite on alpha blend
                : new Color(0.14f, 0.14f, 0.14f, 0.95f);   // visible gray on the fallback blend
            main.startSize = _baseSize;
        }

        // mirror the vanilla exhaust velocity so both plumes behave alike
        main.startSpeed = _vanilla.main.startSpeed;

        if (!_loggedEmit && smokeDensity > 0.3f)
        {
            _loggedEmit = true;
            TurboModel.Log.LogInfo($"soot emitting: S={smokeDensity:0.00} rate={smokeDensity * TurboModel.SmokeMaxRate.Value:0} " +
                                   $"playing={_soot.isPlaying} count={_soot.particleCount} goActive={_soot.gameObject.activeSelf}");
        }
    }

    internal void Destroy()
    {
        if (_ownedMaterial != null) Object.Destroy(_ownedMaterial);
        if (_soot != null) Object.Destroy(_soot.gameObject);
    }
}
