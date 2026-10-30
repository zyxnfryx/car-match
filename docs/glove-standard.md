# ZFX tutorial glove standard (from Flock Five) — port spec for Lot Loop

Sources read 2026-10-29 (all quotes/numbers below are from these files; nothing invented):
- Repo: zyxnfryx/flock-five, local checkout /workspace/flock-five.
  - Checked-out HEAD b6d7dde (2026-09-25) is STALE: it has no glove code.
  - Glove code read from local branch `pr217` @ 977d43e (2026-10-29 15:16 PT), which contains
    4a798cb "Register Build126TapCueTests…", 324d3e1 "b125: shared glove travel tunable", 81223ec "Merge PR #188: glove-closer-start".
  - Caveat: pr217 is NOT an ancestor of origin/main (f36d2e3) — confirm with Creator which branch is the shipped one before porting.
- /workspace/flockfive_tapcue (mockup frames: idle/, tap/, layers/quill_*.png, particles/feather_*.png, *_preview.gif) — this is a quill/feather tap-cue mockup, NOT the glove.
- /workspace/inspector-tutorial/CHECKLIST.md

## 1. Assets — there is NO glove sprite/texture
- The glove is **procedurally rasterized at runtime** in C#: `CoachGloveTex()` / `CoachGloveCurl(dip)` / `RasterCoachGlove` / `FinishGloveTex`
  in `Assets/_Project/Scripts/App/FlockFiveApp.Coach.cs` (pr217, ~L7893–8110).
  - Texture: `GloveW = 220`, `GloveH = 300` px, `TextureFormat.RGBA32`, no mips, `FilterMode.Bilinear`, `TextureWrapMode.Clamp`, `HideFlags.HideAndDontSave`, named "CoachGlove"/"CoachGloveCurl".
  - `GloveCurlSteps = 4` curl poses (index-finger bow lerps `GloveIndexBowRest = -14` → `GloveIndexBowPress = -20`). Pose picked by dip 0..1.
  - Fingertip pixel set by `GloveTip(98f, 116f, 88f, 236f, 13f, 11f, GloveW, GloveH)` → `_gloveTipU/_gloveTipV`; draw rect is anchored so the tip pixel sits on the pivot.
  - Shape table: `CoachCap(...)` array just above `CoachGloveTex()` (~L7930).
  - Splash warm-up rasters it a band of rows per frame (`GloveWarmRows`, `GloveWarmDone`) so the first lesson doesn't hitch.
  - Fallback aspect if texture missing: `dh * (220f/300f)`.
- So there are no .meta import settings to copy. (The `Assets/_Project/Art/Resources/Sprites/fx_hand_*.png` files are poker-hand FX loaded by SpriteCatalog.cs at 200 PPU, filterMode 1 — unrelated to the tutorial glove.)
- Separate small helper: `Assets/_Project/Scripts/App/TapCue.cs` (b126) — tiny fingertip on the caption box corner for tap-to-continue (AnyTap) steps only; excluded on glove/required steps.
- Look-at ARROW (not glove) for "look at this" steps: `CoachArrowAt(Rect target, float s)` (Coach.cs ~L1904; commit 8bbe7bf "solid flat-top/pointed-bottom marker with outline, eased ~1.2s hover").

