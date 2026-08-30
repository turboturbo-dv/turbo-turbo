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
