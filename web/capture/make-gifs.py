"""Turns the step pictures of capture/flows.spec.ts into one GIF per flow.

    python capture/make-gifs.py

Reads web/test-results/capture-frames/<flow>/NN-label.png and writes docs/assets/flows/<flow>.gif. It needs Python 3 and Pillow
(`pip install pillow`); this is a documentation helper, not part of the application, so it is not a dependency of the project.
"""

from pathlib import Path

from PIL import Image

ROOT = Path(__file__).resolve().parent.parent
FRAMES = ROOT / "test-results" / "capture-frames"
OUT = ROOT.parent / "docs" / "assets" / "flows"
WIDTH, HEIGHT = 960, 600
BACKGROUND = (248, 250, 252)
SECONDS_PER_STEP = 2.2


def fit(image: Image.Image) -> Image.Image:
    """Scales a picture into the common canvas without changing its proportions, so desktop and phone steps can share one GIF."""
    image = image.convert("RGB")
    scale = min(WIDTH / image.width, HEIGHT / image.height)
    resized = image.resize((max(1, round(image.width * scale)), max(1, round(image.height * scale))), Image.LANCZOS)
    canvas = Image.new("RGB", (WIDTH, HEIGHT), BACKGROUND)
    canvas.paste(resized, ((WIDTH - resized.width) // 2, (HEIGHT - resized.height) // 2))
    return canvas.quantize(colors=128, method=Image.Quantize.MEDIANCUT)


def main() -> None:
    OUT.mkdir(parents=True, exist_ok=True)
    flows = sorted(path for path in FRAMES.iterdir() if path.is_dir()) if FRAMES.exists() else []
    if not flows:
        raise SystemExit(f"No frames in {FRAMES}. Run the flows first: npx playwright test -c playwright.capture.config.ts flows.spec.ts")

    for flow in flows:
        steps = sorted(flow.glob("*.png"))
        pictures = [fit(Image.open(step)) for step in steps]
        target = OUT / f"{flow.name}.gif"
        pictures[0].save(
            target,
            save_all=True,
            append_images=pictures[1:],
            duration=int(SECONDS_PER_STEP * 1000),
            loop=0,
            optimize=True,
        )
        print(f"{target.relative_to(ROOT.parent)}: {len(pictures)} steps, {target.stat().st_size // 1024} KB")


if __name__ == "__main__":
    main()
