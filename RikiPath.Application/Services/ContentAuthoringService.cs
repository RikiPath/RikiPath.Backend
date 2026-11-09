using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RikiPath.Application.Exceptions;
using RikiPath.Application.IClients;
using RikiPath.Application.IServices;
using RikiPath.Application.Requests.ContentAuthoring;
using RikiPath.Application.Responses;
using RikiPath.Application.Responses.ContentAuthoring;
using RikiPath.Domain.Entities;
using RikiPath.Domain.Enums;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace RikiPath.Application.Services
{
    /// <summary>
    /// AI hỗ trợ content author soạn nội dung: Kanji, Vocabulary, GrammarPattern, Lesson,
    /// PracticeExercise, MockTest. Mọi loại đều theo pattern 2 bước (Suggest -> Confirm).
    ///
    /// GIẢ ĐỊNH CẦN XÁC NHẬN (không có trong Entities.zip, đã đánh dấu TODO ở chỗ dùng):
    ///  - unitOfWork có các repo: Vocabularies, GrammarPatterns, LanguageSkills, CertificateLevels, CertificateSections.
    ///  - ImportEntityType có các giá trị: Kanji, Vocabulary, Lesson, PracticeExercise, MockTest và một giá trị cho ngữ pháp (GrammarPattern hoặc Grammar).
    ///  - Tầng Apply (sau khi admin duyệt) deserialize PayloadJson theo đúng shape ở các hàm Confirm* bên dưới.
    /// </summary>
    public class ContentAuthoringService(
        IUnitOfWork unitOfWork,
        IAiContentAuthoringClient aiClient,
        IClaimService claimService,
        ILogger<ContentAuthoringService> logger) : IContentAuthoringService
    {
        private const int MaxItemsAllowed = 30;
        private const int MaxAiAttempts = 2;

        private const int ChoiceOptionCount = 4;
        private const int MaxQuestionsPerSection = 20;
        private const int MaxMockSections = 6;
        private const int MaxPracticeQuestions = 20;
        private const int BlindSolveChunkSize = 40;
        private const int MaxPromptCandidates = 200;

        private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

        /// <summary>
        /// Giá trị ImportEntityType dành cho ngữ pháp. Dò theo tên (GrammarPattern / Grammar) để service vẫn build
        /// được dù enum đặt tên nào. Khuyến nghị: thêm đúng giá trị "GrammarPattern" vào enum ImportEntityType.
        /// </summary>
        internal static readonly ImportEntityType? GrammarEntityType = ResolveGrammarEntityType();

        private static ImportEntityType? ResolveGrammarEntityType()
        {
            foreach (var name in new[] { "GrammarPattern", "Grammar", "GrammarPatterns" })
            {
                if (Enum.TryParse<ImportEntityType>(name, ignoreCase: true, out var value) &&
                    Enum.IsDefined(typeof(ImportEntityType), value))
                    return value;
            }
            return null;
        }

        // =====================================================================================
        // SYSTEM PROMPTS
        // =====================================================================================

        private const string KanjiSystemPrompt = """
            Bạn là trợ lý hỗ trợ content author soạn nội dung Hán tự (Kanji) cho ứng dụng học tiếng Nhật.
            Chỉ đề xuất Kanji CÓ THẬT, đúng ý nghĩa/cách đọc/số nét theo kiến thức tiếng Nhật chuẩn. Không bịa.
            Không đề xuất trùng lặp ký tự trong cùng một lần trả lời.
            BẮT BUỘC: Toàn bộ nội dung (meaning, rationale) phải viết 100% bằng TIẾNG VIỆT, không dùng tiếng Anh.
            Chỉ trả về một JSON object hợp lệ, không Markdown, đúng cấu trúc:
            { "items": [ { "character": string (1 ký tự Hán), "meaning": string (nghĩa tiếng Việt),
              "sinoVietnamese": string (âm Hán Việt), "onYomi": string (âm On), "kunYomi": string (âm Kun),
              "strokeCount": int (số nét), "rationale": string (lý do chọn, 1 câu ngắn tiếng Việt) } ] }
            """;

        private const string KanjiAccuracyCheckSystemPrompt = """
            Bạn là chuyên gia tiếng Nhật. Nhiệm vụ: KIỂM TRA ĐỘ CHÍNH XÁC của dữ liệu Kanji được cung cấp
            sẵn (KHÔNG đề xuất Kanji mới, KHÔNG thêm/bớt mục nào). Với mỗi mục, đối chiếu ký tự, nghĩa,
            âm Hán Việt, âm On, âm Kun, số nét với kiến thức tiếng Nhật chuẩn.
            Nếu MỘT trường bất kỳ sai hoặc không chính xác, liệt kê rõ: tên trường + giá trị đang sai +
            giá trị đúng nên là gì. Nếu tất cả các trường đều đúng, đánh dấu isAccurate = true và issues rỗng.
            BẮT BUỘC: issues viết 100% bằng TIẾNG VIỆT, không dùng tiếng Anh.
            Chỉ trả về một JSON object hợp lệ, không Markdown, đúng cấu trúc:
            { "results": [ { "id": int (giữ nguyên đúng id đã cho, không đổi),
              "isAccurate": boolean, "issues": string[] } ] }
            Phải trả đủ kết quả cho TẤT CẢ các id được cung cấp, không được bỏ sót id nào.
            """;

        private const string VocabularySystemPrompt = """
            Bạn là trợ lý hỗ trợ content author soạn TỪ VỰNG tiếng Nhật cho ứng dụng học JLPT.
            Chỉ đề xuất từ CÓ THẬT, đúng cách đọc và đúng nghĩa. Không bịa. Nếu KHÔNG chắc chắn về cách đọc
            hoặc nghĩa của một từ thì BỎ từ đó, đừng đoán.
            - reading: viết bằng hiragana/katakana (không romaji, không kanji).
            - exampleSentence: một câu tiếng Nhật tự nhiên, ngắn, đúng cấp độ, có chứa từ đó (được phép chia thể).
            - Không đề xuất trùng lặp trong cùng một lần trả lời.
            BẮT BUỘC: meaning, exampleSentenceMeaning, rationale viết 100% bằng TIẾNG VIỆT, không dùng tiếng Anh.
            Chỉ trả về một JSON object hợp lệ, không Markdown, đúng cấu trúc:
            { "items": [ { "word": string, "reading": string, "meaning": string,
              "exampleSentence": string, "exampleSentenceMeaning": string,
              "rationale": string (lý do chọn, 1 câu ngắn tiếng Việt) } ] }
            """;

        private const string GrammarSystemPrompt = """
            Bạn là trợ lý hỗ trợ content author soạn NGỮ PHÁP tiếng Nhật cho ứng dụng học JLPT.
            Chỉ đề xuất mẫu ngữ pháp CÓ THẬT, đúng cấu trúc và cách dùng. Không bịa. Nếu không chắc chắn thì BỎ mục đó.
            - title: tên mẫu ngữ pháp ngắn gọn (VD: "〜ながら").
            - structure: công thức kết hợp (VD: "V-ます(bỏ ます) + ながら").
            - usageNotes: giải thích cách dùng, sắc thái, lưu ý, bằng tiếng Việt.
            - exampleSentence: câu tiếng Nhật tự nhiên, đúng mẫu, đúng cấp độ.
            - Không đề xuất trùng lặp trong cùng một lần trả lời.
            BẮT BUỘC: usageNotes, exampleSentenceMeaning, rationale viết 100% bằng TIẾNG VIỆT, không dùng tiếng Anh.
            Chỉ trả về một JSON object hợp lệ, không Markdown, đúng cấu trúc:
            { "items": [ { "title": string, "structure": string, "usageNotes": string,
              "exampleSentence": string, "exampleSentenceMeaning": string,
              "rationale": string (lý do chọn, 1 câu ngắn tiếng Việt) } ] }
            """;

        private const string VocabularyAccuracyCheckSystemPrompt = """
            Bạn là chuyên gia tiếng Nhật. Nhiệm vụ: KIỂM TRA ĐỘ CHÍNH XÁC của dữ liệu TỪ VỰNG được cung cấp
            (KHÔNG đề xuất từ mới, KHÔNG thêm/bớt mục). Mỗi mục là một JSON có các trường word, reading, meaning,
            exampleSentence, exampleSentenceMeaning. Đối chiếu: cách đọc có đúng với từ không, nghĩa có đúng không,
            câu ví dụ có đúng ngữ pháp/tự nhiên, có chứa từ đó không, và bản dịch có khớp câu ví dụ không.
            Nếu MỘT trường bất kỳ sai, liệt kê rõ: tên trường + giá trị đang sai + giá trị đúng nên là gì.
            Nếu tất cả đúng, isAccurate = true và issues rỗng.
            BẮT BUỘC: issues viết 100% bằng TIẾNG VIỆT.
            Chỉ trả về một JSON object hợp lệ, không Markdown, đúng cấu trúc:
            { "results": [ { "id": int (giữ nguyên đúng id đã cho), "isAccurate": boolean, "issues": string[] } ] }
            Phải trả đủ kết quả cho TẤT CẢ các id được cung cấp.
            """;

        private const string GrammarAccuracyCheckSystemPrompt = """
            Bạn là chuyên gia tiếng Nhật. Nhiệm vụ: KIỂM TRA ĐỘ CHÍNH XÁC của dữ liệu NGỮ PHÁP được cung cấp
            (KHÔNG đề xuất mẫu mới, KHÔNG thêm/bớt mục). Mỗi mục là một JSON có các trường title, structure,
            usageNotes, exampleSentence, exampleSentenceMeaning. Đối chiếu: cấu trúc kết hợp có đúng không,
            giải thích cách dùng có đúng không, câu ví dụ có đúng mẫu và tự nhiên không, bản dịch có khớp không.
            Nếu MỘT trường bất kỳ sai, liệt kê rõ: tên trường + giá trị đang sai + giá trị đúng nên là gì.
            Nếu tất cả đúng, isAccurate = true và issues rỗng.
            BẮT BUỘC: issues viết 100% bằng TIẾNG VIỆT.
            Chỉ trả về một JSON object hợp lệ, không Markdown, đúng cấu trúc:
            { "results": [ { "id": int (giữ nguyên đúng id đã cho), "isAccurate": boolean, "issues": string[] } ] }
            Phải trả đủ kết quả cho TẤT CẢ các id được cung cấp.
            """;

        private const string LessonSystemPrompt = """
            Bạn là trợ lý hỗ trợ content author thiết kế BÀI HỌC tiếng Nhật cho ứng dụng học JLPT.
            Nhiệm vụ: đề xuất tiêu đề, mô tả và CHỌN nội dung liên kết cho bài học.
            - CHỈ chọn Kanji / từ vựng / ngữ pháp trong danh sách ỨNG VIÊN được cung cấp. Tuyệt đối không bịa
              mục không có trong danh sách. Nếu danh sách ứng viên không đủ phù hợp, chọn ít hơn thay vì bịa.
            - Chỉ chọn những mục thực sự liên quan đến chủ đề bài học.
            - Không đặt tiêu đề trùng với các bài học đã có (được liệt kê).
            BẮT BUỘC: title, description, rationale viết 100% bằng TIẾNG VIỆT (từ tiếng Nhật được phép giữ nguyên).
            Chỉ trả về một JSON object hợp lệ, không Markdown, đúng cấu trúc:
            { "title": string, "description": string,
              "kanji": [ string (1 ký tự Hán) ],
              "vocabulary": [ { "word": string, "reading": string } ],
              "grammar": [ string (title mẫu ngữ pháp đúng như trong danh sách) ],
              "rationale": string (1-2 câu tiếng Việt) }
            """;

        internal const string ChoiceQuestionSystemPrompt = """
            Bạn là giáo viên tiếng Nhật chuyên soạn đề trắc nghiệm theo phong cách JLPT cho ứng dụng học tiếng Nhật.
            QUY TẮC BẮT BUỘC cho MỖI câu hỏi:
            - Có đúng 4 đáp án, trong đó đúng 1 đáp án đúng (isCorrect = true), 3 đáp án nhiễu hợp lý nhưng chắc chắn sai.
            - Câu hỏi phải có ĐÚNG MỘT đáp án đúng rõ ràng, không mơ hồ. Không dùng đáp án kiểu "tất cả các đáp án trên".
            - Bốn đáp án không được trùng nhau.
            - questionText viết bằng tiếng Nhật theo dạng đề JLPT (có thể kèm chỉ dẫn ngắn).
            - explanation viết 100% bằng TIẾNG VIỆT: giải thích vì sao đáp án đúng là đúng và vì sao các đáp án còn lại sai.
            - Chỉ dùng kiến thức tiếng Nhật chuẩn, đúng cấp độ được yêu cầu. Không bịa.
            - Nếu là phần NGHE: mỗi câu phải có audioScript (lời thoại/đoạn nghe bằng tiếng Nhật) đủ để trả lời câu hỏi;
              câu hỏi chỉ dựa vào nội dung audioScript. Nếu KHÔNG phải phần nghe: audioScript = null.
            Chỉ trả về một JSON object hợp lệ, không Markdown, đúng cấu trúc:
            { "title": string (chỉ cần khi được yêu cầu, ngược lại để chuỗi rỗng),
              "description": string (chỉ cần khi được yêu cầu, ngược lại để chuỗi rỗng),
              "questions": [ { "questionText": string, "audioScript": string | null, "explanation": string,
                "options": [ { "text": string, "isCorrect": boolean } ] } ] }
            """;

        private const string BlindSolveSystemPrompt = """
            Bạn là giáo viên tiếng Nhật đang tự làm một đề trắc nghiệm. Hãy giải TỪNG câu độc lập, chọn đáp án
            đúng nhất theo kiến thức tiếng Nhật chuẩn. Với câu nghe, dựa vào "Nội dung nghe" được cho.
            Nếu một câu mơ hồ (có hơn một đáp án hợp lý, hoặc không có đáp án nào đúng), đặt isAmbiguous = true
            và vẫn chọn đáp án phù hợp nhất.
            Chỉ trả về một JSON object hợp lệ, không Markdown, đúng cấu trúc:
            { "answers": [ { "q": int (đúng số câu đã cho), "choice": "A" | "B" | "C" | "D", "isAmbiguous": boolean } ] }
            Phải trả đủ TẤT CẢ các câu.
            """;

        // =====================================================================================
        // SCAN
        // =====================================================================================

        public async Task<ApiResponse<ScanContentAccuracyResponse>> ScanMyDraftContentAsync(
            ScanContentAccuracyRequest request, CancellationToken cancellationToken)
        {
            try
            {
                var userId = claimService.GetUserClaim().Id;

                var draftRows = await unitOfWork.ContentApprovalRequests.Query()
                    .Where(r => r.SubmittedByUserId == userId &&
                                r.Status == ContentApprovalStatus.Draft &&
                                (!request.BatchId.HasValue || r.BatchId == request.BatchId.Value))
                    .OrderBy(r => r.SourceRowNumber)
                    .ToListAsync(cancellationToken);

                if (draftRows.Count == 0)
                    return ApiResponse<ScanContentAccuracyResponse>.Fail(
                        "Không có nội dung nào ở trạng thái chờ duyệt để kiểm tra.");

                var results = new List<ContentAccuracyResult>();

                foreach (var group in draftRows.GroupBy(r => r.EntityType))
                {
                    var rows = group.ToList();

                    var entityType = group.Key;
                    List<ContentAccuracyResult> groupResults;

                    if (entityType == ImportEntityType.Kanji)
                        groupResults = await ScanKanjiAccuracyAsync(userId, rows, cancellationToken);
                    else if (entityType == ImportEntityType.Vocabulary)
                        groupResults = await ScanTextAccuracyAsync(userId, rows, VocabularyAccuracyCheckSystemPrompt, cancellationToken);
                    else if (GrammarEntityType.HasValue && entityType == GrammarEntityType.Value)
                        groupResults = await ScanTextAccuracyAsync(userId, rows, GrammarAccuracyCheckSystemPrompt, cancellationToken);
                    else if (entityType == ImportEntityType.PracticeExercise)
                        groupResults = await ScanChoiceQuestionsAsync(userId, rows, isMockTest: false, cancellationToken);
                    else if (entityType == ImportEntityType.MockTest)
                        groupResults = await ScanChoiceQuestionsAsync(userId, rows, isMockTest: true, cancellationToken);
                    else
                        groupResults = rows.Select(UnsupportedResult).ToList();

                    results.AddRange(groupResults);
                }

                var issueCount = results.Count(r => r.IsSupported && !r.IsAccurate);
                return ApiResponse<ScanContentAccuracyResponse>.Success(new ScanContentAccuracyResponse
                {
                    Results = results,
                    IssueCount = issueCount,
                });
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to scan draft content accuracy.");
                return ApiResponse<ScanContentAccuracyResponse>.Fail(
                    "Không thể kiểm tra nội dung lúc này, vui lòng thử lại.", HttpStatusCode.InternalServerError);
            }
        }

        private static ContentAccuracyResult UnsupportedResult(ContentApprovalRequest row) => new()
        {
            ApprovalRequestId = row.Id,
            EntityType = row.EntityType.ToString(),
            RowKey = row.RowKey,
            IsSupported = false,
            IsAccurate = true,
            Issues = new List<string> { $"Loại nội dung {row.EntityType} chưa được AI hỗ trợ kiểm tra tự động." },
        };

        private static ContentAccuracyResult BrokenResult(ContentApprovalRequest row) => new()
        {
            ApprovalRequestId = row.Id,
            EntityType = row.EntityType.ToString(),
            RowKey = row.RowKey,
            IsSupported = true,
            IsAccurate = false,
            Issues = new List<string> { "Dữ liệu đã lưu bị lỗi định dạng, không đọc được để kiểm tra." },
        };

        private static ContentAccuracyResult CouldNotCheckResult(ContentApprovalRequest row, string? reason) => new()
        {
            ApprovalRequestId = row.Id,
            EntityType = row.EntityType.ToString(),
            RowKey = row.RowKey,
            IsSupported = true,
            IsAccurate = false,
            Issues = new List<string> { $"Không kiểm tra được bằng AI lúc này ({reason}). Vui lòng thử lại." },
        };

        private async Task<List<ContentAccuracyResult>> ScanKanjiAccuracyAsync(
            int userId, List<ContentApprovalRequest> kanjiRows, CancellationToken cancellationToken)
        {
            // Gửi kèm Id thật của ContentApprovalRequest để khớp ngược kết quả AI trả về — tránh phụ
            // thuộc vào RowKey (có thể trùng ký tự Kanji nếu author gửi lặp) để match sai dòng.
            var payloads = kanjiRows.Select(row =>
            {
                KanjiPayloadForAccuracyCheck? payload = null;
                try
                {
                    payload = JsonSerializer.Deserialize<KanjiPayloadForAccuracyCheck>(
                        row.PayloadJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                }
                catch (JsonException)
                {
                    // PayloadJson hỏng -> coi như không kiểm tra được, trả thẳng issue báo lỗi dữ liệu.
                }
                return (Row: row, Payload: payload);
            }).ToList();

            var brokenRows = payloads.Where(p => p.Payload is null).ToList();
            var validRows = payloads.Where(p => p.Payload is not null).ToList();

            var results = brokenRows.Select(p => BrokenResult(p.Row)).ToList();

            if (validRows.Count == 0)
                return results;

            var userPrompt = BuildKanjiAccuracyCheckPrompt(validRows.Select(p => (p.Row.Id, p.Payload!)).ToList());

            string? lastError = null;
            Dictionary<int, (bool IsAccurate, List<string> Issues)>? parsedResults = null;

            for (var attempt = 1; attempt <= MaxAiAttempts; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var currentPrompt = lastError is null
                    ? userPrompt
                    : $"{userPrompt}\n\n[LƯU Ý] Lần trả lời trước bị từ chối vì: {lastError}. Hãy sửa lại, trả đủ tất cả id.";

                string rawJson;
                try
                {
                    rawJson = StripCodeFence(await aiClient.GenerateContentJsonAsync(
                        userId, KanjiAccuracyCheckSystemPrompt, currentPrompt, cancellationToken));
                }
                catch (AiQuotaExceededException)
                {
                    lastError = "hết hạn mức gọi AI";
                    break;
                }

                var expectedIds = validRows.Select(p => p.Row.Id).ToHashSet();
                var parsed = TryParseAccuracyResults(rawJson, expectedIds, out lastError);
                if (parsed is not null)
                {
                    parsedResults = parsed;
                    break;
                }

                logger.LogWarning("AI kanji accuracy check rejected (attempt {Attempt}/{Max}): {Reason}. UserId={UserId}",
                    attempt, MaxAiAttempts, lastError, userId);
            }

            if (parsedResults is null)
            {
                // AI không trả được kết quả hợp lệ sau các lần thử — báo rõ cho author thay vì im lặng bỏ qua.
                results.AddRange(validRows.Select(p => CouldNotCheckResult(p.Row, lastError)));
                return results;
            }

            foreach (var (row, _) in validRows)
            {
                var (isAccurate, issues) = parsedResults.TryGetValue(row.Id, out var found)
                    ? found
                    : (false, new List<string> { "AI không trả kết quả cho mục này, vui lòng thử lại." });

                results.Add(new ContentAccuracyResult
                {
                    ApprovalRequestId = row.Id,
                    EntityType = row.EntityType.ToString(),
                    RowKey = row.RowKey,
                    IsSupported = true,
                    IsAccurate = isAccurate,
                    Issues = issues,
                });
            }

            return results;
        }

        /// <summary>Kiểm tra độ chính xác cho các loại "bảng phẳng" (Vocabulary, Grammar): gửi nguyên PayloadJson cho AI chấm.</summary>
        private async Task<List<ContentAccuracyResult>> ScanTextAccuracyAsync(
            int userId, List<ContentApprovalRequest> rows, string systemPrompt, CancellationToken cancellationToken)
        {
            var results = new List<ContentAccuracyResult>();
            var validRows = new List<ContentApprovalRequest>();

            foreach (var row in rows)
            {
                if (IsValidJson(row.PayloadJson)) validRows.Add(row);
                else results.Add(BrokenResult(row));
            }

            if (validRows.Count == 0)
                return results;

            var sb = new StringBuilder();
            sb.AppendLine("[DANH SÁCH CẦN KIỂM TRA ĐỘ CHÍNH XÁC — KHÔNG đề xuất thêm, chỉ chấm đúng/sai]");
            foreach (var row in validRows)
                sb.AppendLine($"- id={row.Id} | data={row.PayloadJson}");

            var expectedIds = validRows.Select(r => r.Id).ToHashSet();

            var run = await RunAiWithRetryAsync<Dictionary<int, (bool IsAccurate, List<string> Issues)>>(
                userId, systemPrompt, sb.ToString(),
                raw =>
                {
                    var parsed = TryParseAccuracyResults(raw, expectedIds, out var err);
                    return (parsed, err);
                },
                "accuracy check", cancellationToken);

            if (run.Value is null)
            {
                var reason = run.QuotaMessage is not null ? "hết hạn mức gọi AI" : run.Error;
                results.AddRange(validRows.Select(r => CouldNotCheckResult(r, reason)));
                return results;
            }

            foreach (var row in validRows)
            {
                var (isAccurate, issues) = run.Value.TryGetValue(row.Id, out var found)
                    ? found
                    : (false, new List<string> { "AI không trả kết quả cho mục này, vui lòng thử lại." });

                results.Add(new ContentAccuracyResult
                {
                    ApprovalRequestId = row.Id,
                    EntityType = row.EntityType.ToString(),
                    RowKey = row.RowKey,
                    IsSupported = true,
                    IsAccurate = isAccurate,
                    Issues = issues,
                });
            }

            return results;
        }

        /// <summary>
        /// Kiểm tra PracticeExercise / MockTest: (1) kiểm tra cấu trúc bằng code (đúng 1 đáp án đúng, đủ 4 đáp án...),
        /// (2) cho AI tự giải lại đề KHÔNG thấy đáp án, rồi so với đáp án đang đánh dấu. Lệch => gắn cờ để author xem lại.
        /// Lưu ý: AI giải lệch KHÔNG chứng minh đáp án sai, chỉ là dấu hiệu cần người kiểm tra.
        /// </summary>
        private async Task<List<ContentAccuracyResult>> ScanChoiceQuestionsAsync(
            int userId, List<ContentApprovalRequest> rows, bool isMockTest, CancellationToken cancellationToken)
        {
            var results = new List<ContentAccuracyResult>();

            foreach (var row in rows)
            {
                cancellationToken.ThrowIfCancellationRequested();

                List<ChoiceQuestionDraft>? questions;
                try
                {
                    questions = isMockTest
                        ? JsonSerializer.Deserialize<ConfirmMockTestRequest>(row.PayloadJson, JsonOpts)?
                            .Sections?.SelectMany(s => s.Questions ?? new List<ChoiceQuestionDraft>()).ToList()
                        : JsonSerializer.Deserialize<ConfirmPracticeExerciseRequest>(row.PayloadJson, JsonOpts)?.Questions;
                }
                catch (JsonException)
                {
                    questions = null;
                }

                if (questions is null || questions.Count == 0)
                {
                    results.Add(BrokenResult(row));
                    continue;
                }

                var issues = CollectChoiceQuestionIssues(questions, requireAudioScript: false);

                var blind = questions.Select((q, i) => new BlindQuestion(
                    i + 1, q.QuestionText, q.AudioScript, (q.Options ?? new()).Select(o => o.Text).ToList())).ToList();

                var (map, error) = await SolveBlindAsync(userId, blind, cancellationToken);
                if (map is null)
                {
                    issues.Add($"Không đối chiếu đáp án bằng AI được lúc này ({error}). Phần kiểm tra cấu trúc ở trên vẫn có hiệu lực.");
                }
                else
                {
                    for (var i = 0; i < questions.Count; i++)
                    {
                        var n = i + 1;
                        var q = questions[i];
                        var options = q.Options ?? new();
                        var marked = options.FindIndex(o => o.IsCorrect);
                        if (options.Count(o => o.IsCorrect) != 1) continue; // đã báo ở phần kiểm tra cấu trúc

                        if (!map.TryGetValue(n, out var ai)) continue;

                        if (ai.Ambiguous)
                            issues.Add($"Câu {n} ({Truncate(q.QuestionText, 30)}): AI thấy câu hỏi mơ hồ, có thể có hơn một đáp án hợp lý hoặc không có đáp án đúng.");

                        if (ai.Choice != marked)
                            issues.Add($"Câu {n} ({Truncate(q.QuestionText, 30)}): AI chọn đáp án {LetterOf(ai.Choice)} nhưng đáp án đang đánh dấu đúng là {LetterOf(marked)}. Cần kiểm tra lại đáp án hoặc câu hỏi.");
                    }
                }

                results.Add(new ContentAccuracyResult
                {
                    ApprovalRequestId = row.Id,
                    EntityType = row.EntityType.ToString(),
                    RowKey = row.RowKey,
                    IsSupported = true,
                    IsAccurate = issues.Count == 0,
                    Issues = issues,
                });
            }

            return results;
        }

        private static string BuildKanjiAccuracyCheckPrompt(List<(int Id, KanjiPayloadForAccuracyCheck Payload)> items)
        {
            var sb = new StringBuilder();
            sb.AppendLine("[DANH SÁCH KANJI CẦN KIỂM TRA ĐỘ CHÍNH XÁC — KHÔNG đề xuất thêm, chỉ chấm đúng/sai]");
            foreach (var (id, payload) in items)
            {
                sb.AppendLine($"- id={id} | character={payload.Character} | meaning={payload.Meaning} | " +
                               $"sinoVietnamese={payload.SinoVietnamese} | onYomi={payload.OnYomi} | " +
                               $"kunYomi={payload.KunYomi} | strokeCount={payload.StrokeCount}");
            }
            return sb.ToString();
        }

        private sealed record KanjiPayloadForAccuracyCheck(
            string Character, string Meaning, string? SinoVietnamese,
            string? OnYomi, string? KunYomi, int StrokeCount, int? LessonId);

        private sealed record AccuracyAiItem(int Id, bool IsAccurate, List<string>? Issues);
        private sealed record AccuracyAiResponse(List<AccuracyAiItem>? Results);

        private static Dictionary<int, (bool IsAccurate, List<string> Issues)>? TryParseAccuracyResults(
            string rawJson, HashSet<int> expectedIds, out string? error)
        {
            try
            {
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var parsed = JsonSerializer.Deserialize<AccuracyAiResponse>(rawJson, options);
                if (parsed?.Results is null || parsed.Results.Count == 0)
                {
                    error = "không có kết quả kiểm tra nào trong JSON trả về";
                    return null;
                }

                var map = parsed.Results.ToDictionary(
                    r => r.Id,
                    r => (r.IsAccurate, r.Issues ?? new List<string>()));

                var missing = expectedIds.Except(map.Keys).ToList();
                if (missing.Count > 0)
                {
                    error = $"thiếu kết quả cho {missing.Count} mục (id: {string.Join(",", missing.Take(5))})";
                    return null;
                }

                error = null;
                return map;
            }
            catch (JsonException)
            {
                error = "JSON sai định dạng (có thể bị ngắt giữa chừng)";
                return null;
            }
        }

        // =====================================================================================
        // KANJI (giữ nguyên)
        // =====================================================================================

        public async Task<ApiResponse<SuggestKanjiResponse>> SuggestKanjiAsync(
            SuggestKanjiRequest request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.Prompt))
                return ApiResponse<SuggestKanjiResponse>.Fail("Vui lòng nhập yêu cầu cho AI.");

            var maxItems = Math.Clamp(request.MaxItems <= 0 ? 10 : request.MaxItems, 1, MaxItemsAllowed);

            try
            {
                var userId = claimService.GetUserClaim().Id;

                // Lấy bài học (nếu author chọn) + cấp độ hiệu lực: cấp độ luôn lấy theo bài học đã chọn.
                var (ctx, ctxError, ctxStatus) = await ResolveContentContextAsync(
                    request.LessonId, request.CertificateLevelId, includeLinkedContent: true, cancellationToken);
                if (ctx is null)
                    return ContextError<SuggestKanjiResponse>(ctxError, ctxStatus);

                var existingCharacters = await unitOfWork.Kanjis.Query()
                    .Where(k => !k.IsDeleted)
                    .Select(k => new { k.Id, k.Character })
                    .ToListAsync(cancellationToken);
                var existingByCharacter = existingCharacters
                    .GroupBy(k => k.Character)
                    .ToDictionary(g => g.Key, g => g.First().Id);

                var userPrompt = BuildKanjiUserPrompt(request.Prompt, maxItems, ctx, existingCharacters.Select(k => k.Character).ToList());

                List<KanjiSuggestionItem>? items = null;
                string? lastError = null;

                for (var attempt = 1; attempt <= MaxAiAttempts; attempt++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var currentPrompt = lastError is null
                        ? userPrompt
                        : $"{userPrompt}\n\n[LƯU Ý] Lần trả lời trước bị từ chối vì: {lastError}. Hãy sửa lại.";

                    string rawJson;
                    try
                    {
                        rawJson = StripCodeFence(await aiClient.GenerateContentJsonAsync(
                            userId, KanjiSystemPrompt, currentPrompt, cancellationToken));
                    }
                    catch (AiQuotaExceededException ex)
                    {
                        return ApiResponse<SuggestKanjiResponse>.Fail(
                            ex.Message, HttpStatusCode.TooManyRequests);
                    }

                    var parsed = TryParseKanjiItems(rawJson, out lastError);
                    if (parsed is null)
                    {
                        logger.LogWarning("AI kanji suggestion rejected (attempt {Attempt}/{Max}): {Reason}. UserId={UserId}",
                            attempt, MaxAiAttempts, lastError, userId);
                        continue;
                    }

                    lastError = ValidateKanjiItems(parsed, maxItems);
                    if (lastError is null)
                    {
                        items = parsed;
                        break;
                    }

                    logger.LogWarning("AI kanji suggestion rejected (attempt {Attempt}/{Max}): {Reason}. UserId={UserId}",
                        attempt, MaxAiAttempts, lastError, userId);
                }

                if (items is null)
                    return ApiResponse<SuggestKanjiResponse>.Fail(
                        $"AI trả về kết quả không hợp lệ ({lastError}). Vui lòng thử lại.", HttpStatusCode.BadGateway);

                // Dò trùng với Kanji đã có thật trong DB — quyết định cuối do content author xem ở bước preview.
                foreach (var item in items)
                {
                    if (existingByCharacter.TryGetValue(item.Character, out var existingId))
                    {
                        item.IsDuplicate = true;
                        item.ExistingKanjiId = existingId;
                    }
                }

                return ApiResponse<SuggestKanjiResponse>.Success(new SuggestKanjiResponse { Items = items });
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to suggest kanji content.");
                return ApiResponse<SuggestKanjiResponse>.Fail(
                    "Không thể tạo đề xuất Kanji lúc này, vui lòng thử lại.", HttpStatusCode.InternalServerError);
            }
        }

        public async Task<ApiResponse<SubmitKanjiApprovalResponse>> ConfirmKanjiSuggestionsAsync(
            ConfirmKanjiSuggestionsRequest request, CancellationToken cancellationToken)
        {
            if (request.Items is null || request.Items.Count == 0)
                return ApiResponse<SubmitKanjiApprovalResponse>.Fail("Danh sách Kanji trống, không có gì để gửi duyệt.");
            if (request.Items.Any(item => string.IsNullOrWhiteSpace(item.Character)))
                return ApiResponse<SubmitKanjiApprovalResponse>.Fail("Có Kanji thiếu ký tự (character).");

            try
            {
                var (ctx, ctxError, ctxStatus) = await ResolveContentContextAsync(
                    request.LessonId, request.CertificateLevelId, includeLinkedContent: false, cancellationToken);
                if (ctx is null)
                    return ContextError<SubmitKanjiApprovalResponse>(ctxError, ctxStatus);

                var userId = claimService.GetUserClaim().Id;
                var batchId = Guid.NewGuid();
                var now = DateTime.UtcNow;

                var rows = new List<ContentApprovalRequest>();
                var sortOrder = 1;
                foreach (var item in request.Items)
                {
                    var payload = new
                    {
                        character = item.Character,
                        meaning = item.Meaning,
                        sinoVietnamese = item.SinoVietnamese,
                        onYomi = item.OnYomi,
                        kunYomi = item.KunYomi,
                        strokeCount = item.StrokeCount,
                        lessonId = request.LessonId,
                        certificateLevelId = ctx.Level.Id,
                    };

                    rows.Add(new ContentApprovalRequest
                    {
                        BatchId = batchId,
                        // TODO: xác nhận lại đúng tên giá trị enum ImportEntityType cho Kanji nếu khác.
                        EntityType = ImportEntityType.Kanji,
                        // TODO: xác nhận lại đúng tên giá trị enum ImportRowAction (New/Modified).
                        Action = item.IsDuplicate ? ImportRowAction.Modified : ImportRowAction.New,
                        TargetEntityId = item.ExistingKanjiId,
                        RowKey = item.Character,
                        PayloadJson = JsonSerializer.Serialize(payload),
                        SourceRowNumber = sortOrder++,
                        SourceFileName = "AI Content Assistant",
                        // TODO: xác nhận lại đúng tên giá trị enum ContentApprovalStatus mặc định
                        // khi content author vừa gửi (Draft hay PendingReview...).
                        Status = ContentApprovalStatus.Draft,
                        SubmittedByUserId = userId,
                        SubmittedAt = now,
                    });
                }

                // TODO: nếu IContentApprovalRequestRepository không có AddRangeAsync, đổi thành
                // foreach (var row in rows) await unitOfWork.ContentApprovalRequests.AddAsync(row, cancellationToken);
                await unitOfWork.ContentApprovalRequests.AddRangeAsync(rows, cancellationToken);
                await unitOfWork.SaveChangesAsync(cancellationToken);

                return ApiResponse<SubmitKanjiApprovalResponse>.Success(new SubmitKanjiApprovalResponse
                {
                    BatchId = batchId,
                    SubmittedCount = rows.Count,
                });
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to submit kanji suggestions for approval.");
                return ApiResponse<SubmitKanjiApprovalResponse>.Fail(
                    "Không thể gửi duyệt lúc này, vui lòng thử lại.", HttpStatusCode.InternalServerError);
            }
        }

        // =====================================================================================
        // VOCABULARY
        // =====================================================================================

        public async Task<ApiResponse<SuggestVocabularyResponse>> SuggestVocabularyAsync(
            SuggestVocabularyRequest request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.Prompt))
                return ApiResponse<SuggestVocabularyResponse>.Fail("Vui lòng nhập yêu cầu cho AI.");

            var maxItems = Math.Clamp(request.MaxItems <= 0 ? 10 : request.MaxItems, 1, MaxItemsAllowed);

            try
            {
                var userId = claimService.GetUserClaim().Id;

                // Lấy bài học (nếu author chọn) + cấp độ hiệu lực: cấp độ luôn lấy theo bài học đã chọn.
                var (ctx, ctxError, ctxStatus) = await ResolveContentContextAsync(
                    request.LessonId, request.CertificateLevelId, includeLinkedContent: true, cancellationToken);
                if (ctx is null)
                    return ContextError<SuggestVocabularyResponse>(ctxError, ctxStatus);

                // TODO: xác nhận tên repo (unitOfWork.Vocabularies).
                var existing = await unitOfWork.Vocabularies.Query()
                    .Where(v => !v.IsDeleted)
                    .Select(v => new { v.Id, v.Word, v.Reading })
                    .ToListAsync(cancellationToken);

                var existingByKey = existing
                    .GroupBy(v => $"{v.Word}|{v.Reading}")
                    .ToDictionary(g => g.Key, g => g.First().Id);

                var userPrompt = BuildListUserPrompt(
                    "từ vựng", request.Prompt, maxItems, ctx,
                    existing.Select(v => v.Word).Distinct().Take(300).ToList(),
                    ctx.Lesson?.LessonVocabularies?.Where(x => x.Vocabulary is not null).Select(x => x.Vocabulary.Word));

                var run = await RunAiWithRetryAsync<List<VocabularySuggestionItem>>(
                    userId, VocabularySystemPrompt, userPrompt,
                    raw => TryParseVocabularyItems(raw, maxItems),
                    "vocabulary suggestion", cancellationToken);

                if (run.QuotaMessage is not null)
                    return ApiResponse<SuggestVocabularyResponse>.Fail(run.QuotaMessage, HttpStatusCode.TooManyRequests);
                if (run.Value is null)
                    return ApiResponse<SuggestVocabularyResponse>.Fail(
                        $"AI trả về kết quả không hợp lệ ({run.Error}). Vui lòng thử lại.", HttpStatusCode.BadGateway);

                foreach (var item in run.Value)
                {
                    if (existingByKey.TryGetValue($"{item.Word}|{item.Reading}", out var existingId))
                    {
                        item.IsDuplicate = true;
                        item.ExistingVocabularyId = existingId;
                    }
                }

                return ApiResponse<SuggestVocabularyResponse>.Success(new SuggestVocabularyResponse { Items = run.Value });
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to suggest vocabulary content.");
                return ApiResponse<SuggestVocabularyResponse>.Fail(
                    "Không thể tạo đề xuất từ vựng lúc này, vui lòng thử lại.", HttpStatusCode.InternalServerError);
            }
        }

        public async Task<ApiResponse<SubmitContentApprovalResponse>> ConfirmVocabularySuggestionsAsync(
            ConfirmVocabularySuggestionsRequest request, CancellationToken cancellationToken)
        {
            if (request.Items is null || request.Items.Count == 0)
                return ApiResponse<SubmitContentApprovalResponse>.Fail("Danh sách từ vựng trống, không có gì để gửi duyệt.");
            if (request.Items.Any(i => string.IsNullOrWhiteSpace(i.Word) ||
                                       string.IsNullOrWhiteSpace(i.Reading) ||
                                       string.IsNullOrWhiteSpace(i.Meaning)))
                return ApiResponse<SubmitContentApprovalResponse>.Fail("Có từ vựng thiếu từ, cách đọc hoặc nghĩa.");

            (ContentContext? Context, string? Error, HttpStatusCode Status) resolved;
            try
            {
                resolved = await ResolveContentContextAsync(
                    request.LessonId, request.CertificateLevelId, includeLinkedContent: false, cancellationToken);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to resolve content context for vocabulary confirm.");
                return ApiResponse<SubmitContentApprovalResponse>.Fail(
                    "Không thể gửi duyệt lúc này, vui lòng thử lại.", HttpStatusCode.InternalServerError);
            }
            if (resolved.Context is null)
                return ContextError<SubmitContentApprovalResponse>(resolved.Error, resolved.Status);
            var levelId = resolved.Context.Level.Id;

            var drafts = request.Items.Select(i => new ApprovalDraft(
                RowKey: i.Word,
                Payload: new
                {
                    word = i.Word,
                    reading = i.Reading,
                    meaning = i.Meaning,
                    exampleSentence = i.ExampleSentence,
                    exampleSentenceMeaning = i.ExampleSentenceMeaning,
                    lessonId = request.LessonId,
                    certificateLevelId = levelId,
                },
                IsExisting: i.IsDuplicate,
                TargetId: i.ExistingVocabularyId)).ToList();

            return await SubmitForApprovalAsync(ImportEntityType.Vocabulary, drafts, cancellationToken);
        }

        private static string BuildListUserPrompt(
            string label, string authorPrompt, int maxItems, ContentContext ctx,
            List<string> existingKeys, IEnumerable<string>? alreadyInLesson)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"[YÊU CẦU CỦA CONTENT AUTHOR]: {authorPrompt.Trim()}");
            sb.AppendLine($"- Đề xuất tối đa {maxItems} {label}.");
            AppendContentContext(sb, ctx, label, alreadyInLesson);
            if (existingKeys.Count > 0)
            {
                sb.AppendLine($"\n[LƯU Ý]: Các {label} sau ĐÃ CÓ trong hệ thống, tránh đề xuất lại trừ khi content author");
                sb.AppendLine("  yêu cầu rõ là muốn cập nhật/bổ sung thông tin cho mục đã có:");
                sb.AppendLine("  " + string.Join(", ", existingKeys));
            }
            return sb.ToString();
        }

        private sealed record VocabularyAiRawItem(
            string? Word, string? Reading, string? Meaning,
            string? ExampleSentence, string? ExampleSentenceMeaning, string? Rationale);

        private sealed record VocabularyAiRawResponse(List<VocabularyAiRawItem>? Items);

        private static (List<VocabularySuggestionItem>? Value, string? Error) TryParseVocabularyItems(string rawJson, int maxItems)
        {
            VocabularyAiRawResponse? parsed;
            try
            {
                parsed = JsonSerializer.Deserialize<VocabularyAiRawResponse>(rawJson, JsonOpts);
            }
            catch (JsonException)
            {
                return (null, "JSON sai định dạng (có thể bị ngắt giữa chừng)");
            }

            if (parsed?.Items is null || parsed.Items.Count == 0)
                return (null, "không có từ vựng nào trong JSON trả về");

            var items = parsed.Items
                .Where(i => !string.IsNullOrWhiteSpace(i.Word))
                .Select(i => new VocabularySuggestionItem
                {
                    Word = i.Word!.Trim(),
                    Reading = (i.Reading ?? string.Empty).Trim(),
                    Meaning = (i.Meaning ?? string.Empty).Trim(),
                    ExampleSentence = i.ExampleSentence?.Trim(),
                    ExampleSentenceMeaning = i.ExampleSentenceMeaning?.Trim(),
                    Rationale = i.Rationale?.Trim(),
                })
                .ToList();

            if (items.Count == 0)
                return (null, "danh sách rỗng");
            if (items.Count > maxItems + 3)
                return (null, $"trả về quá nhiều từ vựng ({items.Count}) so với yêu cầu ({maxItems})");
            if (items.Any(i => string.IsNullOrWhiteSpace(i.Reading) || !IsKanaOnly(i.Reading)))
                return (null, "có từ vựng thiếu cách đọc hoặc cách đọc không phải hiragana/katakana");
            if (items.Any(i => string.IsNullOrWhiteSpace(i.Meaning)))
                return (null, "có từ vựng thiếu nghĩa");
            if (items.Any(i => LooksEnglish(i.Meaning) || LooksEnglish(i.ExampleSentenceMeaning) || LooksEnglish(i.Rationale)))
                return (null, "phần nghĩa/lý do lẫn tiếng Anh, phải viết hoàn toàn bằng tiếng Việt");
            if (items.Select(i => $"{i.Word}|{i.Reading}").Distinct().Count() != items.Count)
                return (null, "có từ vựng bị trùng lặp trong cùng một lần đề xuất");

            return (items, null);
        }

        // =====================================================================================
        // GRAMMAR
        // =====================================================================================

        public async Task<ApiResponse<SuggestGrammarResponse>> SuggestGrammarAsync(
            SuggestGrammarRequest request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.Prompt))
                return ApiResponse<SuggestGrammarResponse>.Fail("Vui lòng nhập yêu cầu cho AI.");

            var maxItems = Math.Clamp(request.MaxItems <= 0 ? 10 : request.MaxItems, 1, MaxItemsAllowed);

            try
            {
                var userId = claimService.GetUserClaim().Id;

                // Lấy bài học (nếu author chọn) + cấp độ hiệu lực: cấp độ luôn lấy theo bài học đã chọn.
                var (ctx, ctxError, ctxStatus) = await ResolveContentContextAsync(
                    request.LessonId, request.CertificateLevelId, includeLinkedContent: true, cancellationToken);
                if (ctx is null)
                    return ContextError<SuggestGrammarResponse>(ctxError, ctxStatus);

                // TODO: xác nhận tên repo (unitOfWork.GrammarPatterns).
                var existing = await unitOfWork.GrammarPatterns.Query()
                    .Where(g => !g.IsDeleted)
                    .Select(g => new { g.Id, g.Title, g.Structure })
                    .ToListAsync(cancellationToken);

                var existingByStructure = existing
                    .GroupBy(g => NormalizeKey(g.Structure))
                    .ToDictionary(g => g.Key, g => g.First().Id);
                var existingByTitle = existing
                    .GroupBy(g => NormalizeKey(g.Title))
                    .ToDictionary(g => g.Key, g => g.First().Id);

                var userPrompt = BuildListUserPrompt(
                    "mẫu ngữ pháp", request.Prompt, maxItems, ctx,
                    existing.Select(g => g.Title).Distinct().Take(200).ToList(),
                    ctx.Lesson?.LessonGrammars?.Where(x => x.GrammarPattern is not null).Select(x => x.GrammarPattern.Title));

                var run = await RunAiWithRetryAsync<List<GrammarSuggestionItem>>(
                    userId, GrammarSystemPrompt, userPrompt,
                    raw => TryParseGrammarItems(raw, maxItems),
                    "grammar suggestion", cancellationToken);

                if (run.QuotaMessage is not null)
                    return ApiResponse<SuggestGrammarResponse>.Fail(run.QuotaMessage, HttpStatusCode.TooManyRequests);
                if (run.Value is null)
                    return ApiResponse<SuggestGrammarResponse>.Fail(
                        $"AI trả về kết quả không hợp lệ ({run.Error}). Vui lòng thử lại.", HttpStatusCode.BadGateway);

                foreach (var item in run.Value)
                {
                    if (existingByStructure.TryGetValue(NormalizeKey(item.Structure), out var idByStructure))
                    {
                        item.IsDuplicate = true;
                        item.ExistingGrammarPatternId = idByStructure;
                    }
                    else if (existingByTitle.TryGetValue(NormalizeKey(item.Title), out var idByTitle))
                    {
                        item.IsDuplicate = true;
                        item.ExistingGrammarPatternId = idByTitle;
                    }
                }

                return ApiResponse<SuggestGrammarResponse>.Success(new SuggestGrammarResponse { Items = run.Value });
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to suggest grammar content.");
                return ApiResponse<SuggestGrammarResponse>.Fail(
                    "Không thể tạo đề xuất ngữ pháp lúc này, vui lòng thử lại.", HttpStatusCode.InternalServerError);
            }
        }

        public async Task<ApiResponse<SubmitContentApprovalResponse>> ConfirmGrammarSuggestionsAsync(
            ConfirmGrammarSuggestionsRequest request, CancellationToken cancellationToken)
        {
            if (request.Items is null || request.Items.Count == 0)
                return ApiResponse<SubmitContentApprovalResponse>.Fail("Danh sách ngữ pháp trống, không có gì để gửi duyệt.");
            if (request.Items.Any(i => string.IsNullOrWhiteSpace(i.Title) || string.IsNullOrWhiteSpace(i.Structure)))
                return ApiResponse<SubmitContentApprovalResponse>.Fail("Có mẫu ngữ pháp thiếu tiêu đề hoặc cấu trúc.");

            (ContentContext? Context, string? Error, HttpStatusCode Status) resolved;
            try
            {
                resolved = await ResolveContentContextAsync(
                    request.LessonId, request.CertificateLevelId, includeLinkedContent: false, cancellationToken);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to resolve content context for grammar confirm.");
                return ApiResponse<SubmitContentApprovalResponse>.Fail(
                    "Không thể gửi duyệt lúc này, vui lòng thử lại.", HttpStatusCode.InternalServerError);
            }
            if (resolved.Context is null)
                return ContextError<SubmitContentApprovalResponse>(resolved.Error, resolved.Status);
            var levelId = resolved.Context.Level.Id;

            var drafts = request.Items.Select(i => new ApprovalDraft(
                RowKey: i.Title,
                Payload: new
                {
                    title = i.Title,
                    structure = i.Structure,
                    usageNotes = i.UsageNotes,
                    exampleSentence = i.ExampleSentence,
                    exampleSentenceMeaning = i.ExampleSentenceMeaning,
                    lessonId = request.LessonId,
                    certificateLevelId = levelId,
                },
                IsExisting: i.IsDuplicate,
                TargetId: i.ExistingGrammarPatternId)).ToList();

            if (!GrammarEntityType.HasValue)
            {
                logger.LogError("ImportEntityType chưa có giá trị cho ngữ pháp (GrammarPattern/Grammar).");
                return ApiResponse<SubmitContentApprovalResponse>.Fail(
                    "Hệ thống chưa cấu hình loại nội dung ngữ pháp, vui lòng liên hệ quản trị viên.",
                    HttpStatusCode.InternalServerError);
            }

            return await SubmitForApprovalAsync(GrammarEntityType.Value, drafts, cancellationToken);
        }

        private sealed record GrammarAiRawItem(
            string? Title, string? Structure, string? UsageNotes,
            string? ExampleSentence, string? ExampleSentenceMeaning, string? Rationale);

        private sealed record GrammarAiRawResponse(List<GrammarAiRawItem>? Items);

        private static (List<GrammarSuggestionItem>? Value, string? Error) TryParseGrammarItems(string rawJson, int maxItems)
        {
            GrammarAiRawResponse? parsed;
            try
            {
                parsed = JsonSerializer.Deserialize<GrammarAiRawResponse>(rawJson, JsonOpts);
            }
            catch (JsonException)
            {
                return (null, "JSON sai định dạng (có thể bị ngắt giữa chừng)");
            }

            if (parsed?.Items is null || parsed.Items.Count == 0)
                return (null, "không có mẫu ngữ pháp nào trong JSON trả về");

            var items = parsed.Items
                .Where(i => !string.IsNullOrWhiteSpace(i.Title))
                .Select(i => new GrammarSuggestionItem
                {
                    Title = i.Title!.Trim(),
                    Structure = (i.Structure ?? string.Empty).Trim(),
                    UsageNotes = i.UsageNotes?.Trim(),
                    ExampleSentence = i.ExampleSentence?.Trim(),
                    ExampleSentenceMeaning = i.ExampleSentenceMeaning?.Trim(),
                    Rationale = i.Rationale?.Trim(),
                })
                .ToList();

            if (items.Count == 0)
                return (null, "danh sách rỗng");
            if (items.Count > maxItems + 3)
                return (null, $"trả về quá nhiều mẫu ngữ pháp ({items.Count}) so với yêu cầu ({maxItems})");
            if (items.Any(i => string.IsNullOrWhiteSpace(i.Structure)))
                return (null, "có mẫu ngữ pháp thiếu cấu trúc");
            if (items.Any(i => LooksEnglish(i.UsageNotes) || LooksEnglish(i.ExampleSentenceMeaning) || LooksEnglish(i.Rationale)))
                return (null, "phần giải thích/lý do lẫn tiếng Anh, phải viết hoàn toàn bằng tiếng Việt");
            if (items.Select(i => NormalizeKey(i.Structure)).Distinct().Count() != items.Count)
                return (null, "có mẫu ngữ pháp bị trùng lặp trong cùng một lần đề xuất");

            return (items, null);
        }

        // =====================================================================================
        // LESSON  (metadata + liên kết tới nội dung ĐÃ CÓ; video do author tự upload)
        // =====================================================================================

        public async Task<ApiResponse<SuggestLessonResponse>> SuggestLessonAsync(
            SuggestLessonRequest request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.Prompt))
                return ApiResponse<SuggestLessonResponse>.Fail("Vui lòng nhập yêu cầu cho AI.");

            var maxPerType = Math.Clamp(request.MaxItemsPerType <= 0 ? 8 : request.MaxItemsPerType, 1, MaxItemsAllowed);

            try
            {
                var userId = claimService.GetUserClaim().Id;

                // Loại chứng chỉ -> cấp độ -> kỹ năng. Lesson chỉ lưu CertificateLevelId + LanguageSkillId;
                // loại chứng chỉ suy ra từ cấp độ (CertificateLevel.CertificateType) và chỉ dùng để kiểm tra + làm rõ prompt.
                var (level, levelError, levelStatus) = await LoadLevelAsync(
                    request.CertificateLevelId, request.CertificateTypeId, cancellationToken);
                if (level is null)
                    return ContextError<SuggestLessonResponse>(levelError, levelStatus);
                var skill = await unitOfWork.LanguageSkills.GetByIdAsync(request.LanguageSkillId, cancellationToken);
                if (skill is null || skill.IsDeleted)
                    return ApiResponse<SuggestLessonResponse>.NotFound("Không tìm thấy kỹ năng.");

                var levelId = level.Id;

                var existingLessons = await unitOfWork.Lessons.Query()
                    .Where(l => !l.IsDeleted && l.CertificateLevelId == levelId && l.LanguageSkillId == skill.Id)
                    .Select(l => new { l.Id, l.Title, l.SortOrder })
                    .ToListAsync(cancellationToken);

                // Toàn bộ nội dung đang có (để resolve) + đánh dấu cái nào thuộc cấp độ này (để làm danh sách ứng viên cho AI).
                var allKanji = await unitOfWork.Kanjis.Query()
                    .Where(k => !k.IsDeleted)
                    .Select(k => new
                    {
                        k.Id,
                        k.Character,
                        k.Meaning,
                        InLevel = k.ContentLevelMappings!.Any(m => !m.IsDeleted && m.CertificateLevelId == levelId),
                    })
                    .ToListAsync(cancellationToken);

                var allVocabulary = await unitOfWork.Vocabularies.Query()
                    .Where(v => !v.IsDeleted)
                    .Select(v => new
                    {
                        v.Id,
                        v.Word,
                        v.Reading,
                        v.Meaning,
                        InLevel = v.ContentLevelMappings!.Any(m => !m.IsDeleted && m.CertificateLevelId == levelId),
                    })
                    .ToListAsync(cancellationToken);

                var allGrammar = await unitOfWork.GrammarPatterns.Query()
                    .Where(g => !g.IsDeleted)
                    .Select(g => new
                    {
                        g.Id,
                        g.Title,
                        g.Structure,
                        InLevel = g.ContentLevelMappings!.Any(m => !m.IsDeleted && m.CertificateLevelId == levelId),
                    })
                    .ToListAsync(cancellationToken);

                var sb = new StringBuilder();
                sb.AppendLine($"[YÊU CẦU CỦA CONTENT AUTHOR]: {request.Prompt.Trim()}");
                sb.AppendLine($"- Loại chứng chỉ: {level.CertificateType.Name}");
                sb.AppendLine($"- Cấp độ: {level.Code}{(string.IsNullOrWhiteSpace(level.Description) ? "" : $" ({level.Description})")}");
                sb.AppendLine($"- Kỹ năng: {skill.Name}");
                sb.AppendLine($"- Chọn tối đa {maxPerType} mục cho MỖI loại (kanji / vocabulary / grammar).");

                if (existingLessons.Count > 0)
                {
                    sb.AppendLine("\n[CÁC BÀI HỌC ĐÃ CÓ — không đặt tiêu đề trùng]:");
                    sb.AppendLine(string.Join("; ", existingLessons.Select(l => l.Title).Take(100)));
                }

                sb.AppendLine("\n[ỨNG VIÊN KANJI — chỉ được chọn trong danh sách này]:");
                sb.AppendLine(string.Join(", ", allKanji.Where(k => k.InLevel).Take(MaxPromptCandidates).Select(k => $"{k.Character}({k.Meaning})")));
                sb.AppendLine("\n[ỨNG VIÊN TỪ VỰNG — chỉ được chọn trong danh sách này]:");
                sb.AppendLine(string.Join(", ", allVocabulary.Where(v => v.InLevel).Take(MaxPromptCandidates).Select(v => $"{v.Word}[{v.Reading}]({v.Meaning})")));
                sb.AppendLine("\n[ỨNG VIÊN NGỮ PHÁP — chỉ được chọn trong danh sách này]:");
                sb.AppendLine(string.Join(", ", allGrammar.Where(g => g.InLevel).Take(MaxPromptCandidates).Select(g => g.Title)));

                var run = await RunAiWithRetryAsync<LessonAiParsed>(
                    userId, LessonSystemPrompt, sb.ToString(),
                    raw => TryParseLesson(raw, maxPerType),
                    "lesson suggestion", cancellationToken);

                if (run.QuotaMessage is not null)
                    return ApiResponse<SuggestLessonResponse>.Fail(run.QuotaMessage, HttpStatusCode.TooManyRequests);
                if (run.Value is null)
                    return ApiResponse<SuggestLessonResponse>.Fail(
                        $"AI trả về kết quả không hợp lệ ({run.Error}). Vui lòng thử lại.", HttpStatusCode.BadGateway);

                var ai = run.Value;

                // Resolve về Id thật trong DB bằng code — không tin Id/khóa do AI tự nêu.
                var kanjiByChar = allKanji.GroupBy(k => k.Character).ToDictionary(g => g.Key, g => g.First().Id);
                var vocabByKey = allVocabulary.GroupBy(v => $"{v.Word}|{v.Reading}").ToDictionary(g => g.Key, g => g.First().Id);
                var vocabByWord = allVocabulary.GroupBy(v => v.Word).ToDictionary(g => g.Key, g => g.Select(x => x.Id).ToList());
                var grammarByTitle = allGrammar.GroupBy(g => NormalizeKey(g.Title)).ToDictionary(g => g.Key, g => g.First().Id);

                var kanjiLinks = ai.Kanji.Select(c => new LessonLinkedItem
                {
                    Key = c,
                    ExistingId = kanjiByChar.TryGetValue(c, out var id) ? id : null,
                }).ToList();

                var vocabLinks = ai.Vocabulary.Select(v =>
                {
                    int? id = null;
                    if (vocabByKey.TryGetValue($"{v.Word}|{v.Reading}", out var exact)) id = exact;
                    else if (vocabByWord.TryGetValue(v.Word, out var byWord) && byWord.Count == 1) id = byWord[0];
                    return new LessonLinkedItem { Key = v.Word, Reading = v.Reading, ExistingId = id };
                }).ToList();

                var grammarLinks = ai.Grammar.Select(t => new LessonLinkedItem
                {
                    Key = t,
                    ExistingId = grammarByTitle.TryGetValue(NormalizeKey(t), out var id) ? id : null,
                }).ToList();

                var dupLesson = existingLessons.FirstOrDefault(l => NormalizeKey(l.Title) == NormalizeKey(ai.Title));

                return ApiResponse<SuggestLessonResponse>.Success(new SuggestLessonResponse
                {
                    CertificateLevelId = level.Id,
                    LanguageSkillId = skill.Id,
                    Title = ai.Title,
                    Description = ai.Description,
                    Rationale = ai.Rationale,
                    SortOrder = existingLessons.Count == 0 ? 1 : existingLessons.Max(l => l.SortOrder) + 1,
                    IsDuplicateTitle = dupLesson is not null,
                    ExistingLessonId = dupLesson?.Id,
                    Kanji = kanjiLinks,
                    Vocabulary = vocabLinks,
                    Grammar = grammarLinks,
                });
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to suggest lesson content.");
                return ApiResponse<SuggestLessonResponse>.Fail(
                    "Không thể tạo đề xuất bài học lúc này, vui lòng thử lại.", HttpStatusCode.InternalServerError);
            }
        }

        public async Task<ApiResponse<SubmitContentApprovalResponse>> ConfirmLessonSuggestionAsync(
            ConfirmLessonSuggestionRequest request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.Title))
                return ApiResponse<SubmitContentApprovalResponse>.Fail("Bài học thiếu tiêu đề.");

            try
            {
                var (level, levelError, levelStatus) = await LoadLevelAsync(
                    request.CertificateLevelId, request.CertificateTypeId, cancellationToken);
                if (level is null)
                    return ContextError<SubmitContentApprovalResponse>(levelError, levelStatus);
                var skill = await unitOfWork.LanguageSkills.GetByIdAsync(request.LanguageSkillId, cancellationToken);
                if (skill is null || skill.IsDeleted)
                    return ApiResponse<SubmitContentApprovalResponse>.NotFound("Không tìm thấy kỹ năng.");

                var kanjiIds = (request.KanjiIds ?? new()).Distinct().ToList();
                var vocabularyIds = (request.VocabularyIds ?? new()).Distinct().ToList();
                var grammarIds = (request.GrammarPatternIds ?? new()).Distinct().ToList();

                if (kanjiIds.Count > 0 &&
                    await unitOfWork.Kanjis.Query().CountAsync(k => !k.IsDeleted && kanjiIds.Contains(k.Id), cancellationToken) != kanjiIds.Count)
                    return ApiResponse<SubmitContentApprovalResponse>.Fail("Có Kanji liên kết không tồn tại.");
                if (vocabularyIds.Count > 0 &&
                    await unitOfWork.Vocabularies.Query().CountAsync(v => !v.IsDeleted && vocabularyIds.Contains(v.Id), cancellationToken) != vocabularyIds.Count)
                    return ApiResponse<SubmitContentApprovalResponse>.Fail("Có từ vựng liên kết không tồn tại.");
                if (grammarIds.Count > 0 &&
                    await unitOfWork.GrammarPatterns.Query().CountAsync(g => !g.IsDeleted && grammarIds.Contains(g.Id), cancellationToken) != grammarIds.Count)
                    return ApiResponse<SubmitContentApprovalResponse>.Fail("Có mẫu ngữ pháp liên kết không tồn tại.");

                var payload = new
                {
                    title = request.Title.Trim(),
                    description = request.Description?.Trim(),
                    sortOrder = request.SortOrder,
                    certificateLevelId = request.CertificateLevelId,
                    languageSkillId = request.LanguageSkillId,
                    kanjiIds,
                    vocabularyIds,
                    grammarPatternIds = grammarIds,
                };

                return await SubmitForApprovalAsync(ImportEntityType.Lesson,
                    new List<ApprovalDraft> { new(request.Title.Trim(), payload, false, null) }, cancellationToken);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to submit lesson suggestion for approval.");
                return ApiResponse<SubmitContentApprovalResponse>.Fail(
                    "Không thể gửi duyệt lúc này, vui lòng thử lại.", HttpStatusCode.InternalServerError);
            }
        }

        private sealed record LessonAiRawVocab(string? Word, string? Reading);
        private sealed record LessonAiRaw(
            string? Title, string? Description, List<string>? Kanji,
            List<LessonAiRawVocab>? Vocabulary, List<string>? Grammar, string? Rationale);

        private sealed record LessonAiParsedVocab(string Word, string Reading);
        private sealed record LessonAiParsed(
            string Title, string? Description, List<string> Kanji,
            List<LessonAiParsedVocab> Vocabulary, List<string> Grammar, string? Rationale);

        private static (LessonAiParsed? Value, string? Error) TryParseLesson(string rawJson, int maxPerType)
        {
            LessonAiRaw? raw;
            try
            {
                raw = JsonSerializer.Deserialize<LessonAiRaw>(rawJson, JsonOpts);
            }
            catch (JsonException)
            {
                return (null, "JSON sai định dạng (có thể bị ngắt giữa chừng)");
            }

            if (raw is null || string.IsNullOrWhiteSpace(raw.Title))
                return (null, "thiếu tiêu đề bài học");

            var kanji = (raw.Kanji ?? new()).Select(c => c?.Trim() ?? string.Empty)
                .Where(c => c.Length > 0 && c.Length <= 2).Distinct().Take(maxPerType).ToList();
            var vocab = (raw.Vocabulary ?? new())
                .Where(v => !string.IsNullOrWhiteSpace(v.Word))
                .Select(v => new LessonAiParsedVocab(v.Word!.Trim(), (v.Reading ?? string.Empty).Trim()))
                .DistinctBy(v => $"{v.Word}|{v.Reading}").Take(maxPerType).ToList();
            var grammar = (raw.Grammar ?? new()).Select(g => g?.Trim() ?? string.Empty)
                .Where(g => g.Length > 0).Distinct().Take(maxPerType).ToList();

            if (kanji.Count + vocab.Count + grammar.Count == 0)
                return (null, "AI không chọn được nội dung liên kết nào");
            if (LooksEnglish(raw.Title) || LooksEnglish(raw.Description) || LooksEnglish(raw.Rationale))
                return (null, "tiêu đề/mô tả/lý do lẫn tiếng Anh, phải viết hoàn toàn bằng tiếng Việt");

            return (new LessonAiParsed(raw.Title.Trim(), raw.Description?.Trim(), kanji, vocab, grammar, raw.Rationale?.Trim()), null);
        }

        // =====================================================================================
        // PRACTICE EXERCISE (gắn với 1 Lesson)
        // =====================================================================================

        public async Task<ApiResponse<SuggestPracticeExerciseResponse>> SuggestPracticeExerciseAsync(
            SuggestPracticeExerciseRequest request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.Prompt))
                return ApiResponse<SuggestPracticeExerciseResponse>.Fail("Vui lòng nhập yêu cầu cho AI.");

            var count = Math.Clamp(request.QuestionCount <= 0 ? 5 : request.QuestionCount, 1, MaxPracticeQuestions);

            try
            {
                var userId = claimService.GetUserClaim().Id;

                var lesson = await unitOfWork.Lessons.Query()
                    .Include(l => l.CertificateLevel)
                    .Include(l => l.LessonKanjis!).ThenInclude(x => x.Kanji)
                    .Include(l => l.LessonVocabularies!).ThenInclude(x => x.Vocabulary)
                    .Include(l => l.LessonGrammars!).ThenInclude(x => x.GrammarPattern)
                    .FirstOrDefaultAsync(l => l.Id == request.LessonId && !l.IsDeleted, cancellationToken);

                if (lesson is null)
                    return ApiResponse<SuggestPracticeExerciseResponse>.NotFound("Không tìm thấy bài học.");

                var skillId = request.LanguageSkillId ?? lesson.LanguageSkillId;
                var skill = await unitOfWork.LanguageSkills.GetByIdAsync(skillId, cancellationToken);
                if (skill is null || skill.IsDeleted)
                    return ApiResponse<SuggestPracticeExerciseResponse>.NotFound("Không tìm thấy kỹ năng.");

                var isListening = IsListeningSkill(skill.Name);

                var sb = new StringBuilder();
                sb.AppendLine($"[YÊU CẦU CỦA CONTENT AUTHOR]: {request.Prompt.Trim()}");
                sb.AppendLine($"- Soạn đúng {count} câu hỏi trắc nghiệm cho bài luyện tập.");
                sb.AppendLine($"- Kỹ năng: {skill.Name}{(isListening ? " (PHẦN NGHE: mỗi câu phải có audioScript)" : "")}");
                sb.AppendLine($"- Cấp độ: {lesson.CertificateLevel?.Code}");
                sb.AppendLine("- Cần cả \"title\" (tiêu đề bài luyện tập) và \"description\" (mô tả ngắn) bằng tiếng Việt.");
                sb.AppendLine("\n[BÀI HỌC]");
                sb.AppendLine($"- Tiêu đề: {lesson.Title}");
                if (!string.IsNullOrWhiteSpace(lesson.Description))
                    sb.AppendLine($"- Mô tả: {lesson.Description}");

                var kanji = (lesson.LessonKanjis ?? new()).Where(x => x.Kanji is not null).Take(30)
                    .Select(x => $"{x.Kanji.Character}({x.Kanji.OnYomi}/{x.Kanji.KunYomi}: {x.Kanji.Meaning})").ToList();
                var vocab = (lesson.LessonVocabularies ?? new()).Where(x => x.Vocabulary is not null).Take(30)
                    .Select(x => $"{x.Vocabulary.Word}[{x.Vocabulary.Reading}]({x.Vocabulary.Meaning})").ToList();
                var grammar = (lesson.LessonGrammars ?? new()).Where(x => x.GrammarPattern is not null).Take(30)
                    .Select(x => $"{x.GrammarPattern.Title}: {x.GrammarPattern.Structure}").ToList();

                if (kanji.Count + vocab.Count + grammar.Count > 0)
                {
                    sb.AppendLine("\n[NỘI DUNG BÀI HỌC — ưu tiên ra câu hỏi bám sát các mục này]");
                    if (kanji.Count > 0) sb.AppendLine("Kanji: " + string.Join(", ", kanji));
                    if (vocab.Count > 0) sb.AppendLine("Từ vựng: " + string.Join(", ", vocab));
                    if (grammar.Count > 0) sb.AppendLine("Ngữ pháp: " + string.Join("; ", grammar));
                }

                var run = await RunAiWithRetryAsync<ChoiceParsed>(
                    userId, ChoiceQuestionSystemPrompt, sb.ToString(),
                    raw => TryParseChoiceResponse(raw, count, isListening, requireTitle: true),
                    "practice exercise suggestion", cancellationToken);

                if (run.QuotaMessage is not null)
                    return ApiResponse<SuggestPracticeExerciseResponse>.Fail(run.QuotaMessage, HttpStatusCode.TooManyRequests);
                if (run.Value is null)
                    return ApiResponse<SuggestPracticeExerciseResponse>.Fail(
                        $"AI trả về kết quả không hợp lệ ({run.Error}). Vui lòng thử lại.", HttpStatusCode.BadGateway);

                foreach (var q in run.Value.Questions) ShuffleOptions(q);

                return ApiResponse<SuggestPracticeExerciseResponse>.Success(new SuggestPracticeExerciseResponse
                {
                    LessonId = lesson.Id,
                    LanguageSkillId = skill.Id,
                    Title = run.Value.Title ?? string.Empty,
                    Description = run.Value.Description,
                    Questions = run.Value.Questions,
                });
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to suggest practice exercise content.");
                return ApiResponse<SuggestPracticeExerciseResponse>.Fail(
                    "Không thể tạo đề xuất bài luyện tập lúc này, vui lòng thử lại.", HttpStatusCode.InternalServerError);
            }
        }

        public async Task<ApiResponse<SubmitContentApprovalResponse>> ConfirmPracticeExerciseSuggestionAsync(
            ConfirmPracticeExerciseRequest request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.Title))
                return ApiResponse<SubmitContentApprovalResponse>.Fail("Bài luyện tập thiếu tiêu đề.");
            if (request.Questions is null || request.Questions.Count == 0)
                return ApiResponse<SubmitContentApprovalResponse>.Fail("Bài luyện tập chưa có câu hỏi nào.");

            // Author có thể đã sửa tay sau bước Suggest -> kiểm tra lại cấu trúc.
            var issues = CollectChoiceQuestionIssues(request.Questions, requireAudioScript: false);
            if (issues.Count > 0)
                return ApiResponse<SubmitContentApprovalResponse>.Fail(string.Join(" ", issues.Take(5)));

            try
            {
                var lesson = await unitOfWork.Lessons.GetByIdAsync(request.LessonId, cancellationToken);
                if (lesson is null || lesson.IsDeleted)
                    return ApiResponse<SubmitContentApprovalResponse>.NotFound("Không tìm thấy bài học.");
                var skill = await unitOfWork.LanguageSkills.GetByIdAsync(request.LanguageSkillId, cancellationToken);
                if (skill is null || skill.IsDeleted)
                    return ApiResponse<SubmitContentApprovalResponse>.NotFound("Không tìm thấy kỹ năng.");

                return await SubmitForApprovalAsync(ImportEntityType.PracticeExercise,
                    new List<ApprovalDraft> { new(request.Title.Trim(), request, false, null) }, cancellationToken);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to submit practice exercise for approval.");
                return ApiResponse<SubmitContentApprovalResponse>.Fail(
                    "Không thể gửi duyệt lúc này, vui lòng thử lại.", HttpStatusCode.InternalServerError);
            }
        }

        // =====================================================================================
        // MOCK TEST (Test -> Section -> Question -> Option), sinh TỪNG SECTION một lượt gọi AI
        // =====================================================================================

        public async Task<ApiResponse<SuggestMockTestResponse>> SuggestMockTestAsync(
            SuggestMockTestRequest request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.Prompt))
                return ApiResponse<SuggestMockTestResponse>.Fail("Vui lòng nhập yêu cầu cho AI.");
            if (request.Sections is null || request.Sections.Count == 0)
                return ApiResponse<SuggestMockTestResponse>.Fail("Vui lòng chọn ít nhất một phần thi.");
            if (request.Sections.Count > MaxMockSections)
                return ApiResponse<SuggestMockTestResponse>.Fail($"Tối đa {MaxMockSections} phần thi cho mỗi lần đề xuất.");

            try
            {
                var userId = claimService.GetUserClaim().Id;

                var (level, levelError, levelStatus) = await LoadLevelAsync(
                    request.CertificateLevelId, request.CertificateTypeId, cancellationToken);
                if (level is null)
                    return ContextError<SuggestMockTestResponse>(levelError, levelStatus);

                var sections = new List<MockTestSectionDraft>();
                var totalMinutes = 0;

                for (var i = 0; i < request.Sections.Count; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var plan = request.Sections[i];
                    var count = Math.Clamp(plan.QuestionCount <= 0 ? 5 : plan.QuestionCount, 1, MaxQuestionsPerSection);

                    // TODO: xác nhận tên repo (unitOfWork.CertificateSections).
                    var certSection = await unitOfWork.CertificateSections.GetByIdAsync(plan.CertificateSectionId, cancellationToken);
                    if (certSection is null || certSection.IsDeleted || certSection.CertificateLevelId != level.Id)
                        return ApiResponse<SuggestMockTestResponse>.Fail(
                            $"Phần thi #{i + 1} không tồn tại hoặc không thuộc cấp độ {level.Code}.");

                    var skill = await unitOfWork.LanguageSkills.GetByIdAsync(plan.LanguageSkillId, cancellationToken);
                    if (skill is null || skill.IsDeleted)
                        return ApiResponse<SuggestMockTestResponse>.Fail($"Phần thi #{i + 1}: không tìm thấy kỹ năng.");

                    var isListening = IsListeningSkill(skill.Name);

                    var sb = new StringBuilder();
                    sb.AppendLine($"[YÊU CẦU CỦA CONTENT AUTHOR]: {request.Prompt.Trim()}");
                    sb.AppendLine($"- Soạn đúng {count} câu hỏi trắc nghiệm cho MỘT phần thi của đề thi thử.");
                    sb.AppendLine($"- Loại chứng chỉ: {level.CertificateType.Name}");
                    sb.AppendLine($"- Cấp độ: {level.Code}{(string.IsNullOrWhiteSpace(level.Description) ? "" : $" ({level.Description})")}");
                    sb.AppendLine($"- Phần thi: {certSection.Name}{(string.IsNullOrWhiteSpace(certSection.Description) ? "" : $" — {certSection.Description}")}");
                    sb.AppendLine($"- Kỹ năng: {skill.Name}{(isListening ? " (PHẦN NGHE: mỗi câu phải có audioScript)" : "")}");
                    sb.AppendLine("- Bám sát dạng câu hỏi và độ khó của kỳ thi thật ở cấp độ này. title/description để chuỗi rỗng.");

                    var run = await RunAiWithRetryAsync<ChoiceParsed>(
                        userId, ChoiceQuestionSystemPrompt, sb.ToString(),
                        raw => TryParseChoiceResponse(raw, count, isListening, requireTitle: false),
                        $"mock test section {i + 1}", cancellationToken);

                    if (run.QuotaMessage is not null)
                        return ApiResponse<SuggestMockTestResponse>.Fail(run.QuotaMessage, HttpStatusCode.TooManyRequests);
                    if (run.Value is null)
                        return ApiResponse<SuggestMockTestResponse>.Fail(
                            $"AI trả về kết quả không hợp lệ cho phần thi \"{certSection.Name}\" ({run.Error}). Vui lòng thử lại.",
                            HttpStatusCode.BadGateway);

                    foreach (var q in run.Value.Questions) ShuffleOptions(q);

                    sections.Add(new MockTestSectionDraft
                    {
                        Title = string.IsNullOrWhiteSpace(plan.Title) ? certSection.Name : plan.Title.Trim(),
                        SortOrder = i + 1,
                        CertificateSectionId = certSection.Id,
                        LanguageSkillId = skill.Id,
                        Questions = run.Value.Questions,
                    });
                    totalMinutes += certSection.DurationMinutes ?? 0;
                }

                var totalQuestions = sections.Sum(s => s.Questions.Count);
                var timeLimit = request.TimeLimitMinutes is > 0
                    ? request.TimeLimitMinutes.Value
                    : totalMinutes > 0 ? totalMinutes : totalQuestions * 2;

                return ApiResponse<SuggestMockTestResponse>.Success(new SuggestMockTestResponse
                {
                    CertificateLevelId = level.Id,
                    Title = string.IsNullOrWhiteSpace(request.Title) ? $"Đề thi thử {level.Code}" : request.Title.Trim(),
                    TimeLimitMinutes = timeLimit,
                    Sections = sections,
                });
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to suggest mock test content.");
                return ApiResponse<SuggestMockTestResponse>.Fail(
                    "Không thể tạo đề xuất đề thi lúc này, vui lòng thử lại.", HttpStatusCode.InternalServerError);
            }
        }

        public async Task<ApiResponse<SubmitContentApprovalResponse>> ConfirmMockTestSuggestionAsync(
            ConfirmMockTestRequest request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.Title))
                return ApiResponse<SubmitContentApprovalResponse>.Fail("Đề thi thiếu tiêu đề.");
            if (request.TimeLimitMinutes <= 0)
                return ApiResponse<SubmitContentApprovalResponse>.Fail("Thời gian làm bài phải lớn hơn 0.");
            if (request.Sections is null || request.Sections.Count == 0 ||
                request.Sections.Any(s => s.Questions is null || s.Questions.Count == 0))
                return ApiResponse<SubmitContentApprovalResponse>.Fail("Đề thi phải có ít nhất một phần thi và mỗi phần có câu hỏi.");

            var issues = new List<string>();
            for (var i = 0; i < request.Sections.Count; i++)
            {
                var sectionIssues = CollectChoiceQuestionIssues(request.Sections[i].Questions, requireAudioScript: false);
                issues.AddRange(sectionIssues.Select(m => $"Phần thi #{i + 1}: {m}"));
            }
            if (issues.Count > 0)
                return ApiResponse<SubmitContentApprovalResponse>.Fail(string.Join(" ", issues.Take(5)));

            try
            {
                var (level, levelError, levelStatus) = await LoadLevelAsync(
                    request.CertificateLevelId, request.CertificateTypeId, cancellationToken);
                if (level is null)
                    return ContextError<SubmitContentApprovalResponse>(levelError, levelStatus);

                foreach (var section in request.Sections)
                {
                    var certSection = await unitOfWork.CertificateSections.GetByIdAsync(section.CertificateSectionId, cancellationToken);
                    if (certSection is null || certSection.IsDeleted || certSection.CertificateLevelId != level.Id)
                        return ApiResponse<SubmitContentApprovalResponse>.Fail(
                            $"Phần thi \"{section.Title}\" không tồn tại hoặc không thuộc cấp độ {level.Code}.");

                    var skill = await unitOfWork.LanguageSkills.GetByIdAsync(section.LanguageSkillId, cancellationToken);
                    if (skill is null || skill.IsDeleted)
                        return ApiResponse<SubmitContentApprovalResponse>.Fail($"Phần thi \"{section.Title}\": không tìm thấy kỹ năng.");
                }

                return await SubmitForApprovalAsync(ImportEntityType.MockTest,
                    new List<ApprovalDraft> { new(request.Title.Trim(), request, false, null) }, cancellationToken);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to submit mock test for approval.");
                return ApiResponse<SubmitContentApprovalResponse>.Fail(
                    "Không thể gửi duyệt lúc này, vui lòng thử lại.", HttpStatusCode.InternalServerError);
            }
        }

        // =====================================================================================
        // CHOICE QUESTIONS — parse / validate / blind-solve (dùng chung Practice + MockTest)
        // =====================================================================================

        private sealed record ChoiceRawOption(string? Text, bool IsCorrect);
        private sealed record ChoiceRawQuestion(
            string? QuestionText, string? AudioScript, string? Explanation, List<ChoiceRawOption>? Options);
        private sealed record ChoiceRawResponse(string? Title, string? Description, List<ChoiceRawQuestion>? Questions);
        internal sealed record ChoiceParsed(string? Title, string? Description, List<ChoiceQuestionDraft> Questions);

        internal static (ChoiceParsed? Value, string? Error) TryParseChoiceResponse(
            string rawJson, int count, bool requireAudioScript, bool requireTitle)
        {
            ChoiceRawResponse? parsed;
            try
            {
                parsed = JsonSerializer.Deserialize<ChoiceRawResponse>(rawJson, JsonOpts);
            }
            catch (JsonException)
            {
                return (null, "JSON sai định dạng (có thể bị ngắt giữa chừng)");
            }

            if (parsed?.Questions is null || parsed.Questions.Count == 0)
                return (null, "không có câu hỏi nào trong JSON trả về");
            if (parsed.Questions.Count < count)
                return (null, $"chỉ có {parsed.Questions.Count}/{count} câu hỏi");

            var questions = parsed.Questions.Take(count).Select(q => new ChoiceQuestionDraft
            {
                QuestionText = (q.QuestionText ?? string.Empty).Trim(),
                AudioScript = requireAudioScript && !string.IsNullOrWhiteSpace(q.AudioScript) ? q.AudioScript.Trim() : null,
                Explanation = (q.Explanation ?? string.Empty).Trim(),
                Options = (q.Options ?? new List<ChoiceRawOption>())
                    .Select(o => new ChoiceOptionDraft { Text = (o.Text ?? string.Empty).Trim(), IsCorrect = o.IsCorrect })
                    .ToList(),
            }).ToList();

            var issues = CollectChoiceQuestionIssues(questions, requireAudioScript);

            var title = parsed.Title?.Trim();
            var description = parsed.Description?.Trim();
            if (requireTitle)
            {
                if (string.IsNullOrWhiteSpace(title)) issues.Add("thiếu tiêu đề bài luyện tập");
                else if (LooksEnglish(title) || LooksEnglish(description)) issues.Add("tiêu đề/mô tả lẫn tiếng Anh, phải viết bằng tiếng Việt");
            }

            if (issues.Count > 0)
                return (null, string.Join("; ", issues.Take(5)));

            return (new ChoiceParsed(title, string.IsNullOrWhiteSpace(description) ? null : description, questions), null);
        }

        /// <summary>Kiểm tra cấu trúc câu hỏi trắc nghiệm bằng code (không tốn AI). Trả về danh sách vấn đề (tiếng Việt).</summary>
        internal static List<string> CollectChoiceQuestionIssues(List<ChoiceQuestionDraft> questions, bool requireAudioScript)
        {
            var issues = new List<string>();

            for (var i = 0; i < questions.Count; i++)
            {
                var q = questions[i];
                var n = i + 1;

                if (string.IsNullOrWhiteSpace(q.QuestionText))
                {
                    issues.Add($"Câu {n}: thiếu nội dung câu hỏi.");
                    continue;
                }

                var options = q.Options ?? new List<ChoiceOptionDraft>();

                if (options.Count != ChoiceOptionCount)
                    issues.Add($"Câu {n}: phải có đúng {ChoiceOptionCount} đáp án (đang có {options.Count}).");
                if (options.Any(o => string.IsNullOrWhiteSpace(o.Text)))
                    issues.Add($"Câu {n}: có đáp án bị trống.");

                var correctCount = options.Count(o => o.IsCorrect);
                if (correctCount != 1)
                    issues.Add($"Câu {n}: phải có đúng 1 đáp án đúng (đang có {correctCount}).");

                if (options.Select(o => NormalizeKey(o.Text ?? string.Empty)).Distinct().Count() != options.Count)
                    issues.Add($"Câu {n}: có các đáp án trùng nhau.");

                if (string.IsNullOrWhiteSpace(q.Explanation))
                    issues.Add($"Câu {n}: thiếu phần giải thích.");
                else if (LooksEnglish(q.Explanation))
                    issues.Add($"Câu {n}: phần giải thích lẫn tiếng Anh, phải viết bằng tiếng Việt.");

                if (requireAudioScript && string.IsNullOrWhiteSpace(q.AudioScript))
                    issues.Add($"Câu {n}: câu nghe thiếu audioScript (nội dung nghe).");
            }

            var duplicateQuestions = questions
                .Where(q => !string.IsNullOrWhiteSpace(q.QuestionText))
                .GroupBy(q => NormalizeKey(q.QuestionText) + "|" + NormalizeKey(q.AudioScript ?? string.Empty) + "|" +
                              string.Join("/", (q.Options ?? new()).Select(o => NormalizeKey(o.Text ?? string.Empty))))
                .Count(g => g.Count() > 1);
            if (duplicateQuestions > 0)
                issues.Add("Có câu hỏi bị trùng lặp hoàn toàn.");

            return issues;
        }

        private sealed record BlindQuestion(int N, string QuestionText, string? AudioScript, List<string> Options);
        private sealed record BlindAiAnswer(int Q, string? Choice, bool IsAmbiguous);
        private sealed record BlindAiResponse(List<BlindAiAnswer>? Answers);

        private async Task<(Dictionary<int, (int Choice, bool Ambiguous)>? Map, string? Error)> SolveBlindAsync(
            int userId, List<BlindQuestion> questions, CancellationToken cancellationToken)
        {
            var map = new Dictionary<int, (int Choice, bool Ambiguous)>();

            foreach (var chunk in questions.Chunk(BlindSolveChunkSize))
            {
                var expected = chunk.ToDictionary(q => q.N, q => q.Options.Count);

                var sb = new StringBuilder();
                sb.AppendLine("[ĐỀ CẦN GIẢI — chọn đáp án cho từng câu]");
                foreach (var q in chunk)
                {
                    sb.AppendLine($"\n[Câu {q.N}] {q.QuestionText}");
                    if (!string.IsNullOrWhiteSpace(q.AudioScript))
                        sb.AppendLine($"Nội dung nghe: {q.AudioScript}");
                    for (var i = 0; i < q.Options.Count; i++)
                        sb.AppendLine($"{LetterOf(i)}. {q.Options[i]}");
                }

                var run = await RunAiWithRetryAsync<Dictionary<int, (int Choice, bool Ambiguous)>>(
                    userId, BlindSolveSystemPrompt, sb.ToString(),
                    raw => TryParseBlindAnswers(raw, expected),
                    "blind solve", cancellationToken);

                if (run.QuotaMessage is not null) return (null, "hết hạn mức gọi AI");
                if (run.Value is null) return (null, run.Error);

                foreach (var kv in run.Value) map[kv.Key] = kv.Value;
            }

            return (map, null);
        }

        private static (Dictionary<int, (int Choice, bool Ambiguous)>? Value, string? Error) TryParseBlindAnswers(
            string rawJson, Dictionary<int, int> expectedOptionCounts)
        {
            BlindAiResponse? parsed;
            try
            {
                parsed = JsonSerializer.Deserialize<BlindAiResponse>(rawJson, JsonOpts);
            }
            catch (JsonException)
            {
                return (null, "JSON sai định dạng (có thể bị ngắt giữa chừng)");
            }

            if (parsed?.Answers is null || parsed.Answers.Count == 0)
                return (null, "không có đáp án nào trong JSON trả về");

            var map = new Dictionary<int, (int Choice, bool Ambiguous)>();
            foreach (var a in parsed.Answers)
            {
                if (!expectedOptionCounts.TryGetValue(a.Q, out var optionCount)) continue;
                var letter = a.Choice?.Trim().ToUpperInvariant();
                if (string.IsNullOrEmpty(letter) || letter.Length != 1) continue;
                var index = letter[0] - 'A';
                if (index < 0 || index >= optionCount) continue;
                map[a.Q] = (index, a.IsAmbiguous);
            }

            var missing = expectedOptionCounts.Keys.Except(map.Keys).ToList();
            if (missing.Count > 0)
                return (null, $"thiếu/sai đáp án cho {missing.Count} câu (số câu: {string.Join(",", missing.Take(5))})");

            return (map, null);
        }


        // =====================================================================================
        // NGỮ CẢNH NỘI DUNG: bài học (nếu có) + cấp độ chứng chỉ hiệu lực
        // =====================================================================================

        private sealed record ContentContext(Lesson? Lesson, CertificateLevel Level);

        /// <summary>
        /// Xác định ngữ cảnh cho Suggest/Confirm Kanji, Vocabulary, Grammar.
        /// - Author chọn bài học: cấp độ lấy TỪ CHÍNH BÀI HỌC đó (nếu request cũng gửi cấp độ mà lệch thì báo lỗi).
        /// - Không chọn bài học: bắt buộc phải có CertificateLevelId trong request.
        /// </summary>
        private async Task<(ContentContext? Context, string? Error, HttpStatusCode Status)> ResolveContentContextAsync(
            int? lessonId, int? requestedLevelId, bool includeLinkedContent, CancellationToken cancellationToken)
        {
            if (lessonId.HasValue)
            {
                var query = unitOfWork.Lessons.Query()
                    .Include(l => l.CertificateLevel).ThenInclude(cl => cl.CertificateType)
                    .Include(l => l.LanguageSkill)
                    .AsQueryable();

                if (includeLinkedContent)
                {
                    query = query
                        .Include(l => l.LessonKanjis!).ThenInclude(x => x.Kanji)
                        .Include(l => l.LessonVocabularies!).ThenInclude(x => x.Vocabulary)
                        .Include(l => l.LessonGrammars!).ThenInclude(x => x.GrammarPattern);
                }

                var lesson = await query.AsSplitQuery()
                    .FirstOrDefaultAsync(l => l.Id == lessonId.Value && !l.IsDeleted, cancellationToken);

                if (lesson is null)
                    return (null, "Không tìm thấy bài học.", HttpStatusCode.NotFound);
                if (requestedLevelId.HasValue && requestedLevelId.Value != lesson.CertificateLevelId)
                    return (null, "Cấp độ chứng chỉ không khớp với cấp độ của bài học đã chọn.", HttpStatusCode.BadRequest);

                return (new ContentContext(lesson, lesson.CertificateLevel), null, HttpStatusCode.OK);
            }

            if (!requestedLevelId.HasValue)
                return (null, "Vui lòng chọn bài học hoặc cấp độ chứng chỉ.", HttpStatusCode.BadRequest);

            var (level, levelError, levelStatus) = await LoadLevelAsync(requestedLevelId.Value, null, cancellationToken);
            if (level is null)
                return (null, levelError, levelStatus);

            return (new ContentContext(null, level), null, HttpStatusCode.OK);
        }

        /// <summary>
        /// Nạp cấp độ kèm loại chứng chỉ. Nếu request có chọn CertificateTypeId thì cấp độ phải thuộc đúng loại đó
        /// (ví dụ N4 thuộc JLPT, không thuộc NAT-TEST). Loại chứng chỉ phải còn hoạt động.
        /// </summary>
        private async Task<(CertificateLevel? Level, string? Error, HttpStatusCode Status)> LoadLevelAsync(
            int levelId, int? certificateTypeId, CancellationToken cancellationToken)
        {
            var level = await unitOfWork.CertificateLevels.Query()
                .Include(l => l.CertificateType)
                .FirstOrDefaultAsync(l => l.Id == levelId && !l.IsDeleted, cancellationToken);

            if (level is null)
                return (null, "Không tìm thấy cấp độ chứng chỉ.", HttpStatusCode.NotFound);
            if (level.CertificateType is null || level.CertificateType.IsDeleted || !level.CertificateType.IsActive)
                return (null, "Loại chứng chỉ của cấp độ này không tồn tại hoặc đã ngừng hoạt động.", HttpStatusCode.BadRequest);
            if (certificateTypeId.HasValue && certificateTypeId.Value != level.CertificateTypeId)
                return (null, $"Cấp độ {level.Code} không thuộc loại chứng chỉ đã chọn.", HttpStatusCode.BadRequest);

            return (level, null, HttpStatusCode.OK);
        }

        private static ApiResponse<T> ContextError<T>(string? error, HttpStatusCode status) =>
            status == HttpStatusCode.NotFound
                ? ApiResponse<T>.NotFound(error ?? "Không tìm thấy dữ liệu.")
                : ApiResponse<T>.Fail(error ?? "Yêu cầu không hợp lệ.");

        /// <summary>Thêm khối [NGỮ CẢNH] vào prompt: cấp độ, bài học đang soạn, và các mục ĐÃ có sẵn trong bài này.</summary>
        private static void AppendContentContext(
            StringBuilder sb, ContentContext ctx, string label, IEnumerable<string>? alreadyInLesson)
        {
            sb.AppendLine("\n[NGỮ CẢNH]");
            var levelDesc = string.IsNullOrWhiteSpace(ctx.Level.Description) ? string.Empty : $" ({ctx.Level.Description})";
            var typeName = ctx.Level.CertificateType?.Name;
            var typePrefix = string.IsNullOrWhiteSpace(typeName) ? string.Empty : typeName + " ";
            sb.AppendLine($"- Cấp độ: {typePrefix}{ctx.Level.Code}{levelDesc} — chỉ đề xuất {label} phù hợp ĐÚNG cấp độ này.");

            if (ctx.Lesson is null) return;

            sb.AppendLine("- Bài học đang soạn:");
            sb.AppendLine($"  + Tiêu đề: {ctx.Lesson.Title}");
            if (!string.IsNullOrWhiteSpace(ctx.Lesson.Description))
                sb.AppendLine($"  + Mô tả: {ctx.Lesson.Description}");
            if (ctx.Lesson.LanguageSkill is not null)
                sb.AppendLine($"  + Kỹ năng: {ctx.Lesson.LanguageSkill.Name}");

            var linked = alreadyInLesson?
                .Where(s => !string.IsNullOrWhiteSpace(s)).Distinct().Take(100).ToList();
            if (linked is { Count: > 0 })
                sb.AppendLine($"  + {label} ĐÃ có trong bài này (không đề xuất lại): {string.Join(", ", linked)}");
        }

        // =====================================================================================
        // SHARED HELPERS
        // =====================================================================================

        private sealed record AiRunResult<T>(T? Value, string? Error, string? QuotaMessage) where T : class;

        /// <summary>Gọi AI + parse/validate, thử lại tối đa MaxAiAttempts lần, kèm lý do lần trước bị từ chối.</summary>
        private async Task<AiRunResult<T>> RunAiWithRetryAsync<T>(
            int userId, string systemPrompt, string userPrompt,
            Func<string, (T? Value, string? Error)> parseAndValidate,
            string label, CancellationToken cancellationToken) where T : class
        {
            string? lastError = null;

            for (var attempt = 1; attempt <= MaxAiAttempts; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var currentPrompt = lastError is null
                    ? userPrompt
                    : $"{userPrompt}\n\n[LƯU Ý] Lần trả lời trước bị từ chối vì: {lastError}. Hãy sửa lại.";

                string rawJson;
                try
                {
                    rawJson = StripCodeFence(await aiClient.GenerateContentJsonAsync(
                        userId, systemPrompt, currentPrompt, cancellationToken));
                }
                catch (AiQuotaExceededException ex)
                {
                    return new AiRunResult<T>(null, null, ex.Message);
                }

                var (value, error) = parseAndValidate(rawJson);
                if (value is not null && error is null)
                    return new AiRunResult<T>(value, null, null);

                lastError = error ?? "không đọc được kết quả";
                logger.LogWarning("AI {Label} rejected (attempt {Attempt}/{Max}): {Reason}. UserId={UserId}",
                    label, attempt, MaxAiAttempts, lastError, userId);
            }

            return new AiRunResult<T>(null, lastError, null);
        }

        private sealed record ApprovalDraft(string RowKey, object Payload, bool IsExisting, int? TargetId);

        /// <summary>Ghi các bản nháp vào ContentApprovalRequest (cùng 1 BatchId) để admin duyệt — giống luồng Confirm Kanji.</summary>
        private async Task<ApiResponse<SubmitContentApprovalResponse>> SubmitForApprovalAsync(
            ImportEntityType entityType, List<ApprovalDraft> drafts, CancellationToken cancellationToken)
        {
            try
            {
                var userId = claimService.GetUserClaim().Id;
                var batchId = Guid.NewGuid();
                var now = DateTime.UtcNow;

                var rows = new List<ContentApprovalRequest>();
                var sortOrder = 1;
                foreach (var draft in drafts)
                {
                    rows.Add(new ContentApprovalRequest
                    {
                        BatchId = batchId,
                        EntityType = entityType,
                        Action = draft.IsExisting ? ImportRowAction.Modified : ImportRowAction.New,
                        TargetEntityId = draft.TargetId,
                        RowKey = draft.RowKey,
                        PayloadJson = JsonSerializer.Serialize(draft.Payload, JsonOpts),
                        SourceRowNumber = sortOrder++,
                        SourceFileName = "AI Content Assistant",
                        Status = ContentApprovalStatus.Draft,
                        SubmittedByUserId = userId,
                        SubmittedAt = now,
                    });
                }

                // TODO: nếu IContentApprovalRequestRepository không có AddRangeAsync, đổi thành
                // foreach (var row in rows) await unitOfWork.ContentApprovalRequests.AddAsync(row, cancellationToken);
                await unitOfWork.ContentApprovalRequests.AddRangeAsync(rows, cancellationToken);
                await unitOfWork.SaveChangesAsync(cancellationToken);

                return ApiResponse<SubmitContentApprovalResponse>.Success(new SubmitContentApprovalResponse
                {
                    BatchId = batchId,
                    SubmittedCount = rows.Count,
                });
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to submit {EntityType} for approval.", entityType);
                return ApiResponse<SubmitContentApprovalResponse>.Fail(
                    "Không thể gửi duyệt lúc này, vui lòng thử lại.", HttpStatusCode.InternalServerError);
            }
        }

        internal static bool IsListeningSkill(string? skillName)
        {
            if (string.IsNullOrWhiteSpace(skillName)) return false;
            var s = skillName.ToLowerInvariant();
            return s.Contains("listen") || s.Contains("nghe") || s.Contains("聴");
        }

        private static bool IsKanaOnly(string text) =>
            Regex.IsMatch(text, @"^[\u3040-\u309F\u30A0-\u30FFー・]+$");

        internal static string NormalizeKey(string? text) =>
            string.IsNullOrEmpty(text) ? string.Empty : new string(text.Where(c => !char.IsWhiteSpace(c)).ToArray());

        private static string LetterOf(int index) => index is >= 0 and < 26 ? ((char)('A' + index)).ToString() : "?";

        private static string Truncate(string text, int max) =>
            text.Length <= max ? text : text[..max] + "…";

        private static bool IsValidJson(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return false;
            try
            {
                using var _ = JsonDocument.Parse(json);
                return true;
            }
            catch (JsonException)
            {
                return false;
            }
        }

        /// <summary>Xáo trộn thứ tự đáp án bằng code để loại bỏ thiên kiến vị trí của AI (hay đặt đáp án đúng ở A/B).</summary>
        internal static void ShuffleOptions(ChoiceQuestionDraft question)
        {
            var options = question.Options;
            for (var i = options.Count - 1; i > 0; i--)
            {
                var j = Random.Shared.Next(i + 1);
                (options[i], options[j]) = (options[j], options[i]);
            }
        }

        // =====================================================================================
        // KANJI helpers (giữ nguyên)
        // =====================================================================================

        private static string BuildKanjiUserPrompt(
            string authorPrompt, int maxItems, ContentContext ctx, List<string> existingCharacters)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"[YÊU CẦU CỦA CONTENT AUTHOR]: {authorPrompt.Trim()}");
            sb.AppendLine($"- Đề xuất tối đa {maxItems} Kanji.");
            AppendContentContext(sb, ctx, "Kanji",
                ctx.Lesson?.LessonKanjis?.Where(x => x.Kanji is not null).Select(x => x.Kanji.Character));
            if (existingCharacters.Count > 0)
            {
                sb.AppendLine("\n[LƯU Ý]: Các Kanji sau ĐÃ CÓ trong hệ thống, tránh đề xuất lại trừ khi content author");
                sb.AppendLine("  yêu cầu rõ là muốn cập nhật/bổ sung thông tin cho Kanji đã có:");
                sb.AppendLine("  " + string.Join(", ", existingCharacters.Take(300)));
            }
            return sb.ToString();
        }

        private sealed record KanjiAiRawItem(
            string? Character, string? Meaning, string? SinoVietnamese,
            string? OnYomi, string? KunYomi, int StrokeCount, string? Rationale);

        private sealed record KanjiAiRawResponse(List<KanjiAiRawItem>? Items);

        private static List<KanjiSuggestionItem>? TryParseKanjiItems(string rawJson, out string? error)
        {
            try
            {
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var parsed = JsonSerializer.Deserialize<KanjiAiRawResponse>(rawJson, options);
                if (parsed?.Items is null || parsed.Items.Count == 0)
                {
                    error = "không có dữ liệu Kanji nào trong JSON trả về";
                    return null;
                }

                error = null;
                return parsed.Items
                    .Where(i => !string.IsNullOrWhiteSpace(i.Character))
                    .Select(i => new KanjiSuggestionItem
                    {
                        Character = i.Character!.Trim(),
                        Meaning = (i.Meaning ?? string.Empty).Trim(),
                        SinoVietnamese = i.SinoVietnamese?.Trim(),
                        OnYomi = i.OnYomi?.Trim(),
                        KunYomi = i.KunYomi?.Trim(),
                        StrokeCount = i.StrokeCount,
                        Rationale = i.Rationale?.Trim(),
                    })
                    .ToList();
            }
            catch (JsonException)
            {
                error = "JSON sai định dạng (có thể bị ngắt giữa chừng)";
                return null;
            }
        }

        private static readonly string[] EnglishTellSigns =
        {
            " the ", " and ", " you ", " your ", " is ", " are ", " with ", " this ", " that ",
            " for ", " have ", " has ", " will ", " meaning ", " character ", " stroke ",
        };

        internal static bool LooksEnglish(string? text)
        {
            if (string.IsNullOrWhiteSpace(text)) return false;
            var padded = " " + text.ToLowerInvariant() + " ";
            return EnglishTellSigns.Count(sign => padded.Contains(sign)) >= 2;
        }

        private static string? ValidateKanjiItems(List<KanjiSuggestionItem> items, int maxItems)
        {
            if (items.Count == 0)
                return "danh sách rỗng";
            if (items.Count > maxItems + 3)
                return $"trả về quá nhiều Kanji ({items.Count}) so với yêu cầu ({maxItems})";
            if (items.Any(i => string.IsNullOrWhiteSpace(i.Character) || i.Character.Length > 4))
                return "có Kanji thiếu ký tự hoặc ký tự không hợp lệ";
            if (items.Any(i => string.IsNullOrWhiteSpace(i.Meaning)))
                return "có Kanji thiếu nghĩa";
            if (items.Any(i => LooksEnglish(i.Meaning) || LooksEnglish(i.Rationale)))
                return "phần nghĩa/lý do lẫn tiếng Anh, phải viết hoàn toàn bằng tiếng Việt";
            if (items.Select(i => i.Character).Distinct().Count() != items.Count)
                return "có Kanji bị trùng lặp trong cùng một lần đề xuất";
            return null;
        }

        internal static string StripCodeFence(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return string.Empty;
            var s = raw.Trim();
            if (!s.StartsWith("```")) return s;

            var firstNewLine = s.IndexOf('\n');
            var lastFence = s.LastIndexOf("```", StringComparison.Ordinal);
            if (firstNewLine >= 0 && lastFence > firstNewLine)
                return s[(firstNewLine + 1)..lastFence].Trim();
            if (firstNewLine >= 0)
                return s[(firstNewLine + 1)..].Trim();
            return s;
        }
    }
}