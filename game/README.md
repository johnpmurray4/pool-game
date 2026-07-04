# game/ — Godot project

The Godot 4 (.NET) project lives here: scenes, rendering, input, UI, and audio.
It references `core/PoolGame.Core` for all simulation — no physics or rules
logic belongs in this directory.

## Setup (requires the Godot editor — not possible in a headless CI/agent session)

1. Install **Godot 4.4+ .NET edition** and the .NET 8 SDK.
2. Create the project in this directory (`project.godot` at `game/`).
3. Add a project reference to the core library in `game/PoolGame.Game.csproj`:
   ```xml
   <ItemGroup>
     <ProjectReference Include="../core/PoolGame.Core/PoolGame.Core.csproj" />
   </ItemGroup>
   ```
4. Android export: install Android Build Template via
   *Project → Install Android Build Template*, set up the Android SDK path and
   a debug keystore in *Editor Settings → Export → Android*, then add an
   Android export preset. (`export_presets.cfg` is gitignored because it can
   contain keystore paths; commit a sanitized `export_presets.example.cfg`.)

## Phase 0 checklist

- [ ] Godot .NET project created, empty main scene runs on desktop
- [ ] Core library referenced and callable from a Godot script
- [ ] Android export produces an APK that runs on a real device
- [ ] **Benchmark scene**: run `Rack.BlackballBreak` + a canned break shot ×50
      on-device and display ms/shot — this number calibrates the AI's
      per-decision time budget (desktop reference: ~14 ms/break)
