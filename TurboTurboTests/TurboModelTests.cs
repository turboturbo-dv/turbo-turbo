using System;

using Shouldly;

using TurboTurbo.Modeling;

using Xunit;

namespace TurboTurboTests
{
    /// <summary>
    /// Tests for the pure-math TurboModel: charge ladder, lambda response,
    /// torque cap, boost lag dynamics and surge detection. Smoke appearance
    /// lives in ExhaustSmokeModel and is tested separately.
    ///
    /// The model's inputs (throttle, rpm) are supplier functions, so every
    /// test scripts them through mutable fields captured by the suppliers.
    /// </summary>
    public class TurboModelTests
    {
        private readonly TurboModel.Settings _settings = new TurboModel.Settings();
        private float _throttle;
        private float _rpmNorm = 1f;

        private TurboModel CreateModel()
        {
            return new TurboModel(_settings, () => _throttle, () => _rpmNorm);
        }

        // ------------------------------------------------------------
        // charge
        // ------------------------------------------------------------

        [Fact]
        public void Charge_AtZeroBoost_IsUnity()
        {
            var model = CreateModel();
            _throttle = 0.4f;
            _rpmNorm = 1f;
            model.Tick(0.016f, fuelNorm: 0.4f, engineOn: true);

            // Charge = NA + (1-NA) x (1 + k x boost) -> at boost 0 this is 1.0
            model.Charge.ShouldBe(1f, tolerance: 0.001f);
        }

        [Fact]
        public void Charge_AtFullBoost_Is_2_125_WithDefaultSettings()
        {
            var model = CreateModel();

            // hold full load long enough for boost to reach equilibrium
            _throttle = 1f;
            _rpmNorm = 1f;
            for (var i = 0; i < 600; i++)
            {
                model.Tick(0.1f, fuelNorm: 1f, engineOn: true);
            }

            model.Boost.ShouldBe(1f, tolerance: 0.01f);
            // NA 0.55 + (1-NA) x (1 + 2.5 x 1) = 0.55 + 1.575 = 2.125
            model.Charge.ShouldBe(2.125f, tolerance: 0.01f);
        }

        // ------------------------------------------------------------
        // boost dynamics
        // ------------------------------------------------------------

        [Fact]
        public void Boost_FirstTick_MatchesFirstOrderLagWithTauUp()
        {
            var model = CreateModel();

            // clean partial load: demand 0.4 = charge/calibration -> overfuel 0:
            // target 0.4, tau = TauUp = 3, delta 1s:
            // boost = 0.4 x (1 - e^(-1/3)) = 0.1134
            _throttle = 0.4f;
            _rpmNorm = 1f;
            model.Tick(1f, fuelNorm: 0.4f, engineOn: true);

            model.Boost.ShouldBe(0.4f * (1f - (float)Math.Exp(-1f / 3f)), tolerance: 0.001f);
        }

        [Fact]
        public void Boost_ThermalFeedback_ShortensSpool_UnderOverfuel()
        {
            var model = CreateModel();

            // full rack from cold: overfuel = 1 - charge/2.5 = 0.6, so
            // tau = TauUp / (1 + ThermalK x Overfuel) = 3 / 1.48 = 2.027:
            // boost = 1 x (1 - e^(-1/2.027)) = 0.3894
            _throttle = 1f;
            _rpmNorm = 1f;
            model.Tick(1f, fuelNorm: 1f, engineOn: true);

            model.Boost.ShouldBe(1f - (float)Math.Exp(-1f / 2.027f), tolerance: 0.001f);
        }

        [Fact]
        public void Boost_Rises_Monotonically_UnderSustainedFullLoad()
        {
            var model = CreateModel();
            _throttle = 1f;
            _rpmNorm = 1f;

            var last = 0f;
            for (var i = 0; i < 50; i++)
            {
                model.Tick(0.1f, fuelNorm: 1f, engineOn: true);
                model.Boost.ShouldBeGreaterThanOrEqualTo(last);
                last = model.Boost;
            }
        }

