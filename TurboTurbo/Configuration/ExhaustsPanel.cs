using System;
using System.Collections.Generic;
using System.Globalization;

using TurboTurbo.Profiles;
using TurboTurbo.Runtime;

using UnityEngine;

namespace TurboTurbo.Configuration;

/// <summary>
/// The profile editor's <c>exhausts</c> panel. Not a <see cref="Sections.Section"/>:
/// exhaust blocks are a dynamic list with add/remove, a kind toggle, a dropdown and
/// per-block collapsible state, none of which fit the <c>ITweakSpec</c> pattern.
/// </summary>
internal sealed class ExhaustsPanel : IEditorPanel
{
    private static readonly float[] Steps = { 0.01f, 0.1f, 1f };

    private static readonly string[] AxisNames = { "X", "Y", "Z" };

    private readonly Func<EngineSimulationHost> _host;
    private readonly Action _onStructuralChange;
    private readonly Action _onNeedsShrink;
    private readonly Action<int> _setMarkerTarget;

    private bool _open;
    private EngineSimulationHost _boundHost;
    private LocoProfile _profile;
    private TrainCar _candidateSource;
    private IReadOnlyList<ExhaustTargets.Candidate> _candidates = Array.Empty<ExhaustTargets.Candidate>();

    private int _openPosition = -1;
    private int _openDropdown = -1;
    private int _markedTarget = -1;
    private readonly string[] _editText = new string[3];

    public bool Open
    {
        get => _open;
        set => _open = value;
    }

    public ExhaustsPanel(
        Func<EngineSimulationHost> host,
        Action onStructuralChange,
        Action onNeedsShrink,
        Action<int> setMarkerTarget)
    {
        _host = host;
        _onStructuralChange = onStructuralChange;
        _onNeedsShrink = onNeedsShrink;
        _setMarkerTarget = setMarkerTarget;
    }

    public void Draw()
    {
        var host = _host();
        if (host == null || host.Profile == null) return;

        Rebuild(host);
        EnsureCandidates(host);

        var profile = host.Profile;
        if (!ReferenceEquals(profile, _profile))
        {
            _profile = profile;
            ClearUiState();
        }

        var entries = BuildEntries(host, profile);
        SyncMarker(host, entries);

        DrawHeader(profile, host.TrainCar);
        if (!_open) return;

        for (var i = 0; i < entries.Count; i++)
        {
            DrawBlock(host, entries, i);
        }
    }

    private void Rebuild(EngineSimulationHost host)
    {
        if (ReferenceEquals(host, _boundHost)) return;
        _boundHost = host;
        _candidateSource = null;
        _candidates = Array.Empty<ExhaustTargets.Candidate>();
        ClearUiState();
    }

    private void EnsureCandidates(EngineSimulationHost host)
    {
        if (ReferenceEquals(host.TrainCar, _candidateSource)) return;

        _candidateSource = host.TrainCar;
        _candidates = host.TrainCar != null
            ? ExhaustTargets.FindCandidates(host.TrainCar)
            : Array.Empty<ExhaustTargets.Candidate>();
    }

