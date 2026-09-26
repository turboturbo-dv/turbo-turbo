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