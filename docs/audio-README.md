# Lot Loop: Park & Sort — SFX pack (procedural, original)
All sounds synthesized in `../render_audio.py` (numpy/scipy, fixed seed, no samples, original C-major motifs).
Format: mono, 44.1 kHz, `.wav` (16-bit PCM) + `.ogg` (Vorbis). SFX true-peak -1 dBTP with a soft tanh limiter; music -18 LUFS.
See `measurements.json` for duration / dBTP / LUFS. `preview.mp3` plays every cue in order (music trimmed to 8 s).

| File | Game event | Unity volume |
|---|---|---|
| car_tap_rev_00..02 | Player taps a car (tap + toy engine rev) | 0.8 |
| drive_out_whoosh_00..02 | Car drives out of the lot | 0.7 |
| slot_pop_00..02 | Car lands in a bar slot (C/E/G pitch) | 0.75 |
| match3_clear | 3 matching cars cleared | 0.9 |
| blocked_honk | Tapped car is blocked | 0.75 |
| button_click | Any UI button | 0.6 |
| booster_undo / booster_shuffle / booster_magnet / booster_super | Booster used | 0.85 |
| level_win_fanfare | Level complete | 0.9 |
| mascot_cheer | Mascot reaction on win (play ~0.3 s after fanfare starts) | 0.8 |
| level_fail | Out of slots / level failed (gentle) | 0.75 |
| coin_gain_00..02 | Coin awarded (rapid sequences: cycle variants) | 0.65 |
| music_bed_loop | Gameplay background, `loop = true` | 0.4 |

Tips
- Randomize the `_00.._02` variants per play (avoid repeating the last one) and add ±3% `pitch` jitter on the AudioSource.
- Duck music to ~0.25 during the fanfare/fail.
- Import: SFX → Decompress On Load (Vorbis/ADPCM); music → Streaming. Use the `.wav` for music looping (Vorbis/MP3 encoders can add padding at the seam); in Unity the imported clip loops sample-accurately.
- Regenerate: `python3 render_audio.py` (needs numpy, scipy, soundfile, pyloudnorm, ffmpeg for the preview).
