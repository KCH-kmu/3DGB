from __future__ import annotations

import argparse
import csv
from pathlib import Path

from PIL import Image, ImageChops


def load_rows(path: Path) -> dict[str, dict[str, str]]:
    with path.open(encoding="utf-8-sig", newline="") as handle:
        return {row["sprite_name"]: row for row in csv.DictReader(handle)}


def crop(atlas: Image.Image, row: dict[str, str]) -> Image.Image:
    return atlas.crop(
        tuple(int(row[key]) for key in ("left", "top", "right", "bottom"))
    )


def main() -> None:
    parser = argparse.ArgumentParser(
        description="Verify that compaction preserves every sprite pixel exactly."
    )
    parser.add_argument("before_image", type=Path)
    parser.add_argument("before_manifest", type=Path)
    parser.add_argument("after_image", type=Path)
    parser.add_argument("after_manifest", type=Path)
    args = parser.parse_args()

    before_rows = load_rows(args.before_manifest)
    after_rows = load_rows(args.after_manifest)
    if before_rows.keys() != after_rows.keys():
        raise SystemExit("Sprite name sets differ between manifests.")

    before = Image.open(args.before_image).convert("RGBA")
    after = Image.open(args.after_image).convert("RGBA")
    mismatches: list[str] = []
    for name in before_rows:
        before_crop = crop(before, before_rows[name])
        after_crop = crop(after, after_rows[name])
        if before_crop.size != after_crop.size or ImageChops.difference(
            before_crop, after_crop
        ).getbbox() is not None:
            mismatches.append(name)

    if mismatches:
        raise SystemExit("Pixel mismatches: " + ", ".join(mismatches))
    print(f"Pixel comparison passed: {len(before_rows)} sprites unchanged.")


if __name__ == "__main__":
    main()
