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
        public int debug;
        public float strength = 0.01f;
        public float freq = 6f;
        public float idleRadius = 0.8f;
        public float fullRadius = 1f;
        public float idleAnimSpeed = 0.5f;
        public float fullAnimSpeed = 2f;
        public float speedMultiplier = 1f;

        [Header("Inherited motion (train velocity + air resistance)")]
        [Range(0f, 1f)] public float inheritFactor = 1f;
        public float drag = 0.8f;
        public float buoyancy = 0.3f;

        /// <summary>World velocity of the vehicle carrying this emitter -
        /// set by the caller every frame (mod: Car velocity, bench: frame
        /// speed). Particles inherit this at emission, then drag decays it.</summary>
        public Vector3 locoVelocity;

        /// <summary>Shader override for contexts where Shader.Find cannot see
        /// the shader (e.g. it lives in an asset bundle) - set before the
        /// first Configure call, or call Configure again after setting.</summary>
        public Shader shaderOverride;

        [Header("Engine signal (0..1) - driven by the mod per frame")]
        [Range(0f, 1f)] public float heat;

        private ParticleSystem _ps;
        private Material _material;
        private float _animTime;
        private float _emitAccumulator;

        /// <summary>Live particle count, for console dumps.</summary>
        public int ParticleCount => _ps != null ? _ps.particleCount : 0;
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

            // air resistance: drag decays the inherited train velocity
            // exponentially once the particle is expelled
            var lvol = _ps.limitVelocityOverLifetime;
            lvol.enabled = true;
            lvol.space = ParticleSystemSimulationSpace.World;
            lvol.limit = 25f;
            lvol.dampen = 0f;
            lvol.drag = drag;
            lvol.multiplyDragByParticleSize = false;
            lvol.multiplyDragByParticleVelocity = true;

            // buoyancy: constant undamped upward drift so particles keep
            // rising after drag has killed the initial kick
            var vol = _ps.velocityOverLifetime;
            vol.enabled = true;
            vol.space = ParticleSystemSimulationSpace.World;
            vol.y = buoyancy;
            vol.x = 0f;
            vol.z = 0f;

            // manual emission: automatic emission can't add the vehicle's
            // velocity to each particle, so Update() emits via EmitParams
            var em = _ps.emission;
            em.enabled = false;
            em.rateOverTime = 0f;

            var rend = GetComponent<ParticleSystemRenderer>();
            if (useShimmerShader)
            {
                var shimmer = shaderOverride != null ? shaderOverride : Shader.Find("TurboTurbo/HeatShimmer");
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

            // manual emission with inherited vehicle velocity:
            //   v = up * upSpeed(heat) + locoVel * inheritFactor + spread
            // drag (limitVelocityOverLifetime) then decays it in sim.
            float rate = Mathf.Lerp(idleRate, fullRate, heat);
            _emitAccumulator += rate * Time.deltaTime;
            int n = (int)_emitAccumulator;
            if (n > 0)
            {
                _emitAccumulator -= n;
                n = Mathf.Min(n, 30); // burst cap after long frames

                float upSpeed = startSpeed * Mathf.Lerp(1f, velocityHeatScale, heat);
                Vector3 coneDir = transform.forward; // cone aims along local +Z (rotated up)
                Vector3 inherited = locoVelocity * inheritFactor;

                for (int i = 0; i < n; i++)
                {
                    var ep = new ParticleSystem.EmitParams
                    {
                        position = transform.position,
                        velocity = coneDir * (upSpeed * Random.Range(0.85f, 1.15f))
                                 + Random.insideUnitSphere * 0.15f
                                 + inherited,
                        startSize = Random.Range(startSizeMin, startSizeMax),
                        startColor = color,
                        startLifetime = lifetime * Random.Range(0.9f, 1.1f),
                    };
                    _ps.Emit(ep, 1);
                }
            }

            if (_material != null)
            {
                // same uniform semantics as HeatQuad.UpdateFade in the mod
                float speed = Mathf.Lerp(idleAnimSpeed, fullAnimSpeed, heat) * speedMultiplier;
                _animTime += Time.deltaTime * speed;
                if (_animTime > 10000f) _animTime -= 10000f;

                _material.SetFloat("_Strength", heat * strength);
                _material.SetFloat("_EffectRadius", Mathf.Lerp(idleRadius, fullRadius, heat));
                _material.SetFloat("_AnimTime", _animTime);
                _material.SetFloat("_Freq", freq);
                _material.SetFloat("_Outline", outline ? 1f : 0f);
                _material.SetFloat("_Debug", debug);
            }
        }
    }
}
