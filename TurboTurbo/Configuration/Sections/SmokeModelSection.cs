using System;
using System.Collections.Generic;

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

        var section = new Section("smoke model", "smokeModel", onRequiresReconfigure) { OnToggle = onToggle };
        var first = models[0];

        void Add(string key, string tooltip, float min, float max,
            Func<ExhaustSmokeModel, float> get, Action<ExhaustSmokeModel, float> set)
        {
            section.AddFloat(key, tooltip, min, max, false,
                () => get(first), v =>
                {
                    foreach (var m in models)
                    {
                        set(m, v);
                        m.Tuning.Validate();
                    }
                });
        }

        Add("cleanMinHeatAlpha", "Clean exhaust opacity at zero heat.", 0f, 0.2f,
            m => m.Tuning.CleanMinHeatAlpha, (m, v) => m.Tuning.CleanMinHeatAlpha = v);
        Add("cleanMaxHeatAlpha", "Clean exhaust opacity at full heat.", 0f, 0.5f,
            m => m.Tuning.CleanMaxHeatAlpha, (m, v) => m.Tuning.CleanMaxHeatAlpha = v);
        Add("cleanBurnHeat", "Heat at which the idle haze is fully gone.", 0.05f, 1f,
            m => m.Tuning.CleanBurnHeat, (m, v) => m.Tuning.CleanBurnHeat = v);
        Add("sootOnsetLambda", "Lambda where soot starts forming.", 0.3f, 1.5f,
            m => m.Tuning.SootOnsetLambda, (m, v) => m.Tuning.SootOnsetLambda = v);
        Add("sootOpaqueLambda", "Lambda where soot reaches maximum opacity.", 0.1f, 1f,
            m => m.Tuning.SootOpaqueLambda, (m, v) => m.Tuning.SootOpaqueLambda = v);
        Add("sootCurveExponent", "Gamma shaping the soot ladder over the lambda deficit.", 0.5f, 3f,
            m => m.Tuning.SootCurveExponent, (m, v) => m.Tuning.SootCurveExponent = v);
        Add("sootMaxAlpha", "Opacity contribution of fully developed soot.", 0f, 1f,
            m => m.Tuning.SootMaxAlpha, (m, v) => m.Tuning.SootMaxAlpha = v);
        Add("wetStackFillHeat", "Heat below which wet stacking starts to occur.", 0f, 1f,
            m => m.Tuning.WetStackFillHeat, (m, v) => m.Tuning.WetStackFillHeat = v);
        Add("wetStackReleaseHeat", "Heat above which the wet stack starts to release.", 0f, 1f,
            m => m.Tuning.WetStackReleaseHeat, (m, v) => m.Tuning.WetStackReleaseHeat = v);
        Add("wetStackFillRate", "Accumulator fill rate [1/s] at zero heat.", 0f, 0.1f,
            m => m.Tuning.WetStackFillRate, (m, v) => m.Tuning.WetStackFillRate = v);
        Add("wetStackReleaseRate", "Release rate [1/s] at full heat.", 0f, 3f,
            m => m.Tuning.WetStackReleaseRate, (m, v) => m.Tuning.WetStackReleaseRate = v);
        Add("wetStackMistStrength", "How strongly the release rate converts into visible mist.", 0f, 5f,
            m => m.Tuning.WetStackMistStrength, (m, v) => m.Tuning.WetStackMistStrength = v);
        Add("wetStackMaxAlpha", "Opacity contribution of the wet-stack mist.", 0f, 1f,
            m => m.Tuning.WetStackMaxAlpha, (m, v) => m.Tuning.WetStackMaxAlpha = v);
        Add("oilTintStrength", "Max blend toward the oil-burn color, reached at high rpm.", 0f, 1f,
            m => m.Tuning.OilTintStrength, (m, v) => m.Tuning.OilTintStrength = v);
        Add("oilRpmExponent", "RPM exponent on the oil tint. Higher keeps oil coloration out of the low RPM range.", 0.1f, 5f,
            m => m.Tuning.OilRpmExponent, (m, v) => m.Tuning.OilRpmExponent = v);
        section.AddButton("fillWetStack", "Fill the wet-stack accumulator to 1.",
            () => { foreach (var m in models) m.FillWetStack(); });
        return section;
    }
}