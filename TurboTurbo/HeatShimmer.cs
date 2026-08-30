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
/// Mode 5 (default route): per-object billboard quads with an unnamed GrabPass
/// shader from our asset bundle. The offset field is computed entirely in the
/// fragment shader (value-noise fBm): a flow-scaled, tapered mask anchored at
/// the stack mouth, rising at a flow-dependent speed, with per-quad material
/// state so multiple engines animate independently. Amplitude/speed/size all
/// derive from the engine's heat signal (normalized fuel consumption).
/// Modes 0-3: probe presets on a clone of the game's own window glass shader.
/// Mode 6: solid unlit debug quad. Legacy route (config-gated): the SCPE
/// post-stack effect (zooms in DV).
/// </summary>
internal static class HeatShimmer
{
    private sealed class HeatQuad
    {
        internal GameObject Go;
        internal Material Material;
        internal LineRenderer Outline;

        internal void Init(Material source, Texture2D noise, Transform parent, Vector3 localPosition)
        {
            // single vertical quad, yaw-billboarded to the active camera each
            // frame (UpdateTransform). Conceptually simple; F2/F3 external
            // views will be addressed later.
            var verts = new[]
            {
                new Vector3(-0.5f, 0f, 0f),
                new Vector3(0.5f, 0f, 0f),
                new Vector3(-0.5f, 1f, 0f),
                new Vector3(0.5f, 1f, 0f),
            };
            var norms = new[]
            {
                Vector3.forward, Vector3.forward, Vector3.forward, Vector3.forward,
            };
            var uvs = new[]
            {
                new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f),
            };
            var tris = new[] { 0, 2, 1, 2, 3, 1 };
            var mesh = new Mesh
            {
                name = "TurboTurbo.HeatQuad",
                vertices = verts,
                normals = norms,
                uv = uvs,
                triangles = tris,
                // droplet hack-renderer trick: giant bounds keep the grab alive
                bounds = new Bounds(Vector3.zero, Vector3.one * 100f),
            };

            Go = new GameObject("TurboTurbo.HeatQuad");
            Go.transform.SetParent(parent, false);
            Go.transform.localPosition = localPosition;
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

            // debug wireframe: rectangle outline in local space, inherits the
            // quad's billboard transform and config-driven scale
            var lineGo = new GameObject("TurboTurbo.HeatQuadOutline");
            lineGo.transform.SetParent(Go.transform, false);
            Outline = lineGo.AddComponent<LineRenderer>();
            Outline.useWorldSpace = false;
            Outline.loop = true;
            Outline.positionCount = 4;
            Outline.SetPositions(new[]
            {
                new Vector3(-0.5f, 0f, 0f),
                new Vector3(0.5f, 0f, 0f),
                new Vector3(0.5f, 1f, 0f),
                new Vector3(-0.5f, 1f, 0f),
            });
            Outline.startWidth = 0.015f;
            Outline.endWidth = 0.015f;
            Outline.startColor = Color.yellow;
            Outline.endColor = Color.yellow;
            Outline.material = new Material(Shader.Find("Sprites/Default"));
            Outline.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            Outline.receiveShadows = false;
            Outline.enabled = TurboConfig.HeatShimmerWire.Value;

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

        internal void UpdateTransform(Camera cam, Vector3 worldOrigin)
        {
            if (Go == null || cam == null) return;
            float h = TurboConfig.HeatShimmerHeight.Value;
            // the quad's local origin is its bottom vertex - anchor it directly
            // at the stack mouth; height grows upward only
            Go.transform.position = worldOrigin;
            Vector3 toCam = cam.transform.position - Go.transform.position;
            toCam.y = 0f;
            if (toCam.sqrMagnitude < 0.001f) toCam = Vector3.forward;
            Go.transform.rotation = Quaternion.LookRotation(toCam.normalized, Vector3.up);
            Go.transform.localScale = new Vector3(TurboConfig.HeatShimmerRadius.Value * 2f, h, 1f);
            if (Outline != null) Outline.enabled = TurboConfig.HeatShimmerWire.Value;
        }

        internal void UpdateFade(float intensity, float rawHeat, float dt)
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
            if (mode == 5)
            {
                // mode 5 renders through ShimmerParticles; the quad stays
                // disabled (still used by probe modes 0-3)
                mr.enabled = false;
                return;
            }
            mr.enabled = true;

            // mode 6: solid unlit yellow quad - proves the MeshRenderer and
            // geometry draw at all, independent of any grab/shader machinery
            if (mode == 6)
            {
                if (_solidMaterial == null)
                {
                    _solidMaterial = new Material(Shader.Find("Sprites/Default")) { name = "TurboTurbo.HeatSolid" };
                    _solidMaterial.color = new Color(1f, 0.9f, 0.1f, 0.85f);
                }
                mr.sharedMaterial = _solidMaterial;
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
            if (Outline != null) UnityEngine.Object.Destroy(Outline.material);
            if (Go != null) UnityEngine.Object.Destroy(Go);
        }
    }

