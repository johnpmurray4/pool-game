# English Pool — Android Game Build Plan

A 3D English pool (UK 8-ball / blackball) game for Android, inspired by Pool Break 3D
but focused on a single discipline done well: reds vs yellows on a 7ft UK table.

---

## 1. Scope

### What we're building (v1)
- **English pool only** — WPA Blackball rules (with an option for EPA "World Rules" later,
  since UK players are split between the two rule sets).
- 7ft UK table: 2" balls, 7 reds, 7 yellows, black (8-ball), cue ball; rounded pocket
  jaws and napped-cloth feel (heavier roll-off than American pool).
- **Game modes at launch:**
  1. Single player vs AI (3–4 difficulty levels)
  2. Local two-player (pass-and-play on one device)
  3. Practice / free table
- 3D rendering with touch controls: drag to aim, pull-back-and-release to strike,
  spin control (english/screw/follow) via a cue-ball contact-point widget, pinch to zoom,
  orbit camera + overhead view toggle.

### Explicitly out of scope for v1 (candidates for v2)
- Online multiplayer
- Other disciplines (snooker, carrom, US 8/9-ball)
- Tournaments/career mode, cosmetics store

Cutting scope to one ruleset and offline play is what makes this achievable — online
multiplayer roughly doubles the project.

---

## 2. Tech stack

