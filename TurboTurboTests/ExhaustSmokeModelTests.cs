using Shouldly;

using TurboTurbo.Modeling;

using UnityEngine;

using Xunit;

namespace TurboTurboTests
{
    public class ExhaustSmokeModelTests
    {
        private readonly ExhaustSmokeModel _model = new ExhaustSmokeModel();

        // run frames until the soot lag converges on the current lambda
        private static void SettleSoot(ExhaustSmokeModel model, float lambda, float heat)
        {
            for (var i = 0; i < 200; i++)
            {
                model.Update(lambda, 0f, heat, engineOn: true, 0.016f);
            }
        }

        // raw soot contribution to the density-scaled mass, before normalizing by SootMaxAlpha and the power ramp
        private static float SootMass(ExhaustSmokeModel model, float heat)
        {
            var tuning = model.Tuning;
            var baseAlpha = Mathf.Lerp(tuning.CleanMinHeatAlpha, tuning.CleanMaxHeatAlpha, heat);
            return model.ParticulateMass / tuning.Density - baseAlpha;
        }

        // recover the soot fraction [0..1] from the density-scaled particulate mass
        private static float SootFraction(ExhaustSmokeModel model, float heat)
        {
            var tuning = model.Tuning;
            var power = Mathf.Lerp(tuning.SootPowerFloor, 1f, Mathf.Pow(heat, tuning.SootPowerExponent));
            return SootMass(model, heat) / tuning.SootMaxAlpha / power;
        }

        // ------------------------------------------------------------
        // engine-off guard
        // ------------------------------------------------------------

        [Fact]
        public void EngineOff_ReturnsClearColor()
        {
            _model.Update(0.5f, 0.5f, 0.5f, engineOn: false, delta: 0.016f);

            _model.Color.ShouldBe(Color.clear);
        }

        // ------------------------------------------------------------
        // base exhaust
        // ------------------------------------------------------------

        [Fact]
        public void CleanHighFlow_Exhaust_UsesCleanBurnTint()
        {
            _model.Update(2f, 0f, 1f, engineOn: true, 0.016f);

            _model.Color.r.ShouldBe(_model.Tuning.ColorCleanBurn.r, tolerance: 0.01f);
            _model.Color.g.ShouldBe(_model.Tuning.ColorCleanBurn.g, tolerance: 0.01f);
            _model.Color.b.ShouldBe(_model.Tuning.ColorCleanBurn.b, tolerance: 0.01f);
        }

        [Fact]
        public void IdleHeat_AtSootOnset_KeepsHazeTint()
        {
            _model.Update(_model.Tuning.SootOnsetLambda, 0f, 0f, engineOn: true, 0.016f);

            _model.Color.r.ShouldBe(_model.Tuning.ColorIdleHaze.r, tolerance: 0.01f);
            _model.Color.g.ShouldBe(_model.Tuning.ColorIdleHaze.g, tolerance: 0.01f);
            _model.Color.b.ShouldBe(_model.Tuning.ColorIdleHaze.b, tolerance: 0.01f);
        }

        [Fact]
        public void Haze_IsFullyGone_AtCleanBurnHeat()
        {
            _model.Update(2f, 0f, _model.Tuning.CleanBurnHeat, engineOn: true, 0.016f);

            _model.Color.r.ShouldBe(_model.Tuning.ColorCleanBurn.r, tolerance: 0.01f);
        }

        [Fact]
        public void BaseColor_BlendsLinearly_WithHeat()
        {
            _model.Update(2f, 0f, _model.Tuning.CleanBurnHeat * 0.5f, engineOn: true, 0.016f);

            _model.Color.r.ShouldBe(
                (_model.Tuning.ColorIdleHaze.r + _model.Tuning.ColorCleanBurn.r) * 0.5f,
                tolerance: 0.01f);
        }

        // ------------------------------------------------------------
        // soot ladder
        // ------------------------------------------------------------

        [Fact]
        public void SootyExhaust_IsDark()
        {
            SettleSoot(_model, _model.Tuning.SootOpaqueLambda, 0.5f);

            _model.Color.r.ShouldBeLessThan(0.2f);
        }

        [Fact]
        public void Soot_AttacksFasterThanItReleases()
        {
            var attack = new ExhaustSmokeModel();
            attack.Update(2f, 0f, 0.5f, engineOn: true, 0.016f);
            attack.Update(attack.Tuning.SootOpaqueLambda, 0f, 0.5f, engineOn: true, 0.016f);

            var release = new ExhaustSmokeModel();
            SettleSoot(release, release.Tuning.SootOpaqueLambda, 0.5f);
            SootFraction(release, 0.5f).ShouldBe(1f, tolerance: 0.01f);
            release.Update(2f, 0f, 0.5f, engineOn: true, 0.016f);

            SootFraction(attack, 0.5f).ShouldBeGreaterThan(
                1f - SootFraction(release, 0.5f),
                "one frame should darken soot faster than one frame clears it");
        }

