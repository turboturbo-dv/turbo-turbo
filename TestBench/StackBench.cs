using System;
using System.Collections.Generic;

using Spectre.Console;
using Spectre.Console.Rendering;

using TurboTurbo.Modeling;

using UnityEngine;

namespace TestBench
{
    /// <summary>Reports on the exhaust stack fouling model, driven by a combustion model.</summary>
    public sealed class StackBench
    {
        private readonly CombustionModel.Settings _settings;
        private readonly StackModel.Settings _stackSettings;

        public StackBench(CombustionModel.Settings settings, StackModel.Settings stackSettings)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _stackSettings = stackSettings ?? new StackModel.Settings();
        }

        public IRenderable StackReport(float ambientK)
        {
            return new Rows(new IRenderable[]
            {
                new Rule($"[yellow]Wet stack[/]  [grey]ambient {BenchCommon.ToC(ambientK):0.#} C, dt {BenchCommon.Dt}s[/]"),
                ColdStartTable(ambientK),
                DryOutTable(ambientK),
                IdleStepPanel(ambientK),
            });
        }

        private IRenderable ColdStartTable(float ambientK)
        {
            var table = new Table()
                .RoundedBorder()
                .Title("Cold start to steady state");

            table.AddColumn(Render.Name("point"));
            foreach (var h in new[] { "rpm", "fps", "manif", "wall", "wet", "t50", "tFull", "slip", "vapour" })
            {
                table.AddColumn(Render.Num(h));
            }

            foreach (var (name, rpm, fps) in BenchCommon.Points)
            {
                var inputs = new BenchCommon.Inputs { Rpm = rpm, Fuel = rpm * fps, AmbientK = ambientK };
                var model = inputs.Build(_settings);
                var stack = new StackModel(_stackSettings, ambientK);

                var wall = new List<float>();
                var wet = new List<float>();
                var window = (int)(BenchCommon.SettleWindowS / BenchCommon.Dt);
                var maxSteps = (int)(BenchCommon.MaxRunS / BenchCommon.Dt);
                for (var step = 0; step < maxSteps; step++)
                {
                    TickPair(model, stack, ambientK);
                    wall.Add(stack.ExhaustWallTempK);
                    wet.Add(stack.WetStack);

                    if (step >= window
                        && Math.Abs(wall[step] - wall[step - window]) < BenchCommon.SettleEpsK
                        && Math.Abs(wet[step] - wet[step - window]) < 1e-4f)
                    {
                        break;
                    }
                }

                var t50 = BenchCommon.FirstCrossing(wet, 0.5f);
                var tFull = BenchCommon.FirstCrossing(wet, 0.999f);

                table.AddRow(
                    name,
                    $"{rpm:0.0}",
                    $"{fps:0.00}",
                    $"{BenchCommon.ToC(model.GasTemperature + model.TurbineTemperatureDropK):0.0}",
                    $"{BenchCommon.ToC(stack.ExhaustWallTempK):0.0}",
                    $"{stack.WetStack:0.00}",
                    Render.Duration(t50),
                    Render.Duration(tFull),
                    $"{stack.SlipRate:0.000}",
                    $"{stack.Vapour:0.00}");
            }

            return table;
        }

