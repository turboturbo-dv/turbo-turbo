# TurboTurbo — Heat Shimmer TODO

## Spikes (investigate, don't commit yet)

- [ ] **Cuboid volume instead of billboard.**
  Draw a rectangular cuboid above the chimney whose faces are the working
  area (like today's quad), so the effect is naturally constrained to a
  squarish region above the exhaust from all angles.
  - *Feedback: cheap after item 3* — once the mask is UV-based and the noise
    is in-shader, the "working area" shader works on any mesh; a box is just
    per-face 0–1 UVs. Expect subtle seams at box edges; the soft taper
    should mostly hide them.

- [ ] **Smoke behind the shimmer.**
  Investigate rendering the smoke *behind* the shimmer effect so the plume
  gets displaced along with the background.
  - *Feedback: the early "yellow cylinder" failure was likely not this
    idea's fault* — it was the broken rim/offset math amplifying the bright
    additive plume. With small true-screen-UV offsets and queue-after-smoke,
    the plume should gently wobble. Composes with item 4 (smoke doesn't
    write depth → stays displaceable).

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

- [ ] **Spike: shimmer particles instead of a fixed quad.**
  Emit *shimmer particles* from the exhaust like smoke: each particle is a
  billboard that displaces the grab locally, rising from the stack and left
  hanging in the air as the loco moves — faithfully modelling the hot-air
  trail behind a driving locomotive, which the fixed quad cannot do.
  - *Why the cost is favourable:*
    - The GrabPass capture is the expensive op, and a particle system renders
      all its billboards as **one draw call / one renderer** = one unnamed
      grab per frame, regardless of particle count.
    - Fragment work scales with *covered pixels*, not particle count — the
      plume covers roughly the same screen area as today's quad, plus 2–3×
      overdraw where particles overlap.
  - *Particle interaction:* none, by design — each particle independently
    displaces the same captured grab; overlaps saturate (last draw wins)
    instead of compounding. Physically close enough (turbulence adds
    sub-linearly anyway).
  - *Bonus wins:* particle alpha-over-lifetime replaces the analytic mask
    (flow-scaled taper for free); particle billboarding solves the
    edge-on-view problem; the noise field rides with each particle (slower
    internal scroll, more physical advection).
  - *Risks:*
    - DV grab semantics with multiple users are treacherous (two-loco
      incident). A single particle renderer is one grab user (likely fine);
      two locos = two renderers = must re-verify in game.
    - 2019.4 plumbing: custom vertex streams (per-particle random phase for
      the noise) + GrabPass in a particle shader is an unusual combination.
    - Overlap tuning so dense plumes read coherent, not crawly.
  - *Prototype plan:* WorkBench first — animate a dummy loco transform on
    rails, emit shimmer particles (world-sim, billboard mode), custom vertex
    streams into the shimmer shader (alpha = mask), verify single vs. two
    emitters, overlap behaviour and perf with 100+ particles.
