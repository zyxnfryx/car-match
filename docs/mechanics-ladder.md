# Lot Loop — mechanics ladder

Ordered list of mechanics by level band. Each entry: intro level, the rule, how the tutorial teaches it, art needs, and the level-data hook. Status: **Live** = playable now; **Stub** = parsed from `levels.json` (`LevelHooks`, `Assets/_Project/Scripts/Puzzle/LevelHooks.cs`) and ignored by the board, solver and tests until implemented.

Teaching pattern for every new mechanic (Flock Five coach style): a one-line blurb on the level, a glove pointing at the one legal tap, everything else dimmed until the first correct tap, then free play. A new mechanic gets its own easy level with almost no other pressure, then one mixing level before the next mechanic.

| # | Band (intro level) | Mechanic | Status |
|---|---|---|---|
| 1 | 1–9 | Early ramp, every lane filled: match three (1), three short lanes (2), third color (3), covered car (4), side lot (5), tunnel (6), fourth color (7), first real mix (8), deeper four-color mix (9). Cars leave only **forward** | Live |
| 2 | 10–15 | More lanes and colors; mixing grows. Level 10 is the first super-hard | Live |
| 3 | 16–25 (16) | Side lots (carports) | Live |
| 3b | 21–25 (21) | Underground garages: a hatch under two parked spots; when both are empty it opens and its queued cars drive up a ramp into them (as `?` cars until they can exit); badge shows how many are still below | Live (build 11) |
| 4 | 26–30 (26) | Mystery boxes: cardboard `?` boxes break when a neighboring car is cleared in a match | Live (build 9) |
| 4b | 31–35 (31) | `?` covered cars (tarps), 1 more every 10 levels | Live |
| 5 | 36–50 (36) | Tunnel / garage spawner (queue, front car only visible) | Live |
| 6 | 51–100 (51) | Everything together; every 10th level is a Super Hard spike (red badge, pays 100) | Live |
| 7 | 101+ | Conveyor delivery | Stub |
| 8 | 101+ | Barriers that open after N moves | Stub |
| 9 | 101+ | Locked gates + keys | Stub |
| 10 | 101+ | Car-park elevator | Stub |
| 11 | 101+ | Stacked / layered cars | Stub |
| 12 | 101+ | Tow-truck drop | Stub |

Levels 1–9 are a hand-set ramp: each lane in those levels is the same length, so the lot does not draw an empty bay. From level 10 the pack follows `tools/levels/gen_levels.py`. Every level is proven Winnable (`RuleChecks.AllLevelsSolvable` and `NoEmptyStartingCells`). Covered cars, side lots, and tunnels appear first in tiny form at levels 4–6; their fuller introductions stay at 31, 16, and 36. Stacks, gates, and barriers stay stubs in `LevelHooks` and are not used here. Blurbs are in the data and are no longer drawn under the level title.

## Delivery: ways new cars enter the field

### 3b. Underground garages — Live (intro level 21 "Underground")
- **Rule:** `levels.json` `"garages": [{"lane": L, "cells": [c, c+1], "cars": [...]}]`. When every listed cell is empty (`UndergroundGarage.SpotsClear`), the hatch opens and the next cars from the queue fill those cells in order, arriving hidden. `HiddenReveal.Sweep` runs `UndergroundGarage.Refill` after every change (tap, booster, load), so the board, the solver (`LotSolve`), and the tests all share one rule. Queued cars count toward the color totals and `IsClear`; the magnet only sees surface cars (`BoardQuery.SurfaceHistogram`).
- **Teach:** level 21 is a 3-lane lot with one 3-car garage under the middle lane's front two spots.
- **Art:** striped hatch with a yellow rim and a count badge; arrivals spiral up out of the hatch (`RampUp`).
- **Ladder:** `tools/levels/add_garages.py` adds one garage (one set) to levels 22–50 and two sets from 51. Mechanic intro levels 26, 31 and 36 are left as they are. Every level stays Winnable.

### 5. Tunnel / garage spawner — Live (intro level 36 "Tunnel Mouth")
- **Rule:** a garage cell holds a queue. Tapping it (lane clear) sends out the front car; the next car rolls up. The count badge shows how many are left. Cars behind the garage in its lane wait until it is empty. Only the front car is visible (visibility lock).
- **Tutorial:** glove on the garage, blurb "Empty the tunnel before the cars behind it can leave." The first tap shows the next car rolling forward.
- **Art:** 3D garage with a roll-up door (`UI/garage`), red count chip (`UI/chip`); still to do: a roll-forward animation.
- **Data:** cell string `"tunnel:coral,teal,amber"` (front first; `?color` hides one).
- **Status:** the first delivery mechanic, already in the rules (`TunnelSpawn.cs`), now with 3D garage art. Next step: animate the push-out.

### 6. Conveyor — Stub (intro ~13)
- **Rule:** a belt at the top of a lane pushes a new car into the back of the lane every `every` successful moves while it still has cars.
- **Tutorial:** glove on the belt's next car with a "+1 in 2 moves" chip; the counter ticks down with each move.
- **Art:** belt strip with rollers (tile and scroll), countdown chip, small arrival bump.
- **Data:** `"hooks": { "conveyor": [{ "lane": 0, "every": 2, "cars": ["coral","teal"] }] }`