        private IRenderable DryOutTable(float ambientK)
        {
            var table = new Table()
                .RoundedBorder()
                .Title("Dry-out from a full stack  [grey]wall warmed first[/]");

            table.AddColumn(Render.Name("point"));
            foreach (var h in new[] { "rpm", "fps", "manif", "peakVap", "t50", "t05", "meanEvap" })
            {
                table.AddColumn(Render.Num(h));
            }

            var dryLoads = new (string Name, float Rpm, float Fps)[]
            {
                ("lug", 0.6f, 1.00f),
                ("cruise", 0.7f, 0.50f),
                ("full", 1.0f, 1.00f),
            };

            foreach (var (name, rpm, fps) in dryLoads)
            {
                var inputs = new BenchCommon.Inputs { Rpm = rpm, Fuel = rpm * fps, AmbientK = ambientK };
                var model = inputs.Build(_settings);
                var stack = new StackModel(_stackSettings, ambientK);

                Warm(model, stack, ambientK);
                stack.Fill();

                var peak = 0f;
                var t50 = -1f;
                var t05 = -1f;
                double evapSum = 0;
                var steps = 0;
                for (var step = 0; step < (int)(BenchCommon.MaxRunS / BenchCommon.Dt); step++)
                {
                    TickPair(model, stack, ambientK);
                    if (stack.Vapour > peak) peak = stack.Vapour;
                    evapSum += stack.EvaporateRate;
                    steps++;

                    var t = (step + 1) * BenchCommon.Dt;
                    if (t50 < 0f && stack.WetStack <= 0.5f) t50 = t;
                    if (t05 < 0f && stack.WetStack <= 0.05f)
                    {
                        t05 = t;
                        break;
                    }
                }

                var meanEvap = steps > 0 ? (float)(evapSum / steps) : 0f;

                table.AddRow(
                    name,
                    $"{rpm:0.0}",
                    $"{fps:0.00}",
                    $"{BenchCommon.ToC(model.GasTemperature + model.TurbineTemperatureDropK):0.0}",
                    $"{peak:0.00}",
                    Render.Duration(t50),
                    Render.Duration(t05),
                    $"{meanEvap:0.000}");
            }

            return table;
        }

        private IRenderable IdleStepPanel(float ambientK)
        {
            var idleInputs = new BenchCommon.Inputs { Rpm = 0.3f, Fuel = 0.03f, AmbientK = ambientK };
            var idleModel = idleInputs.Build(_settings);
            var idleStack = new StackModel(_stackSettings, ambientK);
            for (var step = 0; step < (int)(BenchCommon.MaxRunS / BenchCommon.Dt); step++)
            {
                TickPair(idleModel, idleStack, ambientK);
                if (idleStack.WetStack >= 0.999f) break;
            }

            var wetIdle = idleStack.WetStack;
            idleInputs.Rpm = 1f;
            idleInputs.Fuel = 1f;
            var stepPeak = 0f;
            var stepT05 = -1f;
            for (var step = 0; step < (int)(BenchCommon.MaxRunS / BenchCommon.Dt); step++)
            {
                TickPair(idleModel, idleStack, ambientK);
                if (idleStack.Vapour > stepPeak) stepPeak = idleStack.Vapour;
                if (idleStack.WetStack <= 0.05f)
                {
                    stepT05 = (step + 1) * BenchCommon.Dt;
                    break;
                }
            }

            var table = new Table().Border(TableBorder.None).HideHeaders();
            table.AddColumn(Render.Name("metric"));
            table.AddColumn(Render.Num("value"));
            table.AddRow("idle wet stack", $"{wetIdle:0.00}");
            table.AddRow("peak vapour", $"{stepPeak:0.00}");
            table.AddRow("clear to 5%", Render.Duration(stepT05));

            return new Panel(table)
                .Header("Idle -> full step after a full stack")
                .RoundedBorder();
        }

        private static void Warm(CombustionModel model, StackModel stack, float ambientK)
        {
            var wall = new List<float>();
            var window = (int)(BenchCommon.SettleWindowS / BenchCommon.Dt);
            var maxSteps = (int)(BenchCommon.MaxRunS / BenchCommon.Dt);
            for (var step = 0; step < maxSteps; step++)
            {
                model.Tick(BenchCommon.Dt, engineOn: true);
                stack.Tick(BenchCommon.Dt, engineOn: true, unburned: 0f, FlowNorm(model), model.GasTemperature, ambientK);
                wall.Add(stack.ExhaustWallTempK);

                if (step >= window && Math.Abs(wall[step] - wall[step - window]) < BenchCommon.SettleEpsK)
                {
                    break;
                }
            }
        }

        private static void TickPair(CombustionModel model, StackModel stack, float ambientK)
        {
            model.Tick(BenchCommon.Dt, engineOn: true);
            var unburned = model.FuelNorm * (1f - model.BurnFractionTemp);
            stack.Tick(BenchCommon.Dt, engineOn: true, unburned, FlowNorm(model), model.GasTemperature, ambientK);
        }

        private static float FlowNorm(CombustionModel model) =>
            Mathf.Clamp01(model.MassFlow / Mathf.Max(1e-4f, model.Charger.ChargeAtFullPower));
    }
}