## 2. Code paths (pr217, Assets/_Project/Scripts/App/)
- `FlockFiveApp.Coach.cs`
  - Size: `GlovePx = 94.4f` (L89, "about 20% under the old 118px"); `GloveDh(s) = GlovePx * s`; `GloveDrawW(dh)` from tex aspect.
  - Target: `GloveTarget(Rect r) => r.center` (fingertip lands on control centre); `GloveTargetAt(r,u,v)` for off-centre art (e.g. `PokerGloveU = 0.56`, `PokerGloveV = 0.80`).
  - Pose/seat: `NaturalGlovePose`, `GloveLockPose` (L1733), `GloveBeginReappear` (L1850), `SeatGlove` (L2741), `ClearGloveOfCaption` (L2372), `GloveSpan`, `DrawnGlovePose` (L4693), `GloveApproachDeg` (0 = finger up, 90 = right, -90 = left).
  - Travel: `GloveEntryScale = 0.67`, `GloveTravelScale = 0.5` (b125: every start keeps half its old distance, never >1), `GloveMinStartHands = 0.4` hand-heights, `GloveNearStartHands = 1.6`; `GloveEntryPoint`, `GloveScaleTravel`.
  - Perch: `GloveRise(s) = max(28*s, dh*0.34)` above target; `GloveArch(s) = max(18*s, dh*0.20)`.
  - Draw: `DrawTutorOverlay` → `DrawRestGlove` / `DrawCoachGlove` (L4507) → `DrawGloveAt` (L4526), via IMGUI `GUI.matrix` + `GUI.DrawTexture`.
  - State machine: `enum TutorGloveAct { None, Reappear, Live, FadeOutOnTap, Hidden }`; `TickGloveAct`, `GloveStartFadeOut`, `NoteGloveTap`, `GloveTapHits`, `GloveArmTap`, `GloveVeilReset`.
