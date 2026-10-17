using System;
using System.Collections.Generic;

using Spectre.Console;
using Spectre.Console.Rendering;

using TurboTurbo;
using TurboTurbo.Modeling;

namespace TestBench
{
    /// <summary>Reports on where the engine's heat goes, and how quickly the block and chamber respond.</summary>
    public sealed class ThermalBench
    {
        private readonly CombustionModel.Settings _settings;
        private readonly ChargerKind _charger;

        public ThermalBench(CombustionModel.Settings settings, ChargerKind charger = ChargerKind.Turbo)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _charger = charger;
        }

        public IRenderable Report(float ambientK)
        {
            return new Rows(new IRenderable[]
            {
                OperatingPointsReport(ambientK),
                new Rule(),
                ThrottleStepReport(ambientK),
                new Rule(),
                AmbientSensitivityReport(),
                new Rule(),
                CooldownReport(ambientK),
            });
        }

        public IRenderable OperatingPointsReport(float ambientK)
        {
            var settle = new Table()
                .RoundedBorder()
                .Title($"Operating points (settle, {_charger})  " +
                       $"[grey]ambient {BenchCommon.ToC(ambientK):0.#} C, dt {BenchCommon.Dt}s[/]");
            settle.AddColumn(Render.Name("point"));
            foreach (var h in new[] { "rpm", "fps", "block", "cyl", "therm", "rise1m" })
            {
                settle.AddColumn(Render.Num(h));
            }

            var response = new Table()
                .RoundedBorder()
                .Title("Operating points (thermal response)");
            response.AddColumn(Render.Name("point"));
            foreach (var h in new[] { "t63", "t90", "tauS", "tOpen" })
            {
                response.AddColumn(Render.Num(h));
            }

            foreach (var (name, rpm, fps) in BenchCommon.Points)
            {
                var inputs = BenchCommon.LoadInputs(_charger, rpm, fps, ambientK);
                var trace = BenchCommon.Measure(inputs.Build(_settings));

                var t63 = BenchCommon.FirstCrossing(trace.Block, ambientK + ((trace.SettleBlockK - ambientK) * 0.63f));
                var t90 = BenchCommon.FirstCrossing(trace.Block, ambientK + ((trace.SettleBlockK - ambientK) * 0.90f));
                var tau = BenchCommon.TimeConstant(trace.Block, ambientK, trace.SettleBlockK);
                var tOpen = BenchCommon.FirstCrossing(trace.Thermostat, 0.001f);
                var rise1m = trace.Block[Math.Min(trace.Block.Count - 1, (int)(60f / BenchCommon.Dt))] - ambientK;

                settle.AddRow(
                    name,
                    $"{rpm:0.0}",
                    $"{fps:0.00}",
                    $"{BenchCommon.ToC(trace.SettleBlockK):0.0}",
                    $"{BenchCommon.ToC(trace.SettleCylinderK):0.0}",
                    $"{trace.SettleThermostat:0.00}",
                    $"{rise1m:0.0}");

                response.AddRow(
                    name,
                    Render.Duration(t63),
                    Render.Duration(t90),
                    Render.Duration(tau),
                    Render.Duration(tOpen));
            }

            return new Columns(new IRenderable[] { settle, response }) { Expand = false };
        }

        public IRenderable AmbientSensitivityReport()
        {
            var table = new Table()
                .RoundedBorder()
                .Title($"Settle vs ambient ({_charger})  [grey]idle = rpm 0.3 fps 0.1, full = rpm 1.0 fps 1.0[/]");

            table.AddColumn(Render.Name("point"));
            foreach (var h in new[] { "ambientC", "blockC", "cylC", "intakeC", "therm", "blockAboveAmbientK" })
            {
                table.AddColumn(Render.Num(h));
            }

            var loads = new (string Name, float Rpm, float Fps)[]
            {
                ("idle", 0.3f, 0.10f),
                ("full", 1.0f, 1.00f),
            };

            foreach (var (name, rpm, fps) in loads)
            {
                foreach (var c in new[] { -20f, 0f, 20f, 40f })
                {
                    var ambientK = PhysicsConstants.KelvinOffset + c;
                    var inputs = BenchCommon.LoadInputs(_charger, rpm, fps, ambientK);
                    var trace = BenchCommon.Measure(inputs.Build(_settings));

                    table.AddRow(
                        name,
                        $"{c:0.#}",
                        $"{BenchCommon.ToC(trace.SettleBlockK):0.0}",
                        $"{BenchCommon.ToC(trace.SettleCylinderK):0.0}",
                        $"{BenchCommon.ToC(trace.SettleIntakeK):0.0}",
                        $"{trace.SettleThermostat:0.00}",
                        $"{trace.SettleBlockK - ambientK:0.0}");
                }
            }

            return table;
        }

