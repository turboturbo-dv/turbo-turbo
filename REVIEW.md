# TurboTurbo Code Review

## Scope

The review covered:

- Everything under `TurboTurbo/`.
- C# source under `WorkBench/`.
- Shader source under `WorkBench/`.

Tests, decompiled game assemblies, logs, root build scripts, TODO documentation, generated artifacts, and git history were used only to establish context.

The review priorities were:

| Priority | Area |
|---|---|
| High | Numerical model correctness and equivalence to the real-world processes represented within each model's stated scope |
| High | Architecture |
| Medium-high | Overly defensive guards |
| Medium-high | Runtime and rendering performance |
| Medium | Build, asset, packaging, and dependency reliability |
| Low | Remaining lifecycle hazards |
| Low | Resource ownership and cleanup |
| Advisory | High-impact test-suite opportunities |

The project was treated as known to build, pass its existing tests, and work in the game. No build or test verification was performed.

Five medium-high-priority findings received primary evaluation. Additional findings are explicitly identified where their conclusions rely partly on subagent judgment.

## Review Findings

No critical or high-severity defect survived focused investigation.

### 1. Engine Cap Depends On An Implicit Simulation-Ordering Contract

**Priority:** Medium-high  
**Judgment:** Primary evaluation  
**References:** `TurboTurbo/Runtime/EngineSimulationHost.cs:84-88`, `TurboTurbo/Runtime/EngineSimulationHost.cs:137-166`

The host retrieves the private `Port` behind `de.THROTTLE`, reads it as model demand, and writes `EffectiveDemand` back to that same port.

The broad review initially suggested persistent self-feedback. A focused trace disproved that for the configured DE6:

- `de.THROTTLE` references `throttleCalculator.LAYSHAFT_POSITION`.
- `DieselEngineDirect` consumes it at simulation component index 31.
- `throttleCalculator` restores the vanilla value at index 33.
- The normal sequence is therefore cap consumption followed by restoration, not permanent ratcheting.

The implementation nevertheless depends on several undocumented contracts:

- The engine must execute before the throttle calculator.
- The calculator must restore the port after every engine tick.
- Unity may run `EngineSimulationHost.Update` before or after `SimController.Update`, producing either same-tick or one-tick-delayed application.
- The host continues ticking when the simulation flow is paused. During skipped simulation frames, the restoring component does not run, allowing repeated reads of the previous capped value.
- Other locomotives do not necessarily share the DE6's port topology. Available DE2 evidence indicates a materially different connection.

There is also a semantic boundary mismatch. `TurboModel` describes `EffectiveDemand` as torque-capped demand, but `DieselEngineDirect` uses the same throttle value for both torque and fuel consumption. The host therefore denies the actual fuel that `Lambda` and `Overfuel` still model as requested fuel. This is not an internal contradiction in `TurboModel`, but the adapter does not clearly define the approximation.

**Recommendation:** Introduce an explicit engine-adapter boundary that runs at a deterministic simulation phase and distinguishes requested fueling from the value written into the game engine. At minimum, document and test the DE6 execution-order contract rather than leaving it implicit in a reflected port write.

### 2. Turbo Whine Harmonic Taper Does Not Prevent Higher-Order Aliasing

**Priority:** Medium-high  
**Judgment:** Primary evaluation  
**References:** `TurboTurbo/Modeling/TurboDsp.cs:130-151`

The DSP tapers nonlinear drive as blade-passing frequency approaches `sampleRate / 6`, protecting the third harmonic near Nyquist. The saturated `tanh(sin)` signal also contains fifth, seventh, and higher odd harmonics.

At 44.1 kHz:

- The fifth harmonic aliases once BPF exceeds 4.41 kHz.
- The configured full-speed BPF is approximately 7.2 kHz.
- The taper does not begin reducing full drive until approximately 4.78 kHz.
- Even the minimum `drive = 1` remains nonlinear and continues producing harmonics.

The fundamental itself remains below Nyquist, but that does not make the nonlinear output band-limited. This makes the comment about harmonics tapering "naturally instead of aliasing" too strong.

**Recommendation:** Measure the aliased energy with an FFT, then choose one of:

