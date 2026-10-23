from __future__ import annotations

import argparse
import csv
import math
import shutil
from dataclasses import dataclass
from pathlib import Path

from PIL import Image


@dataclass(frozen=True)
class Rect:
    x: int
    y: int
    width: int
    height: int

    @property
    def right(self) -> int:
        return self.x + self.width

    @property
    def bottom(self) -> int:
        return self.y + self.height


@dataclass
class Item:
    name: str
    image: Image.Image
    row: dict[str, str]


def intersects(first: Rect, second: Rect) -> bool:
    return (
        first.x < second.right
        and first.right > second.x
        and first.y < second.bottom
        and first.bottom > second.y
    )


def split_free_rect(free: Rect, used: Rect) -> list[Rect]:
    if not intersects(free, used):
        return [free]

    result: list[Rect] = []
    if used.x > free.x:
        result.append(Rect(free.x, free.y, used.x - free.x, free.height))
    if used.right < free.right:
        result.append(Rect(used.right, free.y, free.right - used.right, free.height))
    if used.y > free.y:
        result.append(Rect(free.x, free.y, free.width, used.y - free.y))
    if used.bottom < free.bottom:
        result.append(Rect(free.x, used.bottom, free.width, free.bottom - used.bottom))
    return [rect for rect in result if rect.width > 0 and rect.height > 0]


def contained(inner: Rect, outer: Rect) -> bool:
    return (
        inner.x >= outer.x
        and inner.y >= outer.y
        and inner.right <= outer.right
        and inner.bottom <= outer.bottom
    )


def prune_free_rects(free_rects: list[Rect]) -> list[Rect]:
    result: list[Rect] = []
    for index, candidate in enumerate(free_rects):
        if any(
            index != other_index and contained(candidate, other)
            for other_index, other in enumerate(free_rects)
        ):
            continue
        if candidate not in result:
            result.append(candidate)
    return result


def try_pack(
    items: list[Item],
    width: int,
    height: int,
    gutter: int,
) -> dict[str, Rect] | None:
    free_rects = [Rect(0, 0, width, height)]
    placements: dict[str, Rect] = {}

    for item in sorted(
        items,
        key=lambda value: (
            max(value.image.width, value.image.height),
            value.image.width * value.image.height,
            value.image.height,
        ),
        reverse=True,
    ):
        packed_width = item.image.width + gutter
        packed_height = item.image.height + gutter
        candidates: list[tuple[tuple[int, int, int, int, int], Rect]] = []
        for free in free_rects:
            if packed_width > free.width or packed_height > free.height:
                continue
            leftover_width = free.width - packed_width
            leftover_height = free.height - packed_height
            score = (
                min(leftover_width, leftover_height),
                max(leftover_width, leftover_height),
                free.y,
                free.x,
                free.width * free.height,
            )
            candidates.append((score, free))

        if not candidates:
            return None

        _, chosen = min(candidates, key=lambda value: value[0])
        used = Rect(chosen.x, chosen.y, packed_width, packed_height)
        placements[item.name] = Rect(
            chosen.x,
            chosen.y,
            item.image.width,
            item.image.height,
        )

        split: list[Rect] = []
        for free in free_rects:
            split.extend(split_free_rect(free, used))
        free_rects = prune_free_rects(split)

    return placements


def compact_pack(
    items: list[Item],
    maximum_width: int,
    maximum_height: int,
    gutter: int,
    candidate_step: int,
) -> tuple[tuple[int, int], dict[str, Rect]]:
    maximum_item_width = max(item.image.width + gutter for item in items)
    maximum_item_height = max(item.image.height + gutter for item in items)
    padded_area = sum(
        (item.image.width + gutter) * (item.image.height + gutter)
        for item in items
    )
    start_width = math.ceil(maximum_item_width / candidate_step) * candidate_step
    best: tuple[tuple[int, int, int], tuple[int, int], dict[str, Rect]] | None = None

    for width in range(start_width, maximum_width + 1, candidate_step):
        minimum_height = max(maximum_item_height, math.ceil(padded_area / width))
        start_height = math.ceil(minimum_height / candidate_step) * candidate_step
        for height in range(start_height, maximum_height + 1, candidate_step):
            candidate_area = width * height
            if best is not None and candidate_area > best[0][0]:
                break
            placements = try_pack(items, width, height, gutter)
            if placements is None:
                continue

            tight_width = max(rect.right for rect in placements.values())
            tight_height = max(rect.bottom for rect in placements.values())
            tight_width = math.ceil(tight_width / 4) * 4
            tight_height = math.ceil(tight_height / 4) * 4
            score = (
                tight_width * tight_height,
                max(tight_width, tight_height),
                abs(tight_width - tight_height),
            )
            if best is None or score < best[0]:
                best = (score, (tight_width, tight_height), placements)
            break

    if best is None:
        raise RuntimeError(
            f"Could not pack sprites within {maximum_width}x{maximum_height}"
        )
    return best[1], best[2]


