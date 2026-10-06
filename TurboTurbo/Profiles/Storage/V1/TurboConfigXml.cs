using System.Collections.Generic;
using System.Xml.Serialization;

namespace TurboTurbo.Profiles.Storage.V1;

[XmlRoot("TurboConfig")]
public sealed class TurboConfigXml
{
    public List<LocoProfileXml> LocoProfiles = new();
}
