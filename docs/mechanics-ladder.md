# Lot Loop — mechanics ladder

Ordered list of mechanics by level band. Each entry: intro level, the rule, how the tutorial teaches it, art needs, and the level-data hook. Status: **Live** = playable now; **Stub** = parsed from `levels.json` (`LevelHooks`, `Assets/_Project/Scripts/Puzzle/LevelHooks.cs`) and ignored by the board, solver and tests until implemented.

Teaching pattern for every new mechanic (Flock Five coach style): a one-line blurb on the level, a glove pointing at the one legal tap, everything else dimmed until the first correct tap, then free play. A new mechanic gets its own easy level with almost no other pressure, then one mixing level before the next mechanic.

| # | Band | Mechanic | Status |
|---|---|---|---|
| 1 | 1–3 | Clear lane + match three | Live |
| 2 | 2 | Shared lanes (colors mixed in a lane) | Live |
| 3 | 4 | `?` covered cars | Live |
| 4 | 5–6 | Tunnel / garage spawner (queue) | Live |
| 5 | 7–8 | Side lots | Live |
| 6 | 13–16 | Conveyor delivery | Stub |
| 7 | 17–20 | Barriers that open after N moves | Stub |
| 8 | 21–25 | Locked gates + keys | Stub |
| 9 | 26–30 | Car-park elevator | Stub |
| 10 | 31–35 | Stacked / layered cars | Stub |
| 11 | 36–40 | Tow-truck drop | Stub |
| 12 | 41+ | Mixes; tunnels showing only the front car at depth | — |

## Delivery: ways new cars enter the field

### 4. Tunnel / garage spawner — Live (intro level 5 "Tunnel Mouth")
- **Rule:** a garage cell holds a queue. Tapping it (lane clear) sends out the front car; the next car rolls up. The count badge shows how many are left. Cars behind the garage in its lane wait until it is empty. Only the front car is visible (visibility lock).
- **Tutorial:** glove on the garage, blurb "Empty the tunnel before the cars behind it can leave." The first tap shows the next car rolling forward.
- **Art:** garage mouth (exists, code-drawn; to repaint in the 3D style with a roll-up door), count badge, a roll-forward animation.
- **Data:** cell string `"tunnel:coral,teal,amber"` (front first; `?color` hides one).
- **Next step:** paint the garage door and animate the push-out; this was the first mechanic on the list, and it is already in the rules (`TunnelSpawn.cs`).

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

### 3. `?` covered cars — Live (intro 4 "Mystery Row")
- **Rule:** a covered car shows its color only once the cell in front of it is empty.
- **Tutorial:** a blurb; the reveal flips the cover off with a pop.
- **Art:** a cover sprite (exists, code-drawn; to repaint as a car under a tarp with a `?`) and a reveal flip.
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

## Rules for adding a mechanic
1. Add rules in `Assets/_Project/Scripts/Puzzle` (pure C#, no Unity types), then teach `LotSolve` and `RuleChecks`.
2. Add EditMode tests in `CarMatch.Tests` and a `tools/SolveCheck` pass so every level stays solvable.
3. Read its hook from `LevelDef.Hooks`, then move it from Stub to Live here.
