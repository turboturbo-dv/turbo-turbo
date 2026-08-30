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

        [Header("Second shimmer (multi-effect grab test)")]
        public bool secondShimmer = true;
        public float secondOffsetX = 3f;
        [Range(0f, 1f)] public float secondHeat = 0.7f;

        private class ShimmerInstance
        {
            internal Material Material;
            internal float AnimTime;
            internal float Heat;
        }

        private readonly List<ShimmerInstance> _shimmers = new List<ShimmerInstance>();
        private Renderer _background;

        private void Start()
        {
            Shader shader = Shader.Find("TurboTurbo/HeatShimmer");

            GameObject cam = new GameObject("BenchCamera");
            Camera camComp = cam.AddComponent<Camera>();
            camComp.backgroundColor = new Color(0.15f, 0.2f, 0.3f);
            camComp.clearFlags = CameraClearFlags.SolidColor;
            cam.transform.position = Vector3.zero;
            cam.transform.rotation = Quaternion.identity;
            camComp.depthTextureMode |= DepthTextureMode.Depth;
            cam.tag = "MainCamera";

            // high-contrast checker backdrop - displacement shows as wobble
            Texture2D checker = MakeChecker(512, 16);
            GameObject bg = GameObject.CreatePrimitive(PrimitiveType.Quad);
            bg.name = "Background";
            bg.transform.position = new Vector3(0f, 0f, 8f);
            bg.transform.localScale = new Vector3(14f, 8f, 1f);
            Material bgMat = new Material(Shader.Find("Unlit/Texture"));
            bgMat.mainTexture = checker;
            bg.GetComponent<Renderer>().sharedMaterial = bgMat;
            _background = bg.GetComponent<Renderer>();

            CreateShimmer(shader, heatQuadPosition, heat);
            if (secondShimmer)
            {
                CreateShimmer(shader, heatQuadPosition + new Vector3(secondOffsetX, 0f, 0f), secondHeat);
            }
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
            // same semantics as HeatQuad.UpdateFade in the mod
            foreach (var s in _shimmers)
            {
                float speed = Mathf.Lerp(idleSpeed, fullSpeed, s.Heat) * speedMultiplier;
                s.AnimTime += Time.deltaTime * speed;
                if (s.AnimTime > 10000f) s.AnimTime -= 10000f;

                s.Material.SetFloat("_Strength", s.Heat * strength);
                s.Material.SetFloat("_EffectRadius", Mathf.Lerp(idleRadius, fullRadius, s.Heat));
                s.Material.SetFloat("_AnimTime", s.AnimTime);
                s.Material.SetFloat("_Freq", freq);
            }

            Material bg = _background != null ? _background.sharedMaterial : null;
            if (bg != null && bg.mainTexture != null)
            {
                bg.mainTextureOffset = new Vector2(Time.time * backgroundScroll, 0f);
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
