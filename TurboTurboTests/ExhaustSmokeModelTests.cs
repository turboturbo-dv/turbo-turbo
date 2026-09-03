using Shouldly;

using TurboTurbo.Modeling;

using UnityEngine;

using Xunit;

namespace TurboTurboTests
{
    /// <summary>
    /// Behavioural tests for the ExhaustSmokeModel: engine-off guard,
    /// lambda-driven soot ladder, oil blowby tint, wet-stack accumulate and
    /// burn-off, and density monotonicity. All expectations reference the
    /// model's own tuning constants instead of hardcoded numbers, so retuning
    /// the model cannot silently invalidate a test.
    /// </summary>
    public class ExhaustSmokeModelTests
    {
        private readonly ExhaustSmokeModel _model = new ExhaustSmokeModel();

        // enough 1s idle steps to fill the wet-stack accumulator (2x margin)
        private static readonly int FillSteps =
            Mathf.CeilToInt(2f / ExhaustSmokeModel.WetStackFillRate);

        // ------------------------------------------------------------
        // engine-off guard
        // ------------------------------------------------------------

        [Fact]
        public void EngineOff_ReturnsClearColorAndZeroDensity()
        {
            _model.Update(0.5f, 0.8f, 0.5f, engineOn: false, delta: 0.016f);

            _model.Color.ShouldBe(Color.clear);
            _model.Density.ShouldBe(0f);
        }

        // ------------------------------------------------------------
        // lambda -> soot
        // ------------------------------------------------------------

        [Fact]
        public void CleanExhaust_KeepsHazeTintAndFloorAlpha()
        {
            // lambda far above onset, low rpm: pure haze, no oil tint
            _model.Update(2f, 0.5f, 0f, engineOn: true, delta: 0.016f);

            _model.Density.ShouldBe(0f, tolerance: 0.001f);
            _model.Color.a.ShouldBe(ExhaustSmokeModel.AlphaFloor, tolerance: 0.01f);
            _model.Color.r.ShouldBe(ExhaustSmokeModel.ColorIdleHaze.r, tolerance: 0.01f);
            _model.Color.g.ShouldBe(ExhaustSmokeModel.ColorIdleHaze.g, tolerance: 0.01f);
            _model.Color.b.ShouldBe(ExhaustSmokeModel.ColorIdleHaze.b, tolerance: 0.01f);
        }

        [Fact]
        public void LambdaAtOpaqueThreshold_FullySoots()
        {
            _model.Update(_model.SootOpaqueLambda, 0.8f, 0.5f, engineOn: true, 0.016f);

            _model.Density.ShouldBe(1f, tolerance: 0.01f);
            _model.Color.a.ShouldBe(ExhaustSmokeModel.AlphaCeiling, tolerance: 0.01f);
        }

        [Fact]
        public void LambdaBetweenThresholds_GivesIntermediateDensity()
        {
            // midway between the soot thresholds gives an intermediate ladder value
            var midLambda = (_model.SootOnsetLambda + _model.SootOpaqueLambda) * 0.5f;
            _model.Update(midLambda, 0.8f, 0.5f, engineOn: true, 0.016f);

            _model.Density.ShouldBeInRange(0.2f, 0.9f);
            _model.Color.a.ShouldBeLessThan(ExhaustSmokeModel.AlphaCeiling);
        }

        [Fact]
        public void SootDensity_MonotonicallyIncreases_AsLambdaFalls()
        {
            var model = new ExhaustSmokeModel();
            var last = -1f;
            for (var lambda = 2f; lambda >= 0.3f; lambda -= 0.05f)
            {
                model.Update(lambda, 0.8f, 0.5f, engineOn: true, 0.016f);
                model.Density.ShouldBeGreaterThanOrEqualTo(last);
                last = model.Density;
            }
        }

        // ------------------------------------------------------------
        // oil blowby
        // ------------------------------------------------------------

