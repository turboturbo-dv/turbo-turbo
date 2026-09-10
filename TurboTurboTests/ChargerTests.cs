using Shouldly;

using TurboTurbo.Modeling;

using Xunit;

namespace TurboTurboTests
{
    public class ChargerTests
    {
        [Fact]
        public void TurboCharger_DefaultLambdaCalibration_IsTurboValue()
        {
            new TurboCharger.Settings().LambdaCalibration.ShouldBe(1.74f);
        }

        [Fact]
        public void AtmosphericCharger_DefaultLambdaCalibration_IsAspiratedValue()
        {
            new AtmosphericCharger.Settings().LambdaCalibration.ShouldBe(0.6f);
        }

        [Fact]
        public void AtmosphericCharger_Charge_AtZeroRpm_IsEtaPeak()
        {
            var charger = new AtmosphericCharger(new AtmosphericCharger.Settings());
            charger.Tick(0.016f, 1f, 0f, 0f, 1f, engineOn: true);

            charger.Charge.ShouldBe(charger.Tuning.EtaPeak, tolerance: 0.0001f);
        }

        [Fact]
        public void AtmosphericCharger_Charge_Falls_WithRpm()
        {
            var charger = new AtmosphericCharger(new AtmosphericCharger.Settings());

            charger.Tick(0.016f, 1f, 0f, 0f, 1f, engineOn: true);
            var low = charger.Charge;
            charger.Tick(0.016f, 1f, 0f, 0.5f, 1f, engineOn: true);
            var mid = charger.Charge;
            charger.Tick(0.016f, 1f, 0f, 1f, 1f, engineOn: true);
            var high = charger.Charge;

            // defaults: 0.9, 0.9 x (1 - 0.25 x 0.25) = 0.84375, 0.9 x 0.75 = 0.675
            low.ShouldBe(0.9f, tolerance: 0.0001f);
            mid.ShouldBe(0.84375f, tolerance: 0.0001f);
            high.ShouldBe(0.675f, tolerance: 0.0001f);
        }

        [Fact]
        public void AtmosphericCharger_Boost_IsZero_And_NeverSurges()
        {
            var charger = new AtmosphericCharger(new AtmosphericCharger.Settings());

            for (var i = 0; i < 10; i++)
            {
                charger.Tick(0.1f, 1f, 0f, 1f, 1f, engineOn: true);
            }

            charger.Boost.ShouldBe(0f);
            charger.Surging.ShouldBeFalse();
        }

        [Fact]
        public void AtmosphericCharger_Heat_TracksFuelTimesCharge()
        {
            var charger = new AtmosphericCharger(new AtmosphericCharger.Settings());
            charger.Tick(0.016f, 0.5f, 0f, 1f, 0.5f, engineOn: true);

            charger.ExhaustHeat.ShouldBe(0.5f * charger.Charge, tolerance: 0.0001f);
        }

        [Fact]
        public void AtmosphericCharger_CopyConstructor_IsIndependent()
        {
            var template = new AtmosphericCharger.Settings { EtaPeak = 0.8f };
            var clone = new AtmosphericCharger.Settings(template);

            clone.EtaPeak.ShouldBe(0.8f);
            clone.EtaPeak = 0.5f;
            template.EtaPeak.ShouldBe(0.8f);
        }

        [Fact]
        public void CombustionModel_Settings_CopyConstructor_IsIndependent()
        {
            var template = new CombustionModel.Settings { TorqueLambdaFloor = 0.6f };
            var clone = new CombustionModel.Settings(template);

            clone.TorqueLambdaFloor.ShouldBe(0.6f);
            clone.TorqueLambdaFloor = 0.9f;
            template.TorqueLambdaFloor.ShouldBe(0.6f);
        }

        [Fact]
        public void CombustionModel_WithAtmosphericCharger_Lambda_UsesAspiratedCalibration()
        {
            var model = new CombustionModel(new CombustionModel.Settings(), () => 1f, () => 1f, () => 1f,
                new AtmosphericCharger(new AtmosphericCharger.Settings()));
            // arrange: settle the charger
            model.Tick(0.016f, engineOn: true);

            // act
            model.Tick(0.016f, engineOn: true);

            model.Lambda.ShouldBe(1.125f, tolerance: 0.001f);
            model.Boost.ShouldBe(0f);
            model.SurgeThisTick.ShouldBeFalse();
        }
    }
}