        [Fact]
        public void EngineOff_ResetsSoot()
        {
            SettleSoot(_model, _model.Tuning.SootOpaqueLambda, 0.5f);
            SootFraction(_model, 0.5f).ShouldBe(1f, tolerance: 0.01f);

            _model.Update(2f, 0f, 0.5f, engineOn: false, 0.016f);
            _model.Update(2f, 0f, 0.5f, engineOn: true, 0.016f);

            SootFraction(_model, 0.5f).ShouldBe(0f, tolerance: 0.001f);
        }

        // ------------------------------------------------------------
        // particulate mass (SMOKE.md density model)
        // ------------------------------------------------------------

        [Fact]
        public void EngineOff_ZeroesParticulateMass()
        {
            _model.Update(0.5f, 0.5f, 0.5f, engineOn: false, delta: 0.016f);

            _model.ParticulateMass.ShouldBe(0f);
        }

        [Fact]
        public void CleanHighFlow_ParticulateMass_IsBaseMass()
        {
            _model.Update(2f, 0f, 1f, engineOn: true, 0.016f);

            _model.ParticulateMass.ShouldBe(
                _model.Tuning.Density * _model.Tuning.CleanMaxHeatAlpha, tolerance: 0.001f);
        }

        [Fact]
        public void Density_ScalesParticulateMass_Linearly()
        {
            var single = new ExhaustSmokeModel { Tuning = { Density = 1f } };
            single.Update(2f, 0f, 1f, engineOn: true, 0.016f);

            var doubled = new ExhaustSmokeModel { Tuning = { Density = 2f } };
            doubled.Update(2f, 0f, 1f, engineOn: true, 0.016f);

            doubled.ParticulateMass.ShouldBe(single.ParticulateMass * 2f, tolerance: 0.001f);
        }

        [Fact]
        public void ParticulateMass_Increases_AsLambdaFalls()
        {
            var model = new ExhaustSmokeModel();
            var last = -1f;
            for (var lambda = 2f; lambda >= 0.3f; lambda -= 0.05f)
            {
                model.Update(lambda, 0f, 0.5f, engineOn: true, 0.016f);
                model.ParticulateMass.ShouldBeGreaterThanOrEqualTo(last);
                last = model.ParticulateMass;
            }
        }

        [Fact]
        public void SootyExhaust_ParticulateMass_ExceedsBaseMass()
        {
            SettleSoot(_model, _model.Tuning.SootOpaqueLambda, 0.5f);

            var baseMass = _model.Tuning.Density * Mathf.Lerp(
                _model.Tuning.CleanMinHeatAlpha, _model.Tuning.CleanMaxHeatAlpha, 0.5f);
            _model.ParticulateMass.ShouldBeGreaterThan(baseMass);
        }

        [Fact]
        public void SootPower_RampsWithHeat()
        {
            var idle = new ExhaustSmokeModel();
            SettleSoot(idle, idle.Tuning.SootOpaqueLambda, 0f);

            var full = new ExhaustSmokeModel();
            SettleSoot(full, full.Tuning.SootOpaqueLambda, 1f);

            SootMass(idle, 0f).ShouldBe(
                SootMass(full, 1f) * full.Tuning.SootPowerFloor, tolerance: 0.01f);
        }

        [Fact]
        public void SootMass_IsMonotonicInHeat()
        {
            var last = -1f;
            for (var heat = 0f; heat <= 1f; heat += 0.1f)
            {
                var model = new ExhaustSmokeModel();
                SettleSoot(model, model.Tuning.SootOpaqueLambda, heat);

                var soot = SootMass(model, heat);
                soot.ShouldBeGreaterThanOrEqualTo(last - 1e-5f);
                last = soot;
            }
        }

        [Fact]
        public void SootPower_ExponentAboveOne_DelaysSoot()
        {
            var linear = new ExhaustSmokeModel { Tuning = { SootPowerExponent = 1f } };
            SettleSoot(linear, linear.Tuning.SootOpaqueLambda, 0.5f);

            var delayed = new ExhaustSmokeModel { Tuning = { SootPowerExponent = 2f } };
            SettleSoot(delayed, delayed.Tuning.SootOpaqueLambda, 0.5f);

            SootMass(delayed, 0.5f).ShouldBeLessThan(SootMass(linear, 0.5f));
        }

        [Fact]
        public void ParticulateMass_StaysWithinCeiling()
        {
            var model = new ExhaustSmokeModel();

            for (var heat = 0f; heat <= 1f; heat += 0.1f)
            {
                for (var lambda = 2f; lambda >= 0.3f; lambda -= 0.1f)
                {
                    model.Update(lambda, 0.5f, heat, engineOn: true, 0.5f, vapour: 1f);
                    model.ParticulateMass.ShouldBeLessThanOrEqualTo(
                        model.MaxParticulateMass + 0.001f);
                }
            }
        }

