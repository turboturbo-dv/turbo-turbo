using Shouldly;

using TurboTurbo;
using TurboTurbo.Modeling;

using Xunit;

namespace TurboTurboTests
{
    public class ChargerTests
    {
        [Fact]
        public void TurboCharger_DefaultLambdaCalibration_IsTurboValue()
        {
            new TurboCharger.Settings().LambdaCalibration.ShouldBeGreaterThan(1f);
        }

        [Fact]
        public void AtmosphericCharger_DefaultLambdaCalibration_IsAspiratedValue()
        {
            new AtmosphericCharger.Settings().LambdaCalibration.ShouldBeLessThan(1f);
        }

        [Fact]
        public void AtmosphericCharger_Charge_AtZeroRpm_IsEtaPeak()
        {
            var charger = new AtmosphericCharger(new AtmosphericCharger.Settings());
            charger.Tick(0.016f, 0f, 1f, 0f, engineOn: true);

            charger.Charge.ShouldBe(charger.Tuning.EtaPeak, tolerance: 0.0001f);
        }

        [Fact]
        public void AtmosphericCharger_Charge_Falls_WithRpm()
        {
            var charger = new AtmosphericCharger(new AtmosphericCharger.Settings());

            charger.Tick(0.016f, 0f, 1f, 0f, engineOn: true);
            var low = charger.Charge;
            charger.Tick(0.016f, 0.5f, 1f, 0f, engineOn: true);
            var mid = charger.Charge;
            charger.Tick(0.016f, 1f, 1f, 0f, engineOn: true);
            var high = charger.Charge;

            // defaults: 0.9, 0.9 x (1 - 0.25 x 0.25) = 0.84375, 0.9 x 0.75 = 0.675
            low.ShouldBe(0.9f, tolerance: 0.0001f);
            mid.ShouldBe(0.84375f, tolerance: 0.0001f);
            high.ShouldBe(0.675f, tolerance: 0.0001f);
        }

        [Fact]
        public void TurboCharger_RatedCharge_IsOnePlusMultiplier()
        {
            var settings = new TurboCharger.Settings();
            var charger = new TurboCharger(settings);

            charger.RatedCharge.ShouldBe(1f + settings.BoostChargeMultiplier, tolerance: 0.0001f);
        }

        [Fact]
        public void AtmosphericCharger_RatedCharge_UsesRpmOneChoke()
        {
            var settings = new AtmosphericCharger.Settings();
            var charger = new AtmosphericCharger(settings);

            charger.RatedCharge.ShouldBe(
                settings.EtaPeak * (1f - settings.ChokeK), tolerance: 0.0001f);
        }

        [Fact]
        public void AtmosphericCharger_Boost_IsZero_And_NeverSurges()
        {
            var charger = new AtmosphericCharger(new AtmosphericCharger.Settings());

            for (var i = 0; i < 10; i++)
            {
                charger.Tick(0.1f, 1f, 1f, 0f, engineOn: true);
            }

            charger.Boost.ShouldBe(0f);
            charger.Surging.ShouldBeFalse();
        }

        [Fact]
        public void AtmosphericCharger_CopyConstructor_IsIndependent()
        {
            var template = new AtmosphericCharger.Settings { EtaPeak = 0.8f, CompressionRatio = 17f, IntakeNtu = 0.2f };
            var clone = new AtmosphericCharger.Settings(template);

            clone.EtaPeak.ShouldBe(0.8f);
            clone.EtaPeak = 0.5f;
            template.EtaPeak.ShouldBe(0.8f);
            clone.CompressionRatio = 20f;
            template.CompressionRatio.ShouldBe(17f);
            clone.IntakeNtu = 0.5f;
            template.IntakeNtu.ShouldBe(0.2f);
        }

        [Fact]
        public void TurboCharger_DefaultCompression_And_IntakeNtu_AreTurboValues()
        {
            var settings = new TurboCharger.Settings();
            settings.CompressionRatio.ShouldBe(13f);
            settings.IntakeNtu.ShouldBe(1f);

            var charger = new TurboCharger(settings);
            charger.CompressionRatio.ShouldBe(13f);
            charger.IntakeNtu.ShouldBe(1f);
        }

        [Fact]
        public void AtmosphericCharger_DefaultCompression_And_IntakeNtu_AreAspiratedValues()
        {
            var settings = new AtmosphericCharger.Settings();
            settings.CompressionRatio.ShouldBe(16f);
            settings.IntakeNtu.ShouldBe(0.15f);

            var charger = new AtmosphericCharger(settings);
            charger.CompressionRatio.ShouldBe(16f);
            charger.IntakeNtu.ShouldBe(0.15f);
        }

