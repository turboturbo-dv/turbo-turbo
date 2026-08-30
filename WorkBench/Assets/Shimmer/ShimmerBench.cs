using UnityEngine;

namespace TurboTurbo.WorkBench
{
    /// <summary>
    /// Standalone harness for the shimmer particle system: a DE6 reference
    /// model inside a movable reference frame, against a high-contrast
    /// scrolling background. All shimmer uniforms are owned by the
    /// ShimmerParticles component; the bench only feeds the heat signal.
    /// </summary>
    public class ShimmerBench : MonoBehaviour
    {
        [Header("Heat signal (0..1) - drives emission, velocity, shimmer")]
        [Range(0f, 1f)] public float heat = 1f;

        [Header("Background scroll speed (uv/s)")]
        public float backgroundScroll = 0.03f;

        [Header("Particle emitter placement")]
        public Vector3 emitterPosition = new Vector3(2.1f, 0.1f, 8f);

        [Header("Reference frame movement (world-sim trail test)")]
        [Range(0f, 8f)] public float moveSpeed = 1.5f;

        private Renderer _background;
        private ShimmerParticles _particleEmitter;
        private GameObject _frame;

        private void Start()
        {
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

            // reusable emitter component (shimmer-particles spike phase 1)
            var go = new GameObject("ShimmerParticles");
            go.transform.position = emitterPosition;
            go.transform.rotation = Quaternion.Euler(-90f, 0f, 0f); // aim the cone up
            _particleEmitter = go.AddComponent<ShimmerParticles>();
            go.transform.SetParent(_frame.transform, false);
        }

        private void Update()
        {
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
            if (moveSpeed > 0.001f)
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
