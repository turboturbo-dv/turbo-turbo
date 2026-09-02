using UnityEngine;
using UnityEngine.Serialization;

namespace TurboTurbo
{
    /// <summary>
    /// Self-contained exhaust shimmer particle emitter.
    /// Each particle is a billboard running the shimmer grab shader: it
    /// displaces the scene behind it with a value-noise field, inherits the
    /// vehicle's velocity at emission (decayed by drag), and fades via the
    /// color alpha envelope.
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
        public float sizeOverLifetimeEnd = 2.5f;
        public float gravity = -0.05f;
        public Color color = new Color(1f, 1f, 1f, 1f); // not actually used by the shader, figure out if we can remove this

        [Header("Inherited motion (train velocity + air resistance)")]
        public float drag = 0.8f;
        public float buoyancy = 0.3f;

        /// <summary>World velocity of the vehicle carrying this emitter.
        /// Particles inherit this at emission, then drag decays it.</summary>
        public Vector3 locoVelocity;

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

        [Header("Shimmer decay (proportional to lifetime)")]
        public float shimmerHoldTime = 0.2f;
        // ඞ sus ඞ - these two should really add up to 1
        public float shimmerDecayTime = 0.5f;

        [Header("Engine signal (0..1) - driven by the mod per frame")]
        [Range(0f, 1f)] public float flow;

        /// <summary>Render queue for the shimmer material. Default 3010 =
        /// after the smoke (3000), so the shimmer displaces the plume;
        /// 2990 = before it.</summary>
        public int renderQueue = 3010;

        /// <summary>Shader override for contexts where Shader.Find cannot see
        /// the shader (e.g. it lives in an asset bundle). Set before the
        /// first Configure call, or call Configure again after setting.</summary>
        public Shader shaderOverride;

        private ParticleSystem _ps;
        private ParticleSystemRenderer _renderer;
        private Material _material;
        private float _animTime;
        private float _emitAccumulator;
        private AnimationCurve _sizeCurve;
        private float _sizeCurveStart = -1f;
        private float _sizeCurveEnd = -1f;
        private Gradient _alphaGradient;
        private float _alphaLife = -1f;
        private float _alphaHold = -1f;
        private float _alphaDecay = -1f;

        /// <summary>Live particle count, for console dumps.</summary>
        public int ParticleCount => _ps != null ? _ps.particleCount : 0;

        private void Awake()
        {
            _ps = GetComponent<ParticleSystem>();
            _renderer = GetComponent<ParticleSystemRenderer>();
        }

        private void Start()
        {
            // deferred to Start so callers can inject shaderOverride between
            // AddComponent and the first (and only) automatic Configure
            Configure();
        }

        private void OnValidate()
        {
            if (Application.isPlaying) Configure();
        }

        public void Configure()
        {
            if (_ps == null) _ps = GetComponent<ParticleSystem>();
            if (_renderer == null) _renderer = GetComponent<ParticleSystemRenderer>();

            var main = _ps.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 200;
            main.gravityModifier = gravity;
            // per-particle properties (startLifetime/startSize/startColor/
            // startSpeed) are deliberately not set on the main module: manual
            // emission provides them per particle via EmitParams, which
            // overrides the main module. If an emission path ever stops
            // setting one, revisit this block.

            _sizeCurve = AnimationCurve.Linear(0f, sizeOverLifetimeStart, 1f, sizeOverLifetimeEnd);
            _sizeCurveStart = sizeOverLifetimeStart;
            _sizeCurveEnd = sizeOverLifetimeEnd;

            var sol = _ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, _sizeCurve);

            // per-particle shimmer envelope: full strength for holdTime,
            // then linear decay over shimmerDecayTime (seconds). Rides the
            // Color alpha stream (the shader's shimmer coverage multiplier).
            float holdEnd = Mathf.Clamp01(shimmerHoldTime / lifetime);
            float decayEnd = Mathf.Clamp01((shimmerHoldTime + shimmerDecayTime) / lifetime);
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
            
