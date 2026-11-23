"""Generate Windows icon assets from the supplied logo (requires Pillow)."""
from pathlib import Path
import argparse
import struct
from PIL import Image

SIZES = (16, 20, 24, 28, 32, 40, 48, 56, 64, 72, 80, 96, 128, 192, 256)
TRAY_SIZES = (16, 20, 24, 28, 32, 40, 48, 64)


def write_ico(path, image, sizes):
    # Explicit 32-bit DIB + AND masks work across Windows icon consumers.
    entries, payloads = [], []
    offset = 6 + 16 * len(sizes)
    for size in sizes:
        frame = image.resize((size, size), Image.Resampling.LANCZOS).convert('RGBA')
        flipped = frame.transpose(Image.Transpose.FLIP_TOP_BOTTOM)
        pixels = flipped.tobytes('raw', 'BGRA')
        stride = ((size + 31) // 32) * 4
        mask = bytearray(stride * size)
        opacity = flipped.getchannel('A')
        for y in range(size):
            for x in range(size):
                if opacity.getpixel((x,y)) == 0:
                    mask[y*stride+x//8] |= 0x80 >> (x%8)
        header = struct.pack('<IiiHHIIiiII', 40, size, size*2, 1, 32, 0, len(pixels), 0, 0, 0, 0)
        payload = header + pixels + mask
        entries.append(struct.pack('<BBBBHHII', size%256, size%256, 0, 0, 1, 32, len(payload), offset))
        payloads.append(payload)
        offset += len(payload)
    path.write_bytes(struct.pack('<HHH', 0, 1, len(sizes)) + b''.join(entries) + b''.join(payloads))

def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('source', type=Path)
    parser.add_argument('--out', type=Path, default=Path(__file__).resolve().parents[1] / 'app' / 'assets')
    args = parser.parse_args()
    args.out.mkdir(parents=True, exist_ok=True)
    logo = Image.open(args.source).convert('RGBA')
    canvas = Image.new('RGBA', (max(logo.size),) * 2)
    canvas.alpha_composite(logo, ((canvas.width-logo.width)//2, (canvas.height-logo.height)//2))
    canvas.save(args.out / 'AetherDrop-logo.png')
    write_ico(args.out / 'AetherDrop.ico', canvas, SIZES)
    # Preserve the supplied PNG's background and original alpha, including its corners.
    tray = canvas.copy()
    write_ico(args.out / 'AetherDrop-tray.ico', tray, TRAY_SIZES)
    canvas.resize((150,150), Image.Resampling.LANCZOS).save(args.out / 'logo150.png')
    canvas.resize((44,44), Image.Resampling.LANCZOS).save(args.out / 'logo44.png')
    icons = args.out / 'icons'
    icons.mkdir(exist_ok=True)
    for size in SIZES:
        small = canvas.resize((size,size), Image.Resampling.LANCZOS)
        small.save(icons / f'AetherDrop-{size}.png')
        write_ico(icons / f'AetherDrop-{size}.ico', canvas, (size,))
    for size in TRAY_SIZES:
        write_ico(icons / f'AetherDrop-tray-{size}.ico', tray, (size,))
    print('Desktop/taskbar ICO layers:', ', '.join(map(str,SIZES)))
    print('Tray ICO layers:', ', '.join(map(str,TRAY_SIZES)))

if __name__ == '__main__':
    main()
