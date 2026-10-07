using System;

using Shouldly;

using TurboTurbo;
using TurboTurbo.Modeling;

using Xunit;

namespace TurboTurboTests
{
    public class CombustionModelTests
    {
        private float _ambientTemperatureK = PhysicsConstants.ReferenceAmbientK;
        private readonly TurboCharger.Settings _chargerSettings = new TurboCharger.Settings();
        private readonly CombustionModel.Settings _combustionSettings = new CombustionModel.Settings();
        private readonly ExhaustVelocitySettings _velocitySettings = new ExhaustVelocitySettings();
        private float _governor;
        private float _fuelNorm;
        private float _rpmNorm = 1f;

        private CombustionModel CreateModel()
        {
            return new CombustionModel(() => _governor, () => _fuelNorm, () => _rpmNorm,
                () => _ambientTemperatureK, new TurboCharger(_chargerSettings), _combustionSettings, _velocitySettings);
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
        // fuel
        // ------------------------------------------------------------

        [Fact]
        public void FuelPerStroke_DividesFuelNormByRpm()
        {
            var model = CreateModel();
            _fuelNorm = 0.5f;
            _rpmNorm = 0.5f;
            model.Tick(0.016f, engineOn: true);

            model.FuelPerStroke.ShouldBe(1f, tolerance: 0.001f);
        }

        // ------------------------------------------------------------
        // boost dynamics
        // ------------------------------------------------------------

        [Fact]
        public void Boost_FirstTick_MatchesFirstOrderLagWithTauUp()
        {
            var model = CreateModel();

            // clean partial load: fuel per stroke 0.4, below charge/calibration
            // so overfuel 0, tau = TauUp = 3, delta 1s:
            // boost = 0.4 x (1 - e^(-1/3)) = 0.1134
            _fuelNorm = 0.4f;
            _rpmNorm = 1f;
            model.Tick(1f, engineOn: true);

            model.Boost.ShouldBe(0.4f * (1f - (float)Math.Exp(-1f / 3f)), tolerance: 0.001f);
        }

        [Fact]
        public void Boost_ThermalFeedback_ShortensSpool_UnderOverfuel()
        {
            var model = CreateModel();

            _fuelNorm = 1f;
            _rpmNorm = 1f;
            model.Tick(1f, engineOn: true);

            var tau = TurboCharger.Settings.DefaultTauUp
                / (1f + TurboCharger.Settings.DefaultThermalK * model.Overfuel);
            model.Boost.ShouldBe(model.ExhaustEnergy * (1f - (float)Math.Exp(-1f / tau)), tolerance: 0.001f);
        }

        [Fact]
        public void Boost_Rises_Monotonically_UnderSustainedFullLoad()
        {
            var model = CreateModel();
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
        public void Boost_Decays_AfterFuelDrops()
        {
            var model = CreateModel();

            // spool up first
            _fuelNorm = 1f;
            _rpmNorm = 1f;
            for (var i = 0; i < 600; i++)
            {
                model.Tick(0.1f, engineOn: true);
            }
            var peak = model.Boost;

            // cut fuel: boost must bleed off through TauDown
            _fuelNorm = 0f;
            for (var i = 0; i < 5; i++)
            {
                model.Tick(0.5f, engineOn: true);
                model.Boost.ShouldBeLessThan(peak);
            }
        }

        // ------------------------------------------------------------
        // surge detection
        // ------------------------------------------------------------

        [Fact]
        public void Surge_Detected_OnSharpGovernorDropAtHighBoost()
        {
            var model = CreateModel();

            // spool boost above 0.75: 5 ticks of 1s at full load
            _fuelNorm = 1f;
            _governor = 1f;
            _rpmNorm = 1f;
            for (var i = 0; i < 5; i++)
            {
                model.Tick(1f, engineOn: true);
            }
            model.Boost.ShouldBeGreaterThan(0.75f);

            // slam governor to 0.69 in one 16ms frame: rate = 0.31 / 0.016 = 19.4/s
            // beats the 15/s threshold, and the 16ms decay leaves boost above
            // the 0.75 gate.
            _governor = 0.69f;
            model.Tick(0.016f, engineOn: true);
            model.SurgeThisTick.ShouldBeTrue();
        }

        [Fact]
        public void NoSurge_WhenGovernorDropIsGradual()
        {
            var model = CreateModel();

            _fuelNorm = 1f;
            _governor = 1f;
            _rpmNorm = 1f;
            for (var i = 0; i < 5; i++)
            {
                model.Tick(1f, engineOn: true);
            }

            // ease down to 0.69 over 20 frames: per-frame rate ~1/s, far
            // below the threshold even though the total drop matches
            for (var i = 0; i < 20; i++)
            {
                _governor = 1f - 0.31f * (i + 1) / 20f;
                model.Tick(0.016f, engineOn: true);
                model.SurgeThisTick.ShouldBeFalse();
            }
        }

        [Fact]
        public void NoSurge_WhenGovernorIsSteady()
        {
            var model = CreateModel();

            _fuelNorm = 1f;
            _governor = 1f;
            _rpmNorm = 1f;
            for (var i = 0; i < 5; i++)
            {
                model.Tick(1f, engineOn: true);
            }

            model.SurgeThisTick.ShouldBeFalse();
        }

        // ------------------------------------------------------------
        // exhaust state
        // ------------------------------------------------------------

        [Fact]
        public void CombustionSettings_CopyConstructor_IsIndependent()
        {
            var template = new CombustionModel.Settings();
            var clone = new CombustionModel.Settings(template);

            clone.RatedExhaustTempK.ShouldBe(template.RatedExhaustTempK);
            clone.RatedExhaustTempK = 123f;
            template.RatedExhaustTempK.ShouldNotBe(123f);
        }

        [Fact]
        public void CombustionSettings_Validate_FloorsRatedExhaustTemp()
        {
            var settings = new CombustionModel.Settings { RatedExhaustTempK = 100f };
            settings.Validate();

            settings.RatedExhaustTempK.ShouldBeGreaterThan(PhysicsConstants.ReferenceAmbientK);
        }

        [Fact]
        public void MassFlow_IsChargeTimesRpm()
        {
            var model = CreateModel();
            _fuelNorm = 0.5f;
            _rpmNorm = 0.5f;
            model.Tick(0.016f, engineOn: true);

            // first tick: Charge = 1 (zero boost), rpm = 0.5
            model.MassFlow.ShouldBe(0.5f, tolerance: 0.001f);
        }

        [Fact]
        public void GasTemperature_Lean_UsesSpecificHeatRelease()
        {
            var model = CreateModel();
            _fuelNorm = 0.5f;
            _rpmNorm = 1f;
            model.Tick(0.016f, engineOn: true);

            model.GasTemperature.ShouldBe(789.178f, tolerance: 0.01f);
        }

        [Fact]
        public void GasDensity_Falls_AsTemperatureRises()
        {
            var model = CreateModel();
            _fuelNorm = 0.5f;
            _rpmNorm = 1f;
            model.Tick(0.016f, engineOn: true);

            model.GasDensity.ShouldBe(
                PhysicsConstants.ReferenceAirDensity * PhysicsConstants.ReferenceAmbientK / 789.178f,
                tolerance: 0.001f);
        }

        [Fact]
        public void GasTemperature_ManifoldReachesRatedTemp_AtFullPower()
        {
            var model = CreateModel();
            _fuelNorm = 1f;
            _rpmNorm = 1f;
            for (var i = 0; i < 600; i++)
            {
                model.Tick(0.1f, engineOn: true);
            }

            model.Charge.ShouldBe(model.Charger.ChargeAtFullPower, tolerance: 0.01f);
            var manifoldK = model.GasTemperature + model.TurbineTemperatureDropK;
            manifoldK.ShouldBe(CombustionModel.Settings.DefaultRatedExhaustTempK, tolerance: 0.5f);
        }

        [Fact]
        public void TurbineDrop_IsZero_WithoutBoost()
        {
            var model = CreateModel();
            _fuelNorm = 0.5f;
            _rpmNorm = 1f;
            model.Tick(0.016f, engineOn: true);

            // first tick: Charge = 1 (no boost), so the gas is not expanded by a turbine
            model.TurbineTemperatureDropK.ShouldBe(0f, tolerance: 0.001f);
        }

        [Fact]
        public void TurbineDrop_AtFullPower_MatchesIsentropicExpansion()
        {
            var model = CreateModel();
            _fuelNorm = 1f;
            _rpmNorm = 1f;
            for (var i = 0; i < 600; i++)
            {
                model.Tick(0.1f, engineOn: true);
            }

            // manifold 760 K expanded across the charge pressure ratio, gamma = 1.4 -> exponent 0.2857
            var expectedDrop = CombustionModel.Settings.DefaultRatedExhaustTempK *
                               (1f - (float)Math.Pow(model.Charger.ChargeAtFullPower, -0.28571));
            model.TurbineTemperatureDropK.ShouldBe(expectedDrop, tolerance: 0.5f);
        }

        [Fact]
        public void GasTemperature_TracksAmbientInput()
        {
            var model = CreateModel();
            _fuelNorm = 0.5f;
            _rpmNorm = 1f;
            _ambientTemperatureK = PhysicsConstants.ReferenceAmbientK - 20f;
            model.Tick(0.016f, engineOn: true);

            // gain is fixed, so EGT shifts down by the same 20 K the ambient input dropped
            model.GasTemperature.ShouldBe(789.178f - 20f, tolerance: 0.01f);
        }

        [Fact]
        public void ExhaustEnergy_IsScaledByCombustionEfficiency()
        {
            var model = CreateModel();
            _fuelNorm = 1f;
            _rpmNorm = 1f;
            model.Tick(0.016f, engineOn: true);

            model.ExhaustEnergy.ShouldBe(1f / 1.41f, tolerance: 0.001f);
            model.Overfuel.ShouldBeGreaterThan(0f);
        }

        [Fact]
        public void ExhaustEnergy_IsHigher_WhenLean()
        {
            var model = CreateModel();
            _fuelNorm = 0.3f;
            _rpmNorm = 1f;
            model.Tick(0.016f, engineOn: true);

            // Lambda > 1, so burn = 1 and energy is the fuel rate itself
            model.ExhaustEnergy.ShouldBe(0.3f, tolerance: 0.001f);
        }

        // ------------------------------------------------------------
        // exhaust velocity
        // ------------------------------------------------------------

        [Fact]
        public void VelocitySettings_CopyConstructor_IsIndependent()
        {
            var template = new ExhaustVelocitySettings { ExhaustVelocityCoefficient = 7f };
            var clone = new ExhaustVelocitySettings(template);

            clone.ExhaustVelocityCoefficient.ShouldBe(7f);
            clone.ExhaustVelocityCoefficient = 3f;
            template.ExhaustVelocityCoefficient.ShouldBe(7f);
        }

        [Fact]
        public void VelocitySettings_Validate_FloorsCoefficient()
        {
            var settings = new ExhaustVelocitySettings { ExhaustVelocityCoefficient = 0f };
            settings.Validate();

            settings.ExhaustVelocityCoefficient.ShouldBeGreaterThan(0f);
        }

        [Fact]
        public void VelocitySettings_Calculate_IsCoefficientTimesFlowOverDensity()
        {
            var settings = new ExhaustVelocitySettings { ExhaustVelocityCoefficient = 4f };

            settings.Calculate(3f, 1.5f).ShouldBe(8f, tolerance: 0.001f);
        }

        [Fact]
        public void ExhaustVelocity_IsCoefficientTimesFlowOverDensity()
        {
            var model = CreateModel();
            _fuelNorm = 0.5f;
            _rpmNorm = 1f;
            model.Tick(0.016f, engineOn: true);

            model.ExhaustVelocity.ShouldBe(
                _velocitySettings.ExhaustVelocityCoefficient * model.MassFlow / model.GasDensity,
                tolerance: 0.001f);
        }

        [Fact]
        public void ExhaustVelocity_ScalesWithCoefficient()
        {
            _velocitySettings.ExhaustVelocityCoefficient = 8f;
            var model = CreateModel();
            _fuelNorm = 0.5f;
            _rpmNorm = 1f;
            model.Tick(0.016f, engineOn: true);

            model.ExhaustVelocity.ShouldBe(8f * model.MassFlow / model.GasDensity, tolerance: 0.001f);
        }

        [Fact]
        public void ExhaustVelocity_IsHigher_WithMoreBoostCharge()
        {
            var low = CreateModel();
            _fuelNorm = 0.5f;
            _rpmNorm = 1f;
            for (var i = 0; i < 600; i++)
            {
                low.Tick(0.1f, engineOn: true);
            }

            _chargerSettings.BoostChargeMultiplier = 3f;
            var high = CreateModel();
            for (var i = 0; i < 600; i++)
            {
                high.Tick(0.1f, engineOn: true);
            }

            high.ExhaustVelocity.ShouldBeGreaterThan(low.ExhaustVelocity);
        }
    }
}
