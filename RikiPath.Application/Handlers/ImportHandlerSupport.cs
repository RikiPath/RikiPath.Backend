using Microsoft.EntityFrameworkCore;
using RikiPath.Application.Helpers;
using RikiPath.Application.Responses.Content;
using RikiPath.Domain.Enums;

namespace RikiPath.Application.Handlers
{
    /// <summary>
    /// Phần dùng chung của các ISheetImportHandler: nạp dữ liệu đã có 1 lần (tránh N+1), kiểm tra "đã có chưa"
    /// (bảng chính + bản nháp đang chờ duyệt), so sánh field, đếm kết quả.
    /// </summary>
    internal static class ImportHandlerSupport
    {
        /// <summary>
        /// Lập chỉ mục theo khóa tự nhiên. Khóa nào xuất hiện nhiều lần trong DB thì KHÔNG ném lỗi (ToDictionary sẽ ném)
        /// mà đưa vào tập "ambiguous" để dòng Excel tương ứng báo Error rõ ràng.
        /// </summary>
        public static (Dictionary<string, T> Unique, HashSet<string> Ambiguous) IndexByKey<T>(
            IEnumerable<T> items, Func<T, string> keySelector, StringComparer? comparer = null)
        {
            comparer ??= StringComparer.Ordinal;
            var unique = new Dictionary<string, T>(comparer);
            var ambiguous = new HashSet<string>(comparer);
            foreach (var item in items)
            {
                var key = keySelector(item);
                if (ambiguous.Contains(key)) continue;
                if (!unique.TryAdd(key, item))
                {
                    unique.Remove(key);
                    ambiguous.Add(key);
                }
            }
            return (unique, ambiguous);
        }

        /// <summary>Code -> Id của các CertificateLevel chưa xóa; Code trùng ở nhiều level thì vào tập ambiguous.</summary>
        public static async Task<(Dictionary<string, int> LevelsByCode, HashSet<string> Ambiguous)> LoadLevelsAsync(
            IUnitOfWork unitOfWork, CancellationToken cancellationToken)
        {
            var levels = await unitOfWork.CertificateLevels.Query()
                .Where(l => !l.IsDeleted)
                .Select(l => new { l.Id, l.Code })
                .ToListAsync(cancellationToken);

            var (unique, ambiguous) = IndexByKey(levels, l => l.Code);
            return (unique.ToDictionary(x => x.Key, x => x.Value.Id, StringComparer.Ordinal), ambiguous);
        }

        /// <summary>
        /// true  = dòng có bản nháp đang chờ duyệt (Draft/PendingReview) bị chặn bằng lỗi, không so sánh với DB.
        /// false = vẫn so sánh đầy đủ với dữ liệu chính trong DB: Chưa có (New) / Cập nhật (Modified) / Đã có (Unchanged).
        /// </summary>
        public const bool BlockOnOpenDraft = false;

        /// <summary>
        /// RowKey của các bản nháp đang mở (Draft hoặc PendingReview) cùng loại nội dung.
        /// Khi <see cref="BlockOnOpenDraft"/> = false trả về tập rỗng (bỏ qua truy vấn) để các handler không còn chặn dòng.
        /// </summary>
        public static async Task<HashSet<string>> LoadOpenRowKeysAsync(
            IUnitOfWork unitOfWork, ImportEntityType entityType, CancellationToken cancellationToken)
        {
            if (!BlockOnOpenDraft)
                return new HashSet<string>(StringComparer.Ordinal);

            var keys = await unitOfWork.ContentApprovalRequests.Query()
                .Where(x => !x.IsDeleted
                    && x.EntityType == entityType
                    && (x.Status == ContentApprovalStatus.Draft || x.Status == ContentApprovalStatus.PendingReview))
                .Select(x => x.RowKey)
                .ToListAsync(cancellationToken);
            return keys.ToHashSet(StringComparer.Ordinal);
        }

        public static void CheckLevel(
            string? levelCode, Dictionary<string, int> levelsByCode, HashSet<string> ambiguousLevelCodes, List<string> errors)
        {
            if (string.IsNullOrWhiteSpace(levelCode))
                errors.Add("Thiếu cột Certificate Level Code.");
            else if (ambiguousLevelCodes.Contains(levelCode))
                errors.Add($"Certificate Level Code '{levelCode}' bị trùng ở nhiều loại chứng chỉ, không xác định được level.");
            else if (!levelsByCode.ContainsKey(levelCode))
                errors.Add($"Certificate Level Code '{levelCode}' không tồn tại trong hệ thống.");
        }

