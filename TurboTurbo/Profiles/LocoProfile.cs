using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

using TurboTurbo.Modeling;
using TurboTurbo.WorkBench;

using UnityEngine;

namespace TurboTurbo.Profiles;

/// <summary>
/// Fully describes the engine configuration for a locomotive livery. Serializable.
/// </summary>
public sealed class LocoProfile
{
    public const int CurrentVersion = 1;

    public int Version { get; set; } = CurrentVersion;
    public string LiveryId { get; set; } = "";
    public bool Enabled { get; set; } = true;
    public List<LocoExhaust> Exhausts { get; set; } = new();
    public ChargerKind ChargerKind { get; set; } = ChargerKind.Turbo;

    // XmlSerializer needs unique type names, so each nested settings class needs an XmlType attribute
    public CombustionModel.Settings Combustion { get; set; }
    public TurboCharger.Settings TurboCharger { get; set; }
    public AtmosphericCharger.Settings Atmospheric { get; set; }
    public ExhaustSmokeModel.Settings Smoke { get; set; }
    public SmokeParticles.Settings SmokeEmitter { get; set; }
    public ShimmerParticles.Settings ShimmerEmitter { get; set; }
    public ExhaustVelocitySettings Velocity { get; set; }

    /// <summary>Builds the charger selected by this profile.</summary>
    public ICharger BuildCharger()
    {
        return ChargerKind == ChargerKind.Atmospheric
            ? new AtmosphericCharger(Atmospheric)
            : new TurboCharger(TurboCharger);
    }

    /// <summary>Returns a deep copy of this profile.</summary>
    public LocoProfile Clone()
    {
        return new LocoProfile
        {
            Version = Version,
            LiveryId = LiveryId,
            Enabled = Enabled,
            ChargerKind = ChargerKind,
            Exhausts = Exhausts?.Select(e => e.Clone()).ToList() ?? new List<LocoExhaust>(),
            Combustion = Combustion != null ? new CombustionModel.Settings(Combustion) : null,
            TurboCharger = TurboCharger != null ? new TurboCharger.Settings(TurboCharger) : null,
            Atmospheric = Atmospheric != null ? new AtmosphericCharger.Settings(Atmospheric) : null,
            Smoke = Smoke != null ? new ExhaustSmokeModel.Settings(Smoke) : null,
            SmokeEmitter = SmokeEmitter != null ? new SmokeParticles.Settings(SmokeEmitter) : null,
            ShimmerEmitter = ShimmerEmitter != null ? new ShimmerParticles.Settings(ShimmerEmitter) : null,
            Velocity = Velocity != null ? new ExhaustVelocitySettings(Velocity) : null,
        };
    }

    /// <summary>Structural validation. Returns an error, or null when the profile is usable.</summary>
    public ValidationError? Validate()
    {
        if (Version != CurrentVersion) return new ValidationError($"unknown version {Version}");
        if (string.IsNullOrWhiteSpace(LiveryId)) return new ValidationError("LiveryId is required");
        if (Exhausts == null || Exhausts.Count == 0)
            return new ValidationError("at least one exhaust is required");
        if (!Enum.IsDefined(typeof(ChargerKind), ChargerKind))
            return new ValidationError($"unknown charger kind {(int)ChargerKind}");

        foreach (var exhaust in Exhausts)
        {
            var error = ValidateExhaust(exhaust);
            if (error != null) return error;
        }

        return null;
    }

    private static ValidationError? ValidateExhaust(LocoExhaust exhaust)
    {
        if (exhaust == null) return new ValidationError("exhaust entry is null");
        if (!Enum.IsDefined(typeof(ExhaustKind), exhaust.Kind))
            return new ValidationError($"unknown exhaust kind {(int)exhaust.Kind}");
        if (!IsFinite(exhaust.Offset)) return new ValidationError("exhaust offset must be finite");
        if (exhaust.Kind == ExhaustKind.Replacement && string.IsNullOrWhiteSpace(exhaust.Name))
            return new ValidationError("replacement exhausts need a particle system name");

        return null;
    }

    private static bool IsFinite(Vector3 v) =>
        !float.IsNaN(v.x) && !float.IsInfinity(v.x)
        && !float.IsNaN(v.y) && !float.IsInfinity(v.y)
        && !float.IsNaN(v.z) && !float.IsInfinity(v.z);

    /// <summary>
    /// Validates and completes this profile: any uninitialized settings blocks are
    /// initialized to their default values. Settings for an unused charger kind are dropped.
    /// Returns an error if validation failed.
    /// </summary>
    internal ValidationError? Complete()
    {
        var error = Validate();
        if (error != null) return error;

        Combustion ??= new CombustionModel.Settings();
        Smoke ??= new ExhaustSmokeModel.Settings();
        SmokeEmitter ??= new SmokeParticles.Settings();
        ShimmerEmitter ??= new ShimmerParticles.Settings();
        Velocity ??= new ExhaustVelocitySettings();

        if (ChargerKind == ChargerKind.Atmospheric)
        {
            Atmospheric ??= new AtmosphericCharger.Settings();
            TurboCharger = null;
            Atmospheric.Validate();
        }
        else
        {
            TurboCharger ??= new TurboCharger.Settings();
            Atmospheric = null;
        }

        Smoke.Validate();
        return null;
    }
}

/// <summary>One exhaust entry in a <see cref="LocoProfile"/>.</summary>
public sealed class LocoExhaust
{
    public ExhaustKind Kind { get; set; } = ExhaustKind.Replacement;

    /// <summary>
    /// Set when declaring a particle system to replace. Leave unset for independent exhausts.
    /// </summary>
    [DefaultValue("")]
    public string Name { get; set; } = "";

    public Vector3 Offset { get; set; }

    public LocoExhaust Clone() => new() { Kind = Kind, Name = Name, Offset = Offset };
}

public enum ExhaustKind
{
    Replacement,
    Independent,
}