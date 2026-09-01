using System;
using System.Collections.Generic;
using System.Linq;
using DV.Simulation.Cars;
using HarmonyLib;
using LocoSim.Definitions;
using LocoSim.Implementations;
using TurboTurbo.Modeling;
using TurboTurbo.Setup;
using UnityEngine;

namespace TurboTurbo.Runtime;

/// <summary>
/// Per-car runtime host: attached to the car's root GameObject by the
/// Orchestrator when a matching EngineConfiguration exists, living alongside
/// DV's own per-car runtime components (SimController, CarDamageModel, ...).
/// Waits for the car's simulation to initialize, then binds the configured
/// features to it (turbo model first; exhaust spawning later).
///
/// Survives car pool cycles deactivated and resumes on revival; the sim
/// bindings stay valid because SimController/SimulationFlow are never
/// rebuilt for pooled cars.
/// </summary>
internal sealed class EngineSimulationHost : MonoBehaviour
{
    private EngineConfiguration _configuration;
    private Logger _log;

    private SimController _simController;
    private bool _simBound;
    private bool _loggedNoSim;

    private TurboModel _turboModel;
    private Port _throttlePort;
    private Func<float> _fuelNorm;
    private Func<bool> _engineOn;

    public EngineSimulationHost Configure(EngineConfiguration configuration, Logger log)
    {
        _configuration = configuration;
        _log = log;
        return this;
    }

    private void Update()
    {
        if (!_simBound)
        {
            TryBindSimulation();
            return;
        }

        if (_turboModel == null) return;

        _turboModel.Tick(Time.deltaTime, _fuelNorm(), _engineOn());

        // write the torque-capped demand back to the engine's throttle port;
        // the upstream throttle chain re-propagates the player's lever before
        // we read again, so this only caps the engine, it never sticks
        _throttlePort.Value = _turboModel.EffectiveDemand;
    }

    private void BindTurbo()
    {
        var flow = _simController.SimulationFlow;
        var engine = flow.OrderedSimComps.OfType<DieselEngineDirect>().FirstOrDefault();
        if (engine == null)
        {
            _log.LogWarning($"[host] no DieselEngineDirect on '{name}' - turbo not bound");
            return;
        }

        // THROTTLE is a PortReference - the actual Port hangs off a private field
        PortReference throttleRef = engine.GetAllPortReferences()
            .FirstOrDefault(r => r.id.EndsWith(".THROTTLE", StringComparison.OrdinalIgnoreCase));
        _throttlePort = throttleRef != null
            ? Traverse.Create(throttleRef).Field("port").GetValue<Port>()
            : null;

        Port rpmPort = engine.GetAllPorts()
            .FirstOrDefault(p => p.id.EndsWith(".RPM_NORMALIZED", StringComparison.OrdinalIgnoreCase));
        Port fuelPort = engine.GetAllPorts()
            .FirstOrDefault(p => p.id.EndsWith(".FUEL_CONSUMPTION_NORMALIZED", StringComparison.OrdinalIgnoreCase));
        Port engineOnPort = engine.GetAllPorts()
            .FirstOrDefault(p => p.id.EndsWith(".ENGINE_ON", StringComparison.OrdinalIgnoreCase));

        if (_throttlePort == null || rpmPort == null)
        {
            _log.LogWarning($"[host] could not resolve throttle/rpm ports on '{name}' - turbo not bound");
            return;
        }

        // suppliers: the model reads the ports through these closures
        _fuelNorm = fuelPort != null
            ? () => Mathf.Clamp01(fuelPort.Value)
            : () => 0f;
        _engineOn = engineOnPort != null
            ? () => engineOnPort.Value > 0.5f
            : () => rpmPort.Value > 0.05f;

        _turboModel = new TurboModel(new TurboModel.Settings(),
            () => _throttlePort.Value,
            () => rpmPort.Value);

        _log.LogInfo($"[host] turbo bound on '{name}' (throttle: {_throttlePort.id}, " +
                     $"fuel: {(fuelPort != null ? fuelPort.id : "MISSING")})");
    }

    /// <summary>
    /// The car's SimulationFlow only exists after SimController.Initialize has
    /// run - retry until it does, then bind the configured features.
    /// </summary>
    private void TryBindSimulation()
    {
        // the host lives on the car root, so plain GetComponent finds the
        // car's SimController (same lookup TrainCar.SimController does)
        _simController = GetComponent<SimController>();
        if (_simController == null || _simController.SimulationFlow == null)
        {
            if (!_loggedNoSim)
            {
                _loggedNoSim = true;
                _log.LogInfo($"[host] no SimController (yet) on '{name}'");
            }
            return;
        }

        _simBound = true;
        _log.LogInfo($"[host] sim bound on '{name}' ({_configuration.HasTurbo} turbo, " +
                     $"{_configuration.ExhaustPositionSelectors.Count} exhaust selector(s))");

        if (_configuration.HasTurbo)
        {
            BindTurbo();
        }
    }
}
