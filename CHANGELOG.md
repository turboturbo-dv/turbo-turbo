## v1.2.0

### Added
- Roots-blown charger simulation. A Roots blower provides a slight boost and is
  tied to the crankshaft, which affects how it behaves in game (lighter, shorter
  bursts of soot on throttle up).

### Changed
- Smoke intensity now gets divided by the number of exhausts that are present,
  to ensure that two overlapping plumes look just as strong as one single plume.

### Fixed
- Effects now render properly in VR mode

## v1.1.0

### Added
- The simulation now includes a detailed thermal model. Combustion warms up the
  surfaces inside the cylinders, which then warm up the engine block.
- Unburned fuel is now simulated alongside partially burned fuel (soot). Unlike
  soot, unburned fuel leaves the engine as vapour, turning into white smoke.
- Added ignition efficiency calculations, which depend on temperature and
  compression ratios. This together with unburned fuel simulation results in
  realistic cold-start behaviour.
- To make the most of the cold starting effects, integration with DV Seasons has
  been added. This is optional, but with Seasons installed, its ambient
  temperature simulation will be used to determine engine temperatures.
- Without Seasons installed, TurboTurbo falls back to its own, simpler, ambient
  temperature calculations that vary only based on time of day and weather.
- The Seasons integration can also be disabled via a toggle in the settings
  menu.
- Added a gentle wind that randomly changes over time. Wind picks up the smoke
  causing it to drift away slowly.
- With Seasons installed, during a blizzard, smoke flies away very rapidly.

### Changed
- Smoke now fades in slightly more quickly after leaving the exhaust, so there
  is less of a gap between the smoke plume and exhaust opening.
- Exhaust velocity now derives directly from exhaust gas flow rate and density.
- The turbocharger is now powered by actually simulating the extraction of heat
  from the exhaust gas flow.
- Reworked wet stack simulation to make use of exhaust gas temperature. When
  fuel vapour is present and the stack is cold, the vapour can condense. When
  the stack warms up, condensed fuel starts to evaporate.
- Improved colour blending in the smoke shader, so that light smoke looks
  better.
- Added slight turbulence to the smoke at standstill.
- Smoke is now emitted while the engine starter runs. This will generally be
  white smoke as misfires are still common at this point.
- Smoke particles now animate faster, and at a fixed frame rate, no longer
  dependent on particle lifetime.
- Improved performance of the lighting calculations in the smoke shader.
- Smoke particle growth rate is now independent of total lifetime. Lifetime now
  purely affects how long a particle remains on screen.
- Improved the fading of large particles when clipping through geometry,
  further softening the edges on large particles that partially intersect other
  objects.
- New configuration file version. The old version is still supported.
- Temporarily raised the draw order of the smoke and shimmer shaders, so they
  draw over the ice effect in Seasons, which draws a bit too late. Will be
  reverted when it is fixed in Seasons.
- Smoke now fades away in fog.
- Smoke plume lifetime is no longer shortened at high speed. Because the smoke
  now fades well, this mechanism is no longer necessary.

## v1.0.0

### Added
- User profiles: These can be created in-game and are saved to user settings
- Mod profiles: Custom vehicle mods can install a profile file to instruct
  TurboTurbo how to apply its effects. Mod loading order is honoured when loading
  profiles (earliest mod to load takes precedence)
- A new profile editor provides an intuitive way to create and edit profiles

### Changed
- Smoke fade-out is no longer hardcoded and now depends on how fast a particle
  grows, so the plume naturally thins as it widens
- Increased the lifespan and maximum size of long, thin smoke plumes
- Smoke now grows quickly just after leaving the exhaust pipe, and more slowly
  afterwards
- Smoke fades away when close to the camera, to prevent it from clipping
  through the cab when inside
- Removed harsh edges on smoke that intersects other surfaces
- Shimmer now draws before the smoke; this gets rid of some glitchy patterns that
  used to show up near smoke edges
- Soot gains a slight ease-in/out over time, so that it can no longer appear
  or disappear instantly after fast throttle changes
- Improved clean smoke opacity calculations near engine idle point
- All loco profiles have been further finetuned to make the effects look better
- Minor tweaks to the combustion simulation model
- Now logs an error if assets could not be loaded

### Fixed
- Fixed an invalid lookup resulting in a log error on some vehicle despawns

## v0.2.2

Fixed info.json encoding issue causing UnityModManager read failures.

## v0.2.1

Fixed incorrect version number causing mod manager installation failures.

## v0.2.0

Adds a naturally aspirated simulation model, and adds profiles for the remaining diesels.

### Added
- DH4 profile (turbocharged)
- DM3, DE2, D1MU profiles (naturally aspirated)

### Changed
- Reduced ambient light tint intensity on smoke at night/evening
- Reduced shadow intensity on light-coloured smoke
- Improved smoke and shimmer shader performance
- Minor graphical adjustments to improve effects appearance

### Fixed
- Smoke no longer detaches from the exhaust at high speed and low frame rate

### Added support for the FPD-4, BLW DRS, and WDM-2

## v0.1.0

First release. Turbocharger simulation that drives dynamic smoke effects.
