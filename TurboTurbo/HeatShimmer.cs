using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Logging;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.PostProcessing;
using TurboTurbo.Inspectors;

namespace TurboTurbo;

/// <summary>
/// Heat shimmer above hot exhausts.
/// Mode 5 (default route): per-loco ShimmerParticles emitters - billboard
/// particles with the shared named-grab shader from our asset bundle. The
/// offset field is computed in the fragment shader (value-noise fBm); each
/// particle carries its own shimmer envelope (color alpha) so the plume
/// trails behind a moving loco. Amplitude/speed/size all derive from the
/// engine's heat signal (normalized fuel consumption).
/// Mode 4: hidden. Config-gated legacy route: the SCPE post-stack effect
/// (broken: zooms in DV).
/// </summary>
internal static class HeatShimmer
{
    private sealed class Source
    {
        internal ParticleSystemInspector Emitter;
        internal GameObject ParticlesGo;
        internal ShimmerParticles Particles;
        internal Vector3 Viewport;
        internal float Intensity;
        internal bool WasVisible;
    }

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

    internal static void LogEffectInit(bool shaderFound)
    {
        Log.LogInfo($"heatHaze renderer init: shader 'Hidden/SC Post Effects/Refraction' {(shaderFound ? "FOUND" : "MISSING")}");
    }

    internal static void LogBlitMode(bool fullscreen)
    {
        Log.LogInfo($"heatHaze render: using {(fullscreen ? "BlitFullscreenTriangle" : "plain Blit")}");
    }

    internal static void Register(ParticleSystemInspector emitter)
    {
        Sources.RemoveAll(s => s.Emitter == emitter);
        Sources.Add(new Source { Emitter = emitter });
        Log.LogInfo($"shimmer: registered heat source from {emitter.Car.ID} (total {Sources.Count})");
    }

    internal static void Unregister(ParticleSystemInspector emitter)
    {
        Source s = Sources.FirstOrDefault(x => x.Emitter == emitter);
        if (s != null)
        {
            if (s.ParticlesGo != null) UnityEngine.Object.Destroy(s.ParticlesGo);
            Sources.Remove(s);
        }
        Log.LogInfo($"shimmer: unregistered heat source ({Sources.Count} remain)");
    }

    /// <summary>Console dump: per-source particle state.</summary>
    internal static string[] Dump()
    {
        var lines = new List<string>();
        foreach (Source s in Sources)
        {
            int count = s.Particles != null ? s.Particles.ParticleCount : -1;
            lines.Add($"[shimmer] {s.Emitter.Car.ID}: heat={s.Intensity:0.000} particles={count} " +
                      $"emitterActive={(s.ParticlesGo != null && s.ParticlesGo.activeSelf)}");
        }
        return lines.ToArray();
    }

    internal static void HandleUpdate()
    {
        if (!TurboConfig.HeatShimmerEnabled.Value) return;

        // config-gated legacy route: SCPE post-stack effect (broken in DV)
        if (TurboConfig.HeatShimmerUsePostStack.Value)
        {
            if (!_initialized) TryInit();
            return;
        }

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

        UpdateSources();

        if (Time.time < _nextDebug) return;
        _nextDebug = Time.time + 5.0;
        var src = string.Join(" | ", Sources.Select(s =>
            $"{s.Emitter.Car.ID}: vp=({s.Viewport.x:0.00},{s.Viewport.y:0.00},{s.Viewport.z:0}) heat={s.Intensity:0.00} vis={s.WasVisible}"));
        Log.LogInfo($"shimmer: sources {Sources.Count} -> {src}");
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

        foreach (Source s in Sources)
        {
            var emitter = s.Emitter;
            Vector3 world = emitter.HeatOrigin;
            Vector3 vp = _camera.WorldToViewportPoint(world);

            s.Intensity = emitter.HeatIntensity;

            bool onScreen = vp.z > 0f && vp.x >= -0.2f && vp.x <= 1.2f && vp.y >= -0.2f && vp.y <= 1.2f;
            s.Viewport = vp;
            s.WasVisible = onScreen && s.Intensity > 0f;

            if (!_loggedSourceVisible && s.WasVisible)
            {
                _loggedSourceVisible = true;
                Log.LogInfo($"shimmer: first heat source on screen: {emitter.Car.ID} " +
                            $"vp=({vp.x:0.00},{vp.y:0.00},{vp.z:0}) heat={s.Intensity:0.00}");
            }

            // particle emitter route (mode 5): lazily created, parented to
            // the car so the exhaust follows it; particles simulate in world
            // space so the plume trails behind a moving loco
            bool particlesActive = TurboConfig.HeatShimmerMode.Value == 5;
            if (particlesActive && s.Particles == null)
            {
                s.ParticlesGo = new GameObject("TurboTurbo.ShimmerParticles");
                s.ParticlesGo.transform.SetParent(emitter.Car.transform, false);
                // emitter sits 0.2 m above the stack mouth (world up
                // converted to car-local space, so gradients/roll are fine)
                Vector3 localMouth = emitter.Car.transform.InverseTransformPoint(emitter.HeatOrigin);
                Vector3 localUp = emitter.Car.transform.InverseTransformDirection(Vector3.up);
                s.ParticlesGo.transform.localPosition = localMouth + localUp * 0.2f;
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
                s.Particles.speedMultiplier = TurboConfig.HeatShimmerSpeed.Value;
                s.Particles.debug = TurboConfig.HeatShimmerDebug.Value;
                s.Particles.outline = TurboConfig.HeatShimmerOutline.Value;
                // particles inherit the loco's world velocity at emission;
                // drag (in the component) then bleeds it off
                if (emitter.Car.rb != null)
                {
                    s.Particles.locoVelocity = emitter.Car.rb.velocity;
                }
                s.Particles.outline = TurboConfig.HeatShimmerOutline.Value;
            }
        }
    }

    private static void EnsureNoiseTexture()
    {
        if (_map != null) return;
        // static random pattern, only used by the legacy post-stack route's
        // haze texture. The particle shimmer field is computed entirely in
        // the fragment shader.
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
        Log.LogInfo("shimmer: static noise texture created (128x128, legacy post-stack route only)");
    }
}
