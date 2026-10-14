using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RikiPath.Application.Helpers;
using RikiPath.Application.Responses.Content;
using RikiPath.Domain.Entities;
using RikiPath.Domain.Enums;
using ApprovalRequest = RikiPath.Domain.Entities.ContentApprovalRequest;
using LessonEntity = RikiPath.Domain.Entities.Lesson;
using VocabularyEntity = RikiPath.Domain.Entities.Vocabulary;

namespace RikiPath.Application.Handlers
{
    public sealed class ApprovalDiffResult
    {
        public List<ApprovalFieldDiffResponse> Fields { get; } = new();
        public List<string> Warnings { get; } = new();
        public bool TargetMissing { get; set; }
    }

    /// <summary>
    /// Dựng bảng so sánh "dữ liệu hiện tại trong DB" với "dữ liệu Content Author đề xuất" cho 1 loại nội dung.
    /// Nạp bản ghi gốc của TẤT CẢ yêu cầu cùng loại bằng 1 truy vấn (tránh N+1). Loại nào chưa có builder riêng
    /// thì handler dùng <see cref="GenericApprovalDiff"/>.
    /// </summary>
    public interface IApprovalDiffBuilder
    {
        ImportEntityType EntityType { get; }
        /// <returns>Khóa = ContentApprovalRequest.Id.</returns>
        Task<Dictionary<int, ApprovalDiffResult>> BuildAsync(IReadOnlyList<ApprovalRequest> requests, CancellationToken cancellationToken);
    }

    /// <summary>Đọc DiffSummaryJson (field -> "cũ → mới") do bước import lưu lại.</summary>
    internal static class ApprovalSummary
    {
        public static Dictionary<string, string> Parse(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return new Dictionary<string, string>(StringComparer.Ordinal);
            try
            {
                return JsonSerializer.Deserialize<Dictionary<string, string>>(json)
                       ?? new Dictionary<string, string>(StringComparer.Ordinal);
            }
            catch (JsonException)
            {
                return new Dictionary<string, string>(StringComparer.Ordinal);
            }
        }

        /// <summary>Lấy phần "cũ" của "cũ → mới" (cũng chấp nhận "cũ => mới").</summary>
        public static string OldPart(string summaryValue)
        {
            var i = summaryValue.IndexOf(" → ", StringComparison.Ordinal);
            if (i < 0) i = summaryValue.IndexOf(" => ", StringComparison.Ordinal);
            return i >= 0 ? summaryValue[..i] : summaryValue;
        }
    }

    internal sealed class VocabularyApprovalDiffBuilder(IUnitOfWork unitOfWork) : IApprovalDiffBuilder
    {
        private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

        public ImportEntityType EntityType => ImportEntityType.Vocabulary;

