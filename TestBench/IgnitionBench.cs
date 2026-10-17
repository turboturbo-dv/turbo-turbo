using System;
using System.Collections.Generic;

using Spectre.Console;
using Spectre.Console.Rendering;

using TurboTurbo;
using TurboTurbo.Modeling;

namespace TestBench
{
    /// <summary>
    /// Reports on whether the injected fuel ignites in time, and what happens to the fuel that doesn't. Late-burned
    /// and unburned fuel are shown relative to the stack model's reference unburned rate.
    /// </summary>
    public sealed class IgnitionBench
    {
        private static readonly float[] Ambients = { -20f, 0f, 20f, 40f };

        // dense early samples, since the chamber surfaces settle within the first minute
        private static readonly float[] IdleTimes = { 2f, 5f, 10f, 20f, 30f, 60f, 120f, 300f, 600f };
        private static readonly float[] LoadTimes = { 2f, 5f, 10f, 20f, 30f, 60f, 120f };

        private readonly CombustionModel.Settings _settings;
        private readonly ChargerKind _charger;

        public IgnitionBench(CombustionModel.Settings settings, ChargerKind charger = ChargerKind.Turbo)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _charger = charger;
        }

        /// <summary>Builds the report. Without <paramref name="full"/>, cranking and cold start to load are left out.</summary>
        public IRenderable Report(float ambientK, bool full = true)
        {
            var parts = new List<IRenderable>
            {
                OperatingPointsReport(ambientK),
                new Rule(),
                ColdStartIdleReport(),
            };

            if (full)
            {
                parts.Add(new Rule());
                parts.Add(CrankingReport());
                parts.Add(new Rule());
                parts.Add(ColdStartToLoadReport());
            }

            return new Rows(parts);
        }

        public IRenderable OperatingPointsReport(float ambientK)
        {
            var table = new Table()
                .RoundedBorder()
                .Title($"Operating points (settled, {_charger})  " +
                       $"[grey]ambient {BenchCommon.ToC(ambientK):0.#} C, dt {BenchCommon.Dt}s[/]");
            table.AddColumn(Render.Name("point"));
            foreach (var h in new[]
                     {
                         "TcK", "pcBar", "m", "bTemp", "inj", "work", "late", "unburned", "bAir", "lambda", "EE", "boost",
                     })
            {
                table.AddColumn(Render.Num(h));
            }

            foreach (var (name, rpm, fps) in BenchCommon.Points)
            {
                var inputs = BenchCommon.LoadInputs(_charger, rpm, fps, ambientK);
                var trace = BenchCommon.Measure(inputs.Build(_settings));

                table.AddRow(
                    name,
                    $"{trace.SettleCompressionK:0}",
                    $"{trace.SettleCompressionPressure:0.0}",
                    Render.Margin(trace.SettleIgnitionMargin, _settings.IgnitionSpread),
                    Render.Fraction(trace.SettleBurnTemp),
                    $"{trace.SettleInjected:0.000}",
                    Render.Fraction(trace.SettleWorkEfficiency),
                    Render.Unburned(trace.SettleLateBurned),
                    Render.Unburned(trace.SettleUnburned),
                    Render.Fraction(trace.SettleBurnAir),
                    $"{trace.SettleLambda:0.00}",
                    $"{trace.SettleExhaustEnergy:0.000}",
                    $"{trace.SettleBoost:0.00}");
            }

            return table;
        }

        public IRenderable CrankingReport()
        {
            const float crankS = 5f;
            var table = new Table()
                .RoundedBorder()
                .Title($"Cranking ({_charger})  [grey]no game fuel, {crankS:0}s of cranking, dt {BenchCommon.Dt}s[/]");
            table.AddColumn(Render.Name("ambient"));
            table.AddColumn(Render.Name("block"));
            foreach (var h in new[] { "rpm", "intakeC", "n", "TcK", "pcBar", "m", "bTemp", "late", "unburned" })
            {
                table.AddColumn(Render.Num(h));
            }

            foreach (var c in Ambients)
            {
                var ambientK = PhysicsConstants.KelvinOffset + c;
                foreach (var (blockLabel, blockK) in new[] { ("cold", ambientK), ("warm", PhysicsConstants.KelvinOffset + 80f) })
                {
                    // the starter motors the engine until it latches at 0.4 * idle rpm, about 0.13 on the DE6 (idle 315 of 950)
                    foreach (var rpm in new[] { 0.065f, 0.13f })
                    {
                        var inputs = new BenchCommon.Inputs { Rpm = rpm, Fuel = 0f, AmbientK = ambientK, Charger = _charger };
                        var model = inputs.Build(_settings);
                        model.SetTemperatureK(blockK);
                        for (var step = 0; step < (int)(crankS / BenchCommon.Dt); step++)
                        {
                            model.Tick(BenchCommon.Dt, engineOn: false, cranking: true);
                        }

                        var s = Snapshot(model, "");
                        table.AddRow(
                            $"{c:0.#} C",
                            blockLabel,
                            $"{rpm:0.00}",
                            $"{BenchCommon.ToC(s.IntakeK):0.0}",
                            $"{s.Exponent:0.000}",
                            $"{s.CompressionK:0}",
                            $"{s.CompressionPressure:0.0}",
                            Render.Margin(s.Margin, _settings.IgnitionSpread),
                            Render.Fraction(s.BurnTemp),
                            Render.Unburned(s.LateBurned),
                            Render.Unburned(s.Unburned));
                    }
                }
            }

            return table;
        }

