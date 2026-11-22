from zipfile import ZipFile, ZIP_DEFLATED
from xml.sax.saxutils import escape
from pathlib import Path

OUT = Path(r"D:\Capstone\BE_JP\RikiPath.Backend\JLPT-Import-Sample-1000-each.xlsx")
MP3 = "https://meetings-vessels-towards-reading.trycloudflare.com/api/audio/content/900004/d25dd54b48dd445d93c8229992243bc6.mp3"
IMG = "https://iomeiswoyuzatihtccln.supabase.co/storage/v1/object/public/rikipath-media/jlpt-images/4cca3f46d094423e88391e1fbf3ef738.jpg"

def cell(ref, value):
    return f'<c r="{ref}" t="inlineStr"><is><t>{escape(str(value))}</t></is></c>'

def sheet_xml(headers, rows):
    out = ['<?xml version="1.0" encoding="UTF-8" standalone="yes"?><worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"><sheetData>']
    for ri, row in enumerate([headers] + rows, 1):
        cells = []
        for ci, value in enumerate(row, 1):
            if value is None: continue
            col = ''
            n = ci
            while n:
                n, rem = divmod(n - 1, 26); col = chr(65 + rem) + col
            cells.append(cell(f'{col}{ri}', value))
        out.append(f'<row r="{ri}">' + ''.join(cells) + '</row>')
    out.append('</sheetData></worksheet>')
    return ''.join(out)

def rows_for(headers, make):
    return [[make(i, h) for h in headers] for i in range(1, 1001)]

schemas = {
    'Lesson': ['LessonTitle','Description','Certificate Level Code','Language Skill Name','Sort Order','ImageUrl','AudioUrl'],
    'Kanji': ['Character','Meaning','OnYomi','KunYomi','SinoVietnamese','StrokeCount','ImageUrl','StrokeOrderImageUrl','Certificate Level Code'],
    'Vocabulary': ['Word','Reading','Meaning','PartOfSpeech','ExampleSentence','ImageUrl','Certificate Level Code'],
    'Grammar Pattern': ['Pattern','Meaning','Explanation','ExampleSentence','ImageUrl','Certificate Level Code'],
    'Mock Test': ['MockTestTitle','Certificate Level Code','SectionCode','SectionTitle','QuestionSortOrder','QuestionType','Language Skill Name','QuestionText','Option1','Option2','Option3','Option4','CorrectOptionNumber','Explanation','AudioUrl','ImageUrl'],
    'Practice Exercise': ['Exercise Title','Exercise Description','Lesson Title','Certificate Level Code','Language Skill Code','Exercise Sort Order','Question Sort Order','Question Text','Option1','Option2','Option3','Option4','Correct Option Number','Explanation','ExerciseImageUrl','ImageUrl'],
}

def make_row(name, i, headers):
    level = ['N5','N4','N3','N2'][(i-1) % 4]
    values = {
        'LessonTitle': f'Bài học mẫu {i:04d}', 'Description': f'Nội dung luyện JLPT mẫu {i:04d}', 'Certificate Level Code': level,
        'Language Skill Name': ['VOCABULARY','GRAMMAR','READING','LISTENING'][(i-1)%4], 'Sort Order': i, 'ImageUrl': IMG if i % 2 == 0 else '', 'AudioUrl': MP3 if i % 3 == 0 else '',
        'Character': chr(0x4E00 + (i % 2000)), 'Meaning': f'Nghĩa Hán tự {i}', 'OnYomi': 'コウ', 'KunYomi': 'い', 'SinoVietnamese': f'Âm Hán Việt {i}', 'StrokeCount': (i % 15) + 1, 'StrokeOrderImageUrl': IMG if i % 2 == 0 else '',
        'Word': f'たんご{i:04d}', 'Reading': f'tango{i:04d}', 'PartOfSpeech': 'Noun', 'ExampleSentence': f'これは例文 {i} です。',
        'Pattern': f'～ mẫu ngữ pháp {i:04d}', 'Explanation': f'Giải thích ngữ pháp mẫu {i}',
        'MockTestTitle': f'Đề thi mẫu {i:04d}', 'SectionCode': 'VOCABULARY', 'SectionTitle': 'Từ vựng', 'QuestionSortOrder': i, 'QuestionType': 'SingleChoice', 'QuestionText': f'Câu hỏi mẫu {i}', 'CorrectOptionNumber': 1, 'AudioUrl': MP3 if i % 3 == 0 else '', 'ImageUrl': IMG if i % 2 == 0 else '',
        'Option1': 'Đáp án đúng', 'Option2': 'Đáp án khác 1', 'Option3': 'Đáp án khác 2', 'Option4': 'Đáp án khác 3',
        'Exercise Title': f'Bài tập mẫu {i:04d}', 'Exercise Description': f'Bài luyện tập mẫu {i}', 'Lesson Title': f'Bài học mẫu {i:04d}', 'Language Skill Code': 'VOCABULARY', 'Exercise Sort Order': i, 'Question Sort Order': 1, 'Question Text': f'Câu luyện tập mẫu {i}', 'Correct Option Number': 1, 'ExerciseImageUrl': IMG if i % 2 == 0 else '',
    }
    return [values.get(h, '') for h in headers]

parts = {}
for name, headers in schemas.items():
    parts[name] = sheet_xml(headers, rows_for(headers, lambda i, _: make_row(name, i, headers)))

workbook = '<?xml version="1.0" encoding="UTF-8" standalone="yes"?><workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships"><sheets>'
for i, name in enumerate(schemas, 1): workbook += f'<sheet name="{escape(name)}" sheetId="{i}" r:id="rId{i}"/>'
workbook += '</sheets></workbook>'
rels = '<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">' + ''.join(f'<Relationship Id="rId{i}" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet{i}.xml"/>' for i in range(1, 7)) + '</Relationships>'
content = '<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/><Default Extension="xml" ContentType="application/xml"/><Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/>' + ''.join(f'<Override PartName="/xl/worksheets/sheet{i}.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/>' for i in range(1,7)) + '</Types>'
with ZipFile(OUT, 'w', ZIP_DEFLATED) as z:
    z.writestr('[Content_Types].xml', content); z.writestr('_rels/.rels', '<?xml version="1.0"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/></Relationships>')
    z.writestr('xl/workbook.xml', workbook); z.writestr('xl/_rels/workbook.xml.rels', rels)
    for i, name in enumerate(schemas, 1): z.writestr(f'xl/worksheets/sheet{i}.xml', parts[name])
print(OUT)
