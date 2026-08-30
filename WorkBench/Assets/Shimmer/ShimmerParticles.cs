using UnityEngine;

namespace TurboTurbo
{
    /// <summary>
    /// Self-contained exhaust shimmer particle emitter. Designed to be
    /// reusable: zero external dependencies, configures its own
    /// ParticleSystem, and exposes a flow-coupling entry point (SetFlow)
    /// that the mod will drive from the engine's heat/smoke signals.
    ///
    /// Each particle is a billboard running the shimmer grab shader: it
    /// displaces the scene behind it with the same noise field the quad
    /// uses, with the flow-scaled radius/speed/strength semantics of the
    /// mod's per-quad materials.
    ///
    /// Planned evolution (shimmer-particles spike):
    ///  - custom vertex streams feed per-particle random phase
    ///  - particle color/alpha replaces the analytic edge mask
    ///  - smoke rendering joins the same shader (phase 2)
    /// </summary>
    [RequireComponent(typeof(ParticleSystem))]
    public class ShimmerParticles : MonoBehaviour
    {
        [Header("Emission (particles/s, lerped by heat)")]
        public float idleRate = 3f;
        public float fullRate = 10f;

        [Header("Particle look")]
        public float lifetime = 2f;
        public float startSizeMin = 0.8f;
        public float startSizeMax = 0.8f;
        public float sizeOverLifetimeStart = 1f;
        public float sizeOverLifetimeEnd = 1.5f;
        public float startSpeed = 1.5f;
        public float velocityHeatScale = 2.5f;
        public float gravity = -0.05f;
        public Color color = new Color(1f, 0.9f, 0.3f, 0.8f);

        [Header("Shimmer decay (seconds, independent of lifetime)")]
        public float shimmerHoldTime = 0.2f;
        public float shimmerDecayTime = 0.5f;

        [Header("Shimmer (matches HeatQuad.UpdateFade semantics)")]
        public bool useShimmerShader = true;
        public bool outline = false;
        public float strength = 0.01f;
        public float freq = 6f;
        public float idleRadius = 0.3f;
        public float fullRadius = 1f;
        public float idleAnimSpeed = 0.5f;
        public float fullAnimSpeed = 2f;

        [Header("Engine signal (0..1) - driven by the mod per frame")]
        [Range(0f, 1f)] public float heat;

        private ParticleSystem _ps;
        private Material _material;
        private float _animTime;
        private AnimationCurve _sizeCurve;
        private float _sizeCurveStart = -1f;
        private float _sizeCurveEnd = -1f;
        private Gradient _alphaGradient;
        private float _alphaLife = -1f;
        private float _alphaHold = -1f;
        private float _alphaDecay = -1f;

        private void Awake()
        {
            _ps = GetComponent<ParticleSystem>();
            Configure();
        }

        /// <summary>Builds the ParticleSystem layout. Idempotent; safe to
        /// call again after changing structural settings.</summary>
        public void Configure()
        {
            if (_ps == null) _ps = GetComponent<ParticleSystem>();

            var main = _ps.main;
            main.startLifetime = lifetime;
            main.startSpeed = startSpeed;
            main.startSize = new ParticleSystem.MinMaxCurve(startSizeMin, startSizeMax);
            main.startColor = color;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 200;
            main.gravityModifier = gravity;

            // growth: particles expand over their lifetime (smoke-like);
            // curve cached so per-frame re-apply doesn't allocate
            if (_sizeCurve == null || _sizeCurveStart != sizeOverLifetimeStart || _sizeCurveEnd != sizeOverLifetimeEnd)
            {
                _sizeCurve = AnimationCurve.Linear(0f, sizeOverLifetimeStart, 1f, sizeOverLifetimeEnd);
                _sizeCurveStart = sizeOverLifetimeStart;
                _sizeCurveEnd = sizeOverLifetimeEnd;
            }
            var sol = _ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, _sizeCurve);

            // per-particle shimmer envelope: full strength for holdTime,
            // then linear decay over shimmerDecayTime (seconds). Rides the
            // Color alpha stream; smoke will later use its own channel so it
            // can outlive the shimmer.
            if (_alphaGradient == null || _alphaLife != lifetime || _alphaHold != shimmerHoldTime || _alphaDecay != shimmerDecayTime)
            {
                float life = Mathf.Max(lifetime, 0.01f);
                float holdEnd = Mathf.Clamp01(shimmerHoldTime / life);
                float decayEnd = Mathf.Clamp01((shimmerHoldTime + shimmerDecayTime) / life);
                _alphaGradient = new Gradient();
                _alphaGradient.SetKeys(
                    new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                    new[]
                    {
                        new GradientAlphaKey(1f, 0f),
                        new GradientAlphaKey(1f, holdEnd),
                        new GradientAlphaKey(0f, decayEnd),
                        new GradientAlphaKey(0f, 1f),
                    });
                _alphaLife = lifetime;
                _alphaHold = shimmerHoldTime;
                _alphaDecay = shimmerDecayTime;
            }
            var col = _ps.colorOverLifetime;
            col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(_alphaGradient);

            var shape = _ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 8f;
            shape.radius = 0.1f;

            var rend = GetComponent<ParticleSystemRenderer>();
            if (useShimmerShader)
            {
                var shimmer = Shader.Find("TurboTurbo/HeatShimmer");
                if (shimmer != null)
                {
                    if (_material == null || _material.shader != shimmer)
                    {
                        _material = new Material(shimmer) { name = "TurboTurbo.ShimmerParticleMat" };
                        rend.material = _material;
                    }
                }
            }
            if (_material == null)
            {
                rend.material = new Material(Shader.Find("Particles/Standard Unlit"));
            }
        }

        /// <summary>Coupling point: the mod feeds the engine's heat signal
        /// here every frame; emission follows the flow.</summary>
        public void SetFlow(float heat01)
        {
            heat = Mathf.Clamp01(heat01);
        }

        private void Update()
        {
            // re-apply layout every frame so inspector edits apply live
            // (cheap module writes; the material is created only once)
            Configure();

            var em = _ps.emission;
            em.rateOverTime = Mathf.Lerp(idleRate, fullRate, heat);

            // initial velocity scales up with the heat signal: hot exhaust
            // leaves the stack faster (applies to newly emitted particles)
            var main = _ps.main;
            main.startSpeed = startSpeed * Mathf.Lerp(1f, velocityHeatScale, heat);

            if (_material != null)
            {
                // same uniform semantics as HeatQuad.UpdateFade in the mod
                float speed = Mathf.Lerp(idleAnimSpeed, fullAnimSpeed, heat);
                _animTime += Time.deltaTime * speed;
                if (_animTime > 10000f) _animTime -= 10000f;

                _material.SetFloat("_Strength", heat * strength);
                _material.SetFloat("_EffectRadius", Mathf.Lerp(idleRadius, fullRadius, heat));
                _material.SetFloat("_AnimTime", _animTime);
                _material.SetFloat("_Freq", freq);
                _material.SetFloat("_Outline", outline ? 1f : 0f);
            }
        }
    }
}
