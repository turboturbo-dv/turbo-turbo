# TurboTurbo

An engine effects mod for Derail Valley.

TurboTurbo extends the diesel engine simulation in Derail Valley to drive
realistic exhaust smoke effects using custom shaders.

## Installation instructions

To install, download the ZIP file and extract to the Mods directory.
If you use modded locomotives, also consider installing the
[Community Profiles](https://github.com/turboturbo-dv/turbo-turbo-contrib).

## Development

Requires the .NET SDK and Unity 2019.4 (for the asset bundle). Run `build.ps1`
once to detect the game directory (or set `GameDir` yourself); the projects
import the generated `GameDir.props`.

- **The mod:** `dotnet build TurboTurbo/TurboTurbo.csproj` builds the
  mod and deploys it by default
  (you can set `DeployMod` to `false` to prevent this)
- **Asset bundle:** `WorkBench/` is a Unity 2019.4 project that
  builds the `turboturbo_assets` bundle. Shader changes require rebuilding it
  from the editor via the `TurboTurbo/Build and Deploy` menu
- **Packaging:** `build.ps1` creates a release package and writes it to `./out`

### Release steps

Manual for now.

1. Update the changelog
2. Update `repository.json` with the new version
3. Commit as `Release vx.x.x`
4. Run `build.ps1`
5. Create GH release and upload the file
6. Upload the file to Nexus