- `CoachTap.cs` — per-step TapRule: `CoachPointer.Glove` (= tap this) vs Arrow (= look at this) vs None; Required steps advance only on `TapZone.Target`.
- `CaptionLayout.cs` — `GloveOverCaptions = true` (b92), `GloveBannerGap = 4f`, `GloveReach(s) = 120*s`, `GloveClearsBanner`, `GloveHitsCaption`.
- `CoachSeat.cs`, `CoachResetGuard.cs` (caption+glove to alpha 0 instantly, no fade, on reset), `BranchHit.cs` (`GloveTol = 0.45`, `GlovePad = 1.25` world units: the glove's target wins taps near it).
- `DeviceFit.cs`, `PhoneArea.cs` — scaling (below).
- Tests to mirror: `Assets/Tests/Editor/GloveTravelTests.cs`, `Build88GloveTests.cs`, `Build88PokerGloveTests.cs`, `Build89GloveCaptionTests.cs`, `Build94GloveBannerTests.cs`, `Build126TapCueTests.cs`, `CoachArrowShapeTests.cs`.

## 3. Timing / animation spec
- Tap cycle (Coach.cs L163–167): `TapHover = 0.56s`, `TapPress = 0.26s`, `TapLift = 0.22s`, `TapPause = 0.16s` → **TapCycle = 1.20s**.
  - `TapDip`: 0 during hover; smoothstep 0→1 over press; smoothstep 1→0 over lift; 0 in pause. `TapTravel` moves the hand from its hold to the target on the same ease.
- Press visual (in `DrawGloveAt`): at dip 1 the texture widens `1 + 0.07*dip` across the finger and squashes `1 - 0.12*dip` along it (fingertip stays on pivot); curl pose switches to the pressed index bow.
- Idle "bob": a poke along the finger axis, not vertical: `sin(unscaledTime * 2.35) * dh * 0.028 * (1 - dip)` (≈2.67 s period, ±2.8% of hand height); zero while held/fading; uses unscaled time so pause doesn't freeze it.
- Entry wiggle: `sin(t*46) * 7 * _gloveWiggle`, scaled by `(1 - travel)`.
- Fade in: `GloveFadeInDur = 0.18s` (linear alpha). Fade out on tap: `GloveFadeOutDur = 0.16s` from current alpha, pose frozen.
- Hide on tap: `NoteGloveTap` starts the fade on the **valid tap frame only** (tap must hit the armed target rect/hit-test; a miss does not hide it; resume-from-background is not a tap).
- Return: if the tap didn't advance the step, the hand comes back after `GloveReturnSec = 0.9s` (Build 85) — a forced step is never left with no hand.
- Step change / dismiss: drops alpha to 0 immediately, no return timer. No idle auto-hide timer was found for the glove (the pest captions use `PestCueSeconds = 4.5`).

## 4. Offset & direction
- Fingertip pixel = target point (control centre or u,v inside it). The hand body trails away from the tip so the palm stays off the control (`GloveMinStartHands`, `GloveRise`).
- Side/approach: picked from screen half — a target in the left half is met from the right and vice versa (`CoachAimAway`, noted in BadgerFrozenUi.cs); upright clamp otherwise; a locked angled approach (`approachDeg`) keeps its angle and does not mirror to fit (b88). Mirroring is a negative X scale around the tip.
- Enters from lower-right beneath the banner (b94), travel halved (b125).
- Stays on glass: `CoachSafeGui(12*s)` safe rect, `GloveScreenMargin = 6`, `SeatGlove` pushes the pivot inside without retargeting the finger.

## 5. Draw order
- IMGUI overlay. `DrawTutorOverlay`: caption plate first, then rest glove, then **glove last** "over the plate and every other tutorial mark", then fire arrow.
- `CaptionLayout.GloveOverCaptions = true` → the glove is never clipped at a caption plate (b92 reversed b89's clip); `RepaintCaptionOver` only re-stamps words when the glove touches the plate.
- Optional dim veil `CoachDim`: radial, clear to 0.28 of half-extent, solid by 0.62, black alpha 0.34.
- In Lot Loop (if world sprites, not IMGUI): put it on the topmost sorting layer/overlay canvas above cars and UI.

## 6. Scaling across devices
- `s = DeviceFit.Ui() = max(H/720, 1) * lerp(1, TabletUi=0.80, Wide(window))`, where `Wide = clamp01((PlayAspect - 9/16) / (3/4 - 9/16))`, PlayAspect capped at `MaxPlayAspect = 3/4`.
- Hand height = `94.4 * s` GUI px. Phones (≈9:16 → Wide≈0) get the full height scale; iPad gets ×0.80 at 3:4. iPad runs in a pillar (`PhoneArea.Pillar`, `PhoneArea.W = min(Screen.width, Screen.height*9/16)` region) so all glove math uses PhoneArea.W/H, not Screen.*.
- The repo has no per-device tables for 402 / 820 / 412 pt widths; derive per device from the formula with real Screen.height — do not hardcode.

## 7. Inspector Tutorial rules (CHECKLIST.md)
- Tap rule (Brandon 2026-10-24): glove/arrow must sit on the **real focus control**, and tapping **that control** (not empty space) must advance the step.
- #6 one focus point per step; no glove during intense action.
- #8 pointers show where, but the step must still teach the system.
- #19 reserve gloves for non-intuitive systems; obvious gestures → wordless/diegetic.
- #22 blind-obedience glove sequences = confusing; still enforce the tap rule.
- #26 not hidden behind UI, not clipped, readable on iPhone 18 Pro, iPad (A16), Samsung.
- From code comments (Flock Five QA): fingertip on target, palm off the control, never under/clipped by caption, never covering the caption words, stays on glass.
- lot-loop.md report (L112) lightest fix: "on L1, a pointer on one front car, then one line at the first match."

## 8. Porting checklist (Lot Loop, zyxnfryx/car-match)
1. Confirm canonical branch (pr217 vs main) with Creator; copy glove raster (GloveW/H, CoachCap table, GloveTip, curl steps, warm-up) or bake 4 curl PNGs from it.
2. Replace the beige paddle in build 20 L1 with one shared glove helper used by every just-in-time lesson.
3. Size 94.4*s with DeviceFit-style s; compute in phone-area coords (iPad pillar).
4. Fingertip on front car's centre (or u,v on art body); body trails away, palm off the car; side from screen half.
5. Tap cycle 0.56/0.26/0.22/0.16 s, squash 0.12 / widen 0.07, poke bob 2.35 rad/s × 2.8%·dh, fade in 0.18 s, out 0.16 s, return 0.9 s.
6. Draw last, above cars, HUD and caption plates; never clipped.
7. Only a hit on the target hides it and advances; misses don't; step change hides instantly.
8. Keep start travel ≤ 0.5× (min 0.4 hand-heights); stays inside safe rect.
9. Port the editor tests (GloveTravelTests, Build88/89/94 glove tests).
10. Inspector pass on iPhone, iPad, Samsung: unclipped, on target, target visible, tap advances.
