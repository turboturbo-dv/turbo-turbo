using UnityEngine;
using UnityEngine.Serialization;

namespace TurboTurbo
{
    /// <summary>
    /// Self-contained exhaust shimmer particle emitter.
    /// Each particle is a billboard running the shimmer grab shader,
    /// it draws the scene behind it, displaced by a noise field.
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
        public Color color = new Color(1f, 1f, 1f, 1f); // TODO: not actually used by the shader, figure out if we can remove this

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
        // ඞ sus ඞ
        private float ShimmerDecayTime => 1 - shimmerHoldTime;

        [Header("Engine signal (0..1) - driven by the mod per frame")]
        [Range(0f, 1f)] public float flow;

        // smoke goes at 3000 by default, higher means we draw on top of the smoke, displacing it, which looks nice
        public int renderQueue = 3010;

        // pretty ugly hack, but we need to have a way to set the shader both from the WorkBench and the mod.
        // call Configure() after setting this.
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

            // some properties are deliberately not set here; we only emit particles manually
            var main = _ps.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 200;
            main.gravityModifier = gravity;

            _sizeCurve = AnimationCurve.Linear(0f, sizeOverLifetimeStart, 1f, sizeOverLifetimeEnd);
            _sizeCurveStart = sizeOverLifetimeStart;
            _sizeCurveEnd = sizeOverLifetimeEnd;

            var sol = _ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, _sizeCurve);

            float holdEnd = Mathf.Clamp01(shimmerHoldTime / lifetime);
            float decayEnd = Mathf.Clamp01((shimmerHoldTime + ShimmerDecayTime) / lifetime);
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
            _alphaDecay = ShimmerDecayTime;
            
            var col = _ps.colorOverLifetime;
            col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(_alphaGradient);

            // no shape module currently, could introduce a small distribution here but for now a point source is fine

            // air resistance: drag decays inherited velocity exponentially once the particle is emitted
            var lvol = _ps.limitVelocityOverLifetime;
            lvol.enabled = true;
            lvol.space = ParticleSystemSimulationSpace.World;
            lvol.limit = 25f;
            lvol.dampen = 0f;
            lvol.drag = drag;
            lvol.multiplyDragByParticleSize = false;
            lvol.multiplyDragByParticleVelocity = true;

            // buoyancy: constant upward drift so particles keep rising after drag has killed the initial kick
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

    public static class ExhaustVelocity
    {
        public const float Idle = 1.5f;
        public const float FullLoad = 10f;

        public static float Calculate(float heat)
        {
            return Mathf.Lerp(Idle, FullLoad, Mathf.Clamp01(heat));
        }
    }

    public static class ExhaustPlacement
    {
        public static void PlaceAt(Transform emitter, Vector3 exhaustPosition, Transform parent, float offsetMeters)
        {
            // probably not the easiest way, but hey, it seems to work even under the heaviest of derailments.
            // if ever you wanted to test if the exhaust emits in the right direction even when the loco is upside down
            // boy have I got you covered
            emitter.SetParent(parent, worldPositionStays: false);
            Vector3 localMouth = parent.InverseTransformPoint(exhaustPosition);
            Vector3 localUp = parent.InverseTransformDirection(Vector3.up);
            emitter.localPosition = localMouth + localUp * offsetMeters;
            emitter.localRotation = Quaternion.Euler(-90f, 0f, 0f);
        }
    }
}