        [Fact]
        public void AtmosphericCharger_Validate_ClampsCompressionAndIntakeNtu()
        {
            var settings = new AtmosphericCharger.Settings { CompressionRatio = 0.5f, IntakeNtu = -1f };

            settings.Validate();

            settings.CompressionRatio.ShouldBeGreaterThan(1f);
            settings.IntakeNtu.ShouldBeGreaterThanOrEqualTo(0f);
        }

        [Fact]
        public void CombustionModel_WithAtmosphericCharger_Lambda_UsesAspiratedCalibration()
        {
            var model = new CombustionModel(() => 1f, () => 1f, () => 1f, () => PhysicsConstants.ReferenceAmbientK,
                new AtmosphericCharger(new AtmosphericCharger.Settings()),
                // ignites everything, so a cold engine doesn't skew the air consumption
                new CombustionModel.Settings { IgnitionTempK = 1f },
                new ExhaustVelocitySettings());
            // arrange: settle the charger
            model.Tick(0.016f, engineOn: true);

            // act
            model.Tick(0.016f, engineOn: true);

            model.Lambda.ShouldBe(
                AtmosphericCharger.Settings.DefaultEtaPeak * (1f - AtmosphericCharger.Settings.DefaultChokeK)
                / AtmosphericCharger.Settings.DefaultLambdaCalibration, tolerance: 0.001f);
            model.Boost.ShouldBe(0f);
            model.SurgeThisTick.ShouldBeFalse();
        }

        [Fact]
        public void RootsCharger_Charge_BeforeFirstTick_IsAmbient()
        {
            new RootsCharger(new RootsCharger.Settings()).Charge.ShouldBe(1f);
        }

        [Fact]
        public void RootsCharger_Charge_Rises_WithRpmSquared()
        {
            var charger = new RootsCharger(new RootsCharger.Settings());

            charger.Tick(0.016f, 0f, 1f, 0f, engineOn: true);
            var standstill = charger.Charge;
            charger.Tick(0.016f, 0.5f, 1f, 0f, engineOn: true);
            var mid = charger.Charge;
            charger.Tick(0.016f, 1f, 1f, 0f, engineOn: true);
            var rated = charger.Charge;

            // defaults: 1, 1 + 0.26 x 0.25 = 1.065, 1.26
            standstill.ShouldBe(1f, tolerance: 0.0001f);
            mid.ShouldBe(1.065f, tolerance: 0.0001f);
            rated.ShouldBe(1.26f, tolerance: 0.0001f);
        }

        [Fact]
        public void RootsCharger_RatedCharge_IsPressureRatio()
        {
            var settings = new RootsCharger.Settings { PressureRatio = 1.4f };
            var charger = new RootsCharger(settings);

            charger.RatedCharge.ShouldBe(1.4f);
            charger.Tick(0.016f, 1f, 1f, 0f, engineOn: true);
            charger.Charge.ShouldBe(charger.RatedCharge, tolerance: 0.0001f);
        }

        [Fact]
        public void RootsCharger_Boost_IsNormalized()
        {
            var charger = new RootsCharger(new RootsCharger.Settings());

            charger.Tick(0.016f, 0f, 1f, 0f, engineOn: true);
            charger.Boost.ShouldBe(0f);
            charger.Tick(0.016f, 0.5f, 1f, 0f, engineOn: true);
            charger.Boost.ShouldBe(0.25f, tolerance: 0.0001f);
            charger.Tick(0.016f, 1f, 1f, 0f, engineOn: true);
            charger.Boost.ShouldBe(1f, tolerance: 0.0001f);
        }

        [Fact]
        public void RootsCharger_FollowsRpm_WithoutLag_IgnoringLoadAndExhaust()
        {
            var idle = new RootsCharger(new RootsCharger.Settings());
            var loaded = new RootsCharger(new RootsCharger.Settings());

            // a single short tick is enough to reach the target, whatever the throttle or exhaust energy
            idle.Tick(0.001f, 0.8f, 0f, 0f, engineOn: true);
            loaded.Tick(0.001f, 0.8f, 1f, 1f, engineOn: true);

            loaded.Charge.ShouldBe(idle.Charge);
            idle.Charge.ShouldBe(1f + 0.26f * 0.64f, tolerance: 0.0001f);
        }

        [Fact]
        public void RootsCharger_NeverSurges_OnThrottleCut()
        {
            var charger = new RootsCharger(new RootsCharger.Settings());

            charger.Tick(0.016f, 1f, 1f, 1f, engineOn: true);
            charger.Tick(0.016f, 1f, 0f, 1f, engineOn: true);

            charger.Surging.ShouldBeFalse();
        }