        public IRenderable CooldownReport(float ambientK)
        {
            var inputs = BenchCommon.LoadInputs(_charger, 1f, 1f, ambientK);
            var model = inputs.Build(_settings);
            var warm = BenchCommon.Measure(model);

            inputs.Fuel = 0f;
            inputs.Rpm = 0f;

            var openK = CombustionModel.Settings.ThermostatOpenK;
            var halfTarget = ambientK + ((warm.SettleBlockK - ambientK) * 0.5f);
            var toOpen = -1f;
            var halfLife = -1f;
            var toAmbient5 = -1f;

            for (var step = 0; step < (int)(BenchCommon.MaxRunS / BenchCommon.Dt); step++)
            {
                model.Tick(BenchCommon.Dt, engineOn: false);
                var t = (step + 1) * BenchCommon.Dt;
                if (toOpen < 0f && model.EngineTempK <= openK)
                {
                    toOpen = t;
                }

                if (halfLife < 0f && model.EngineTempK <= halfTarget)
                {
                    halfLife = t;
                }

                if (toAmbient5 < 0f && model.EngineTempK <= ambientK + 5f)
                {
                    toAmbient5 = t;
                }

                if (toAmbient5 >= 0f)
                {
                    break;
                }
            }

            var table = new Table().Border(TableBorder.None).HideHeaders();
            table.AddColumn(Render.Name("metric"));
            table.AddColumn(Render.Num("value"));
            table.AddRow("warm block", $"{BenchCommon.ToC(warm.SettleBlockK):0.0} C");
            table.AddRow("thermostat closes", Render.Duration(toOpen));
            table.AddRow("excess half-life", Render.Duration(halfLife));
            table.AddRow("block within 5 C of ambient", Render.Duration(toAmbient5));

            return new Panel(table)
                .Header($"Cooldown after full load (engine off, {_charger})  " +
                        $"[grey]ambient {BenchCommon.ToC(ambientK):0.#} C[/]")
                .RoundedBorder();
        }

        public IRenderable ThrottleStepReport(float ambientK)
        {
            var table = new Table()
                .RoundedBorder()
                .Title($"Throttle step to full load ({_charger})  [grey]ambient {BenchCommon.ToC(ambientK):0.#} C, " +
                       (_charger == ChargerKind.Turbo ? "decoupled turbo, " : "") + $"dt {BenchCommon.Dt}s[/]");

            table.AddColumn(Render.Name("step"));
            foreach (var h in new[]
                     {
                         "from", "to", "dCyl", "dBlk", "t63", "t90", "tau", "r5", "r10", "r30",
                     })
            {
                table.AddColumn(Render.Num(h));
            }

            var baselines = new (string Name, float Rpm, float Fps)[]
            {
                ("idle", 0.3f, 0.10f),
                ("cruise", 0.7f, 0.50f),
            };

            foreach (var (name, baseRpm, baseFps) in baselines)
            {
                var inputs = BenchCommon.LoadInputs(_charger, baseRpm, baseFps, ambientK);
                var model = inputs.Build(_settings);
                BenchCommon.Measure(model); // warm to the baseline load

                var baseCyl = model.ChamberTempK;
                var baseBlock = model.EngineTempK;

                inputs.Rpm = 1f;
                inputs.Fuel = 1f;

                var cyl = new List<float>();
                var block = new List<float>();
                var excess = new List<float>();
                var window = (int)(BenchCommon.SettleWindowS / BenchCommon.Dt);
                var maxSteps = (int)(BenchCommon.MaxRunS / BenchCommon.Dt);
                for (var step = 0; step < maxSteps; step++)
                {
                    model.Tick(BenchCommon.Dt, engineOn: true);
                    cyl.Add(model.ChamberTempK);
                    block.Add(model.EngineTempK);
                    excess.Add(model.ChamberTempK - model.EngineTempK);

                    if (step >= window && Math.Abs(block[step] - block[step - window]) < BenchCommon.SettleEpsK)
                    {
                        break;
                    }
                }

                var endCyl = model.ChamberTempK;
                var endBlock = model.EngineTempK;
                var baseExcess = baseCyl - baseBlock;
                var endExcess = endCyl - endBlock;

                // the cylinder-above-block gap isolates TauChamber: the block moves slowly, so
                // fitting this signal avoids the slow block rise dominating the time constant
                var t63 = BenchCommon.FirstCrossing(excess, baseExcess + ((endExcess - baseExcess) * 0.63f));
                var t90 = BenchCommon.FirstCrossing(excess, baseExcess + ((endExcess - baseExcess) * 0.90f));
                var tau = BenchCommon.TimeConstant(excess, baseExcess, endExcess);

                var r5 = cyl[Math.Min(cyl.Count - 1, (int)(5f / BenchCommon.Dt))] - baseCyl;
                var r10 = cyl[Math.Min(cyl.Count - 1, (int)(10f / BenchCommon.Dt))] - baseCyl;
                var r30 = cyl[Math.Min(cyl.Count - 1, (int)(30f / BenchCommon.Dt))] - baseCyl;

                table.AddRow(
                    name,
                    $"{BenchCommon.ToC(baseCyl):0.0}",
                    $"{BenchCommon.ToC(endCyl):0.0}",
                    $"{endCyl - baseCyl:0.0}",
                    $"{endBlock - baseBlock:0.0}",
                    Render.Duration(t63),
                    Render.Duration(t90),
                    Render.Duration(tau),
                    $"{r5:0.0}",
                    $"{r10:0.0}",
                    $"{r30:0.0}");
            }

            return table;
        }
    }
}
