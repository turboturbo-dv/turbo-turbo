using System.ComponentModel;

namespace TurboTurbo.Profiles.Storage.V1;

public sealed class VelocityXml
{
    internal const float DefaultExhaustVelocityCoefficient = 5f;

    [DefaultValue(DefaultExhaustVelocityCoefficient)]
    public float ExhaustVelocityCoefficient = DefaultExhaustVelocityCoefficient;
}
