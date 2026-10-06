using System.ComponentModel;

namespace TurboTurbo.Profiles.Storage.V1;

public sealed class CombustionXml
{
    internal const float DefaultRatedExhaustTempK = 760f;

    [DefaultValue(DefaultRatedExhaustTempK)]
    public float RatedExhaustTempK { get; set; } = DefaultRatedExhaustTempK;
}
