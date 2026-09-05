# TurboTurbo TODO

- **Enable toggle on turbo section (dev panel)**
  Add a toggle to enable/disable the turbo model in the dev panel,
  so we can test the engine without it.

- **Cleanup pass**
  Do a full pass over the code base to eliminate defensive guards and other
  needlessly defensive code. If we cannot envision a justification for a guard,
  it should be removed, as it just adds unnecessary noise and complexity.

- **Particle emitter architecture refactor**
  Currently, both the smoke and shimmer emitters are independent, but they share
  a lot of common logic. It may be worth refactoring them into a common base
  class, but we should also consider that the shimmer emitter will in the future
  also be used for effects like the dynamic brake vents, so it should not be
  tied to the idea that it always pairs up with a smoke emitter.

- **Investigate transform logic**
  It can probably be simplified, and we may need to create our own copy rather
  than relying on the transform taken from the configuration.

- **Make dev panel state tracking resilient**
  If there are no tracked locos, and one appears, the panel tracks it only
  halfway: the telemetry appears, but the specs do not. We should investigate
  this.

- **Distinguish between adding and replacing exhausts in public API**
  The replace method should resolve a ParticleSystem, which represents the
  exhaust to be replaced, as well as an optional offset in case we want to
  adjust the new exhaust's position slightly. The add method should just
  resolve a transform.

- **Dev panel: time-series chart for telemetry values (e.g. lambda
  through throttle maneuvers)**
  Design is settled, implementation deferred. Sketch:
  - *Renderer:* rect-strip lines — one tinted `GUI.DrawTexture` quad per
    sample pair (`Texture2D.whiteTexture`), drawn into a
    `GUILayoutUtility.GetRect` area. Clips correctly in the window, no GL
    or texture lifecycle; ~600-900 quads/repaint is fine. Texture2D
    plotting is the upgrade path if this ever shows cost.
  - *Sampling:* fixed-rate (20 Hz) ring buffers, decoupled from frame
    rate; 30 s window (600 floats/channel). Tick from the panel's Update,
    gated on `_visible`; buffers cleared on car rebind (same trigger as
    BuildSections). Per-frame sampling would make the x-axis fps-dependent
    and miss spikes at low fps.
  - *Channels:* `ChartChannel` { name, color, Func<float> getter closed
    over the bound host (like spec builders), enabled flag, ring buffer }.
    View state only — deliberately OUTSIDE the spec framework (no
    reset/YAML dump semantics).
  - *Scale:* auto-range across enabled channels with 10% headroom + min/max
    labels; known failure mode (one spike flattens the rest) → possible
    "freeze range" toggle later.
  - *Lambda bonus:* horizontal reference lines at
    `ExhaustSmokeModel.SootOnsetLambda`/`SootOpaqueLambda` when the lambda
    channel is enabled — shows exactly when a maneuver crosses the soot
    thresholds; auto-updates with panel retuning.
  - *Deferred:* adjustable window/rate, per-channel normalization, stacked
    charts, texture renderer.

## Spikes (investigate, don't commit yet)

- **Spike: slight gaussian blur in the shimmer.**
  Add a small gaussian blur to the shimmer to model smaller-scale shimmering
  that isn't really recognizable on its own and just serves to slightly
  increase the apparent "density" of the hot air (softens the background a
  touch inside the mask).

- **Spike: displaced samples landing on foreground silhouettes.**
  With the depth fix, background pixels adjacent to foreground objects are
  correctly left undisplaced *at their own position*, but their computed
  offset can still resolve to a foreground pixel in the grab, smearing
  foreground texture into the background. Candidate approaches: validate the
  offset target with a second depth compare at (base UV + offset) and
  reject/shrink offsets that cross a depth discontinuity; or dilate/
  edge-extend the foreground depth so protected regions are wider than the
  geometry itself.

- **Spike: integrate the turbo whine synth into the mod.**
  Port the whine from WhineBench (`shared/WhineSynthGemini.cs`) into the
  runtime, driven by `TurboModel` (Boost, Demand, RpmNorm) and gated on
  ENGINE_ON like the effects. The old mod's `TurboTurboOld/TurboAudio.cs`
  documents a working reference path — start from it, then evaluate the
  upgrades below.
  - *Known-working baseline ("GameStyle" path):* pre-render two seamless
    8 s loops with `RenderLoop` (identical seed, one cab-filtered, so they
    stay sample-aligned), play both on per-loco AudioSources, and drive
    pitch + volume per frame — Unity ramps AudioSource parameters
    internally, so it is always smooth. Crossfade exterior/cab by
    `PlayerManager.Car` with an eased tau. Pitch lerp (~0.11..1.0) over an
    eased boost state (tau ~0.8 s, matching the bench sweep); dipole-shaped
    volume (steep boost exponent, weighted by a boost x load pressure
    factor) so the whistle only pierces the mix under load.
  - *Candidate upgrades to evaluate:*
    - **Borrow the vanilla mixer group** (the old mod copied it off the
      car's LayeredAudio layers, with a retry because car audio loads
      late) so cab snapshot ducking applies to the whine equally.
    - **Drive the whine through DV's own LayeredAudio** (a code-built
      instance or an injected layer) instead of raw AudioSources, to
      inherit the game's volume/pitch curves, doppler and mixing
      conventions. Study how vanilla layers are driven per frame
      (`SetVolume`/`SetPitch`) before committing.
    - **Share clips across all DE6s:** loop parameters are identical per
      car type, so render one clip pair once and reuse it for every pooled
      car (the old mod rendered per loco — a few MB of PCM each).
  - *Drop candidate:* the old DSP mode (live per-sample streaming clip).
    Finicky (needed PCM-callback diagnostics) and the loop path is
    indistinguishable at 8 s loop length; only revisit if per-sample state
    coupling (e.g. true surge response) proves audible.
  - *Risks:* cab/exterior crossfade quality (the sample-aligned single-seed
    trick keeps the mix coherent — keep it); mixer group resolution timing;
    long-session loop wrap audibility (the equal-power crossfade should
    smear it); pitch shifting a looped clip changes its perceived length
    (fine for a whine, verify no clicks at minimum pitch).
  - *Prototype plan:* port the GameStyle path into `EngineSimulationHost`
    behind a small per-car whine component (same lifecycle as the effects
    emitters), verify in game against the WhineBench reference render,
    then A/B a LayeredAudio-based variant.
