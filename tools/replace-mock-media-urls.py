from zipfile import ZipFile, ZIP_DEFLATED
from pathlib import Path
from copy import deepcopy
import xml.etree.ElementTree as ET

src = Path(r"D:\Capstone\BE_JP\RikiPath.Backend\Data Import Bulk - TEST 5000.xlsx")
dst = src.with_name("Data Import Bulk - TEST 5000-media-urls.xlsx")
MP3 = "https://meetings-vessels-towards-reading.trycloudflare.com/api/audio/content/900004/d25dd54b48dd445d93c8229992243bc6.mp3"
IMG = "https://iomeiswoyuzatihtccln.supabase.co/storage/v1/object/public/rikipath-media/jlpt-images/4cca3f46d094423e88391e1fbf3ef738.jpg"
NS = "http://schemas.openxmlformats.org/spreadsheetml/2006/main"
ET.register_namespace('', NS)

def col_number(ref):
    n = 0
    for ch in ref:
        if ch.isalpha(): n = n * 26 + ord(ch.upper()) - 64
        else: break
    return n

def text_of(cell, shared):
    if cell.get('t') == 'inlineStr':
        return ''.join(cell.itertext())
    value = cell.find(f'{{{NS}}}v')
    if value is None or value.text is None: return ''
    if cell.get('t') == 's': return shared[int(value.text)]
    return value.text

with ZipFile(src, 'r') as zin:
    shared = []
    if 'xl/sharedStrings.xml' in zin.namelist():
        root = ET.fromstring(zin.read('xl/sharedStrings.xml'))
        shared = [''.join(si.itertext()) for si in root.findall(f'{{{NS}}}si')]
    sheet = ET.fromstring(zin.read('xl/worksheets/sheet5.xml'))
    rows = sheet.findall(f'.//{{{NS}}}sheetData/{{{NS}}}row')
    headers = {}
    for c in rows[0].findall(f'{{{NS}}}c'):
        headers[text_of(c, shared).strip().lower()] = col_number(c.get('r', ''))
    audio_col = headers.get('audiourl')
    image_col = headers.get('imageurl')
    if not audio_col or not image_col:
        raise SystemExit(f'Missing Mock Test columns: {headers}')
    changed_audio = changed_image = 0
    for row in rows[1:]:
        for c in row.findall(f'{{{NS}}}c'):
            col = col_number(c.get('r', ''))
            value = MP3 if col == audio_col else IMG if col == image_col else None
            if value is None: continue
            for child in list(c): c.remove(child)
            c.set('t', 'inlineStr')
            is_node = ET.SubElement(c, f'{{{NS}}}is')
            ET.SubElement(is_node, f'{{{NS}}}t').text = value
            if col == audio_col: changed_audio += 1
            else: changed_image += 1
    updated = ET.tostring(sheet, encoding='utf-8', xml_declaration=True)
    with ZipFile(dst, 'w', ZIP_DEFLATED) as zout:
        for item in zin.infolist():
            zout.writestr(item, updated if item.filename == 'xl/worksheets/sheet5.xml' else zin.read(item.filename))
print(f'{dst}\nAudioUrl changed: {changed_audio}\nImageUrl changed: {changed_image}')
