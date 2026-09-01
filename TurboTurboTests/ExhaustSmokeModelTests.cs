using Shouldly;
using TurboTurbo.Modeling;
using UnityEngine;
using Xunit;

namespace TurboTurboTests
{
    /// <summary>
    /// Behavioural tests for the ExhaustSmokeModel: engine-off guard,
    /// lambda-driven soot ladder, oil blowby tint, wet-stack accumulate and
    /// burn-off, and density monotonicity.
    /// </summary>
    public class ExhaustSmokeModelTests
    {
        private readonly ExhaustSmokeModel _model = new ExhaustSmokeModel();

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
            _model.Color.a.ShouldBe(0.22f, tolerance: 0.01f);
            _model.Color.r.ShouldBe(0.62f, tolerance: 0.01f);
            _model.Color.g.ShouldBe(0.59f, tolerance: 0.01f);
            _model.Color.b.ShouldBe(0.51f, tolerance: 0.01f);
        }

        [Fact]
        public void LambdaAtOpaqueThreshold_FullySoots()
        {
            // defaults: onset 0.85, opaque 0.45 -> lambda 0.45 = soot factor 1
            _model.Update(0.45f, 0.8f, 0.5f, engineOn: true, 0.016f);

            _model.Density.ShouldBe(1f, tolerance: 0.01f);
            _model.Color.a.ShouldBe(0.95f, tolerance: 0.01f);
        }

        [Fact]
        public void LambdaBetweenThresholds_GivesIntermediateDensity()
        {
            // defaults: onset 0.85, opaque 0.45 -> lambda 0.65 is midway
            _model.Update(0.65f, 0.8f, 0.5f, engineOn: true, 0.016f);

            _model.Density.ShouldBeInRange(0.2f, 0.9f);
            _model.Color.a.ShouldBeLessThan(0.95f);
        }

        [Fact]
        public void SootDensity_MonotonicallyIncreases_AsLambdaFalls()
        {
            var model = new ExhaustSmokeModel();
            float last = -1f;
            for (float lambda = 2f; lambda >= 0.3f; lambda -= 0.05f)
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

            var oilBurn = new Vector3(0.44f, 0.52f, 0.55f);
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
            // idle: fill the accumulator (fill rate 0.08/s, 1s steps)
            for (int i = 0; i < 300; i++)
            {
                _model.Update(1.2f, 0f, 0.3f, engineOn: true, 1f);
            }

            // throttle up: burn-off must drive density and straw tint
            _model.Update(1.2f, 0.5f, 0.5f, engineOn: true, 0.1f);

            _model.Color.r.ShouldBeGreaterThan(0.62f, "wet-stack burn should push red above the haze base");
            _model.Color.a.ShouldBeGreaterThanOrEqualTo(0.6f);
            _model.Density.ShouldBeGreaterThan(0f);
        }

        [Fact]
        public void WetStack_DoesNotBurnOff_BelowDemandGate()
        {
            // accumulate
            for (int i = 0; i < 20; i++)
            {
                _model.Update(1.2f, 0f, 0.5f, engineOn: true, 1f);
            }

            // small demand increase but still under the 0.15 burn gate
            _model.Update(1.2f, 0.12f, 0.5f, engineOn: true, 1f);

            _model.Density.ShouldBe(0f, tolerance: 0.001f);
        }

        [Fact]
        public void WetStack_BurnsOff_Completely_UnderSustainedLoad()
        {
            // idle a long time to fully accumulate
            for (int i = 0; i < 300; i++)
            {
                _model.Update(1.2f, 0f, 0.3f, engineOn: true, 1f);
            }
            // then hold load long enough to burn everything off
            for (int i = 0; i < 200; i++)
            {
                _model.Update(1.2f, 0.8f, 0.6f, engineOn: true, 0.1f);
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
