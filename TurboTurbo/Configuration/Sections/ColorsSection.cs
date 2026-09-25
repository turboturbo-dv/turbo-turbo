using System;
using System.Collections.Generic;

using TurboTurbo.Modeling;
using TurboTurbo.Runtime;

using UnityEngine;

namespace TurboTurbo.Configuration.Sections;

internal static class ColorsSection
{
    public static Section Build(EngineSimulationHost host, Action onToggle, Action<ColorSpec> onRequestEdit)
    {
        var models = new List<ExhaustSmokeModel>();
        foreach (var e in host.Exhausts)
        {
            models.Add(e.Smoke.Model);
        }

        if (models.Count == 0) return null;

        var first = models[0];
        var section = new Section("smoke colors") { OnToggle = onToggle };
        Wire(section, "colorIdleHaze", "Haze tint at idle and low load.", onRequestEdit,
            () => first.Tuning.ColorIdleHaze, v => { foreach (var m in models) m.Tuning.ColorIdleHaze = v; });
        Wire(section, "colorCleanBurn", "Clean burn tint.", onRequestEdit,
            () => first.Tuning.ColorCleanBurn, v => { foreach (var m in models) m.Tuning.ColorCleanBurn = v; });
        Wire(section, "colorHeavySoot", "Soot tint.", onRequestEdit,
            () => first.Tuning.ColorHeavySoot, v => { foreach (var m in models) m.Tuning.ColorHeavySoot = v; });
        Wire(section, "colorWetStack", "Wet stack burn tint.", onRequestEdit,
            () => first.Tuning.ColorWetStack, v => { foreach (var m in models) m.Tuning.ColorWetStack = v; });
        Wire(section, "colorOilBurn", "Oil burn tint.", onRequestEdit,
            () => first.Tuning.ColorOilBurn, v => { foreach (var m in models) m.Tuning.ColorOilBurn = v; });
        return section;
    }

    private static void Wire(Section section, string key, string tooltip, Action<ColorSpec> onRequestEdit,
        Func<Color> get, Action<Color> set)
    {
        var spec = section.AddColor(key, tooltip, get, set);
        spec.RequestEdit += () => onRequestEdit(spec);
    }
}