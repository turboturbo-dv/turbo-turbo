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
        public float idleRate = 2f;
        public float fullRate = 12f;

        [Header("Particle look")]
        public float lifetime = 2f;
        public float startSizeMin = 0.8f;
        public float startSizeMax = 0.8f;
        public float startSpeed = 1.5f;
        public float velocityHeatScale = 2.5f;
        public float gravity = -0.05f;
        public Color color = new Color(1f, 0.9f, 0.3f, 0.8f);

        [Header("Shimmer (matches HeatQuad.UpdateFade semantics)")]
        public bool useShimmerShader = true;
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
                    _material = new Material(shimmer) { name = "TurboTurbo.ShimmerParticleMat" };
                    rend.material = _material;
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
            }
        }
    }
}
