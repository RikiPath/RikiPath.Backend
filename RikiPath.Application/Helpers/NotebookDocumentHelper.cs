using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace RikiPath.Application.Helpers
{
    public sealed class NotebookDocumentParseResult
    {
        public string ContentJson { get; init; } = NotebookDocumentHelper.EmptyDocumentJson;
        public string ContentPlainText { get; init; } = string.Empty;
        public int ChecklistTotalCount { get; init; }
        public int ChecklistCompletedCount { get; init; }
    }

    /// <summary>
    /// Canonical validator, XSS neutralizer, plain-text indexer, and checklist aggregator
    /// for Personal Notebook V3 structured rich-text JSON AST documents.
    /// </summary>
    public static partial class NotebookDocumentHelper
    {
        public const string EmptyDocumentJson = "{\"type\":\"doc\",\"version\":1,\"blocks\":[]}";
        private const int MaxBlocks = 500;
        private const int MaxRunsPerBlock = 200;
        private const int MaxBlockTextLength = 8000;
        private const int MaxTotalPlainLength = 200_000;

        private static readonly HashSet<string> AllowedBlockTypes = new(StringComparer.Ordinal)
        {
            "paragraph",
            "heading",
            "bullet_item",
            "ordered_item",
            "checklist_item",
            "quote",
        };

        private static readonly HashSet<string> AllowedHighlightColors = new(StringComparer.Ordinal)
        {
            "yellow",
            "mint",
            "sakura",
        };

        private static readonly JsonSerializerOptions WriteOptions = new()
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            WriteIndented = false,
        };

        [GeneratedRegex(@"<script\b[^>]*>[\s\S]*?<\/script>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
        private static partial Regex ScriptTagRegex();

        [GeneratedRegex(@"javascript\s*:", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
        private static partial Regex JavascriptSchemeRegex();

        [GeneratedRegex(@"data\s*:\s*text\/html", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
        private static partial Regex DataHtmlRegex();

        [GeneratedRegex(@"\son[a-z]+\s*=", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
        private static partial Regex InlineEventAttrRegex();

        [GeneratedRegex(@"<!--rp-(nb|entry):[\s\S]*?-->", RegexOptions.CultureInvariant)]
        private static partial Regex LegacyCommentMarkerRegex();

        public static string NormalizePlainText(string? text, int maxLength = 0)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            var cleaned = LegacyCommentMarkerRegex().Replace(text, string.Empty);
            cleaned = ScriptTagRegex().Replace(cleaned, string.Empty);
            cleaned = JavascriptSchemeRegex().Replace(cleaned, string.Empty);
            cleaned = DataHtmlRegex().Replace(cleaned, string.Empty);
            cleaned = InlineEventAttrRegex().Replace(cleaned, " ");
            cleaned = cleaned.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');

            if (maxLength > 0 && cleaned.Length > maxLength)
                cleaned = cleaned[..maxLength];

            return cleaned;
        }

        public static string? SanitizeHighlightColor(string? color)
        {
            if (string.IsNullOrWhiteSpace(color))
                return null;
            var normalized = color.Trim().ToLowerInvariant();
            return AllowedHighlightColors.Contains(normalized) ? normalized : null;
        }

        public static string? SanitizeInternalPath(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return null;
            var trimmed = path.Trim();
            if (!trimmed.StartsWith('/') || trimmed.StartsWith("//", StringComparison.Ordinal))
                return null;
            return trimmed.Length > 300 ? trimmed[..300] : trimmed;
        }

        public static List<string> NormalizeTags(IEnumerable<string>? tags)
        {
            var result = new List<string>();
            if (tags is null)
                return result;

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var raw in tags)
            {
                var clean = NormalizePlainText(raw, 40).Trim();
                if (clean.Length == 0 || !seen.Add(clean))
                    continue;
                result.Add(clean);
                if (result.Count >= 12)
                    break;
            }
            return result;
        }

        public static string SerializeTags(IEnumerable<string>? tags)
            => JsonSerializer.Serialize(NormalizeTags(tags), WriteOptions);

        public static List<string> DeserializeTags(string? tagsJson)
        {
            if (string.IsNullOrWhiteSpace(tagsJson))
                return [];
            try
            {
                var parsed = JsonSerializer.Deserialize<List<string>>(tagsJson);
                return NormalizeTags(parsed);
            }
            catch (JsonException)
            {
                return [];
            }
        }

        public static string BuildExcerpt(string? plainText, int maxLength = 140)
        {
            if (string.IsNullOrWhiteSpace(plainText))
                return string.Empty;
            var singleLine = Regex.Replace(plainText.Trim(), @"\s+", " ");
            return singleLine.Length <= maxLength ? singleLine : $"{singleLine[..(maxLength - 1)].TrimEnd()}…";
        }

        public static NotebookDocumentParseResult ParseOrNormalizeDocument(string? rawContentJsonOrText)
        {
            if (string.IsNullOrWhiteSpace(rawContentJsonOrText))
            {
                return new NotebookDocumentParseResult();
            }

            var trimmed = rawContentJsonOrText.Trim();
            if (trimmed.StartsWith('{'))
            {
                try
                {
                    using var doc = JsonDocument.Parse(trimmed);
                    if (doc.RootElement.ValueKind == JsonValueKind.Object &&
                        doc.RootElement.TryGetProperty("blocks", out var blocksProp) &&
                        blocksProp.ValueKind == JsonValueKind.Array)
                    {
                        return CanonicalizeBlocksArray(blocksProp);
                    }
                }
                catch (JsonException)
                {
                    // Fall through to plain-text conversion.
                }
            }

            return ConvertPlainOrLegacyTextToDocument(trimmed);
        }

        private static NotebookDocumentParseResult CanonicalizeBlocksArray(JsonElement blocksProp)
        {
            var blocks = new List<Dictionary<string, object?>>();
            var plainSb = new StringBuilder();
            var checklistTotal = 0;
            var checklistCompleted = 0;

            var index = 0;
            foreach (var blockEl in blocksProp.EnumerateArray())
            {
                if (blocks.Count >= MaxBlocks)
                    break;
                if (blockEl.ValueKind != JsonValueKind.Object)
                    continue;

                index++;
                var type = blockEl.TryGetProperty("type", out var tProp) && tProp.ValueKind == JsonValueKind.String
                    ? tProp.GetString()?.Trim().ToLowerInvariant() ?? "paragraph"
                    : "paragraph";
                if (!AllowedBlockTypes.Contains(type))
                    type = "paragraph";

                var id = blockEl.TryGetProperty("id", out var idProp) && idProp.ValueKind == JsonValueKind.String
                    ? NormalizePlainText(idProp.GetString(), 48).Trim()
                    : string.Empty;
                if (id.Length == 0)
                    id = $"b{index}";

                var runs = new List<Dictionary<string, object?>>();
                var blockTextSb = new StringBuilder();

                if (blockEl.TryGetProperty("runs", out var runsProp) && runsProp.ValueKind == JsonValueKind.Array)
                {
                    foreach (var runEl in runsProp.EnumerateArray())
                    {
                        if (runs.Count >= MaxRunsPerBlock || blockTextSb.Length >= MaxBlockTextLength)
                            break;
                        if (runEl.ValueKind != JsonValueKind.Object)
                            continue;

                        var rawText = runEl.TryGetProperty("text", out var txtProp) && txtProp.ValueKind == JsonValueKind.String
                            ? txtProp.GetString()
                            : null;
                        var text = NormalizePlainText(rawText, MaxBlockTextLength - blockTextSb.Length);
                        if (text.Length == 0)
                            continue;

                        var runObj = new Dictionary<string, object?>(StringComparer.Ordinal)
                        {
                            ["text"] = text,
                        };

                        if (runEl.TryGetProperty("bold", out var bProp) && bProp.ValueKind == JsonValueKind.True)
                            runObj["bold"] = true;
                        if (runEl.TryGetProperty("italic", out var iProp) && iProp.ValueKind == JsonValueKind.True)
                            runObj["italic"] = true;

                        if (runEl.TryGetProperty("highlight", out var hProp))
                        {
                            if (hProp.ValueKind == JsonValueKind.String)
                            {
                                var color = SanitizeHighlightColor(hProp.GetString());
                                if (color is not null)
                                    runObj["highlight"] = color;
                            }
                            else if (hProp.ValueKind == JsonValueKind.True)
                            {
                                runObj["highlight"] = "yellow";
                            }
                        }

                        runs.Add(runObj);
                        blockTextSb.Append(text);
                    }
                }
                else if (blockEl.TryGetProperty("text", out var legacyTextProp) && legacyTextProp.ValueKind == JsonValueKind.String)
                {
                    var text = NormalizePlainText(legacyTextProp.GetString(), MaxBlockTextLength);
                    if (text.Length > 0)
                    {
                        runs.Add(new Dictionary<string, object?>(StringComparer.Ordinal) { ["text"] = text });
                        blockTextSb.Append(text);
                    }
                }

                var blockObj = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["id"] = id,
                    ["type"] = type,
                    ["runs"] = runs,
                };

                if (type == "heading")
                {
                    var level = 2;
                    if (blockEl.TryGetProperty("level", out var lvlProp) && lvlProp.ValueKind == JsonValueKind.Number && lvlProp.TryGetInt32(out var parsedLevel))
                        level = Math.Clamp(parsedLevel, 1, 3);
                    blockObj["level"] = level;
                }
                else if (type == "checklist_item")
                {
                    var isChecked = blockEl.TryGetProperty("checked", out var chkProp) && chkProp.ValueKind == JsonValueKind.True;
                    blockObj["checked"] = isChecked;
                    checklistTotal++;
                    if (isChecked)
                        checklistCompleted++;
                }

                blocks.Add(blockObj);
                var line = blockTextSb.ToString().TrimEnd();
                if (line.Length > 0)
                {
                    if (plainSb.Length > 0)
                        plainSb.Append('\n');
                    plainSb.Append(line);
                }
            }

            var payload = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["type"] = "doc",
                ["version"] = 1,
                ["blocks"] = blocks,
            };

            var plainText = plainSb.ToString();
            if (plainText.Length > MaxTotalPlainLength)
                plainText = plainText[..MaxTotalPlainLength];

            return new NotebookDocumentParseResult
            {
                ContentJson = JsonSerializer.Serialize(payload, WriteOptions),
                ContentPlainText = plainText,
                ChecklistTotalCount = checklistTotal,
                ChecklistCompletedCount = checklistCompleted,
            };
        }

        private static NotebookDocumentParseResult ConvertPlainOrLegacyTextToDocument(string text)
        {
            var cleaned = NormalizePlainText(text, MaxTotalPlainLength);
            var lines = cleaned.Split('\n');
            var blocks = new List<Dictionary<string, object?>>();
            var plainSb = new StringBuilder();
            var checklistTotal = 0;
            var checklistCompleted = 0;

            for (var i = 0; i < lines.Length && blocks.Count < MaxBlocks; i++)
            {
                var line = lines[i];
                var id = $"b{i + 1}";
                var type = "paragraph";
                int? headingLevel = null;
                bool? isChecked = null;
                var content = line;

                if (line.StartsWith("### ", StringComparison.Ordinal))
                {
                    type = "heading";
                    headingLevel = 3;
                    content = line[4..];
                }
                else if (line.StartsWith("## ", StringComparison.Ordinal))
                {
                    type = "heading";
                    headingLevel = 2;
                    content = line[3..];
                }
                else if (line.StartsWith("# ", StringComparison.Ordinal))
                {
                    type = "heading";
                    headingLevel = 1;
                    content = line[2..];
                }
                else if (line.StartsWith("- [x] ", StringComparison.OrdinalIgnoreCase) || line.StartsWith("[x] ", StringComparison.OrdinalIgnoreCase))
                {
                    type = "checklist_item";
                    isChecked = true;
                    content = line.StartsWith("- ", StringComparison.Ordinal) ? line[6..] : line[4..];
                    checklistTotal++;
                    checklistCompleted++;
                }
                else if (line.StartsWith("- [ ] ", StringComparison.Ordinal) || line.StartsWith("[ ] ", StringComparison.Ordinal))
                {
                    type = "checklist_item";
                    isChecked = false;
                    content = line.StartsWith("- ", StringComparison.Ordinal) ? line[6..] : line[4..];
                    checklistTotal++;
                }
                else if (line.StartsWith("- ", StringComparison.Ordinal) || line.StartsWith("* ", StringComparison.Ordinal))
                {
                    type = "bullet_item";
                    content = line[2..];
                }
                else if (Regex.IsMatch(line, @"^\d+\.\s+"))
                {
                    type = "ordered_item";
                    content = Regex.Replace(line, @"^\d+\.\s+", string.Empty);
                }

                if (content.Length > MaxBlockTextLength)
                    content = content[..MaxBlockTextLength];

                var runs = content.Length > 0
                    ? new List<Dictionary<string, object?>> { new(StringComparer.Ordinal) { ["text"] = content } }
                    : new List<Dictionary<string, object?>>();

                var blockObj = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["id"] = id,
                    ["type"] = type,
                    ["runs"] = runs,
                };
                if (headingLevel.HasValue)
                    blockObj["level"] = headingLevel.Value;
                if (isChecked.HasValue)
                    blockObj["checked"] = isChecked.Value;

                blocks.Add(blockObj);
                if (!string.IsNullOrWhiteSpace(content))
                {
                    if (plainSb.Length > 0)
                        plainSb.Append('\n');
                    plainSb.Append(content.TrimEnd());
                }
            }

            var docPayload = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["type"] = "doc",
                ["version"] = 1,
                ["blocks"] = blocks,
            };

            return new NotebookDocumentParseResult
            {
                ContentJson = JsonSerializer.Serialize(docPayload, WriteOptions),
                ContentPlainText = plainSb.ToString(),
                ChecklistTotalCount = checklistTotal,
                ChecklistCompletedCount = checklistCompleted,
            };
        }
    }
}
