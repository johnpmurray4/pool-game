# English Pool

A 3D English pool (UK 8-ball / blackball) game for Android. WPA Blackball
rules, 7ft table, reds vs yellows.

See [PLAN.md](PLAN.md) for the full build plan.

## Layout

| Directory | Contents |
|---|---|
| [`core/`](core/) | Engine-agnostic C# simulation: physics, rules, AI — pure .NET 8, fully unit-tested, no Godot dependency |
| [`game/`](game/) | Godot 4 (.NET) project: rendering, input, UI, audio |
| [`tools/`](tools/) | Debug visualizer and physics tuning harness (dev-only) |

## Development

```sh
dotnet test core/PoolGame.sln    # run the simulation test suite
```

The physics core is a deterministic, event-based simulation (no fixed
timesteps): within each motion regime ball paths are quadratic in time, so
collision/cushion/pocket times are found by polynomial root isolation. The
same headless sim powers gameplay, the AI's shot search, and replays.
