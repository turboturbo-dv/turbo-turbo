using System.Collections.Generic;
using System.Linq;

using TurboTurbo.Effects;
using TurboTurbo.Modeling;
using TurboTurbo.Profiles.Storage.Current;

namespace TurboTurbo.Profiles.Storage;

/// <summary>Converts between the persistence schema and the runtime profile model.</summary>
internal static class ProfileMapper
{
    public static LocoProfile ToRuntime(LocoProfileXml xml)
    {
        if (xml == null) return null;

        return new LocoProfile
        {
            LiveryId = xml.LiveryId,
            Enabled = xml.Enabled,
            ChargerKind = xml.ChargerKind,
            Exhausts = xml.Exhausts?.Select(ToRuntime).ToList() ?? new List<LocoExhaust>(),
            TurboCharger = ToRuntime(xml.TurboCharger),
            Atmospheric = ToRuntime(xml.Atmospheric),
            Roots = ToRuntime(xml.Roots),
            Smoke = ToRuntime(xml.Smoke),
            SmokeEmitter = ToRuntime(xml.SmokeEmitter),
            ShimmerEmitter = ToRuntime(xml.ShimmerEmitter),
            Velocity = ToRuntime(xml.Velocity),
            Combustion = ToRuntime(xml.Combustion),
            Stack = ToRuntime(xml.Stack),
        };
    }

    public static LocoProfileXml ToXml(LocoProfile profile)
    {
        if (profile == null) return null;

        return new LocoProfileXml
        {
            LiveryId = profile.LiveryId,
            Enabled = profile.Enabled,
            ChargerKind = profile.ChargerKind,
            Exhausts = profile.Exhausts?.Select(ToXml).ToList() ?? new List<ExhaustXml>(),
            TurboCharger = ToXml(profile.TurboCharger),
            Atmospheric = ToXml(profile.Atmospheric),
            Roots = ToXml(profile.Roots),
            Smoke = ToXml(profile.Smoke),
            SmokeEmitter = ToXml(profile.SmokeEmitter),
            ShimmerEmitter = ToXml(profile.ShimmerEmitter),
            Velocity = ToXml(profile.Velocity),
            Combustion = ToXml(profile.Combustion),
            Stack = ToXml(profile.Stack),
        };
    }

    private static LocoExhaust ToRuntime(ExhaustXml xml) =>
        xml == null ? null : new LocoExhaust { Kind = xml.Kind, Path = xml.Path, Offset = xml.Offset };

    private static ExhaustXml ToXml(LocoExhaust exhaust) =>
        exhaust == null ? null : new ExhaustXml { Kind = exhaust.Kind, Path = exhaust.Path, Offset = exhaust.Offset };

    private static TurboCharger.Settings ToRuntime(TurboChargerXml xml) =>
        xml == null ? null : new TurboCharger.Settings
        {
            LambdaCalibration = xml.LambdaCalibration,
            BoostChargeMultiplier = xml.BoostChargeMultiplier,
            TauUp = xml.TauUp,
            TauDown = xml.TauDown,
            SurgeRateThreshold = xml.SurgeRateThreshold,
            CompressionRatio = xml.CompressionRatio,
            IntakeNtu = xml.IntakeNtu,
        };

    private static TurboChargerXml ToXml(TurboCharger.Settings settings) =>
        settings == null ? null : new TurboChargerXml
        {
            LambdaCalibration = settings.LambdaCalibration,
            BoostChargeMultiplier = settings.BoostChargeMultiplier,
            TauUp = settings.TauUp,
            TauDown = settings.TauDown,
            SurgeRateThreshold = settings.SurgeRateThreshold,
            CompressionRatio = settings.CompressionRatio,
            IntakeNtu = settings.IntakeNtu,
        };

    private static AtmosphericCharger.Settings ToRuntime(AtmosphericXml xml) =>
        xml == null ? null : new AtmosphericCharger.Settings
        {
            EtaPeak = xml.EtaPeak,
            ChokeK = xml.ChokeK,
            ChokeBeta = xml.ChokeBeta,
            LambdaCalibration = xml.LambdaCalibration,
            CompressionRatio = xml.CompressionRatio,
            IntakeNtu = xml.IntakeNtu,
        };

