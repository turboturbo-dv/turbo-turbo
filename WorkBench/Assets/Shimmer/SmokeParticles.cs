using System.ComponentModel;
using System.Xml.Serialization;

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

        [XmlType("SmokeEmitterSettings")]
        public sealed class Settings
        {
            internal const float DefaultIdleEmissionRate = 15f;
            internal const float DefaultFullEmissionRate = 75f;
            internal const float DefaultLifetime = 3f;
            internal const float DefaultStartSizeMin = 0.3f;
            internal const float DefaultStartSizeMax = 0.5f;
            internal const float DefaultSizeOverLifetimeStart = 1f;
            internal const float DefaultSizeOverLifetimeEnd = 9.5f;
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
            internal const float DefaultMinFadeDist = 1f;
            internal const float DefaultMaxFadeDist = 2f;

            [DefaultValue(DefaultIdleEmissionRate)]
            public float idleEmissionRate = DefaultIdleEmissionRate;

            [DefaultValue(DefaultFullEmissionRate)]
            public float fullEmissionRate = DefaultFullEmissionRate;

            [DefaultValue(DefaultLifetime)]
            public float lifetime = DefaultLifetime;

            [DefaultValue(DefaultStartSizeMin)]
            public float startSizeMin = DefaultStartSizeMin;

            [DefaultValue(DefaultStartSizeMax)]
            public float startSizeMax = DefaultStartSizeMax;

            [DefaultValue(DefaultSizeOverLifetimeStart)]
            public float sizeOverLifetimeStart = DefaultSizeOverLifetimeStart;

            [DefaultValue(DefaultSizeOverLifetimeEnd)]
            public float sizeOverLifetimeEnd = DefaultSizeOverLifetimeEnd;

            [DefaultValue(DefaultBuoyancy)]
            public float buoyancy = DefaultBuoyancy;

            [DefaultValue(DefaultDrag)]
            public float drag = DefaultDrag;

            [DefaultValue(DefaultAngularVelocityMax)]
            public float angularVelocityMax = DefaultAngularVelocityMax;

            [DefaultValue(DefaultSpeedNormMax)]
            public float speedNormMax = DefaultSpeedNormMax;

            [DefaultValue(DefaultSpeedLifetimeScale)]
            public float speedLifetimeScale = DefaultSpeedLifetimeScale;

            [DefaultValue(DefaultSpeedJitter)]
            public float speedJitter = DefaultSpeedJitter;

            [DefaultValue(DefaultTurbulenceStrength)]
            public float turbulenceStrength = DefaultTurbulenceStrength;

            [DefaultValue(DefaultTurbulenceFrequency)]
            public float turbulenceFrequency = DefaultTurbulenceFrequency;

            [DefaultValue(DefaultTurbulenceScrollSpeed)]
            public float turbulenceScrollSpeed = DefaultTurbulenceScrollSpeed;

            [DefaultValue(DefaultLightSaturation)]
            public float lightSaturation = DefaultLightSaturation;

            [DefaultValue(DefaultMaxShadowFloor)]
            public float maxShadowFloor = DefaultMaxShadowFloor;

            [DefaultValue(DefaultMinFadeDist)]
            public float minFadeDist = DefaultMinFadeDist;

            [DefaultValue(DefaultMaxFadeDist)]
            public float maxFadeDist = DefaultMaxFadeDist;

            public Settings()
            {
            }

            public Settings(Settings other)
            {
                idleEmissionRate = other.idleEmissionRate;
                fullEmissionRate = other.fullEmissionRate;
                lifetime = other.lifetime;
                startSizeMin = other.startSizeMin;
                startSizeMax = other.startSizeMax;
                sizeOverLifetimeStart = other.sizeOverLifetimeStart;
                sizeOverLifetimeEnd = other.sizeOverLifetimeEnd;
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
                minFadeDist = other.minFadeDist;
                maxFadeDist = other.maxFadeDist;
            }
        }

        [Header("Smoke model inputs")]
        public float lambda = 1.2f;
        [Range(0f, 1f)] public float rpmNorm = 0.5f;
        [Range(0f, 1f)] public float heat;

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
        private readonly ExhaustSmokeModel _model = new ExhaustSmokeModel();
        private float _emitAccumulator;
        private AnimationCurve _sizeCurve;

        public int ParticleCount => _ps.particleCount;

        /// <summary>The internal appearance model (dev panel edits its settings).</summary>
        internal ExhaustSmokeModel Model => _model;

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
            _sizeCurve = AnimationCurve.Linear(0f, s.sizeOverLifetimeStart, 1f, s.sizeOverLifetimeEnd);

            var sol = _ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, _sizeCurve);

            var fade = new Gradient();
            fade.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(1f, 0.025f),
                    new GradientAlphaKey(1f, 0.4f),
                    new GradientAlphaKey(0f, 1f),
                });
            var col = _ps.colorOverLifetime;
            col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(fade);

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
            if (shader != null)
            {
                rend.material.shader = shader;
                rend.material.mainTexture = atlas;
                SetLightSaturation(s.lightSaturation);
                SetMaxShadowFloor(s.maxShadowFloor);
                SetMinFadeDist(s.minFadeDist);
                SetMaxFadeDist(s.maxFadeDist);
            }
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

        public void SetMinFadeDist(float value)
        {
            tuning.minFadeDist = value;
            if (_renderer != null) _renderer.material.SetFloat(MinFadeDist, value);
        }

        public void SetMaxFadeDist(float value)
        {
            tuning.maxFadeDist = value;
            if (_renderer != null) _renderer.material.SetFloat(MaxFadeDist, value);
        }

        private void Update()
        {
            var dt = Time.deltaTime;

            _model.Update(lambda, rpmNorm, heat, engineOn, dt);

            var s = tuning;

            // smoke dispersion and turbulence scales with this
            var speedNorm = Mathf.Clamp01(absSpeed / s.speedNormMax);

            var noise = _ps.noise;
            noise.strength = s.turbulenceStrength * speedNorm;

            if (engineOn)
            {
                _emitAccumulator += Mathf.Lerp(s.idleEmissionRate, s.fullEmissionRate, heat) * dt;
            }

            var n = (int)_emitAccumulator;
            if (n > 0)
            {
                _emitAccumulator -= n;

                // just a safety to avoid runaway particle counts if there's a long lag spike
                n = Mathf.Min(n, 30);

                var upSpeed = Mathf.Lerp(velocity.Idle, velocity.FullLoad, Mathf.Clamp01(heat));

                // custom emit requires us to apply the simulation space manually
                var simPos = ParticleSimSpace.Position(customSimulationSpace, transform.position);

                var baseEmitVelocity = transform.forward * upSpeed;
                var jitterVelocity = (speedNorm * s.speedJitter + 0.15f);

                var lifetime = s.lifetime * Mathf.Lerp(1f, s.speedLifetimeScale, speedNorm);

                for (var i = 0; i < n; i++)
                {
                    var emitVelocity = baseEmitVelocity * Random.Range(0.85f, 1.15f);
                    var worldVelocity = locoVelocity + emitVelocity + Random.insideUnitSphere * jitterVelocity;

                    var simSpaceVelocity = ParticleSimSpace.Direction(customSimulationSpace, worldVelocity);

                    var ep = new ParticleSystem.EmitParams
                    {
                        // unity will perform one velocity integration step before drawing the particle,
                        // offsetting its spawn point at high speeds and/or low frame rates: back-date it
                        position = simPos - simSpaceVelocity * dt,
                        velocity = simSpaceVelocity,
                        startSize = Random.Range(s.startSizeMin, s.startSizeMax),
                        startColor = _model.Color,
                        startLifetime = lifetime * Random.Range(0.9f, 1.1f),
                        // random orientation + slow spin gives the appearance of a turbulent smoke column
                        rotation = Random.Range(0f, 360f),
                        angularVelocity = Random.Range(-s.angularVelocityMax, s.angularVelocityMax),
                    };
                    _ps.Emit(ep, 1);
                }
            }
        }
    }
}