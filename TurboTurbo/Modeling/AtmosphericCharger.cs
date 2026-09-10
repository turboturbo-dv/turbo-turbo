using System;

using UnityEngine;

namespace TurboTurbo.Modeling;

/// <summary>Models naturally aspirated charging. Charge depends only on engine RPM.</summary>
public sealed class AtmosphericCharger : ICharger
{
    /// <summary>Configuration constants. Defaults give a reasonable starting point.</summary>
    public sealed class Settings
    {
        public float EtaPeak { get; set; } = 0.9f;
        public float ChokeK { get; set; } = 0.25f;
        public float ChokeBeta { get; set; } = 2f;
        public float LambdaCalibration { get; set; } = 0.6f;

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

    public void Tick(float delta, float fuelDemand, float overfuel, float rpmNorm, float throttle, bool engineOn)
    {
        var s = _tuning;

        var chokeFactor = (1f - s.ChokeK * Mathf.Pow(rpmNorm, s.ChokeBeta));
        Charge = s.EtaPeak * chokeFactor;
        ExhaustHeat = fuelDemand * Charge;
    }

    public ICharger Clone() => new AtmosphericCharger(new Settings(_tuning));
}