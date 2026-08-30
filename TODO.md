# TurboTurbo — Heat Shimmer TODO

## Tuning / improvement backlog

- [x] **1. Shimmer strength should track engine mass flow, not soot.**
  Currently strength follows the soot-emission signal. More flow through the
  engine should mean more shimmer; incomplete combustion shouldn't matter.
  Derive heat from RPM + fuel consumption (more fuel burned → more heat →
  stronger shimmer).
  - *Feedback/agreed additions:*
    - Use fuel flow as the primary signal (RPM alone lies — high idle RPM
      isn't much heat); RPM as a secondary/normalize factor.
    - Apply thermal inertia, but **asymmetric**: rise instantly (or near-
      instantly) so throttle kicks read immediately, decay slowly (~3 s) so
      the column lingers after the throttle closes. A plain low-pass would
      wrongly delay the attack too.

- [x] **2. Shimmer speed as a flow-dependent range.**
  Idle (min flow) → speed 0.5, full flow → speed 2. Boosting flow should
  also boost how fast the shimmer propagates upward (hot air leaving the
  exhaust faster), not just the amplitude.

- [x] **3. Break up the repetitive pattern.**
  The displacement shows strong horizontal/vertical repetition (sum-of-sines
  noise). Use a proper noise function (value/Perlin/simplex) for a more
  random, organic look.
  - *Feedback: do 2+3+5 together as one shader rewrite.* Move the offset
    field into the fragment shader (2–3 octaves of value noise, vertically
    stretched for the rising-plume look). This eliminates the shared CPU
    map, the 15 Hz updates, the one-global-scroll-speed problem (per-engine
    `_FlowSpeed` uniform), and the repetition, all at once.

- [ ] **4. Foreground bleed.**
  Objects in front of the shimmer effect have their edges bleed/displace
  into the effect. Looks strange.
  - *Feedback/diagnosis:* opaque geometry nearer than the quad (handrails,
    other cars) is inside the grab, so it gets displaced even though it is
    in front of the hot air. Fix: compare the quad's per-fragment view depth
    against `_CameraDepthTexture`; if opaque geometry is in front, output
    the grab undisplaced. Transparent smoke doesn't write depth, so it stays
    displaceable (which matters for the smoke spike below).

- [x] **5. Dynamic effect sizing within the quad.**
  1. Scale the size of the entire effect within the quad based on engine
     flow.
  2. Smoothly blend the effect edge into the non-shimmering area by
     tapering strength near the edge.
  3. At max flow the taper should exactly reach the quad edges — then total
     size is tuned purely by rescaling the quad.
  4. At idle the effect is small but still visible, only directly above the
     chimney; most of the quad unaffected.
  - *Feedback:* lands on the quad-space mask; radius = lerp(idleRadius, 1.0,
    flow) with a smoothstep taper. Do together with 2+3 (same shader).

- [x] **6. Camera support (F2/F3).**
  The billboard only faces the first-person camera; the orbiting and free
  roam cameras see the effect edge-on/disappearing. Make it face the active
  camera (or otherwise work in external views).
  - *Feedback: partly fixed already — verify before building more.* The
    unnamed grab (per-camera capture) removed the multi-camera stale-grab
    failure mode, and shader-side noise (item 3) removes the viewport-mask
    dependency on `ActiveCamera`. Remaining work is likely just billboard
    orientation.

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

## Suggested order

1. Item 1 (flow signal with asymmetric inertia) — build, test.
2. Items 2+3+5 together (one shader-side rewrite: noise, per-flow speed,
   flow-scaled tapered mask) — build, test.
3. Item 4 (depth test).
4. Item 6 (verify external cameras after 2+3).
5. Spikes: cuboid, then smoke ordering.
