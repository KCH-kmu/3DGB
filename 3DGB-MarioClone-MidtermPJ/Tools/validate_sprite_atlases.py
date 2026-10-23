from __future__ import annotations

import argparse
import csv
from collections import Counter, defaultdict
from pathlib import Path

from PIL import Image


def rectangles_overlap(a: dict[str, str], b: dict[str, str]) -> bool:
    return (
        int(a["left"]) < int(b["right"])
        and int(a["right"]) > int(b["left"])
        and int(a["top"]) < int(b["bottom"])
        and int(a["bottom"]) > int(b["top"])
    )


def main() -> None:
    parser = argparse.ArgumentParser(description="Validate repacked midterm sprite atlases.")
    parser.add_argument("project_root", type=Path)
    parser.add_argument("--expected-total", type=int, default=976)
    args = parser.parse_args()

    project_root = args.project_root.resolve()
    asset_root = project_root / "Assets" / "MidtermSprites"
    manifest_path = asset_root / "sprite_manifest.csv"
    with manifest_path.open(encoding="utf-8-sig", newline="") as handle:
        rows = list(csv.DictReader(handle))

    errors: list[str] = []
    if len(rows) != args.expected_total:
        errors.append(f"Expected {args.expected_total} rows, found {len(rows)}")

    duplicate_names = [
        name for name, count in Counter(row["sprite_name"] for row in rows).items() if count > 1
    ]
    if duplicate_names:
        errors.append(f"Duplicate names: {', '.join(duplicate_names)}")

    by_source: dict[str, list[dict[str, str]]] = defaultdict(list)
    for row in rows:
        by_source[row["source"]].append(row)

    print(f"Manifest rows: {len(rows)}")
    for source_key in sorted(by_source):
        source_path = asset_root / source_key
        if not source_path.is_file():
            errors.append(f"Missing atlas: {source_key}")
            continue

        with Image.open(source_path) as atlas:
            rgba = atlas.convert("RGBA")
            atlas_width, atlas_height = rgba.size
            rows_for_source = by_source[source_key]
            empty_count = 0
            out_of_bounds_count = 0
            overlap_count = 0

            for row in rows_for_source:
                left, top, right, bottom = (
                    int(row["left"]),
                    int(row["top"]),
                    int(row["right"]),
                    int(row["bottom"]),
                )
                if not (
                    0 <= left < right <= atlas_width
                    and 0 <= top < bottom <= atlas_height
                ):
                    out_of_bounds_count += 1
                    continue
                if rgba.crop((left, top, right, bottom)).getchannel("A").getbbox() is None:
                    empty_count += 1

            for index, first in enumerate(rows_for_source):
                for second in rows_for_source[index + 1 :]:
                    if rectangles_overlap(first, second):
                        overlap_count += 1

            print(
                f"{source_key}: {len(rows_for_source)} sprites, "
                f"{atlas_width}x{atlas_height}, overlaps={overlap_count}, "
                f"empty={empty_count}, out_of_bounds={out_of_bounds_count}"
            )
            if overlap_count:
                errors.append(f"{source_key}: {overlap_count} overlapping rectangle pairs")
            if empty_count:
                errors.append(f"{source_key}: {empty_count} empty sprites")
            if out_of_bounds_count:
                errors.append(f"{source_key}: {out_of_bounds_count} out-of-bounds sprites")

    if errors:
        raise SystemExit("Validation failed:\n- " + "\n- ".join(errors))
    print("Validation passed.")


if __name__ == "__main__":
    main()
