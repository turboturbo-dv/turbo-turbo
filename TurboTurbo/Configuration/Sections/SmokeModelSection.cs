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

        Add("Smoke density", "Scales the total volume of smoke produced.\n" +
                             "This should be one of the first things you adjust in a new profile. To start, try choosing " +
                             "a value that matches an equally powerful built-in locomotive. The built-ins use:\n" +
                             $"  * DE6: {StockConfiguration.De6Density}\n" +
                             $"  * DH4: {StockConfiguration.Dh4Density}\n" +
                             $"  * DM3: {StockConfiguration.Dm3Density}\n" +
                             $"  * DE2: {StockConfiguration.De2Density}\n\n" +
                             "Then further adjust as appropriate until the smoke looks right.\n" +
                             "To adjust the proportions of different smoke types relative to each other, change the " +
                             "relative smoke opacities below.", 0f, 250f,
            m => m.Tuning.Density, (m, v) => m.Tuning.Density = v, TweakGrade.Basic, requiresReconfigure: true);

        Add("Clean burn load", "Load at which the idle haze is fully gone.", 0.05f, 1f,
            m => m.Tuning.CleanBurnHeat, (m, v) => m.Tuning.CleanBurnHeat = v);

        Add("Clean smoke weight (min)", "Intensity of clean exhaust at zero load, relative to other types of smoke.", 0f, 0.1f,
            m => m.Tuning.CleanMinHeatAlpha, (m, v) => m.Tuning.CleanMinHeatAlpha = v, TweakGrade.Basic);

        Add("Clean smoke weight (max)", "Intensity of clean exhaust at full load, relative to other types of smoke.", 0f, 0.5f,
            m => m.Tuning.CleanMaxHeatAlpha, (m, v) => m.Tuning.CleanMaxHeatAlpha = v, TweakGrade.Basic);

        Add("Soot weight", "Intensity of heavy soot at maximum opacity, relative to other types of smoke. ", 0f, 1f,
            m => m.Tuning.SootMaxAlpha, (m, v) => m.Tuning.SootMaxAlpha = v, TweakGrade.Basic);

        Add("Soot curve shape", "Exponent shaping the soot curve over the lambda deficit.\n" +
                                "Values above 1 delay heavy soot until closer to the soot opaque lambda point.", 0.5f, 3f,
            m => m.Tuning.SootCurveExponent, (m, v) => m.Tuning.SootCurveExponent = v);

        Add("Soot increase time", "Time constant (seconds) easing soot in when lambda suddenly drops into the sooty range.\n" +
                                "Keep this slightly above zero to avoid harsh single-frame transitions.", 0.01f, 0.5f,
            m => m.Tuning.SootIncreaseTau, (m, v) => m.Tuning.SootIncreaseTau = v);

        Add("Soot decrease time", "Time constant (seconds) easing soot out when lambda recovers.\n" +
                                 "Keep this a bit higher to represent that it takes a moment for soot to clear from the" +
                                 " exhaust stack, even during a rapid throttle cut where combustion instantly goes clean.", 0.01f, 2f,
            m => m.Tuning.SootDecreaseTau, (m, v) => m.Tuning.SootDecreaseTau = v);

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

        Add("Wet stack weight", "Intensity of wet-stack mist, relative to other types of smoke.\n" +
                                "Should not normally require adjustment, change mist strength instead.", 0f, 1f,
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
            TweakGrade grade = TweakGrade.Advanced, bool requiresReconfigure = false)
        {
            section.AddFloat(key, tooltip, min, max, requiresReconfigure,
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