        // ------------------------------------------------------------
        // oil tint
        // ------------------------------------------------------------

        [Fact]
        public void HighRpm_BlendsTowardOilBurnTint()
        {
            // clean exhaust (lambda at the clean threshold), only rpm differs
            var idleRpm = new ExhaustSmokeModel();
            idleRpm.Update(2f, 0f, 1f, engineOn: true, 0.016f);

            var fullRpm = new ExhaustSmokeModel();
            fullRpm.Update(2f, 1f, 1f, engineOn: true, 0.016f);

            var oilBurn = _model.Tuning.ColorOilBurn.Rgb();
            fullRpm.Color.Rgb().DistanceTo(oilBurn).ShouldBeLessThan(
                idleRpm.Color.Rgb().DistanceTo(oilBurn),
                "high rpm should blend the tint toward the oil-burn color");
        }

        // ------------------------------------------------------------
        // fuel-vapour hookup
        // ------------------------------------------------------------

        [Fact]
        public void Vapour_IncreasesParticulateMass()
        {
            var dry = new ExhaustSmokeModel();
            dry.Update(2f, 0f, 0.5f, engineOn: true, 0.016f, vapour: 0f);

            var wet = new ExhaustSmokeModel();
            wet.Update(2f, 0f, 0.5f, engineOn: true, 0.016f, vapour: 1f);

            wet.ParticulateMass.ShouldBeGreaterThan(dry.ParticulateMass);
        }

        [Fact]
        public void Vapour_BlendsTowardWetStackColor()
        {
            var dry = new ExhaustSmokeModel();
            dry.Update(2f, 0f, 1f, engineOn: true, 0.016f, vapour: 0f);

            var wet = new ExhaustSmokeModel();
            wet.Update(2f, 0f, 1f, engineOn: true, 0.016f, vapour: 1f);

            var wetColor = _model.Tuning.ColorWetStack.Rgb();
            wet.Color.Rgb().DistanceTo(wetColor).ShouldBeLessThan(
                dry.Color.Rgb().DistanceTo(wetColor),
                "fuel vapour should push the tint toward the wet-stack color");
        }

        [Fact]
        public void Settings_CopyConstructor_IsIndependent()
        {
            var template = new ExhaustSmokeModel.Settings
            {
                SootOnsetLambda = 0.5f,
                SootIncreaseTau = 0.2f,
                SootDecreaseTau = 1.5f,
            };
            var clone = new ExhaustSmokeModel.Settings(template);

            clone.SootOnsetLambda.ShouldBe(0.5f);
            clone.SootIncreaseTau.ShouldBe(0.2f);
            clone.SootDecreaseTau.ShouldBe(1.5f);
            clone.SootIncreaseTau = 0.9f;
            template.SootIncreaseTau.ShouldBe(0.2f);
        }

        [Fact]
        public void Settings_CopyConstructor_CopiesDensity()
        {
            var template = new ExhaustSmokeModel.Settings { Density = 3f };
            var clone = new ExhaustSmokeModel.Settings(template);

            clone.Density.ShouldBe(3f);
        }

        // ------------------------------------------------------------
        // invariants
        // ------------------------------------------------------------

        [Fact]
        public void Validate_RestoresLambdaOrdering()
        {
            var model = new ExhaustSmokeModel
            {
                Tuning =
                {
                    SootOnsetLambda = 1.2f,
                    SootOpaqueLambda = 1.1f,
                },
            };
            model.Tuning.Validate();

            model.Tuning.SootOpaqueLambda.ShouldBeLessThan(model.Tuning.SootOnsetLambda);
        }

        [Fact]
        public void Validate_ClampsSootTaus()
        {
            var model = new ExhaustSmokeModel
            {
                Tuning =
                {
                    SootIncreaseTau = -1f,
                    SootDecreaseTau = 0f,
                },
            };
            model.Tuning.Validate();

            model.Tuning.SootIncreaseTau.ShouldBeGreaterThan(0f);
            model.Tuning.SootDecreaseTau.ShouldBeGreaterThan(0f);
        }

        [Fact]
        public void Validate_ClampsNegativeDensity()
        {
            var model = new ExhaustSmokeModel { Tuning = { Density = -5f } };
            model.Tuning.Validate();

            model.Tuning.Density.ShouldBe(0f);
        }
    }

    internal static class SmokeColorExtensions
    {
        public static Vector3 Rgb(this Color c) => new Vector3(c.r, c.g, c.b);

        public static float DistanceTo(this Vector3 v, Vector3 other) =>
            Vector3.Distance(v, other);
    }
}