- Crossfade toward an unsaturated sine before the fifth harmonic reaches Nyquist.
- Use a genuinely band-limited waveshaping method.
- Oversample the nonlinear branch and filter before downsampling.
- Accept the coloration deliberately and weaken the anti-aliasing claim.

This matters to the bench and future audio integration, not the currently running game mod.

### 3. Pre-Rendered Whine Loops Are Crossfaded Before Reaching Steady Pitch

**Priority:** Medium-high  
**Judgment:** Primary evaluation  
**References:** `TurboTurbo/Modeling/TurboSynth.cs:55-95`

`RenderLoop` discards `4 * TauSpool + 1` seconds before retaining the loop. With the default `TauSpool = 1.8`, approximately 1.05% of the shaft-speed error remains when the retained section begins.

At full load:

- Initial retained shaft speed is roughly 378 RPM below equilibrium.
- Most of that difference decays during the retained eight-second segment.
- With 12 blades, the BPF differs by approximately 75 Hz between the retained head and tail.

The equal-power crossfade removes a discontinuous click, but it blends tones at different frequencies. Over the 0.2-second crossfade this creates repeated beating rather than the documented gentle phase smear.

**Recommendation:** Initialize the loop renderer at the analytical steady state, or discard based on a convergence tolerance rather than a fixed four-time-constant rule. Add a seam-frequency test before selecting a final tolerance.

### 4. Smoke Tuning Allows Invalid Cross-Parameter States

**Priority:** Medium-high  
**Judgment:** Primary evaluation  
**References:** `WorkBench/Assets/Shimmer/ExhaustSmokeModel.cs:61-71`, `TurboTurbo/DevUI/TurboDevPanel.cs:227-256`

Two calculations require relationships that the tuning interface does not enforce:

```text
SootOnsetLambda > SootOpaqueLambda
WetStackBurnRampDemand > WetStackIdleDemand
```

The slider ranges overlap, making equality and inversion reachable.

Consequences include:

- An inverted soot ladder, where richer combustion produces less soot.
- An inverted wet-stack ramp, where burn visibility is strongest at lower demand.
- Infinite or NaN intermediate values when thresholds match.
- In specific NaN cases, poisoning `_emitAccumulator`, after which the smoke emitter stops producing particles until recreated.

This is more than ordinary defensive validation. These are actual model invariants and belong at the model/configuration boundary.

**Recommendation:** Represent smoke tuning as a validated settings object and enforce relational constraints when values change. Do not scatter denominator epsilon guards through `Update`, since those would hide invalid configuration and make inverted settings appear valid.

### 5. Heat Shimmer Performs Its Most Expensive Work For Non-Contributing Pixels

**Priority:** Medium-high  
**Judgment:** Primary evaluation  
**References:** `WorkBench/Assets/Shimmer/HeatShimmer.shader:68-101`, `WorkBench/Assets/Shimmer/HeatShimmer.shader:115-172`

Each fragment evaluates two three-octave FBM fields before calculating final alpha. That is 24 hash evaluations, plus interpolation and texture work, even when the fragment contributes nothing.

The current radial mask makes exactly zero contribution over approximately:

- 63% of the billboard at the default idle radius of 0.8.
- 42% of the billboard at the full-load radius of 1.0.

Late-life particles are also the largest while their alpha approaches zero. This compounds transparent overdraw with the shader's most expensive computation.

A normal-path early skip is blend-equivalent at alpha zero. It must be placed after the outline handling and disabled for shader debug modes.

**Recommendation:** Compute mask and particle alpha before FBM and skip fragments below a small threshold. Profile the compiled shader to ensure the branch remains effective and does not cause a larger early-Z regression.

## Additional Findings

The following findings were not given the same depth of primary investigation. Their conclusions rely partly on subagent judgment where stated.

### Medium

#### Dead `fuelNorm` Contract

**References:** `TurboTurbo/Modeling/TurboModel.cs:78-118`, `TurboTurbo/Runtime/EngineSimulationHost.cs:42`, `TurboTurbo/Runtime/EngineSimulationHost.cs:84`, `TurboTurbo/Runtime/EngineSimulationHost.cs:145-159`

`TurboModel.Tick` accepts `fuelNorm`, but never reads it. The host still resolves and reads `FUEL_CONSUMPTION_NORMALIZED` every frame.