        public async Task<Dictionary<int, ApprovalDiffResult>> BuildAsync(
            IReadOnlyList<ApprovalRequest> requests, CancellationToken cancellationToken)
        {
            var results = new Dictionary<int, ApprovalDiffResult>();
            var payloads = new Dictionary<int, VocabularyImportPayloadResponse>();

            foreach (var r in requests)
            {
                results[r.Id] = new ApprovalDiffResult();
                try
                {
                    var payload = JsonSerializer.Deserialize<VocabularyImportPayloadResponse>(r.PayloadJson, JsonOptions);
                    if (payload is null) throw new JsonException();
                    payloads[r.Id] = payload;
                }
                catch (JsonException)
                {
                    results[r.Id].Warnings.Add("Không đọc được PayloadJson của yêu cầu này.");
                }
            }

            // Bản ghi gốc cho các yêu cầu "cập nhật": 1 truy vấn cho cả lô.
            var targetIds = requests
                .Where(r => payloads.ContainsKey(r.Id) && r.TargetEntityId.HasValue)
                .Select(r => r.TargetEntityId!.Value)
                .Distinct()
                .ToList();

            var existingById = new Dictionary<int, VocabularyEntity>();
            if (targetIds.Count > 0)
            {
                // Đã sửa: Chuyển từ .Include(v => v.CertificateLevel) sang .Include(v => v.ContentLevelMappings!).ThenInclude(...)
                existingById = (await unitOfWork.Vocabularies.Query()
                    .Include(v => v.ContentLevelMappings!)
                        .ThenInclude(m => m.CertificateLevel)
                    .Include(v => v.LessonVocabularies!).ThenInclude(lv => lv.Lesson).ThenInclude(l => l.CertificateLevel)
                    .Where(v => targetIds.Contains(v.Id) && !v.IsDeleted)
                    .ToListAsync(cancellationToken)).ToDictionary(v => v.Id);
            }

            // Yêu cầu "thêm mới": kiểm tra từ đó đã được tạo trong lúc chờ duyệt chưa (tránh tạo trùng).
            var newWords = requests
                .Where(r => r.Action == ImportRowAction.New && payloads.ContainsKey(r.Id))
                .Select(r => payloads[r.Id].Word)
                .Distinct()
                .ToList();

            var existingKeys = new HashSet<string>(StringComparer.Ordinal);
            if (newWords.Count > 0)
            {
                var found = await unitOfWork.Vocabularies.Query()
                    .Where(v => !v.IsDeleted && newWords.Contains(v.Word))
                    .Select(v => new { v.Word, v.Reading })
                    .ToListAsync(cancellationToken);
                foreach (var x in found) existingKeys.Add($"{x.Word}|{x.Reading}");
            }

            foreach (var r in requests)
            {
                if (!payloads.TryGetValue(r.Id, out var p)) continue;
                var result = results[r.Id];

                VocabularyEntity? existing = null;
                if (r.Action == ImportRowAction.Modified)
                {
                    if (r.TargetEntityId is int id && existingById.TryGetValue(id, out var found))
                    {
                        existing = found;
                    }
                    else
                    {
                        result.TargetMissing = true;
                        result.Warnings.Add("Bản ghi cần cập nhật không còn trong hệ thống (đã bị xóa hoặc không tồn tại).");
                    }
                }
                else if (existingKeys.Contains($"{p.Word}|{p.Reading}"))
                {
                    result.Warnings.Add($"Từ '{p.Word}' ({p.Reading}) hiện đã có trong hệ thống: duyệt yêu cầu thêm mới này sẽ tạo bản ghi trùng.");
                }

                var hasOld = existing is not null;
                result.Fields.Add(Field("word", "Từ", existing?.Word, p.Word, hasOld));
                result.Fields.Add(Field("reading", "Cách đọc", existing?.Reading, p.Reading, hasOld));
                result.Fields.Add(Field("meaning", "Nghĩa", existing?.Meaning, p.Meaning, hasOld));
                result.Fields.Add(Field("exampleSentence", "Câu ví dụ", existing?.ExampleSentence, p.ExampleSentence, hasOld));
                result.Fields.Add(Field("exampleSentenceMeaning", "Nghĩa câu ví dụ", existing?.ExampleSentenceMeaning, p.ExampleSentenceMeaning, hasOld));
                result.Fields.Add(Field("audioUrl", "Audio", existing?.AudioUrl, p.AudioUrl, hasOld, skipWhenNewEmpty: true));

                // Đã sửa: Lấy Level Code từ tập hợp ContentLevelMappings
                var existingLevelCode = existing?.ContentLevelMappings?
                    .Where(m => !m.IsDeleted && m.CertificateLevel != null)
                    .Select(m => m.CertificateLevel.Code)
                    .FirstOrDefault();

                result.Fields.Add(Field("certificateLevelCode", "Level", existingLevelCode, p.CertificateLevelCode, hasOld));
                result.Fields.Add(LessonField(existing, p));
            }

            return results;
        }

