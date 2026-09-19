using System;
using System.Linq;

using UnityEngine;

namespace TurboTurbo;

public static class Extensions
{
    // hyper-specific extension methods is my middle name
    public static T GetFirstComponentInChildren<T>(this Component component, bool includeInactive = false, Func<T, bool> predicate = null) where T : Component
    {
        return component.GetComponentsInChildren<T>(includeInactive).FirstOrDefault(c => predicate == null || predicate(c));
    }

    public static string LogIdentifier(this TrainCar car)
    {
        if (car == null) return "?/?";

        var liveryId = string.IsNullOrWhiteSpace(car.carLivery?.id) ? "?" : car.carLivery.id;
        var carId = string.IsNullOrWhiteSpace(car.ID) ? "?" : car.ID;
        return $"{liveryId}/{carId}";
    }
}