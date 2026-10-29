using System;

using TurboTurbo.Modeling;
using TurboTurbo.Runtime;

namespace TurboTurbo.Configuration.Sections;

internal static class ChargerSection
{
    private const string LambdaCalibrationToolTip =
        "Adjust this to calibrate Lambda (air-to-fuel ratio) inside the engine.\n" +
        "Affects the amount of soot the engine produces at all power levels:\n" +
        " * Raise this to make the engine run richer.\n" +
        " * Lower it to make it run leaner.\n\n" +
        "As a starting guideline, bring the engine to full load, then adjust the calibration so that lambda remains just above 1.3.";

    private const string CompressionRatioToolTip =
        "Effective compression ratio of the cylinders, counted from the moment the cylinder is sealed.\n" +
        "Higher values result in hotter compression. Naturally aspirated engines " +
        "typically sit near 16, turbocharged engines somewhat lower, roots-blown around 13.\n" +
        "Note that nominal compression ratio is often the published value, but that is not what this is. " +
        "The effective ratio is always lower, by an amount that generally depends on the type of engine and charger.";

    private const string IntakeNtuToolTip =
        "Intake manifold heat transfer effectiveness at rated flow.\n" +
        "When intake air is cooler than the block, the air is warmed up by it. If it is warmer (generally as a result of " +
        "being warmed up by compression in the intake) the air is instead cooled down.\n" +
        " * On a naturally aspirated engine, this value tends to be low, as air only exchanges heat while flowing through the manifold.\n" +
        " * On a turbocharged engine equipped with an intercooler, this value will be high.\n" +
        " * On a roots-blown engine, there is normally no intercooler installed, so it will again be quite low.";

