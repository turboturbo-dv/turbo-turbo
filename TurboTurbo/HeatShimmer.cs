using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Logging;
using DV.Rain;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.PostProcessing;

namespace TurboTurbo;

/// <summary>
/// Heat shimmer above hot exhausts.
/// Default route: per-object GrabPass quads on a clone of the game's own
/// window glass shader (DV/NewWindowDropletsShader*) using its mist mode -
/// the one refraction path proven to work in DV's rendering setup. The
/// scrolling noise is generated CPU-side into a shared texture used as the
/// mist normal map, and per-frame strength tracks engine heat.
/// Legacy route (config-gated): the SCPE post-stack effect (zooms in DV).
/// </summary>
internal static class HeatShimmer
{
    private sealed class HeatQuad
    {
        internal GameObject Go;
        internal Material Material;

        internal void Init(Material source, Texture2D noise, Transform parent, Vector3 localPosition)
        {
            // open-ended cylinder = column of hot air above the stack exit.
            // No billboarding needed: grab refraction is screen-space, and a
            // cylinder presents a facing surface from every horizontal angle
            // regardless of which camera renders the view (F1/F2/F3).
            const int segments = 20;
            var verts = new Vector3[segments * 2];
            var norms = new Vector3[segments * 2];
            var uvs = new Vector2[segments * 2];
            var tris = new int[segments * 6];
            for (int s = 0; s < segments; s++)
            {
                float a = (float)s / segments * Mathf.PI * 2f;
                float x = Mathf.Cos(a) * 0.5f;
                float z = Mathf.Sin(a) * 0.5f;
                float u = (float)s / segments;
                verts[s * 2] = new Vector3(x, 0f, z);
                verts[s * 2 + 1] = new Vector3(x, 1f, z);
                norms[s * 2] = new Vector3(x, 0f, z);
                norms[s * 2 + 1] = new Vector3(x, 0f, z);
                uvs[s * 2] = new Vector2(u, 0f);
                uvs[s * 2 + 1] = new Vector2(u, 1f);
                int s1 = (s + 1) % segments;
                tris[s * 6 + 0] = s * 2;
                tris[s * 6 + 1] = s1 * 2;
                tris[s * 6 + 2] = s * 2 + 1;
                tris[s * 6 + 3] = s1 * 2;
                tris[s * 6 + 4] = s1 * 2 + 1;
                tris[s * 6 + 5] = s * 2 + 1;
            }
            var mesh = new Mesh
            {
                name = "TurboTurbo.HeatColumn",
                vertices = verts,
                normals = norms,
                uv = uvs,
                triangles = tris,
                // droplet hack-renderer trick: giant bounds keep the grab alive
                bounds = new Bounds(Vector3.zero, Vector3.one * 100f),
            };

            Go = new GameObject("TurboTurbo.HeatColumn");
            Go.transform.SetParent(parent, false);
            Go.transform.localPosition = localPosition;
            Go.transform.localScale = new Vector3(1.6f, 2.4f, 1.6f);
            var mf = Go.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;
            var mr = Go.AddComponent<MeshRenderer>();
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;

            Material = new Material(source) { name = "TurboTurbo.HeatGlass" };
            if (Material.HasProperty("rainAmount")) Material.SetFloat("rainAmount", 0f);
            if (Material.HasProperty("dropletFadeAmount")) Material.SetFloat("dropletFadeAmount", 0f);
            if (Material.HasProperty("dropletCount")) Material.SetFloat("dropletCount", 0f);
            if (Material.HasProperty("mistFadeAmount")) Material.SetFloat("mistFadeAmount", 0f);
            if (Material.HasProperty("_useSecondGrabPass")) Material.SetInt("_useSecondGrabPass", 0);
            if (noise != null && Material.HasProperty("_MistBumpMap")) Material.SetTexture("_MistBumpMap", noise);
            mr.sharedMaterial = Material;

            // probe diagnostics: keywords + refraction-capable shader inventory
            Log.LogInfo($"shimmer: column material '{Material.name}' shader='{Material.shader.name}' " +
                        $"srcKeywords=[{string.Join(",", source.shaderKeywords)}] " +
                        $"cloneKeywords=[{string.Join(",", Material.shaderKeywords)}]");
            if (!_shaderInventoryLogged)
            {
                _shaderInventoryLogged = true;
                var refractionShaders = Resources.FindObjectsOfTypeAll<Shader>()
                    .Select(s => s.name)
                    .Where(n => !string.IsNullOrEmpty(n))
                    .Where(n => System.Text.RegularExpressions.Regex.IsMatch(n,
                        @"grab|glass|refract|drop|mist|water|distort|bumpdistort", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                    .Distinct()
                    .OrderBy(n => n)
                    .ToList();
                Log.LogInfo($"shimmer: refraction-capable shaders in build: {string.Join(", ", refractionShaders)}");
                foreach (string name in CandidateGlassShaders)
                {
                    Shader s = Shader.Find(name);
                    Log.LogInfo($"shimmer: candidate '{name}': {(s != null ? "FOUND" : "missing")}");
                    if (s != null)
                    {
                        AltGlassShader = s;
                        if (name.StartsWith("DV/SightGlass"))
                        {
                            var probe = new Material(s);
                            Log.LogInfo($"shimmer: sightGlass texProps=[{string.Join(",", probe.GetTexturePropertyNames())}]");
                            UnityEngine.Object.Destroy(probe);
                        }
                        break;
                    }
                }
                if (AltGlassShader != null)
                {
                    _altMaterial = new Material(AltGlassShader) { name = "TurboTurbo.HeatAlt" };
                    Log.LogInfo($"shimmer: alt glass material ready from '{AltGlassShader.name}'");
                }
            }
        }

        internal void UpdateFade(float intensity)
        {
            if (Material == null) return;
            var mr = Go.GetComponent<MeshRenderer>();
            if (mr == null) return;

            int mode = TurboConfig.HeatShimmerMode.Value;
            if (mode == 4)
            {
                mr.enabled = false;
                return;
            }
            mr.enabled = true;

            if (mode == 5)
            {
                Shader bundleShader = ModAssets.HeatShimmerShader;
                if (bundleShader != null)
                {
                if (_bundleMaterial == null || _bundleMaterial.shader != bundleShader)
                {
                    _bundleMaterial = new Material(bundleShader) { name = "TurboTurbo.HeatShimmerMat" };
                    // render BEFORE the smoke particles (3000) so the named
                    // GrabPass executes pre-plume: refracting the additive
                    // smoke reads as a yellow cylinder. The shader tag default
                    // is overridden here; changing the tag breaks LoadFromFile.
                    _bundleMaterial.renderQueue = 2990;
                    Log.LogInfo("shimmer: mode 5 material built from bundle shader 'TurboTurbo/HeatShimmer' (queue 2990)");
                }
                mr.sharedMaterial = _bundleMaterial;
                if (HeatShimmer.NoiseTexture != null) _bundleMaterial.SetTexture("_MainTex", HeatShimmer.NoiseTexture);
                _bundleMaterial.SetFloat("_Strength", intensity * TurboConfig.HeatShimmerStrength.Value);
                }
                else if (HeatShimmer.AltMaterial != null)
                {
                    // fallback: plain instrument glass - mostly invisible, kept for probing
                    mr.sharedMaterial = HeatShimmer.AltMaterial;
                    if (AltMaterial.HasProperty("_MainTex") && HeatShimmer.NoiseTexture != null)
                    {
                        AltMaterial.SetTexture("_MainTex", HeatShimmer.NoiseTexture);
                    }
                    if (AltMaterial.HasProperty("_Color"))
                    {
                        AltMaterial.SetColor("_Color", new Color(1f, 1f, 1f, Mathf.Clamp01(intensity * 0.8f)));
                    }
                }
                return;
            }

            mr.sharedMaterial = Material;

            float mist = intensity, rain = 0f;
            int secondGrab = 0;
            switch (mode)
            {
                case 0: secondGrab = 0; mist = intensity; rain = 0f; break;
                case 1: secondGrab = 1; mist = intensity; rain = 0f; break;
                case 2: secondGrab = 0; mist = intensity * 0.4f; rain = 0f; break;
                case 3: secondGrab = 1; mist = 0f; rain = intensity; break;
            }

            if (Material.HasProperty("mistFadeAmount")) Material.SetFloat("mistFadeAmount", mist);
            if (Material.HasProperty("rainAmount")) Material.SetFloat("rainAmount", rain);
            if (Material.HasProperty("dropletFadeAmount")) Material.SetFloat("dropletFadeAmount", 0f);
            if (Material.HasProperty("dropletCount")) Material.SetFloat("dropletCount", 0f);
            if (Material.HasProperty("_useSecondGrabPass")) Material.SetInt("_useSecondGrabPass", secondGrab);
        }

        internal void Destroy()
        {
            if (Material != null) UnityEngine.Object.Destroy(Material);
            if (Go != null) UnityEngine.Object.Destroy(Go);
        }
    }

    private sealed class Source
    {
        internal TurboSmokeEmitter Emitter;
        internal HeatQuad Quad;
        internal Vector3 Viewport;
        internal float Radius;
        internal float Intensity;
        internal bool WasVisible;
    }

    private const int MapW = 256;
    private const int MapH = 144;
    private const float MapUpdateHz = 15f;
    private const float HeatWorldRadius = 1.2f;
    private const float StackOffset = 0.6f;

    private static readonly List<Source> Sources = new();
    private static readonly ManualLogSource Log = BepInEx.Logging.Logger.CreateLogSource("HeatShimmer");

    private static Texture2D _map;
    private static Color[] _pixels;
    private static PostProcessVolume _volume;
    private static Camera _camera;

    private static bool _initialized;
    private static double _retryTimer;
    private static double _scroll;
    private static double _nextMapUpdate;
    private static double _nextDebug;
    private static bool _loggedSourceVisible;
    private static bool _loggedMapDistortion;
    private static bool _loggedNoLayer;
    private static bool _shaderInventoryLogged;

    private static readonly string[] CandidateGlassShaders =
    {
        "FX/Glass/Stained BumpDistort",   // Unity classic: grab + bump refraction
        "DV/SightGlass",
        "BadDog/BGWater",
    };

    private static Shader AltGlassShader;
    private static Material _altMaterial;
    private static Material _bundleMaterial;

    internal static Material AltMaterial => _altMaterial;
    internal static Texture2D NoiseTexture => _map;
    private static bool _loggedQuadCreated;

    internal static void LogEffectInit(bool shaderFound)
    {
        Log.LogInfo($"heatHaze renderer init: shader 'Hidden/SC Post Effects/Refraction' {(shaderFound ? "FOUND" : "MISSING")}");
    }

    internal static void LogBlitMode(bool fullscreen)
    {
        Log.LogInfo($"heatHaze render: using {(fullscreen ? "BlitFullscreenTriangle" : "plain Blit")}");
    }

    internal static void Register(TurboSmokeEmitter emitter)
    {
        Sources.RemoveAll(s => s.Emitter == emitter);
        var s = new Source { Emitter = emitter };

        if (!TurboConfig.HeatShimmerUsePostStack.Value)
        {
            EnsureNoiseTexture();
            Material glass = FindWindowMaterial(emitter.Car);
            if (glass != null)
            {
                s.Quad = new HeatQuad();
                s.Quad.Init(glass, _map, emitter.Car.transform,
                    emitter.Car.transform.InverseTransformPoint(emitter.HeatOrigin));
                if (!_loggedQuadCreated)
                {
                    _loggedQuadCreated = true;
                    Log.LogInfo($"shimmer: heat quad created from glass material '{glass.name}' shader='{glass.shader.name}' " +
                                $"useSecondGrabPass=0 rain=0 droplets=0 (mist mode)");
                }
            }
            else
            {
                Log.LogWarning($"shimmer: no droplet glass material on {emitter.Car.carType} [{emitter.Car.ID}] - heat quad unavailable");
            }
        }

        Sources.Add(s);
        Log.LogInfo($"shimmer: registered heat source from {emitter.Car.ID} (total {Sources.Count}, quad={(s.Quad != null ? "yes" : "no")})");
    }

    internal static void Unregister(TurboSmokeEmitter emitter)
    {
        Source s = Sources.FirstOrDefault(x => x.Emitter == emitter);
        if (s != null)
        {
            s.Quad?.Destroy();
            Sources.Remove(s);
        }
        Log.LogInfo($"shimmer: unregistered heat source ({Sources.Count} remain)");
    }

    internal static void HandleUpdate()
    {
        if (!TurboConfig.HeatShimmerEnabled.Value) return;

        bool postStack = TurboConfig.HeatShimmerUsePostStack.Value;
        if (postStack)
        {
            if (!_initialized) TryInit();
            if (!_initialized) return;
        }
        else
        {
            EnsureNoiseTexture();
            if (_camera == null) _camera = GetViewCamera();
            if (_camera == null) return;
        }

        _scroll += Time.deltaTime * TurboConfig.HeatShimmerSpeed.Value;
        UpdateSources();
        UpdateMap();

        if (Time.time < _nextDebug) return;
        _nextDebug = Time.time + 5.0;
        var src = string.Join(" | ", Sources.Select(s =>
            $"{s.Emitter.Car.ID}: vp=({s.Viewport.x:0.00},{s.Viewport.y:0.00},{s.Viewport.z:0}) r={s.Radius:0.000} heat={s.Intensity:0.00} vis={s.WasVisible} quad={(s.Quad != null ? "on" : "-")}"));
        Log.LogInfo($"shimmer: sources {Sources.Count} -> {src}");
    }

    private static Material FindWindowMaterial(TrainCar car)
    {
        foreach (Window win in car.GetComponentsInChildren<Window>(true))
        {
            if (win.visuals == null) continue;
            foreach (MeshRenderer mr in win.visuals)
            {
                Material m = mr.sharedMaterial;
                if (m != null && m.shader != null && m.shader.name.StartsWith("DV/NewWindowDroplets"))
                {
                    return m;
                }
            }
        }
        return null;
    }

    private static void EnsureNoiseTexture()
    {
        if (_map != null) return;
        _map = new Texture2D(MapW, MapH, TextureFormat.ARGB32, mipChain: false, linear: true)
        {
            name = "TurboTurbo.HeatNoise",
            wrapMode = TextureWrapMode.Repeat,
            filterMode = FilterMode.Bilinear,
        };
        _pixels = new Color[MapW * MapH];
        _map.SetPixels(_pixels);
        _map.Apply();
        Log.LogInfo($"shimmer: noise texture created ({MapW}x{MapH}, repeat)");
    }

    private static void TryInit()
    {
        if (Time.time < _retryTimer) return;
        _retryTimer = Time.time + 2.0;

        var layers = UnityEngine.Object.FindObjectsOfType<PostProcessLayer>();
        Log.LogInfo($"shimmer init: PostProcessLayer instances found: {layers.Length}");
        if (layers.Length == 0)
        {
            if (!_loggedNoLayer)
            {
                _loggedNoLayer = true;
                Log.LogWarning("shimmer init: no PostProcessLayer in scene - the game may not use the post stack in this mode. Retrying.");
            }
            return;
        }
        foreach (var l in layers)
        {
            var cam = l.GetComponent<Camera>();
            string bundleList = "";
            if (l.sortedBundles != null)
            {
                bundleList = string.Join(" | ", l.sortedBundles.Select(kv =>
                    $"{kv.Key}: " + string.Join(",", kv.Value.Select(b => Type.GetType(b.assemblyQualifiedName)?.Name ?? b.assemblyQualifiedName.Split(',')[0]))));
            }
            Log.LogInfo($"  layer on '{l.gameObject.name}' cam={(cam != null ? cam.name : "?")} enabled={l.enabled} " +
                        $"volumeLayer=0x{l.volumeLayer.value:X8} trigger={(l.volumeTrigger != null ? l.volumeTrigger.name : "none")} " +
                        $"finalBlitToCameraTarget={l.finalBlitToCameraTarget} aa={l.antialiasingMode} " +
                        $"fog={l.fog.enabled} breakBeforeCC={l.breakBeforeColorGrading} bundlesInited={l.haveBundlesBeenInited} " +
                        $"depthFlags={l.cameraDepthFlags}");
            if (bundleList.Length > 0) Log.LogInfo($"    registered bundles: {bundleList}");

            var refrBundle = l.GetBundle<HeatHazeEffect>();
            if (refrBundle != null)
            {
                Log.LogInfo($"    heatHaze bundle: event={refrBundle.attribute.eventType} " +
                            $"allowInSceneView={refrBundle.attribute.allowInSceneView} menu='{refrBundle.attribute.menuItem}'");
            }
            else
            {
                Log.LogWarning($"    heatHaze bundle NOT registered on this layer (PPv2 did not discover our effect)");
            }
        }

        Log.LogInfo($"shimmer init: renderPipeline={(GraphicsSettings.currentRenderPipeline != null ? "SRP (unexpected!)" : "Built-in")}");

        _camera = GetViewCamera();
        if (_camera == null)
        {
            Log.LogWarning("shimmer init: Camera.main not found yet, retrying");
            return;
        }
        Log.LogInfo($"shimmer init: binding to camera '{_camera.name}' " +
                    $"{_camera.pixelWidth}x{_camera.pixelHeight} (screen {Screen.width}x{Screen.height}) " +
                    $"targetTexture={(_camera.targetTexture != null ? $"{_camera.targetTexture.name}({_camera.targetTexture.width}x{_camera.targetTexture.height})" : "none")} " +
                    $"fov={_camera.fieldOfView:0.0} path={_camera.renderingPath} dynRes={_camera.allowDynamicResolution} physCam={_camera.usePhysicalProperties}");

        var layerMask = layers[0].volumeLayer;
        int goLayer = 0;
        for (int i = 0; i < 32; i++)
        {
            if ((layerMask & (1 << i)) != 0)
            {
                goLayer = i;
                break;
            }
        }

        var go = new GameObject("TurboTurbo.HeatShimmer") { layer = goLayer };
        _volume = go.AddComponent<PostProcessVolume>();
        _volume.isGlobal = true;
        _volume.priority = 1000f;
        _volume.weight = 1f;

        var profile = ScriptableObject.CreateInstance<PostProcessProfile>();
        _volume.profile = profile;

        var refr = profile.AddSettings<HeatHazeEffect>();
        EnsureNoiseTexture();
        refr.hazeTex.value = _map;
        refr.hazeTex.overrideState = true;
        refr.amount.overrideState = true;
        refr.amount.value = 1f;
        refr.useFullscreenTriangle.overrideState = true;
        refr.useFullscreenTriangle.value = TurboConfig.HeatShimmerFullscreenTriangle.Value;
        refr.enabled.overrideState = true;
        refr.enabled.value = true;

        Log.LogInfo($"shimmer init: volume created on layer {goLayer} (LayerMask 0x{layerMask.value:X8}), " +
                    $"isGlobal={_volume.isGlobal}, priority={_volume.priority}, " +
                    $"heatHaze: tex={MapW}x{MapH} linear amount=1.0 blit={(refr.useFullscreenTriangle.value ? "fullscreenTriangle" : "plainBlit")}");

        _initialized = true;
    }

    /// <summary>The camera actually rendering the view - the player camera in
    /// first person, but the external (F2/F3) camera when active.</summary>
    private static Camera GetViewCamera()
    {
        return PlayerManager.ActiveCamera != null ? PlayerManager.ActiveCamera : Camera.main;
    }

    private static void UpdateSources()
    {
        if (_camera == null) _camera = GetViewCamera();
        if (_camera == null) return;

        float tanHalf = Mathf.Tan(_camera.fieldOfView * 0.5f * Mathf.Deg2Rad);

        foreach (Source s in Sources)
        {
            var emitter = s.Emitter;
            Vector3 world = emitter.HeatOrigin;
            Vector3 vp = _camera.WorldToViewportPoint(world);

            s.Intensity = emitter.HeatIntensity;

            bool onScreen = vp.z > 0f && vp.x >= -0.2f && vp.x <= 1.2f && vp.y >= -0.2f && vp.y <= 1.2f;
            if (!onScreen)
            {
                s.Radius = 0f;
                s.WasVisible = false;
                s.Viewport = vp;
            }
            else
            {
                float rFrac = HeatWorldRadius / (vp.z * tanHalf);
                s.Radius = Mathf.Clamp(rFrac, 0.01f, 0.35f);
                s.Viewport = vp;
                s.WasVisible = s.Intensity > 0f;

                if (!_loggedSourceVisible && s.Intensity > 0.1f && s.Radius > 0.02f)
                {
                    _loggedSourceVisible = true;
                    Log.LogInfo($"shimmer: first heat source on screen: {emitter.Car.ID} " +
                                $"vp=({vp.x:0.00},{vp.y:0.00},{vp.z:0}) r={s.Radius:0.000} heat={s.Intensity:0.00}");
                }
            }

            if (s.Quad != null)
            {
                // column geometry follows config live
                s.Quad.Go.transform.localScale = new Vector3(
                    TurboConfig.HeatShimmerRadius.Value * 2f,
                    TurboConfig.HeatShimmerHeight.Value,
                    TurboConfig.HeatShimmerRadius.Value * 2f);

                // column is world-anchored above the stack exit; only the
                // refraction strength tracks engine heat
                float fade = onScreen
                    ? s.Intensity * TurboConfig.HeatShimmerStrength.Value
                    : 0f;
                s.Quad.UpdateFade(fade);
            }
        }
    }

    private static void UpdateMap()
    {
        double now = Time.time;
        if (now < _nextMapUpdate) return;
        _nextMapUpdate = now + 1.0 / MapUpdateHz;
        if (_map == null || _pixels == null) return;

        float t = (float)_scroll;
        float maskPeak = 0f;
        float freq = TurboConfig.HeatShimmerFreq.Value;

        int i = 0;
        for (int y = 0; y < MapH; y++)
        {
            float v = (y + 0.5f) / MapH;
            for (int x = 0; x < MapW; x++, i++)
            {
                float u = (x + 0.5f) / MapW;

                float mask = 0f;
                foreach (Source s in Sources)
                {
                    if (s.Radius <= 0f || s.Intensity <= 0f) continue;
                    float dx = (u - s.Viewport.x) * ((float)MapW / MapH);
                    float dy = v - s.Viewport.y;
                    float d = Mathf.Sqrt(dx * dx + dy * dy) / s.Radius;
                    if (d >= 1f) continue;
                    float falloff = 1f - d;
                    mask += s.Intensity * falloff * falloff;
                }
                mask = Mathf.Clamp01(mask);
                if (mask > maskPeak) maskPeak = mask;

                float nx = Mathf.Sin((v * 7f - t * 1.9f) * 6.28f * freq + Mathf.Sin(u * 5f + t * 0.8f * freq) * 2.4f)
                         + 0.5f * Mathf.Sin((v * 13f - t * 3.1f) * 6.28f * freq + u * 11f * freq);
                float ny = Mathf.Sin((v * 6f - t * 2.4f) * 6.28f * freq + Mathf.Sin(u * 4f - t * 1.3f * freq) * 2.2f)
                         + 0.5f * Mathf.Sin((u * 9f + t * 1.1f) * 6.28f * freq + v * 13f * freq);
                nx *= 0.33f;
                ny *= 0.33f;

                _pixels[i] = new Color(
                    0.5f + nx * mask * 0.5f,
                    0.5f + ny * mask * 0.5f,
                    mask,
                    1f);
            }
        }

        _map.SetPixels(_pixels);
        _map.Apply();

        if (!_loggedMapDistortion && maskPeak > 0.2f)
        {
            _loggedMapDistortion = true;
            Log.LogInfo($"shimmer: map now carries distortion (maskPeak={maskPeak:0.00})");
        }
    }
}
