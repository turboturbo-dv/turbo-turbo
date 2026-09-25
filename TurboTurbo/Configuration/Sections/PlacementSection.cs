using System;

using TurboTurbo.Runtime;

namespace TurboTurbo.Configuration.Sections;

internal static class PlacementSection
{
    public static Section Build(EngineSimulationHost host, Action onToggle, Func<bool> getMarkersVisible, Action<bool> setMarkersVisible)
    {
        var exhausts = host.Exhausts;
        if (exhausts.Count == 0) return null;

        var section = new Section("exhaust placement") { OnToggle = onToggle };
        for (var i = 0; i < exhausts.Count; i++)
        {
            var e = exhausts[i];
            section.AddFloat($"exhaust{i}OffsetX", "Exhaust placement offset [m], lateral.",
                -1f, 1f, false,
                () => e.Offset.x,
                v => { e.Offset.x = v; e.Source.Offset = e.Offset; e.Reposition(); });
            section.AddFloat($"exhaust{i}OffsetY", "Exhaust placement offset [m], vertical.",
                -1f, 2f, false,
                () => e.Offset.y,
                v => { e.Offset.y = v; e.Source.Offset = e.Offset; e.Reposition(); });
            section.AddFloat($"exhaust{i}OffsetZ", "Exhaust placement offset [m], fore/aft.",
                -1f, 1f, false,
                () => e.Offset.z,
                v => { e.Offset.z = v; e.Source.Offset = e.Offset; e.Reposition(); });
        }
        section.AddBool("showMarkers", "Debug: show axis crosses at the modded emitter positions.",
            false, () => getMarkersVisible(), setMarkersVisible);
        return section;
    }
}