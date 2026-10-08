using System.Collections.Generic;
using System.Linq;

using TurboTurbo.Modeling;
using TurboTurbo.Profiles.Storage.V1;
using TurboTurbo.WorkBench;

namespace TurboTurbo.Profiles.Storage;

/// <summary>Converts between the persistence schema and the runtime profile model.</summary>
internal static class ProfileMapper
{
    public static LocoProfile ToRuntime(LocoProfileXml xml)
    {
        if (xml == null) return null;

        return new LocoProfile
        {
            Version = xml.Version,
            LiveryId = xml.LiveryId,
            Enabled = xml.Enabled,
            ChargerKind = xml.ChargerKind,
            Exhausts = xml.Exhausts?.Select(ToRuntime).ToList() ?? new List<LocoExhaust>(),
            TurboCharger = ToRuntime(xml.TurboCharger),
            Atmospheric = ToRuntime(xml.Atmospheric),
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
            Version = profile.Version,
            LiveryId = profile.LiveryId,
            Enabled = profile.Enabled,
            ChargerKind = profile.ChargerKind,
            Exhausts = profile.Exhausts?.Select(ToXml).ToList() ?? new List<ExhaustXml>(),
            TurboCharger = ToXml(profile.TurboCharger),
            Atmospheric = ToXml(profile.Atmospheric),
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
            MinSpoolTau = xml.MinSpoolTau,
            ThermalK = xml.ThermalK,
            SurgeRateThreshold = xml.SurgeRateThreshold,
        };

    private static TurboChargerXml ToXml(TurboCharger.Settings settings) =>
        settings == null ? null : new TurboChargerXml
        {
            LambdaCalibration = settings.LambdaCalibration,
            BoostChargeMultiplier = settings.BoostChargeMultiplier,
            TauUp = settings.TauUp,
            TauDown = settings.TauDown,
            MinSpoolTau = settings.MinSpoolTau,
            ThermalK = settings.ThermalK,
            SurgeRateThreshold = settings.SurgeRateThreshold,
        };

    private static AtmosphericCharger.Settings ToRuntime(AtmosphericXml xml) =>
        xml == null ? null : new AtmosphericCharger.Settings
        {
            EtaPeak = xml.EtaPeak,
            ChokeK = xml.ChokeK,
            ChokeBeta = xml.ChokeBeta,
            LambdaCalibration = xml.LambdaCalibration,
        };

    private static AtmosphericXml ToXml(AtmosphericCharger.Settings settings) =>
        settings == null ? null : new AtmosphericXml
        {
            EtaPeak = settings.EtaPeak,
            ChokeK = settings.ChokeK,
            ChokeBeta = settings.ChokeBeta,
            LambdaCalibration = settings.LambdaCalibration,
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
            idleEmissionRate = xml.idleEmissionRate,
            fullEmissionRate = xml.fullEmissionRate,
            lifetime = xml.lifetime,
            startSize = xml.startSize,
            startSizeVariance = xml.startSizeVariance,
            sizeOverLifetimeEnd = xml.sizeOverLifetimeEnd,
            sizeOverLifetimeExponent = xml.sizeOverLifetimeExponent,
            buoyancy = xml.buoyancy,
            drag = xml.drag,
            angularVelocityMax = xml.angularVelocityMax,
            speedNormMax = xml.speedNormMax,
            speedLifetimeScale = xml.speedLifetimeScale,
            speedJitter = xml.speedJitter,
            turbulenceStrength = xml.turbulenceStrength,
            turbulenceFrequency = xml.turbulenceFrequency,
            turbulenceScrollSpeed = xml.turbulenceScrollSpeed,
            lightSaturation = xml.lightSaturation,
            maxShadowFloor = xml.maxShadowFloor,
        };

    private static SmokeEmitterXml ToXml(SmokeParticles.Settings settings) =>
        settings == null ? null : new SmokeEmitterXml
        {
            idleEmissionRate = settings.idleEmissionRate,
            fullEmissionRate = settings.fullEmissionRate,
            lifetime = settings.lifetime,
            startSize = settings.startSize,
            startSizeVariance = settings.startSizeVariance,
            sizeOverLifetimeEnd = settings.sizeOverLifetimeEnd,
            sizeOverLifetimeExponent = settings.sizeOverLifetimeExponent,
            buoyancy = settings.buoyancy,
            drag = settings.drag,
            angularVelocityMax = settings.angularVelocityMax,
            speedNormMax = settings.speedNormMax,
            speedLifetimeScale = settings.speedLifetimeScale,
            speedJitter = settings.speedJitter,
            turbulenceStrength = settings.turbulenceStrength,
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
            speedLifetimeScale = xml.speedLifetimeScale,
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
            speedLifetimeScale = settings.speedLifetimeScale,
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
                TauCylinder = xml.TauCylinder,
                TauEngine = xml.TauEngine,
                TauCooldownOpen = xml.TauCooldownOpen,
                TauCooldownClosed = xml.TauCooldownClosed,
                CylinderGainK = xml.CylinderGainK,
                ColdWallFloorK = xml.ColdWallFloorK,
                WarmWallTargetK = xml.WarmWallTargetK,
                MinBurnFractionAtCold = xml.MinBurnFractionAtCold,
            };

    private static CombustionXml ToXml(CombustionModel.Settings settings) =>
        settings == null
            ? null
            : new CombustionXml
            {
                RatedExhaustTempK = settings.RatedExhaustTempK,
                TauCylinder = settings.TauCylinder,
                TauEngine = settings.TauEngine,
                TauCooldownOpen = settings.TauCooldownOpen,
                TauCooldownClosed = settings.TauCooldownClosed,
                CylinderGainK = settings.CylinderGainK,
                ColdWallFloorK = settings.ColdWallFloorK,
                WarmWallTargetK = settings.WarmWallTargetK,
                MinBurnFractionAtCold = settings.MinBurnFractionAtCold,
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
