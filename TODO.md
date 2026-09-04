# TurboTurbo TODO

- [ ] **Cleanup pass**
  Do a full pass over the code base to eliminate defensive guards and other
  needlessly defensive code. If we cannot envision a justification for a guard,
  it should be removed, as it just adds unnecessary noise and complexity.

- [ ] **Particle emitter architecture refactor**
  Currently, both the smoke and shimmer emitters are independent, but they share
  a lot of common logic. It may be worth refactoring them into a common base
  class, but we should also consider that the shimmer emitter will in the future
  also be used for effects like the dynamic brake vents, so it should not be
  tied to the idea that it always pairs up with a smoke emitter.

- [ ] **Investigate transform logic**
  It can probably be simplified, and we may need to create our own copy rather
  than relying on the transform taken from the configuration.

- [ ] **Dev UI activation tweaks**
  Make dev UI hotkey configurable. Independent of hotkey, it should also be
  possible to toggle it by typing `turbodev` in the console. This way, if
  no hotkey is set (which will be the default), the dev panel can still be
  opened. To improve the workflow, the dev panel should also have a "close"
  button, so it can be closed without having to type `turbodev` again.

- [ ] **Make dev panel state tracking resilient**
  If there are no tracked locos, and one appears, the panel tracks it only
  halfway: the telemetry appears, but the specs do not. We should investigate
  this.

- [ ] **Distinguish between adding and replacing exhausts in public API**
  The replace method should resolve a ParticleSystem, which represents the
  exhaust to be replaced, as well as an optional offset in case we want to
  adjust the new exhaust's position slightly. The add method should just
  resolve a transform.

