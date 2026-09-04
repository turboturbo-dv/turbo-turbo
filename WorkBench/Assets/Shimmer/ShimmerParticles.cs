using TurboTurbo.Modeling;

using UnityEngine;

namespace TurboTurbo
{
    [RequireComponent(typeof(ParticleSystem))]
    public class ShimmerParticles : MonoBehaviour
    {
        [Header("Emission (particles/s)")]
        public float idleRate = 5f;
        public float fullRate = 7f;

        [Header("Particle look")]
        public float lifetime = 1.5f;
        public float startSizeMin = 0.8f;
        public float startSizeMax = 0.8f;
        public float sizeOverLifetimeStart = 1f;
        public float sizeOverLifetimeEnd = 6f;
        public float gravity = -0.05f;
        public Color color = new Color(1f, 1f, 1f, 1f); // TODO: not actually used by the shader, figure out if we can remove this

        [Header("Particle motion")]
        public float drag = 0.8f;
        public float buoyancy = 0.3f;

        /// <summary>World velocity of the vehicle carrying this emitter.
        /// Particles inherit this at emission, then drag decays it.</summary>
        public Vector3 locoVelocity;

        [Header("Shimmer")]
        public bool outline = false;
        public int debug;
        public float strength = 0.014f;
        public float baseStrength = 0.1f;
        public float freq = 6f;
        public float idleRadius = 0.8f;
        public float fullRadius = 1f;
        public float idleAnimSpeed = 0.5f;
        public float fullAnimSpeed = 2f;
        public float speedMultiplier = 1f;

        [Header("Shimmer envelope (fraction of lifetime)")]
        [Range(0f, 1f)] public float shimmerHoldTime = 0.2f;

        [Header("Engine signal (0..1)")]
        [Range(0f, 1f)] public float heat;

        // smoke goes at 3000 by default, higher means we draw on top of the smoke, displacing it, which looks nice
        public int renderQueue = 3010;

        public Shader shader;

        /// <summary>Custom simulation space. If null, world space is used.</summary>
        public Transform customSimulationSpace;

        private ParticleSystem _ps;
        private ParticleSystemRenderer _renderer;
        private Material _material;
        private float _animTime;
        private float _emitAccumulator;
        private AnimationCurve _sizeCurve;
        private float _sizeCurveStart = -1f;
        private float _sizeCurveEnd = -1f;
        private Gradient _alphaGradient;

        public int ParticleCount => _ps != null ? _ps.particleCount : 0;

        /// <summary>Builds the ParticleSystem layout. Call once after the
        /// required properties (shader, customSimulationSpace) are assigned;
        /// call again after edits.</summary>
        public void Configure()
        {
            if (_ps == null) _ps = GetComponent<ParticleSystem>();
            if (_renderer == null) _renderer = GetComponent<ParticleSystemRenderer>();

            // some properties are deliberately not set here; we only emit particles manually
            var main = _ps.main;
            if (customSimulationSpace != null)
            {
                main.simulationSpace = ParticleSystemSimulationSpace.Custom;
                main.customSimulationSpace = customSimulationSpace;
            }
            else
            {
                main.simulationSpace = ParticleSystemSimulationSpace.World;
            }
            main.maxParticles = 200;
            main.gravityModifier = gravity;

            _sizeCurve = AnimationCurve.Linear(0f, sizeOverLifetimeStart, 1f, sizeOverLifetimeEnd);
            _sizeCurveStart = sizeOverLifetimeStart;
            _sizeCurveEnd = sizeOverLifetimeEnd;

            var sol = _ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, _sizeCurve);

            var holdEnd = Mathf.Clamp01(shimmerHoldTime);
            _alphaGradient = new Gradient();
            _alphaGradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(1f, holdEnd),
                    new GradientAlphaKey(0f, 1f),
                });

            var col = _ps.colorOverLifetime;
            col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(_alphaGradient);

            // models air resistance
            var lvol = _ps.limitVelocityOverLifetime;
            lvol.enabled = true;
            lvol.space = ParticleSystemSimulationSpace.World;
            lvol.limit = 25f;
            lvol.dampen = 0f;
            lvol.drag = drag;
            lvol.multiplyDragByParticleSize = false;
            lvol.multiplyDragByParticleVelocity = true;

            // some buoyancy to counteract the resistance
            var vol = _ps.velocityOverLifetime;
            vol.enabled = true;
            vol.space = ParticleSystemSimulationSpace.World;
            vol.y = buoyancy;
            vol.x = 0f;
            vol.z = 0f;

            var em = _ps.emission;
            em.enabled = false;

            var rend = _renderer;
            // we may want YoungestInFront here too, though it probably won't matter too much.
            // any flicker that results from sorting issues just looks like shimmer noise anyway.
            rend.sortMode = ParticleSystemSortMode.Distance;
            if (shader != null)
            {
                if (_material == null || _material.shader != shader)
                {
                    _material = new Material(shader) { name = "TurboTurbo.ShimmerParticleMat" };
                    rend.material = _material;
                }
                _material.renderQueue = renderQueue;
            }
            else
            {
                Debug.LogWarning("[ShimmerParticles] no shader set - assign one and call Configure (mod: asset bundle, bench: Shader.Find)");
            }
        }

        private void OnValidate()
        {
            if (Application.isPlaying) Configure();
        }

        public void SetFlow(float newHeat)
        {
            heat = Mathf.Clamp01(newHeat);
        }

        private void Update()
        {
            var rate = Mathf.Lerp(idleRate, fullRate, heat);
            _emitAccumulator += rate * Time.deltaTime;
            var n = (int)_emitAccumulator;
            if (n > 0)
            {
                _emitAccumulator -= n;

                // just a safety to avoid runaway particle counts if there's a long lag spike
                n = Mathf.Min(n, 30);

                var upSpeed = ExhaustVelocity.Calculate(heat);
                var coneDir = transform.forward;

                // custom emit requires us to apply the simulation space manually
                var simPos = ParticleSimSpace.Position(customSimulationSpace, transform.position);

                for (var i = 0; i < n; i++)
                {
                    var ep = new ParticleSystem.EmitParams
                    {
                        position = simPos,
                        velocity = ParticleSimSpace.Direction(customSimulationSpace,
                            coneDir * (upSpeed * Random.Range(0.85f, 1.15f))
                                     + Random.insideUnitSphere * 0.15f
                                     + locoVelocity),
                        startSize = Random.Range(startSizeMin, startSizeMax),
                        startColor = color,
                        startLifetime = lifetime * Random.Range(0.9f, 1.1f),
                    };
                    _ps.Emit(ep, 1);
                }
            }

            if (_material != null)
            {
                var speed = Mathf.Lerp(idleAnimSpeed, fullAnimSpeed, heat) * speedMultiplier;
                _animTime += Time.deltaTime * speed;
                if (_animTime > 10000f) _animTime -= 10000f;

                _material.SetFloat("_Strength", Mathf.Lerp(baseStrength, 1f, heat) * strength);
                _material.SetFloat("_EffectRadius", Mathf.Lerp(idleRadius, fullRadius, heat));
                _material.SetFloat("_AnimTime", _animTime);
                _material.SetFloat("_Freq", freq);
                _material.SetFloat("_Outline", outline ? 1f : 0f);
                _material.SetFloat("_Debug", debug);
            }
        }
    }
}