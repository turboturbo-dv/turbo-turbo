using Shouldly;

using TurboTurbo.Modeling;

using UnityEngine;

using Xunit;

namespace TurboTurboTests
{
    /// <summary>
    /// Behavioural tests for the heat-based ExhaustSmokeModel (SMOKE.md):
    /// engine-off guard, heat-driven base color, lambda-driven baseline
    /// opacity, oil tint, the soot ladder, and heat-driven wet-stack fill
    /// and release, plus the relational invariants enforced by Validate.
    /// All expectations reference the model's own tuning constants instead
    /// of hardcoded numbers, so retuning the model cannot silently
    /// invalidate a test.
    /// </summary>
    public class ExhaustSmokeModelTests
    {
        private readonly ExhaustSmokeModel _model = new ExhaustSmokeModel();

        // enough zero-heat 1s steps to fill the wet-stack accumulator (2x margin)
        private int FillSteps => Mathf.CeilToInt(2f / _model.Tuning.WetStackFillRate);

        private void Fill()
        {
            for (var i = 0; i < FillSteps; i++)
            {
                _model.Update(2f, 0.3f, 0f, engineOn: true, 1f);
            }
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

        [Fact]
        public void EngineOff_PreservesAccumulator()
        {
            Fill();
            var stored = _model.WetStackAccumulator;
            stored.ShouldBeGreaterThan(0.9f);

            _model.Update(0.5f, 0.5f, 1f, engineOn: false, 1000f);

            _model.WetStackAccumulator.ShouldBe(stored);
        }

        // ------------------------------------------------------------
        // base exhaust
        // ------------------------------------------------------------

        [Fact]
        public void CleanHighFlow_Exhaust_IsFaintCleanBurn()
        {
            _model.Update(_model.Tuning.CleanExhaustLambda, 0f, 1f, engineOn: true, 0.016f);

            _model.Color.a.ShouldBe(_model.Tuning.CleanExhaustAlpha, tolerance: 0.001f);
            _model.Color.r.ShouldBe(_model.Tuning.ColorCleanBurn.r, tolerance: 0.01f);
            _model.Color.g.ShouldBe(_model.Tuning.ColorCleanBurn.g, tolerance: 0.01f);
            _model.Color.b.ShouldBe(_model.Tuning.ColorCleanBurn.b, tolerance: 0.01f);
        }

        [Fact]
        public void IdleHeat_AtSootOnset_KeepsHazeTintAndAlpha()
        {
            _model.Update(_model.Tuning.SootOnsetLambda, 0f, 0f, engineOn: true, 0.016f);

            _model.Color.a.ShouldBe(_model.Tuning.HazeAlpha, tolerance: 0.001f);
            _model.Color.r.ShouldBe(_model.Tuning.ColorIdleHaze.r, tolerance: 0.01f);
            _model.Color.g.ShouldBe(_model.Tuning.ColorIdleHaze.g, tolerance: 0.01f);
            _model.Color.b.ShouldBe(_model.Tuning.ColorIdleHaze.b, tolerance: 0.01f);
        }

        [Fact]
        public void Haze_IsFullyGone_AtCleanBurnHeat()
        {
            _model.Update(_model.Tuning.CleanExhaustLambda, 0f, _model.Tuning.CleanBurnHeat, engineOn: true, 0.016f);

            _model.Color.r.ShouldBe(_model.Tuning.ColorCleanBurn.r, tolerance: 0.01f);
        }

        [Fact]
        public void BaseColor_BlendsLinearly_WithHeat()
        {
            _model.Update(_model.Tuning.CleanExhaustLambda, 0f, _model.Tuning.CleanBurnHeat * 0.5f, engineOn: true, 0.016f);

            _model.Color.r.ShouldBe(
                (_model.Tuning.ColorIdleHaze.r + _model.Tuning.ColorCleanBurn.r) * 0.5f,
                tolerance: 0.01f);
        }

        // ------------------------------------------------------------
        // soot ladder
        // ------------------------------------------------------------

        [Fact]
        public void SootyExhaust_IsDarkAndDense()
        {
            _model.Update(_model.Tuning.SootOpaqueLambda, 0f, 0.5f, engineOn: true, 0.016f);

            var expectedAlpha = 1f - (1f - _model.Tuning.HazeAlpha) * (1f - _model.Tuning.SootMaxAlpha);
            _model.Color.a.ShouldBe(expectedAlpha, tolerance: 0.005f);
            _model.Color.r.ShouldBeLessThan(0.2f);
        }

        [Fact]
        public void Alpha_Increases_AsLambdaFalls()
        {
            var model = new ExhaustSmokeModel();
            var last = -1f;
            for (var lambda = 2f; lambda >= 0.3f; lambda -= 0.05f)
            {
                model.Update(lambda, 0f, 0.5f, engineOn: true, 0.016f);
                model.Color.a.ShouldBeGreaterThanOrEqualTo(last);
                last = model.Color.a;
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
        // wet stacking
        // ------------------------------------------------------------

        [Fact]
        public void WetStack_Fills_AtIdleHeat()
        {
            Fill();

            _model.WetStackAccumulator.ShouldBeGreaterThan(0.9f);
        }

        [Fact]
        public void WetStack_NeutralBand_PreservesAccumulator()
        {
            Fill();
            var stored = _model.WetStackAccumulator;

            var midBandHeat = (_model.Tuning.WetStackFillHeat + _model.Tuning.WetStackReleaseHeat) * 0.5f;
            _model.Update(2f, 0.3f, midBandHeat, engineOn: true, 1000f);

            _model.WetStackAccumulator.ShouldBe(stored, tolerance: 0.0001f);
        }

        [Fact]
        public void WetStack_Releases_UnderHighHeat()
        {
            Fill();

            _model.Update(2f, 0.3f, 1f, engineOn: true, 0.016f);

            _model.WetStackAccumulator.ShouldBeLessThan(1f);
            _model.Color.a.ShouldBeGreaterThan(_model.Tuning.HazeAlpha);
            _model.Color.r.ShouldBeGreaterThan(_model.Tuning.ColorIdleHaze.r,
                "wet-stack mist should push the color toward off-white");
        }

        [Fact]
        public void WetStack_Drains_Completely_UnderSustainedHeat()
        {
            Fill();

            // 0.1s steps, generous margin over the linear release time
            var steps = Mathf.CeilToInt(2f / (_model.Tuning.WetStackReleaseRate * 0.1f));
            for (var i = 0; i < steps; i++)
            {
                _model.Update(2f, 0.3f, 1f, engineOn: true, 0.1f);
            }

            _model.WetStackAccumulator.ShouldBe(0f, tolerance: 0.001f);
            _model.Color.a.ShouldBe(_model.Tuning.CleanExhaustAlpha, tolerance: 0.01f);
        }

        [Fact]
        public void Settings_CopyConstructor_IsIndependent()
        {
            var template = new ExhaustSmokeModel.Settings { SootOnsetLambda = 0.5f };
            var clone = new ExhaustSmokeModel.Settings(template);

            clone.SootOnsetLambda.ShouldBe(0.5f);
            clone.SootOnsetLambda = 0.9f;
            template.SootOnsetLambda.ShouldBe(0.5f);
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
                    CleanExhaustLambda = 0.9f,
                    SootOnsetLambda = 1.2f,
                    SootOpaqueLambda = 1.1f,
                },
            };
            model.Tuning.Validate();

            model.Tuning.SootOpaqueLambda.ShouldBeLessThan(model.Tuning.SootOnsetLambda);
            model.Tuning.SootOnsetLambda.ShouldBeLessThan(model.Tuning.CleanExhaustLambda);
        }

        [Fact]
        public void Validate_RestoresWetStackHeatOrdering()
        {
            var model = new ExhaustSmokeModel
            {
                Tuning =
                {
                    WetStackFillHeat = 0.8f,
                    WetStackReleaseHeat = 0.3f,
                },
            };
            model.Tuning.Validate();

            model.Tuning.WetStackFillHeat.ShouldBeLessThan(model.Tuning.WetStackReleaseHeat);
            model.Tuning.WetStackReleaseHeat.ShouldBeLessThanOrEqualTo(1f);
        }
    }

    internal static class SmokeColorExtensions
    {
        public static Vector3 Rgb(this Color c) => new Vector3(c.r, c.g, c.b);

        public static float DistanceTo(this Vector3 v, Vector3 other) =>
            Vector3.Distance(v, other);
    }
}