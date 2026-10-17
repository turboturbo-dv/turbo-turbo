using System;
using System.Collections.Generic;

using TurboTurbo;
using TurboTurbo.Modeling;

namespace TestBench
{
    /// <summary>Shared scaffolding for the thermal, ignition and stack benches.</summary>
    public static class BenchCommon
    {
        public const float Dt = 0.1f;

        public const float SettleWindowS = 600f;
        public const float SettleEpsK = 0.01f;
        public const float MaxRunS = 6f * 3600f;

        public static readonly float ReferenceUnburned = new StackModel.Settings().ReferenceUnburned;

        public static readonly (string Name, float Rpm, float FuelPerStroke)[] Points =
        {
            ("idle", 0.3f, 0.10f),
            ("light", 0.5f, 0.25f),
            ("lug", 0.6f, 1.00f),
            ("cruise", 0.7f, 0.50f),
            ("full", 1.0f, 1.00f),
        };

        public static float FirstCrossing(List<float> series, float threshold)
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

        public static string Fmt(float seconds)
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

        public static float ToC(float kelvin) => kelvin - PhysicsConstants.KelvinOffset;

        public static Inputs LoadInputs(ChargerKind charger, float rpm, float fps, float ambientK) => new Inputs
        {
            Rpm = rpm,
            Fuel = rpm * fps,
            AmbientK = ambientK,
            Charger = charger,
        };

        /// <summary>Runs the model at constant inputs until the block settles, recording the warm-up.</summary>
        public static Trace Measure(CombustionModel model)
        {
            var trace = new Trace();
            var window = (int)(SettleWindowS / Dt);
            var maxSteps = (int)(MaxRunS / Dt);

            for (var step = 0; step < maxSteps; step++)
            {
                model.Tick(Dt, engineOn: true);
                trace.Block.Add(model.EngineTempK);
                trace.Cylinder.Add(model.ChamberTempK);
                trace.Thermostat.Add(model.ThermostatOpen);

                if (step >= window && Math.Abs(trace.Block[step] - trace.Block[step - window]) < SettleEpsK)
                {
                    trace.Settled = true;
                    break;
                }
            }

            trace.Capture(model);
            return trace;
        }

        /// <summary>
        /// Least-squares fit of ln(deficit) against time over the 20%..70% rise band. Returns -1 when the series
        /// does not rise.
        /// </summary>
        public static float TimeConstant(List<float> series, float start, float settle)
        {
            var span = settle - start;
            if (span <= 0f)
            {
                return -1f;
            }

            double n = 0, sx = 0, sy = 0, sxx = 0, sxy = 0;
            for (var i = 0; i < series.Count; i++)
            {
                var frac = (series[i] - start) / span;
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

        /// <summary>Warm-up series recorded by <see cref="Measure"/>, and the model state where it ended.</summary>
        public sealed class Trace
        {
            public readonly List<float> Block = new List<float>();
            public readonly List<float> Cylinder = new List<float>();
            public readonly List<float> Thermostat = new List<float>();
            public bool Settled;
            public float SettleBlockK;
            public float SettleCylinderK;
            public float SettleIntakeK;
            public float SettleCompressionK;
            public float SettleCompressionPressure;
            public float SettleIgnitionMargin;
            public float SettleThermostat;
            public float SettleBurnAir;
            public float SettleBurnTemp;
            public float SettleInjected;
            public float SettleWorkEfficiency;
            public float SettleLateBurned;
            public float SettleUnburned;
            public float SettleLambda;
            public float SettleExhaustEnergy;
            public float SettleBoost;

            public void Capture(CombustionModel model)
            {
                SettleBlockK = model.EngineTempK;
                SettleCylinderK = model.ChamberTempK;
                SettleIntakeK = model.IntakeTempK;
                SettleCompressionK = model.CompressionTempK;
                SettleCompressionPressure = model.CompressionPressure;
                SettleIgnitionMargin = model.IgnitionMargin;
                SettleThermostat = model.ThermostatOpen;
                SettleBurnAir = model.BurnFractionAir;
                SettleBurnTemp = model.BurnFractionTemp;
                SettleInjected = model.InjectedPerStroke;
                SettleWorkEfficiency = model.WorkEfficiency;
                SettleLateBurned = model.LateBurnedFuel / ReferenceUnburned;
                SettleUnburned = model.UnburnedFuel / ReferenceUnburned;
                SettleLambda = model.Lambda;
                SettleExhaustEnergy = model.ExhaustEnergy;
                SettleBoost = model.Boost;
            }
        }

        public sealed class Inputs
        {
            public float Fuel;
            public float Rpm = 1f;
            public float AmbientK;
            public ChargerKind Charger = ChargerKind.Turbo;

            public bool Atmospheric => Charger == ChargerKind.Atmospheric;

            public CombustionModel Build(CombustionModel.Settings settings)
            {
                var copy = new CombustionModel.Settings(settings);
                copy.Validate();

                ICharger charger = Atmospheric
                    ? new AtmosphericCharger(new AtmosphericCharger.Settings
                    {
                        // DE2-like naturally aspirated geometry
                        EtaPeak = 0.86f,
                        ChokeK = 0.285f,
                        LambdaCalibration = 0.525f,
                    })
                    // near-zero spool constants make the charger instantaneous, so the bench measures
                    // pure thermal dynamics with no turbo transient mixed in
                    : new TurboCharger(new TurboCharger.Settings { TauUp = 0.01f, TauDown = 0.01f, MinSpoolTau = 0.01f });

                return new CombustionModel(() => 0f, () => Fuel, () => Rpm, () => AmbientK,
                    charger, copy, new ExhaustVelocitySettings());
            }
        }
    }
}