        public IRenderable ColdStartIdleReport() =>
            ColdStartTable("Cold start idle", 0.3f, 0.10f, IdleTimes, Ambients, warm: true);

        public IRenderable ColdStartToLoadReport() =>
            ColdStartTable("Cold start to full load", 1f, 1f, LoadTimes, new[] { -20f, 20f }, warm: false);

        private IRenderable ColdStartTable(string title, float rpm, float fps, float[] times, float[] ambients,
            bool warm)
        {
            var table = new Table()
                .RoundedBorder()
                .Title($"{title} ({_charger})  [grey]rpm {rpm:0.0}, fps {fps:0.00}, dt {BenchCommon.Dt}s[/]");
            table.AddColumn(Render.Name("ambient"));
            table.AddColumn(Render.Name("time"));
            foreach (var h in new[]
                     {
                         "blockC", "wallC", "intakeC", "n", "TcK", "pcBar", "m", "bTemp", "late", "unburned",
                     })
            {
                table.AddColumn(Render.Num(h));
            }

            foreach (var c in ambients)
            {
                var ambientK = PhysicsConstants.KelvinOffset + c;
                var samples = ColdStartSamples(rpm, fps, ambientK, times, warm);
                for (var i = 0; i < samples.Count; i++)
                {
                    var s = samples[i];
                    table.AddRow(
                        i == 0 ? $"{c:0.#} C" : "",
                        s.Label,
                        $"{BenchCommon.ToC(s.BlockK):0.0}",
                        $"{BenchCommon.ToC(s.WallK):0.0}",
                        $"{BenchCommon.ToC(s.IntakeK):0.0}",
                        $"{s.Exponent:0.000}",
                        $"{s.CompressionK:0}",
                        $"{s.CompressionPressure:0.0}",
                        Render.Margin(s.Margin, _settings.IgnitionSpread),
                        Render.Fraction(s.BurnTemp),
                        Render.Unburned(s.LateBurned),
                        Render.Unburned(s.Unburned));
                }
            }

            return table;
        }

        private List<ColdSample> ColdStartSamples(float rpm, float fps, float ambientK, float[] times, bool warm)
        {
            var model = BenchCommon.LoadInputs(_charger, rpm, fps, ambientK).Build(_settings);

            var samples = new List<ColdSample>();
            var block = new List<float>();
            var window = (int)(BenchCommon.SettleWindowS / BenchCommon.Dt);
            var maxSteps = (int)(BenchCommon.MaxRunS / BenchCommon.Dt);
            var next = 0;

            for (var step = 0; step < maxSteps; step++)
            {
                model.Tick(BenchCommon.Dt, engineOn: true);
                var t = (step + 1) * BenchCommon.Dt;
                block.Add(model.EngineTempK);

                while (next < times.Length && t >= times[next] - 1e-4f)
                {
                    samples.Add(Snapshot(model, BenchCommon.Fmt(times[next])));
                    next++;
                }

                if (next < times.Length)
                {
                    continue;
                }

                if (!warm)
                {
                    break;
                }

                if (step >= window && Math.Abs(block[step] - block[step - window]) < BenchCommon.SettleEpsK)
                {
                    samples.Add(Snapshot(model, "warm"));
                    break;
                }
            }

            if (warm && samples[samples.Count - 1].Label != "warm")
            {
                samples.Add(Snapshot(model, "warm"));
            }

            return samples;
        }

        private static ColdSample Snapshot(CombustionModel model, string label) => new ColdSample
        {
            Label = label,
            BlockK = model.EngineTempK,
            WallK = model.ChamberTempK,
            IntakeK = model.IntakeTempK,
            CompressionK = model.CompressionTempK,
            CompressionPressure = model.CompressionPressure,
            Exponent = model.CompressionExponent,
            Margin = model.IgnitionMargin,
            BurnTemp = model.BurnFractionTemp,
            LateBurned = model.LateBurnedFuel / BenchCommon.ReferenceUnburned,
            Unburned = model.UnburnedFuel / BenchCommon.ReferenceUnburned,
        };

        private sealed class ColdSample
        {
            public string Label;
            public float BlockK;
            public float WallK;
            public float IntakeK;
            public float CompressionK;
            public float CompressionPressure;
            public float Exponent;
            public float Margin;
            public float BurnTemp;
            public float LateBurned;
            public float Unburned;
        }
    }
}