        public static void CompareField(Dictionary<string, string> changed, string field, string? oldValue, string? newValue)
        {
            var a = oldValue ?? string.Empty;
            var b = newValue ?? string.Empty;
            if (!string.Equals(a, b, StringComparison.Ordinal))
                changed[field] = $"{a} → {b}";
        }

        // ===================== Dùng cho handler gộp nhiều dòng Excel thành 1 thực thể (Mock Test, Practice Exercise) =====================

        /// <summary>Trim; chuỗi rỗng/chỉ có khoảng trắng -> null.</summary>
        public static string? Clean(string? raw) => string.IsNullOrWhiteSpace(raw) ? null : raw.Trim();

        public static bool TryPositiveInt(string? raw, out int value) => int.TryParse(raw, out value) && value > 0;

        /// <summary>
        /// Thuộc tính cấp "cha" lặp ở nhiều dòng (tiêu đề đề thi, thời gian...): lấy giá trị khác rỗng đầu tiên;
        /// các dòng sau được để trống, nhưng nếu có ghi thì phải trùng - lệch nhau là lỗi (thường do gõ nhầm).
        /// </summary>
        public static string? Consistent(IReadOnlyList<ExcelRow> group, string label, Func<ExcelRow, string?> selector, List<string> errors)
        {
            string? value = null;
            ExcelRow? source = null;
            foreach (var r in group)
            {
                var v = Clean(selector(r));
                if (v is null) continue;
                if (value is null) { value = v; source = r; }
                else if (!string.Equals(value, v, StringComparison.Ordinal))
                    errors.Add($"Dòng {r.RowNumber}: {label} '{v}' khác với dòng {source!.RowNumber} ('{value}').");
            }
            return value;
        }

        public static string? Consistent(IReadOnlyList<ExcelRow> group, string header, List<string> errors)
            => Consistent(group, header, r => r.Get(header), errors);

        /// <summary>Cột URL tùy chọn: rỗng -> null; có giá trị thì phải là http/https hợp lệ.</summary>
        public static string? CheckUrl(string? raw, string label, string rowPrefix, List<string> errors)
        {
            var v = Clean(raw);
            if (v is null) return null;
            if (!Uri.TryCreate(v, UriKind.Absolute, out var uri)
                || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                errors.Add($"{rowPrefix}{label} phải là đường dẫn http/https hợp lệ.");
                return null;
            }
            return v;
        }

        /// <summary>
        /// Đọc Option1..4 + số thứ tự đáp án đúng. Cần >= 2 đáp án, không được bỏ trống giữa chừng
        /// (Option3 trống mà Option4 có), và đáp án đúng phải trỏ vào 1 đáp án có nội dung. Lỗi -> thêm vào errors và trả null.
        /// </summary>
        public static List<ImportOptionPayloadResponse>? ParseOptions(
            ExcelRow row, IReadOnlyList<string> optionHeaders, string correctHeader, string rowPrefix, List<string> errors)
        {
            var before = errors.Count;
            var texts = optionHeaders.Select(h => Clean(row.Get(h))).ToList();

            var lastFilled = texts.FindLastIndex(t => t is not null);
            for (var i = 0; i < lastFilled; i++)
            {
                if (texts[i] is null)
                {
                    errors.Add($"{rowPrefix}{optionHeaders[i]} bị trống nhưng {optionHeaders[lastFilled]} có giá trị.");
                    break;
                }
            }
            if (texts.Count(t => t is not null) < 2)
                errors.Add($"{rowPrefix}Cần ít nhất 2 đáp án.");

            var correctRaw = Clean(row.Get(correctHeader));
            var correct = 0;
            if (correctRaw is null)
                errors.Add($"{rowPrefix}Thiếu cột {correctHeader}.");
            else if (!int.TryParse(correctRaw, out correct) || correct < 1 || correct > texts.Count || texts[correct - 1] is null)
                errors.Add($"{rowPrefix}{correctHeader} '{correctRaw}' không trỏ tới một đáp án có nội dung.");

            if (errors.Count > before) return null;

            var list = new List<ImportOptionPayloadResponse>();
            for (var i = 0; i < texts.Count; i++)
            {
                if (texts[i] is null) continue;
                list.Add(new ImportOptionPayloadResponse { SortOrder = i + 1, OptionText = texts[i]!, IsCorrect = i + 1 == correct });
            }
            return list;
        }