    public static Section Build(EngineSimulationHost host, Action onRequiresReconfigure, Action onToggle)
    {
        var model = host.CombustionModel;
        var title = model.Charger switch
        {
            AtmosphericCharger => "Atmospheric charger",
            RootsCharger => "Roots blower",
            _ => "Turbocharger",
        };
        var section = new Section(title, onRequiresReconfigure)
        {
            Open = true,
            OnToggle = onToggle,
        };
        if (model.Charger is TurboCharger turbo)
        {
            var t = turbo.Tuning;
            section.AddFloat("Lambda calibration",
                LambdaCalibrationToolTip,
                1f, 4f, false, () => t.LambdaCalibration, v => t.LambdaCalibration = v, TweakGrade.Basic);
            section.AddFloat("Spool up",
                "Time constant (in seconds) for building turbo boost pressure in the intake manifold.\n" +
                "Higher values cause slower response when opening the throttle," +
                "causing it to take longer before the soot clears and the engine runs clean again.",
                0.25f, 8f, false, () => t.TauUp, v => t.TauUp = v, TweakGrade.Basic);
            section.AddFloat("Spool down",
                "Time constant (in seconds) for losing turbo boost pressure in the intake manifold.\n" +
                "Higher values cause boost pressure to remain high for longer when closing the throttle," +
                "producing less soot if the engine drops power briefly and then comes back on.",
                0.1f, 4f, false, () => t.TauDown, v => t.TauDown = v, TweakGrade.Basic);
            section.AddFloat("Boost charge multiplier",
                "Scales the maximum amount of boost pressure supplied by the turbocharger, on top of atmospheric.\n" +
                "Changing this affects the amount of air available to the engine at full load (where the turbo " +
                "is most effective), but it has little effect at low load (where the turbo hardly does anything).\n" +
                "A realistic value will generally sit somewhere in the 1 - 1.8 range.\n" +
                "Adjusting this affects available air at full power, and therefore may require you to make a " +
                "corresponding change to lambda calibration as well.",
                0f, 2.5f, false, () => t.BoostChargeMultiplier, v => t.BoostChargeMultiplier = v);
            section.AddFloat("Compression ratio",
                CompressionRatioToolTip,
                10f, 25f, false, () => t.CompressionRatio, v => t.CompressionRatio = v, TweakGrade.Developer);
            section.AddFloat("Intake NTU",
                IntakeNtuToolTip,
                0f, 3f, false, () => t.IntakeNtu, v => t.IntakeNtu = v, TweakGrade.Developer);

            // not yet used
            // section.AddFloat("surgeRateThreshold",
            //     "Demand drop rate [1/s] that triggers a surge while boost is above 0.75.",
            //     0f, 60f, false, () => t.SurgeRateThreshold, v => t.SurgeRateThreshold = v);
        }
        else if (model.Charger is AtmosphericCharger na)
        {
            var a = na.Tuning;
            section.AddFloat("Lambda calibration",
                LambdaCalibrationToolTip,
                0.2f, 1.5f, false, () => a.LambdaCalibration, v => a.LambdaCalibration = v, TweakGrade.Basic);
            section.AddFloat("Maximum charge",
                "Theoretical maximum achievable charge density.\n" +
                "In practice, charge will always be lower than this because of choke losses that scale with RPM.\n" +
                "Realisic values tend to sit in the 0.8 - 0.9 range, with older engines more likely to sit at the " +
                "bottom of this range.",
                0.5f, 1f, false, () => a.EtaPeak, v => { a.EtaPeak = v; a.Validate(); });
            section.AddFloat("Choke curve multiplier",
                "High-RPM breathing loss factor. Higher values cause more choke loss overall.\n" +
                "Realistic values range from 0.1 (modern, efficient engines) to 0.4 (old, inefficient, or intake restricted engines)",
                0f, 1f, false, () => a.ChokeK, v => { a.ChokeK = v; a.Validate(); });
            section.AddFloat("Choke curve shape",
                "High-RPM breathing loss exponent. Shapes the choke loss curve. Values above 1 delay choke losses " +
                "to the top RPM range.\nThe default value of 2 is generally realistic here (pressure loss scales with " +
                "the square of RPM).",
                0f, 4f, false, () => a.ChokeBeta, v => { a.ChokeBeta = v; a.Validate(); });
            section.AddFloat("Compression ratio",
                CompressionRatioToolTip,
                10f, 25f, false, () => a.CompressionRatio, v => { a.CompressionRatio = v; a.Validate(); }, TweakGrade.Developer);
            section.AddFloat("Intake NTU",
                IntakeNtuToolTip,
                0f, 3f, false, () => a.IntakeNtu, v => { a.IntakeNtu = v; a.Validate(); }, TweakGrade.Developer);
        }
        else if (model.Charger is RootsCharger roots)
        {
            var r = roots.Tuning;
            section.AddFloat("Lambda calibration",
                LambdaCalibrationToolTip,
                0.2f, 2f, false, () => r.LambdaCalibration, v => { r.LambdaCalibration = v; r.Validate(); }, TweakGrade.Basic);
            section.AddFloat("Pressure ratio",
                "Air box pressure at full engine speed, relative to atmospheric.\n" +
                "This sets how much air the blower supplies at full speed. At lower speeds, boost falls off quickly, " +
                "so it has much less effect there.\n" +
                "Roots-blown engines sit around 1.2 - 1.3 (an EMD 567 is about 1.26).\n" +
                "Adjusting this affects available air at full power, and therefore may require you to make a " +
                "corresponding change to lambda calibration as well.",
                1f, 1.6f, false, () => r.PressureRatio, v => { r.PressureRatio = v; r.Validate(); }, TweakGrade.Basic);
            section.AddFloat("Boost curve shape",
                "Shapes how boost builds up with engine speed. Higher values delay boost until higher RPM, " +
                "resulting in more smoke when throttling up from low speed.\n" +
                "The default value of 2 is generally realistic here (pressure scales with the square of RPM).",
                1f, 3f, false, () => r.BoostExponent, v => { r.BoostExponent = v; r.Validate(); });
            section.AddFloat("Blower efficiency",
                "How efficiently the blower compresses air. Less efficiency means air heats up more by being compressed.\n" +
                "As roots blowers are quite inefficient, expect a realistic value to be in the range of 0.5 - 0.65.",
                0.3f, 1f, false, () => r.CompressorEfficiency, v => { r.CompressorEfficiency = v; r.Validate(); },
                TweakGrade.Developer);
            section.AddFloat("Compression ratio",
                CompressionRatioToolTip,
                10f, 25f, false, () => r.CompressionRatio, v => { r.CompressionRatio = v; r.Validate(); }, TweakGrade.Developer);
            section.AddFloat("Intake NTU",
                IntakeNtuToolTip,
                0f, 3f, false, () => r.IntakeNtu, v => { r.IntakeNtu = v; r.Validate(); }, TweakGrade.Developer);
        }

        return section;
    }
}
