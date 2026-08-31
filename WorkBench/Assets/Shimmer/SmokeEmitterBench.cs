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
        private ParticleSystemRenderer _rend;
        private Material _vanillaMaterial;
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
            // locate the vanilla exhaust and its material: the game renders
            // this smoke with the LIT Standard shader + Cloud01_8x8 atlas -
            // scene lighting is what makes it look correct
            var loco = GameObject.Find("LocoDE6");
            var vanilla = loco != null
                ? loco.GetComponentsInChildren<ParticleSystem>(true).FirstOrDefault(ps => ps.name == exhaustName)
                : null;
            Material vanillaMaterial = vanilla != null && vanilla.GetComponent<ParticleSystemRenderer>() != null
                ? vanilla.GetComponent<ParticleSystemRenderer>().sharedMaterial
                : null;

            if (vanillaMaterial != null)
            {
                _vanillaMaterial = vanillaMaterial;
                // the smoke texture: the vanilla exhaust's own Cloud01_8x8 atlas
                _cloudAtlas = vanillaMaterial.mainTexture;
            }

            if (_cloudAtlas == null)
            {
                Debug.LogWarning($"[SmokeEmitterBench] Could not find atlas texture on '{exhaustName}' (vanilla material)!");
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

            // smooth fade-out: alpha-only gradient (rgb untouched, so the
            // model color survives) - fades from opaque to transparent over
            // the last 60% of the lifetime; multiplies the per-particle
            // model color in the vertex color stream

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

            // texture sheet animation: 8x8 cloud atlas, random start tile,
            // cycling through the set over the lifetime (vanilla behavior)
            var tsa = _ps.textureSheetAnimation;
            tsa.enabled = true;
            tsa.numTilesX = 8;
            tsa.numTilesY = 8;
            tsa.mode = ParticleSystemAnimationMode.Grid;
            tsa.cycleCount = 1;
            // frameOverTime MUST be Curve mode: the two-constant constructor
            // (MinMaxCurve(min, max)) picks ONE random frame per particle and
            // freezes it - the classic no-animation trap
            tsa.frameOverTime = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0f, 1f, 1f));
            tsa.startFrame = new ParticleSystem.MinMaxCurve(0f, 1f); // random phase

            // 2. renderer material: our own unlit smoke shader - texture x
            // vertex color (model color per particle) x envelope alpha.
            // TSA tile UVs are baked into the UV stream by the renderer.
            var rend = GetComponent<ParticleSystemRenderer>();
            _rend = rend;
            rend.sortMode = ParticleSystemSortMode.Distance;
            rend.material.shader = Shader.Find("TurboTurbo/Smoke");
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
                        startColor = _model.Color, // rgb+alpha baked per particle at emission
                        startLifetime = lifetime * Random.Range(0.9f, 1.1f),
                        // rotation disabled: to verify TSA animation frames
                        // rotation = Random.Range(0f, 360f),
                    };
                    _ps.Emit(ep, 1);
                }
            }
        }
    }
}
