from __future__ import annotations

import argparse
import csv
import shutil
import tempfile
from pathlib import Path

from PIL import Image

from build_sprite_contacts import build_contact_sheet
from repack_background_sprites import isolate_sprites, pack_sprites


REMAINING_ATLASES = (
    "Boss.png",
    "Effects.png",
    "Enemies.png",
    "Grounds.png",
    "Hero.png",
    "Level-end.png",
    "Objects.png",
    "Pickups.png",
    "Tiles.png",
    "UI.png",
)

# These are board annotations or palette swatches, not game sprites.
EXCLUDED_SPRITE_NAMES = {
    "Hero_067",
    "Hero_068",
    "Objects_001",
    "Objects_077",
    "Objects_078",
    "UI_002",
    "UI_003",
    "UI_004",
    "UI_018",
    "UI_019",
}

# A few annotations touch a real sprite and therefore form one connected component.
# Clear only the annotation area before component isolation; the comparison board is
# copied before this cleanup and always remains unchanged.
ANNOTATION_ERASE_BOXES = {
    "Hero.png": ((925, 689, 1033, 718),),  # "Climb (Loop)" suffix
    "Objects.png": (
        (757, 526, 860, 558),  # "CRUSHER"
        (1040, 703, 1220, 733),  # one-way-platform direction arrows
    ),
    "Tiles.png": ((12, 679, 210, 708),),  # "CAVE THEME ELEMENTS"
}


def write_manifest(path: Path, rows: list[dict[str, str]]) -> None:
    fieldnames = list(rows[0].keys())
    with path.open("w", newline="", encoding="utf-8-sig") as handle:
        writer = csv.DictWriter(handle, fieldnames=fieldnames)
        writer.writeheader()
        writer.writerows(rows)


def main() -> None:
    parser = argparse.ArgumentParser(
        description="Pixel-isolate and repack every midterm atlas except Backgrounds."
    )
    parser.add_argument("project_root", type=Path)
    parser.add_argument("--detection-threshold", type=int, default=160)
    parser.add_argument("--growth-pixels", type=int, default=8)
    parser.add_argument("--gutter", type=int, default=8)
    args = parser.parse_args()

    project_root = args.project_root.resolve()
    assets_root = project_root / "Assets" / "MidtermSprites"
    manifest_path = assets_root / "sprite_manifest.csv"
    reference_root = project_root / "Documentation" / "MidtermSpriteReferenceBoards"
    catalog_root = project_root / "Documentation" / "MidtermSpriteCatalog"
    preview_temp_root = project_root / "Temp"

    if not manifest_path.is_file():
        raise FileNotFoundError(manifest_path)

    reference_root.mkdir(parents=True, exist_ok=True)
    catalog_root.mkdir(parents=True, exist_ok=True)
    preview_temp_root.mkdir(parents=True, exist_ok=True)

    with manifest_path.open(encoding="utf-8-sig", newline="") as handle:
        all_rows = list(csv.DictReader(handle))
    all_rows = [
        row for row in all_rows if row["sprite_name"] not in EXCLUDED_SPRITE_NAMES
    ]

    results: list[str] = []
    for filename in REMAINING_ATLASES:
        source_key = f"Raw/{filename}"
        source_path = assets_root / source_key
        if not source_path.is_file():
            raise FileNotFoundError(source_path)

        source_rows = [row for row in all_rows if row["source"] == source_key]
        if not source_rows:
            raise RuntimeError(f"No manifest rows found for {source_key}")

        shutil.copy2(source_path, reference_root / filename)
        source = Image.open(source_path).convert("RGBA")
        for box in ANNOTATION_ERASE_BOXES.get(filename, ()):
            source.paste((0, 0, 0, 0), box)
        cuts = isolate_sprites(
            source,
            source_rows,
            args.detection_threshold,
            args.growth_pixels,
        )

        atlas, placements = pack_sprites(
            cuts,
            maximum_width=2048,
            gutter=args.gutter,
            inner_padding=2,
        )
        if atlas.height > 2048:
            atlas, placements = pack_sprites(
                cuts,
                maximum_width=4096,
                gutter=args.gutter,
                inner_padding=2,
            )
        if atlas.width > 4096 or atlas.height > 4096:
            raise RuntimeError(
                f"Packed atlas exceeds 4096 pixels: {filename} {atlas.size}"
            )

        atlas.save(source_path, optimize=True)
        for row in source_rows:
            left, top, right, bottom = placements[row["sprite_name"]]
            row.update(
                {
                    "left": str(left),
                    "top": str(top),
                    "right": str(right),
                    "bottom": str(bottom),
                    "width": str(right - left),
                    "height": str(bottom - top),
                }
            )

        with tempfile.TemporaryDirectory(
            prefix=f"{Path(filename).stem}-sprite-preview-",
            dir=preview_temp_root,
        ) as temp:
            preview_root = Path(temp)
            for cut in cuts:
                cut.image.save(preview_root / f"{cut.name}.png", optimize=True)
            catalog_name = Path(filename).stem.replace("-", "_") + ".jpg"
            build_contact_sheet(preview_root, catalog_root / catalog_name)

        results.append(f"{filename}: {len(cuts)} sprites, {atlas.width}x{atlas.height}")

    write_manifest(manifest_path, all_rows)
    (reference_root / "README.txt").write_text(
        "These PNG files are the pre-repack comparison boards.\n"
        "Assets/MidtermSprites/Raw contains the cleaned, repacked Sprite Mode "
        "Multiple atlases.\n",
        encoding="utf-8",
    )
    print("Repacked remaining midterm sprite atlases:")
    for result in results:
        print(result)


if __name__ == "__main__":
    main()
