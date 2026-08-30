using System.Linq;
using UnityEngine;

namespace TurboTurbo;

/// <summary>
/// The exhaust smoke emitter. When TakeOverExhaust is on, this is the sole
/// exhaust system: a clone of the vanilla exhaust, driven per frame by engine
/// rpm (clean haze) plus the turbo model's smoke density (soot), with color
/// lerping between the two. Otherwise it supplements the vanilla system with
/// soot only, following the game's own DamagedEngineSmoke pattern.
/// </summary>
internal sealed class TurboSmokeEmitter
{
    private readonly ParticleSystem _vanilla;
    private readonly ParticleSystem _soot;
    private readonly float _vanillaSize;
    private readonly Color _cleanColor;
    private readonly bool _ownsExhaust;
    private readonly Material _ownedMaterial;
    private bool _loggedEmit;

    private const float StackOffset = 0.05f;

    internal TrainCar Car { get; set; }

    /// <summary>Heat shimmer strength [0..1], fed to HeatShimmer every frame.</summary>
    internal float HeatIntensity { get; private set; }

    /// <summary>World position the shimmer hovers above (exhaust stack exit).</summary>
    internal Vector3 HeatOrigin => _vanilla.transform.position + Vector3.up * StackOffset;

    internal TurboSmokeEmitter(ParticleSystem vanilla, Material blackMaterial, bool ownsExhaust)
    {
        _vanilla = vanilla;
        _ownsExhaust = ownsExhaust;
        _cleanColor = vanilla.main.startColor.color;

        var go = Object.Instantiate(vanilla.gameObject, vanilla.transform.parent);
        go.name = ownsExhaust ? "TurboTurbo.Exhaust" : "TurboTurbo.Soot";
        foreach (var mb in go.GetComponentsInChildren<MonoBehaviour>(true))
        {
            Object.Destroy(mb);
        }

        // the vanilla exhaust GO is parked inactive while the engine is off
        // (TurnOffPS) - a clone made at spawn would stay invisible forever
        go.SetActive(true);

        _soot = go.GetComponent<ParticleSystem>();

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
            rend.material = darkBlend; // instance material, owned by us
        }
        else if (sourceMat != null)
        {
            rend.sharedMaterial = sourceMat;
        }

        var main = _soot.main;
        main.startColor = _cleanColor;
        _vanillaSize = main.startSize.constant;
        main.startSize = CurrentSize();

        // soft edges: fast fade-in, long plateau, smooth fade-out. The plateau
        // alpha lives in startColor (written per frame) so ParticleAlpha can be
        // tuned live from the console.
        var col = _soot.colorOverLifetime;
        var gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[]
            {
                new GradientAlphaKey(0.4f, 0f),
                new GradientAlphaKey(1f, 0.12f),
                new GradientAlphaKey(1f, 0.6f),
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

        // the vanilla emission module also emits per meter travelled and may
        // carry bursts - the emitter must respond to its inputs only
        var sootEm = _soot.emission;
        float distRateWas = sootEm.rateOverDistance.constant;
        int burstsWas = sootEm.burstCount;
        sootEm.rateOverDistance = 0f;
        sootEm.SetBursts(new ParticleSystem.Burst[0]);
        TurboModel.Log.LogInfo($"soot: cleared distance rate (was {distRateWas:0.##}) and {burstsWas} burst(s)");
    }

    private const float HeatDecayTime = 3f;
    private float _heat;

    internal void Update(Color smokeColor, float smokeDensity, float rpmNorm, float fuelNorm, bool engineOn)
    {
        var em = _soot.emission;
        var main = _soot.main;

        // the vanilla tint ships near-opaque (tuned for their additive shader);
        // scale its alpha down for honest alpha-blended haze
        Color clean = new Color(_cleanColor.r, _cleanColor.g, _cleanColor.b,
            _cleanColor.a * TurboConfig.CleanAlpha.Value);

        if (!engineOn)
        {
            em.rateOverTime = 0f;
            main.startColor = clean;
        }
        else if (_ownsExhaust)
        {
            // the exhaust smoke model is authoritative on color (rgb+alpha)
            // and density; haze rate scales with rpm, soot rate with density
            em.rateOverTime = rpmNorm * TurboConfig.CleanRate.Value
                              + smokeDensity * TurboConfig.SmokeMaxRate.Value;
            main.startColor = smokeColor;
        }
        else
        {
            em.rateOverTime = smokeDensity * TurboConfig.SmokeMaxRate.Value;
            main.startColor = smokeColor;
        }

        // aligned exhaust velocity: shared ExhaustVelocity curve, with
        // ExhaustSpeed as the full-load exit speed (idle = /3)
        main.startSpeed = _ownsExhaust
            ? ExhaustVelocity.Calculate(TurboConfig.ExhaustSpeed.Value, HeatIntensity)
            : _vanilla.main.startSpeed;

        // heat shimmer tracks engine mass flow directly (normalized fuel
        // consumption): fuel 0 -> heat 0.15 (idle), fuel 1 -> heat 1.
        // Asymmetric thermal inertia: the stack heats instantly on a throttle
        // kick but cools down slowly.
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

        if (!_loggedEmit && smokeDensity > 0.3f)
        {
            _loggedEmit = true;
            TurboModel.Log.LogInfo($"soot emitting: S={smokeDensity:0.00} playing={_soot.isPlaying} " +
                                   $"count={_soot.particleCount} goActive={_soot.gameObject.activeSelf}");
        }
    }

    internal void Destroy()
    {
        HeatShimmer.Unregister(this);
        if (_ownedMaterial != null) Object.Destroy(_ownedMaterial);
        if (_soot != null) Object.Destroy(_soot.gameObject);
    }

    private ParticleSystem.MinMaxCurve CurrentSize(float mult = 1f)
    {
        float size = _vanillaSize * TurboConfig.SmokeSizeMult.Value * mult;
        return new ParticleSystem.MinMaxCurve(0.75f * size, 1.35f * size);
    }

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

    /// <summary>
    /// DV/SmokeShader is additive: dark tints are invisible. For true black
    /// soot we need an alpha-blended or multiplicative shader - hunt for one
    /// shipped in the build, copying the smoke texture onto it.
    /// </summary>
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

    private static bool _shaderListLogged;
}