Git history indicates this is residue from an older fuel-driven heat model. The current model intentionally uses engine-gated demand as its normalized fueling proxy. The cleanest current action is to remove the parameter and binding unless fuel-derived heat is intentionally restored.

The dead parameter is also misleading because the game's normalized fuel-consumption signal is materially different from demand: it is RPM-scaled and includes an idle floor.

#### Partially Initialized Host Is Exposed To The Development UI

**References:** `TurboTurbo/Runtime/EngineSimulationHost.cs:45-55`, `TurboTurbo/Runtime/EngineSimulationHost.cs:67-68`, `TurboTurbo/Runtime/EngineSimulationHost.cs:181`, `TurboTurbo/DevUI/TurboDevPanel.cs:168-181`, `TurboTurbo/DevUI/TurboDevPanel.cs:443`, `TurboTurbo/DevUI/TurboDevPanel.cs:467-469`

`_trainCar` is assigned only during effect binding, despite `Configure` already retrieving it into a local. `CarId` dereferences `_trainCar`, while the dev panel reads `CarId` specifically when the host is not bound. `BuildSections` also dereferences `host.TurboModel` without checking `Bound`.

The likely result is transient or permanent development-panel exceptions around failed or incomplete binding. Assigning `_trainCar` in `Configure` and making the panel explicitly represent unbound hosts would remove most of the invalid partial state.

This conclusion relies partly on the subagent's lifecycle trace, though the invalid property access is directly visible in the source.

#### Per-Sample Implementation Contradicts The DSP Performance Contract

**References:** `TurboTurbo/Modeling/TurboDsp.cs:17-18`, `TurboTurbo/Modeling/TurboDsp.cs:102-103`, `TurboTurbo/Modeling/TurboDsp.cs:162`

The class documentation says transcendental setup is precomputed, but `ProcessSample` calculates `Math.Exp` and `Math.Pow` every sample. `_lastDt` is assigned but never read, suggesting unfinished caching.

This is not current game-runtime cost because audio integration is absent. Before integration, the decay coefficient should be cached by `dt`, and other per-sample transcendental work should be reviewed against actual buffer-level state changes.

#### Named GrabPass Imposes A Fixed Per-Camera Cost

**Reference:** `WorkBench/Assets/Shimmer/HeatShimmer.shader:16-19`

The named GrabPass correctly amortizes the framebuffer copy across all shimmer renderers, but one tiny visible particle still triggers a full-screen copy for that camera.

The estimated read-plus-write bandwidth per frame for an RGBA8 target is approximately:

| Resolution | Approximate transfer |
|---|---:|
| 1920x1080 | 16.6 MB |
| 2560x1440 | 29.5 MB |
| 3840x2160 | 66.4 MB |

MSAA or additional cameras may increase the practical cost. This is a strong static concern, but actual impact needs GPU timing.

#### Stale Harmony Reference Configuration

**Reference:** `TurboTurbo/TurboTurbo.csproj:38-39`

The explicit `0Harmony` reference points at undefined `$(BepInExCore)`. The known-good build currently succeeds because Unity Mod Manager brings Harmony transitively.

The explicit reference is therefore misleading and makes dependency ownership unclear. If Harmony is intentionally transitive, remove the dead reference. If it is a direct dependency, reference it directly and intentionally.

This finding relies on the reliability subagent's project-assets inspection.

### Low Or Documented

#### Vent Configuration Is Accepted And Discarded

**References:** `TurboTurbo/Setup/EngineOptions.cs:38-53`, `TurboTurbo/Main.cs:28-31`

`EngineOptions` accepts traction-motor and dynamic-brake vent selectors but drops both lists in `Build`. This is documented unfinished work, but accepting unsupported configuration silently is worse than leaving the API absent.

#### Smoke Tuning Mixes Global And Per-Instance State

**Reference:** `WorkBench/Assets/Shimmer/ExhaustSmokeModel.cs:12-27`

`ExhaustSmokeModel` mixes instance soot thresholds with process-wide static tuning fields. The dev panel consequently has separate locomotive and global mutation paths. This will become difficult to reason about once multiple engine configurations exist.

