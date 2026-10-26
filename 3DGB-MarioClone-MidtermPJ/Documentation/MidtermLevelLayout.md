# Midterm Level Layout

The seven guide images are represented by seven independent Unity scenes under
`Assets/Scenes/Levels`. The original `Assets/Scenes/MarioClone.unity` remains
unchanged as a reference scene.

| Build index | Scene | Guide focus |
| --- | --- | --- |
| 0 | `Level1_GreenFields` | Intro ground route, pipes, moving platforms, simple enemies |
| 1 | `Level2_ForestCaves` | Dark cave route, vertical platforms, spikes and lava gaps |
| 2 | `Level3_WaterfallCliffs` | Long water gaps, floating routes and moving platforms |
| 3 | `Level4_SnowMountain` | Snow platforms, height changes and ice-gap timing |
| 4 | `Level5_DesertRuins` | Sand ruins, traps, pipes and broken elevated paths |
| 5 | `Level6_FireCastle` | Lava castle, narrow pillars and dense timed platforms |
| 6 | `Level7_SkyIslands` | Floating islands, flying enemies and final boss arena |

The scenes do not share one repeated obstacle template. Their guide-specific
landmarks include the Green Fields rope bridge, Forest Caves crystals and
hanging mace, Waterfall Cliffs ropes and falls, Snow Mountain ice ledges,
Desert Ruins columns, Fire Castle hanging hazards, and Sky Islands cloud and
wind routes.

Visual placement rules keep decorative sprites grounded on the supporting
terrain, keep collectible coins clear of brick/question-block rows, and draw
water or lava behind the foreground terrain edge. Progression objects and
ground enemies are also offset from liquid gaps, spikes, and pipe footprints.

Every scene contains a player start, HUD, camera bounds, fall-respawn zone,
terrain colliders, coins, enemies, moving platforms, Fire Flower placement,
checkpoint placement, key, locked door and level goal. Level 7 additionally
contains the boss placement and explicit boss-arena boundary markers.

The terrain, coin pickup, enemy movement, moving platform, fall respawn and goal
objects reuse the existing project scripts. Fire Flowers are functional
collectibles: they enlarge the player by 20 percent, enable X-key fireball
shooting, and absorb the next hit before a life is deducted. Checkpoint,
key/door and boss objects remain deliberately named placement objects for the
next implementation stage.

Five preview renders covering the full route of every scene are stored in
`Documentation/MidtermLevelPreviews`.