    private static AtmosphericXml ToXml(AtmosphericCharger.Settings settings) =>
        settings == null ? null : new AtmosphericXml
        {
            EtaPeak = settings.EtaPeak,
            ChokeK = settings.ChokeK,
            ChokeBeta = settings.ChokeBeta,
            LambdaCalibration = settings.LambdaCalibration,
            CompressionRatio = settings.CompressionRatio,
            IntakeNtu = settings.IntakeNtu,
        };

    private static RootsCharger.Settings ToRuntime(RootsChargerXml xml) =>
        xml == null ? null : new RootsCharger.Settings
        {
            PressureRatio = xml.PressureRatio,
            BoostExponent = xml.BoostExponent,
            CompressorEfficiency = xml.CompressorEfficiency,
            LambdaCalibration = xml.LambdaCalibration,
            CompressionRatio = xml.CompressionRatio,
            IntakeNtu = xml.IntakeNtu,
        };

    private static RootsChargerXml ToXml(RootsCharger.Settings settings) =>
        settings == null ? null : new RootsChargerXml
        {
            PressureRatio = settings.PressureRatio,
            BoostExponent = settings.BoostExponent,
            CompressorEfficiency = settings.CompressorEfficiency,
            LambdaCalibration = settings.LambdaCalibration,
            CompressionRatio = settings.CompressionRatio,
            IntakeNtu = settings.IntakeNtu,
        };

    private static ExhaustSmokeModel.Settings ToRuntime(SmokeModelXml xml) =>
        xml == null ? null : new ExhaustSmokeModel.Settings
        {
            Density = xml.Density,
            ColorIdleHaze = ColorHex.Parse(xml.ColorIdleHazeHex),
            ColorCleanBurn = ColorHex.Parse(xml.ColorCleanBurnHex),
            ColorHeavySoot = ColorHex.Parse(xml.ColorHeavySootHex),
            ColorWetStack = ColorHex.Parse(xml.ColorWetStackHex),
            ColorOilBurn = ColorHex.Parse(xml.ColorOilBurnHex),
            CleanMinHeatAlpha = xml.CleanMinHeatAlpha,
            CleanMaxHeatAlpha = xml.CleanMaxHeatAlpha,
            CleanBurnHeat = xml.CleanBurnHeat,
            SootOnsetLambda = xml.SootOnsetLambda,
            SootOpaqueLambda = xml.SootOpaqueLambda,
            SootCurveExponent = xml.SootCurveExponent,
            SootIncreaseTau = xml.SootIncreaseTau,
            SootDecreaseTau = xml.SootDecreaseTau,
            SootMaxAlpha = xml.SootMaxAlpha,
            SootPowerFloor = xml.SootPowerFloor,
            SootPowerExponent = xml.SootPowerExponent,
            WetStackMaxAlpha = xml.WetStackMaxAlpha,
            OilTintStrength = xml.OilTintStrength,
            OilRpmExponent = xml.OilRpmExponent,
        };

    private static SmokeModelXml ToXml(ExhaustSmokeModel.Settings settings) =>
        settings == null ? null : new SmokeModelXml
        {
            Density = settings.Density,
            ColorIdleHazeHex = ColorHex.Format(settings.ColorIdleHaze),
            ColorCleanBurnHex = ColorHex.Format(settings.ColorCleanBurn),
            ColorHeavySootHex = ColorHex.Format(settings.ColorHeavySoot),
            ColorWetStackHex = ColorHex.Format(settings.ColorWetStack),
            ColorOilBurnHex = ColorHex.Format(settings.ColorOilBurn),
            CleanMinHeatAlpha = settings.CleanMinHeatAlpha,
            CleanMaxHeatAlpha = settings.CleanMaxHeatAlpha,
            CleanBurnHeat = settings.CleanBurnHeat,
            SootOnsetLambda = settings.SootOnsetLambda,
            SootOpaqueLambda = settings.SootOpaqueLambda,
            SootCurveExponent = settings.SootCurveExponent,
            SootIncreaseTau = settings.SootIncreaseTau,
            SootDecreaseTau = settings.SootDecreaseTau,
            SootMaxAlpha = settings.SootMaxAlpha,
            SootPowerFloor = settings.SootPowerFloor,
            SootPowerExponent = settings.SootPowerExponent,
            WetStackMaxAlpha = settings.WetStackMaxAlpha,
            OilTintStrength = settings.OilTintStrength,
            OilRpmExponent = settings.OilRpmExponent,
        };

