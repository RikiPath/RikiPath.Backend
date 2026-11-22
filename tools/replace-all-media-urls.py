from pathlib import Path
from zipfile import ZipFile, ZIP_DEFLATED
import xml.etree.ElementTree as ET

SOURCE = Path(r"D:\Capstone\BE_JP\RikiPath.Backend\Data Import Bulk - TEST 5000-media-urls.xlsx")
OUTPUT = SOURCE.with_name("Data Import Bulk - TEST 5000-media-urls-updated.xlsx")
IMAGE_URL = "https://iomeiswoyuzatihtccln.supabase.co/storage/v1/object/public/rikipath-media/jlpt-images/4cca3f46d094423e88391e1fbf3ef738.jpg"
AUDIO_URL = "https://meetings-vessels-towards-reading.trycloudflare.com/api/audio/content/900004/d25dd54b48dd445d93c8229992243bc6.mp3"
MAIN_NS = "http://schemas.openxmlformats.org/spreadsheetml/2006/main"
REL_NS = "http://schemas.openxmlformats.org/officeDocument/2006/relationships"
ET.register_namespace("", MAIN_NS)


def column_number(reference):
    value = 0
    for character in reference:
        if not character.isalpha():
            break
        value = value * 26 + ord(character.upper()) - ord("A") + 1
    return value


def cell_text(cell, shared_strings):
    if cell.get("t") == "inlineStr":
        return "".join(cell.itertext())
    value = cell.find(f"{{{MAIN_NS}}}v")
    if value is None or value.text is None:
        return ""
    if cell.get("t") == "s":
        return shared_strings[int(value.text)]
    return value.text


def set_inline_string(cell, value):
    for child in list(cell):
        cell.remove(child)
    cell.set("t", "inlineStr")
    inline = ET.SubElement(cell, f"{{{MAIN_NS}}}is")
    ET.SubElement(inline, f"{{{MAIN_NS}}}t").text = value


def is_media_header(header):
    normalized = header.strip().lower()
    return normalized.endswith("audiourl"), normalized.endswith("imageurl")


with ZipFile(SOURCE, "r") as source_zip:
    shared_strings = []
    if "xl/sharedStrings.xml" in source_zip.namelist():
        shared_root = ET.fromstring(source_zip.read("xl/sharedStrings.xml"))
        shared_strings = ["".join(item.itertext()) for item in shared_root.findall(f"{{{MAIN_NS}}}si")]

    workbook_root = ET.fromstring(source_zip.read("xl/workbook.xml"))
    relationships = ET.fromstring(source_zip.read("xl/_rels/workbook.xml.rels"))
    targets = {
        relationship.get("Id"): relationship.get("Target", "").lstrip("/")
        for relationship in relationships
    }
    updates = {}

    for sheet in workbook_root.find(f"{{{MAIN_NS}}}sheets"):
        relationship_id = sheet.get(f"{{{REL_NS}}}id")
        sheet_path = targets[relationship_id]
        root = ET.fromstring(source_zip.read(sheet_path))
        rows = root.findall(f".//{{{MAIN_NS}}}sheetData/{{{MAIN_NS}}}row")
        if not rows:
            continue

        headers = {}
        for cell in rows[0].findall(f"{{{MAIN_NS}}}c"):
            header = cell_text(cell, shared_strings)
            if header:
                headers[column_number(cell.get("r", ""))] = header

        media_columns = {
            column: (AUDIO_URL if is_audio else IMAGE_URL)
            for column, header in headers.items()
            for is_audio, is_image in [is_media_header(header)]
            if is_audio or is_image
        }
        if not media_columns:
            continue

        changed = 0
        for row in rows[1:]:
            for cell in row.findall(f"{{{MAIN_NS}}}c"):
                column = column_number(cell.get("r", ""))
                replacement = media_columns.get(column)
                if replacement is None or not cell_text(cell, shared_strings).strip():
                    continue
                set_inline_string(cell, replacement)
                changed += 1

        updates[sheet_path] = ET.tostring(root, encoding="utf-8", xml_declaration=True)
        print(f"{sheet.get('name')}: {changed} media URL cells updated")

    with ZipFile(OUTPUT, "w", ZIP_DEFLATED) as output_zip:
        for item in source_zip.infolist():
            output_zip.writestr(item, updates.get(item.filename, source_zip.read(item.filename)))

print(OUTPUT)