        [Fact]
        public void HighRpm_BlendsTowardOilBurnTint()
        {
            // clean exhaust (lambda far above onset), only rpm differs
            var idleRpm = new ExhaustSmokeModel();
            idleRpm.Update(2f, 0.5f, 0f, engineOn: true, 0.016f);

            var fullRpm = new ExhaustSmokeModel();
            fullRpm.Update(2f, 0.5f, 1f, engineOn: true, 0.016f);

            var oilBurn = new Vector3(
                ExhaustSmokeModel.ColorOilBurn.r,
                ExhaustSmokeModel.ColorOilBurn.g,
                ExhaustSmokeModel.ColorOilBurn.b);
            fullRpm.Color.Rgb().DistanceTo(oilBurn).ShouldBeLessThan(
                idleRpm.Color.Rgb().DistanceTo(oilBurn),
                "high rpm should blend the tint toward the oil-burn color");
        }

        // ------------------------------------------------------------
        // wet stacking
        // ------------------------------------------------------------

        [Fact]
        public void WetStack_AccumulatesAtIdle_BurnsOffUnderLoad()
        {
            // idle: fill the accumulator (1s steps)
            for (var i = 0; i < FillSteps; i++)
            {
                _model.Update(1.2f, 0f, 0.3f, engineOn: true, 1f);
            }

            // throttle up: burn-off must drive density and straw tint
            _model.Update(1.2f, 0.5f, 0.5f, engineOn: true, 0.1f);

            _model.Color.r.ShouldBeGreaterThan(ExhaustSmokeModel.ColorIdleHaze.r,
                "wet-stack burn should push red above the haze base");
            _model.Color.a.ShouldBeGreaterThanOrEqualTo(
                ExhaustSmokeModel.AlphaFloor + (ExhaustSmokeModel.AlphaCeiling - ExhaustSmokeModel.AlphaFloor) * 0.5f,
                "the burn cloud should sit well above the haze floor");
            _model.Density.ShouldBeGreaterThan(0f);
        }

        [Fact]
        public void WetStack_Idle_AlphaStaysAtHazeFloor()
        {
            // idle long enough to fully wet-stack
            for (var i = 0; i < FillSteps; i++)
            {
                _model.Update(1.2f, 0f, 0.3f, engineOn: true, 1f);
            }

            // idling never densifies the exhaust: alpha stays at the haze
            // floor even with a fully filled accumulator (regression: the
            // opacity term used the raw accumulator instead of the
            // demand-gated burn)
            _model.Color.a.ShouldBe(ExhaustSmokeModel.AlphaFloor, tolerance: 0.001f);
            _model.Density.ShouldBe(0f, tolerance: 0.001f);
        }

        [Fact]
        public void WetStack_DoesNotBurnOff_BelowDemandGate()
        {
            // accumulate
            for (var i = 0; i < FillSteps; i++)
            {
                _model.Update(1.2f, 0f, 0.5f, engineOn: true, 1f);
            }

            // demand increase but still under the burn gate
            var gatedDemand = ExhaustSmokeModel.WetStackBurnDemand * 0.8f;
            _model.Update(1.2f, gatedDemand, 0.5f, engineOn: true, 1f);

            _model.Density.ShouldBe(0f, tolerance: 0.001f);
        }

        [Fact]
        public void WetStack_BurnsOff_Completely_UnderSustainedLoad()
        {
            // idle a long time to fully accumulate
            for (var i = 0; i < FillSteps; i++)
            {
                _model.Update(1.2f, 0f, 0.3f, engineOn: true, 1f);
            }

            // then hold load long enough to burn everything off
            // (0.1s steps, 2x the drain time at this demand)
            const float burnDemand = 0.8f;
            var burnSteps = 2 * Mathf.CeilToInt(
                1f / (0.1f * burnDemand * ExhaustSmokeModel.WetStackBurnRate));
            for (var i = 0; i < burnSteps; i++)
            {
                _model.Update(1.2f, burnDemand, 0.6f, engineOn: true, 0.1f);
            }

            // after burn-off, a throttle blip must not produce straw puffs
            _model.Update(1.2f, 0.8f, 0.5f, engineOn: true, 0.016f);
            _model.Density.ShouldBe(0f, tolerance: 0.001f);
        }

        // ------------------------------------------------------------
        // helpers
        // ------------------------------------------------------------

        private static Vector3 Rgb(Color c) => new Vector3(c.r, c.g, c.b);
    }

    internal static class SmokeColorExtensions
    {
        public static Vector3 Rgb(this Color c) => new Vector3(c.r, c.g, c.b);

        public static float DistanceTo(this Vector3 v, Vector3 other) =>
            Vector3.Distance(v, other);
    }
}