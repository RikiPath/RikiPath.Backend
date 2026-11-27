using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using ClosedXML.Excel;

namespace RikiPath.Application.Helpers;

public sealed record AttachmentPreviewRun(
    string Text,
    bool Bold = false,
    bool Italic = false,
    string? Href = null);

public sealed record AttachmentPreviewBlock(
    string Type,
    List<AttachmentPreviewRun>? Runs = null,
    List<List<List<AttachmentPreviewRun>>>? Rows = null);

public sealed record AttachmentPreviewCell(
    string Text,
    string? Href = null);

public sealed record AttachmentPreviewSheet(
    string Name,
    List<List<AttachmentPreviewCell>> Rows,
    bool Truncated);

public sealed record AttachmentPreviewPayload(
    string Kind,
    string FileName,
    string Extension,
    string ContentType,
    long SizeBytes,
    string? Message = null,
    List<AttachmentPreviewBlock>? Blocks = null,
    List<AttachmentPreviewSheet>? Sheets = null,
    string? Text = null,
    bool Truncated = false);

/// <summary>
/// Kiểm tra tính nhất quán giữa phần mở rộng, Content-Type và nội dung nhị phân thực tế,
/// đồng thời dựng bản xem trước chỉ đọc cho Word (.docx), bảng tính (.xlsx/.csv) và văn bản (.txt)
/// mà không bao giờ chỉnh sửa hay ghi đè tài liệu gốc.
/// </summary>
public static class AttachmentContentInspector
{
    public const string UnsupportedPreviewMessage = "Tài liệu này chưa hỗ trợ xem trước. Bạn có thể tải xuống để xem.";

    private const int MaxPreviewTextBytes = 200 * 1024;
    private const int MaxDocxBlocks = 600;
    private const int MaxTableRows = 200;
    private const int MaxTableCols = 30;
    private const int MaxSheets = 10;

    private static readonly XNamespace WNs = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
    private static readonly XNamespace RNs = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private static readonly XNamespace RelNs = "http://schemas.openxmlformats.org/package/2006/relationships";

    public static string ResolveCanonicalContentType(string? ext) => (ext ?? string.Empty).Trim().ToLowerInvariant() switch
    {
        ".pdf" => "application/pdf",
        ".png" => "image/png",
        ".jpg" or ".jpeg" => "image/jpeg",
        ".webp" => "image/webp",
        ".gif" => "image/gif",
        ".doc" => "application/msword",
        ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        ".xls" => "application/vnd.ms-excel",
        ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        ".csv" => "text/csv; charset=utf-8",
        ".txt" => "text/plain; charset=utf-8",
        ".mp3" => "audio/mpeg",
        ".wav" => "audio/wav",
        ".m4a" => "audio/mp4",
        ".ogg" => "audio/ogg",
        ".webm" => "audio/webm",
        ".mp4" => "video/mp4",
        _ => "application/octet-stream",
    };

