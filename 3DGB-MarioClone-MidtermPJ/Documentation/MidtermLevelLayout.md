# Midterm Level Layout

The project contains two playable Unity scenes under `Assets/Scenes/Levels`.
`Assets/Scenes/Midterm_LevelTemplate.unity` is retained as the
generation/reference scene and is not included in Build Settings.

| Build index | Scene | Guide focus |
| --- | --- | --- |
| 0 | `Midterm_Level1_GreenFields` | Intro ground route, pipes, moving platforms, simple enemies |
| 1 | `Midterm_Level6_BossCastle` | Lava castle approach followed by the final boss arena |

Completing Level 1 loads Level 6. Completing Level 6 wraps back to Level 1.

The two scenes keep separate guide-specific layouts: the Green Fields rope
bridge and introductory traversal in Level 1, and the lava-castle approach and
Boss Castle arena in Level 6.

Each route ends near x=68.5 instead of the former x=108-scale layout. Measured
from the player start at x=-7, this reduces the playable horizontal span from
about 115 units to about 75.5 units (roughly 34 percent) while retaining the
guide-specific traversal identity.

Visual placement rules keep decorative sprites grounded on the supporting
terrain, keep collectible coins clear of brick/question-block rows, and draw
water or lava behind the foreground terrain edge. Progression objects and
ground enemies are offset from liquid gaps, spikes, and pipe footprints.
Objects from the former long-route layouts are filtered out past the compact
route limit so that no orphaned hazards or decorations remain beyond the
playable area.

Both scenes contain a player start, HUD, camera bounds, fall-respawn zone,
terrain colliders, coins, enemies, moving platforms, Fire Flower placement,
checkpoint placement, key, locked door and level goal. Level 6 additionally
contains a functional boss, explicit boss-arena boundary markers, and the boss
health HUD.

The terrain, coin pickup, enemy movement, moving platform, fall respawn and goal
objects reuse the existing project scripts. Fire Flowers are functional
collectibles: they enlarge the player by 20 percent, enable X-key fireball
shooting, and absorb the next hit before a life is deducted. The checkpoint and
respawn flow is active. The Level 6 boss patrols and charges inside the arena,
takes stomp or fireball damage, updates the boss-health HUD, blocks the goal
until defeated, and awards score on defeat.

Five preview renders covering each retained scene are stored in
`Documentation/MidtermLevelPreviews`. Previews for Levels 2-5 and Level 7 are
removed by the level builder.
