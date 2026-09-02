# TurboTurbo TODO

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

