using System;
using System.Collections.Generic;
using DV.ThingTypes;
using UnityEngine;

namespace TurboTurbo.Setup;

public class EngineOptions()
{
    private bool _hasTurbo = false;
    private readonly List<Func<TrainCar, Transform>> _exhaustTransforms = new();
    private readonly List<Func<TrainCar, Transform>> _tractionVentTransforms = new();
    private readonly List<Func<TrainCar, Transform>> _dynamicBrakeVentTransforms = new();

    public EngineOptions AddTurbo()
    {
        _hasTurbo = true;
        return this;
    }

    public EngineOptions AddEngineExhaust(Func<TrainCar, Transform> transformSelector)
    {
        _exhaustTransforms.Add(transformSelector);
        return this;
    }

    public EngineOptions AddTractionMotorVent(Func<TrainCar, Transform> transformSelector)
    {
        _tractionVentTransforms.Add(transformSelector);
        return this;
    }

    public EngineOptions AddDynamicBrakeVent(Func<TrainCar, Transform> transformSelector)
    {
        _dynamicBrakeVentTransforms.Add(transformSelector);
        return this;
    }

    internal EngineConfiguration Build()
    {
        return new EngineConfiguration(_hasTurbo, _exhaustTransforms);
    }
}