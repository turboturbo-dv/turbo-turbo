using DV.ThingTypes;

using UnityEngine;

namespace TurboTurbo;

internal static class StockConfiguration
{
    public static void Apply()
    {
        ConfigureDe6();
        ConfigureDh4();
        ConfigureDm3();
        ConfigureDe2();
        ConfigureDm1U();
        ConfigureModdedLocos();
    }

    private static void ConfigureDe6()
    {
        Controller.ConfigureEngine(TrainCarType.LocoDiesel, options => options
            .ReplaceEngineExhaust(
                c => c.GetFirstComponentInChildren<ParticleSystem>(true, ps => ps.name == "ExhaustEngineSmoke"),
                new Vector3(0.02f, 0.15f, -0.02f)));
    }

    private static void ConfigureDh4()
    {
        Controller.ConfigureEngine(TrainCarType.LocoDH4, options => options
            .ReplaceEngineExhaust(
                c => c.GetFirstComponentInChildren<ParticleSystem>(true, ps => ps.name == "ExhaustEngineSmoke"),
                new Vector3(0f, 0.03f, 0f))
            .ConfigureTurboCharger(t =>
            {
                // the DH4 runs slightly cleaner and has a lighter turbo that spins up faster
                t.LambdaCalibration = 1.7f;
                t.TauUp = 2;
            })
            .ConfigureSmoke(s =>
            {
                // The DH4 has a more modern engine with better filtration, generating less soot.
                // also adjusts for the fact that the exhaust opening is bigger, so smoke particles start out bigger,
                // and therefore less dense.
                s.SootMaxAlpha = 0.33f;
                s.WetStackMistStrength = 1.2f;
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
                e.startSizeMin = 0.6f;
                e.startSizeMax = 0.6f;
                // less engine power means less intense shimmer
                e.strength = 0.01f;
            }));
    }

    private static void ConfigureDm3()
    {
        Controller.ConfigureEngine(TrainCarType.LocoDM3, options => options
            .ReplaceEngineExhaust(
                c => c.GetFirstComponentInChildren<ParticleSystem>(true, ps => ps.name == "ExhaustEngineSmoke"),
                new Vector3(0f, -0.02f, 0f))
            .UseAtmosphericCharger(c =>
            {
                // with these parameters, the DM3 starts producing black smoke near the redline
                c.EtaPeak = 0.83f;
                c.ChokeK = 0.26f;
                c.LambdaCalibration = 0.67f;
            })
            .ConfigureExhaustVelocity(s =>
            {
                // again slightly lower max exhaust velocity
                s.FullLoad = 13f;
            })
            .ConfigureSmoke(s =>
            {
                // some oil burning gives the DM3 a distinctive blue-gray smoke
                s.CleanExhaustAlpha = 0.04f;
                s.OilTintStrength = 0.5f;
                s.OilRpmExponent = 0.5f;
            })
            .ConfigureSmokeEmitter(e =>
            {
                // sized to match the exhaust pipe
                e.startSizeMin = 0.25f;
                e.startSizeMax = 0.4f;
                e.sizeOverLifetimeEnd = 11;
            })
            .ConfigureShimmerEmitter(e =>
            {
                // slight down rating again to match engine power
                e.lifetime = 1.2f;
                e.startSizeMin = 0.4f;
                e.startSizeMax = 0.4f;
                e.sizeOverLifetimeEnd = 8;
            }));
    }

    private static void ConfigureDe2()
    {
        Controller.ConfigureEngine(TrainCarType.LocoShunter, options => options
            .ReplaceEngineExhaust(
                c => c.GetFirstComponentInChildren<ParticleSystem>(true, ps => ps.name == "ExhaustEngineSmoke"),
                new Vector3(0f, 0f, 0f))
            .UseAtmosphericCharger(c =>
            {
                // a reasonably clean naturally aspirated engine, shouldn't really generate soot normally
                c.EtaPeak = 0.86f;
                c.ChokeK = 0.24f;
                c.LambdaCalibration = 0.63f;

            })
            .ConfigureSmoke(s =>
            {
                // not much wet stacking occurs in a smaller engine
                s.WetStackMistStrength = 1f;
                s.OilTintStrength = 0.35f;
            })
            .ConfigureExhaustVelocity(e =>
            {
                // lower power, so lower exhaust velocity
                e.Idle = 1f;
                e.FullLoad = 10f;
            })
            .ConfigureSmokeEmitter(e =>
            {
                e.sizeOverLifetimeEnd = 12f;
            })
            .ConfigureShimmerEmitter(e =>
            {
                // exhaust pipe is quite thin, so shimmer starts out small and rapidly grows bigger
                e.startSizeMin = 0.35f;
                e.startSizeMax = 0.35f;
                e.sizeOverLifetimeEnd = 9;

                // lower power, so shimmer is less intense and disperses more quickly
                e.lifetime = 1f;
                e.strength = 0.004f;

                // particle velocity adds enough movement, no need to scroll the effect itself
                e.idleAnimSpeed = 0f;
                e.fullAnimSpeed = 0f;
            }));
    }

    private static void ConfigureDm1U()
    {
        // this model is generally similar to the DM3, but weaker and smaller
        Controller.ConfigureEngine(TrainCarType.LocoDM1U, options => options
            .ReplaceEngineExhaust(
                c => c.GetFirstComponentInChildren<ParticleSystem>(true, ps => ps.name == "ExhaustEngineSmoke"),
                new Vector3(0f, 0f, 0f))
            .UseAtmosphericCharger(c =>
            {
                c.EtaPeak = 0.83f;
                c.ChokeK = 0.26f;
                c.LambdaCalibration = 0.67f;
            })
            .ConfigureExhaustVelocity(s =>
            {
                s.FullLoad = 11f;
            })
            .ConfigureSmoke(s =>
            {
                s.CleanExhaustAlpha = 0.03f;
                s.OilTintStrength = 0.5f;
                s.OilRpmExponent = 0.5f;
            })
            .ConfigureSmokeEmitter(e =>
            {
                e.startSizeMin = 0.25f;
                e.startSizeMax = 0.35f;
                e.sizeOverLifetimeEnd = 12;
            })
            .ConfigureShimmerEmitter(e =>
            {
                e.lifetime = 1.2f;
                e.startSizeMin = 0.3f;
                e.startSizeMax = 0.3f;
                e.sizeOverLifetimeEnd = 8;
                e.strength = 0.008f;
            }));
    }

    private static void ConfigureModdedLocos()
    {
        // todo
    }
}