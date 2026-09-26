using System;
using System.Collections.Generic;

using UnityEngine;

namespace TurboTurbo.Runtime;

/// <summary>
/// Discovers the exhaust particle systems on a car and picks a default one to
/// </summary>
internal static class ExhaustTargets
{
    public record struct Candidate(ParticleSystem Ps, string Name, string Path);

    /// <summary>
    /// Heuristic to pick a default replacement target by name, names matching
    /// 'ExhaustEngineSmoke' (DV's default name) in some way are preferred.
    /// Returns -1 when there are no candidates.
    /// </summary>
    public static int PickDefault(IReadOnlyList<string> names)
    {
        if (names == null || names.Count == 0) return -1;

        for (var i = 0; i < names.Count; i++)
        {
            if (Contains(names[i], "ExhaustEngineSmoke")) return i;
        }

        for (var i = 0; i < names.Count; i++)
        {
            if (Contains(names[i], "Exhaust")) return i;
        }

        return 0;
    }

    /// <summary>
    /// Root particle systems on the car, excluding the mod's own, in hierarchy order.
    /// </summary>
    public static IReadOnlyList<Candidate> FindCandidates(TrainCar car)
    {
        var candidates = new List<Candidate>();
        if (car == null) return candidates;

        var carTransform = car.transform;
        foreach (var ps in car.GetComponentsInChildren<ParticleSystem>(true))
        {
            var parent = ps.transform.parent;
            if (parent != null && parent.GetComponent<ParticleSystem>() != null) continue;
            if (Naming.IsOurs(ps.transform.name)) continue;

            candidates.Add(new Candidate(ps, ps.transform.name, PathOf(carTransform, ps.transform)));
        }

        return candidates;
    }

    /// <summary>
    /// Picks a default replacement target from the car's candidates, returning its
    /// car-relative path, or null when there are none.
    /// </summary>
    public static string TryDefaultPath(TrainCar car)
    {
        var candidates = FindCandidates(car);
        var names = new List<string>(candidates.Count);
        foreach (var candidate in candidates) names.Add(candidate.Name);

        var pick = PickDefault(names);
        return pick >= 0 ? candidates[pick].Path : null;
    }

    /// <summary>
    /// Resolves a stored target string to the particle system it names, or null.
    /// A car-relative path matches exactly, disambiguating duplicate names; a bare
    /// particle-system name is accepted as a fallback for hand-authored configs.
    /// </summary>
    public static ParticleSystem Resolve(TrainCar car, string path)
    {
        if (string.IsNullOrEmpty(path)) return null;

        var candidates = FindCandidates(car);

        foreach (var candidate in candidates)
        {
            if (candidate.Path == path) return candidate.Ps;
        }

        foreach (var candidate in candidates)
        {
            if (candidate.Name == path) return candidate.Ps;
        }

        return null;
    }

    private static bool Contains(string name, string token) =>
        name != null && name.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0;

    private static string PathOf(Transform root, Transform target)
    {
        var parts = new List<string>();
        for (var current = target; current != null && current != root; current = current.parent)
        {
            parts.Add(current.name);
        }

        parts.Reverse();
        return string.Join("/", parts);
    }
}