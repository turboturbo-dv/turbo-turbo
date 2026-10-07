using System;
using System.Collections.Generic;
using System.Text;

using TurboTurbo;
using TurboTurbo.Modeling;

namespace TestBench
{
    public sealed class ThermalBench
    {
        public const float Dt = 0.1f;

        private const float SettleWindowS = 600f;
        private const float SettleEpsK = 0.01f;
        private const float MaxRunS = 6f * 3600f;

        private static readonly (string Name, float Rpm, float FuelPerStroke)[] Points =
        {
            ("idle", 0.3f, 0.10f),
            ("light", 0.5f, 0.25f),
            ("cruise", 0.7f, 0.50f),
            ("heavy", 0.9f, 0.80f),
            ("full", 1.0f, 1.00f),
        };

        private readonly CombustionModel.Settings _settings;

        public ThermalBench(CombustionModel.Settings settings)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        public string OperatingPointsReport(float ambientK)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Operating points at ambient {ToC(ambientK):0.#} C (dt {Dt}s)");
            sb.AppendLine(
                $"{"point",-8} {"rpm",4} {"fps",5} | {"blockC",6} {"cylC",5} | " +
                $"{"therm",5} {"burnAir",7} {"burnTemp",8} | {"lambda",6} {"EE",6} {"boost",5} | " +
                $"{"t63s",7} {"t90s",7} {"tauS",6} {"tOpenS",7} {"rise1m",6}");

            foreach (var (name, rpm, fps) in Points)
            {
                var inputs = new Inputs { Rpm = rpm, Fuel = rpm * fps, AmbientK = ambientK };
                var trace = Measure(inputs.Build(_settings), ambientK);

                var t63 = FirstCrossing(trace.Block, ambientK + ((trace.SettleBlockK - ambientK) * 0.63f));
                var t90 = FirstCrossing(trace.Block, ambientK + ((trace.SettleBlockK - ambientK) * 0.90f));
                var tau = TimeConstant(trace.Block, ambientK, trace.SettleBlockK);
                var tOpen = FirstCrossing(trace.Thermostat, 0.001f);
                var rise1m = trace.Block[(int)(60f / Dt)] - ambientK;

                sb.AppendLine(
                    $"{name,-8} {rpm,4:0.0} {fps,5:0.00} | " +
                    $"{ToC(trace.SettleBlockK),6:0.0} {ToC(trace.SettleCylinderK),5:0.0} | " +
                    $"{trace.SettleThermostat,5:0.00} {trace.SettleBurnAir,7:0.000} {trace.SettleBurnTemp,8:0.000} | " +
                    $"{trace.SettleLambda,6:0.00} {trace.SettleExhaustEnergy,6:0.000} {trace.SettleBoost,5:0.00} | " +
                    $"{Fmt(t63),7} {Fmt(t90),7} {Fmt(tau),6} {Fmt(tOpen),7} {rise1m,6:0.0}");
            }

            return sb.ToString();
        }

        public string AmbientSensitivityReport()
        {
            var sb = new StringBuilder();
            sb.AppendLine("Settle vs ambient (idle = rpm 0.3 fps 0.1, full = rpm 1.0 fps 1.0)");
            sb.AppendLine(
                $"{"point",-7} {"ambientC",8}   {"blockC",6}  {"cylC",5}   {"therm",5}   blockAboveAmbientK");

            var loads = new (string Name, float Rpm, float Fps)[]
            {
                ("idle", 0.3f, 0.10f),
                ("full", 1.0f, 1.00f),
            };

            foreach (var (name, rpm, fps) in loads)
            {
                foreach (var c in new[] { 0f, 20f, 40f })
                {
                    var ambientK = PhysicsConstants.KelvinOffset + c;
                    var inputs = new Inputs { Rpm = rpm, Fuel = rpm * fps, AmbientK = ambientK };
                    var trace = Measure(inputs.Build(_settings), ambientK);

                    sb.AppendLine(
                        $"{name,-7} {c,8:0.#}   {ToC(trace.SettleBlockK),6:0.0}  {ToC(trace.SettleCylinderK),5:0.0}   " +
                        $"{trace.SettleThermostat,5:0.00}   {trace.SettleBlockK - ambientK,8:0.0}");
                }
            }

            return sb.ToString();
        }

