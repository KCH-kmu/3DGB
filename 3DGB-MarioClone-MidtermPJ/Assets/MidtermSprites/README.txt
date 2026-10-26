Midterm sprite import

Raw contains 11 cleaned and repacked sprite atlases.
Each Raw atlas is imported by Unity as Sprite Mode: Multiple.
Open a Raw texture in Sprite Editor to inspect its named slices.
LevelGuides contains the retained Level1 and Level6 reference layouts, not
runtime sprites.
sprite_manifest.csv records all 966 slices and their atlas coordinates.

Every sprite was pixel-isolated and repacked with transparent padding and an
8-pixel gutter so neighboring artwork does not appear inside another slice.
Board annotations that touched artwork were removed from the cleaned atlases.
Temporary pre-repack boards and visual contact sheets were removed after the
sprite import and visual QA work was completed.

All 11 atlases use compact packing while retaining the 8-pixel gutter. Their
final sizes are:
Backgrounds 1880x856, Boss 1048x1016, Effects 980x504,
Enemies 1684x376, Grounds 1752x792, Hero 1588x696,
Level-end 1848x696, Objects 1592x472, Pickups 1368x664,
Tiles 1784x728, and UI 1688x760.

The compact imported atlases and sprite_manifest.csv are the maintained source
of truth for the final project.
