using System.Collections.Generic;

using TurboTurbo.Modeling;

using UnityEngine;

namespace TurboTurbo.WorkBench
{
    [RequireComponent(typeof(ParticleSystem))]
    public class SmokeParticles : MonoBehaviour
    {
        private static readonly int LightSaturation = Shader.PropertyToID("_Saturation");
        private static readonly int MaxShadowFloor = Shader.PropertyToID("_MaxShadowFloor");
        private static readonly int MinFadeDist = Shader.PropertyToID("_MinFadeDist");
        private static readonly int MaxFadeDist = Shader.PropertyToID("_MaxFadeDist");
        private static readonly int DensityScale = Shader.PropertyToID("_DensityScale");
        private static readonly int DensityFalloffId = Shader.PropertyToID("_DensityFalloff");
        private static readonly int SoftParticlesFadeId = Shader.PropertyToID("_SoftParticlesFade");
        private static readonly int FadeInSecondsId = Shader.PropertyToID("_FadeInSeconds");

        // the CPU write and shader read of the encoded density must use reciprocal powers
        private const float DensityEncodeExponent = 0.5f;

        /// <summary>Decay exponent for the density model, applied to every emitter.</summary>
        public static float DensityFalloff = 1.8f;

        /// <summary>Distance [m] over which smoke fades out as it approaches opaque geometry. 0 disables.</summary>
        public static float SoftParticlesFade = 0.15f;

        /// <summary>Absolute time [s] for a new smoke particle to reach full opacity.</summary>
        public static float FadeInSeconds = 0.07f;

        // near-camera fade range, in metres
        private const float FadeDistMin = 1.5f;
        private const float FadeDistMax = 2.5f;

        public sealed class Settings
        {
            internal const float DefaultIdleEmissionRate = 15f;
            internal const float DefaultFullEmissionRate = 75f;
            internal const float DefaultLifetime = 4.5f;
            internal const float DefaultStartSize = 0.39f;
            internal const float DefaultStartSizeVariance = 0.29f;
            internal const float DefaultSizeOverLifetimeEnd = 6f;
            internal const float DefaultSizeOverLifetimeExponent = 0.65f;
            internal const float DefaultBuoyancy = 0.1f;
            internal const float DefaultDrag = 0.6f;
            internal const float DefaultAngularVelocityMax = 20f;
            internal const float DefaultSpeedNormMax = 15f;
            internal const float DefaultSpeedLifetimeScale = 0.3f;
            internal const float DefaultSpeedJitter = 0.5f;
            internal const float DefaultTurbulenceStrength = 1.25f;
            internal const float DefaultTurbulenceFrequency = 0.5f;
            internal const float DefaultTurbulenceScrollSpeed = 0f;
            internal const float DefaultLightSaturation = 0.35f;
            internal const float DefaultMaxShadowFloor = 0.65f;

            public float idleEmissionRate = DefaultIdleEmissionRate;

            public float fullEmissionRate = DefaultFullEmissionRate;

            public float lifetime = DefaultLifetime;

            public float startSize = DefaultStartSize;

            public float startSizeVariance = DefaultStartSizeVariance;

            public float sizeOverLifetimeEnd = DefaultSizeOverLifetimeEnd;

            public float sizeOverLifetimeExponent = DefaultSizeOverLifetimeExponent;

            public float buoyancy = DefaultBuoyancy;

            public float drag = DefaultDrag;

            public float angularVelocityMax = DefaultAngularVelocityMax;

            public float speedNormMax = DefaultSpeedNormMax;

            public float speedLifetimeScale = DefaultSpeedLifetimeScale;

            public float speedJitter = DefaultSpeedJitter;

            public float turbulenceStrength = DefaultTurbulenceStrength;

            public float turbulenceFrequency = DefaultTurbulenceFrequency;

            public float turbulenceScrollSpeed = DefaultTurbulenceScrollSpeed;

            public float lightSaturation = DefaultLightSaturation;

