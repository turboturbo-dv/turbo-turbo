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
        "As a starting guideline, try to tune it so that lambda sits just above 1.3 " +
        "at full load.";

    public static Section Build(EngineSimulationHost host, Action onRequiresReconfigure, Action onToggle)
    {
        var model = host.CombustionModel;
        var aspirated = model.Charger is AtmosphericCharger;
        var section = new Section(aspirated ? "Atmospheric charger" : "Turbocharger", onRequiresReconfigure)
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
            section.AddFloat("Tau up",
                "Time constant (in seconds) for building turbo boost pressure in the intake manifold.\n" +
                "Higher values cause slower response when opening the throttle," +
                "causing it to take longer before the soot clears and the engine runs clean again.",
                0.25f, 8f, false, () => t.TauUp, v => t.TauUp = v, TweakGrade.Basic);
            section.AddFloat("Tau down",
                "Time constant (in seconds) for losing turbo boost pressure in the intake manifold.\n" +
                "Higher values cause boost pressure to remain high for longer when closing the throttle," +
                "producing less soot if the engine drops power briefly and then comes back on.",
                0.1f, 4f, false, () => t.TauDown, v => t.TauDown = v, TweakGrade.Basic);
            section.AddFloat("Boost charge multiplier",
                "Scales the maximum amount of boost pressure supplied by the turbocharger.\n" +
                "Changing this affects the amount of air available to the engine at full load (where the turbo " +
                "is most effective), but it has little effect at low load (where the turbo hardly does anything).\n" +
                "A realistic value will generally sit somewhere in the 2 - 2.5 range.\n" +
                "Adjusting this affects available air at full power, and therefore may require you to make a " +
                "corresponding change to lambda calibration as well.",
                0f, 3f, false, () => t.BoostChargeMultiplier, v => t.BoostChargeMultiplier = v);
            section.AddFloat("Boost curve shape",
                "The exponent that shapes the load-to-target boost curve. Values above 1 make it more exponential, " +
                "requiring less boost at low load, and more boost at high load, which matches real-world behaviour.",
                0f, 3f, false, () => t.RpmBoostExponent, v => t.RpmBoostExponent = v);
            section.AddFloat("Thermal K",
                "Thermal feedback strength. Higher values cause quicker boost build-up when overfueling.",
                0f, 3f, false, () => t.ThermalK, v => t.ThermalK = v);
            section.AddFloat("Tau up (min)",
                "Time constant floor, applied when building boost pressure. " +
                "Puts a lower limit on how much overfueling is able to shorten boost pressure build-up time.\n" +
                "This is mostly a safeguard and should not be relied on to change engine behaviour.",
                0.1f, 2f, false, () => t.MinSpoolTau, v => t.MinSpoolTau = v);

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
        }

        return section;
    }
}
