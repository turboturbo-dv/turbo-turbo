using System.ComponentModel;

namespace TurboTurbo.Profiles.Storage.Current;

public sealed class RootsChargerXml
{
    internal const float DefaultPressureRatio = 1.26f;
    internal const float DefaultBoostExponent = 2f;
    internal const float DefaultCompressorEfficiency = 0.6f;
    internal const float DefaultLambdaCalibration = 0.9f;
    internal const float DefaultCompressionRatio = 13f;
    internal const float DefaultIntakeNtu = 0.15f;

    [DefaultValue(DefaultPressureRatio)]
    public float PressureRatio { get; set; } = DefaultPressureRatio;

    [DefaultValue(DefaultBoostExponent)]
    public float BoostExponent { get; set; } = DefaultBoostExponent;

    [DefaultValue(DefaultCompressorEfficiency)]
    public float CompressorEfficiency { get; set; } = DefaultCompressorEfficiency;

    [DefaultValue(DefaultLambdaCalibration)]
    public float LambdaCalibration { get; set; } = DefaultLambdaCalibration;

    [DefaultValue(DefaultCompressionRatio)]
    public float CompressionRatio { get; set; } = DefaultCompressionRatio;

    [DefaultValue(DefaultIntakeNtu)]
    public float IntakeNtu { get; set; } = DefaultIntakeNtu;
}