            public float maxShadowFloor = DefaultMaxShadowFloor;

            public Settings()
            {
            }

            public Settings(Settings other)
            {
                idleEmissionRate = other.idleEmissionRate;
                fullEmissionRate = other.fullEmissionRate;
                lifetime = other.lifetime;
                startSize = other.startSize;
                startSizeVariance = other.startSizeVariance;
                sizeOverLifetimeEnd = other.sizeOverLifetimeEnd;
                sizeOverLifetimeExponent = other.sizeOverLifetimeExponent;
                buoyancy = other.buoyancy;
                drag = other.drag;
                angularVelocityMax = other.angularVelocityMax;
                speedNormMax = other.speedNormMax;
                speedLifetimeScale = other.speedLifetimeScale;
                speedJitter = other.speedJitter;
                turbulenceStrength = other.turbulenceStrength;
                turbulenceFrequency = other.turbulenceFrequency;
                turbulenceScrollSpeed = other.turbulenceScrollSpeed;
                lightSaturation = other.lightSaturation;
                maxShadowFloor = other.maxShadowFloor;
            }
        }

        [Header("Smoke model inputs")]
        public float lambda = 1.2f;
        [Range(0f, 1f)] public float rpmNorm = 0.5f;
        [Range(0f, 1f)] public float heat;

        /// <summary>Normalized stack fuel-vapour driver from the stack model.</summary>
        [Range(0f, 1f)] public float vapour;

        /// <summary>Exhaust mass-flow proxy, used by the coefficient velocity model.</summary>
        public float massFlow;

        /// <summary>Exhaust gas density [kg/m^3], used by the coefficient velocity model.</summary>
        public float gasDensity;

        public bool engineOn = true;

        public Vector3 locoVelocity;

        /// <summary>Absolute speed of the vehicle carrying this emitter [m/s].</summary>
        public float absSpeed;

        /// <summary>Per-engine emission and appearance tuning, cloned at bind.</summary>
        public Settings tuning = new Settings();

        /// <summary>Exhaust flow range shared by this host's smoke and shimmer emitters.</summary>
        public ExhaustVelocitySettings velocity = new ExhaustVelocitySettings();

        public Shader shader;
        public Texture atlas;

        /// <summary>Custom simulation space. If null, world space is used.</summary>
        public Transform customSimulationSpace;

        private ParticleSystem _ps;
        private ParticleSystemRenderer _renderer;
        private float _emitAccumulator;
        private AnimationCurve _sizeCurve;
        private float _densityScale = 1f;

        public int ParticleCount => _ps.particleCount;

        /// <summary>The internal appearance model (dev panel edits its settings).</summary>
        internal ExhaustSmokeModel Model { get; } = new ExhaustSmokeModel();

        private void OnValidate()
        {
            if (Application.isPlaying) Configure();
        }

        /// <summary>Builds the ParticleSystem layout. Call once after the
        /// required properties (shader, atlas, customSimulationSpace) are
        /// assigned; call again after edits.</summary>
        public void Configure()
        {
            if (_ps == null) _ps = GetComponent<ParticleSystem>();

            if (shader == null || atlas == null)
            {
                Debug.LogWarning("[SmokeParticles] missing shader or atlas, smoke will not render");
            }

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

            main.maxParticles = 400;
            main.gravityModifier = 0f;

            // growth: smoke expands as it disperses
            _sizeCurve = ParticleCurves.BakeSizeCurve(s.startSize, s.sizeOverLifetimeEnd, s.sizeOverLifetimeExponent);

            var sol = _ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, _sizeCurve);

            // the whole alpha envelope lives in the shader, so the particle system must not apply one
            var col = _ps.colorOverLifetime;
            col.enabled = false;

            // no shape module currently, could introduce a small distribution here but for now a point source is fine

            var lvol = _ps.limitVelocityOverLifetime;
            lvol.enabled = true;
            lvol.space = ParticleSystemSimulationSpace.World;

