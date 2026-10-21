# Lot Loop FX sprites
All strips: horizontal, 256x256 cells, RGBA transparent, soft alpha (premultiplied-blended, 2x supersampled). Regenerate: `python3 gen_fx.py` (numpy + Pillow, ~20 s, deterministic seed).

| File | Frames | Cell | FPS | Use |
|---|---|---|---|---|
| fx_sparkle_burst.png | 10 | 256 | 20 | Shuffle / general sparkle: 2 anticipation, pop, expanding colored 4-point stars + twinkles, fade. One-shot. |
| magnet_icon.png | 1 | 256 | – | Static glossy red/silver horseshoe magnet booster icon |
| fx_magnet.png | 25 | 256 | see parts | Combined: frames 0-5 icon loop, 6-15 pull trail, 16-24 impact |
| fx_magnet_icon_loop.png | 6 | 256 | 10-12 | Magnet wobble + zap sparks, loop |
| fx_magnet_pull.png | 10 | 256 | 16 | Wavy colored streaks curving to target at (x≈0.81·cell, center), loop. Rotate/flip to aim. |
| fx_magnet_impact.png | 9 | 256 | 20 | Snap/impact burst when cars arrive. One-shot. |
| fx_super_glow.png | 20 | 256 | 20 | Combined: frames 0-9 sweep, 10-19 pop |
| fx_super_glow_sweep.png | 10 | 256 | 20 | Gold diagonal shine band over a 256 cell (overlay on car/button, additive or normal). One-shot or loop with a pause. |
| fx_super_glow_pop.png | 10 | 256 | 20 | Gold pop: rays, ring, spinning coins + stars. One-shot. |
| fx_*_preview.gif | – | 256 | – | Previews on gray |
| fx_sheet_preview.png | – | half-scale | – | Contact sheet; each effect on asphalt (#34363C) and terracotta (#C46848) rows |
