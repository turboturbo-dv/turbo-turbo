using DV.ThingTypes;

using UnityEngine;

namespace TurboTurbo;

internal static class StockConfiguration
{
    public static void Apply()
    {
        ConfigureDe6();
        ConfigureDh4();
        ConfigureModdedLocos();
    }

    private static void ConfigureDe6()
    {
        Controller.ConfigureEngine(TrainCarType.LocoDiesel, options => options
            .AddTurbo()
            .ReplaceEngineExhaust(
                c => c.GetFirstComponentInChildren<ParticleSystem>(true, ps => ps.name == "ExhaustEngineSmoke"),
                new Vector3(0.02f, 0.15f, -0.02f)));
    }

    private static void ConfigureDh4()
    {
        Controller.ConfigureEngine(TrainCarType.LocoDH4, options => options
            .AddTurbo()
            .ReplaceEngineExhaust(
                c => c.GetFirstComponentInChildren<ParticleSystem>(true, ps => ps.name == "ExhaustEngineSmoke"),
                new Vector3(0f, 0.03f, 0f))
            .ConfigureTurbo(t =>
            {
                // the DH4 runs slightly cleaner and has a lighter turbo that spins up faster
                t.LambdaCalibration = 1.7f;
                t.TauUp = 2;
            })
            .ConfigureSmoke(s =>
            {
                // a newer engine with better exhaust filters means that when smoke is generated, it is less dense.
                // also adjusts for the fact that the exhaust opening is bigger, so smoke is spread out more
                s.SootMaxAlpha = 0.30f;
                s.WetStackMistStrength = 0.95f;
                s.OilRpmExponent = 2.1f;
            })
            .ConfigureExhaustVelocity(v =>
            {
                // raise base exhaust velocity so smoke clears the cab at high speed and low power
                v.Idle = 3.05f;
                // lower engine power, so slightly lower max exhaust velocity
                v.FullLoad = 14f;

            })
            .ConfigureSmokeEmitter(e =>
            {
                // bigger exhaust opening means particles start out bigger too
                e.startSizeMin = 0.4f;
                e.startSizeMax = 0.6f;
            })
            .ConfigureShimmerEmitter(e =>
            {
                // less engine power and bigger exhaust opening means less intense shimmer
                e.strength = 0.01f;
            }));
    }

    private static void ConfigureModdedLocos()
    {
        // todo
    }
}