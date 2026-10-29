using System;
using System.Collections.Generic;
using System.Linq;

using TurboTurbo.Effects;
using TurboTurbo.Modeling;

using UnityEngine;

namespace TurboTurbo.Profiles;

/// <summary>
/// Fully describes the engine configuration for a locomotive livery. Serializable.
/// </summary>
public sealed class LocoProfile
{
    public string LiveryId { get; set; } = "";
    public bool Enabled { get; set; } = true;
    public List<LocoExhaust> Exhausts { get; set; } = new();
    public ChargerKind ChargerKind { get; set; } = ChargerKind.Turbo;

    public TurboCharger.Settings TurboCharger { get; set; }
    public AtmosphericCharger.Settings Atmospheric { get; set; }
    public RootsCharger.Settings Roots { get; set; }
    public ExhaustSmokeModel.Settings Smoke { get; set; }
    public SmokeParticles.Settings SmokeEmitter { get; set; }
    public ShimmerParticles.Settings ShimmerEmitter { get; set; }
    public ExhaustVelocitySettings Velocity { get; set; }
    public CombustionModel.Settings Combustion { get; set; }
    public StackModel.Settings Stack { get; set; }

    /// <summary>Builds the charger selected by this profile.</summary>
    public ICharger BuildCharger()
    {
        return ChargerKind switch
        {
            ChargerKind.Atmospheric => new AtmosphericCharger(Atmospheric),
            ChargerKind.Roots => new RootsCharger(Roots),
            _ => new TurboCharger(TurboCharger),
        };
    }

    /// <summary>Returns a deep copy of this profile.</summary>
    public LocoProfile Clone()
    {
        return new LocoProfile
        {
            LiveryId = LiveryId,
            Enabled = Enabled,
            ChargerKind = ChargerKind,
            Exhausts = Exhausts?.Select(e => e.Clone()).ToList() ?? new List<LocoExhaust>(),
            TurboCharger = TurboCharger != null ? new TurboCharger.Settings(TurboCharger) : null,
            Atmospheric = Atmospheric != null ? new AtmosphericCharger.Settings(Atmospheric) : null,
            Roots = Roots != null ? new RootsCharger.Settings(Roots) : null,
            Smoke =Smoke != null ? new ExhaustSmokeModel.Settings(Smoke) : null,
            SmokeEmitter = SmokeEmitter != null ? new SmokeParticles.Settings(SmokeEmitter) : null,
            ShimmerEmitter = ShimmerEmitter != null ? new ShimmerParticles.Settings(ShimmerEmitter) : null,
            Velocity = Velocity != null ? new ExhaustVelocitySettings(Velocity) : null,
            Combustion = Combustion != null ? new CombustionModel.Settings(Combustion) : null,
            Stack = Stack != null ? new StackModel.Settings(Stack) : null,
        };
    }

    /// <summary>Structural validation. Returns an error, or null when the profile is usable.</summary>
    internal Error? Validate()
    {
        if (string.IsNullOrWhiteSpace(LiveryId)) return new Error("LiveryId is required");
        if (Exhausts == null || Exhausts.Count == 0)
            return new Error("at least one exhaust is required");
        if (!Enum.IsDefined(typeof(ChargerKind), ChargerKind))
            return new Error($"unknown charger kind {(int)ChargerKind}");

        foreach (var exhaust in Exhausts)
        {
            var error = ValidateExhaust(exhaust);
            if (error != null) return error;
        }

        return null;
    }

    private static Error? ValidateExhaust(LocoExhaust exhaust)
    {
        if (exhaust == null) return new Error("exhaust entry is null");
        if (!Enum.IsDefined(typeof(ExhaustKind), exhaust.Kind))
            return new Error($"unknown exhaust kind {(int)exhaust.Kind}");
        if (!IsFinite(exhaust.Offset)) return new Error("exhaust offset must be finite");
        if (exhaust.Kind == ExhaustKind.Replacement && string.IsNullOrWhiteSpace(exhaust.Path))
            return new Error("replacement exhausts need a particle system path");

        return null;
    }

    private static bool IsFinite(Vector3 v) =>
        !float.IsNaN(v.x) && !float.IsInfinity(v.x)
        && !float.IsNaN(v.y) && !float.IsInfinity(v.y)
        && !float.IsNaN(v.z) && !float.IsInfinity(v.z);

    /// <summary>
    /// Validates and normalizes this profile: any uninitialized settings blocks are
    /// initialized to their default values. Settings for an unused charger kind are dropped.
    /// Returns an error if validation failed. Note that this is a mutating operation.
    /// </summary>
    internal Error? Normalize()
    {
        var error = Validate();
        if (error != null) return error;

        Smoke ??= new ExhaustSmokeModel.Settings();
        Smoke.Validate();

        SmokeEmitter ??= new SmokeParticles.Settings();

        ShimmerEmitter ??= new ShimmerParticles.Settings();

        Velocity ??= new ExhaustVelocitySettings();
        Velocity.Validate();

        Combustion ??= new CombustionModel.Settings();
        Combustion.Validate();

        Stack ??= new StackModel.Settings();
        Stack.Validate();

        if (ChargerKind != ChargerKind.Turbo) TurboCharger = null;
        if (ChargerKind != ChargerKind.Atmospheric) Atmospheric = null;
        if (ChargerKind != ChargerKind.Roots) Roots = null;

        switch (ChargerKind)
        {
            case ChargerKind.Atmospheric:
                Atmospheric ??= new AtmosphericCharger.Settings();
                Atmospheric.Validate();
                break;
            case ChargerKind.Roots:
                Roots ??= new RootsCharger.Settings();
                Roots.Validate();
                break;
            default:
                TurboCharger ??= new TurboCharger.Settings();
                TurboCharger.Validate();
                break;
        }

        return null;
    }
}

/// <summary>One exhaust entry in a <see cref="LocoProfile"/>.</summary>
public sealed class LocoExhaust
{
    public ExhaustKind Kind { get; set; } = ExhaustKind.Replacement;

    /// <summary>
    /// Car-relative transform path of the particle system to replace. Leave unset
    /// for independent exhausts.
    /// </summary>
    public string Path { get; set; } = "";

    public Vector3 Offset { get; set; }

    public static LocoExhaust Replacement(string path, Vector3 offset = default) =>
        new() { Kind = ExhaustKind.Replacement, Path = path, Offset = offset };

    public static LocoExhaust Independent(Vector3 offset = default) =>
        new() { Kind = ExhaustKind.Independent, Offset = offset };

    public LocoExhaust Clone() => new() { Kind = Kind, Path = Path, Offset = Offset };
}

public enum ExhaustKind
{
    Replacement,
    Independent,
}
