from __future__ import annotations

import argparse
import csv
import shutil
import tempfile
from dataclasses import dataclass
from pathlib import Path

import numpy as np
from PIL import Image, ImageFilter

from build_sprite_contacts import build_contact_sheet
from slice_midterm_sprites import Component, DisjointSet, row_runs


@dataclass
class SpriteCut:
    name: str
    image: Image.Image
    manifest_row: dict[str, str]


def label_components(mask: np.ndarray) -> tuple[np.ndarray, dict[int, Component]]:
    dsu = DisjointSet()
    runs: list[tuple[int, int, int, int]] = []
    previous: list[tuple[int, int, int]] = []

    for y, row in enumerate(mask):
        current: list[tuple[int, int, int]] = []
        previous_index = 0
        for start, end in row_runs(row):
            run_id = dsu.add()
            runs.append((start, end, y, run_id))
            current.append((start, end, run_id))

            while (
                previous_index < len(previous)
                and previous[previous_index][1] < start - 1
            ):
                previous_index += 1

            overlap_index = previous_index
            while (
                overlap_index < len(previous)
                and previous[overlap_index][0] <= end + 1
            ):
                previous_start, previous_end, previous_id = previous[overlap_index]
                if previous_end >= start - 1 and previous_start <= end + 1:
                    dsu.union(run_id, previous_id)
                overlap_index += 1

        previous = current

    aggregate: dict[int, list[int]] = {}
    for start, end, y, run_id in runs:
        root = dsu.find(run_id)
        if root not in aggregate:
            aggregate[root] = [start, y, end, y + 1, end - start]
            continue
        item = aggregate[root]
        item[0] = min(item[0], start)
        item[1] = min(item[1], y)
        item[2] = max(item[2], end)
        item[3] = max(item[3], y + 1)
        item[4] += end - start

    root_to_label = {root: index for index, root in enumerate(aggregate, start=1)}
    labels = np.zeros(mask.shape, dtype=np.int32)
    for start, end, y, run_id in runs:
        labels[y, start:end] = root_to_label[dsu.find(run_id)]

    components = {
        root_to_label[root]: Component(*values)
        for root, values in aggregate.items()
    }
    return labels, components


def dilate(mask: np.ndarray) -> np.ndarray:
    padded = np.pad(mask, 1, constant_values=False)
    result = np.zeros_like(mask)
    for y_offset in range(3):
        for x_offset in range(3):
            result |= padded[
                y_offset : y_offset + mask.shape[0],
                x_offset : x_offset + mask.shape[1],
            ]
    return result


def isolate_sprites(
    source: Image.Image,
    rows: list[dict[str, str]],
    detection_threshold: int,
    growth_pixels: int,
) -> list[SpriteCut]:
    rgba = np.asarray(source.convert("RGBA")).copy()
    alpha = rgba[:, :, 3]
    high_alpha = Image.fromarray(
        np.where(alpha >= detection_threshold, 255, 0).astype(np.uint8),
        mode="L",
    ).filter(ImageFilter.MinFilter(3))
    labels, components = label_components(np.asarray(high_alpha) > 0)

    cuts: list[SpriteCut] = []
    for row in rows:
        left, top, right, bottom = map(
            int,
            (row["left"], row["top"], row["right"], row["bottom"]),
        )
        target_area = int(row["component_area"])
        region_labels = labels[top:bottom, left:right]
        candidate_labels = [
            int(label)
            for label in np.unique(region_labels)
            if label != 0
        ]
        if not candidate_labels:
            raise RuntimeError(f"No component found for {row['sprite_name']}")

        exact_labels = [
            label
            for label in candidate_labels
            if components[label].area == target_area
        ]
        labels_to_rank = exact_labels or candidate_labels
        owner_label = max(
            labels_to_rank,
            key=lambda label: int((region_labels == label).sum()),
        )

        owner = region_labels == owner_label
        foreign_high = (region_labels != 0) & ~owner
        visible = alpha[top:bottom, left:right] > 0
        for _ in range(growth_pixels):
            owner |= dilate(owner) & visible & ~foreign_high

        crop_data = rgba[top:bottom, left:right].copy()
        crop_data[~owner] = (0, 0, 0, 0)
        owned_alpha = crop_data[:, :, 3] > 0
        ys, xs = np.nonzero(owned_alpha)
        if len(xs) == 0:
            raise RuntimeError(f"Empty isolated sprite: {row['sprite_name']}")

        crop_data = crop_data[
            ys.min() : ys.max() + 1,
            xs.min() : xs.max() + 1,
        ]
        cuts.append(
            SpriteCut(
                row["sprite_name"],
                Image.fromarray(crop_data, mode="RGBA"),
                row,
            )
        )

    return cuts


def next_power_of_two(value: int) -> int:
    return 1 << max(0, value - 1).bit_length()


