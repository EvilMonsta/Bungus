
2D sci-fi action prototype in **C# / .NET 10** without Unity.

## Implemented now
- Main menu, pause, death/restart flow.
- Fullscreen launch.
- Buildings (**blue zones**) and outposts (**red zones**).
- Loot now spawns only inside buildings/outposts.
- Strong enemies (triangles) guard loot zones, have more HP and shoot burst fire.
- Outposts contain many strong enemies and a boss (large square):
  - periodic ram,
  - AoE slam (transparent warning circle),
  - periodic shooting.
- Enemy AI:
  - aggro on direct ranged/melee hit,
  - aggro propagation to enemies that can see the attacked target,
  - vision distance + FOV with dashed debug lines,
  - sweep behavior left-right (no full rotation),
  - patrol enemies moving A<->B with direction-based blind zone behind.
- Inventory/UI:
  - separate ranged/melee weapon slots,
  - separate armor slot,
  - separate stacked consumable slots (medkit/stim),
  - trash slot (new item sent to trash deletes previous trash item),
  - square cells and hover tooltips.
- Damage scaling:
  - melee scales from STR + AGI (STR weighted stronger),
  - ranged scales from TECH,
  - weapon rarity adds bonus and color for bullets/slash arcs.

## Controls
- `WASD` move
- `LMB` attack
- `Space` dodge
- `E` switch active weapon (ranged/melee)
- `Q` medkit
- `R` stim
- `TAB` inventory
- `ESC` pause/menu