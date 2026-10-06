using System.Xml.Serialization;

using TurboTurbo.Profiles.Storage.V1;

namespace TurboTurbo.Profiles.Storage;

/// <summary>Source of truth for the TurboConfig.xml format.</summary>
internal static class TurboConfigCodec
{
    public const string FileName = "TurboConfig.xml";

    public static readonly XmlSerializer Serializer = new(typeof(TurboConfigXml));
}