def pack_sprites(
    cuts: list[SpriteCut],
    maximum_width: int,
    gutter: int,
    inner_padding: int,
) -> tuple[Image.Image, dict[str, tuple[int, int, int, int]]]:
    placements: dict[str, tuple[int, int, int, int]] = {}
    layout: list[tuple[SpriteCut, int, int, int, int]] = []
    shelves: list[dict[str, int]] = []
    maximum_right = 0

    for cut in sorted(
        cuts,
        key=lambda item: (item.image.height, item.image.width),
        reverse=True,
    ):
        width = cut.image.width + inner_padding * 2
        height = cut.image.height + inner_padding * 2
        shelf = next(
            (
                candidate
                for candidate in shelves
                if height <= candidate["height"]
                and candidate["x"] + width + gutter <= maximum_width
            ),
            None,
        )
        if shelf is None:
            y = (
                gutter
                if not shelves
                else max(item["y"] + item["height"] for item in shelves) + gutter
            )
            shelf = {"x": gutter, "y": y, "height": height}
            shelves.append(shelf)

        left = shelf["x"]
        top = shelf["y"]
        layout.append((cut, left, top, width, height))
        placements[cut.name] = (left, top, left + width, top + height)
        maximum_right = max(maximum_right, left + width + gutter)
        shelf["x"] += width + gutter

    content_height = max(
        item["y"] + item["height"] for item in shelves
    ) + gutter
    atlas_width = min(maximum_width, next_power_of_two(maximum_right))
    atlas_height = next_power_of_two(content_height)
    atlas = Image.new("RGBA", (atlas_width, atlas_height), (0, 0, 0, 0))
    for cut, left, top, _, _ in layout:
        atlas.alpha_composite(
            cut.image,
            (left + inner_padding, top + inner_padding),
        )

    return atlas, placements


def write_manifest(
    manifest_path: Path,
    rows: list[dict[str, str]],
    placements: dict[str, tuple[int, int, int, int]],
) -> None:
    for row in rows:
        if row["sprite_name"] not in placements:
            continue
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

    fieldnames = list(rows[0].keys())
    with manifest_path.open("w", newline="", encoding="utf-8-sig") as handle:
        writer = csv.DictWriter(handle, fieldnames=fieldnames)
        writer.writeheader()
        writer.writerows(rows)


def main() -> None:
    parser = argparse.ArgumentParser(
        description="Isolate and repack only the Backgrounds midterm sprite atlas."
    )
    parser.add_argument("project_root", type=Path)
    parser.add_argument("--detection-threshold", type=int, default=160)
    parser.add_argument("--growth-pixels", type=int, default=6)
    parser.add_argument("--gutter", type=int, default=8)
    args = parser.parse_args()

    project_root = args.project_root.resolve()
    assets_root = project_root / "Assets" / "MidtermSprites"
    source_path = assets_root / "Raw" / "Backgrounds.png"
    manifest_path = assets_root / "sprite_manifest.csv"
    reference_root = project_root / "Documentation" / "MidtermSpriteReferenceBoards"
    catalog_root = project_root / "Documentation" / "MidtermSpriteCatalog"
    reference_path = reference_root / "Backgrounds.png"

    if not source_path.is_file() or not manifest_path.is_file():
        raise FileNotFoundError("Background atlas or sprite manifest is missing")

    reference_root.mkdir(parents=True, exist_ok=True)
    catalog_root.mkdir(parents=True, exist_ok=True)
    shutil.copy2(source_path, reference_path)

    with manifest_path.open(encoding="utf-8-sig", newline="") as handle:
        all_rows = list(csv.DictReader(handle))
    background_rows = [
        row for row in all_rows if row["source"] == "Raw/Backgrounds.png"
    ]
    if not background_rows:
        raise RuntimeError("No Backgrounds rows found in sprite manifest")

    source = Image.open(source_path).convert("RGBA")
    cuts = isolate_sprites(
        source,
        background_rows,
        args.detection_threshold,
        args.growth_pixels,
    )
    atlas, placements = pack_sprites(cuts, 2048, args.gutter, inner_padding=2)
    atlas.save(source_path, optimize=True)
    write_manifest(manifest_path, all_rows, placements)

    preview_temp_root = project_root / "Temp"
    preview_temp_root.mkdir(parents=True, exist_ok=True)
    with tempfile.TemporaryDirectory(
        prefix="background-sprite-preview-",
        dir=preview_temp_root,
    ) as temp:
        preview_root = Path(temp)
        for cut in cuts:
            cut.image.save(preview_root / f"{cut.name}.png", optimize=True)
        build_contact_sheet(preview_root, catalog_root / "Backgrounds.jpg")

    (reference_root / "README.txt").write_text(
        "Backgrounds.png is the pre-repack comparison board.\n"
        "Assets/MidtermSprites/Raw/Backgrounds.png is the cleaned, repacked "
        "Sprite Mode Multiple atlas.\n",
        encoding="utf-8",
    )
    print(f"Isolated and repacked {len(cuts)} Backgrounds sprites.")
    print(f"Atlas size: {atlas.width}x{atlas.height}")
    print(f"Original reference: {reference_path}")


if __name__ == "__main__":
    main()
