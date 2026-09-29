## Unreleased

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
- Slightly reduced max shimmer intensity on the DM3
- Slowed down DE6 turbo spool-down time
- Minor tweaks to the combustion simulation model
- Now logs an error if assets could not be loaded

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
