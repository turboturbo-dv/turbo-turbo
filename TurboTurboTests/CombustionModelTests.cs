using System;

using Shouldly;

using TurboTurbo.Modeling;

using Xunit;

namespace TurboTurboTests
{
    public class CombustionModelTests
    {
        private readonly CombustionModel.Settings _settings = new CombustionModel.Settings();
        private readonly TurboCharger.Settings _chargerSettings = new TurboCharger.Settings();
        private float _throttle;
        private float _fuelNorm;
        private float _rpmNorm = 1f;

        private CombustionModel CreateModel()
        {
            return new CombustionModel(_settings, () => _throttle, () => _fuelNorm, () => _rpmNorm, new TurboCharger(_chargerSettings));
        }

        [Fact]
        public void Settings_CopyConstructor_IsIndependent()
        {
            var template = new TurboCharger.Settings();
            var clone = new TurboCharger.Settings(template);

            clone.LambdaCalibration.ShouldBe(template.LambdaCalibration);
            clone.LambdaCalibration = 0.5f;
            template.LambdaCalibration.ShouldNotBe(0.5f);
        }

        // ------------------------------------------------------------
        // charge
        // ------------------------------------------------------------

        [Fact]
        public void Charge_AtZeroBoost_IsUnity()
        {
            var model = CreateModel();
            _throttle = 0.4f;
            _fuelNorm = 0.4f;
            _rpmNorm = 1f;
            model.Tick(0.016f, engineOn: true);

            // Charge = 1 + k x boost -> at boost 0 this is 1.0
            model.Charge.ShouldBe(1f, tolerance: 0.001f);
        }

        [Fact]
        public void Charge_AtFullBoost_Is_1_Plus_BoostChargeMultiplier()
        {
            var model = CreateModel();

            // hold full load long enough for boost to reach equilibrium
            _throttle = 1f;
            _fuelNorm = 1f;
            _rpmNorm = 1f;
            for (var i = 0; i < 600; i++)
            {
                model.Tick(0.1f, engineOn: true);
            }

            model.Boost.ShouldBe(1f, tolerance: 0.01f);
            model.Charge.ShouldBe(1f + _chargerSettings.BoostChargeMultiplier, tolerance: 0.01f);
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
            _fuelNorm = 0.4f;
            _rpmNorm = 1f;
            model.Tick(1f, engineOn: true);

            model.Boost.ShouldBe(0.4f * (1f - (float)Math.Exp(-1f / 3f)), tolerance: 0.001f);
        }

        [Fact]
        public void Boost_ThermalFeedback_ShortensSpool_UnderOverfuel()
        {
            var model = CreateModel();

            // full rack from cold: overfuel = 1 - charge/1.74 = 0.425, so
            // tau = TauUp / (1 + ThermalK x Overfuel) = 3 / 1.340 = 2.238:
            // boost = 1 x (1 - e^(-1/2.238)) = 0.3603
            _throttle = 1f;
            _fuelNorm = 1f;
            _rpmNorm = 1f;
            model.Tick(1f, engineOn: true);

            model.Boost.ShouldBe(1f - (float)Math.Exp(-1f / 2.238f), tolerance: 0.001f);
        }

        [Fact]
        public void Boost_Rises_Monotonically_UnderSustainedFullLoad()
        {
            var model = CreateModel();
            _throttle = 1f;
            _fuelNorm = 1f;
            _rpmNorm = 1f;

            var last = 0f;
            for (var i = 0; i < 50; i++)
            {
                model.Tick(0.1f, engineOn: true);
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
            _fuelNorm = 1f;
            _rpmNorm = 1f;
            for (var i = 0; i < 600; i++)
            {
                model.Tick(0.1f, engineOn: true);
            }
            var peak = model.Boost;

            // cut the throttle: boost must bleed off through TauDown
            _throttle = 0f;
            _fuelNorm = 0f;
            for (var i = 0; i < 5; i++)
            {
                model.Tick(0.5f, engineOn: true);
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

            // boost 0 -> charge 1; cap = 1 / (calibration x floor) = 1 / 1.218 = 0.821
            _throttle = 0.9f;
            _fuelNorm = 0.9f;
            _rpmNorm = 1f;
            model.Tick(0.016f, engineOn: true);

            model.EffectiveDemand.ShouldBe(0.821f, tolerance: 0.01f);
            model.EffectiveDemand.ShouldBeLessThan(0.9f);
        }

        [Fact]
        public void TorqueCap_DoesNotLimit_LowThrottle()
        {
            var model = CreateModel();

            _throttle = 0.3f;
            _fuelNorm = 0.3f;
            _rpmNorm = 1f;
            model.Tick(0.016f, engineOn: true);

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
                _fuelNorm = throttle;
                model.Tick(0.016f, engineOn: true);
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

            // combustion follows measured fuel; a stopped engine reads no fuel
            _fuelNorm = 0f;
            model.Tick(0.016f, engineOn: false);

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
            _fuelNorm = 1f;
            _rpmNorm = 1f;
            for (var i = 0; i < 5; i++)
            {
                model.Tick(1f, engineOn: true);
            }
            model.Boost.ShouldBeGreaterThan(0.75f);

            // slam to 0.69 in one 16ms frame: rate = 0.31 / 0.016 = 19.4/s
            // beats the 15/s threshold, and the 16ms decay leaves boost above
            // the 0.75 gate.
            _throttle = 0.69f;
            _fuelNorm = 0.69f;
            model.Tick(0.016f, engineOn: true);
            model.SurgeThisTick.ShouldBeTrue();
        }

        [Fact]
        public void NoSurge_WhenDemandDropIsGradual()
        {
            var model = CreateModel();

            _throttle = 1f;
            _fuelNorm = 1f;
            _rpmNorm = 1f;
            for (var i = 0; i < 5; i++)
            {
                model.Tick(1f, engineOn: true);
            }

            // ease down to 0.69 over 20 frames: per-frame rate ~1/s, far
            // below the threshold even though the total drop matches
            for (var i = 0; i < 20; i++)
            {
                _throttle = 1f - 0.31f * (i + 1) / 20f;
                _fuelNorm = _throttle;
                model.Tick(0.016f, engineOn: true);
                model.SurgeThisTick.ShouldBeFalse();
            }
        }

        [Fact]
        public void NoSurge_WhenDemandIsSteady()
        {
            var model = CreateModel();

            _throttle = 1f;
            _fuelNorm = 1f;
            _rpmNorm = 1f;
            for (var i = 0; i < 5; i++)
            {
                model.Tick(1f, engineOn: true);
            }

            model.SurgeThisTick.ShouldBeFalse();
        }
    }
}