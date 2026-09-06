using System;
using System.Collections.Generic;

using UnityEngine;

namespace TurboTurbo.Setup;

public class EngineOptions
{
    private bool _hasTurbo = false;
    private readonly List<ExhaustBinding> _exhausts = new();

    public EngineOptions AddTurbo()
    {
        _hasTurbo = true;
        return this;
    }

    /// <summary>Adds a new engine exhaust at the given position.</summary>
    public EngineOptions AddEngineExhaust(Func<TrainCar, Transform> transformSelector)
    {
        _exhausts.Add(new ExhaustBinding(transformSelector, null, Vector3.zero));
        return this;
    }

    /// <summary>
    /// Takes over an existing exhaust ParticleSystem: its emission is disabled so that it is effectively replaced.
    /// The optional offset, in car-local space, shifts the new emitters relative to the transform
    /// of the existing particle system.
    /// </summary>
    public EngineOptions ReplaceEngineExhaust(Func<TrainCar, ParticleSystem> psSelector, Vector3? offset = null)
    {
        _exhausts.Add(new ExhaustBinding(null, psSelector, offset ?? Vector3.zero));
        return this;
    }

    internal EngineConfiguration Build()
    {
        return new EngineConfiguration(_hasTurbo, _exhausts);
    }
}