            // we only apply drag, but we need to override the restrictive defaults on limit/dampen
            lvol.limit = 1000f;
            lvol.dampen = 0f;
            lvol.drag = s.drag;

            lvol.multiplyDragByParticleSize = false;
            lvol.multiplyDragByParticleVelocity = true;

            // vanilla turbulence: static noise field (scrollSpeed 0) traversed by
            // the moving particles, with per-particle random offsets. strength is
            // speed-modulated in Update; Configure bakes the peak so the bench
            // edit-mode preview shows the full field
            var noise = _ps.noise;
            noise.enabled = true;
            noise.quality = ParticleSystemNoiseQuality.High;
            noise.frequency = s.turbulenceFrequency;
            noise.strength = s.turbulenceStrength;
            noise.damping = true;
            noise.separateAxes = false;
            noise.scrollSpeed = s.turbulenceScrollSpeed;
            noise.remapEnabled = false;

            // some buoyancy to counteract the resistance
            var vol = _ps.velocityOverLifetime;
            vol.enabled = true;
            vol.space = ParticleSystemSimulationSpace.World;
            vol.y = s.buoyancy;
            vol.x = 0f;
            vol.z = 0f;

            var em = _ps.emission;
            em.enabled = false;

            var tsa = _ps.textureSheetAnimation;
            tsa.enabled = true;
            tsa.numTilesX = 8;
            tsa.numTilesY = 8;
            tsa.mode = ParticleSystemAnimationMode.Grid;
            tsa.cycleCount = 1;
            tsa.frameOverTime = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0f, 1f, 1f));
            // random phase seems to look nice, though the game doesn't do this
            tsa.startFrame = new ParticleSystem.MinMaxCurve(0f, 1f);

            var rend = GetComponent<ParticleSystemRenderer>();
            _renderer = rend;

            // nothing really works perfectly here, but YoungestInFront is pretty good, as you generally want newer
            // particles to be more visible than older ones. When looking at a thick smoke trail from the back it
            // can look a bit weird, but sorting by distance is worse as it can make particles pop through each 
            // other over time, which looks very unnatural.
            rend.sortMode = ParticleSystemSortMode.YoungestInFront;
            rend.SetActiveVertexStreams(new List<ParticleSystemVertexStream>
            {
                ParticleSystemVertexStream.Position,
                ParticleSystemVertexStream.Color,
                ParticleSystemVertexStream.UV,
                ParticleSystemVertexStream.SizeX,
                ParticleSystemVertexStream.AgePercent,
                ParticleSystemVertexStream.InvStartLifetime,
            });
            if (shader != null)
            {
                rend.material.shader = shader;
                rend.material.mainTexture = atlas;
                SetLightSaturation(s.lightSaturation);
                SetMaxShadowFloor(s.maxShadowFloor);
                rend.material.SetFloat(MinFadeDist, FadeDistMin);
                rend.material.SetFloat(MaxFadeDist, FadeDistMax);
                SetDensityScale(ComputeDensityScale());
                ApplyDensityFalloff();
                ApplySoftParticlesFade();
                ApplyFadeInSeconds();
            }
        }

        // upper bound of per-particle density, so the encoded value stays in [0,1]
        private float ComputeDensityScale()
        {
            var minRate = Mathf.Min(tuning.idleEmissionRate, tuning.fullEmissionRate);
            return Mathf.Max(1e-4f, Model.MaxParticulateMass / Mathf.Max(1f, minRate));
        }

        public void SetLightSaturation(float value)
        {
            tuning.lightSaturation = value;
            if (_renderer != null) _renderer.material.SetFloat(LightSaturation, value);
        }

        public void SetMaxShadowFloor(float value)
        {
            tuning.maxShadowFloor = value;
            if (_renderer != null) _renderer.material.SetFloat(MaxShadowFloor, value);
        }

        public void SetDensityScale(float value)
        {
            _densityScale = value;
            if (_renderer != null) _renderer.material.SetFloat(DensityScale, value);
        }

        public void ApplyDensityFalloff()
        {
            if (_renderer != null) _renderer.material.SetFloat(DensityFalloffId, DensityFalloff);
        }

        public void ApplySoftParticlesFade()
        {
            if (_renderer != null) _renderer.material.SetFloat(SoftParticlesFadeId, SoftParticlesFade);
        }

        public void ApplyFadeInSeconds()
        {
            if (_renderer != null) _renderer.material.SetFloat(FadeInSecondsId, FadeInSeconds);
        }

        private void Update()
        {
            var dt = Time.deltaTime;

            Model.Update(lambda, rpmNorm, heat, engineOn, dt, vapour);

            var s = tuning;

            // smoke dispersion and turbulence scales with this
            var speedNorm = Mathf.Clamp01(absSpeed / s.speedNormMax);

            var noise = _ps.noise;
            noise.strength = s.turbulenceStrength * speedNorm;

            var emissionRate = Mathf.Lerp(s.idleEmissionRate, s.fullEmissionRate, heat);
            if (engineOn)
            {
                _emitAccumulator += emissionRate * dt;
            }

            var n = (int)_emitAccumulator;
            if (n > 0)
            {
                _emitAccumulator -= n;

                // just a safety to avoid runaway particle counts if there's a long lag spike
                n = Mathf.Min(n, 30);

                var upSpeed = velocity.Calculate(massFlow, gasDensity);

                // custom emit requires us to apply the simulation space manually
                var simPos = ParticleSimSpace.Position(customSimulationSpace, transform.position);

                var baseEmitVelocity = transform.forward * upSpeed;
                var jitterVelocity = (speedNorm * s.speedJitter + 0.15f);

                var lifetime = s.lifetime * Mathf.Lerp(1f, s.speedLifetimeScale, speedNorm);

                var spawnColor = EncodeDensityColor(Model.Color, emissionRate);

                for (var i = 0; i < n; i++)
                {
                    var emitVelocity = baseEmitVelocity * Random.Range(0.85f, 1.15f);
                    var worldVelocity = locoVelocity + emitVelocity + Random.insideUnitSphere * jitterVelocity;

                    var simSpaceVelocity = ParticleSimSpace.Direction(customSimulationSpace, worldVelocity);

                    var baseSize = Mathf.Max(0.01f, s.startSize);
                    var spread = 1f + Mathf.Max(0f, s.startSizeVariance);

                    var ep = new ParticleSystem.EmitParams
                    {
                        // unity will perform one velocity integration step before drawing the particle,
                        // offsetting its spawn point at high speeds and/or low frame rates: back-date it
                        position = simPos - simSpaceVelocity * dt,
                        velocity = simSpaceVelocity,
                        startSize = Random.Range(baseSize / spread, baseSize * spread),
                        startColor = spawnColor,
                        startLifetime = lifetime * Random.Range(0.9f, 1.1f),
                        // random orientation + slow spin gives the appearance of a turbulent smoke column
                        rotation = Random.Range(0f, 360f),
                        angularVelocity = Random.Range(-s.angularVelocityMax, s.angularVelocityMax),
                    };
                    _ps.Emit(ep, 1);
                }
            }
        }

        // encode per-particle density into the alpha channel of the given color
        private Color EncodeDensityColor(Color modelColor, float emissionRate)
        {
            // all this tomfoolery really does is encode the current per-particle particulate mass into an 8-bit wide
            // channel. 8 bits isn't much, so let's make sure that every bit represents a quantity that is attainable
            // for our current particle emitter. we also apply a gamma curve to shift some more detail into the
            // low-density range, where subtle variations matter more.
            var density = emissionRate > 0f
                ? Mathf.Clamp01(Model.ParticulateMass / emissionRate / _densityScale)
                : 0f;
            var q = Mathf.Pow(density, DensityEncodeExponent);
            return new Color(modelColor.r, modelColor.g, modelColor.b, q);
        }
    }
}
