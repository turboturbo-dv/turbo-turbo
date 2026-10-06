using System.ComponentModel;

namespace TurboTurbo.Profiles.Storage.V1;

public sealed class AtmosphericXml
{
    internal const float DefaultEtaPeak = 0.9f;
    internal const float DefaultChokeK = 0.25f;
    internal const float DefaultChokeBeta = 2f;
    internal const float DefaultLambdaCalibration = 0.49f;

    [DefaultValue(DefaultEtaPeak)]
    public float EtaPeak { get; set; } = DefaultEtaPeak;

    [DefaultValue(DefaultChokeK)]
    public float ChokeK { get; set; } = DefaultChokeK;

    [DefaultValue(DefaultChokeBeta)]
    public float ChokeBeta { get; set; } = DefaultChokeBeta;

    [DefaultValue(DefaultLambdaCalibration)]
    public float LambdaCalibration { get; set; } = DefaultLambdaCalibration;
}