#### Surge Event Is Frame-Rate Dependent

**Reference:** `TurboTurbo/Modeling/TurboModel.cs:116`

`SurgeThisTick` uses a fixed per-tick demand change instead of a rate. It is frame-rate dependent and physically only a rough load-rejection proxy. It currently has no production consumer, while `TurboDsp` has a separate rate-based surge detector.

#### Shimmer Material Properties Are Rewritten Every Frame

**Reference:** `WorkBench/Assets/Shimmer/ShimmerParticles.cs:201-212`

`ShimmerParticles` writes six material properties every frame, including mostly constant values. The work is definite but probably minor relative to fragment and GrabPass cost.

#### Smoke Continues Updating While The Engine Is Off

**Reference:** `WorkBench/Assets/Shimmer/SmokeParticles.cs:180-225`

Smoke continues updating and can briefly emit transparent particles while the engine is off. This has low expected impact.

#### Runtime Shader Support Is Not Checked

**References:** `TurboTurbo/ModAssets.cs:26`, `TurboTurbo/ModAssets.cs:78-85`, `WorkBench/Assets/Editor/BuildBundle.cs:124-125`

The editor bundle verifier checks `shader.isSupported`, while runtime validation checks only whether the shader references are non-null. An unsupported but successfully loaded shader is therefore considered valid at runtime.

This is unlikely on the known target but creates an avoidable difference between build-time and runtime diagnostics.

## Numerical Assessment

### TurboModel

Within its stated empirical scope, the model is largely coherent:

- Boost uses an exact first-order exponential update and is not inherently frame-rate dependent.
- Per-stroke charge increases monotonically with normalized boost.
- Lambda decreases with requested fueling and increases with available charge.
- The torque cap directly expresses the configured lambda floor.
- RPM-dependent boost equilibrium captures reduced exhaust mass flow at low engine speed.
- Asymmetric rise and decay constants plausibly represent spool and blow-down.
- Overfuel shortening the spool-up time is a reasonable exhaust-enthalpy approximation.

Important qualifications:

- `Boost` is called a "pressure ratio [0..1]" in `TurboTurbo/Modeling/TurboModel.cs:36`, but it is actually a normalized boost state. A pressure ratio would normally begin at 1.
- `fuelDemand` is requested throttle gated by engine state, not measured fuel consumption. This is defensible if the API says so.
- `Lambda` is a calibrated smoke and air-availability proxy, not physical diesel lambda.
- `Overfuel` describes requested excess fueling even though the adapter subsequently suppresses actual game fuel.
- The surge output is a qualitative event detector, not a compressor-map model.

There is no clear need to replace this with a thermodynamic turbo model. The important improvement is making the abstraction and adapter contract explicit.

One subtle output-timing consideration remains: `Charge`, `Lambda`, and `EffectiveDemand` are computed from the pre-update `_boost`, while public `Boost` is assigned after `_boost` advances (`TurboTurbo/Modeling/TurboModel.cs:90-114`). The explicit-step computation is valid, but consumers reading all outputs after `Tick` see values from slightly different points in the step. This is normally negligible at frame-sized timesteps and was not elevated to a finding.

### ExhaustSmokeModel

The smoke model is appropriately scoped as an appearance model:

- Lambda-driven soot is monotonic under valid threshold configuration and causally appropriate.
- Wet stacking accumulates at low demand and burns under load.
- Compressed accumulation and burn times are reasonable gameplay tuning rather than correctness errors.
- RPM-driven oil tint is an artistic proxy, not a model of actual blowby or wear.

The main correctness problem is invalid relational tuning, covered in finding 4.

A smaller visible discontinuity exists because wet-stack visibility is hard-gated by `WetStackBurnDemand` while its ramp starts at `WetStackIdleDemand`. With defaults, crossing the burn-demand threshold after extended idling introduces a one-frame step in visible wet burn. This is primarily a calibration and presentation issue.

The accumulator also uses clamped linear integration. A sufficiently large frame interval can consume the stored amount before producing a visible burn frame. That is acceptable for normal game timesteps but worth covering if deterministic replay or unusually large timestep support becomes a goal.

### TurboDsp And TurboSynth

The core DSP is stronger than its lack of tests suggests:

