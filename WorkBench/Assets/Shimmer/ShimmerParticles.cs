using System;
using System.Collections.Generic;

using TurboTurbo.Modeling;

using UnityEngine;

namespace TurboTurbo
{
    [RequireComponent(typeof(ParticleSystem))]
    public class ShimmerParticles : MonoBehaviour
    {
        private static readonly int Strength = Shader.PropertyToID("_Strength");
        private static readonly int EffectRadius = Shader.PropertyToID("_EffectRadius");
        private static readonly int AnimTime = Shader.PropertyToID("_AnimTime");
        private static readonly int Freq = Shader.PropertyToID("_Freq");
        private static readonly int Outline = Shader.PropertyToID("_Outline");
        private static readonly int Debug1 = Shader.PropertyToID("_Debug");

        public sealed class Settings
        {
            public float idleRate = 5f;
            public float fullRate = 10f;

            public float lifetime = 1.5f;
            public float startSizeMin = 0.8f;
            public float startSizeMax = 0.8f;
            public float sizeOverLifetimeStart = 1f;
            public float sizeOverLifetimeEnd = 6f;
            public float gravity = -0.05f;

            public float drag = 0.8f;
            public float buoyancy = 0.3f;

            public float speedNormMax = 15f;
            public float speedLifetimeScale = 0.4f;
            public float speedJitter = 0.5f;

            public float strength = 0.014f;
            public float baseStrength = 0.1f;
            public float freq = 6f;
            public float idleRadius = 0.8f;
            public float fullRadius = 1f;
            public float idleAnimSpeed = 0.5f;
            public float fullAnimSpeed = 2f;
            public float speedMultiplier = 1f;

            public float shimmerHoldTime = 0.15f;
            public float decayK = 4f;

            public float yOffset = 0.1f;

            public Settings()
            {
            }

            public Settings(Settings other)
            {
                idleRate = other.idleRate;
                fullRate = other.fullRate;
                lifetime = other.lifetime;
                startSizeMin = other.startSizeMin;
                startSizeMax = other.startSizeMax;
                sizeOverLifetimeStart = other.sizeOverLifetimeStart;
                sizeOverLifetimeEnd = other.sizeOverLifetimeEnd;
                gravity = other.gravity;
                drag = other.drag;
                buoyancy = other.buoyancy;
                speedNormMax = other.speedNormMax;
                speedLifetimeScale = other.speedLifetimeScale;
                speedJitter = other.speedJitter;
                strength = other.strength;
                baseStrength = other.baseStrength;
                freq = other.freq;
                idleRadius = other.idleRadius;
                fullRadius = other.fullRadius;
                idleAnimSpeed = other.idleAnimSpeed;
                fullAnimSpeed = other.fullAnimSpeed;
                speedMultiplier = other.speedMultiplier;
                shimmerHoldTime = other.shimmerHoldTime;
                decayK = other.decayK;
                yOffset = other.yOffset;
            }
        }

        [Header("Debug")]
        public bool outline;
        public int debug;

        // smoke goes at 3000 by default, higher means we draw on top of the smoke, displacing it, which looks nice
        public int renderQueue = 3010;

        [Header("Engine signal (0..1)")]
        [Range(0f, 1f)] public float heat;

        /// <summary>World velocity of the vehicle carrying this emitter.
        /// Particles inherit this at emission, then drag decays it.</summary>
        public Vector3 locoVelocity;

        /// <summary>Absolute speed of the vehicle carrying this emitter [m/s].</summary>
        public float absSpeed;

        /// <summary>Per-engine emission and appearance tuning, cloned at bind.</summary>
        public Settings tuning = new Settings();

        /// <summary>Exhaust flow range shared by this host's smoke and shimmer emitters.</summary>
        public ExhaustVelocitySettings velocity = new ExhaustVelocitySettings();

        public Shader shader;

        /// <summary>Custom simulation space. If null, world space is used.</summary>
        public Transform customSimulationSpace;

        private ParticleSystem _ps;
        private ParticleSystemRenderer _renderer;
        private Material _material;
        private float _animTime;
        private float _emitAccumulator;
        private AnimationCurve _sizeCurve;
        private Gradient _alphaGradient;

        public int ParticleCount => _ps != null ? _ps.particleCount : 0;

        /// <summary>Builds the ParticleSystem layout. Call once after the
        /// required properties (shader, customSimulationSpace) are assigned;
        /// call again after edits.</summary>
        public void Configure()
        {
            if (_ps == null) _ps = GetComponent<ParticleSystem>();
            if (_renderer == null) _renderer = GetComponent<ParticleSystemRenderer>();

            var s = tuning;

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
            main.gravityModifier = s.gravity;

            _sizeCurve = AnimationCurve.Linear(0f, s.sizeOverLifetimeStart, 1f, s.sizeOverLifetimeEnd);

            var sol = _ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, _sizeCurve);