**Recommendation: Godot 4 (GDScript/C#) with a custom billiards physics layer.**

| Option | Pros | Cons |
|---|---|---|
| **Godot 4** ✅ | Free/MIT, small APKs, great Android export, easy 3D scene work | Smaller ecosystem than Unity |
| Unity | Huge ecosystem, asset store | Licensing/runtime fee history, heavier, splash screen on free tier |
| Kotlin + libGDX | Full control, JVM-native | You build everything (scene graph, tooling) yourself |
| Flutter + Flame | Nice UI tooling | Weak fit for 3D games |

The engine choice matters less than people expect because **we write the physics
ourselves either way.** General rigid-body engines (Bullet/PhysX/Jolt) don't model
billiards well — cue-ball spin, sliding→rolling transitions, cushion throw, and napped
cloth need a purpose-built simulation. The engine is used for rendering, audio, input,
and UI; the physics is our own deterministic module.

---

## 3. Architecture

Keep the simulation pure and engine-agnostic — this is the single most important
structural decision:

```
┌────────────────────────────────────────────┐
│                  Godot layer               │
│  Rendering · Input · Audio · UI · Menus    │
└───────────────┬────────────────────────────┘
                │ shot parameters in,
                │ event stream out
┌───────────────▼────────────────────────────┐
│         Core simulation (pure code)        │
│  ┌──────────────┐   ┌───────────────────┐  │
│  │ Physics      │   │ Rules engine      │  │
│  │ (continuous  │──▶│ (blackball state  │  │
│  │  event-based │   │  machine: fouls,  │  │
│  │  simulation) │   │  turns, win/loss) │  │
│  └──────────────┘   └───────────────────┘  │
│  Deterministic, fixed-seed, no rendering   │
└───────────────┬────────────────────────────┘
                │ same API
┌───────────────▼────────────────────────────┐
│  AI opponent (samples shots, runs physics  │
│  headlessly, scores outcomes)              │
└────────────────────────────────────────────┘
```

Why this shape:
- **Deterministic replayable physics** → AI can simulate candidate shots using the exact
  same code the game uses; replays and shot previews come for free; unit tests are trivial.
- **Rules engine as a state machine** consuming physics events (`BallPotted`, `CushionHit`,
  `FirstContact`, `BallOffTable`) → rules are testable without graphics, and swapping
  WPA Blackball ↔ EPA World Rules later is a strategy swap, not a rewrite.

### Module breakdown
| Module | Responsibility |
|---|---|
| `core/physics` | Ball/cushion/pocket simulation, event-based time stepping |
| `core/rules` | Blackball state machine: open table, groups, fouls, free shot/two visits, win conditions |
| `core/ai` | Shot generation + evaluation via headless simulation |
| `game/` | Godot scenes: table, balls, cue, camera rig |
| `game/input` | Aim, power, spin widget, camera gestures |
| `game/ui` | HUD (turn indicator, group colors, foul messages), menus, settings |
| `game/audio` | Ball clacks (velocity-scaled), cushion thuds, pocket drops, ambience |
| `persistence` | Settings, stats, save-in-progress game (JSON/SQLite) |

---

## 4. Physics — the heart of the game

Event-based continuous simulation (not naive fixed-timestep collision checking):
solve for the *time* of the next event analytically, advance all balls to that moment,
resolve, repeat. This is how serious billiards sims work and it's both faster and more
accurate on mobile.

Ball states and transitions to model:
- **Stationary → sliding → rolling → stationary** (different friction regimes;
  sliding friction ≫ rolling resistance)
- **Spin**: side (english), top (follow), back (screw/draw); spin decay over time
- **Ball–ball collision**: near-elastic with throw (spin transfer / collision-induced throw)
- **Ball–cushion**: restitution + spin interaction (side spin changes rebound angle)
- **Pockets**: geometric capture with jaw rattling — UK rounded pockets reject balls
  hit too fast at an angle, which is a signature part of the English pool feel
- **Optional polish**: cue elevation → swerve/massé; napped cloth directional drift

Reference: Han (2005) "Dynamics in carom and three cushion billiards" and the
math used by projects like *pooltool* (open-source Python billiards sim) — we
reimplement, not port, but the equations are well documented.

**Tuning matters more than equations.** Budget real time for slow-motion comparison
against real UK pool footage. Physics parameters (friction coefficients, restitution,
cushion response) live in a data file, not code, so tuning doesn't require rebuilds.

---

## 5. Rules engine (WPA Blackball)

State machine covering:
- Break rules (what constitutes a fair break, potting on the break, open table)
- Group assignment (reds/yellows decided by first legal pot after the break)
- Legal shot definition: hit own group first, then any ball pots or any ball reaches a cushion
- Fouls: wrong ball first, no cushion after contact, potting opponent's ball, cue ball
  potted, ball off table, playing out of turn, touching balls
- Foul consequence: opponent gets **one visit with a free shot** (may play any ball)
  — this is the big behavioral difference from US 8-ball's ball-in-hand
- Black-ball scenarios: win by legally potting black, **lose** by potting black early,
  potting black with a foul, or potting black and cue ball together
- Stalemate/re-rack conditions

Every rule gets a unit test driven by scripted physics event sequences.

---

## 6. AI opponent

Simulation-based (no ML needed):
1. Enumerate candidate shots: for each legal object ball × each pocket, compute the
   ghost-ball aim point; add safety shots (thin contacts, distance plays).
2. Run each candidate through the headless physics sim with small parameter jitter.
3. Score outcomes: pot success, cue-ball position for the next shot, risk of fouls,
   safety value if no pot is on.
4. Difficulty = execution noise + search depth: easy AI adds aim/power error and only
   looks 1 shot ahead; hard AI has low noise and evaluates position play.

This is only feasible because the physics core is deterministic and headless — one more
payoff of the architecture in §3.

---

## 7. Rendering, controls, feel

- **Table/balls**: modest poly counts, PBR-lite materials; balls are the hero asset —
  good specular highlights and shadows sell the whole scene. Baked lighting on the table.
- **Camera**: orbit around cue ball for aiming, smooth follow during shots,
  tap-to-toggle overhead tactical view.
- **Aiming aids**: guideline showing cue-ball path + object-ball projected line
  (length/accuracy of the guide scales down with difficulty setting).
- **Controls** (mirroring what works in Pool Break / 8 Ball Pool):
  - Drag anywhere to rotate aim (fine adjustment slider for precision)
  - Power: pull-back gesture on the cue or a power slider
  - Spin: tap the cue-ball icon → set contact point on a large overlay
- **Performance target**: 60fps on mid-range devices (~Snapdragon 6-series), minimal
  GC pressure during shots (preallocate everything in the sim loop).

---

## 8. Milestones

| Phase | Deliverable | Est. |
|---|---|---|
| **0. Skeleton** | Godot project, Android export pipeline working on a real device, CI building APKs | 1 wk |
| **1. Physics core** | Headless sim: ball motion, collisions, cushions, pockets, spin; unit tests + a desktop debug visualizer | 3–4 wks |
| **2. Playable table** | 3D table + balls rendered, touch aiming/power/spin, you can pot balls (no rules) | 2–3 wks |
| **3. Rules** | Full blackball state machine, two-player pass-and-play is a complete legal game | 2 wks |
| **4. AI** | Simulation-based AI with difficulty levels | 2–3 wks |
| **5. Polish** | Audio, menus, settings, stats, aiming-aid options, physics tuning pass, juice (ball trails, pocket animations) | 3 wks |
| **6. Release prep** | Device-matrix testing, performance profiling, Play Store listing, closed beta | 2 wks |

~3–4 months part-time for a competent solo dev. Physics (phase 1) is the highest-risk
item, which is why it comes first and stands alone — if the sim feels good in the debug
visualizer, everything downstream is derisked.

### Suggested repo layout
```
pool-game/
├── core/            # engine-agnostic sim (physics, rules, ai) + tests
├── game/            # Godot project (scenes, scripts, assets)
├── tools/           # debug visualizer, physics tuning harness
├── docs/            # rules reference, physics notes
└── .github/         # CI: run core tests, build debug APK
```

---

## 9. Testing & CI

- **Physics**: golden tests (known shot in → exact ball positions out, thanks to
  determinism), conservation checks, regression suite of tricky shots (thin cuts,
  rail-first, double kisses).
- **Rules**: exhaustive unit tests per foul/win/loss scenario.
- **CI (GitHub Actions)**: core test suite on every push; debug APK artifact per commit;
  release builds via signed AAB.
- **Playtesting**: the physics-feel loop needs humans — get builds into hands early
  via Play Store internal testing track.

---

## 10. Distribution

- Google Play, minSdk ~26 (Android 8+), target latest SDK as Play requires.
- v1 free, no ads/IAP complexity — decide monetization (cosmetic cues/tables, ad-supported
  with remove-ads IAP) after there's a fun game.
- v2 candidates: online multiplayer (adds server + matchmaking — significant),
  EPA World Rules toggle, tournaments, replays sharing.

---

## Immediate next steps

1. Phase 0: create the Godot project + Android export, commit the skeleton.
2. Start `core/physics` with ball motion + ball-ball collision and a test suite.
3. Build the desktop debug visualizer early — it's the main tool for the whole project.