- Filter stability bounds appear sound.
- Sample-rate-scaled coefficients are used consistently.
- The PRNG implementation and zero-seed handling are appropriate.
- External-state interpolation is numerically simple and deterministic.
- Tanh normalization bounds the tonal branch.
- Blade-passing-frequency arithmetic is physically meaningful.
- Cab filtering is downstream and does not disturb paired-loop phase alignment.
- The internal turbo lag is bounded under large positive timesteps.

The two material issues are the non-steady loop seam and incomplete harmonic alias control.

Additional integration considerations include:

- External boost has a lower bound but no upper bound, allowing extreme waveshaping drive if a future game adapter supplies unexpected values.
- Switching from external state back to internal state can step the boost-dependent gain because internal boost is immediately recomputed from RPM and load.
- The live DSP mix has no output limiter. Existing offline render paths normalize after rendering, so this only matters if live streaming is restored.
- The SVF coefficient updates in absolute 25 Hz increments. This is a large relative interval near the 40 Hz floor, though the noise-driven input makes audible stepping uncertain.

## Architecture Assessment

The architecture has several notable strengths:

- Game-specific coupling is mostly concentrated in `EngineSimulationHost`.
- `TurboModel` and `ExhaustSmokeModel` are pure enough to test without running Unity.
- Asset loss and spawner recreation are handled deliberately.
- Particle-origin shifting is centralized rather than scattered through emitters.
- The development-panel specification system cleanly separates display controls from tuned fields.
- Linked source files prevent bench/runtime source-copy drift.
- Expensive reflection and port discovery are confined to initial binding rather than hot paths.

The main architectural pressure points are:

1. `EngineSimulationHost` handles discovery, reflection, simulation adaptation, model execution, game mutation, effect construction, and effect updates. The fragile throttle-port contract is buried inside that broad responsibility.
2. `TurboModel` pulls throttle and RPM through constructor closures while receiving other inputs through `Tick`. Passing a complete input snapshot to `Tick` would make data dependencies and adapter feedback visible.
3. `EngineConfiguration` does not carry model settings, while `EngineSimulationHost` constructs defaults directly. Supporting another locomotive will require editing runtime construction rather than composing configuration.
4. Effect settings mix static, per-model, and per-emitter mutation lifetimes.
5. Runtime effect sources live under WorkBench and span `TurboTurbo.Modeling`, `TurboTurbo.WorkBench`, and the root `TurboTurbo` namespace. Linked compilation prevents drift, but ownership and naming no longer reflect the deployed architecture.
6. The dormant audio path is compiled into the runtime assembly without an active integration boundary. This is harmless today but obscures which APIs are prototypes and which are supported runtime contracts.

A focused architectural improvement would extract a small deterministic engine adapter and make `EngineSimulationHost` primarily responsible for Unity lifecycle and composition. A broad framework rewrite is not warranted.

### Suggested Target Shape

A minimal target architecture could have these boundaries:

| Component | Responsibility |
|---|---|
| Engine adapter | Resolve supported game ports, capture one input snapshot, apply model output at a deterministic simulation phase |
| `TurboModel` | Advance pure turbo state from explicit inputs and return explicit outputs |
| Effect host | Translate model outputs and car motion into emitter inputs |
| Effect models | Compute appearance and emission policy without Unity object discovery |
| Unity emitters | Configure particle systems and perform emission |
| Configuration | Own per-engine numerical and effect settings, including relational validation |

This separation would address the highest architecture finding without introducing a large framework.

## Defensive Guard Assessment