    public static bool AreExtensionsEquivalent(string? extA, string? extB)
    {
        var a = (extA ?? string.Empty).Trim().ToLowerInvariant();
        var b = (extB ?? string.Empty).Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b))
            return false;
        if (a == b)
            return true;
        return (a is ".jpg" or ".jpeg") && (b is ".jpg" or ".jpeg");
    }

    public static bool ValidateUploadContent(
        Stream seekableStream,
        string? fileName,
        out string canonicalContentType,
        out string? errorMessage)
    {
        var ext = Path.GetExtension(fileName ?? string.Empty).Trim().ToLowerInvariant();
        canonicalContentType = ResolveCanonicalContentType(ext);
        errorMessage = null;

        if (string.IsNullOrWhiteSpace(ext) || !seekableStream.CanRead)
        {
            errorMessage = "Tệp tải lên chưa hợp lệ. Vui lòng kiểm tra lại định dạng tệp.";
            return false;
        }

        if (seekableStream.CanSeek)
            seekableStream.Position = 0;

        var header = ReadHeader(seekableStream, 4096);
        if (seekableStream.CanSeek)
            seekableStream.Position = 0;

        if (header.Length == 0)
        {
            errorMessage = "Tệp tải lên đang trống.";
            return false;
        }

        var valid = ext switch
        {
            ".pdf" => IsPdf(header),
            ".png" => IsPng(header),
            ".jpg" or ".jpeg" => IsJpeg(header),
            ".gif" => IsGif(header),
            ".webp" => IsWebp(header),
            ".doc" or ".xls" => IsOle2(header),
            ".docx" => IsZip(header) && HasOpenXmlStructure(seekableStream, "word/"),
            ".xlsx" => IsZip(header) && HasOpenXmlStructure(seekableStream, "xl/"),
            ".txt" or ".csv" => !LooksBinary(header),
            ".mp3" or ".wav" or ".m4a" or ".ogg" or ".webm" or ".mp4" => header.Length > 0,
            _ => false,
        };

        if (seekableStream.CanSeek)
            seekableStream.Position = 0;

        if (!valid)
        {
            errorMessage = "Nội dung tệp không khớp với phần mở rộng của tệp đã chọn. Vui lòng kiểm tra lại tệp gốc.";
            return false;
        }

        return true;
    }

    public static string DetectActualExtension(Stream seekableStream, string? declaredExt)
    {
        var fallback = (declaredExt ?? string.Empty).Trim().ToLowerInvariant();
        if (!seekableStream.CanRead)
            return fallback;

        if (seekableStream.CanSeek)
            seekableStream.Position = 0;

        var header = ReadHeader(seekableStream, 32);
        if (seekableStream.CanSeek)
            seekableStream.Position = 0;

        if (IsPdf(header)) return ".pdf";
        if (IsPng(header)) return ".png";
        if (IsJpeg(header)) return ".jpg";
        if (IsGif(header)) return ".gif";
        if (IsWebp(header)) return ".webp";

        if (IsZip(header))
        {
            var isWord = HasOpenXmlStructure(seekableStream, "word/");
            if (seekableStream.CanSeek) seekableStream.Position = 0;
            if (isWord) return ".docx";

            var isExcel = HasOpenXmlStructure(seekableStream, "xl/");
            if (seekableStream.CanSeek) seekableStream.Position = 0;
            if (isExcel) return ".xlsx";
        }

        if (IsOle2(header))
        {
            return fallback is ".xls" or ".doc" ? fallback : ".doc";
        }

        return fallback;
    }

    public static string EnsureFilenameMatchesExtension(string displayName, string? actualExt)
    {
        var cleanName = Path.GetFileName((displayName ?? string.Empty).Trim());
        if (string.IsNullOrWhiteSpace(cleanName))
            cleanName = "tai-lieu-dinh-kem";

        var ext = (actualExt ?? string.Empty).Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(ext))
            return cleanName;

        var currentExt = Path.GetExtension(cleanName);
        if (AreExtensionsEquivalent(currentExt, ext))
            return cleanName;

        var baseName = Path.GetFileNameWithoutExtension(cleanName);
        if (string.IsNullOrWhiteSpace(baseName))
            baseName = "tai-lieu-dinh-kem";

        return $"{baseName}{ext}";
    }

    public static AttachmentPreviewPayload BuildPreviewPayload(
        Stream seekableStream,
        string displayName,
        string actualExt,
        string actualMime,
        long sizeBytes)
    {
        var ext = (actualExt ?? string.Empty).Trim().ToLowerInvariant();
        var fileName = EnsureFilenameMatchesExtension(displayName, ext);

        if (seekableStream.CanSeek)
            seekableStream.Position = 0;

        switch (ext)
        {
            case ".pdf":
                return new AttachmentPreviewPayload(
                    Kind: "pdf",
                    FileName: fileName,
                    Extension: ext,
                    ContentType: actualMime,
                    SizeBytes: sizeBytes);

            case ".png":
            case ".jpg":
            case ".jpeg":
            case ".webp":
            case ".gif":
                return new AttachmentPreviewPayload(
                    Kind: "image",
                    FileName: fileName,
                    Extension: ext,
                    ContentType: actualMime,
                    SizeBytes: sizeBytes);

            case ".txt":
                return ReadTextPreview(seekableStream, fileName, ext, actualMime, sizeBytes);

            case ".docx":
                return ReadDocxPreview(seekableStream, fileName, ext, actualMime, sizeBytes);

            case ".xlsx":
                return ReadXlsxPreview(seekableStream, fileName, ext, actualMime, sizeBytes);

            case ".csv":
                return ReadCsvPreview(seekableStream, fileName, ext, actualMime, sizeBytes);

            default:
                return new AttachmentPreviewPayload(
                    Kind: "unsupported",
                    FileName: fileName,
                    Extension: ext,
                    ContentType: actualMime,
                    SizeBytes: sizeBytes,
                    Message: UnsupportedPreviewMessage);
        }
    }

    private static AttachmentPreviewPayload ReadTextPreview(
        Stream stream, string fileName, string ext, string mime, long sizeBytes)
    {
        try
        {
            if (stream.CanSeek) stream.Position = 0;
            var toRead = (int)Math.Min(MaxPreviewTextBytes + 1, stream.Length);
            var buf = new byte[toRead];
            var offset = 0;
            while (offset < toRead)
            {
                var read = stream.Read(buf, offset, toRead - offset);
                if (read <= 0) break;
                offset += read;
            }
            var truncated = offset > MaxPreviewTextBytes || sizeBytes > MaxPreviewTextBytes;
            var actualLen = Math.Min(offset, MaxPreviewTextBytes);
            var text = Encoding.UTF8.GetString(buf, 0, actualLen);
            return new AttachmentPreviewPayload(
                Kind: "text",
                FileName: fileName,
                Extension: ext,
                ContentType: mime,
                SizeBytes: sizeBytes,
                Text: text,
                Truncated: truncated);
        }
        catch
        {
            return new AttachmentPreviewPayload(
                Kind: "unsupported",
                FileName: fileName,
                Extension: ext,
                ContentType: mime,
                SizeBytes: sizeBytes,
                Message: UnsupportedPreviewMessage);
        }
    }

    private static AttachmentPreviewPayload ReadDocxPreview(
        Stream stream, string fileName, string ext, string mime, long sizeBytes)
    {
        try
        {
            if (stream.CanSeek) stream.Position = 0;
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);

            var docEntry = archive.Entries.FirstOrDefault(
                e => string.Equals(e.FullName, "word/document.xml", StringComparison.OrdinalIgnoreCase));
            if (docEntry is null)
            {
                return new AttachmentPreviewPayload(
                    Kind: "unsupported",
                    FileName: fileName,
                    Extension: ext,
                    ContentType: mime,
                    SizeBytes: sizeBytes,
                    Message: UnsupportedPreviewMessage);
            }

            var hyperlinkMap = new Dictionary<string, string>(StringComparer.Ordinal);
            var relsEntry = archive.Entries.FirstOrDefault(
                e => string.Equals(e.FullName, "word/_rels/document.xml.rels", StringComparison.OrdinalIgnoreCase));
            if (relsEntry is not null)
            {
                using var relStream = relsEntry.Open();
                var relDoc = XDocument.Load(relStream, System.Xml.Linq.LoadOptions.None);
                foreach (var rel in relDoc.Descendants(RelNs + "Relationship"))
                {
                    var id = (string?)rel.Attribute("Id");
                    var target = (string?)rel.Attribute("Target");
                    if (!string.IsNullOrWhiteSpace(id) && IsSafePreviewHref(target))
                        hyperlinkMap[id] = target!.Trim();
                }
            }

            using var docStream = docEntry.Open();
            var doc = XDocument.Load(docStream, System.Xml.Linq.LoadOptions.PreserveWhitespace);
            var body = doc.Root?.Element(WNs + "body");
            if (body is null)
            {
                return new AttachmentPreviewPayload(
                    Kind: "unsupported",
                    FileName: fileName,
                    Extension: ext,
                    ContentType: mime,
                    SizeBytes: sizeBytes,
                    Message: UnsupportedPreviewMessage);
            }

            var blocks = new List<AttachmentPreviewBlock>();
            var truncated = false;

            foreach (var child in body.Elements())
            {
                if (blocks.Count >= MaxDocxBlocks)
                {
                    truncated = true;
                    break;
                }

                if (child.Name == WNs + "p")
                {
                    var block = ParseDocxParagraph(child, hyperlinkMap);
                    if (block is not null)
                        blocks.Add(block);
                }
                else if (child.Name == WNs + "tbl")
                {
                    var tableBlock = ParseDocxTable(child, hyperlinkMap);
                    if (tableBlock is not null)
                        blocks.Add(tableBlock);
                }
            }

            return new AttachmentPreviewPayload(
                Kind: "docx",
                FileName: fileName,
                Extension: ext,
                ContentType: mime,
                SizeBytes: sizeBytes,
                Blocks: blocks,
                Truncated: truncated);
        }
        catch
        {
            return new AttachmentPreviewPayload(
                Kind: "unsupported",
                FileName: fileName,
                Extension: ext,
                ContentType: mime,
                SizeBytes: sizeBytes,
                Message: UnsupportedPreviewMessage);
        }
    }

    private static AttachmentPreviewBlock? ParseDocxParagraph(
        XElement paragraph,
        IReadOnlyDictionary<string, string> hyperlinkMap)
    {
        var pPr = paragraph.Element(WNs + "pPr");
        var styleVal = (string?)pPr?.Element(WNs + "pStyle")?.Attribute(WNs + "val") ?? string.Empty;
        var isList = pPr?.Element(WNs + "numPr") is not null;

        var blockType = styleVal.ToUpperInvariant() switch
        {
            "HEADING1" or "TITLE" => "heading1",
            "HEADING2" or "SUBTITLE" => "heading2",
            "HEADING3" or "HEADING4" => "heading3",
            _ => isList ? "list-item" : "paragraph",
        };

        var runs = ExtractDocxRuns(paragraph, hyperlinkMap);
        return new AttachmentPreviewBlock(blockType, Runs: runs);
    }

    private static AttachmentPreviewBlock? ParseDocxTable(
        XElement table,
        IReadOnlyDictionary<string, string> hyperlinkMap)
    {
        var rows = new List<List<List<AttachmentPreviewRun>>>();
        foreach (var tr in table.Elements(WNs + "tr"))
        {
            if (rows.Count >= MaxTableRows) break;
            var rowCells = new List<List<AttachmentPreviewRun>>();
            foreach (var tc in tr.Elements(WNs + "tc"))
            {
                if (rowCells.Count >= MaxTableCols) break;
                var cellRuns = new List<AttachmentPreviewRun>();
                var cellParagraphs = tc.Elements(WNs + "p").ToList();
                for (var pIdx = 0; pIdx < cellParagraphs.Count; pIdx++)
                {
                    if (pIdx > 0 && cellRuns.Count > 0)
                        cellRuns.Add(new AttachmentPreviewRun("\n"));
                    cellRuns.AddRange(ExtractDocxRuns(cellParagraphs[pIdx], hyperlinkMap));
                }
                rowCells.Add(cellRuns);
            }
            if (rowCells.Count > 0)
                rows.Add(rowCells);
        }

        return rows.Count > 0 ? new AttachmentPreviewBlock("table", Rows: rows) : null;
    }

    private static List<AttachmentPreviewRun> ExtractDocxRuns(
        XElement container,
        IReadOnlyDictionary<string, string> hyperlinkMap)
    {
        var runs = new List<AttachmentPreviewRun>();

        void AppendRun(XElement rElement, string? href)
        {
            var rPr = rElement.Element(WNs + "rPr");
            var bold = IsDocxToggleEnabled(rPr?.Element(WNs + "b"));
            var italic = IsDocxToggleEnabled(rPr?.Element(WNs + "i"));

            var sb = new StringBuilder();
            foreach (var node in rElement.Elements())
            {
                if (node.Name == WNs + "t")
                    sb.Append(node.Value);
                else if (node.Name == WNs + "br" || node.Name == WNs + "cr")
                    sb.Append('\n');
                else if (node.Name == WNs + "tab")
                    sb.Append(' ');
            }

            var text = sb.ToString();
            if (!string.IsNullOrEmpty(text))
                runs.Add(new AttachmentPreviewRun(text, bold, italic, href));
        }

        foreach (var child in container.Elements())
        {
            if (child.Name == WNs + "r")
            {
                AppendRun(child, null);
            }
            else if (child.Name == WNs + "hyperlink")
            {
                var relId = (string?)child.Attribute(RNs + "id");
                var href = relId is not null && hyperlinkMap.TryGetValue(relId, out var resolved)
                    ? resolved
                    : null;
                foreach (var innerRun in child.Elements(WNs + "r"))
                    AppendRun(innerRun, href);
            }
            else if (child.Name == WNs + "fldSimple")
            {
                var instr = (string?)child.Attribute(WNs + "instr") ?? string.Empty;
                var href = TryExtractHyperlinkInstructionUrl(instr);
                foreach (var innerRun in child.Elements(WNs + "r"))
                    AppendRun(innerRun, href);
            }
        }

        return runs;
    }

    private static bool IsDocxToggleEnabled(XElement? toggleElement)
    {
        if (toggleElement is null) return false;
        var val = (string?)toggleElement.Attribute(WNs + "val");
        if (string.IsNullOrWhiteSpace(val)) return true;
        return !string.Equals(val, "0", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(val, "false", StringComparison.OrdinalIgnoreCase);
    }

    private static string? TryExtractHyperlinkInstructionUrl(string instruction)
    {
        if (string.IsNullOrWhiteSpace(instruction)) return null;
        var trimmed = instruction.Trim();
        if (!trimmed.StartsWith("HYPERLINK", StringComparison.OrdinalIgnoreCase)) return null;
        var firstQuote = trimmed.IndexOf('"');
        if (firstQuote < 0) return null;
        var secondQuote = trimmed.IndexOf('"', firstQuote + 1);
        if (secondQuote <= firstQuote + 1) return null;
        var url = trimmed[(firstQuote + 1)..secondQuote].Trim();
        return IsSafePreviewHref(url) ? url : null;
    }

    private static AttachmentPreviewPayload ReadXlsxPreview(
        Stream stream, string fileName, string ext, string mime, long sizeBytes)
    {
        try
        {
            if (stream.CanSeek) stream.Position = 0;
            var culture = CultureInfo.GetCultureInfo("vi-VN");
            using var workbook = new XLWorkbook(stream);
            var sheets = new List<AttachmentPreviewSheet>();

            foreach (var sheet in workbook.Worksheets)
            {
                if (sheet.Visibility != XLWorksheetVisibility.Visible)
                    continue;
                if (sheets.Count >= MaxSheets)
                    break;

                var range = sheet.RangeUsed();
                if (range is null)
                {
                    sheets.Add(new AttachmentPreviewSheet(sheet.Name, [], Truncated: false));
                    continue;
                }

                var firstRow = range.RangeAddress.FirstAddress.RowNumber;
                var lastRowAll = range.RangeAddress.LastAddress.RowNumber;
                var firstCol = range.RangeAddress.FirstAddress.ColumnNumber;
                var lastColAll = range.RangeAddress.LastAddress.ColumnNumber;

                var lastRow = Math.Min(lastRowAll, firstRow + MaxTableRows - 1);
                var lastCol = Math.Min(lastColAll, firstCol + MaxTableCols - 1);
                var truncated = lastRowAll > lastRow || lastColAll > lastCol;

                var rows = new List<List<AttachmentPreviewCell>>(lastRow - firstRow + 1);
                for (var r = firstRow; r <= lastRow; r++)
                {
                    var rowCells = new List<AttachmentPreviewCell>(lastCol - firstCol + 1);
                    var hasAnyContent = false;
                    for (var c = firstCol; c <= lastCol; c++)
                    {
                        var cell = sheet.Cell(r, c);
                        var value = cell.HasFormula
                            ? (cell.CachedValue.IsBlank ? "=" + cell.FormulaA1 : cell.CachedValue.ToString(culture))
                            : cell.GetFormattedString(culture);
                        string? href = null;
                        if (cell.HasHyperlink)
                        {
                            var hl = cell.GetHyperlink();
                            var candidateUrl = hl.IsExternal ? hl.ExternalAddress?.ToString() : null;
                            if (IsSafePreviewHref(candidateUrl))
                                href = candidateUrl!.Trim();
                        }
                        if (!string.IsNullOrEmpty(value) || href is not null)
                            hasAnyContent = true;
                        rowCells.Add(new AttachmentPreviewCell(value ?? string.Empty, href));
                    }
                    if (hasAnyContent || rows.Count > 0)
                        rows.Add(rowCells);
                }

                while (rows.Count > 0 && rows[^1].All(c => string.IsNullOrEmpty(c.Text) && c.Href is null))
                    rows.RemoveAt(rows.Count - 1);

                sheets.Add(new AttachmentPreviewSheet(sheet.Name, rows, truncated));
            }

            if (sheets.Count == 0)
            {
                return new AttachmentPreviewPayload(
                    Kind: "unsupported",
                    FileName: fileName,
                    Extension: ext,
                    ContentType: mime,
                    SizeBytes: sizeBytes,
                    Message: UnsupportedPreviewMessage);
            }

            return new AttachmentPreviewPayload(
                Kind: "spreadsheet",
                FileName: fileName,
                Extension: ext,
                ContentType: mime,
                SizeBytes: sizeBytes,
                Sheets: sheets);
        }
        catch
        {
            return new AttachmentPreviewPayload(
                Kind: "unsupported",
                FileName: fileName,
                Extension: ext,
                ContentType: mime,
                SizeBytes: sizeBytes,
                Message: UnsupportedPreviewMessage);
        }
    }

    private static AttachmentPreviewPayload ReadCsvPreview(
        Stream stream, string fileName, string ext, string mime, long sizeBytes)
    {
        try
        {
            if (stream.CanSeek) stream.Position = 0;
            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 8192, leaveOpen: true);
            var rows = new List<List<AttachmentPreviewCell>>();
            var truncated = false;

            while (reader.ReadLine() is { } line)
            {
                if (rows.Count >= MaxTableRows)
                {
                    truncated = true;
                    break;
                }
                var parsed = ParseCsvLine(line);
                if (parsed.Count > MaxTableCols)
                {
                    truncated = true;
                    parsed = parsed.Take(MaxTableCols).ToList();
                }
                rows.Add(parsed.Select(v => new AttachmentPreviewCell(v)).ToList());
            }

            var sheet = new AttachmentPreviewSheet(Path.GetFileNameWithoutExtension(fileName), rows, truncated);
            return new AttachmentPreviewPayload(
                Kind: "spreadsheet",
                FileName: fileName,
                Extension: ext,
                ContentType: mime,
                SizeBytes: sizeBytes,
                Sheets: [sheet],
                Truncated: truncated);
        }
        catch
        {
            return new AttachmentPreviewPayload(
                Kind: "unsupported",
                FileName: fileName,
                Extension: ext,
                ContentType: mime,
                SizeBytes: sizeBytes,
                Message: UnsupportedPreviewMessage);
        }
    }

    private static List<string> ParseCsvLine(string line)
    {
        var cells = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < line.Length; i++)
        {
            var ch = line[i];
            if (inQuotes)
            {
                if (ch == '"')
                {
                    if (i + 1 < line.Length && line[i + 1] == '"')
                    {
                        current.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    current.Append(ch);
                }
            }
            else if (ch == '"')
            {
                inQuotes = true;
            }
            else if (ch == ',')
            {
                cells.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(ch);
            }
        }

        cells.Add(current.ToString());
        return cells;
    }

    private static bool IsSafePreviewHref(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return false;
        return Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeMailto);
    }

    private static byte[] ReadHeader(Stream stream, int count)
    {
        var buffer = new byte[count];
        var readTotal = 0;
        while (readTotal < count)
        {
            var n = stream.Read(buffer, readTotal, count - readTotal);
            if (n <= 0) break;
            readTotal += n;
        }
        if (readTotal == buffer.Length) return buffer;
        var exact = new byte[readTotal];
        Array.Copy(buffer, exact, readTotal);
        return exact;
    }

    private static bool HasOpenXmlStructure(Stream seekableStream, string requiredPrefix)
    {
        try
        {
            if (seekableStream.CanSeek) seekableStream.Position = 0;
            using var archive = new ZipArchive(seekableStream, ZipArchiveMode.Read, leaveOpen: true);
            var hasContentTypes = false;
            var hasPrefix = false;
            foreach (var entry in archive.Entries)
            {
                if (string.Equals(entry.FullName, "[Content_Types].xml", StringComparison.OrdinalIgnoreCase))
                    hasContentTypes = true;
                if (entry.FullName.StartsWith(requiredPrefix, StringComparison.OrdinalIgnoreCase))
                    hasPrefix = true;
                if (hasContentTypes && hasPrefix)
                    return true;
            }
            return false;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsPdf(byte[] b) =>
        b.Length >= 5 && b[0] == 0x25 && b[1] == 0x50 && b[2] == 0x44 && b[3] == 0x46 && b[4] == 0x2D;

    private static bool IsPng(byte[] b) =>
        b.Length >= 8 && b[0] == 0x89 && b[1] == 0x50 && b[2] == 0x4E && b[3] == 0x47
        && b[4] == 0x0D && b[5] == 0x0A && b[6] == 0x1A && b[7] == 0x0A;

    private static bool IsJpeg(byte[] b) =>
        b.Length >= 3 && b[0] == 0xFF && b[1] == 0xD8 && b[2] == 0xFF;

    private static bool IsGif(byte[] b) =>
        b.Length >= 6 && b[0] == 0x47 && b[1] == 0x49 && b[2] == 0x46 && b[3] == 0x38
        && (b[4] == 0x37 || b[4] == 0x39) && b[5] == 0x61;

    private static bool IsWebp(byte[] b) =>
        b.Length >= 12 && b[0] == 0x52 && b[1] == 0x49 && b[2] == 0x46 && b[3] == 0x46
        && b[8] == 0x57 && b[9] == 0x45 && b[10] == 0x42 && b[11] == 0x50;

    private static bool IsZip(byte[] b) =>
        b.Length >= 4 && b[0] == 0x50 && b[1] == 0x4B
        && ((b[2] == 0x03 && b[3] == 0x04) || (b[2] == 0x05 && b[3] == 0x06));

    private static bool IsOle2(byte[] b) =>
        b.Length >= 8 && b[0] == 0xD0 && b[1] == 0xCF && b[2] == 0x11 && b[3] == 0xE0
        && b[4] == 0xA1 && b[5] == 0xB1 && b[6] == 0x1A && b[7] == 0xE1;

    private static bool LooksBinary(byte[] b)
    {
        if (IsPdf(b) || IsPng(b) || IsJpeg(b) || IsGif(b) || IsWebp(b) || IsZip(b) || IsOle2(b))
            return true;
        if (b.Length >= 2 && b[0] == 0x4D && b[1] == 0x5A) // MZ executable
            return true;
        var limit = Math.Min(b.Length, 4096);
        for (var i = 0; i < limit; i++)
        {
            if (b[i] == 0) return true;
        }
        return false;
    }
}
