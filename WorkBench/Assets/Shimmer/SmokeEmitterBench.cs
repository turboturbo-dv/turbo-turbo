using UnityEngine;

namespace TurboTurbo.WorkBench
{
    /// <summary>
    /// Bench harness for a fully-owned exhaust smoke emitter: a fresh
    /// ParticleSystem built from code (no cloning of the game's exhaust, so
    /// no inherited DV modules can sabotage rendering), driven by the
    /// ExhaustSmokeModel - color and density baked per particle at emission
    /// time. Placement is owned by ShimmerBench; the component only
    /// configures and simulates.
    /// </summary>
    [RequireComponent(typeof(ParticleSystem))]
    public class SmokeEmitterBench : MonoBehaviour
    {
        [Header("Smoke model inputs (engineOn = true)")]
        public float lambda = 1.2f;
        [Range(0f, 1f)] public float demand = 0.3f;
        [Range(0f, 1f)] public float rpmNorm = 0.5f;

        [Header("Emission (match TurboSmoke semantics)")]
        public float cleanRate = 20f;
        public float maxRate = 120f;

        [Header("Particle look")]
        public float lifetime = 4f;
        public float startSizeMin = 1f;
        public float startSizeMax = 1.4f;
        public float sizeOverLifetimeStart = 1f;
        public float sizeOverLifetimeEnd = 2f;
        public float buoyancy = 0.3f;
        public float drag = 0.8f;

        /// <summary>Shared engine heat signal (fed by ShimmerBench).</summary>
        [Range(0f, 1f)] public float heat;

        private ParticleSystem _ps;
        private readonly ExhaustSmokeModel _model = new ExhaustSmokeModel();
        private float _emitAccumulator;
        private AnimationCurve _sizeCurve;
        private float _sizeCurveStart = -1f;
        private float _sizeCurveEnd = -1f;

        /// <summary>Live particle count, for console dumps.</summary>
        public int ParticleCount => _ps != null ? _ps.particleCount : 0;

        private void Awake()
        {
            _ps = GetComponent<ParticleSystem>();
            if (_ps == null) _ps = gameObject.AddComponent<ParticleSystem>();
        }

        private void Start()
        {
            Configure();
        }

        /// <summary>Builds the ParticleSystem layout. Idempotent; safe to
        /// call again after changing structural settings.</summary>
        public void Configure()
        {
            if (_ps == null) _ps = GetComponent<ParticleSystem>();

            var main = _ps.main;
            main.startLifetime = lifetime;
            main.startSpeed = 0f; // velocity is set per particle at emission
            main.startSize = new ParticleSystem.MinMaxCurve(startSizeMin, startSizeMax);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 400;
            main.gravityModifier = 0f;

            // growth: smoke expands as it disperses
            if (_sizeCurve == null || _sizeCurveStart != sizeOverLifetimeStart || _sizeCurveEnd != sizeOverLifetimeEnd)
            {
                _sizeCurve = AnimationCurve.Linear(0f, sizeOverLifetimeStart, 1f, sizeOverLifetimeEnd);
                _sizeCurveStart = sizeOverLifetimeStart;
                _sizeCurveEnd = sizeOverLifetimeEnd;
            }
            var sol = _ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, _sizeCurve);

            // no colorOverLifetime: the model's color (baked per particle at
            // emission) fully owns the appearance

            // air resistance decays the inherited train velocity
            var lvol = _ps.limitVelocityOverLifetime;
            lvol.enabled = true;
            lvol.space = ParticleSystemSimulationSpace.World;
            lvol.limit = 25f;
            lvol.dampen = 0f;
            lvol.drag = drag;
            lvol.multiplyDragByParticleSize = false;
            lvol.multiplyDragByParticleVelocity = true;

            // buoyancy: hot flue gas keeps drifting up
            var vol = _ps.velocityOverLifetime;
            vol.enabled = true;
            vol.space = ParticleSystemSimulationSpace.World;
            vol.y = buoyancy;
            vol.x = 0f;
            vol.z = 0f;

            var em = _ps.emission;
            em.enabled = false; // manual emission via EmitParams

            var rend = GetComponent<ParticleSystemRenderer>();
            rend.sortMode = ParticleSystemSortMode.Distance;
            rend.material.shader = Shader.Find("Legacy Shaders/Particles/Alpha Blended");
            rend.material.mainTexture = CreatePuffTexture();
            rend.material.SetColor("_TintColor", Color.white);
        }

        private void Update()
        {
            // re-apply layout every frame so inspector edits apply live
            Configure();

            // per-frame model evaluation (engineOn = true)
            _model.Update(lambda, demand, rpmNorm, engineOn: true, Time.deltaTime);

            // manual emission: exit velocity = shared ExhaustVelocity curve
            float rate = rpmNorm * cleanRate + _model.Density * maxRate;
            _emitAccumulator += rate * Time.deltaTime;
            int n = (int)_emitAccumulator;
            if (n > 0)
            {
                _emitAccumulator -= n;
                n = Mathf.Min(n, 30);

                float upSpeed = ExhaustVelocity.Calculate(heat);
                Vector3 coneDir = transform.forward;

                for (int i = 0; i < n; i++)
                {
                    var ep = new ParticleSystem.EmitParams
                    {
                        position = transform.position,
                        velocity = coneDir * (upSpeed * Random.Range(0.85f, 1.15f))
                                 + Random.insideUnitSphere * 0.15f,
                        startSize = Random.Range(startSizeMin, startSizeMax),
                        startColor = _model.Color,
                        startLifetime = lifetime * Random.Range(0.9f, 1.1f),
                    };
                    _ps.Emit(ep, 1);
                }
            }
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
