using System;
using System.Linq;

using UnityEngine;

namespace TurboTurbo;

public static class UnityExtensions
{
    // hyper-specific extension methods is my middle name
    public static T GetFirstComponentInChildren<T>(this Component component, bool includeInactive = false, Func<T, bool> predicate = null) where T : Component
    {
        return component.GetComponentsInChildren<T>(includeInactive).FirstOrDefault(c => predicate == null || predicate(c));
    }
}