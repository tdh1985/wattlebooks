# draws the wattlebooks logo, dark logo and app icon as svg and png
# needs fonttools and uharfbuzz, and chrome for the png renders
import os, pathlib, re, subprocess, tempfile, urllib.request
import uharfbuzz as hb
from fontTools.pens.boundsPen import BoundsPen
from fontTools.pens.svgPathPen import SVGPathPen
from fontTools.pens.transformPen import TransformPen

here = pathlib.Path(__file__).parent
work = pathlib.Path(tempfile.gettempdir()) / 'wattlebooks'
chrome = os.environ.get('CHROME', r'C:\Program Files\Google\Chrome\Application\chrome.exe')
lora_url = 'https://raw.githubusercontent.com/google/fonts/main/ofl/lora/Lora%5Bwght%5D.ttf'
navy = '#16325C'
modes = {
    'light': dict(bg='#FFFFFF', text=navy, leaf='#5E8F3E', rib='#7FAE5A', stem='#4B6B2A', gold='#F2B233',
                  amber='#E0A033', cover=navy, paper='#E6ECF5', line='#AEBBD0', spine='#C9D3E2'),
    'dark': dict(bg='#0F1524', text='#E6E9F1', leaf='#8DBB63', rib='#A9D07F', stem='#79A052', gold='#F6C03F',
                 amber='#F0B64E', cover='#7F8DAA', paper='#E6E9F1', line='#9AA6BE', spine='#C9D0DE'),
    'icon': dict(bg=navy, text='#FFFFFF', leaf='#8DBB63', rib='#A9D07F', stem='#8DBB63', gold='#F6C03F',
                 amber='#F0B64E', cover='#A9B4C8', paper='#FFFFFF', line='#C5CEDC', spine='#DCE2EC'),
}
raceme = [(0, 0), (-1.3, 0.7), (1.2, -0.9), (1.4, 1.0), (-0.4, -1.6), (-1.6, -0.8), (0.3, 1.7)]
small = [(0, 0), (-1.25, 0.8), (1.2, -0.7), (0.2, -1.55), (1.3, 1.0)]

def puff(x, y, r, fill):
    # the dotted rim is what makes a plain disc read as a fluffy wattle ball
    return (f'<circle cx="{x:.1f}" cy="{y:.1f}" r="{r:.1f}" fill="{fill}"/>'
            f'<circle cx="{x:.1f}" cy="{y:.1f}" r="{r * 1.08:.1f}" fill="none" stroke="{fill}" '
            f'stroke-width="{r * 0.36:.1f}" stroke-dasharray="0 {r * 0.46:.2f}" stroke-linecap="round"/>')

def leaf(x, y, length, width, angle, p, flip=False):
    f, L, W = (-1 if flip else 1), length, width
    d = (f'M0,0 C{0.25 * L:.1f},{-W * f:.1f} {0.7 * L:.1f},{-0.75 * W * f:.1f} {L:.1f},0 '
         f'C{0.7 * L:.1f},{0.12 * W * f:.1f} {0.25 * L:.1f},{0.35 * W * f:.1f} 0,0Z')
    rib = (f'<path d="M{0.06 * L:.1f},{-0.05 * W * f:.1f} Q{0.5 * L:.1f},{-0.42 * W * f:.1f} {0.94 * L:.1f},{-0.03 * W * f:.1f}" '
           f'fill="none" stroke="{p["rib"]}" stroke-width="{max(2, W * 0.09):.1f}" stroke-linecap="round"/>')
    return f'<g transform="translate({x},{y}) rotate({angle})"><path d="{d}" fill="{p["leaf"]}"/>{rib}</g>'

def cluster(cx, cy, r, offsets, p):
    return ''.join(puff(cx + dx * r * 1.12, cy + dy * r * 1.12, r, p['gold'] if i % 2 == 0 else p['amber'])
                   for i, (dx, dy) in enumerate(offsets))

def stalk(d, p, width=7):
    return f'<path d="{d}" fill="none" stroke="{p["stem"]}" stroke-width="{width}" stroke-linecap="round"/>'

def mirror(d):
    return re.sub(r'(-?\d+(?:\.\d+)?),(-?\d+(?:\.\d+)?)', lambda m: f'{600 - float(m.group(1)):g},{m.group(2)}', d)

def book(p):
    cover = 'M300,470 C250,442 170,436 90,452 L90,546 C170,530 250,534 300,562 Z'
    page = 'M300,455 C250,425 175,420 105,437 L105,528 C175,513 250,518 300,548 Z'
    s = [f'<path d="{cover}" fill="{p["cover"]}"/><path d="{mirror(cover)}" fill="{p["cover"]}"/>',
         f'<path d="{page}" fill="{p["paper"]}"/><path d="{mirror(page)}" fill="{p["paper"]}"/>',
         f'<path d="M300,462 V545" stroke="{p["spine"]}" stroke-width="4"/>']
    # the short first line echoes the heading line on an invoice docket
    for drop, end in ((34, 200), (56, 135), (78, 135)):
        line = f'M278,{452 + drop} C245,{428 + drop} 190,{423 + drop} {end},{433 + drop - (3 if end == 200 else 0)}'
        for d in (line, mirror(line)):
            s.append(f'<path d="{d}" fill="none" stroke="{p["line"]}" stroke-width="5" stroke-linecap="round"/>')
    return ''.join(s)

def mark(p):
    return ''.join([
        stalk('M300,470 C298,390 296,330 318,262 C336,205 368,160 405,128', p, 10),
        stalk('M299,392 C280,360 252,330 222,306', p),
        stalk('M314,282 C340,268 368,258 398,250', p),
        leaf(297, 425, 178, 38, -160, p),
        leaf(303, 350, 150, 32, -20, p, flip=True),
        leaf(322, 262, 118, 27, -128, p),
        cluster(214, 298, 19, small, p),
        cluster(408, 246, 18, small, p),
        cluster(412, 118, 21, raceme, p),
        book(p),
    ])

