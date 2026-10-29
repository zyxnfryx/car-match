# Lot Loop: Park & Sort — Audio pack v2 (procedural, original)
Rendered by `../render_v2.py` (v2: everything current) which reuses synth helpers from `../render_audio.py` (v1; still renders the legacy `music_bed_loop`).
No samples; all melodies original, all in C major. Run from `/workspace/carmatch`: `python3 render_v2.py` (needs numpy, scipy, soundfile, pyloudnorm, ffmpeg).

- **SFX:** mono 44.1 kHz, `.wav` 16-bit + `.ogg`, all loudness-matched to **-14 LUFS** (short clips are padded with silence to 0.5 s before measuring), true peak ≤ -1 dBTP, first/last samples 0.
- **Music:** stereo 44.1 kHz, seamless whole-bar loops, **-18 LUFS**, peak ≤ -2 dBTP.
- Numbers per file are in `measurements.json`. `preview.mp3` has all the taps and squeaks, then the other SFX, then 20 s of each music track.

Because the SFX are now equal loudness, the volumes below are mix intent, not compensation.

| File | Game event | Unity volume |
|---|---|---|
| car_tap_rev_00..04 | Player touches a car (rubbery boop + vroom-vroom; 02/04 = horn chirp + rev) | 0.85 |
| car_select_squeak_00..02 | Optional layer on touch / car eye-blink idle | 0.45 |
| drive_out_whoosh_00..02 | Car drives out of the lot | 0.65 |
| slot_pop_00..02 | Car lands in a bar slot | 0.7 |
| match3_clear | 3 cars matched and cleared | 0.9 |
| blocked_honk | Tapped car is blocked | 0.7 |
| button_click | UI button | 0.5 |
| booster_undo / _shuffle / _magnet / _super | Booster used | 0.8 |
| level_win_fanfare | Level complete, once, on the lot celebration | 0.9 |
| mascot_cheer | Unused on a clear (two short bursts; was the double beep). Studio card uses beep_beep instead | 0.75 |
| level_fail | Level failed (gentle) | 0.7 |
| coin_gain_00..02 | Coin awarded | 0.6 |
| music_home_loop | Home / map screen (100 BPM, 16 bars, 38.4 s) | 0.4 |
| music_gameplay_loop | In-level (112 BPM, 28 bars, 60 s: A A B B A A + turnaround) | 0.32 |
| music_bed_loop | Legacy v1 bed (kept) | 0.4 |

Tips
- Randomize the variants each time a sound plays, avoiding the one that just played, and add ±3% random pitch on the AudioSource. If you layer the squeak on the tap, start it 0–30 ms later.
- Lower the music to about 0.25 during the fanfare and fail stingers. Crossfade between the home and gameplay tracks over about 1 s.
- Import settings for SFX: Decompress On Load. Import settings for music: Streaming, with Loop enabled. Import the music `.wav` and let Unity encode it, so the loop stays sample-accurate. A standalone `.ogg` or `.mp3` file may get encoder padding at the loop point.
