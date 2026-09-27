# wraps the artifact page in a full document for github pages
import shutil, sys, pathlib
site = pathlib.Path(__file__).parent / 'site'
out = pathlib.Path(sys.argv[1])
s = (site / 'index.html').read_text(encoding='utf-8')
head, rest = s.split('<style>', 1)
head = head.replace('<title>InvoiceDesk</title>', '<title>InvoiceDesk: free invoicing for sole traders</title>')
meta = ('<link rel="icon" href="img/logo.png">\n'
        '<meta property="og:title" content="InvoiceDesk">\n'
        '<meta property="og:description" content="Free invoicing for sole traders. Tax invoices, GST and your BAS, with no monthly fee.">\n'
        '<meta property="og:image" content="https://tdh1985.github.io/invoicedesk/img/og.png">\n'
        '<meta name="twitter:card" content="summary_large_image">\n')
doc = ('<!doctype html>\n<html lang="en">\n<head>\n<meta charset="utf-8">\n'
       '<meta name="viewport" content="width=device-width, initial-scale=1, viewport-fit=cover">\n'
       + head + meta + '<style>' + rest.replace('</style>\n', '</style>\n</head>\n<body>\n', 1) + '</body>\n</html>\n')
(out / 'index.html').write_text(doc, encoding='utf-8', newline='\n')
(out / 'img').mkdir(exist_ok=True)
for f in (site / 'img').iterdir():
    if f.suffix in ('.webp', '.mp4') or f.name.startswith('logo'):
        shutil.copy2(f, out / 'img' / f.name)
print('built', out)