    private void DrawHeader(LocoProfile profile, TrainCar car)
    {
        GUILayout.BeginHorizontal();
        if (GUILayout.Button((_open ? "▾ " : "▸ ") + "exhausts", Styles.SectionHeader)) _open = !_open;
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("add", GUILayout.Width(60f))) Add(profile, car);
        GUILayout.EndHorizontal();
    }

    private void DrawBlock(EngineSimulationHost host, List<Entry> entries, int index)
    {
        var entry = entries[index];
        var bound = host.EffectsBound;

        GUILayout.BeginVertical(Styles.TelemetryBox);
        GUILayout.BeginHorizontal();
        GUILayout.Label($"exhaust {index}");
        GUILayout.FlexibleSpace();
        GUI.enabled = entries.Count > 1;
        if (GUILayout.Button("delete", GUILayout.Width(60f))) Delete(index);
        GUI.enabled = true;
        GUILayout.EndHorizontal();

        var replace = GUILayout.Toggle(entry.Scope.Kind == ExhaustKind.Replacement, "replace existing");
        if (replace != (entry.Scope.Kind == ExhaustKind.Replacement)) SetKind(index, replace);

        if (replace)
        {
            DrawDropdown(index, entry.Scope.Name);
        }
        else
        {
            GUILayout.Label("adds a new exhaust emitter at the car origin.", Styles.WrappedLabel);
        }

        var position = _openPosition == index;
        GUI.enabled = bound;
        if (GUILayout.Button((position ? "▾ " : "▸ ") + "position", Styles.SectionHeader)) TogglePosition(index);
        GUI.enabled = true;

        if (position) DrawPosition(entry, bound);

        GUILayout.EndVertical();
        GUILayout.Space(2f);
    }

    private void DrawDropdown(int index, string current)
    {
        if (GUILayout.Button(NameLabel(current) + "  ▾"))
        {
            _openDropdown = _openDropdown == index ? -1 : index;
        }

        if (_openDropdown != index) return;

        if (_candidates.Count == 0)
        {
            GUILayout.Label("no particle systems found");
            return;
        }

        foreach (var candidate in _candidates)
        {
            var marker = candidate.Name == current ? "* " : "  ";
            if (GUILayout.Button(marker + candidate.Path)) SetName(index, candidate.Name);
        }
    }

    private void DrawPosition(Entry entry, bool bound)
    {
        for (var axis = 0; axis < 3; axis++)
        {
            DrawOffsetRow(entry, axis, AxisNames[axis], bound);
        }
    }

    private void DrawOffsetRow(Entry entry, int axis, string label, bool bound)
    {
        GUI.enabled = bound;
        var value = entry.GetAxis(axis);

        GUILayout.BeginHorizontal();
        GUILayout.Label(label, GUILayout.Width(20f));
        for (var s = Steps.Length - 1; s >= 0; s--) DrawNudge(entry, axis, -Steps[s]);

        var shown = _editText[axis] ?? value.ToString("0.###", CultureInfo.InvariantCulture);
        var typed = GUILayout.TextField(shown);
        if (typed != shown)
        {
            _editText[axis] = typed;
            if (float.TryParse(typed, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
            {
                entry.SetAxis(axis, parsed);
            }
        }

        for (var s = 0; s < Steps.Length; s++) DrawNudge(entry, axis, Steps[s]);
        GUILayout.EndHorizontal();

        GUI.enabled = true;
    }

    private void DrawNudge(Entry entry, int axis, float delta)
    {
        var label = $"{(delta < 0f ? "-" : "+")}{Mathf.Abs(delta):0.##}";
        if (!GUILayout.Button(label, GUILayout.Width(48f))) return;

        entry.SetAxis(axis, entry.GetAxis(axis) + delta);
        _editText[axis] = null;
    }

    private void Add(LocoProfile profile, TrainCar car)
    {
        var name = car != null ? ExhaustTargets.TryDefault(car) : null;
        profile.Exhausts.Add(name != null ? LocoExhaust.Replacement(name) : LocoExhaust.Independent());
        _onStructuralChange();
    }

    private void Delete(int index)
    {
        _profile.Exhausts.RemoveAt(index);
        ClearUiState();
        _onStructuralChange();
    }

    private void SetKind(int index, bool replace)
    {
        var exhaust = _profile.Exhausts[index];
        if (replace)
        {
            var name = exhaust.Name;
            if (string.IsNullOrEmpty(name)) name = _boundHost?.TrainCar != null ? ExhaustTargets.TryDefault(_boundHost.TrainCar) : null;

            if (string.IsNullOrEmpty(name))
            {
                // no particle system to replace: keep it independent
                return;
            }

            exhaust.Kind = ExhaustKind.Replacement;
            exhaust.Name = name;
        }
        else
        {
            exhaust.Kind = ExhaustKind.Independent;
            exhaust.Name = "";
        }

        ClearUiState();
        _onStructuralChange();
    }

    private void SetName(int index, string name)
    {
        var exhaust = _profile.Exhausts[index];
        exhaust.Kind = ExhaustKind.Replacement;
        exhaust.Name = name;

        _openDropdown = -1;
        _onStructuralChange();
    }

    private void TogglePosition(int index)
    {
        _openPosition = _openPosition == index ? -1 : index;
        _openDropdown = -1;
        Array.Clear(_editText, 0, _editText.Length);
        _onNeedsShrink();
    }

    private List<Entry> BuildEntries(EngineSimulationHost host, LocoProfile profile)
    {
        var entries = new List<Entry>(profile.Exhausts.Count);
        for (var i = 0; i < profile.Exhausts.Count; i++)
        {
            entries.Add(new Entry
            {
                Scope = profile.Exhausts[i],
                Emitters = i < host.Exhausts.Count ? host.Exhausts[i] : null,
            });
        }

        return entries;
    }

    private void SyncMarker(EngineSimulationHost host, List<Entry> entries)
    {
        var index = -1;
        if (_open
            && host.EffectsBound
            && _openPosition >= 0
            && _openPosition < entries.Count
            && entries[_openPosition].Emitters != null)
        {
            index = _openPosition;
        }

        if (index == _markedTarget) return;
        _markedTarget = index;
        _setMarkerTarget(index);
    }

    private string NameLabel(string name)
    {
        if (string.IsNullOrEmpty(name)) return "(choose a particle system)";

        foreach (var candidate in _candidates)
        {
            if (candidate.Name == name) return candidate.Path;
        }

        return $"{name} (missing)";
    }

    private void ClearUiState()
    {
        _openPosition = -1;
        _openDropdown = -1;
        Array.Clear(_editText, 0, _editText.Length);
    }

    private sealed class Entry
    {
        public LocoExhaust Scope;
        public EngineSimulationHost.ExhaustEmitters Emitters;

        public float GetAxis(int axis) => axis switch
        {
            0 => Scope.Offset.x,
            1 => Scope.Offset.y,
            _ => Scope.Offset.z,
        };

        public void SetAxis(int axis, float value)
        {
            var offset = Scope.Offset;
            switch (axis)
            {
                case 0: offset.x = value; break;
                case 1: offset.y = value; break;
                default: offset.z = value; break;
            }

            Scope.Offset = offset;

            if (Emitters == null) return;
            Emitters.Offset = offset;
            Emitters.Reposition();
        }
    }
}