| Guard area | Recommendation |
|---|---|
| Simulation-flow retry in `TryBindSimulation` | Retain. It protects a real initialization window. |
| Spawner destroyed-vs-null handling | Retain. The `ReferenceEquals` distinction is required by Unity object semantics. |
| Duplicate host protection for pooled cars | Retain. It enforces a real idempotency invariant. |
| Missing `DieselEngineDirect`, throttle, or RPM | Retain the boundary checks, but make failure explicit rather than leaving a silent partial host. |
| Missing fuel port fallback | Remove with dead `fuelNorm` plumbing. |
| Missing `ENGINE_ON` fallback to `rpm > 0.05` | Remove or make it an explicit compatibility policy. Every `DieselEngineDirect` defines the port, and the fallback gives incorrect coast-down semantics. |
| Repeated `rpmNorm` and `fuelDemand` clamps inside `TurboModel.Tick` | Remove downstream duplicates at `TurboTurbo/Modeling/TurboModel.cs:98`, `TurboTurbo/Modeling/TurboModel.cs:107-108`; retain the input clamps at `TurboTurbo/Modeling/TurboModel.cs:82-83`. |
| Lambda zero-fuel denominator floor | Retain. It is mathematically required. |
| DSP stability, Nyquist, normalization, and zero-seed guards | Retain. These are substantive numerical protections. |
| Per-frame emission cap | Retain. Dropping particles after a major hitch is preferable to an emission burst. |
| Asset loading failure at initial startup | Retain fail-fast behavior. |
| Warn-and-continue after shader reload failure | Prefer one bind-level validation and a clear disabled-effects state over multiple partially configured emitters. |
| Exhaust selector null handling | Retain graceful skipping, but surface configuration failure once and clearly rather than warning independently for every car. |
| Dev-panel selected-index clamp | Retain. The tracked host list is mutable. |
| Gradient-key limit handling | Retain. Unity enforces the eight-key limit. |

The main over-defensiveness is in the engine binding path: guaranteed ports receive fallback semantics instead of establishing one explicit supported-engine contract. The model and shader guards are mostly justified and should not be included in a broad cleanup.

## Performance Assessment

### CPU

The normal panel-closed gameplay path is largely allocation-free:

- `TurboModel.Tick` is pure arithmetic.
- `ExhaustSmokeModel.Update` is pure arithmetic.
- Port discovery, LINQ, and reflection occur only during binding.
- Car velocity is retrieved once per host update and shared across exhaust effects.
- Emission accumulators are bounded against lag-spike bursts.

The strongest CPU opportunities are smaller than the shader concerns:

- Hoist custom simulation-space transformations out of per-particle emission loops where possible (`SmokeParticles.cs:206-223`, `ShimmerParticles.cs:184-198`).
- Avoid rewriting unchanged material and particle-system module values every frame.
- Cache the surge decay coefficient by `dt` instead of evaluating `Math.Exp` every audio sample.
- Treat open development-panel allocations separately from normal gameplay; the panel object is destroyed when closed, so its IMGUI string churn is not a general runtime concern.

### GPU

The heat shimmer is the likely dominant effect cost:

- Transparent billboards receive no depth-writing benefit against each other.
- Particles grow substantially over their lifetime while fading.
- Two three-octave FBM evaluations run per covered fragment.
- A framebuffer grab occurs once per camera when any shimmer renderer draws.

The first optimization to investigate is early rejection of zero and near-zero contribution. The second is distance or screen-size gating that prevents a distant shimmer particle from triggering the full-screen GrabPass.

Replacing GrabPass with a lower-resolution command-buffer capture could reduce bandwidth, but that is a larger rendering architecture change and should follow profiling rather than precede it.

## Build, Asset, And Dependency Reliability

The in-scope build and asset code is generally explicit:

- Initial asset-bundle failure aborts mod loading rather than leaving a half-running mod.
- Asset-bundle loss after world reload is detected and reloaded.
- The editor bundle tool verifies asset presence and shader support.
- Runtime asset paths are explicit and failures include useful diagnostics.

The principal concerns are:

- Harmony dependency ownership is unclear because a dead explicit HintPath coexists with a working transitive package dependency.
- Bundle and shader identity strings are duplicated between runtime code, editor tooling, and packaging context.
- Runtime validation does not check shader support as the editor verifier does.
- A failed bundle reload logs the exception and an additional error, then effect binding may continue with null shaders.

These are medium or lower reliability concerns rather than evidence that the known-good build is unreliable.

## High-Impact Test Suites

### 1. Engine Adapter Contract Suite

Extract the DE6 port interaction behind a small interface. Simulate:

- Engine consumption before throttle-calculator restoration.
- Both possible Unity `Update` orderings.
- Skipped simulation frames while the host still updates.
- Engine shutdown and restart.
- Alternate locomotive wiring where the referenced port is not restored after engine consumption.
- Fuel and torque consumption from the same written throttle value.

