# Lot Loop

ZFX Gaming's iPhone portrait car-matching puzzle. Cars leave a lot only when the lane in front of them is clear, drive into a 7-slot bar, and three of a color clear. Fill the bar and the lot fails. Clear the lot and it is won.

The player-facing name is **Lot Loop**. The repo is `car-match`. Art, the mascot, and the street are original placeholders drawn in code. Nothing is taken from Car Match: Traffic Puzzle.

**Unity 6.3 LTS** — editor `6000.3.22f1`. URP. Input System only. Bundle `com.zfxgames.carmatch`. Company `zfxgames`. Portrait 1080×1920.

## Play

1. Open this folder in Unity Hub with **6000.3.22f1**.
2. Open `Assets/_Project/Scenes/Boot.unity` if it is not already the open scene.
3. Press Play, or **Car Match → Play** (that menu also pins the Game view to 1080×1920).
4. The home screen builds itself at runtime. No baked board is required.

Tap a car, tunnel mouth, or side-lot car that has a clear path to the bottom. It enters the bar. Three of a color leave. The gold ring marks what you can tap. A `?` cover flips when the cell in front of it empties. A tunnel shows how many cars are still inside. A side lot stays shut until its lane cell and every cell closer to the exit are empty.

Boosters, paid from the same wallet the home screen shows:

| Booster | Cost | What it does |
| --- | --- | --- |
| Undo | 300 | Puts the last car back |
| Shuffle | 300 | Shuffles colors on the lot. Slots and `?` covers stay |
| Super | 900 | Puts up to three cars back |
| Magnet | 600 | Pulls one set of three off the lot. They do not fill the bar |

Wins and fails both pay **20** coins. Starting a lot is free. **Retry** on the fail screen spends a life and pays the 20. **Continue** plays the rewarded-ad stub and puts the last three cars back, with no life spent and no coin pay. There is one fail screen. Lives regenerate every 20 minutes, up to 5.

Twelve lots live in `Assets/_Project/Resources/levels.json`. Beating one opens the next. The mascot bounce on the win screen is a stand-in; the celebration itself is stubbed.

## Tests

EditMode tests are `CarMatch.Tests` under `Assets/_Project/Scripts/Editor/Tests`. Unity 6.3 ships Test Framework as a core package, so it is not pinned in `Packages/manifest.json`. In the Editor: **Window → General → Test Runner → EditMode → Run All**.

The same checks run without Unity:

```bash
dotnet run --project tools/SolveCheck -c Release
```

`Car Match → Solve Levels` runs them inside the editor and prints the result.

## Layout

- `Assets/_Project/Scripts/Puzzle/` — `CarMatch.Core`. No `UnityEngine` references. Rules, levels, boosters, solver.
- `Assets/_Project/Scripts/App/` — bootstrap, purse, ad seam, procedural art, screens.
- `Assets/_Project/Scripts/Editor/` — **Car Match** menu. Pins portrait, bundle id, and Input System only.
- `Assets/_Project/Resources/levels.json` — the hand-authored lots.
- `Assets/Settings/` — URP asset, same pipeline setup as flock-five.

`CarMatchApp` boots from `[RuntimeInitializeOnLoadMethod]` after the boot scene loads.

## Shared pieces

One helper per behavior. The screen, the solver, and the tests call these. They do not each keep a copy.

| Helper | Used by |
| --- | --- |
| `MatchClear` | Every tap. Inserts into the 7-slot bar and clears a set of three |
| `ExitPath` | Input, the gold-ring highlight, and the solver |
| `HiddenReveal` | Level load, every removal, and shuffle |
| `TunnelSpawn` | Taps and the magnet, including the painted count |
| `SideLotGate` | Input, highlight, reveal, and the solver |
| `BoardRules.Play` | The only tap mutation. Snapshots for undo live here |
| `BoosterKit` | Undo, super undo, shuffle, magnet, and the ad-continue |
| `LotSolve` | Solvability tests and **Car Match → Solve Levels** |
| `Economy` | `Purse` persists it. Tests check pay, prices, and regen |
| `LevelJson` | The player and the tests read the same JSON |
| `RuleChecks` | EditMode tests and `tools/SolveCheck` |

`IAdService` is the ad seam. `StubAdService` waits a beat and grants the reward. No ad SDK is wired up.
