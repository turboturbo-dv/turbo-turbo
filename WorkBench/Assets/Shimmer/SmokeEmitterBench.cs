using System.Linq;
using UnityEngine;

namespace TurboTurbo.WorkBench
{
    /// <summary>
    /// Bench harness for the game's exhaust smoke emitter: clones the imported
    /// DE6's ExhaustEngineSmoke (the same template TurboSmokeEmitter clones in
    /// the mod, so shape/size/flipbook/renderer all match), strips the dead DV
    /// scripts, and drives it from the ExhaustSmokeModel - the same wiring
    /// TurboSmoke uses in the mod. Parented under the reference frame so the
    /// two exhaust systems (smoke + shimmer particles) can be evaluated side
    /// by side, including draw-order experiments.
    /// </summary>
    public class SmokeEmitterBench : MonoBehaviour
    {
        [Header("Emitter source (children of the imported LocoDE6)")]
        public string exhaustName = "ExhaustEngineSmoke";
        public string damagedSmokeName = "DamagedEngineSmoke";

        [Header("Smoke model inputs (engineOn = true)")]
        public float lambda = 1.2f;
        [Range(0f, 1f)] public float demand = 0.3f;
        [Range(0f, 1f)] public float rpmNorm = 0.5f;

        /// <summary>Shared engine heat signal (fed by ShimmerBench).</summary>
        [Range(0f, 1f)] public float heat;

        [Header("Emission (match TurboSmoke semantics)")]
        public float cleanRate = 20f;
        public float maxRate = 120f;

        private ParticleSystem _smoke;
        private readonly ExhaustSmokeModel _model = new ExhaustSmokeModel();

        /// <summary>Create the emitter as a child of the reference frame.</summary>
        public void Build(Transform parent)
        {
            var loco = GameObject.Find("LocoDE6");
            if (loco == null)
            {
                Debug.LogError("SmokeEmitterBench: no LocoDE6 in the scene (run TurboTurbo -> WorkBench Scene)");
                return;
            }

            var allPs = loco.GetComponentsInChildren<ParticleSystem>(true);
            ParticleSystem vanilla = allPs.FirstOrDefault(ps => ps.name == exhaustName);
            if (vanilla == null)
            {
                Debug.LogError($"SmokeEmitterBench: '{exhaustName}' not found on the imported loco");
                return;
            }

            // clone the vanilla exhaust, strip the dead DV behaviours (same
            // recipe as TurboSmokeEmitter so the setup matches the mod)
            var go = Object.Instantiate(vanilla.gameObject, parent);
            go.name = "TurboTurbo.SmokeBench";
            foreach (var mb in go.GetComponentsInChildren<MonoBehaviour>(true))
            {
                Destroy(mb); // runtime destroy; missing-script cleanup handles the rest
            }
            go.transform.position = loco.transform.position;
            go.transform.rotation = loco.transform.rotation;
            go.SetActive(true);
            _smoke = go.GetComponent<ParticleSystem>();

            // borrow the damaged-engine material for proven-visible rendering,
            // then mirror TurboSmokeEmitter: flipbook off + procedural puff
            // texture (the vanilla 8x8 cloud atlas tiles read as grey squares)
            var damaged = allPs.FirstOrDefault(ps => ps.name == damagedSmokeName);
            var pr = _smoke.GetComponent<ParticleSystemRenderer>();
            if (damaged != null)
            {
                var mat = damaged.GetComponent<ParticleSystemRenderer>().sharedMaterial;
                if (mat != null)
                {
                    pr.material = new Material(mat) { name = "TurboTurbo.SmokeBenchMat" };
                }
            }

            var tsa = _smoke.textureSheetAnimation;
            if (tsa.enabled)
            {
                tsa.enabled = false;
            }
            pr.material.mainTexture = CreatePuffTexture();

            var main = _smoke.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var em = _smoke.emission;
            em.enabled = true;
            em.rateOverTime = 0f;
            em.rateOverDistance = 0f;
            em.SetBursts(new ParticleSystem.Burst[0]);
            _smoke.Play();

            Debug.Log($"SmokeEmitterBench: cloned '{exhaustName}', material=" +
                      $"{_smoke.GetComponent<ParticleSystemRenderer>().sharedMaterial?.shader?.name}");
        }

        private void Update()
        {
            if (_smoke == null) return;

            // drive the emitter exactly like TurboSmoke.Update does in the mod
            _model.Update(lambda, demand, rpmNorm, engineOn: true, Time.deltaTime);

            var em = _smoke.emission;
            em.rateOverTime = rpmNorm * cleanRate + _model.Density * maxRate;

            var main = _smoke.main;
            main.startColor = _model.Color;
            // aligned exhaust velocity: shimmer formula (base x lerp(1, 3, heat)),
            // with exhaustSpeed kept as the full-load exit speed
            main.startSpeed = ExhaustVelocity.Calculate(heat);
        }

        /// <summary>Procedural soft radial puff (mirrors TurboSmokeEmitter).</summary>
        private static Texture2D CreatePuffTexture()
        {
            const int size = 128;
            var tex = new Texture2D(size, size, TextureFormat.ARGB32, false) { name = "TurboTurbo.SmokeBenchTex" };
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
    }
}
