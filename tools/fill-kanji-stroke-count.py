from pathlib import Path
from zipfile import ZIP_DEFLATED, ZipFile
import xml.etree.ElementTree as ET


SOURCE = Path(r"D:\Capstone\BE_JP\RikiPath.Backend\Data Import Bulk - TEST 5000-media-urls-updated.xlsx")
OUTPUT = SOURCE.with_name("Data Import Bulk - TEST 5000-media-urls-updated-stroke-count.xlsx")
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


def set_numeric_value(cell, value):
    cell.attrib.pop("t", None)
    for child in list(cell):
        cell.remove(child)
    ET.SubElement(cell, f"{{{MAIN_NS}}}v").text = str(value)


with ZipFile(SOURCE, "r") as source_zip:
    shared_strings = []
    if "xl/sharedStrings.xml" in source_zip.namelist():
        shared_root = ET.fromstring(source_zip.read("xl/sharedStrings.xml"))
        shared_strings = [
            "".join(item.itertext())
            for item in shared_root.findall(f"{{{MAIN_NS}}}si")
        ]

    workbook_root = ET.fromstring(source_zip.read("xl/workbook.xml"))
    relationships = ET.fromstring(source_zip.read("xl/_rels/workbook.xml.rels"))
    targets = {
        relationship.get("Id"): relationship.get("Target", "").lstrip("/")
        for relationship in relationships
    }
    updates = {}
    total = 0

    for sheet in workbook_root.find(f"{{{MAIN_NS}}}sheets"):
        if sheet.get("name") != "Kanji":
            continue
        relationship_id = sheet.get(f"{{{REL_NS}}}id")
        sheet_path = targets[relationship_id]
        root = ET.fromstring(source_zip.read(sheet_path))
        rows = root.findall(f".//{{{MAIN_NS}}}sheetData/{{{MAIN_NS}}}row")
        if not rows:
            continue

        stroke_column = None
        for cell in rows[0].findall(f"{{{MAIN_NS}}}c"):
            if cell_text(cell, shared_strings).strip().lower() == "stroke count":
                stroke_column = column_number(cell.get("r", ""))
                break
        if stroke_column is None:
            raise RuntimeError("Kanji sheet does not contain a Stroke Count column")

        for index, row in enumerate(rows[1:]):
            # The workbook is demo/import data and the source column is blank. Keep
            # existing values, and use a deterministic 1..15 sample range for blanks.
            value = 1 + (index % 15)
            target_cell = None
            for cell in row.findall(f"{{{MAIN_NS}}}c"):
                if column_number(cell.get("r", "")) == stroke_column:
                    target_cell = cell
                    break
            if target_cell is None:
                target_cell = ET.Element(f"{{{MAIN_NS}}}c", {"r": f"F{row.get('r')}"})
                row.append(target_cell)
            if not cell_text(target_cell, shared_strings).strip():
                set_numeric_value(target_cell, value)
                total += 1

        updates[sheet_path] = ET.tostring(root, encoding="utf-8", xml_declaration=True)

    with ZipFile(OUTPUT, "w", ZIP_DEFLATED) as output_zip:
        for item in source_zip.infolist():
            output_zip.writestr(item, updates.get(item.filename, source_zip.read(item.filename)))

print(f"Filled {total} blank Kanji Stroke Count cells")
print(OUTPUT)
