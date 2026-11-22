from pathlib import Path
from zipfile import ZIP_DEFLATED, ZipFile
import xml.etree.ElementTree as ET


SOURCE = Path(
    r"D:\Capstone\BE_JP\RikiPath.Backend\Data Import Bulk - TEST 5000-media-urls-updated-stroke-count.xlsx"
)
OUTPUT = SOURCE.with_name("Data Import Bulk - TEST 5000-media-urls-updated-sections.xlsx")
MAIN_NS = "http://schemas.openxmlformats.org/spreadsheetml/2006/main"
REL_NS = "http://schemas.openxmlformats.org/officeDocument/2006/relationships"
ET.register_namespace("", MAIN_NS)

SECTION_CODE_MAP = {
    "RPK_N5_GRAMMAR": "N5_GRAMMAR_READING",
    "RPK_N5_READING": "N5_GRAMMAR_READING",
    "RPK_N3_VOCABULARY": "N3_VOCAB",
    "RPK_N3_GRAMMAR": "N3_GRAMMAR",
    "RPK_N3_READING": "N3_READING",
    "RPK_N3_LISTENING": "N3_LISTENING",
    "RPK_N2_VOCABULARY": "N2_VOCAB",
    "RPK_N2_GRAMMAR": "N2_GRAMMAR",
    "RPK_N2_READING": "N2_READING",
    "RPK_N2_LISTENING": "N2_LISTENING",
}


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
        if sheet.get("name") != "Mock Test":
            continue
        relationship_id = sheet.get(f"{{{REL_NS}}}id")
        sheet_path = targets[relationship_id]
        root = ET.fromstring(source_zip.read(sheet_path))
        rows = root.findall(f".//{{{MAIN_NS}}}sheetData/{{{MAIN_NS}}}row")
        if not rows:
            continue

        section_column = None
        for cell in rows[0].findall(f"{{{MAIN_NS}}}c"):
            if cell_text(cell, shared_strings).strip().lower() == "sectioncode":
                section_column = column_number(cell.get("r", ""))
                break
        if section_column is None:
            raise RuntimeError("Mock Test sheet does not contain a SectionCode column")

        for row in rows[1:]:
            for cell in row.findall(f"{{{MAIN_NS}}}c"):
                if column_number(cell.get("r", "")) != section_column:
                    continue
                old_code = cell_text(cell, shared_strings).strip().upper()
                new_code = SECTION_CODE_MAP.get(old_code)
                if new_code is not None:
                    set_inline_string(cell, new_code)
                    total += 1

        updates[sheet_path] = ET.tostring(root, encoding="utf-8", xml_declaration=True)

    with ZipFile(OUTPUT, "w", ZIP_DEFLATED) as output_zip:
        for item in source_zip.infolist():
            output_zip.writestr(item, updates.get(item.filename, source_zip.read(item.filename)))

print(f"Normalized {total} Mock Test SectionCode cells")
print(OUTPUT)
