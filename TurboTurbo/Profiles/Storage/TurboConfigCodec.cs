using System.Xml.Serialization;

namespace TurboTurbo.Profiles.Storage;

/// <summary>Source of truth for the TurboConfig.xml format.</summary>
internal static class TurboConfigCodec
{
    public const string FileName = "TurboConfig.xml";

    public static readonly XmlSerializer Serializer = new(typeof(TurboConfig));
}
