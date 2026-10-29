using System.Collections.Generic;

using TurboTurbo.Modeling;

using UnityEngine;

namespace TurboTurbo.Effects
{
    [RequireComponent(typeof(ParticleSystem))]
    public class SmokeParticles : MonoBehaviour
    {
        private static readonly int LightSaturation = Shader.PropertyToID("_Saturation");
        private static readonly int MaxShadowFloor = Shader.PropertyToID("_MaxShadowFloor");
        private static readonly int TexKneeId = Shader.PropertyToID("_TexKnee");
        private static readonly int TexShoulderId = Shader.PropertyToID("_TexShoulder");
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

        /// <summary>
        /// Distance over which smoke fades out as it approaches opaque geometry, as a fraction of particle size.
        /// 0 disables.
        /// </summary>
        public static float SoftParticlesFade = 0.3f;

        /// <summary>Absolute time [s] for a new smoke particle to reach full opacity.</summary>
        public static float FadeInSeconds = 0.07f;

        public const float DefaultLifetime = 4.5f;

        /// <summary>
        /// Nominal smoke particle lifetime [s] in still air.
        /// </summary>
        public static float Lifetime = DefaultLifetime;

        public const float DefaultIdleEmissionRate = 15f;
        public const float DefaultFullEmissionRate = 75f;

        /// <summary>Emission rate [p/s] at zero exhaust energy, applied to every emitter.</summary>
        public static float IdleEmissionRate = DefaultIdleEmissionRate;

        /// <summary>Emission rate [p/s] at full exhaust energy, applied to every emitter.</summary>
        public static float FullEmissionRate = DefaultFullEmissionRate;

        /// <summary>Soft shoulder applied to the atlas before the shadow ramp. Smaller = stronger lift.</summary>
        public static float TexKnee = 0.15f;

        /// <summary>Blends the atlas soft shoulder in; false restores the raw atlas.</summary>
        public static bool TexShoulder = true;

        // near-camera fade range, in metres
        private const float FadeDistMin = 1.5f;
        private const float FadeDistMax = 2.5f;

        // texture sheet animation rate
        private const float AnimationFps = 30f;

        // DV's steam effects use these sorting parameters, so we should adopt them as well
        // to ensure our smoke gets properly sorted by distance against steam.
        private const int SortingRenderQueue = 3000;
        private const int SortingOrder = 5;
        private const float SortingFudge = 10f;

        // relative spread of particle lifetimes around the nominal lifetime
        private const float LifetimeJitter = 0.1f;

        // particle age [s] at which a nominal particle reaches the plume size.
        // the exponent shapes growth around it
        private const float GrowthReferenceAge = 5f;

        public sealed class Settings
        {
            internal const float DefaultStartSize = 0.39f;
            internal const float DefaultStartSizeVariance = 0.29f;
            internal const float DefaultPlumeSize = 6.4f;
            internal const float DefaultGrowthExponent = 0.65f;
            internal const float DefaultBuoyancy = 0.1f;
            internal const float DefaultDrag = 0.6f;
            internal const float DefaultAngularVelocityMax = 20f;
            internal const float DefaultSpeedNormMax = 15f;
            internal const float DefaultSpeedGrowthScale = 2.2f;
            internal const float DefaultSpeedJitter = 0.5f;
            internal const float DefaultTurbulenceStrengthMin = 0.125f;
            internal const float DefaultTurbulenceStrengthMax = 1.25f;
            internal const float DefaultTurbulenceFrequency = 0.5f;
            internal const float DefaultTurbulenceScrollSpeed = 0f;
            internal const float DefaultLightSaturation = 0.35f;
            internal const float DefaultMaxShadowFloor = 0.65f;

            public float startSize = DefaultStartSize;

            public float startSizeVariance = DefaultStartSizeVariance;

            public float plumeSize = DefaultPlumeSize;

            public float growthExponent = DefaultGrowthExponent;

            public float buoyancy = DefaultBuoyancy;

            public float drag = DefaultDrag;

            public float angularVelocityMax = DefaultAngularVelocityMax;

            public float speedNormMax = DefaultSpeedNormMax;

            public float speedGrowthScale = DefaultSpeedGrowthScale;

            public float speedJitter = DefaultSpeedJitter;

            public float turbulenceStrengthMin = DefaultTurbulenceStrengthMin;

            public float turbulenceStrengthMax = DefaultTurbulenceStrengthMax;

            public float turbulenceFrequency = DefaultTurbulenceFrequency;

            public float turbulenceScrollSpeed = DefaultTurbulenceScrollSpeed;

            public float lightSaturation = DefaultLightSaturation;

            public float maxShadowFloor = DefaultMaxShadowFloor;

            public Settings()
            {
            }

