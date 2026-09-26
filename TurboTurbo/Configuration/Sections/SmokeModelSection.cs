using System;
using System.Collections.Generic;
using System.Linq;

using TurboTurbo.Modeling;
using TurboTurbo.Runtime;

namespace TurboTurbo.Configuration.Sections;

internal static class SmokeModelSection
{
    public static Section Build(EngineSimulationHost host, Action onRequiresReconfigure, Action onToggle)
    {
        var models = new List<ExhaustSmokeModel>();
        foreach (var e in host.Exhausts)
        {
            models.Add(e.Smoke.Model);
        }

        if (models.Count == 0) return null;

        var section = new Section("Smoke model", onRequiresReconfigure) { OnToggle = onToggle };
        var first = models[0];

        Add("Clean burn load", "Load at which the idle haze is fully gone.", 0.05f, 1f,
            m => m.Tuning.CleanBurnHeat, (m, v) => m.Tuning.CleanBurnHeat = v);

        Add("Clean opacity (min load)", "Clean exhaust opacity at zero load.", 0f, 0.2f,
            m => m.Tuning.CleanMinHeatAlpha, (m, v) => m.Tuning.CleanMinHeatAlpha = v, TweakGrade.Basic);

        Add("Clean opacity (max load)", "Clean exhaust opacity at full load.", 0f, 0.5f,
            m => m.Tuning.CleanMaxHeatAlpha, (m, v) => m.Tuning.CleanMaxHeatAlpha = v, TweakGrade.Basic);

        Add("Soot onset lambda", "Lambda where soot starts forming. Normally does not need to be changed, adjust lambda calibration instead.", 0.9f, 1.5f,
            m => m.Tuning.SootOnsetLambda, (m, v) => m.Tuning.SootOnsetLambda = v);

        Add("Soot opaque lambda", "Lambda where soot reaches maximum opacity.", 0.5f, 1.2f,
            m => m.Tuning.SootOpaqueLambda, (m, v) => m.Tuning.SootOpaqueLambda = v);

        Add("Soot curve shape", "Exponent shaping the soot curve over the lambda deficit." +
                                "Values above 1 delay heavy soot until closer to the soot opaque lambda point.", 0.5f, 3f,
            m => m.Tuning.SootCurveExponent, (m, v) => m.Tuning.SootCurveExponent = v);

        Add("Soot opacity", "Maximum opacity of heavy soot. Lower this to make soot less intense.", 0f, 1f,
            m => m.Tuning.SootMaxAlpha, (m, v) => m.Tuning.SootMaxAlpha = v, TweakGrade.Basic);

        Add("Wet stack mist strength", "Intensity of the wet-stacking effect. Prolonged idling causes unburned fuel to " +
                                       "accumulate in the exhaust stack, which is released as white smoke when throttling " +
                                       "up.\nHigher values increase the intensity of this effect.", 0f, 5f,
            m => m.Tuning.WetStackMistStrength, (m, v) => m.Tuning.WetStackMistStrength = v, TweakGrade.Basic);

        Add("Wet stack fill load", "Load below which unburned fuel starts to accumulate in the exhaust stack.", 0f, 1f,
            m => m.Tuning.WetStackFillHeat, (m, v) => m.Tuning.WetStackFillHeat = v);

        Add("Wet stack release load", "Load above which unburned fuel starts to vaporise out of the exhaust stack, creating white smoke.", 0f, 1f,
            m => m.Tuning.WetStackReleaseHeat, (m, v) => m.Tuning.WetStackReleaseHeat = v);

        Add("Wet stack fill rate", "Wet stack fill rate at zero load.", 0f, 0.1f,
            m => m.Tuning.WetStackFillRate, (m, v) => m.Tuning.WetStackFillRate = v);

        Add("Wet stack release rate", "Wet stack release rate at full load.", 0f, 3f,
            m => m.Tuning.WetStackReleaseRate, (m, v) => m.Tuning.WetStackReleaseRate = v);

        Add("Wet stack opacity", "Maximum opacity of wet-stack mist. Should not normally require adjustment, change mist strength instead.", 0f, 1f,
            m => m.Tuning.WetStackMaxAlpha, (m, v) => m.Tuning.WetStackMaxAlpha = v);

        Add("Oil tint strength", "Intensity of the oil-burning effect. At high RPM, more oil leaks into the cylinders, " +
                                 "tinting exhaust smoke blue as it burns.", 0f, 1f,
            m => m.Tuning.OilTintStrength, (m, v) => m.Tuning.OilTintStrength = v, TweakGrade.Basic);

        Add("Oil tint curve shape", "RPM exponent on oil tint curve. \n" +
                                    " * Values above 1 delay the oil-burning effect further towards the high RPM range.\n" +
                                    " * Values below 1 make the effect more uniform regardless of RPM", 0.1f, 5f,
            m => m.Tuning.OilRpmExponent, (m, v) => m.Tuning.OilRpmExponent = v);

        section.AddProgressButton("Fill wet stack", "Immediately fill the wet stack to 100%, to test the effect.",
            () => { foreach (var m in models) m.FillWetStack(); },
            () => models.Max(m => m.WetStackAccumulator), TweakGrade.Basic);

        return section;

        void Add(string key, string tooltip, float min, float max,
            Func<ExhaustSmokeModel, float> get, Action<ExhaustSmokeModel, float> set,
            TweakGrade grade = TweakGrade.Advanced)
        {
            section.AddFloat(key, tooltip, min, max, false,
                () => get(first), v =>
                {
                    foreach (var m in models)
                    {
                        set(m, v);
                        m.Tuning.Validate();
                    }
                }, grade);
        }
    }
}