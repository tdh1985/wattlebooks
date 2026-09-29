# wraps the artifact page in a full document for github pages
import datetime, html, json, re, shutil, sys, pathlib
here = pathlib.Path(__file__).parent
site = here / 'site'
out = pathlib.Path(sys.argv[1])
url = 'https://tdh1985.github.io/invoicedesk/'
repo = 'https://github.com/tdh1985/invoicedesk'
title = 'InvoiceDesk: free invoice app for sole traders, GST and BAS'
og_alt = 'InvoiceDesk: tax invoices, GST and your BAS with no monthly fee, beside the invoice editor'
# bing and other indexnow engines fetch this file to trust our pings
indexnow_key = 'd7f1989c9676aa3d101197f4be473fc2'
# search console looks for this file to prove we own the site
google_file = 'google071558514d1d4664.html'
bing_code = '77C5D8499272D6A84A16324C05F488DB'
today = datetime.date.today().isoformat()
version = re.search(r'<Version>([^<]+)', (here.parent / 'Directory.Build.props').read_text()).group(1)
img = lambda name: url + 'img/' + name
shots = ['invoice-preview.webp', 'dashboard.webp', 'receipt-scan.webp', 'reports.webp']

def text(markup):
    return ' '.join(html.unescape(re.sub(r'<[^>]+>', '', markup)).split())

def document(head, css, body):
    return ('<!doctype html>\n<html lang="en-AU">\n<head>\n<meta charset="utf-8">\n'
            '<meta name="viewport" content="width=device-width, initial-scale=1, viewport-fit=cover">\n'
            + head + '<style>' + css + '</style>\n</head>\n<body>\n' + body + '</body>\n</html>\n')

def common(page_url):
    return (f'<link rel="canonical" href="{page_url}">\n'
            '<meta name="robots" content="max-image-preview:large">\n'
            '<meta name="theme-color" content="#F5F6F2" media="(prefers-color-scheme: light)">\n'
            '<meta name="theme-color" content="#0E1424" media="(prefers-color-scheme: dark)">\n'
            '<link rel="icon" href="img/logo.png">\n')

s = (site / 'index.html').read_text(encoding='utf-8')
head, rest = s.split('<style>', 1)
css, body = rest.split('</style>\n', 1)
head = head.replace('<title>InvoiceDesk</title>', f'<title>{title}</title>')
desc = html.unescape(re.search(r'<meta name="description" content="([^"]+)"', head).group(1))
fonts = ''.join(re.findall(r'<link [^>]+>\n', head))

# built from the page's own questions so the two can't drift apart
faq = [{'@type': 'Question', 'name': text(q), 'acceptedAnswer': {'@type': 'Answer', 'text': text(a)}}
       for q, a in re.findall(r'<summary>(.*?)</summary>\s*<p>(.*?)</p>', body, re.S)]
vtt = (site / 'img' / 'promo.vtt').read_text(encoding='utf-8').splitlines()
transcript = ' '.join(l.strip() for l in vtt if l.strip() and '-->' not in l and not l.startswith('WEBVTT'))

