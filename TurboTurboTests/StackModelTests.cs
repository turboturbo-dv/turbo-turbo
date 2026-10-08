using System;

using Shouldly;

using TurboTurbo;
using TurboTurbo.Modeling;

using Xunit;

namespace TurboTurboTests
{
    public class StackModelTests
    {
        private const float Dt = 0.1f;

        private static float Ambient => PhysicsConstants.ReferenceAmbientK;

        private static void Run(StackModel model, float seconds, float unburned, float flow, float gasTempK,
            bool engineOn = true)
        {
            var steps = (int)Math.Round(seconds / Dt);
            for (var i = 0; i < steps; i++)
            {
                model.Tick(Dt, engineOn, unburned, flow, gasTempK, Ambient);
            }
        }

        [Fact]
        public void ReferenceColdIdle_FillsInFillTime()
        {
            var s = new StackModel.Settings();
            var model = new StackModel(s, Ambient);

            // keep the wall below the capture band so the deposit rate stays at the reference
            var gasColdK = s.CaptureColdK - 20f;
            var t = 0f;
            while (t < s.FillTime * 2f && model.WetStack < 1f)
            {
                model.Tick(Dt, engineOn: true, s.ReferenceUnburned, flowNorm: 0f, gasColdK, Ambient);
                t += Dt;
            }

            t.ShouldBe(s.FillTime, tolerance: 1f);
            model.WetStack.ShouldBe(1f, tolerance: 1e-4f);
        }

        [Fact]
        public void FullStack_FullyHot_ClearsInClearTime()
        {
            var s = new StackModel.Settings();
            var model = new StackModel(s, Ambient);

            var hotK = s.ClearFullK + 100f;
            Run(model, 3600f, unburned: 0f, flow: 1f, gasTempK: hotK);
            model.ExhaustWallTempK.ShouldBeGreaterThan(s.ClearFullK);

            model.Fill();
            var t = 0f;
            while (t < s.ClearTime * 3f && model.WetStack > 0.05f)
            {
                model.Tick(Dt, engineOn: true, unburned: 0f, flowNorm: 1f, hotK, Ambient);
                t += Dt;
            }

            t.ShouldBe(s.ClearTime, tolerance: s.ClearTime * 0.05f);
        }

        [Fact]
        public void HotWall_DoesNotDeposit()
        {
            var s = new StackModel.Settings();
            var model = new StackModel(s, Ambient);
            var hotK = s.ClearFullK + 100f;

            Run(model, 3600f, unburned: 0f, flow: 1f, gasTempK: hotK);
            Run(model, 600f, unburned: s.ReferenceUnburned, flow: 0.5f, gasTempK: hotK);

            model.WetStack.ShouldBe(0f, tolerance: 1e-4f);
        }

        [Fact]
        public void LowFlow_DepositsMore_ThanHighFlow_AtEqualWallTemperature()
        {
            // a huge tau pins the wall so only the flow-dependent contact differs
            var s = new StackModel.Settings { TauWall = 1e6f };
            var low = new StackModel(s, Ambient);
            var high = new StackModel(s, Ambient);
            var gasColdK = Ambient + 50f;

            Run(low, 60f, unburned: s.ReferenceUnburned, flow: 0f, gasTempK: gasColdK);
            Run(high, 60f, unburned: s.ReferenceUnburned, flow: 1f, gasTempK: gasColdK);

            low.ExhaustWallTempK.ShouldBe(high.ExhaustWallTempK, tolerance: 0.5f);
            low.WetStack.ShouldBeGreaterThan(high.WetStack);
        }

        [Fact]
        public void SlipRate_IsPositive_AtMaximumColdness()
        {
            var s = new StackModel.Settings();
            var model = new StackModel(s, Ambient);

            model.Tick(Dt, engineOn: true, s.ReferenceUnburned, flowNorm: 0f, Ambient, Ambient);

            model.SlipRate.ShouldBeGreaterThan(0f);
            model.Vapour.ShouldBeGreaterThan(0f);
        }