    private sealed class Source
    {
        internal TurboSmokeEmitter Emitter;
        internal HeatQuad Quad;
        internal GameObject ParticlesGo;
        internal ShimmerParticles Particles;
        internal Vector3 Viewport;
        internal float Radius;
        internal float Intensity;
        internal bool WasVisible;
    }

    private const float HeatWorldRadius = 1.2f;
    private const float StackOffset = 0.6f;

    private static readonly List<Source> Sources = new();
    private static readonly ManualLogSource Log = BepInEx.Logging.Logger.CreateLogSource("HeatShimmer");

    private static Texture2D _map;
    private static PostProcessVolume _volume;
    private static Camera _camera;

    private static bool _initialized;
    private static double _retryTimer;
    private static double _nextDebug;
    private static bool _loggedSourceVisible;
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
    private static Material _solidMaterial;

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
            if (s.ParticlesGo != null) UnityEngine.Object.Destroy(s.ParticlesGo);
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

        UpdateSources();

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
        // static random pattern, only used as the legacy droplet modes'
        // _MistBumpMap (the droplet shader scrolls it itself). The mode 5
        // shimmer field is computed entirely in the fragment shader now.
        _map = new Texture2D(128, 128, TextureFormat.ARGB32, mipChain: false, linear: true)
        {
            name = "TurboTurbo.HeatNoise",
            wrapMode = TextureWrapMode.Repeat,
            filterMode = FilterMode.Bilinear,
        };
        var pixels = new Color[128 * 128];
        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = new Color(UnityEngine.Random.value, UnityEngine.Random.value, UnityEngine.Random.value, 1f);
        }
        _map.SetPixels(pixels);
        _map.Apply();
        Log.LogInfo("shimmer: static noise texture created (128x128, legacy droplet modes only)");
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
                    $"heatHaze: linear amount=1.0 blit={(refr.useFullscreenTriangle.value ? "fullscreenTriangle" : "plainBlit")}");

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
        // refresh every frame: PlayerManager.ActiveCamera tracks the game's
        // own camera switches (F1 first person <-> F2/F3 external via
        // PlayerCameraOverride), so never cache the camera here
        _camera = GetViewCamera();
        if (_camera == null) return;

        // foreground bleed fix needs the scene depth; in deferred rendering
        // this reuses the G-buffer depth (no extra prepass). PPv2 only
        // enables it for effects that ask, so set the flag every frame.
        if ((_camera.depthTextureMode & DepthTextureMode.Depth) == 0)
        {
            _camera.depthTextureMode |= DepthTextureMode.Depth;
        }

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
                // billboard quad kept only for the droplet probe modes (0-3);
                // mode 5 renders through ShimmerParticles below
                s.Quad.UpdateTransform(_camera, emitter.HeatOrigin);
                float fade = onScreen
                    ? s.Intensity * TurboConfig.HeatShimmerStrength.Value
                    : 0f;
                s.Quad.UpdateFade(fade, s.Intensity, Time.deltaTime);
            }

            // particle emitter route (mode 5): lazily created, parented to
            // the car so the exhaust follows it; particles simulate in world
            // space so the plume trails behind a moving loco
            bool particlesActive = TurboConfig.HeatShimmerMode.Value == 5;
            if (particlesActive && s.Particles == null)
            {
                s.ParticlesGo = new GameObject("TurboTurbo.ShimmerParticles");
                s.ParticlesGo.transform.SetParent(emitter.Car.transform, false);
                s.ParticlesGo.transform.localPosition =
                    emitter.Car.transform.InverseTransformPoint(emitter.HeatOrigin);
                s.ParticlesGo.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f); // cone up
                s.Particles = s.ParticlesGo.AddComponent<ShimmerParticles>();
                s.Particles.shaderOverride = ModAssets.HeatShimmerShader;
                s.Particles.strength = TurboConfig.HeatShimmerStrength.Value;
                s.Particles.Configure();
                Log.LogInfo($"shimmer: particle emitter created for {emitter.Car.ID}");
            }
            if (s.ParticlesGo != null)
            {
                s.ParticlesGo.SetActive(particlesActive);
            }
            if (particlesActive && s.Particles != null)
            {
                // raw heat (0..1): the component applies HeatShimmerStrength
                // itself. Not gated on onScreen - the plume exists in the
                // world even when unobserved, building a trail.
                s.Particles.SetFlow(s.Intensity);
                s.Particles.strength = TurboConfig.HeatShimmerStrength.Value;
                s.Particles.freq = TurboConfig.HeatShimmerFreq.Value;
            }
        }
    }
}