            var col = _ps.colorOverLifetime;
            col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(_alphaGradient);

            // no shape module: EmitParams.position places every particle at
            // the emitter origin, bypassing the shape; spread comes from the
            // per-particle velocity jitter in Update()

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

            var rend = _renderer;
            // we may want YoungestInFront here too, though it probably won't matter too much.
            // any flicker that results from sorting issues just looks like shimmer noise anyway.
            rend.sortMode = ParticleSystemSortMode.Distance;
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
                    _material.renderQueue = renderQueue;
                }
            }
            if (_material == null)
            {
                rend.material = new Material(Shader.Find("Particles/Standard Unlit"));
            }
        }

        /// <summary>Coupling point: the mod feeds the engine's heat signal
        /// here every frame; emission follows the flow.</summary>
        public void SetFlow(float newFlow)
        {
            flow = Mathf.Clamp01(newFlow);
        }

        private void Update()
        {
            float rate = Mathf.Lerp(idleRate, fullRate, flow);
            _emitAccumulator += rate * Time.deltaTime;
            int n = (int)_emitAccumulator;
            if (n > 0)
            {
                _emitAccumulator -= n;
                n = Mathf.Min(n, 30); // burst cap after long frames

                float upSpeed = ExhaustVelocity.Calculate(flow);
                Vector3 coneDir = transform.forward; // cone aims along local +Z (rotated up)

                for (int i = 0; i < n; i++)
                {
                    var ep = new ParticleSystem.EmitParams
                    {
                        position = transform.position,
                        velocity = coneDir * (upSpeed * Random.Range(0.85f, 1.15f))
                                 + Random.insideUnitSphere * 0.15f
                                 + locoVelocity,
                        startSize = Random.Range(startSizeMin, startSizeMax),
                        startColor = color,
                        startLifetime = lifetime * Random.Range(0.9f, 1.1f),
                    };
                    _ps.Emit(ep, 1);
                }
            }

            if (_material != null)
            {
                float speed = Mathf.Lerp(idleAnimSpeed, fullAnimSpeed, flow) * speedMultiplier;
                _animTime += Time.deltaTime * speed;
                if (_animTime > 10000f) _animTime -= 10000f;

                _material.SetFloat("_Strength", flow * strength);
                _material.SetFloat("_EffectRadius", Mathf.Lerp(idleRadius, fullRadius, flow));
                _material.SetFloat("_AnimTime", _animTime);
                _material.SetFloat("_Freq", freq);
                _material.SetFloat("_Outline", outline ? 1f : 0f);
                _material.SetFloat("_Debug", debug);
            }
        }
    }

    /// <summary>
    /// Shared exhaust exit-velocity calculation for all exhaust emitters.
    /// </summary>
    public static class ExhaustVelocity
    {
        /// <summary>Shared exhaust exit speeds [m/s].</summary>
        public const float Idle = 1.5f;
        public const float FullLoad = 10f;

        public static float Calculate(float heat)
        {
            return Mathf.Lerp(Idle, FullLoad, Mathf.Clamp01(heat));
        }
    }

    public static class ExhaustPlacement
    {
        /// <summary>Places an emitter relative to <paramref name="parent"/>:
        /// <paramref name="exhaustPosition"/> raised by
        /// <paramref name="offsetMeters"/> along world up, cone pointing up
        /// the parent's +Y.</summary>
        public static void PlaceAt(Transform emitter, Vector3 exhaustPosition, Transform parent, float offsetMeters)
        {
            emitter.SetParent(parent, worldPositionStays: false);
            Vector3 localMouth = parent.InverseTransformPoint(exhaustPosition);
            Vector3 localUp = parent.InverseTransformDirection(Vector3.up);
            emitter.localPosition = localMouth + localUp * offsetMeters;
            emitter.localRotation = Quaternion.Euler(-90f, 0f, 0f);
        }
    }
}