### 9. Car-park elevator — Stub (intro ~26)
- **Rule:** an elevator at a lane's back holds floors. When the lane in front of it is empty, the next floor comes down and puts its cars into the lane.
- **Tutorial:** clear the lane, glove on the elevator, the doors open and the cars roll out.
- **Art:** an elevator shaft with a floor indicator (1/3), door open/close frames, a light.
- **Data:** `"elevator": [{ "lane": 3, "floors": [["amber"], ["lime","lime"]] }]`

### 11. Tow-truck drop — Stub (intro ~36)
- **Rule:** after `after` moves, a tow truck drives in and drops a car at (`lane`, `at`) if that cell is empty; otherwise it waits.
- **Tutorial:** a warning chip counts down on the drop cell, then the truck animates in.
- **Art:** a tow truck in the same 3D style (in 8 headings for its path), a hook/drop animation, a dashed target-cell marker.
- **Data:** `"tow": [{ "after": 6, "lane": 1, "at": 2, "car": "violet" }]`

## Visibility locks: ways the field hides information

### 4. Mystery boxes — Live (intro level 26 "Mystery Boxes")
Level token `box:<color>`. A box fills a lane cell, blocks the lane and can't be tapped. When a match clears in the bar, any box in a cell next to (same lane ±1, or neighboring lane same cell) where one of the three cleared cars was parked breaks: a glowing orb flies from the bar, the box shatters with a tear, and the car inside is revealed. Rule: `MysteryBox.BreakNear` (bar origins kept in `Board.BarFrom`), handled by `LotSolve` via `BoardRules.TryTap`; test `MysteryBoxes_BreakNextToAMatch`. 1 box at 26, +1 every 12 levels (max 5).

### 4b. `?` covered cars — Live (intro level 31 "Mystery Row")
- **Rule:** a covered car shows its color only once the cell in front of it is empty.
- **Tutorial:** a blurb; the reveal flips the cover off with a pop.
- **Art:** a car under a tarp with a chunky `?` (`UI/tarp`); still to do: a reveal flip.
- **Data:** `"?amber"` cell prefix.

### 4b. Tunnels show only the front car — Live (part of #4)

### 10. Stacked / layered cars — Stub (intro ~31)
- **Rule:** a cell holds a car on top of others (a two-level car carrier or a lift). Tapping the top car exposes the next one in the same cell. Only the top car's color is known; lower ones show as silhouettes.
- **Tutorial:** glove on the top car; the lower car lifts up into view.
- **Art:** a lift platform and a silhouette tint for lower cars, plus a "×2" pip.
- **Data:** `"stacks": [{ "lane": 2, "at": 1, "under": ["sky","sky"] }]`

### 8. Locked gates + keys — Stub (intro ~21)
- **Rule:** a gate blocks a lane cell. A key car of matching color (a car with a key icon) unlocks it when it reaches the bar.
- **Tutorial:** glove on the key car, then the gate swings open.
- **Art:** a boom gate with a colored padlock, a key badge on the car, an unlock sparkle.
- **Data:** `"gates": [{ "lane": 1, "at": 0, "key": "gold" }]`

### 7. Barriers that open after N moves — Stub (intro ~17)
- **Rule:** a striped barrier blocks the lane mouth and lifts after `opensAfter` moves.
- **Tutorial:** a counter chip on the barrier; the glove points at other lanes first.
- **Art:** a striped barrier arm, number chip, lift animation.
- **Data:** `"barriers": [{ "lane": 4, "at": 0, "opensAfter": 5 }]`

## Build 11 retune (rear exits)
Rear exits made mid/late levels easier. `tools/levels/retune_backexit.py` re-rolled every level from 25 up whose casual-player win rate (`SolveCheck --rate`: 400 greedy playthroughs) rose more than 10 points above its pre-build-11 rate (`tools/levels/rate_before_b11.txt`). It added sets, mixing and `?` cars until the rate was back near its old value. `?` cars and boxes never sit at either end of a lane. Levels 1–24 are unchanged (levels 1–5 still win 100%). All 100 are still Winnable.

## Front-only exits
The back of every lane is a wall again. `ExitPath.LaneOpen` is front-clear only, side lots leave only through `FrontClear`, and the drive never pulls out onto the ring road. A painted curb closes the top of each lane. Levels the wall made unsolvable are reordered within their lanes so the pack stays solvable.

## Rules for adding a mechanic
1. Add rules in `Assets/_Project/Scripts/Puzzle` (pure C#, no Unity types), then teach `LotSolve` and `RuleChecks`.
2. Add EditMode tests in `CarMatch.Tests` and a `tools/SolveCheck` pass so every level stays solvable.
3. Read its hook from `LevelDef.Hooks`, then move it from Stub to Live here.


### Intersections (H-shaped / crossing lanes) — Not yet
Seen in Car Match later levels: horizontal traffic must clear before vertical lanes open. Needs a 2D grid exit model; not in build 9.
