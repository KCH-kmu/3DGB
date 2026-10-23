from __future__ import annotations

import argparse
import csv
import io
import shutil
import sys
import zipfile
from dataclasses import dataclass
from pathlib import Path

import numpy as np
from PIL import Image, ImageFilter


ATLAS_NAMES = {
    "Backgrounds.png",
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
}


@dataclass(frozen=True)
class Component:
    left: int
    top: int
    right: int
    bottom: int
    area: int

    @property
    def width(self) -> int:
        return self.right - self.left

    @property
    def height(self) -> int:
        return self.bottom - self.top


class DisjointSet:
    def __init__(self) -> None:
        self.parent: list[int] = []
        self.rank: list[int] = []

    def add(self) -> int:
        index = len(self.parent)
        self.parent.append(index)
        self.rank.append(0)
        return index

    def find(self, value: int) -> int:
        parent = self.parent[value]
        if parent != value:
            self.parent[value] = self.find(parent)
        return self.parent[value]

    def union(self, left: int, right: int) -> None:
        left_root = self.find(left)
        right_root = self.find(right)
        if left_root == right_root:
            return
        if self.rank[left_root] < self.rank[right_root]:
            left_root, right_root = right_root, left_root
        self.parent[right_root] = left_root
        if self.rank[left_root] == self.rank[right_root]:
            self.rank[left_root] += 1


def row_runs(row: np.ndarray) -> list[tuple[int, int]]:
    padded = np.pad(row.astype(np.int8), (1, 1), constant_values=0)
    changes = np.diff(padded)
    starts = np.flatnonzero(changes == 1)
    ends = np.flatnonzero(changes == -1)
    return [(int(start), int(end)) for start, end in zip(starts, ends)]


def connected_components(mask: np.ndarray) -> list[Component]:
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

    return [
        Component(left, top, right, bottom, area)
        for left, top, right, bottom, area in aggregate.values()
    ]


def clean_alpha(image: Image.Image, threshold: int) -> Image.Image:
    rgba = image.convert("RGBA")
    data = np.asarray(rgba).copy()
    alpha = data[:, :, 3]
    data[alpha < threshold] = (0, 0, 0, 0)
    return Image.fromarray(data, mode="RGBA")


def mean_luminance(image: Image.Image, component: Component) -> float:
    crop = np.asarray(
        image.crop(
            (component.left, component.top, component.right, component.bottom)
        )
    )
    alpha = crop[:, :, 3] > 0
    if not np.any(alpha):
        return 0.0
    rgb = crop[:, :, :3].astype(np.float32)
    luminance = 0.2126 * rgb[:, :, 0] + 0.7152 * rgb[:, :, 1] + 0.0722 * rgb[:, :, 2]
    return float(luminance[alpha].mean())


def is_probable_label(
    image: Image.Image,
    component: Component,
    stem: str,
) -> bool:
    width = component.width
    height = component.height
    if width < 12 or height < 12 or component.area < 220:
        return True

    aspect = width / max(height, 1)
    luminance = mean_luminance(image, component)

    # Section-title bars are dark, wide, and shallow. Removing these also
    # prevents labels such as "IDLE" and "GROUND TILES" from becoming assets.
    if height <= 64 and width >= 80 and aspect >= 2.0 and luminance < 105:
        return True

    # Individual outlined letters are normally small disconnected components.
    if height <= 30 and width <= 48 and component.area < 650:
        return True

    # These character/collectible boards use outlined text above rows of
    # sprites. Those labels are not runtime art. Projectile-sized components
    # in the lower portion of Enemies are retained.
    label_heavy_sources = {"Hero", "Boss", "Enemies", "Pickups"}
    if stem in label_heavy_sources and height <= 42 and width >= 26:
        if stem != "Enemies" or component.top < 900:
            return True

    return False


