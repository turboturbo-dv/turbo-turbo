using System;

using UnityEngine;

namespace TurboTurbo.Modeling;

/// <summary>
/// Models a Roots blower feeding a two-stroke engine.
/// A Roots blower is geared to the crankshaft, so its charge delivered is proportional to engine speed.
/// </summary>
public sealed class RootsCharger : ICharger
{
    /// <summary>Configuration constants. Defaults give a reasonable starting point.</summary>
    public sealed class Settings
    {
        // this is justified by at least one data point (EMD 16-567 air box pressure at 800 rpm = 7.9 inHg)
        internal const float DefaultPressureRatio = 1.26f;
        internal const float DefaultBoostExponent = 2f;

        // a roots blower is rather inefficient due to air rushing in when the lobes open, so this is quite low.
        // EMD's numbers put the 567 blower at 0.49 on shaft power, but we're modelling pure compressor efficiency,
        // so we can reasonably assume slightly higher efficiency here
        internal const float DefaultCompressorEfficiency = 0.6f;
        internal const float DefaultLambdaCalibration = 0.9f;
        // the cylinder seals when the exhaust valves close, 60 degrees after bottom dead centre: 16:1 nominal
        // gives 12.9:1 effective. this also reproduces the 567's published compression pressures
        internal const float DefaultCompressionRatio = 13f;
        internal const float DefaultIntakeNtu = 0.15f;

        public float PressureRatio { get; set; } = DefaultPressureRatio;

        public float BoostExponent { get; set; } = DefaultBoostExponent;

        public float CompressorEfficiency { get; set; } = DefaultCompressorEfficiency;

        public float LambdaCalibration { get; set; } = DefaultLambdaCalibration;

        public float CompressionRatio { get; set; } = DefaultCompressionRatio;

        public float IntakeNtu { get; set; } = DefaultIntakeNtu;

        public Settings()
        {
        }

        public Settings(Settings other)
        {
            PressureRatio = other.PressureRatio;
            BoostExponent = other.BoostExponent;
            CompressorEfficiency = other.CompressorEfficiency;
            LambdaCalibration = other.LambdaCalibration;
            CompressionRatio = other.CompressionRatio;
            IntakeNtu = other.IntakeNtu;
        }

        public void Validate()
        {
            PressureRatio = Mathf.Max(1f, PressureRatio);
            BoostExponent = Mathf.Max(0f, BoostExponent);
            CompressorEfficiency = Mathf.Clamp(CompressorEfficiency, 0.05f, 1f);
            LambdaCalibration = Mathf.Max(0.01f, LambdaCalibration);
            CompressionRatio = Mathf.Max(1.01f, CompressionRatio);
            IntakeNtu = Mathf.Max(0f, IntakeNtu);
        }
    }

    public Settings Tuning { get; }

    public float Charge { get; private set; }

    public float RatedCharge => Tuning.PressureRatio;

    public float Boost { get; private set; }

    // no surge (obviously), because it's tied to the crankshaft
    public bool Surging => false;

    public float LambdaCalibration => Tuning.LambdaCalibration;

    public float CompressionRatio => Tuning.CompressionRatio;

    public float IntakeNtu => Tuning.IntakeNtu;

    public float CompressorEfficiency => Tuning.CompressorEfficiency;

    public float TurbinePressureRatio => 1f;

    public RootsCharger(Settings settings)
    {
        Tuning = settings ?? throw new ArgumentNullException(nameof(settings));

        // correct for a stationary blower, and this also ensures we have a valid charge on first tick
        Charge = 1f;
    }

    public void Tick(float delta, float rpmNorm, float governorNorm,
        float exhaustEnergy, bool engineOn)
    {
        var s = Tuning;

        Boost = Mathf.Pow(rpmNorm, s.BoostExponent);
        Charge = 1f + (s.PressureRatio - 1f) * Boost;
    }

    public ICharger Clone() => new RootsCharger(new Settings(Tuning));
}
