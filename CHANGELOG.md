## Unreleased

### Added
- User profiles: These can be created in-game and are saved to user settings
- Mod profiles: Custom vehicle mods can supply a profile alongside their files
  to instruct TurboTurbo how to apply its effects
- A new profile editor provides an intuitive way to create and edit profiles

### Changed
- Smoke now fades when close to the camera, to prevent it from clipping through the cab when inside
- Adjusted clean smoke opacity calculations to be more intuitive
- Slightly reduced max shimmer intensity on the DM3
- Minor tweaks to the combustion simulation model

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