        [Fact]
        public void EngineOff_ColdWall_PreservesWetStack()
        {
            var s = new StackModel.Settings { TauWall = 1e6f };
            var model = new StackModel(s, Ambient);

            Run(model, 600f, unburned: s.ReferenceUnburned, flow: 0f, gasTempK: Ambient);
            model.WetStack.ShouldBeGreaterThan(0f);

            var stored = model.WetStack;
            Run(model, 600f, unburned: 0f, flow: 0f, gasTempK: Ambient, engineOn: false);

            model.WetStack.ShouldBe(stored, tolerance: 1e-4f);
        }

        [Fact]
        public void EngineOff_HotWall_CoolsAndEvaporates()
        {
            var s = new StackModel.Settings();
            var model = new StackModel(s, Ambient);
            var hotK = s.ClearFullK + 100f;

            Run(model, 3600f, unburned: 0f, flow: 1f, gasTempK: hotK);
            model.Fill();

            var wallBefore = model.ExhaustWallTempK;
            Run(model, 60f, unburned: 0f, flow: 0f, gasTempK: hotK, engineOn: false);

            model.ExhaustWallTempK.ShouldBeLessThan(wallBefore);
            model.WetStack.ShouldBeLessThan(1f);
        }

        [Fact]
        public void Result_IsIndependentOfStepSize()
        {
            var s = new StackModel.Settings { ReferenceUnburned = 1f };
            var coarse = new StackModel(s, Ambient);
            var fine = new StackModel(s, Ambient);
            const float gasK = 500f;

            for (var i = 0; i < 600; i++)
            {
                coarse.Tick(0.1f, true, 1f, 1f, gasK, Ambient);
            }

            for (var i = 0; i < 6000; i++)
            {
                fine.Tick(0.01f, true, 1f, 1f, gasK, Ambient);
            }

            coarse.ExhaustWallTempK.ShouldBe(fine.ExhaustWallTempK, tolerance: 0.5f);
            coarse.WetStack.ShouldBe(fine.WetStack, tolerance: 0.005f);
        }

        [Fact]
        public void WetStack_IsClampedToUnit()
        {
            var s = new StackModel.Settings();
            var model = new StackModel(s, Ambient);

            Run(model, 1000f, unburned: s.ReferenceUnburned, flow: 0f, gasTempK: Ambient);

            model.WetStack.ShouldBe(1f, tolerance: 1e-4f);
        }

        [Fact]
        public void Validate_OrdersTemperatureBands()
        {
            var s = new StackModel.Settings
            {
                CaptureColdK = 600f,
                CaptureHotK = 400f,
                ClearStartK = 300f,
                ClearFullK = 200f,
            };
            s.Validate();

            s.CaptureColdK.ShouldBeLessThan(s.CaptureHotK);
            s.CaptureHotK.ShouldBeLessThanOrEqualTo(s.ClearStartK);
            s.ClearStartK.ShouldBeLessThan(s.ClearFullK);
        }

        [Fact]
        public void Validate_ClampsContactAndStick()
        {
            var s = new StackModel.Settings
            {
                ContactIdle = 0.2f,
                ContactFullFlow = 0.9f,
                StickMax = 2f,
            };
            s.Validate();

            s.ContactFullFlow.ShouldBeLessThanOrEqualTo(s.ContactIdle);
            s.StickMax.ShouldBe(1f);
        }

        [Fact]
        public void Settings_CopyConstructor_IsIndependent()
        {
            var template = new StackModel.Settings { FillTime = 90f, StickMax = 0.5f };
            var clone = new StackModel.Settings(template);

            clone.FillTime.ShouldBe(90f);
            clone.StickMax.ShouldBe(0.5f);

            clone.FillTime = 30f;
            template.FillTime.ShouldBe(90f);
        }
    }
}