        private static ApprovalFieldDiffResponse Field(
            string key, string label, string? oldValue, string? newValue, bool hasOld, bool skipWhenNewEmpty = false)
        {
            bool changed;
            if (!hasOld) changed = !string.IsNullOrWhiteSpace(newValue);
            else if (skipWhenNewEmpty && string.IsNullOrWhiteSpace(newValue)) changed = false;
            else changed = !string.Equals(oldValue ?? string.Empty, newValue ?? string.Empty, StringComparison.Ordinal);

            return new ApprovalFieldDiffResponse
            {
                Field = key,
                Label = label,
                OldValue = hasOld ? oldValue : null,
                NewValue = newValue,
                IsChanged = changed,
            };
        }

        /// <summary>Cùng quy tắc và cùng định dạng chuỗi với ImportHandlerSupport.CompareLesson.</summary>
        private static ApprovalFieldDiffResponse LessonField(VocabularyEntity? existing, VocabularyImportPayloadResponse p)
        {
            var hasNew = !string.IsNullOrWhiteSpace(p.LessonTitle);
            var newText = hasNew ? $"{p.LessonTitle} ({p.CertificateLevelCode})" : null;

            if (existing is null)
                return new ApprovalFieldDiffResponse { Field = "lessonTitle", Label = "Bài học", OldValue = null, NewValue = newText, IsChanged = hasNew };

            var current = (existing.LessonVocabularies?.Select(lv => lv.Lesson) ?? Enumerable.Empty<LessonEntity?>())
                .Where(l => l is { IsDeleted: false })
                .Select(l => l!)
                .ToList();

            var oldText = current.Count == 0
                ? "(chưa thuộc bài nào)"
                : string.Join(", ", current.Select(l => $"{l.Title} ({l.CertificateLevel?.Code})"));

            var changed = hasNew && !current.Any(l => l.Title == p.LessonTitle && l.CertificateLevelId == p.CertificateLevelId);

            return new ApprovalFieldDiffResponse { Field = "lessonTitle", Label = "Bài học", OldValue = oldText, NewValue = newText, IsChanged = changed };
        }
    }

    /// <summary>
    /// Dự phòng cho loại nội dung chưa có builder riêng: giá trị mới lấy từ PayloadJson, giá trị cũ chỉ lấy được
    /// từ DiffSummaryJson (có cho các trường đã thay đổi).
    /// </summary>
    internal static class GenericApprovalDiff
    {
        public static ApprovalDiffResult Build(ApprovalRequest r, Dictionary<string, string> summary)
        {
            var result = new ApprovalDiffResult();
            result.Warnings.Add(
                $"Loại '{r.EntityType}' chưa có bộ so sánh riêng: giá trị cũ lấy từ bản tóm tắt lưu lúc import nên chỉ có ở các trường đã thay đổi.");

            try
            {
                using var doc = JsonDocument.Parse(r.PayloadJson);
                if (doc.RootElement.ValueKind != JsonValueKind.Object) throw new JsonException();

                foreach (var prop in doc.RootElement.EnumerateObject())
                {
                    if (prop.Name.EndsWith("Id", StringComparison.Ordinal)) continue;

                    var key = char.ToLowerInvariant(prop.Name[0]) + prop.Name[1..];
                    var newValue = prop.Value.ValueKind switch
                    {
                        JsonValueKind.Null => null,
                        JsonValueKind.String => prop.Value.GetString(),
                        _ => prop.Value.GetRawText(),
                    };

                    string? oldValue = newValue;
                    bool changed;
                    if (r.Action == ImportRowAction.New)
                    {
                        oldValue = null;
                        changed = !string.IsNullOrWhiteSpace(newValue);
                    }
                    else if (summary.TryGetValue(key, out var s))
                    {
                        oldValue = ApprovalSummary.OldPart(s);
                        changed = true;
                    }
                    else
                    {
                        changed = false;
                    }

                    result.Fields.Add(new ApprovalFieldDiffResponse
                    {
                        Field = key,
                        Label = key,
                        OldValue = oldValue,
                        NewValue = newValue,
                        IsChanged = changed,
                    });
                }
            }
            catch (JsonException)
            {
                result.Warnings.Add("Không đọc được PayloadJson của yêu cầu này.");
            }

            return result;
        }
    }
}