            public Settings(Settings other)
            {
                startSize = other.startSize;
                startSizeVariance = other.startSizeVariance;
                plumeSize = other.plumeSize;
                growthExponent = other.growthExponent;
                buoyancy = other.buoyancy;
                drag = other.drag;
                angularVelocityMax = other.angularVelocityMax;
                speedNormMax = other.speedNormMax;
                speedGrowthScale = other.speedGrowthScale;
                speedJitter = other.speedJitter;
                turbulenceStrengthMin = other.turbulenceStrengthMin;
                turbulenceStrengthMax = other.turbulenceStrengthMax;
                turbulenceFrequency = other.turbulenceFrequency;
                turbulenceScrollSpeed = other.turbulenceScrollSpeed;
                lightSaturation = other.lightSaturation;
                maxShadowFloor = other.maxShadowFloor;
            }
        }

        [Header("Smoke model inputs")]
        public float lambda = 1.2f;
        [Range(0f, 1f)] public float rpmNorm = 0.5f;
        [Range(0f, 1f)] public float exhaustEnergy;

        /// <summary>Normalized stack fuel-vapour driver from the stack model.</summary>
        [Range(0f, 1f)] public float vapour;

        /// <summary>Exhaust mass-flow proxy, used by the coefficient velocity model.</summary>
        public float massFlow;

        /// <summary>Exhaust gas density [kg/m^3], used by the coefficient velocity model.</summary>
        public float gasDensity;

        public bool engineOn = true;

        public Vector3 locoVelocity;

        /// <summary>Per-engine emission and appearance tuning, cloned at bind.</summary>
        public Settings tuning = new Settings();

        /// <summary>Exhaust flow range shared by this host's smoke and shimmer emitters.</summary>
        public ExhaustVelocitySettings velocity = new ExhaustVelocitySettings();

        public Shader shader;
        public Texture atlas;

        /// <summary>Simulation space drifting with the wind. Required.</summary>
        public WindFrame windFrame;

        private ParticleSystem _ps;
        private ParticleSystemRenderer _renderer;
        private float _emitAccumulator;
        private readonly GrowthCurve _sizeCurve = new GrowthCurve(GrowthReferenceAge);
        private float _densityScale = 1f;

        public int ParticleCount => _ps.particleCount;

        /// <summary>The appearance model that turns the engine signals into smoke colour and opacity.</summary>
        internal ExhaustSmokeModel Model { get; } = new ExhaustSmokeModel();

        private void OnValidate()
        {
            if (Application.isPlaying && windFrame != null) Configure();
        }

        /// <summary>Builds the ParticleSystem layout. Call once after the
        /// required properties (shader, atlas, windFrame) are
        /// assigned; call again after edits. Throws if windFrame is missing.</summary>
        public void Configure()
        {
            if (_ps == null) _ps = GetComponent<ParticleSystem>();

            if (shader == null || atlas == null)
            {
                Debug.LogWarning("[SmokeParticles] missing shader or atlas, smoke will not render");
            }

            if (windFrame == null)
            {
                throw new System.InvalidOperationException("[SmokeParticles] windFrame must be assigned before Configure");
            }
            windFrame.Bind(_ps);

            var s = tuning;

            // some properties are deliberately not set here; we only emit particles manually
            var main = _ps.main;

            main.maxParticles = ParticleBudget.Capacity(
                Mathf.Max(IdleEmissionRate, FullEmissionRate),
                Lifetime * (1f + LifetimeJitter));
            main.gravityModifier = 0f;

            _sizeCurve.SetExponent(s.growthExponent);
            // ensures that the size curve instantly recalculates to the new exponent,
            // instead of smoothly stepping towards it
            _sizeCurve.Reset();

            var sol = _ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, _sizeCurve.Curve);

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

            // we use the noise module to inject random variation into particle velocity.
            // strength of the noise is governed by train speed
            var noise = _ps.noise;
            noise.enabled = true;
            noise.quality = ParticleSystemNoiseQuality.High;
            noise.frequency = s.turbulenceFrequency;
            noise.strength = s.turbulenceStrengthMin;
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
            tsa.timeMode = ParticleSystemAnimationTimeMode.FPS;
            tsa.fps = AnimationFps;
            // random phase seems to look nice, though the game doesn't do this
            tsa.startFrame = new ParticleSystem.MinMaxCurve(0f, 1f);

            var rend = GetComponent<ParticleSystemRenderer>();
            _renderer = rend;