This protects the mod's central behavior and would make the implicit execution-order dependency executable documentation.

### 2. DSP Spectral Suite

Render fixed operating points and use FFT assertions for:

- Fundamental blade-passing frequency.
- Fifth- and seventh-harmonic alias energy.
- Sample-rate consistency at 44.1, 48, and 96 kHz.
- Intake-filter center frequency and stability.
- Surge modulation frequency and decay.
- Cab-filter attenuation.

This is the highest-value numerical suite for the currently untested audio model.

### 3. Loop Seam Suite

Measure around the generated loop boundary:

- Instantaneous frequency at the retained head and tail.
- Maximum waveform discontinuity.
- Crossfade amplitude modulation.
- Cab/exterior sample alignment.
- Determinism for a fixed seed and settings object.

Assert a defined maximum head-tail pitch difference rather than merely checking sample continuity.

### 4. Smoke Settings Property Suite

Sweep valid tuning combinations and assert:

- Finite color and density outputs.
- Monotonic soot response as lambda falls.
- Monotonic wet-stack burn visibility as demand rises.
- Accumulator bounds under varying timestep and demand.
- Engine-off accumulator semantics.
- Explicit rejection or normalization of inverted thresholds.

Include exact-equality cases for both cross-field denominators.

### 5. TurboModel Property Suite

Sweep the development-panel parameter ranges and assert:

- Finite outputs.
- Charge monotonicity with boost.
- Effective demand never exceeds requested demand.
- Torque-cap behavior agrees with the configured lambda floor.
- Stable convergence to analytical boost equilibrium.
- Engine-off boost decay.
- Comparable trajectories across practical timesteps.
- Explicit behavior for over-rev RPM inputs.

The existing example tests are useful; this suite would target invariants and parameter interactions rather than more point examples.

### 6. Configuration Contract Suite

Assert that every `EngineOptions` method either contributes to `EngineConfiguration` or rejects unsupported configuration. Include per-engine model and effect settings once they are moved into configuration.

### 7. Unbound-Host Development UI Suite

Exercise hosts:

- Before simulation binding.
- After engine binding failure.
- With a turbo model but no effects.
- After the selected host disappears.
- With multiple hosts where the player car has no host.

This protects the diagnostic tooling exactly when it is most needed.

### 8. GPU Profiling Scenario Suite

Capture one, five, and ten visible DE6s at idle and load, at 1080p and 4K. Compare:

- Named GrabPass cost.
- Fragment cost.
- Billboard overdraw.
- An early-alpha-skip shader variant.
- Near and distant camera positions.
- Single- and multi-camera rendering if the game uses additional cameras.

This should be a repeatable profiling scenario rather than a unit test.

## Existing Strengths And Rejected Concerns

Several tempting findings were rejected after inspection:

- The DE6 throttle write does not persistently self-feed during normal flowing simulation because the later throttle calculator restores the source port.
- Shared WorkBench/runtime source does not drift because the same physical files are linked into the projects.
- `TurboModel` boost integration is frame-rate independent for a constant target and time constant.
- Particle world-origin shifting is explicitly handled through a custom simulation space.
- Asset-bundle loss during reload is explicitly detected and repaired.
- Spawner re-hooking and duplicate-host prevention address real game lifecycle behavior.
- The named GrabPass is shared across shimmer users rather than repeated for every particle or locomotive.
- Mathematical DSP guards generally enforce real stability or denominator constraints and should not be removed as generic defensive code.
- The emitter lag-spike cap deliberately drops overflow particles rather than deferring a large burst, which is the appropriate visual behavior.

## Recommended Order Of Work

1. Make the DE6 engine-adapter timing and demand semantics explicit and testable.
2. Add the DSP spectral and loop-seam tests before integrating audio.
3. Enforce smoke-model relational invariants.
4. Profile and optimize zero-contribution shimmer fragments.
5. Remove dead `fuelNorm` plumbing or deliberately restore its intended role.
6. Eliminate the partially initialized host state exposed to the development panel.
7. Clarify configuration ownership for per-engine model and effect settings.
8. Clean up dead or misleading dependency and vent configuration surfaces.

No source or project behavior was changed as part of this review.
