using UnityEngine;

namespace TurboTurbo.WorkBench
{
    /// <summary>
    /// Standalone harness for the heat shimmer shader: mirrors the mod's
    /// runtime uniform semantics (amplitude/radius/speed all derived from a
    /// 0..1 heat signal) against a high-contrast scrolling background, with
    /// an opaque occluder to verify the depth-based foreground fix.
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

        private Material _shimmer;
        private float _animTime;
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

            // opaque occluder in front of the shimmer quad: its edges must
            // stay crisp (depth-based foreground fix)
            GameObject occluder = GameObject.CreatePrimitive(PrimitiveType.Cube);
            occluder.name = "Occluder";
            occluder.transform.position = new Vector3(0.35f, 0.1f, 1.6f);
            occluder.transform.localScale = new Vector3(0.22f, 0.5f, 0.22f);
            Material occMat = new Material(Shader.Find("Unlit/Color"));
            occMat.color = new Color(0.85f, 0.3f, 0.1f);
            occluder.GetComponent<Renderer>().sharedMaterial = occMat;

            // shimmer quad
            GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "HeatQuad";
            quad.transform.position = new Vector3(0f, 0f, 4f);
            quad.transform.localScale = new Vector3(2f, 3f, 1f);
            _shimmer = new Material(shader) { name = "TurboTurbo.HeatShimmerMat" };
            quad.GetComponent<Renderer>().sharedMaterial = _shimmer;
        }

        private void Update()
        {
            if (_shimmer == null) return;

            // same semantics as HeatQuad.UpdateFade in the mod
            float speed = Mathf.Lerp(idleSpeed, fullSpeed, heat) * speedMultiplier;
            _animTime += Time.deltaTime * speed;
            if (_animTime > 10000f) _animTime -= 10000f;

            _shimmer.SetFloat("_Strength", heat * strength);
            _shimmer.SetFloat("_EffectRadius", Mathf.Lerp(idleRadius, fullRadius, heat));
            _shimmer.SetFloat("_AnimTime", _animTime);
            _shimmer.SetFloat("_Freq", freq);

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