def sort_components(components: list[Component]) -> list[Component]:
    # Bucketing nearby Y coordinates keeps each visual row in left-to-right order.
    return sorted(components, key=lambda item: (item.top // 24, item.left, item.top))


def refine_components(components: list[Component], stem: str) -> list[Component]:
    if stem != "Boss":
        return components

    refined: list[Component] = []
    for component in components:
        # The overview caption is connected to the large boss by a few opaque
        # pixels in the supplied board. Keep only the character below it.
        if (
            component.left <= 20
            and 50 <= component.top <= 70
            and component.right >= 290
            and component.bottom >= 350
        ):
            refined.append(
                Component(
                    component.left,
                    91,
                    component.right,
                    component.bottom,
                    component.area,
                )
            )
            continue

        # The optional-shield caption and the first two translucent shield
        # frames form one alpha component. Replace that mixed component with
        # two explicit frame rectangles. The third shield is already detected
        # separately to the right.
        if (
            740 <= component.left <= 765
            and 670 <= component.top <= 695
            and component.right >= 1030
            and component.bottom >= 805
        ):
            refined.extend(
                (
                    Component(768, 711, 901, 816, 12000),
                    Component(900, 711, 1040, 816, 12000),
                )
            )
            continue

        refined.append(component)

    return refined


def slice_atlas(
    image: Image.Image,
    output_dir: Path,
    stem: str,
    source_filename: str,
    alpha_threshold: int,
    detection_threshold: int,
    padding: int,
    erosion_size: int,
    write_individual_pngs: bool,
) -> list[dict[str, object]]:
    cleaned = clean_alpha(image, alpha_threshold)
    alpha_data = np.asarray(cleaned.getchannel("A"))
    detection_alpha = Image.fromarray(
        np.where(alpha_data >= detection_threshold, 255, 0).astype(np.uint8),
        mode="L",
    )
    # A light erosion breaks the thin antialiased bridges that occasionally
    # connect a section label to a nearby sprite. Padding restores the visual
    # edge when the original cleaned pixels are cropped below.
    component_mask = (
        detection_alpha.filter(ImageFilter.MinFilter(erosion_size))
        if erosion_size > 1
        else detection_alpha
    )
    mask = np.asarray(component_mask) > 0
    components = [
        component
        for component in connected_components(mask)
        if not is_probable_label(cleaned, component, stem)
    ]
    components = refine_components(components, stem)
    components = sort_components(components)

    if write_individual_pngs:
        output_dir.mkdir(parents=True, exist_ok=True)
    manifest: list[dict[str, object]] = []
    for index, component in enumerate(components, start=1):
        left = max(0, component.left - padding)
        top = max(0, component.top - padding)
        right = min(cleaned.width, component.right + padding)
        bottom = min(cleaned.height, component.bottom + padding)
        crop = cleaned.crop((left, top, right, bottom))

        sprite_name = f"{stem}_{index:03d}"
        if write_individual_pngs:
            crop.save(output_dir / f"{sprite_name}.png", optimize=True)
        manifest.append(
            {
                "sprite_name": sprite_name,
                "source": f"Raw/{source_filename}",
                "index": index,
                "left": left,
                "top": top,
                "right": right,
                "bottom": bottom,
                "width": right - left,
                "height": bottom - top,
                "component_area": component.area,
            }
        )

    return manifest


def write_manifest(path: Path, rows: list[dict[str, object]]) -> None:
    fieldnames = [
        "sprite_name",
        "source",
        "index",
        "left",
        "top",
        "right",
        "bottom",
        "width",
        "height",
        "component_area",
    ]
    with path.open("w", newline="", encoding="utf-8-sig") as handle:
        writer = csv.DictWriter(handle, fieldnames=fieldnames)
        writer.writeheader()
        writer.writerows(rows)


def extract_and_slice(
    archive_path: Path,
    project_root: Path,
    alpha_threshold: int,
    detection_threshold: int,
    padding: int,
    erosion_size: int,
    write_individual_pngs: bool,
) -> None:
    assets_root = project_root / "Assets" / "MidtermSprites"
    raw_root = assets_root / "Raw"
    guides_root = assets_root / "LevelGuides"
    processed_root = assets_root / "Processed"

    if assets_root.exists():
        raise FileExistsError(
            f"Refusing to overwrite existing generated folder: {assets_root}"
        )

    raw_root.mkdir(parents=True)
    guides_root.mkdir(parents=True)
    if write_individual_pngs:
        processed_root.mkdir(parents=True)

    manifest: list[dict[str, object]] = []
    try:
        with zipfile.ZipFile(archive_path) as archive:
            png_names = sorted(
                name
                for name in archive.namelist()
                if name.lower().endswith(".png")
            )
            for name in png_names:
                data = archive.read(name)
                image = Image.open(io.BytesIO(data))
                filename = Path(name).name

                if filename in ATLAS_NAMES:
                    raw_path = raw_root / filename
                    cleaned_atlas = clean_alpha(image, alpha_threshold)
                    cleaned_atlas.save(raw_path, optimize=True)
                    stem = Path(filename).stem.replace("-", "_")
                    manifest.extend(
                        slice_atlas(
                            cleaned_atlas,
                            processed_root / stem,
                            stem,
                            filename,
                            alpha_threshold,
                            detection_threshold,
                            padding,
                            erosion_size,
                            write_individual_pngs,
                        )
                    )
                elif filename.lower().startswith("level"):
                    (guides_root / filename).write_bytes(data)
                else:
                    (raw_root / filename).write_bytes(data)

        write_manifest(assets_root / "sprite_manifest.csv", manifest)
        (assets_root / "README.txt").write_text(
            "Midterm sprite import\n"
            "\n"
            "Raw contains the original shared sprite boards.\n"
            "Each Raw board is imported by Unity as Sprite Mode: Multiple.\n"
            "Open a Raw texture in Sprite Editor to inspect its named slices.\n"
            "LevelGuides contains Level1-Level7 reference layouts, not runtime sprites.\n"
            "sprite_manifest.csv records every slice and its source coordinates.\n",
            encoding="utf-8",
        )
    except Exception:
        shutil.rmtree(assets_root, ignore_errors=True)
        raise

    print(f"Imported {len(ATLAS_NAMES)} source atlases.")
    print(f"Defined {len(manifest)} atlas sprite slices.")
    if write_individual_pngs:
        print("Also generated individual PNG previews.")
    print(f"Output: {assets_root}")


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Extract and deterministically slice the shared midterm sprite pack."
    )
    parser.add_argument("archive", type=Path)
    parser.add_argument("project_root", type=Path)
    parser.add_argument("--alpha-threshold", type=int, default=24)
    parser.add_argument("--detection-threshold", type=int, default=160)
    parser.add_argument("--padding", type=int, default=2)
    parser.add_argument("--erosion-size", type=int, choices=(1, 3, 5), default=3)
    parser.add_argument(
        "--write-individual-pngs",
        action="store_true",
        help="Also write each slice as a standalone PNG for QA/contact sheets.",
    )
    return parser.parse_args()


def main() -> None:
    args = parse_args()
    archive = args.archive.resolve()
    project_root = args.project_root.resolve()
    if not archive.is_file():
        raise FileNotFoundError(archive)
    if not (project_root / "Assets").is_dir():
        raise FileNotFoundError(f"Unity Assets folder not found: {project_root}")
    if not 1 <= args.alpha_threshold <= 254:
        raise ValueError("alpha threshold must be between 1 and 254")
    if not args.alpha_threshold <= args.detection_threshold <= 254:
        raise ValueError(
            "detection threshold must be between alpha threshold and 254"
        )
    extract_and_slice(
        archive,
        project_root,
        args.alpha_threshold,
        args.detection_threshold,
        max(0, args.padding),
        args.erosion_size,
        args.write_individual_pngs,
    )


if __name__ == "__main__":
    try:
        main()
    except Exception as error:
        print(f"ERROR: {error}", file=sys.stderr)
        raise
