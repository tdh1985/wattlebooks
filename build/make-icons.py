# renders the wattlebooks art into every icon the app, store and mac build use
# run docs/wattlebooks/make_logo.py first. needs pillow and chrome
import io, os, pathlib, shutil, struct, subprocess, tempfile
from PIL import Image

root = pathlib.Path(__file__).resolve().parent.parent
art = root / 'docs' / 'wattlebooks'
msix = root / 'build' / 'msix' / 'Assets'
work = pathlib.Path(tempfile.gettempdir()) / 'wattlebooks-icons'
chrome = os.environ.get('CHROME', r'C:\Program Files\Google\Chrome\Application\chrome.exe')
big, small = art / 'wattlebooks-icon.svg', art / 'wattlebooks-icon-small.svg'

def render(svg, w, h=None, pad=0):
    h = h or w
    out = work / f'{svg.stem}-{w}x{h}-{pad}.png'
    page = out.with_suffix('.html')
    page.write_text(f'<!doctype html><style>html,body{{margin:0;background:transparent}}'
                    f'img{{display:block;width:{w - 2 * pad}px;height:{h - 2 * pad}px;margin:{pad}px}}</style>'
                    f'<img src="{svg.as_uri()}">')
    subprocess.run([chrome, '--headless=new', '--disable-gpu', '--hide-scrollbars', '--default-background-color=00000000',
                    f'--window-size={w},{h}', f'--screenshot={out}', page.as_uri()], check=True, capture_output=True)
    return Image.open(out).convert('RGBA')

def icon(px):
    # below about 50px the sprig turns to mush, so the simpler art takes over
    return render(small if px <= 50 else big, px)

def save(im, path):
    im.save(path)
    print(path.relative_to(root), im.size)

def write_ico(path, images):
    blobs = []
    for im in images:
        buf = io.BytesIO()
        im.save(buf, 'PNG')
        blobs.append((im.width, buf.getvalue()))
    offset = 6 + 16 * len(blobs)
    head, body = struct.pack('<HHH', 0, 1, len(blobs)), b''
    for w, data in blobs:
        head += struct.pack('<BBBBHHII', w % 256, w % 256, 0, 0, 1, 32, len(data), offset)
        offset += len(data)
        body += data
    path.write_bytes(head + body)
    print(path.relative_to(root), [im.width for im in images])

work.mkdir(parents=True, exist_ok=True)

for px in (16, 24, 32, 48, 256):
    im = icon(px)
    save(im, msix / f'Square44x44Logo.targetsize-{px}.png')
    save(im, msix / f'Square44x44Logo.targetsize-{px}_altform-unplated.png')
for scale, px in ((100, 44), (200, 88)):
    save(icon(px), msix / f'Square44x44Logo.scale-{scale}.png')
for scale, px in ((100, 50), (200, 100)):
    save(icon(px), msix / f'StoreLogo.scale-{scale}.png')
for scale, k in ((100, 1), (200, 2)):
    save(render(art / 'wattlebooks-tile.svg', 150 * k), msix / f'Square150x150Logo.scale-{scale}.png')
    save(render(art / 'wattlebooks-tile-wide.svg', 310 * k, 150 * k), msix / f'Wide310x150Logo.scale-{scale}.png')

write_ico(root / 'src' / 'Wattlebooks.App' / 'Assets' / 'app.ico', [icon(px) for px in (16, 24, 32, 48, 64, 128, 256)])
save(icon(256), root / 'src' / 'Wattlebooks.Desktop' / 'Assets' / 'icon.png')

# mac icons sit inside a margin so they line up with the rest of the dock
mac = render(big, 1024, pad=100)
mac.save(root / 'src' / 'Wattlebooks.Desktop' / 'Mac' / 'Wattlebooks.icns', format='ICNS')
print('src/Wattlebooks.Desktop/Mac/Wattlebooks.icns', mac.size)

img = root / 'src' / 'Wattlebooks.Ui' / 'wwwroot' / 'img'
img.mkdir(exist_ok=True)
for name in ('wattlebooks-icon.svg', 'wattlebooks-icon-small.svg', 'wattlebooks-logo.svg', 'wattlebooks-logo-dark.svg',
             'wattlebooks-wordmark.svg'):
    shutil.copyfile(art / name, img / name)
    print(f'wwwroot/img/{name}')
