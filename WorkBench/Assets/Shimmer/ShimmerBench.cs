using System.Collections.Generic;
using UnityEngine;

namespace TurboTurbo.WorkBench
{
    /// <summary>
    /// Standalone harness for the heat shimmer shader: mirrors the mod's
    /// runtime uniform semantics (amplitude/radius/speed all derived from a
    /// 0..1 heat signal) against a high-contrast scrolling background.
    /// </summary>
    public class ShimmerBench : MonoBehaviour
    {
        [Header("Heat signal (0..1) - drives amplitude, radius and speed")]
        [Range(0f, 1f)] public float heat = 1f;

        [Header("Shader params (defaults = tuned in-game values)")]
        public float strength = 0.01f;
        public float freq = 6f;
        public float speedMultiplier = 4f;

        [Header("Flow-driven ranges (match HeatShimmer.cs)")]
        public float idleRadius = 0.3f;
        public float fullRadius = 1f;
        public float idleSpeed = 0.5f;
        public float fullSpeed = 2f;

        [Header("Background scroll speed (uv/s)")]
        public float backgroundScroll = 0.03f;

        [Header("Where the heat quad spawns (manual, tuned to the DE6 stack)")]
        public Vector3 heatQuadPosition = new Vector3(-2.54f, 1.5f, 8f);

        [Header("Plain particle emitter (no shimmer - particle R&D)")]
        public bool particles = true;
        public float particleOffsetX = 3f;

        [Header("Reference frame movement (world-sim trail test)")]
        [Range(0f, 8f)] public float moveSpeed = 1.5f;

        private class ShimmerInstance
        {
            internal Material Material;
            internal float AnimTime;
            internal float Heat;
        }

        private readonly List<ShimmerInstance> _shimmers = new List<ShimmerInstance>();
        private Renderer _background;
        private ShimmerParticles _particleEmitter;
        private GameObject _frame;
        private Vector3 _frameStartPos;

        private void Start()
        {
            Shader shader = Shader.Find("TurboTurbo/HeatShimmer");

            // everything bench-side lives under one reference frame; moving
            // the frame while particles simulate in world space leaves them
            // trailing behind, like a loco driving away from its plume
            _frame = new GameObject("ReferenceFrame");

            GameObject cam = new GameObject("BenchCamera");
            Camera camComp = cam.AddComponent<Camera>();
            camComp.backgroundColor = new Color(0.15f, 0.2f, 0.3f);
            camComp.clearFlags = CameraClearFlags.SolidColor;
            cam.transform.position = Vector3.zero;
            cam.transform.rotation = Quaternion.identity;
            camComp.depthTextureMode |= DepthTextureMode.Depth;
            cam.tag = "MainCamera";
            cam.transform.SetParent(_frame.transform, false);

            // high-contrast checker backdrop - displacement shows as wobble
            Texture2D checker = MakeChecker(512, 16);
            GameObject bg = GameObject.CreatePrimitive(PrimitiveType.Quad);
            bg.name = "Background";
            bg.transform.position = new Vector3(0f, 0f, 10f);
            bg.transform.localScale = new Vector3(14f, 12f, 1f);
            Material bgMat = new Material(Shader.Find("Unlit/Texture"));
            bgMat.mainTexture = checker;
            bg.GetComponent<Renderer>().sharedMaterial = bgMat;
            _background = bg.GetComponent<Renderer>();
            bg.transform.SetParent(_frame.transform, false);

            // the imported loco joins the reference frame too
            var loco = GameObject.Find("LocoDE6");
            if (loco != null) loco.transform.SetParent(_frame.transform, true);

            CreateShimmer(shader, heatQuadPosition, heat);

            if (particles)
            {
                // reusable emitter component (shimmer-particles spike phase 1)
                var go = new GameObject("PlainParticles");
                go.transform.position = new Vector3(2.1f, 0.1f, heatQuadPosition.z);
                go.transform.rotation = Quaternion.Euler(-90f, 0f, 0f); // aim the cone up
                _particleEmitter = go.AddComponent<ShimmerParticles>();
                go.transform.SetParent(_frame.transform, false);
            }

            _frameStartPos = _frame.transform.position;
        }

        private void CreateShimmer(Shader shader, Vector3 position, float heat)
        {
            GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = $"HeatQuad{_shimmers.Count + 1}";
            quad.transform.position = position;
            quad.transform.localScale = new Vector3(2f, 3f, 1f);
            var instance = new ShimmerInstance
            {
                // own material instance per quad, like per-quad materials in
                // the mod: each shimmer is a separate renderer + grab user
                Material = new Material(shader) { name = $"TurboTurbo.HeatShimmerMat{_shimmers.Count + 1}" },
                Heat = heat,
            };
            quad.GetComponent<Renderer>().sharedMaterial = instance.Material;
            _shimmers.Add(instance);
        }

        private void Update()
        {
            // same semantics as HeatQuad.UpdateFade in the mod; the live
            // heat slider drives the shimmer (s.Heat is only the creation
            // default, the slider is the live signal)
            foreach (var s in _shimmers)
            {
                float speed = Mathf.Lerp(idleSpeed, fullSpeed, heat) * speedMultiplier;
                s.AnimTime += Time.deltaTime * speed;
                if (s.AnimTime > 10000f) s.AnimTime -= 10000f;

                s.Material.SetFloat("_Strength", heat * strength);
                s.Material.SetFloat("_EffectRadius", Mathf.Lerp(idleRadius, fullRadius, heat));
                s.Material.SetFloat("_AnimTime", s.AnimTime);
                s.Material.SetFloat("_Freq", freq);
            }

            // the bench heat slider drives the emitter exactly like the mod's
            // HeatIntensity will (UpdateSources -> SetFlow per frame)
            if (_particleEmitter != null)
            {
                _particleEmitter.SetFlow(heat);
            }

            Material bg = _background != null ? _background.sharedMaterial : null;
            if (bg != null && bg.mainTexture != null)
            {
                bg.mainTextureOffset = new Vector2(Time.time * backgroundScroll, 0f);
            }

            // slide the whole reference frame; particles (world sim) stay
            // behind and form the trail. Linear speed, live slider, 0 = rest
            if (_frame != null && moveSpeed > 0.001f)
            {
                _frame.transform.position += Vector3.right * (moveSpeed * Time.deltaTime);
            }
        }

        private static Texture2D MakeChecker(int size, int cells)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear,
            };
            var px = new Color[size * size];
            int cell = size / cells;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    bool on = ((x / cell) + (y / cell)) % 2 == 0;
                    float v = on ? 0.95f : 0.1f;
                    px[y * size + x] = new Color(v, v, v, 1f);
                }
            }
            tex.SetPixels(px);
            tex.Apply();
            return tex;
        }
    }
}
