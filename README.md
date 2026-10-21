# Lot Loop

ZFX Gaming's iPhone portrait car-matching puzzle. Cars leave a lot only when the lane in front of them is clear, drive into a 7-slot bar, and three of a color clear. Fill the bar and the lot fails. Clear the lot and it is won.

The player-facing name is **Lot Loop**. The repo is `car-match`. The cars and the mascot are original 3D-style paintings. Nothing is taken from Car Match: Traffic Puzzle.

**Unity 6.3 LTS** — editor `6000.3.22f1`. URP. Input System only. Bundle `com.zfxgames.carmatch`. Company `zfxgames`. Portrait 1080×1920.

## Play

1. Open this folder in Unity Hub with **6000.3.22f1**.
2. Open `Assets/_Project/Scenes/Boot.unity` if it is not already the open scene.
3. Press Play, or **Car Match → Play** (that menu also pins the Game view to 1080×1920).
4. The home screen builds itself at runtime. No baked board is required.

Tap a car, tunnel mouth, or side-lot car that has a clear path to the bottom. It drives along that path off its spot and into the next open slot of the bar (about 0.4s, easing). Input stays locked while it is moving. Three of a color leave only after it arrives. The gold ring marks what you can tap. Open eyes mean that same clear exit. Closed eyes mean the car is blocked. A `?` cover still hides the car until the cell in front of it empties; the eyes show up on the redraw after that reveal. A tunnel shows how many cars are still inside. A side lot stays shut until its lane cell and every cell closer to the exit are empty.

Every car is asked again after a move, undo, shuffle, magnet, or reveal. The painted cars live in `Assets/_Project/Art/Cars/`. Idle poses face south: `car_<paint>_open.png` and `car_<paint>_closed.png`. Closed eyes are only drawn facing south, which is enough because a blocked car does not move. A drive does not rotate these sprites (they already show the face, and each one has a shadow). It swaps in `car_<paint>_open_<dir>.png` for the nearest of `N`, `NE`, `E`, `SE`, `S`, `SW`, `W`, and `NW`, and leaves the transform unrotated. The six paintings cover the eight game colors: coral and rose use red, amber and gold use yellow, lime uses green, teal uses teal, sky uses blue, and violet uses purple. A missing file falls back to the code-drawn car, which does rotate to face the way it is moving. The drive is view-only: `BoardRules.Play` still applies the move in one step, so the solver and the edit-mode tests do not wait on the animation.

Boosters, paid from the same wallet the home screen shows:

| Booster | Cost | What it does |
| --- | --- | --- |
| Undo | 300 | Puts the last car back |
| Shuffle | 300 | Shuffles colors on the lot. Slots and `?` covers stay |
| Super | 900 | Puts up to three cars back |
| Magnet | 600 | Pulls one set of three off the lot. They do not fill the bar |

Wins and fails both pay **20** coins. Starting a lot is free. **Retry** on the fail screen spends a life and pays the 20. **Continue** plays the rewarded-ad stub and puts the last three cars back, with no life spent and no coin pay. There is one fail screen. Lives regenerate every 20 minutes, up to 5.

Twelve lots live in `Assets/_Project/Resources/levels.json`. Beating one opens the next. The home screen shows `mascot_idle`. Coming home after a clear plays `mascot_happy`, then `mascot_cheer` with a small bounce. Those paintings are in `Assets/_Project/Art/Mascot/`.

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
| `EyeState` | The board view and the tests. Open means `ExitPath` says that tap is legal. It names the idle sprites and the eight `car_<color>_open_<dir>.png` headings, and picks the nearest heading from a movement vector |
| `HiddenReveal` | Level load, every removal, and shuffle |
| `TunnelSpawn` | Taps and the magnet, including the painted count |
| `SideLotGate` | Input, highlight, reveal, and the solver |
| `BoardRules.Play` | The only tap mutation. Snapshots for undo live here |
| `BoosterKit` | Undo, super undo, shuffle, magnet, and the ad-continue |
| `LotSolve` | Solvability tests and **Car Match → Solve Levels** |
| `Economy` | `Purse` persists it. Tests check pay, prices, and regen |
| `LevelJson` | The player and the tests read the same JSON |
| `RuleChecks` | EditMode tests and `tools/SolveCheck` |

`IAdService` is the ad seam. `AdServices.Create()` is the only place that picks one: `LevelPlayAdService` on an iOS/Android device once real keys are set, otherwise `StubAdService` (editor, desktop, placeholder keys).

## Rewarded ads (Unity LevelPlay)

This uses the same flow as ZFX Casino / More More More Keno (`www/core/ads.js` + `LevelPlayRewarded.swift`), built on the Unity package `com.unity.services.levelplay` **9.5.1** (wraps LevelPlay SDK 9.6.0) plus `com.unity.ads.ios-support` 1.2.0 for ATT. It's rewarded only, like keno: no banner, no interstitial.

- Flow: ATT prompt before the first request (iOS) → test suite + `ValidateIntegration` when `TestMode` is on → `LevelPlay.Init(appKey)` → one reusable `LevelPlayRewardedAd`. The reward comes only from `OnAdRewarded`, with a 0.5 s grace after close. No fill, an early close, or a failed show grants nothing and goes back to the fail screen. The next ad preloads after each close. If init fails, it retries on the next request.
- Keys live in one asset: `Assets/_Project/Resources/AdConfig.asset`. They hold the Lot Loop iOS and Android keys. **Do not reuse the ZFX Casino keys.**
- iOS: `Editor/IosAdPlist.cs` adds `NSUserTrackingUsageDescription`, keno's 77 Unity/LevelPlay `SKAdNetworkItems`, `ITSAppUsesNonExemptEncryption=NO`, and `-ObjC`. Minimum iOS is 13; build with Xcode 26+.
- Android: the bundle is `com.zfxgames.carmatch`, IL2CPP, ARM64+ARMv7, min API 25. The LevelPlay package merges its own manifest (INTERNET, AD_ID, activities) and resolves Gradle deps through the External Dependency Manager it bundles. Run **Assets → External Dependency Manager → Android Resolver → Force Resolve** with Custom Main Gradle Template / Gradle Properties Template enabled (Player Settings → Publishing Settings), as the LevelPlay docs say.

### Brandon: LevelPlay dashboard TODO
1. ✅ Done: LevelPlay → Apps → add **Lot Loop iOS** (bundle `com.zfxgames.carmatch`) and **Lot Loop Android** (package `com.zfxgames.carmatch`). Copy each App Key.
2. ✅ Done: Setup → Ad Units: make one **Rewarded** unit per app (for example "continue"). Copy the ad unit IDs.
3. ✅ Done: all four values are in `AdConfig.asset`.
4. Turn on test mode / register test devices. Before store builds, set `TestMode` to false.
5. Adapters: open **Ads Mediation → Network Manager** in Unity, then install the Unity Ads adapter (and any other networks you turn on in the dashboard). If you add networks, add their SKAdNetwork IDs to `IosAdPlist.cs`.
6. App Store Connect / Play Console: declare ads and advertising ID / data use.