        [Fact]
        public void Boost_Decays_AfterDemandDrops()
        {
            var model = CreateModel();

            // spool up first
            _throttle = 1f;
            _rpmNorm = 1f;
            for (var i = 0; i < 600; i++)
            {
                model.Tick(0.1f, fuelNorm: 1f, engineOn: true);
            }
            var peak = model.Boost;

            // cut the throttle: boost must bleed off through TauDown
            _throttle = 0f;
            for (var i = 0; i < 5; i++)
            {
                model.Tick(0.5f, fuelNorm: 0f, engineOn: true);
                model.Boost.ShouldBeLessThan(peak);
            }
        }

        // ------------------------------------------------------------
        // torque cap
        // ------------------------------------------------------------

        [Fact]
        public void TorqueCap_LimitsEffectiveDemand_AtHighThrottle()
        {
            var model = CreateModel();

            // boost 0 -> charge 1; cap = 1 / (calibration x floor) = 1 / 1.75 = 0.571
            _throttle = 0.9f;
            _rpmNorm = 1f;
            model.Tick(0.016f, fuelNorm: 0.9f, engineOn: true);

            model.EffectiveDemand.ShouldBe(0.571f, tolerance: 0.01f);
            model.EffectiveDemand.ShouldBeLessThan(0.9f);
        }

        [Fact]
        public void TorqueCap_DoesNotLimit_LowThrottle()
        {
            var model = CreateModel();

            _throttle = 0.3f;
            _rpmNorm = 1f;
            model.Tick(0.016f, fuelNorm: 0.3f, engineOn: true);

            model.EffectiveDemand.ShouldBe(0.3f, tolerance: 0.001f);
        }

        [Fact]
        public void EffectiveDemand_NeverExceedsThrottle_AcrossSweep()
        {
            var model = CreateModel();
            _rpmNorm = 1f;

            for (var throttle = 0f; throttle <= 1f; throttle += 0.05f)
            {
                _throttle = throttle;
                model.Tick(0.016f, fuelNorm: throttle, engineOn: true);
                model.EffectiveDemand.ShouldBeLessThanOrEqualTo(throttle + 0.0001f);
            }
        }

        // ------------------------------------------------------------
        // engine off
        // ------------------------------------------------------------

        [Fact]
        public void EngineOff_ZeroesEffectiveDemand()
        {
            var model = CreateModel();
            _throttle = 0.9f;

            model.Tick(0.016f, fuelNorm: 0f, engineOn: false);

            model.EffectiveDemand.ShouldBe(0f, tolerance: 0.0001f);
        }

        // ------------------------------------------------------------
        // surge detection
        // ------------------------------------------------------------

        [Fact]
        public void Surge_Detected_OnSharpPartialDemandDropAtHighBoost()
        {
            var model = CreateModel();

            // spool boost above 0.75: 5 ticks of 1s at full load
            _throttle = 1f;
            _rpmNorm = 1f;
            for (var i = 0; i < 5; i++)
            {
                model.Tick(1f, fuelNorm: 1f, engineOn: true);
            }
            model.Boost.ShouldBeGreaterThan(0.75f);

            // partial slam to 0.69: drop = 0.31 > 0.3 triggers the surge, and
            // the post-decay boost (target 0.69) stays above the 0.75 gate.
            // A full slam to 0 decays boost to ~0.33 within the same tick, so
            // the post-decay boost fails the gate and no surge is reported.
            _throttle = 0.69f;
            model.Tick(1f, fuelNorm: 0.69f, engineOn: true);
            model.SurgeThisTick.ShouldBeTrue();
        }

        [Fact]
        public void NoSurge_WhenDemandIsSteady()
        {
            var model = CreateModel();

            _throttle = 1f;
            _rpmNorm = 1f;
            for (var i = 0; i < 5; i++)
            {
                model.Tick(1f, fuelNorm: 1f, engineOn: true);
            }

            model.SurgeThisTick.ShouldBeFalse();
        }
    }
}