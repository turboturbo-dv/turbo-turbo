using System;
using System.Collections.Generic;

using TurboTurbo;
using TurboTurbo.Modeling;

namespace TestBench
{
    /// <summary>Shared scaffolding for the engine and stack benches.</summary>
    public static class BenchCommon
    {
        public const float Dt = 0.1f;

        public const float SettleWindowS = 600f;
        public const float SettleEpsK = 0.01f;
        public const float MaxRunS = 6f * 3600f;

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

        /// <summary>Mutable load inputs plus a builder for an instantaneous-turbo combustion model.</summary>
        public sealed class Inputs
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
    }
}