        public string CooldownReport(float ambientK)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Cooldown after full load to idle (ambient {ToC(ambientK):0.#} C)");

            var inputs = new Inputs { Rpm = 1f, Fuel = 1f, AmbientK = ambientK };
            var model = inputs.Build(_settings);
            var warm = Measure(model, ambientK);

            inputs.Fuel = 0f;
            inputs.Rpm = 0.3f;

            var openK = CombustionModel.Settings.ThermostatOpenK;
            var halfTarget = ambientK + ((warm.SettleBlockK - ambientK) * 0.5f);
            var toOpen = -1f;
            var halfLife = -1f;
            var toAmbient5 = -1f;

            for (var step = 0; step < (int)(MaxRunS / Dt); step++)
            {
                model.Tick(Dt, engineOn: true);
                var t = (step + 1) * Dt;
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

            sb.AppendLine($"warm block                 : {ToC(warm.SettleBlockK):0.0} C");
            sb.AppendLine($"thermostat closes          : {Fmt(toOpen)}");
            sb.AppendLine($"excess half-life           : {Fmt(halfLife)}");
            sb.AppendLine($"block within 5 C of ambient: {Fmt(toAmbient5)}");
            return sb.ToString();
        }

        public string ThrottleStepReport(float ambientK)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Throttle step to full load at ambient {ToC(ambientK):0.#} C (decoupled turbo, dt {Dt}s)");
            sb.AppendLine(
                $"{"step",-8} {"fromC",6} {"toC",5} | {"dCyl",5} {"dBlock",6} | " +
                $"{"excT63",7} {"excT90",7} {"excTau",6} | {"rise5",6} {"rise10",7} {"rise30",7}");

            var baselines = new (string Name, float Rpm, float Fps)[]
            {
                ("idle", 0.3f, 0.10f),
                ("cruise", 0.7f, 0.50f),
            };

            foreach (var (name, baseRpm, baseFps) in baselines)
            {
                var inputs = new Inputs { Rpm = baseRpm, Fuel = baseRpm * baseFps, AmbientK = ambientK };
                var model = inputs.Build(_settings);
                Measure(model, ambientK); // warm to the baseline load

                var baseCyl = model.CylinderTempK;
                var baseBlock = model.EngineTempK;

                inputs.Rpm = 1f;
                inputs.Fuel = 1f;

                var cyl = new List<float>();
                var block = new List<float>();
                var excess = new List<float>();
                var window = (int)(SettleWindowS / Dt);
                var maxSteps = (int)(MaxRunS / Dt);
                for (var step = 0; step < maxSteps; step++)
                {
                    model.Tick(Dt, engineOn: true);
                    cyl.Add(model.CylinderTempK);
                    block.Add(model.EngineTempK);
                    excess.Add(model.CylinderTempK - model.EngineTempK);

                    if (step >= window && Math.Abs(block[step] - block[step - window]) < SettleEpsK)
                    {
                        break;
                    }
                }

                var endCyl = model.CylinderTempK;
                var endBlock = model.EngineTempK;
                var baseExcess = baseCyl - baseBlock;
                var endExcess = endCyl - endBlock;

                // the cylinder-above-block gap isolates TauCylinder: the block moves slowly, so
                // fitting this signal avoids the slow block rise dominating the time constant
                var t63 = FirstCrossing(excess, baseExcess + ((endExcess - baseExcess) * 0.63f));
                var t90 = FirstCrossing(excess, baseExcess + ((endExcess - baseExcess) * 0.90f));
                var tau = TimeConstant(excess, baseExcess, endExcess);

                var r5 = cyl[Math.Min(cyl.Count - 1, (int)(5f / Dt))] - baseCyl;
                var r10 = cyl[Math.Min(cyl.Count - 1, (int)(10f / Dt))] - baseCyl;
                var r30 = cyl[Math.Min(cyl.Count - 1, (int)(30f / Dt))] - baseCyl;

                sb.AppendLine(
                    $"{name,-8} {ToC(baseCyl),6:0.0} {ToC(endCyl),5:0.0} | " +
                    $"{endCyl - baseCyl,5:0.0} {endBlock - baseBlock,6:0.0} | " +
                    $"{Fmt(t63),7} {Fmt(t90),7} {Fmt(tau),6} | " +
                    $"{r5,6:0.0} {r10,7:0.0} {r30,7:0.0}");
            }

            return sb.ToString();
        }

        private static Trace Measure(CombustionModel model, float ambientK)
        {
            var trace = new Trace();
            var window = (int)(SettleWindowS / Dt);
            var maxSteps = (int)(MaxRunS / Dt);

            for (var step = 0; step < maxSteps; step++)
            {
                model.Tick(Dt, engineOn: true);
                trace.Block.Add(model.EngineTempK);
                trace.Cylinder.Add(model.CylinderTempK);
                trace.Thermostat.Add(model.ThermostatOpen);

                if (step >= window && Math.Abs(trace.Block[step] - trace.Block[step - window]) < SettleEpsK)
                {
                    trace.Settled = true;
                    Capture(trace, model, step);
                    break;
                }
            }

            if (!trace.Settled)
            {
                Capture(trace, model, trace.Block.Count - 1);
            }

            return trace;
        }

        private static void Capture(Trace trace, CombustionModel model, int step)
        {
            trace.SettleBlockK = model.EngineTempK;
            trace.SettleCylinderK = model.CylinderTempK;
            trace.SettleThermostat = model.ThermostatOpen;
            trace.SettleBurnAir = model.BurnFractionAir;
            trace.SettleBurnTemp = model.BurnFractionTemp;
            trace.SettleLambda = model.Lambda;
            trace.SettleExhaustEnergy = model.ExhaustEnergy;
            trace.SettleBoost = model.Boost;
        }

        private static float FirstCrossing(List<float> series, float threshold)
        {
            for (var i = 0; i < series.Count; i++)
            {
                if (series[i] >= threshold)
                {
                    return i * Dt;
                }
            }

            return -1f;
        }

        // least-squares fit of ln(deficit) vs time over the 20%..70% rise band; -1 when flat
        private static float TimeConstant(List<float> series, float ambient, float settle)
        {
            var span = settle - ambient;
            if (span <= 0f)
            {
                return -1f;
            }

            double n = 0, sx = 0, sy = 0, sxx = 0, sxy = 0;
            for (var i = 0; i < series.Count; i++)
            {
                var frac = (series[i] - ambient) / span;
                if (frac < 0.2f || frac > 0.7f)
                {
                    continue;
                }

                var deficit = settle - series[i];
                if (deficit <= 0f)
                {
                    continue;
                }

                double x = i * Dt;
                var y = Math.Log(deficit);
                n++;
                sx += x;
                sy += y;
                sxx += x * x;
                sxy += x * y;
            }

            if (n < 2)
            {
                return -1f;
            }

            var denom = n * sxx - sx * sx;
            if (Math.Abs(denom) < 1e-9)
            {
                return -1f;
            }

            var slope = (n * sxy - sx * sy) / denom;
            return slope >= 0 ? -1f : (float)(-1.0 / slope);
        }

        private static string Fmt(float seconds)
        {
            if (seconds < 0f)
            {
                return "n/a";
            }

            var total = (int)Math.Round(seconds);
            var h = total / 3600;
            var m = (total % 3600) / 60;
            var s = total % 60;
            if (h > 0)
            {
                return $"{h}h{m}m";
            }

            return m > 0 ? $"{m}m{s}s" : $"{s}s";
        }

        private static float ToC(float kelvin) => kelvin - PhysicsConstants.KelvinOffset;

        private sealed class Inputs
        {
            public float Fuel;
            public float Rpm = 1f;
            public float AmbientK;

            public CombustionModel Build(CombustionModel.Settings settings)
            {
                var copy = new CombustionModel.Settings(settings);
                copy.Validate();
                // near-zero spool constants make the charger instantaneous, so the bench measures
                // pure thermal dynamics with no turbo transient mixed in
                var turbo = new TurboCharger.Settings { TauUp = 0.01f, TauDown = 0.01f, MinSpoolTau = 0.01f };
                return new CombustionModel(() => 0f, () => Fuel, () => Rpm, () => AmbientK,
                    new TurboCharger(turbo), copy, new ExhaustVelocitySettings());
            }
        }

        private sealed class Trace
        {
            public readonly List<float> Block = new List<float>();
            public readonly List<float> Cylinder = new List<float>();
            public readonly List<float> Thermostat = new List<float>();
            public bool Settled;
            public float SettleBlockK;
            public float SettleCylinderK;
            public float SettleThermostat;
            public float SettleBurnAir;
            public float SettleBurnTemp;
            public float SettleLambda;
            public float SettleExhaustEnergy;
            public float SettleBoost;
        }
    }
}
