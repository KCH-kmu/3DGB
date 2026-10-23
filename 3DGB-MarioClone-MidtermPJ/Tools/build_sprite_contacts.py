from __future__ import annotations

import argparse
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont


def build_contact_sheet(source: Path, output: Path) -> None:
    files = sorted(source.glob("*.png"))
    columns = 10
    cell_width = 128
    cell_height = 128
    label_height = 22
    rows = (len(files) + columns - 1) // columns
    sheet = Image.new(
        "RGBA",
        (columns * cell_width, rows * (cell_height + label_height)),
        (35, 38, 45, 255),
    )
    draw = ImageDraw.Draw(sheet)
    font = ImageFont.load_default()

    for index, path in enumerate(files):
        image = Image.open(path).convert("RGBA")
        image.thumbnail((cell_width - 12, cell_height - 12), Image.Resampling.LANCZOS)
        column = index % columns
        row = index // columns
        x = column * cell_width + (cell_width - image.width) // 2
        y = row * (cell_height + label_height) + (cell_height - image.height) // 2
        checker = Image.new("RGBA", (cell_width, cell_height), (245, 245, 245, 255))
        checker_draw = ImageDraw.Draw(checker)
        step = 12
        for checker_y in range(0, cell_height, step):
            for checker_x in range(0, cell_width, step):
                if (checker_x // step + checker_y // step) % 2:
                    checker_draw.rectangle(
                        (checker_x, checker_y, checker_x + step - 1, checker_y + step - 1),
                        fill=(210, 210, 210, 255),
                    )
        sheet.alpha_composite(checker, (column * cell_width, row * (cell_height + label_height)))
        sheet.alpha_composite(image, (x, y))
        label = path.stem
        label_x = column * cell_width + 4
        label_y = row * (cell_height + label_height) + cell_height + 4
        draw.text((label_x, label_y), label, fill=(255, 255, 255, 255), font=font)

    output.parent.mkdir(parents=True, exist_ok=True)
    sheet.convert("RGB").save(output, quality=90)


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("processed_root", type=Path)
    parser.add_argument("output_root", type=Path)
    args = parser.parse_args()
    for source in sorted(path for path in args.processed_root.iterdir() if path.is_dir()):
        build_contact_sheet(source, args.output_root / f"{source.name}.jpg")


if __name__ == "__main__":
    main()
