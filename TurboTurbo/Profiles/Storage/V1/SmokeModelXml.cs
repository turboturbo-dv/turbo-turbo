using System.ComponentModel;

namespace TurboTurbo.Profiles.Storage.V1;

public sealed class SmokeModelXml
{
    internal const float DefaultDensity = 150f;

    internal const string DefaultColorIdleHazeHex = "9E9678FF";
    internal const string DefaultColorCleanBurnHex = "737373FF";
    internal const string DefaultColorHeavySootHex = "0D0D0DFF";
    internal const string DefaultColorWetStackHex = "FFFFF2FF";
    internal const string DefaultColorOilBurnHex = "7085D9FF";

    [DefaultValue(DefaultDensity)]
    public float Density = DefaultDensity;

    [DefaultValue(DefaultColorIdleHazeHex)]
    public string ColorIdleHazeHex { get; set; } = DefaultColorIdleHazeHex;

    [DefaultValue(DefaultColorCleanBurnHex)]
    public string ColorCleanBurnHex { get; set; } = DefaultColorCleanBurnHex;

    [DefaultValue(DefaultColorHeavySootHex)]
    public string ColorHeavySootHex { get; set; } = DefaultColorHeavySootHex;

    [DefaultValue(DefaultColorWetStackHex)]
    public string ColorWetStackHex { get; set; } = DefaultColorWetStackHex;

    [DefaultValue(DefaultColorOilBurnHex)]
    public string ColorOilBurnHex { get; set; } = DefaultColorOilBurnHex;

    internal const float DefaultCleanMinHeatAlpha = 0.002f;
    internal const float DefaultCleanMaxHeatAlpha = 0.08f;
    internal const float DefaultCleanBurnHeat = 0.2f;
    internal const float DefaultSootOnsetLambda = 1.3f;
    internal const float DefaultSootOpaqueLambda = 1.05f;
    internal const float DefaultSootCurveExponent = 2f;
    internal const float DefaultSootIncreaseTau = 0.08f;
    internal const float DefaultSootDecreaseTau = 0.4f;
    internal const float DefaultSootMaxAlpha = 0.95f;
    internal const float DefaultSootPowerFloor = 0.1f;
    internal const float DefaultSootPowerExponent = 1f;
    internal const float DefaultWetStackMaxAlpha = 0.95f;
    internal const float DefaultOilTintStrength = 0.3f;
    internal const float DefaultOilRpmExponent = 2.5f;

    [DefaultValue(DefaultCleanMinHeatAlpha)]
    public float CleanMinHeatAlpha = DefaultCleanMinHeatAlpha;

    [DefaultValue(DefaultCleanMaxHeatAlpha)]
    public float CleanMaxHeatAlpha = DefaultCleanMaxHeatAlpha;

    [DefaultValue(DefaultCleanBurnHeat)]
    public float CleanBurnHeat = DefaultCleanBurnHeat;

    [DefaultValue(DefaultSootOnsetLambda)]
    public float SootOnsetLambda = DefaultSootOnsetLambda;

    [DefaultValue(DefaultSootOpaqueLambda)]
    public float SootOpaqueLambda = DefaultSootOpaqueLambda;

    [DefaultValue(DefaultSootCurveExponent)]
    public float SootCurveExponent = DefaultSootCurveExponent;

    [DefaultValue(DefaultSootIncreaseTau)]
    public float SootIncreaseTau = DefaultSootIncreaseTau;

    [DefaultValue(DefaultSootDecreaseTau)]
    public float SootDecreaseTau = DefaultSootDecreaseTau;

    [DefaultValue(DefaultSootMaxAlpha)]
    public float SootMaxAlpha = DefaultSootMaxAlpha;

    [DefaultValue(DefaultSootPowerFloor)]
    public float SootPowerFloor = DefaultSootPowerFloor;

    [DefaultValue(DefaultSootPowerExponent)]
    public float SootPowerExponent = DefaultSootPowerExponent;

    [DefaultValue(DefaultWetStackMaxAlpha)]
    public float WetStackMaxAlpha = DefaultWetStackMaxAlpha;

    [DefaultValue(DefaultOilTintStrength)]
    public float OilTintStrength = DefaultOilTintStrength;

    [DefaultValue(DefaultOilRpmExponent)]
    public float OilRpmExponent = DefaultOilRpmExponent;
}