            // nothing really works perfectly here, but YoungestInFront is pretty good, as you generally want newer
            // particles to be more visible than older ones. When looking at a thick smoke trail from the back it
            // can look a bit weird, but sorting by distance is worse as it can make particles pop through each 
            // other over time, which looks very unnatural.
            rend.sortMode = ParticleSystemSortMode.YoungestInFront;
            rend.sortingOrder = SortingOrder;
            rend.sortingFudge = SortingFudge;
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
                rend.material.renderQueue = SortingRenderQueue;
                SetLightSaturation(s.lightSaturation);
                SetMaxShadowFloor(s.maxShadowFloor);
                rend.material.SetFloat(MinFadeDist, FadeDistMin);
                rend.material.SetFloat(MaxFadeDist, FadeDistMax);
                SetDensityScale(ComputeDensityScale());
                ApplyDensityFalloff();
                ApplySoftParticlesFade();
                ApplyFadeInSeconds();
                ApplyTexKnee();
            }
        }

        // upper bound of per-particle density, so the encoded value stays in [0,1]
        private float ComputeDensityScale()
        {
            var minRate = Mathf.Min(IdleEmissionRate, FullEmissionRate);
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

        public void ApplyTexKnee()
        {
            if (_renderer == null) return;
            _renderer.material.SetFloat(TexKneeId, TexKnee);
            _renderer.material.SetFloat(TexShoulderId, TexShoulder ? 1f : 0f);
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

        private void OnDestroy()
        {
            if (windFrame != null) windFrame.Unbind(_ps);
        }

        private void OnEnable() => Camera.onPreCull += SortAtEmitter;

        private void OnDisable() => Camera.onPreCull -= SortAtEmitter;

        // Unity sorts the renderer by its bounds centre, which trails far behind the exhaust once the plume is
        // long. offset the fudge so the plume sorts as if it were at the exhaust instead.
        private void SortAtEmitter(Camera cam)
        {
            if (_renderer == null) return;
            var eye = cam.transform.position;
            var emitterDistance = Vector3.Distance(eye, transform.position);
            var boundsDistance = Vector3.Distance(eye, _renderer.bounds.center);
            _renderer.sortingFudge = SortingFudge + emitterDistance - boundsDistance;
        }

        private void Update()
        {
            var dt = Time.deltaTime;

            Model.Update(lambda, rpmNorm, exhaustEnergy, engineOn, dt, vapour);

            var s = tuning;

            // smoke dispersion and turbulence scale with airspeed
            var speedNorm = Mathf.Clamp01(windFrame.AirVelocity(locoVelocity).magnitude / s.speedNormMax);
            UpdateGrowth(speedNorm, dt);

            var noise = _ps.noise;
            noise.strength = Mathf.Lerp(s.turbulenceStrengthMin, s.turbulenceStrengthMax, speedNorm);

            var emissionRate = Mathf.Lerp(IdleEmissionRate, FullEmissionRate, exhaustEnergy);
            if (engineOn)
            {
                _emitAccumulator += emissionRate * dt;
            }

            var n = (int)_emitAccumulator;
            if (n > 0)
            {
                _emitAccumulator -= n;

                n = Mathf.Min(n, ParticleBudget.MaxBurst);

                var upSpeed = velocity.Calculate(massFlow, gasDensity);

                // custom emit requires us to apply the simulation space manually
                var simPos = windFrame.ToSimPosition(transform.position);

                var baseEmitVelocity = transform.forward * upSpeed;
                var jitterVelocity = (speedNorm * s.speedJitter + 0.15f);

                var spawnColor = EncodeDensityColor(Model.Color, emissionRate);

                for (var i = 0; i < n; i++)
                {
                    var emitVelocity = baseEmitVelocity * Random.Range(0.85f, 1.15f);
                    var worldVelocity = locoVelocity + emitVelocity + Random.insideUnitSphere * jitterVelocity;

                    // the simulation space drifts with the air, so particle velocities are relative to the air
                    var simSpaceVelocity = windFrame.AirVelocity(worldVelocity);

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
                        startLifetime = Lifetime * Random.Range(1f - LifetimeJitter, 1f + LifetimeJitter),
                        // random orientation + slow spin gives the appearance of a turbulent smoke column
                        rotation = Random.Range(0f, 360f),
                        angularVelocity = Random.Range(-s.angularVelocityMax, s.angularVelocityMax),
                    };
                    _ps.Emit(ep, 1);
                }
            }
        }

        /// <summary>
        /// Calculates the growth increase a particle experiences, relative to its start size, at the reference age,
        /// based on the loco's normalised speed relative to the wind frame. A value of 1 means the particle grows by
        /// 100% at the reference age.
        /// </summary>
        private float RelativeGrowth(float speedNorm)
        {
            var s = tuning;
            var p = _sizeCurve.Exponent;
            var growthTime = Mathf.Pow(Mathf.Max(0.01f, s.speedGrowthScale), -1f / p);
            var w = Mathf.Pow(Mathf.Lerp(1f, growthTime, speedNorm), -p);
            return Mathf.Max(0f, s.plumeSize / Mathf.Max(0.01f, s.startSize) - 1f) * w;
        }

        /// <summary>
        /// Updates the growth curve based on the loco's normalised speed, relative to the wind frame.
        /// </summary>
        private void UpdateGrowth(float speedNorm, float dt)
        {
            var growth = RelativeGrowth(speedNorm);
            if (!_sizeCurve.SetGrowth(growth, Lifetime, dt)) return;

            var sol = _ps.sizeOverLifetime;
            sol.size = new ParticleSystem.MinMaxCurve(1f, _sizeCurve.Curve);
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
