using System;
using System.Collections.Generic;
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

    // XmlSerializer needs unique type names, so each nested Settings class
    // carries an XmlType attribute. Null blocks serialize as no element.
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
            Exhausts = Exhausts?.Select(e => new LocoExhaust { Kind = e.Kind, Name = e.Name, Offset = e.Offset }).ToList()
                       ?? new List<LocoExhaust>(),
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
    public string Validate()
    {
        if (Version != CurrentVersion) return $"unknown version {Version}";
        if (string.IsNullOrWhiteSpace(LiveryId)) return "LiveryId is required";
        if (Exhausts == null || Exhausts.Count == 0) return "at least one exhaust is required";
        if (!Enum.IsDefined(typeof(ChargerKind), ChargerKind)) return $"unknown charger kind {(int)ChargerKind}";
        foreach (var exhaust in Exhausts)
        {
            var error = ValidateExhaust(exhaust);
            if (error != null) return error;
        }
        return null;
    }

    private static string ValidateExhaust(LocoExhaust exhaust)
    {
        if (exhaust == null) return "exhaust entry is null";
        if (!Enum.IsDefined(typeof(ExhaustKind), exhaust.Kind)) return $"unknown exhaust kind {(int)exhaust.Kind}";
        if (!IsFinite(exhaust.Offset)) return "exhaust offset must be finite";
        if (exhaust.Kind == ExhaustKind.Replacement && string.IsNullOrWhiteSpace(exhaust.Name))
            return "replacement exhausts need a particle system name";
        return null;
    }

    private static bool IsFinite(Vector3 v) =>
        !float.IsNaN(v.x) && !float.IsInfinity(v.x)
        && !float.IsNaN(v.y) && !float.IsInfinity(v.y)
        && !float.IsNaN(v.z) && !float.IsInfinity(v.z);
}

/// <summary>One exhaust entry in a <see cref="LocoProfile"/>.</summary>
public sealed class LocoExhaust
{
    public ExhaustKind Kind { get; set; } = ExhaustKind.Replacement;
    /// <summary>
    /// Exact particle system name for replacements. Ignored for independent exhausts,
    /// which hang off the car's own transform.
    /// </summary>
    public string Name { get; set; } = "";

    public Vector3 Offset { get; set; }
}

public enum ExhaustKind
{
    Replacement,
    Independent,
}