        /// <summary>Chuỗi đại diện nội dung 1 câu hỏi, để so sánh câu cũ (DB) với câu mới (Excel).</summary>
        public static string QuestionSignature(
            string sectionTitle, string? text, string? explanation, string? audioUrl, string? imageUrl,
            IEnumerable<(string Text, bool IsCorrect)> options)
        {
            var opts = options.ToList();
            var correctIndex = opts.FindIndex(o => o.IsCorrect) + 1; // 0 nếu không có đáp án đúng
            return string.Join("\u001F",
                sectionTitle.Trim(), text?.Trim() ?? "", explanation?.Trim() ?? "", audioUrl?.Trim() ?? "", imageUrl?.Trim() ?? "",
                string.Join("|", opts.Select(o => o.Text.Trim())), correctIndex);
        }

        /// <summary>Tóm tắt khác biệt giữa các câu hỏi cũ/mới theo số thứ tự câu; null nếu giống hệt.</summary>
        public static string? QuestionDiffSummary(IReadOnlyDictionary<int, string> existing, IReadOnlyDictionary<int, string> incoming)
        {
            var added = incoming.Keys.Where(k => !existing.ContainsKey(k)).OrderBy(k => k).ToList();
            var removed = existing.Keys.Where(k => !incoming.ContainsKey(k)).OrderBy(k => k).ToList();
            var edited = incoming.Where(kv => existing.TryGetValue(kv.Key, out var old) && old != kv.Value)
                .Select(kv => kv.Key).OrderBy(k => k).ToList();

            if (added.Count + removed.Count + edited.Count == 0) return null;

            var parts = new List<string>();
            if (added.Count > 0) parts.Add($"thêm câu {string.Join(", ", added)}");
            if (edited.Count > 0) parts.Add($"sửa câu {string.Join(", ", edited)}");
            if (removed.Count > 0) parts.Add($"xóa câu {string.Join(", ", removed)}");
            return string.Join("; ", parts);
        }

        /// <summary>
        /// So khớp bài học Excel ghi với (các) bài mà nội dung ĐANG thuộc về trong DB, để báo cho Content Author
        /// biết nội dung bị chuyển bài. Bài học là duy nhất theo (Title, CertificateLevelId) nên so cả level:
        /// "Bài 1" của N5 và "Bài 1" của N4 là 2 bài khác nhau.
        /// Ví dụ kết quả: "Bài 1 (N5) → Bài 2 (N5)" hoặc "(chưa thuộc bài nào) → Bài 2 (N5)".
        /// </summary>
        public static void CompareLesson(
            Dictionary<string, string> changed,
            string? lessonTitle,
            int levelId,
            string levelCode,
            IEnumerable<Domain.Entities.Lesson?>? currentLessons)
        {
            if (string.IsNullOrWhiteSpace(lessonTitle)) return;

            var current = (currentLessons ?? Enumerable.Empty<Domain.Entities.Lesson?>())
                .Where(l => l is { IsDeleted: false })
                .Select(l => l!)
                .ToList();

            if (current.Any(l => l.Title == lessonTitle && l.CertificateLevelId == levelId)) return;

            var from = current.Count == 0
                ? "(chưa thuộc bài nào)"
                : string.Join(", ", current.Select(l => $"{l.Title} ({l.CertificateLevel?.Code})"));
            changed["lessonTitle"] = $"{from} → {lessonTitle} ({levelCode})";
        }

        public static void Tally(SheetImportResponse result, ImportRowDiffResponse diff)
        {
            switch (diff.Action)
            {
                case ImportRowAction.New: result.NewCount++; break;
                case ImportRowAction.Modified: result.ModifiedCount++; break;
                case ImportRowAction.Unchanged: result.UnchangedCount++; break;
                case ImportRowAction.Error: result.ErrorCount++; break;
            }
        }
    }
}