- [ ] **Dev panel: time-series chart for telemetry values (e.g. lambda
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

- [ ] **Light the smoke shader so plumes aren't bright at night (option A)**
  Our smoke shader is unlit (constant per-particle color), so the plume
  keeps daytime brightness at night. Investigation findings, ready to
  work from:
  - *What vanilla does (runtime dump, logs/DE6_particle_systems.log):*
    the ExhaustEngineSmoke renderer material is `'ExhaustSmokeBlack'`
    with shader **`DV/SmokeShader`** (DV-custom - almost certainly the
    same FAKE_LIGHTING/SOFT_CLIPPING family as the explosion/white smoke
    materials), tex Cloud01_8x8, queue 3000, sortMode YoungestInFront,
    inheritVelocity Initial curve=1. Note: the extracted
    `ExhaustSmokeBlack.mat` claims Legacy Shaders/Diffuse - the runtime
    material differs from the bench import; trust the dump. Vanilla is
    lit (fake-lighting family) - being lit is why it reads correctly at
    night. Key vanilla numbers: lifetime 1-1.5 s, startSpeed 7.5,
    startSize 4, gravity 0.045, cone 23.6 deg, maxParticles 113,
    CoL alpha fade-in key at 0.047 then decay to 0, emission module
    DISABLED (DV drives it via bursts from code).
  - *What the lighting environment exposes:* the sun is a single
    directional driven by the Time of Day asset
    (`LightingCoordinator` gets it via `TOD_Components.LightSource`,
    intensity roughly 0.3-1 per its `SunlightIntensity01`
    InverseLerp). In **shader space** (built-in forward) any shader can
    read `_WorldSpaceLightPos0` + `_LightColor0` (main directional),
    `unity_AmbientSky/Equator/Ground` or `ShadeSH9()` (ambient), and
    `unity_Fog*` — regardless of "Lighting Off". In **C#**:
    `RenderSettings.sun`, `sun.color * sun.intensity`,
    `RenderSettings.ambientLight`.
  - *Plan (option A — custom shader lighting):* extend `TurboTurbo/Smoke`
    keeping vertex color x atlas x envelope, and add
    `albedo x (ambient + sunColor x facingFactor)` with a fixed UP normal
    (billboards have no real normal; top-lit smoke, same approach as
    DV's FAKE_LIGHTING), facing factor ~0.6 to avoid full-black when the
    sun is behind. Optionally `UNITY_APPLY_FOG` for distance
    integration. Per-particle soot/haze/straw colors and blending all
    survive. ~20 lines of HLSL.
  - *Trap to avoid (option C):* cloning the vanilla lit material is lit
    for free but Legacy/Diffuse ignores vertex color AND alpha - kills
    the per-particle model colors, the fade envelope and soft blending.
  - *Stopgap (option B), if shader iteration is unwanted:* per frame in
    C#, compute `sun.color * intensity + ambientLight` and multiply into
    `ExhaustSmokeModel.Color` before baking. Uniform tint only, baked at
    emission (fine over a 2 s life), no fog, no directional feel.
  - *Runtime verification (log from BindEffects, decompiled source can't
    answer these):* the vanilla PS renderer's actual runtime shader name
    and `lightProbeUsage`; confirm TOD ambient mode (trilight vs flat).

- [ ] **Speed-based smoke dispersion (shorten trail with speed, keep
  dense plume at standstill)**
  Design is settled, implementation deferred (pending another
  investigation). Sketch:
  - *Signal:* `TrainCar.GetAbsSpeed()` (TrainCar.cs:1133) - scalar m/s
    along the car's forward axis, sign-independent (reversing reads
    positive), robust against derailment tumbling (unlike
    `velocity.magnitude`). Fed per frame by the host:
    `smoke.speed = _trainCar.GetAbsSpeed()` in UpdateEffects.
  - *Why:* trail length ≈ speed x particle lifetime (world-sim puffs are
    left behind where emitted) - at 54 km/h with 1.5 s life that's a 20+ m
    trail. Real plumes are torn apart by relative wind, which grows with
    speed; heavy-dense smoke while lugging from standstill must survive
    (speed ≈ 0 → speedNorm ≈ 0 → full life, soot-driven rate untouched).
  - *Knobs (all emission-time EmitParams - NO per-frame Configure, module
    properties like drag are structural and stay out of the per-frame
    path):*
    - lifetime shortening: `startLifetime = lifetime x Lerp(1,
      speedLifetimeScale, speedNorm)`, speedLifetimeScale ≈ 0.4;
    - dispersion jitter: `Random.insideUnitSphere x (baseJitter +
      speedJitter x speedNorm)`, baseJitter = current 0.15,
      speedJitter ≈ 0.5.
  - *Normalization:* `speedNorm = Clamp01(absSpeed / speedNormMax)`,
    speedNormMax ≈ 15 m/s (near DE6 top speed).
  - *Dev panel:* three live specs (speedNormMax, speedLifetimeScale,
    speedJitter) + `speed` in telemetry; knobs join the YAML dump
    automatically. Shimmer unaffected (short-lived, hugs the stack).
  - *Effort:* ~15 lines in SmokeParticles, 1 line in the host, 4 spec rows.

## Spikes (investigate, don't commit yet)

- [ ] **Spike: slight gaussian blur in the shimmer.**
  Add a small gaussian blur to the shimmer to model smaller-scale shimmering
  that isn't really recognizable on its own and just serves to slightly
  increase the apparent "density" of the hot air (softens the background a
  touch inside the mask).

- [ ] **Spike: displaced samples landing on foreground silhouettes.**
  With the depth fix, background pixels adjacent to foreground objects are
  correctly left undisplaced *at their own position*, but their computed
  offset can still resolve to a foreground pixel in the grab, smearing
  foreground texture into the background. Candidate approaches: validate the
  offset target with a second depth compare at (base UV + offset) and
  reject/shrink offsets that cross a depth discontinuity; or dilate/
  edge-extend the foreground depth so protected regions are wider than the
  geometry itself.

- [ ] **Spike: integrate the turbo whine synth into the mod.**
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
