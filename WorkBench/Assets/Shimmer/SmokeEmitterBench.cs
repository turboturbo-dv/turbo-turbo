using System.Linq;
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

        [Header("Atlas source (children of the imported LocoDE6)")]
        public string exhaustName = "ExhaustEngineSmoke";
        public string damagedSmokeName = "DamagedEngineSmoke";

        [Header("Emission (match TurboSmoke semantics)")]
        public float cleanRate = 20f;
        public float maxRate = 120f;

        [Header("Particle look")]
        public float lifetime = 2f;
        public float startSizeMin = 1f;
        public float startSizeMax = 1.4f;
        public float sizeOverLifetimeStart = 1f;
        public float sizeOverLifetimeEnd = 4f;
        public float buoyancy = 0.3f;
        public float drag = 0.8f;

        /// <summary>Shared engine heat signal (fed by ShimmerBench).</summary>
        [Range(0f, 1f)] public float heat;

        private ParticleSystem _ps;
        private Texture _cloudAtlas;
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
            // grab the cloud atlas from the imported loco's damaged-smoke
            // material (same texture TurboSmokeEmitter's borrowed material uses)
            var loco = GameObject.Find("LocoDE6");
            var damaged = loco != null
                ? loco.GetComponentsInChildren<ParticleSystem>(true).FirstOrDefault(ps => ps.name == damagedSmokeName)
                : null;
            if (damaged != null && damaged.GetComponent<ParticleSystemRenderer>().sharedMaterial != null)
            {
                _cloudAtlas = damaged.GetComponent<ParticleSystemRenderer>().sharedMaterial.mainTexture;
            }

            if (_cloudAtlas == null)
            {
                Debug.LogWarning($"[SmokeEmitterBench] Could not find atlas texture from '{damagedSmokeName}' on LocoDE6!");
            }

            // dump the shader + texture the ORIGINAL vanilla exhaust uses
            // (its own renderer material, before any of our changes)
            var vanilla = loco != null
                ? loco.GetComponentsInChildren<ParticleSystem>(true).FirstOrDefault(ps => ps.name == exhaustName)
                : null;
            if (vanilla != null && vanilla.GetComponent<ParticleSystemRenderer>().sharedMaterial != null)
            {
                var mat = vanilla.GetComponent<ParticleSystemRenderer>().sharedMaterial;
                var texName = mat.mainTexture != null ? mat.mainTexture.name : "NULL";
                var texSize = mat.mainTexture != null ? $"{mat.mainTexture.width}x{mat.mainTexture.height}" : "0x0";
                Debug.Log($"[SmokeEmitterBench] vanilla '{vanilla.name}' uses shader '{mat.shader?.name}' " +
                          $"texture '{texName}' ({texSize})");
            }

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

            // smooth fade-out: alpha-only gradient (rgb untouched, so the
            // model color survives) - fades from opaque to transparent over
            // the last 60% of the lifetime
            var fade = new Gradient();
            fade.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(1f, 0.4f),
                    new GradientAlphaKey(0f, 1f),
                });
            var col = _ps.colorOverLifetime;
            col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(fade);

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

            // 1. texture sheet animation OFF: TSA feeds tile offsets through
            // the UV stream (constant per particle) expecting the shader to
            // implement the flipbook - custom shaders sampling the raw UV
            // stream render one solid tile color per particle instead
            var tsa = _ps.textureSheetAnimation;
            tsa.enabled = false;

            // 2. renderer material: single smoke texture (DieselSmoke.png from
            // GameAssets), standard alpha-blended particle shader - no TSA, no
            // flipbook logic: the whole texture maps across each billboard
            var rend = GetComponent<ParticleSystemRenderer>();
            rend.sortMode = ParticleSystemSortMode.Distance;
            rend.material.shader = Shader.Find("Legacy Shaders/Particles/Alpha Blended");
            rend.material.SetColor("_TintColor", Color.white);
            rend.material.mainTexture = _cloudAtlas;
        }

        private void Update()
        {
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
                        rotation = Random.Range(0f, 360f),
                    };
                    _ps.Emit(ep, 1);
                }
            }
        }
    }
}