        [Fact]
        public void RootsCharger_HasNoTurbine_AndUsesConfiguredCompressorEfficiency()
        {
            var charger = new RootsCharger(new RootsCharger.Settings { CompressorEfficiency = 0.6f });

            charger.Tick(0.016f, 1f, 1f, 1f, engineOn: true);

            charger.TurbinePressureRatio.ShouldBe(1f);
            charger.CompressorEfficiency.ShouldBe(0.6f);
        }

        [Fact]
        public void RootsCharger_CopyConstructor_IsIndependent()
        {
            var template = new RootsCharger.Settings { PressureRatio = 1.4f, BoostExponent = 1.5f };
            var clone = new RootsCharger.Settings(template);

            clone.PressureRatio.ShouldBe(1.4f);
            clone.BoostExponent.ShouldBe(1.5f);
            clone.PressureRatio = 1.2f;
            clone.BoostExponent = 2f;
            template.PressureRatio.ShouldBe(1.4f);
            template.BoostExponent.ShouldBe(1.5f);
        }

        [Fact]
        public void RootsCharger_Validate_ClampsSettings()
        {
            var settings = new RootsCharger.Settings
            {
                PressureRatio = 0.5f,
                BoostExponent = -1f,
                CompressorEfficiency = 0f,
                LambdaCalibration = 0f,
                CompressionRatio = 0.5f,
                IntakeNtu = -1f,
            };

            settings.Validate();

            settings.PressureRatio.ShouldBe(1f);
            settings.BoostExponent.ShouldBe(0f);
            settings.CompressorEfficiency.ShouldBeGreaterThan(0f);
            settings.LambdaCalibration.ShouldBeGreaterThan(0f);
            settings.CompressionRatio.ShouldBeGreaterThan(1f);
            settings.IntakeNtu.ShouldBe(0f);

            settings.CompressorEfficiency = 2f;
            settings.Validate();
            settings.CompressorEfficiency.ShouldBe(1f);
        }

        [Fact]
        public void RootsCharger_Clone_HasIndependentSettings()
        {
            var charger = new RootsCharger(new RootsCharger.Settings());
            var clone = (RootsCharger)charger.Clone();

            clone.Tuning.PressureRatio = 1.5f;

            charger.Tuning.PressureRatio.ShouldBe(RootsCharger.Settings.DefaultPressureRatio);
        }

        [Fact]
        public void CombustionModel_WithRootsCharger_Lambda_UsesRootsCalibration()
        {
            var model = CreateRootsModel(new RootsCharger.Settings());
            // arrange: settle the charger
            model.Tick(0.016f, engineOn: true);

            // act
            model.Tick(0.016f, engineOn: true);

            model.Lambda.ShouldBe(
                RootsCharger.Settings.DefaultPressureRatio / RootsCharger.Settings.DefaultLambdaCalibration,
                tolerance: 0.001f);
            model.Boost.ShouldBe(1f, tolerance: 0.0001f);
            model.SurgeThisTick.ShouldBeFalse();
        }

        [Fact]
        public void CombustionModel_WithRootsCharger_HasNoTurbineDrop()
        {
            var model = CreateRootsModel(new RootsCharger.Settings());
            model.Tick(0.016f, engineOn: true);

            model.Tick(0.016f, engineOn: true);

            model.Charge.ShouldBeGreaterThan(1f);
            model.TurbineTemperatureDropK.ShouldBe(0f);
            model.GasTemperature.ShouldBe(model.ManifoldTempK);
        }

        [Fact]
        public void CombustionModel_WithRootsCharger_LessEfficientBlower_HeatsIntakeMore()
        {
            // no manifold heat exchange, so the intake temperature is set by compression alone
            var efficient = CreateRootsModel(new RootsCharger.Settings { CompressorEfficiency = 0.75f, IntakeNtu = 0f });
            var roots = CreateRootsModel(new RootsCharger.Settings { IntakeNtu = 0f });
            efficient.Tick(0.016f, engineOn: true);
            roots.Tick(0.016f, engineOn: true);

            efficient.Tick(0.016f, engineOn: true);
            roots.Tick(0.016f, engineOn: true);

            efficient.Charge.ShouldBe(roots.Charge);
            efficient.IntakeTempK.ShouldBeGreaterThan(PhysicsConstants.ReferenceAmbientK);
            roots.IntakeTempK.ShouldBeGreaterThan(efficient.IntakeTempK);
        }

        // full fuel at rated speed, igniting everything so a cold engine doesn't skew the air consumption
        private static CombustionModel CreateRootsModel(RootsCharger.Settings settings)
            => new CombustionModel(() => 1f, () => 1f, () => 1f, () => PhysicsConstants.ReferenceAmbientK,
                new RootsCharger(settings),
                new CombustionModel.Settings { IgnitionTempK = 1f },
                new ExhaustVelocitySettings());
    }
}
