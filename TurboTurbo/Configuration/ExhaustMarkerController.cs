using System;

using TurboTurbo.Runtime;

using UnityEngine;

namespace TurboTurbo.Configuration;

/// <summary>
/// Owns a single exhaust position marker for the profile editor: creates it for
/// the targeted exhaust, follows it each frame, and clears it on teardown.
/// </summary>
internal sealed class ExhaustMarkerController
{
    private readonly Func<EngineSimulationHost> _host;

    private ExhaustOffsetMarker _marker;
    private int _index = -1;

    public ExhaustMarkerController(Func<EngineSimulationHost> host)
    {
        _host = host;
    }

    /// <summary>
    /// Points the marker at <paramref name="index"/> in the host's exhausts, creating
    /// it if needed. Pass a negative index to clear.
    /// </summary>
    public void SetTarget(int index)
    {
        Clear();

        var host = _host();
        if (host == null || !host.EffectsBound) return;
        if (index < 0 || index >= host.Exhausts.Count) return;

        var go = new GameObject(Naming.Create("ExhaustMarker"));
        _marker = go.AddComponent<ExhaustOffsetMarker>();
        _marker.Attach(host.TrainCar.transform);
        _marker.SetLocalPosition(host.Exhausts[index].LocalPosition);
        _index = index;
    }

    /// <summary>Moves the marker to the current position of its target.</summary>
    public void Sync()
    {
        if (_marker == null) return;

        var host = _host();
        if (host == null || _index < 0 || _index >= host.Exhausts.Count) return;

        _marker.SetLocalPosition(host.Exhausts[_index].LocalPosition);
    }

    /// <summary>Destroys the marker, if any.</summary>
    public void Clear()
    {
        if (_marker != null)
        {
            UnityEngine.Object.Destroy(_marker.gameObject);
            _marker = null;
        }

        _index = -1;
    }
}