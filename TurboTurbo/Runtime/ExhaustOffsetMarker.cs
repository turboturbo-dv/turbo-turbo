using UnityEngine;
using UnityEngine.Rendering;

namespace TurboTurbo.Runtime;

/// <summary>
/// World-space axis cross marking one exhaust position.
/// </summary>
internal sealed class ExhaustOffsetMarker : MonoBehaviour
{
    private const float HalfLength = 0.25f;
    private const float LineWidth = 0.01f;

    private static readonly Vector3[] Axes = { Vector3.right, Vector3.up, Vector3.forward };
    private static readonly string[] AxisNames = { "X", "Y", "Z" };
    private static readonly Color[] AxisColors = { Color.red, Color.green, Color.blue };

    private static Material[] _materials;

    private void Awake()
    {
        EnsureMaterials();
        for (var a = 0; a < 3; a++)
        {
            var axis = new GameObject(AxisNames[a]);
            axis.transform.SetParent(transform, worldPositionStays: false);

            var line = axis.AddComponent<LineRenderer>();
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
    }

    public void Attach(Transform parent)
    {
        transform.SetParent(parent, worldPositionStays: false);
        transform.localRotation = Quaternion.identity;
    }

    public void SetLocalPosition(Vector3 local) => transform.localPosition = local;

    public void SetVisible(bool visible) => gameObject.SetActive(visible);

    private static void EnsureMaterials()
    {
        if (_materials != null) return;

        var shader = Shader.Find("Unlit/Color");
        _materials = new Material[3];
        for (var a = 0; a < 3; a++)
        {
            _materials[a] = new Material(shader)
            {
                color = AxisColors[a],
                name = Naming.Create($"ExhaustMarkerMat[{a}]"),
                hideFlags = HideFlags.HideAndDontSave,
            };
        }
    }
}