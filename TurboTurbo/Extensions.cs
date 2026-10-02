using System;
using System.Collections.Generic;

namespace TurboTurbo;

public static class Extensions
{
    /// <summary>
    /// Generates an identifier for <paramref name="car"/>, for use in logging.
    /// </summary>
    public static string LogIdentifier(this TrainCar car)
    {
        if (car == null) return "?/?";

        var liveryId = string.IsNullOrWhiteSpace(car.carLivery?.id) ? "?" : car.carLivery.id;
        return $"{liveryId}/{car.DisplayId()}";
    }

    /// <summary>
    /// The car's ID, or "?" when its logic car is gone. Use this when displaying the ID of a car,
    /// as direct access may result in an error message being logged if logicCar is not set.
    /// </summary>
    public static string DisplayId(this TrainCar car)
    {
        if (car == null || car.logicCar == null) return "?";

        return string.IsNullOrWhiteSpace(car.ID) ? "?" : car.ID;
    }
    
    public static T? FirstOrNull<T>(this IEnumerable<T> source, Func<T, bool> predicate) where T : struct
    {
        foreach (var item in source)
        {
            if (predicate(item)) return item;
        }
        return null;
    }
}