def write_manifest(path: Path, rows: list[dict[str, str]]) -> None:
    with path.open("w", newline="", encoding="utf-8-sig") as handle:
        writer = csv.DictWriter(handle, fieldnames=list(rows[0].keys()))
        writer.writeheader()
        writer.writerows(rows)


def main() -> None:
    parser = argparse.ArgumentParser(
        description="Compact one already-cleaned atlas with a MaxRects-style packer."
    )
    parser.add_argument("project_root", type=Path)
    parser.add_argument("--atlas", default="Backgrounds.png")
    parser.add_argument("--gutter", type=int, default=8)
    parser.add_argument("--maximum-size", type=int, default=2048)
    parser.add_argument("--candidate-step", type=int, default=32)
    args = parser.parse_args()

    project_root = args.project_root.resolve()
    asset_root = project_root / "Assets" / "MidtermSprites"
    manifest_path = asset_root / "sprite_manifest.csv"
    source_key = f"Raw/{args.atlas}"
    atlas_path = asset_root / source_key
    example_root = project_root / "Documentation" / "MidtermSpritePackingExamples"
    example_root.mkdir(parents=True, exist_ok=True)

    with manifest_path.open(encoding="utf-8-sig", newline="") as handle:
        rows = list(csv.DictReader(handle))
    atlas_rows = [row for row in rows if row["source"] == source_key]
    if not atlas_rows:
        raise RuntimeError(f"No manifest rows found for {source_key}")
    before_rows = [row.copy() for row in atlas_rows]

    source = Image.open(atlas_path).convert("RGBA")
    before_size = source.size
    items: list[Item] = []
    for row in atlas_rows:
        box = tuple(
            int(row[key]) for key in ("left", "top", "right", "bottom")
        )
        items.append(Item(row["sprite_name"], source.crop(box), row))

    packed_size, placements = compact_pack(
        items,
        maximum_width=min(args.maximum_size, before_size[0]),
        maximum_height=min(args.maximum_size, before_size[1]),
        gutter=args.gutter,
        candidate_step=args.candidate_step,
    )
    compact = Image.new("RGBA", packed_size, (0, 0, 0, 0))
    for item in items:
        rect = placements[item.name]
        compact.alpha_composite(item.image, (rect.x, rect.y))
        item.row.update(
            {
                "left": str(rect.x),
                "top": str(rect.y),
                "right": str(rect.right),
                "bottom": str(rect.bottom),
                "width": str(rect.width),
                "height": str(rect.height),
            }
        )

    stem = Path(args.atlas).stem
    before_path = example_root / f"{stem}_BeforeCompact.png"
    after_path = example_root / f"{stem}_AfterCompact.png"
    shutil.copy2(atlas_path, before_path)
    write_manifest(example_root / f"{stem}_BeforeCompact.csv", before_rows)
    compact.save(atlas_path, optimize=True)
    compact.save(after_path, optimize=True)
    write_manifest(manifest_path, rows)
    write_manifest(example_root / f"{stem}_AfterCompact.csv", atlas_rows)

    before_area = before_size[0] * before_size[1]
    after_area = packed_size[0] * packed_size[1]
    sprite_rect_area = sum(item.image.width * item.image.height for item in items)
    reduction = (1 - after_area / before_area) * 100
    stats = (
        f"Atlas: {args.atlas}\n"
        f"Sprites: {len(items)}\n"
        f"Before: {before_size[0]}x{before_size[1]} ({before_area:,} pixels)\n"
        f"After: {packed_size[0]}x{packed_size[1]} ({after_area:,} pixels)\n"
        f"Area reduction: {reduction:.2f}%\n"
        f"Rectangle fill before: {sprite_rect_area / before_area * 100:.2f}%\n"
        f"Rectangle fill after: {sprite_rect_area / after_area * 100:.2f}%\n"
        f"Gutter: {args.gutter}px\n"
    )
    (example_root / f"{stem}_CompactStats.txt").write_text(stats, encoding="utf-8")
    print(stats, end="")


if __name__ == "__main__":
    main()