graph = [
    {'@type': 'Organization', '@id': url + '#org', 'name': 'InvoiceDesk', 'url': url,
     'logo': img('logo.png'), 'sameAs': [repo]},
    {'@type': 'WebSite', '@id': url + '#website', 'url': url, 'name': 'InvoiceDesk',
     'inLanguage': 'en-AU', 'publisher': {'@id': url + '#org'}},
    {'@type': ['WebPage', 'FAQPage'], '@id': url + '#page', 'url': url, 'name': title,
     'description': desc, 'inLanguage': 'en-AU', 'dateModified': today,
     'isPartOf': {'@id': url + '#website'}, 'about': {'@id': url + '#app'},
     'primaryImageOfPage': img('og.png'), 'mainEntity': faq},
    {'@type': 'SoftwareApplication', '@id': url + '#app', 'name': 'InvoiceDesk', 'url': url,
     'description': desc, 'applicationCategory': 'BusinessApplication',
     'applicationSubCategory': 'Invoicing', 'operatingSystem': 'Windows 10, Windows 11, macOS, Linux',
     'softwareVersion': version, 'downloadUrl': repo + '/releases/latest',
     'releaseNotes': repo + '/releases', 'license': 'https://opensource.org/licenses/MIT',
     'isAccessibleForFree': True, 'offers': {'@type': 'Offer', 'price': '0', 'priceCurrency': 'AUD'},
     'countriesSupported': 'AU, NZ, GB, CA, US', 'inLanguage': 'en',
     'featureList': [
         'Tax invoices with GST, VAT or sales tax',
         'Quotes that turn into invoices',
         'Repeating invoices',
         'Payment reminders and client statements',
         'Receipt scanning on Windows',
         'BAS worksheet and tax return figures for Australia, New Zealand, the UK, Canada and the US',
         'Profit and loss reports as PDF or CSV',
         'Billing in 13 currencies',
         'Optional phone app for receipts and quick invoices',
     ],
     'image': img('og.png'), 'screenshot': [img(n) for n in shots],
     'publisher': {'@id': url + '#org'}, 'sameAs': [repo]},
    {'@type': 'VideoObject', '@id': url + '#video', 'name': 'InvoiceDesk in under a minute',
     'description': 'What InvoiceDesk does, from your first invoice to your GST, VAT or sales tax figures, '
                    'for Australia, New Zealand, the UK, Canada and the US, on Windows, Mac and Linux.',
     'thumbnailUrl': img('promo-poster.webp'), 'contentUrl': img('promo.mp4'),
     'uploadDate': '2026-09-30T12:00:00+10:00', 'duration': 'PT54S', 'inLanguage': 'en-AU',
     'transcript': transcript},
]
ld = json.dumps({'@context': 'https://schema.org', '@graph': graph}, ensure_ascii=False, indent=1)
meta = (common(url)
        + f'<meta name="msvalidate.01" content="{bing_code}">\n'
        '<meta property="og:type" content="website">\n'
        '<meta property="og:site_name" content="InvoiceDesk">\n'
        '<meta property="og:locale" content="en_AU">\n'
        f'<meta property="og:url" content="{url}">\n'
        '<meta property="og:title" content="InvoiceDesk: free invoicing for sole traders">\n'
        '<meta property="og:description" content="Free invoicing for sole traders. Tax invoices, GST and your BAS, with no monthly fee.">\n'
        f'<meta property="og:image" content="{img("og.png")}">\n'
        '<meta property="og:image:width" content="1200">\n'
        '<meta property="og:image:height" content="630">\n'
        f'<meta property="og:image:alt" content="{og_alt}">\n'
        '<meta name="twitter:card" content="summary_large_image">\n'
        f'<meta name="twitter:image:alt" content="{og_alt}">\n'
        '<script type="application/ld+json">\n' + ld.replace('</', '<\\/') + '\n</script>\n')
(out / 'index.html').write_text(document(head + meta, css, body), encoding='utf-8', newline='\n')

# the privacy page borrows the main stylesheet and adds a few rules of its own
p = (site / 'privacy.html').read_text(encoding='utf-8')
p_head, p_rest = p.split('<style>', 1)
p_css, p_body = p_rest.split('</style>\n', 1)
p_url = url + 'privacy.html'
(out / 'privacy.html').write_text(document(p_head + fonts + common(p_url), css + p_css, p_body),
                                  encoding='utf-8', newline='\n')

pictures = ''.join(f'<image:image><image:loc>{img(n)}</image:loc></image:image>\n' for n in shots)
(out / 'sitemap.xml').write_text(
    '<?xml version="1.0" encoding="UTF-8"?>\n'
    '<urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9" '
    'xmlns:image="http://www.google.com/schemas/sitemap-image/1.1">\n'
    f'<url>\n<loc>{url}</loc>\n<lastmod>{today}</lastmod>\n{pictures}</url>\n'
    f'<url>\n<loc>{p_url}</loc>\n<lastmod>{today}</lastmod>\n</url>\n'
    '</urlset>\n', encoding='utf-8', newline='\n')
(out / 'llms.txt').write_text((site / 'llms.txt').read_text(encoding='utf-8').replace('{version}', version),
                              encoding='utf-8', newline='\n')
(out / f'{indexnow_key}.txt').write_text(indexnow_key, encoding='utf-8')
(out / google_file).write_text(f'google-site-verification: {google_file}', encoding='utf-8')

(out / 'img').mkdir(exist_ok=True)
for f in (site / 'img').iterdir():
    if f.suffix in ('.webp', '.mp4', '.vtt') or f.name.startswith('logo'):
        shutil.copy2(f, out / 'img' / f.name)
print('built', out, 'version', version, 'questions', len(faq))