def lora():
    font = work / 'Lora[wght].ttf'
    if not font.exists():
        work.mkdir(parents=True, exist_ok=True)
        urllib.request.urlretrieve(lora_url, font)
    return font.read_bytes()

def wordmark(centre, baseline, size):
    # outlined so the logo looks the same on machines without lora
    face, glyphs, advance = hb.Face(lora()), [], 0
    for text, weight in (('Wattle', 700), ('books', 500)):
        font = hb.Font(face)
        font.set_variations({'wght': weight})
        buf = hb.Buffer()
        buf.add_str(text)
        buf.guess_segment_properties()
        hb.shape(font, buf)
        for info, pos in zip(buf.glyph_infos, buf.glyph_positions):
            glyphs.append((font, info.codepoint, advance + pos.x_offset, pos.y_offset))
            advance += pos.x_advance
    scale = size / face.upem
    left = centre - advance * scale / 2
    pen = SVGPathPen(None, ntos=lambda v: f'{v:.1f}'.rstrip('0').rstrip('.'))
    bounds = BoundsPen(None)
    for font, gid, x, y in glyphs:
        flip = (scale, 0, 0, -scale, left + x * scale, baseline - y * scale)
        font.draw_glyph_with_pen(gid, TransformPen(pen, flip))
        font.draw_glyph_with_pen(gid, TransformPen(bounds, flip))
    return pen.getCommands(), bounds.bounds

def logo(mode):
    p = modes[mode]
    d, (x1, _, x2, y2) = wordmark(350, 738, 100)
    # the mark sits at 140..560 across and 84..592 down once shifted by (50,30)
    left, top, right, bottom = min(x1, 140) - 30, 84 - 30, max(x2, 560) + 30, y2 + 30
    box = (round(left), round(top), round(right - left), round(bottom - top))
    return (f'<svg xmlns="http://www.w3.org/2000/svg" viewBox="{" ".join(map(str, box))}">'
            f'<g transform="translate(50,30)">{mark(p)}</g><path d="{d}" fill="{p["text"]}"/></svg>'), box[2:]

def icon():
    p = modes['icon']
    return (f'<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 600 600"><rect width="600" height="600" rx="120" '
            f'fill="{p["bg"]}"/><g transform="translate(30,23) scale(.9)">{mark(p)}</g></svg>'), (600, 600)

def small_icon():
    # for 16 to 48px, where the sprig's fine detail turns to mush
    p = modes['icon']
    cover = 'M300,380 C245,352 160,346 78,362 L78,514 C160,498 245,502 300,534 Z'
    page = 'M300,360 C245,330 165,326 95,344 L95,492 C165,476 245,480 300,510 Z'
    blooms = ''.join(f'<circle cx="{x}" cy="{y}" r="62" fill="{p[c]}" stroke="{navy}" stroke-width="12" paint-order="stroke"/>'
                     for x, y, c in ((244, 222, 'amber'), (356, 222, 'gold'), (300, 128, 'gold')))
    leaf_shape = 'M0,0 C40,-56 112,-42 160,0 C112,6.7 40,19.6 0,0Z'
    return (f'<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 600 600"><rect width="600" height="600" rx="120" '
            f'fill="{p["bg"]}"/><path d="M300,372 C300,320 302,280 300,232" fill="none" stroke="{p["leaf"]}" '
            f'stroke-width="30" stroke-linecap="round"/><path transform="translate(300,330) rotate(-152)" '
            f'd="{leaf_shape}" fill="{p["leaf"]}"/><path d="{cover}" fill="{p["cover"]}"/>'
            f'<path d="{mirror(cover)}" fill="{p["cover"]}"/><path d="{page}" fill="{p["paper"]}"/>'
            f'<path d="{mirror(page)}" fill="{p["paper"]}"/><path d="M300,366 V506" stroke="{navy}" '
            f'stroke-width="14"/>{blooms}</svg>')

def png(svg, size, bg, scale=2, out=None):
    w, h = size[0] * scale, size[1] * scale
    out = out or svg.with_suffix('.png')
    page = work / (out.stem + '.html')
    page.write_text(f'<!doctype html><style>html,body{{margin:0;background:{bg}}}img{{display:block;width:{w}px;height:{h}px}}'
                    f'</style><img src="{svg.as_uri()}">')
    subprocess.run([chrome, '--headless=new', '--disable-gpu', '--hide-scrollbars', '--default-background-color=00000000',
                    f'--window-size={w},{h}', f'--screenshot={out}', page.as_uri()],
                   check=True, capture_output=True)

work.mkdir(parents=True, exist_ok=True)
for name, (svg, size), bg in (('wattlebooks-logo', logo('light'), modes['light']['bg']),
                              ('wattlebooks-logo-dark', logo('dark'), modes['dark']['bg']),
                              ('wattlebooks-icon', icon(), 'transparent')):
    out = here / f'{name}.svg'
    out.write_text(svg, encoding='utf-8')
    png(out, size, bg)
    print(out.name, size)

small_svg = here / 'wattlebooks-icon-small.svg'
small_svg.write_text(small_icon(), encoding='utf-8')
# rendered at true size since these are the pixels windows actually shows
for px in (16, 24, 32, 48):
    png(small_svg, (px, px), 'transparent', scale=1, out=here / f'wattlebooks-icon-small-{px}.png')
print(small_svg.name, '16 24 32 48')
