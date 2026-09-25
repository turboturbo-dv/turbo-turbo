using System;
using System.ComponentModel;
using System.Xml.Serialization;

using UnityEngine;

namespace TurboTurbo.Modeling;

/// <summary>Models naturally aspirated charging. Charge depends only on engine RPM.</summary>
public sealed class AtmosphericCharger : ICharger
{
    /// <summary>Configuration constants. Defaults give a reasonable starting point.</summary>
    [XmlType("AtmosphericChargerSettings")]
    public sealed class Settings
    {
        internal const float DefaultEtaPeak = 0.9f;
        internal const float DefaultChokeK = 0.25f;
        internal const float DefaultChokeBeta = 2f;
        internal const float DefaultLambdaCalibration = 0.49f;

        [DefaultValue(DefaultEtaPeak)]
        public float EtaPeak { get; set; } = DefaultEtaPeak;

        [DefaultValue(DefaultChokeK)]
        public float ChokeK { get; set; } = DefaultChokeK;

        [DefaultValue(DefaultChokeBeta)]
        public float ChokeBeta { get; set; } = DefaultChokeBeta;

        [DefaultValue(DefaultLambdaCalibration)]
        public float LambdaCalibration { get; set; } = DefaultLambdaCalibration;

        public Settings()
        {
        }

        public Settings(Settings other)
        {
            EtaPeak = other.EtaPeak;
            ChokeK = other.ChokeK;
            ChokeBeta = other.ChokeBeta;
            LambdaCalibration = other.LambdaCalibration;
        }

        public void Validate()
        {
            EtaPeak = Mathf.Max(0.01f, EtaPeak);
            ChokeK = Mathf.Clamp01(ChokeK);
            ChokeBeta = Mathf.Max(0f, ChokeBeta);
        }
    }

    private readonly Settings _tuning;

    public Settings Tuning => _tuning;

    public float Charge { get; private set; }

    public float Boost => 0f;

    public bool Surging => false;

    public float LambdaCalibration => _tuning.LambdaCalibration;

    public float ExhaustHeat { get; private set; }

    public AtmosphericCharger(Settings settings)
    {
        _tuning = settings ?? throw new ArgumentNullException(nameof(settings));

        // make sure a fresh instance always reads a valid charge level right away (0 isn't valid).
        // without this, the combustion model div by zero as it calculates lambda on its first tick
        Charge = settings.EtaPeak;
    }

    public void Tick(float delta, float fuelPerStroke, float overfuel, float rpmNorm, float governorNorm, bool engineOn)
    {
        var s = _tuning;

        var chokeFactor = (1f - s.ChokeK * Mathf.Pow(rpmNorm, s.ChokeBeta));
        Charge = s.EtaPeak * chokeFactor;
        ExhaustHeat = fuelPerStroke * Charge;
    }

    public ICharger Clone() => new AtmosphericCharger(new Settings(_tuning));
}