    private static SmokeParticles.Settings ToRuntime(SmokeEmitterXml xml) =>
        xml == null ? null : new SmokeParticles.Settings
        {
            startSize = xml.startSize,
            startSizeVariance = xml.startSizeVariance,
            plumeSize = xml.plumeSize,
            growthExponent = xml.growthExponent,
            buoyancy = xml.buoyancy,
            drag = xml.drag,
            angularVelocityMax = xml.angularVelocityMax,
            speedNormMax = xml.speedNormMax,
            speedGrowthScale = xml.speedGrowthScale,
            speedJitter = xml.speedJitter,
            turbulenceStrengthMin = xml.turbulenceStrengthMin,
            turbulenceStrengthMax = xml.turbulenceStrengthMax,
            turbulenceFrequency = xml.turbulenceFrequency,
            turbulenceScrollSpeed = xml.turbulenceScrollSpeed,
            lightSaturation = xml.lightSaturation,
            maxShadowFloor = xml.maxShadowFloor,
        };

    private static SmokeEmitterXml ToXml(SmokeParticles.Settings settings) =>
        settings == null ? null : new SmokeEmitterXml
        {
            startSize = settings.startSize,
            startSizeVariance = settings.startSizeVariance,
            plumeSize = settings.plumeSize,
            growthExponent = settings.growthExponent,
            buoyancy = settings.buoyancy,
            drag = settings.drag,
            angularVelocityMax = settings.angularVelocityMax,
            speedNormMax = settings.speedNormMax,
            speedGrowthScale = settings.speedGrowthScale,
            speedJitter = settings.speedJitter,
            turbulenceStrengthMin = settings.turbulenceStrengthMin,
            turbulenceStrengthMax = settings.turbulenceStrengthMax,
            turbulenceFrequency = settings.turbulenceFrequency,
            turbulenceScrollSpeed = settings.turbulenceScrollSpeed,
            lightSaturation = settings.lightSaturation,
            maxShadowFloor = settings.maxShadowFloor,
        };

    private static ShimmerParticles.Settings ToRuntime(ShimmerEmitterXml xml) =>
        xml == null ? null : new ShimmerParticles.Settings
        {
            idleRate = xml.idleRate,
            fullRate = xml.fullRate,
            lifetime = xml.lifetime,
            startSize = xml.startSize,
            startSizeVariance = xml.startSizeVariance,
            sizeOverLifetimeEnd = xml.sizeOverLifetimeEnd,
            sizeOverLifetimeExponent = xml.sizeOverLifetimeExponent,
            drag = xml.drag,
            buoyancy = xml.buoyancy,
            speedNormMax = xml.speedNormMax,
            speedJitter = xml.speedJitter,
            strength = xml.strength,
            baseStrength = xml.baseStrength,
            freq = xml.freq,
            idleRadius = xml.idleRadius,
            fullRadius = xml.fullRadius,
            idleAnimSpeed = xml.idleAnimSpeed,
            fullAnimSpeed = xml.fullAnimSpeed,
            speedMultiplier = xml.speedMultiplier,
            shimmerHoldTime = xml.shimmerHoldTime,
            decayK = xml.decayK,
            yOffset = xml.yOffset,
        };

