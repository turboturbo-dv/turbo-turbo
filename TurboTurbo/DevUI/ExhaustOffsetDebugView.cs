using System.Collections.Generic;

using TurboTurbo.Runtime;

using UnityEngine;
using UnityEngine.Rendering;

namespace TurboTurbo.DevUI;

internal sealed class ExhaustOffsetDebugView : MonoBehaviour
{
    private const float HalfLength = 0.25f;
    private const float LineWidth = 0.01f;
    private const string MarkerPrefix = "TurboTurbo.OffsetMarker";

    private static readonly Vector3[] Axes = { Vector3.right, Vector3.up, Vector3.forward };
    private static readonly Color[] AxisColors = { Color.red, Color.green, Color.blue };
    private static readonly string[] AxisNames = { "X", "Y", "Z" };

    private readonly Logger _log = Log.ForContext("offsetmarkers");

    private EngineSimulationHost _host;
    private bool _visible;
    private readonly List<GameObject> _markers = new();
    private readonly List<Material> _materials = new();

    public void SetHost(EngineSimulationHost host)
    {
        if (ReferenceEquals(_host, host)) return;
        ClearMarkers();
        _host = host;
        if (_visible) Rebuild();
    }

    public void SetVisible(bool visible)
    {
        if (visible == _visible) return;
        _visible = visible;
        if (visible) Rebuild();
        else ClearMarkers();
        _log.Info(visible ? "markers shown" : "markers hidden");
    }

    private void Update()
    {
        if (_host == null || _host.TrainCar == null)
        {
            ClearMarkers();
            return;
        }

        if (!_visible) return;

        for (var i = _markers.Count - 1; i >= 0; i--)
        {
            if (_markers[i] == null) _markers.RemoveAt(i);
        }

        if (_markers.Count != _host.Exhausts.Count)
        {
            Rebuild();
            return;
        }

        for (var i = 0; i < _markers.Count; i++)
        {
            _markers[i].transform.localPosition = _host.Exhausts[i].Mouth + _host.Exhausts[i].Offset;
        }
    }

    private void OnDestroy()
    {
        ClearMarkers();
        foreach (var material in _materials)
        {
            Destroy(material);
        }

        _materials.Clear();
    }

    private void Rebuild()
    {
        ClearOrphans();
        if (_host == null || _host.TrainCar == null) return;

        EnsureMaterials();
        var parent = _host.TrainCar.transform;
        for (var i = _markers.Count - 1; i >= _host.Exhausts.Count; i--)
        {
            Destroy(_markers[i]);
            _markers.RemoveAt(i);
        }

        while (_markers.Count < _host.Exhausts.Count)
        {
            _markers.Add(CreateMarker(parent, _markers.Count));
        }

        for (var i = 0; i < _markers.Count; i++)
        {
            _markers[i].transform.localPosition = _host.Exhausts[i].Mouth + _host.Exhausts[i].Offset;
        }
    }

    private GameObject CreateMarker(Transform parent, int index)
    {
        var existing = parent.Find($"{MarkerPrefix}[{index}]");
        var root = existing != null ? existing.gameObject : new GameObject($"{MarkerPrefix}[{index}]");
        // identity local rotation keeps the axes car-local, matching the offset sliders
        root.transform.SetParent(parent, worldPositionStays: false);
        root.transform.localRotation = Quaternion.identity;
        for (var a = 0; a < 3; a++)
        {
            var axis = root.transform.Find(AxisNames[a]);
            GameObject axisObject;
            if (axis != null)
            {
                axisObject = axis.gameObject;
            }
            else
            {
                axisObject = new GameObject(AxisNames[a]);
                axisObject.transform.SetParent(root.transform, worldPositionStays: false);
            }

            var line = axisObject.GetComponent<LineRenderer>();
            if (line == null) line = axisObject.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.positionCount = 2;
            line.SetPosition(0, -Axes[a] * HalfLength);
            line.SetPosition(1, Axes[a] * HalfLength);
            line.startWidth = LineWidth;
            line.endWidth = LineWidth;
            line.sharedMaterial = _materials[a];
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
        }

        return root;
    }

    private void ClearOrphans()
    {
        if (_host == null || _host.TrainCar == null) return;
        var parent = _host.TrainCar.transform;
        foreach (Transform child in parent)
        {
            if (!child.name.StartsWith(MarkerPrefix)) continue;
            if (_markers.Contains(child.gameObject)) continue;
            Destroy(child.gameObject);
        }
    }

    private void ClearMarkers()
    {
        ClearOrphans();
        foreach (var marker in _markers)
        {
            if (marker != null) Destroy(marker);
        }

        _markers.Clear();
    }
    private void EnsureMaterials()
    {
        if (_materials.Count == 3) return;
        var shader = Shader.Find("Unlit/Color");
        for (var a = 0; a < 3; a++)
        {
            _materials.Add(new Material(shader) { color = AxisColors[a], name = $"{MarkerPrefix}Mat[{a}]" });
        }
    }
}