Compact sprite atlas comparison

All atlases retain an 8-pixel gutter. Every before/after sprite crop was
verified pixel-for-pixel after packing.

Atlas        Before       After        Area reduction
Backgrounds  2048x1024    1880x856      23.26%
Boss         2048x1024    1048x1016     49.23%
Effects      2048x512     980x504       52.90%
Enemies      2048x512     1684x376      39.61%
Grounds      2048x1024    1752x792      33.83%
Hero         2048x1024    1588x696      47.30%
Level-end    2048x1024    1848x696      38.67%
Objects      2048x512     1592x472      28.34%
Pickups      2048x1024    1368x664      56.69%
Tiles        2048x1024    1784x728      38.07%
UI           2048x1024    1688x760      38.83%

Combined atlas area reduction: 40.66%

*_BeforeCompact.png and *_BeforeCompact.csv contain the previous atlas and
slice coordinates. *_AfterCompact.png and *_AfterCompact.csv contain the
compact result. *_CompactStats.txt contains the per-atlas calculation.