            _alphaGradient = BakeAlphaDecayFunction(s.shimmerHoldTime, t => DecayRational(s.decayK, t));

            var col = _ps.colorOverLifetime;
            col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(_alphaGradient);

            // models air resistance
            var lvol = _ps.limitVelocityOverLifetime;
            lvol.enabled = true;
            lvol.space = ParticleSystemSimulationSpace.World;
            lvol.limit = 25f;
            lvol.dampen = 0f;
            lvol.drag = s.drag;
            lvol.multiplyDragByParticleSize = false;
            lvol.multiplyDragByParticleVelocity = true;

            // some buoyancy to counteract the resistance
            var vol = _ps.velocityOverLifetime;
            vol.enabled = true;
            vol.space = ParticleSystemSimulationSpace.World;
            vol.y = s.buoyancy;
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
            var s = tuning;

            var rate = Mathf.Lerp(s.idleRate, s.fullRate, heat);
            _emitAccumulator += rate * Time.deltaTime;
            var n = (int)_emitAccumulator;
            if (n > 0)
            {
                _emitAccumulator -= n;

                // just a safety to avoid runaway particle counts if there's a long lag spike
                n = Mathf.Min(n, 30);

                var upSpeed = Mathf.Lerp(velocity.Idle, velocity.FullLoad, Mathf.Clamp01(heat));
                var coneDir = transform.forward;

                // relative wind tears the plume apart with speed: shorter lifetime, more dispersion jitter
                var speedNorm = Mathf.Clamp01(absSpeed / s.speedNormMax);

                // custom emit requires us to apply the simulation space manually
                var simPos = ParticleSimSpace.Position(customSimulationSpace, transform.position)
                    + ParticleSimSpace.Direction(customSimulationSpace, Vector3.up * s.yOffset);

                for (var i = 0; i < n; i++)
                {
                    var ep = new ParticleSystem.EmitParams
                    {
                        position = simPos,
                        velocity = ParticleSimSpace.Direction(customSimulationSpace,
                            coneDir * (upSpeed * UnityEngine.Random.Range(0.85f, 1.15f))
                                     + UnityEngine.Random.insideUnitSphere * (0.15f + s.speedJitter * speedNorm)
                                     + locoVelocity),
                        startSize = UnityEngine.Random.Range(s.startSizeMin, s.startSizeMax),
                        startColor = Color.white,
                        startLifetime = s.lifetime * UnityEngine.Random.Range(0.9f, 1.1f) * Mathf.Lerp(1f, s.speedLifetimeScale, speedNorm),
                    };
                    _ps.Emit(ep, 1);
                }
            }

            if (_material != null)
            {
                var speed = Mathf.Lerp(s.idleAnimSpeed, s.fullAnimSpeed, heat) * s.speedMultiplier;
                _animTime += Time.deltaTime * speed;
                if (_animTime > 10000f) _animTime -= 10000f;

                _material.SetFloat(Strength, Mathf.Lerp(s.baseStrength, 1f, heat) * s.strength);
                _material.SetFloat(EffectRadius, Mathf.Lerp(s.idleRadius, s.fullRadius, heat));
                _material.SetFloat(AnimTime, _animTime);
                _material.SetFloat(Freq, s.freq);
                _material.SetFloat(Outline, outline ? 1f : 0f);
                _material.SetFloat(Debug1, debug);
            }
        }

        /// <summary>
        /// Bakes an alpha decay function to a <see cref="Gradient"/>.
        /// Holds alpha at 1 for <paramref name="holdFraction"/>, then decays according to <see cref="decay"/>,
        /// which is a function of t in [0,1] to alpha in [0,1].
        /// </summary>
        private static Gradient BakeAlphaDecayFunction(float holdFraction, Func<float, float> decay)
        {
            // hard limit enforced by Unity: up to 8 alpha keys, subtract 1 for the start key
            var maxDecayKeys = 7;

            holdFraction = Mathf.Clamp01(holdFraction);

            var alphaKeys = new List<GradientAlphaKey> { new GradientAlphaKey(1f, 0f) };
            if (holdFraction > 0f)
            {
                alphaKeys.Add(new GradientAlphaKey(1f, holdFraction));
                maxDecayKeys--;
            }

            if (holdFraction < 1f)
            {
                for (var i = 1; i <= maxDecayKeys; i++)
                {
                    var t = (float)i / maxDecayKeys;
                    alphaKeys.Add(new GradientAlphaKey(decay(t), holdFraction + (1f - holdFraction) * t));
                }
            }

            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                alphaKeys.ToArray());
            return gradient;
        }

        private static float DecayRational(float k, float t)
        {
            var d = 1f + k * t;
            return (1f - t * t) / (d * d);
        }
    }
}