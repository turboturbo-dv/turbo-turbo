using System;

using UnityEngine;

namespace TurboTurbo.Modeling;

/// <summary>
/// Models naturally aspirated charging.
/// A naturally aspirated engine always charges to a slight vacuum, as the only way to get air into the cylinders is
/// through suction. As RPM rises, this effect becomes more pronounced, because the same cylinder volume needs to be
/// filled in less time.
/// </summary>
public sealed class AtmosphericCharger : ICharger
{
    /// <summary>Configuration constants. Defaults give a reasonable starting point.</summary>
    public sealed class Settings
    {
        internal const float DefaultEtaPeak = 0.9f;
        internal const float DefaultChokeK = 0.25f;
        internal const float DefaultChokeBeta = 2f;
        internal const float DefaultLambdaCalibration = 0.49f;
        internal const float DefaultCompressionRatio = 16f;
        internal const float DefaultIntakeNtu = 0.15f;

        public float EtaPeak { get; set; } = DefaultEtaPeak;

        public float ChokeK { get; set; } = DefaultChokeK;

        /// <summary>
        /// Exponent shaping the choke curve. 2 is physically accurate, as pressure drop across a restriction scales
        /// with the square of velocity.
        /// </summary>
        public float ChokeBeta { get; set; } = DefaultChokeBeta;

        public float LambdaCalibration { get; set; } = DefaultLambdaCalibration;

        public float CompressionRatio { get; set; } = DefaultCompressionRatio;

        public float IntakeNtu { get; set; } = DefaultIntakeNtu;

        public Settings()
        {
        }

        public Settings(Settings other)
        {
            EtaPeak = other.EtaPeak;
            ChokeK = other.ChokeK;
            ChokeBeta = other.ChokeBeta;
            LambdaCalibration = other.LambdaCalibration;
            CompressionRatio = other.CompressionRatio;
            IntakeNtu = other.IntakeNtu;
        }

        public void Validate()
        {
            EtaPeak = Mathf.Max(0.01f, EtaPeak);
            LambdaCalibration = Mathf.Max(0.01f, LambdaCalibration);
            ChokeK = Mathf.Clamp01(ChokeK);
            ChokeBeta = Mathf.Max(0f, ChokeBeta);
            CompressionRatio = Mathf.Max(1.01f, CompressionRatio);
            IntakeNtu = Mathf.Max(0f, IntakeNtu);
        }
    }

    public Settings Tuning { get; }

    public float Charge { get; private set; }

    public float RatedCharge => Tuning.EtaPeak * (1f - Tuning.ChokeK);

    public float Boost => 0f;

    public bool Surging => false;

    public float LambdaCalibration => Tuning.LambdaCalibration;

    public float CompressionRatio => Tuning.CompressionRatio;

    public float IntakeNtu => Tuning.IntakeNtu;

    // we'll just say it is 100% efficient.
    // it doesn't matter much as there is no compressor, so intake air gets no compression heating.
    public float CompressorEfficiency => 1f;

    public float TurbinePressureRatio => 1f;

    public AtmosphericCharger(Settings settings)
    {
        Tuning = settings ?? throw new ArgumentNullException(nameof(settings));

        // make sure a fresh instance always reads a valid charge level right away (0 isn't valid).
        // without this, the combustion model div by zero as it calculates lambda on its first tick
        Charge = settings.EtaPeak;
    }

    public void Tick(float delta, float rpmNorm, float governorNorm,
        float exhaustEnergy, bool engineOn)
    {
        var s = Tuning;

        var chokeFactor = (1f - s.ChokeK * Mathf.Pow(rpmNorm, s.ChokeBeta));
        Charge = s.EtaPeak * chokeFactor;
    }

    public ICharger Clone() => new AtmosphericCharger(new Settings(Tuning));
}