    private static ShimmerEmitterXml ToXml(ShimmerParticles.Settings settings) =>
        settings == null ? null : new ShimmerEmitterXml
        {
            idleRate = settings.idleRate,
            fullRate = settings.fullRate,
            lifetime = settings.lifetime,
            startSize = settings.startSize,
            startSizeVariance = settings.startSizeVariance,
            sizeOverLifetimeEnd = settings.sizeOverLifetimeEnd,
            sizeOverLifetimeExponent = settings.sizeOverLifetimeExponent,
            drag = settings.drag,
            buoyancy = settings.buoyancy,
            speedNormMax = settings.speedNormMax,
            speedJitter = settings.speedJitter,
            strength = settings.strength,
            baseStrength = settings.baseStrength,
            freq = settings.freq,
            idleRadius = settings.idleRadius,
            fullRadius = settings.fullRadius,
            idleAnimSpeed = settings.idleAnimSpeed,
            fullAnimSpeed = settings.fullAnimSpeed,
            speedMultiplier = settings.speedMultiplier,
            shimmerHoldTime = settings.shimmerHoldTime,
            decayK = settings.decayK,
            yOffset = settings.yOffset,
        };

    private static ExhaustVelocitySettings ToRuntime(VelocityXml xml) =>
        xml == null ? null : new ExhaustVelocitySettings { ExhaustVelocityCoefficient = xml.ExhaustVelocityCoefficient };

    private static VelocityXml ToXml(ExhaustVelocitySettings settings) =>
        settings == null ? null : new VelocityXml { ExhaustVelocityCoefficient = settings.ExhaustVelocityCoefficient };

    private static CombustionModel.Settings ToRuntime(CombustionXml xml) =>
        xml == null
            ? null
            : new CombustionModel.Settings
            {
                RatedExhaustTempK = xml.RatedExhaustTempK,
                TauChamber = xml.TauChamber,
                TauEngine = xml.TauEngine,
                TauCooldownOpen = xml.TauCooldownOpen,
                TauCooldownClosed = xml.TauCooldownClosed,
                ChamberGainK = xml.ChamberGainK,
                PolytropicExponent = xml.PolytropicExponent,
                IgnitionTempK = xml.IgnitionTempK,
                IgnitionSpread = xml.IgnitionSpread,
                LateBurnWork = xml.LateBurnWork,
                QuenchFraction = xml.QuenchFraction,
                CrankingFuelPerStroke = xml.CrankingFuelPerStroke,
            };

    private static CombustionXml ToXml(CombustionModel.Settings settings) =>
        settings == null
            ? null
            : new CombustionXml
            {
                RatedExhaustTempK = settings.RatedExhaustTempK,
                TauChamber = settings.TauChamber,
                TauEngine = settings.TauEngine,
                TauCooldownOpen = settings.TauCooldownOpen,
                TauCooldownClosed = settings.TauCooldownClosed,
                ChamberGainK = settings.ChamberGainK,
                PolytropicExponent = settings.PolytropicExponent,
                IgnitionTempK = settings.IgnitionTempK,
                IgnitionSpread = settings.IgnitionSpread,
                LateBurnWork = settings.LateBurnWork,
                QuenchFraction = settings.QuenchFraction,
                CrankingFuelPerStroke = settings.CrankingFuelPerStroke,
            };

    private static StackModel.Settings ToRuntime(StackXml xml) =>
        xml == null
            ? null
            : new StackModel.Settings
            {
                FillTime = xml.FillTime,
                ClearTime = xml.ClearTime,
                TauWall = xml.TauWall,
                FlowWarmBias = xml.FlowWarmBias,
                CaptureColdK = xml.CaptureColdK,
                CaptureHotK = xml.CaptureHotK,
                ClearStartK = xml.ClearStartK,
                ClearFullK = xml.ClearFullK,
                ContactIdle = xml.ContactIdle,
                ContactFullFlow = xml.ContactFullFlow,
                StickMax = xml.StickMax,
                ReferenceUnburned = xml.ReferenceUnburned,
            };

    private static StackXml ToXml(StackModel.Settings settings) =>
        settings == null
            ? null
            : new StackXml
            {
                FillTime = settings.FillTime,
                ClearTime = settings.ClearTime,
                TauWall = settings.TauWall,
                FlowWarmBias = settings.FlowWarmBias,
                CaptureColdK = settings.CaptureColdK,
                CaptureHotK = settings.CaptureHotK,
                ClearStartK = settings.ClearStartK,
                ClearFullK = settings.ClearFullK,
                ContactIdle = settings.ContactIdle,
                ContactFullFlow = settings.ContactFullFlow,
                StickMax = settings.StickMax,
                ReferenceUnburned = settings.ReferenceUnburned,
            };
}
