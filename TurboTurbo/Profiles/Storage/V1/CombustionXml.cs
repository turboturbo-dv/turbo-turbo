using System.ComponentModel;

namespace TurboTurbo.Profiles.Storage.V1;

public sealed class CombustionXml
{
    internal const float DefaultRatedExhaustTempK = 760f;
    internal const float DefaultTauCylinder = 10f;
    internal const float DefaultTauEngine = 55f;
    internal const float DefaultTauCooldownOpen = 65f;
    internal const float DefaultTauCooldownClosed = 1200f;
    internal const float DefaultCylinderGainK = 60f;
    internal const float DefaultColdWallFloorK = 260f;
    internal const float DefaultWarmWallTargetK = 370f;
    internal const float DefaultMinBurnFractionAtCold = 0.85f;

    [DefaultValue(DefaultRatedExhaustTempK)]
    public float RatedExhaustTempK { get; set; } = DefaultRatedExhaustTempK;

    [DefaultValue(DefaultTauCylinder)]
    public float TauCylinder { get; set; } = DefaultTauCylinder;

    [DefaultValue(DefaultTauEngine)]
    public float TauEngine { get; set; } = DefaultTauEngine;

    [DefaultValue(DefaultTauCooldownOpen)]
    public float TauCooldownOpen { get; set; } = DefaultTauCooldownOpen;

    [DefaultValue(DefaultTauCooldownClosed)]
    public float TauCooldownClosed { get; set; } = DefaultTauCooldownClosed;

    [DefaultValue(DefaultCylinderGainK)]
    public float CylinderGainK { get; set; } = DefaultCylinderGainK;

    [DefaultValue(DefaultColdWallFloorK)]
    public float ColdWallFloorK { get; set; } = DefaultColdWallFloorK;

    [DefaultValue(DefaultWarmWallTargetK)]
    public float WarmWallTargetK { get; set; } = DefaultWarmWallTargetK;

    [DefaultValue(DefaultMinBurnFractionAtCold)]
    public float MinBurnFractionAtCold { get; set; } = DefaultMinBurnFractionAtCold;
}
