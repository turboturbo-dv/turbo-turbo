using System.Collections.Generic;
using System.ComponentModel;
using System.Xml.Serialization;

using TurboTurbo.Modeling;

using UnityEngine;

namespace TurboTurbo.Profiles.Storage.Current;

[XmlRoot("LocoProfile")]
[XmlType("LocoProfile")]
public sealed class LocoProfileXml
{
    public int Version { get; set; } = ProfileSchema.CurrentVersion;
    public string LiveryId { get; set; } = "";
    public bool Enabled { get; set; } = true;
    public List<ExhaustXml> Exhausts { get; set; } = new();
    public ChargerKind ChargerKind { get; set; } = ChargerKind.Turbo;

    public TurboChargerXml TurboCharger { get; set; }
    public AtmosphericXml Atmospheric { get; set; }
    public RootsChargerXml Roots { get; set; }
    public SmokeModelXml Smoke { get; set; }
    public SmokeEmitterXml SmokeEmitter { get; set; }
    public ShimmerEmitterXml ShimmerEmitter { get; set; }
    public VelocityXml Velocity { get; set; }
    public CombustionXml Combustion { get; set; }
    public StackXml Stack { get; set; }
}

[XmlType("LocoExhaust")]
public sealed class ExhaustXml
{
    public ExhaustKind Kind { get; set; } = ExhaustKind.Replacement;

    [DefaultValue("")]
    public string Path { get; set; } = "";

    public Vector3 Offset { get; set; }
}
