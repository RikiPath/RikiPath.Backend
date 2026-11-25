using System.Text;
using System.Text.Json;
using ClosedXML.Excel;
using RikiPath.Application.IServices;

namespace RikiPath.Application.Services.ContentAuthoring;

/// <summary>Sinh 7 mẫu nhập liệu JLPT chuẩn (.xlsx / .json) kèm hướng dẫn, phiên bản và dữ liệu mẫu tách biệt.</summary>
public sealed class SkillTemplateService : ISkillTemplateService
{
    private const string SchemaVersion = "2.0";

    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public TemplateFileResult GenerateTemplate(string templateKind, string format)
    {
        var normalizedKind = (templateKind ?? string.Empty).Trim().ToLowerInvariant();
        var isJson = string.Equals(format?.Trim(), "json", StringComparison.OrdinalIgnoreCase);

        return normalizedKind switch
        {
            "vocabulary" => isJson
                ? JsonFile("rikipath-template-vocabulary-v2.json", BuildVocabularyJson())
                : ExcelFile("rikipath-template-vocabulary-v2.xlsx", BuildVocabularyWorkbook()),
            "kanji" => isJson
                ? JsonFile("rikipath-template-kanji-v2.json", BuildKanjiJson())
                : ExcelFile("rikipath-template-kanji-v2.xlsx", BuildKanjiWorkbook()),
            "grammar" => isJson
                ? JsonFile("rikipath-template-grammar-v2.json", BuildGrammarJson())
                : ExcelFile("rikipath-template-grammar-v2.xlsx", BuildGrammarWorkbook()),
            "reading" => isJson
                ? JsonFile("rikipath-template-reading-v2.json", BuildReadingJson())
                : ExcelFile("rikipath-template-reading-v2.xlsx", BuildReadingWorkbook()),
            "listening" => isJson
                ? JsonFile("rikipath-template-listening-v2.json", BuildListeningJson())
                : ExcelFile("rikipath-template-listening-v2.xlsx", BuildListeningWorkbook()),
            "mixed-practice" => isJson
                ? JsonFile("rikipath-template-mixed-practice-v2.json", BuildMixedPracticeJson())
                : ExcelFile("rikipath-template-mixed-practice-v2.xlsx", BuildMixedPracticeWorkbook()),
            "full-mock-test" => isJson
                ? JsonFile("rikipath-template-full-mock-test-v2.json", BuildFullMockTestJson())
                : ExcelFile("rikipath-template-full-mock-test-v2.xlsx", BuildFullMockTestWorkbook()),
            _ => throw new ArgumentException(
                "Loại mẫu không hợp lệ. Hỗ trợ: vocabulary, kanji, grammar, reading, listening, mixed-practice, full-mock-test.",
                nameof(templateKind))
        };
    }

    private static TemplateFileResult JsonFile(string name, object payload)
        => new(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload, JsonOpts)), "application/json; charset=utf-8", name);

    private static TemplateFileResult ExcelFile(string name, XLWorkbook wb)
    {
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        wb.Dispose();
        return new(ms.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", name);
    }

    private static void AddGuideSheet(XLWorkbook wb, string title, (string Column, string Requirement, string Notes)[] columns, string[] supportedTypes)
    {
        var ws = wb.AddWorksheet("_Guide");
        ws.Cell(1, 1).Value = "Template";
        ws.Cell(1, 2).Value = title;
        ws.Cell(2, 1).Value = "Version";
        ws.Cell(2, 2).Value = SchemaVersion;
        ws.Cell(3, 1).Value = "Supported JLPT Levels";
        ws.Cell(3, 2).Value = "N5, N4, N3, N2";
        ws.Cell(4, 1).Value = "Supported QuestionTypes";
        ws.Cell(4, 2).Value = string.Join(", ", supportedTypes);
        ws.Cell(5, 1).Value = "Sample Rule";
        ws.Cell(5, 2).Value = "Dữ liệu mẫu đặt trong sheet _Samples (hoặc đánh dấu IsSample=TRUE) sẽ không được import làm câu hỏi thật.";

        ws.Cell(7, 1).Value = "ColumnName";
        ws.Cell(7, 2).Value = "Requirement";
        ws.Cell(7, 3).Value = "Validation Notes";
        ws.Range(7, 1, 7, 3).Style.Font.Bold = true;

        for (var i = 0; i < columns.Length; i++)
        {
            ws.Cell(8 + i, 1).Value = columns[i].Column;
            ws.Cell(8 + i, 2).Value = columns[i].Requirement;
            ws.Cell(8 + i, 3).Value = columns[i].Notes;
        }
        ws.Columns().AdjustToContents();
    }

    private static void AddDataSheet(XLWorkbook wb, string name, string[] headers, string[][] sampleRows)
    {
        var dataWs = wb.AddWorksheet(name);
        for (var i = 0; i < headers.Length; i++)
        {
            dataWs.Cell(1, i + 1).Value = headers[i];
            dataWs.Cell(1, i + 1).Style.Font.Bold = true;
        }
        dataWs.Columns().AdjustToContents();

        var sampleWs = wb.Worksheets.Contains("_Samples") ? wb.Worksheet("_Samples") : wb.AddWorksheet("_Samples");
        var startRow = sampleWs.LastRowUsed()?.RowNumber() + 2 ?? 1;
        sampleWs.Cell(startRow, 1).Value = $"[SAMPLE SHEET: {name}]";
        sampleWs.Cell(startRow, 1).Style.Font.Bold = true;
        for (var i = 0; i < headers.Length; i++)
            sampleWs.Cell(startRow + 1, i + 1).Value = headers[i];
        for (var r = 0; r < sampleRows.Length; r++)
            for (var c = 0; c < sampleRows[r].Length; c++)
                sampleWs.Cell(startRow + 2 + r, c + 1).Value = sampleRows[r][c];
        sampleWs.Columns().AdjustToContents();
    }

    private static XLWorkbook BuildVocabularyWorkbook()
    {
        var wb = new XLWorkbook();
        string[] headers = ["LevelCode", "TemplateKind", "QuestionType", "Word", "Reading", "Meaning", "ExampleSentence", "QuestionPrompt", "Option1", "Option2", "Option3", "Option4", "CorrectAnswer", "AcceptedAnswers", "Explanation", "AudioUrl", "ImageUrl", "Topic", "Tags", "Source"];
        AddGuideSheet(wb, "Vocabulary Template", [
            ("LevelCode", "Required", "N5 | N4 | N3 | N2"),
            ("TemplateKind", "Required", "VocabMeaning | VocabReading | VocabContextFill | VocabUsage"),
            ("QuestionType", "Required", "SingleChoice | MultipleChoice | FillBlank"),
            ("Word", "Required", "Từ/cụm từ tiếng Nhật"),
            ("Reading", "Conditional", "Hiragana/Katakana bắt buộc cho VocabReading"),
            ("Meaning", "Optional", "Nghĩa tiếng Việt"),
            ("QuestionPrompt", "Required", "Đề bài hoàn chỉnh"),
            ("Option1..Option4", "Conditional", "Ít nhất 2 lựa chọn khi trắc nghiệm"),
            ("CorrectAnswer", "Conditional", "1..4 hoặc chữ A..D khi trắc nghiệm"),
            ("AcceptedAnswers", "Conditional", "Phân tách bởi | khi FillBlank"),
            ("AudioUrl", "Optional", "Chỉ điền khi nội dung có audio; URL phải trỏ tới file âm thanh đã upload."),
            ("ImageUrl", "Optional", "Có thể để trống nếu nội dung không có hình ảnh."),
        ], ["SingleChoice", "MultipleChoice", "FillBlank"]);
        AddDataSheet(wb, "VocabularyQuestions", headers, [
            ["N5", "VocabReading", "SingleChoice", "図書館", "としょかん", "Thư viện", "毎日図書館で勉強します。", "「図書館」の読み方として最もよいものを1つ選びなさい。", "としょかん", "としょがん", "どしょかん", "とうしょかん", "1", "", "図書館 đọc là としょかん.", "", "", "Trường học", "N5,Từ vựng", "Giáo trình nội bộ"]
        ]);
        return wb;
    }

    private static XLWorkbook BuildKanjiWorkbook()
    {
        var wb = new XLWorkbook();
        string[] headers = ["LevelCode", "TemplateKind", "QuestionType", "Kanji", "Meaning", "SinoVietnamese", "OnYomi", "KunYomi", "StrokeCount", "ContextSentence", "QuestionPrompt", "Option1", "Option2", "Option3", "Option4", "CorrectAnswer", "Explanation", "Topic", "Source"];
        AddGuideSheet(wb, "Kanji Template", [
            ("LevelCode", "Required", "N5 | N4 | N3 | N2"),
            ("TemplateKind", "Required", "KanjiReading | KanjiMeaning | KanjiContextSelect"),
            ("Kanji", "Required", "Ký tự chữ Hán hợp lệ"),
            ("Meaning", "Required", "Nghĩa tiếng Việt"),
            ("OnYomi / KunYomi", "Optional", "Không tự bịa cách đọc không chắc chắn"),
            ("QuestionPrompt", "Required", "Ngữ cảnh hoặc câu hỏi nhận diện"),
            ("ImageUrl", "Optional", "Có thể để trống nếu nội dung không có hình ảnh."),
        ], ["SingleChoice"]);
        AddDataSheet(wb, "KanjiQuestions", headers, [
            ["N5", "KanjiReading", "SingleChoice", "休", "Nghỉ ngơi", "HƯU", "キュウ", "やす.む", "6", "日曜日は休みます。", "下線のことば「休みます」の漢字の意味に合うものを選びなさい。", "Ngủ", "Nghỉ", "Đi", "Đọc", "2", "休（HƯU）nghĩa là nghỉ ngơi (やすむ).", "Sinh hoạt", "Giáo trình N5"]
        ]);
        return wb;
    }

    private static XLWorkbook BuildGrammarWorkbook()
    {
        var wb = new XLWorkbook();
        string[] headers = ["LevelCode", "TemplateKind", "QuestionType", "GrammarPattern", "Meaning", "Structure", "UsageExplanation", "ExampleSentence", "QuestionPrompt", "Option1", "Option2", "Option3", "Option4", "CorrectAnswer", "AcceptedAnswers", "OrderingTokens", "OrderingAnswer", "Explanation", "Topic", "Source"];
        AddGuideSheet(wb, "Grammar Template", [
            ("LevelCode", "Required", "N5 | N4 | N3 | N2"),
            ("TemplateKind", "Required", "GrammarMultipleChoice | GrammarFillBlank | GrammarOrdering"),
            ("QuestionType", "Required", "SingleChoice | MultipleChoice | FillBlank | Ordering"),
            ("GrammarPattern", "Required", "Tên mẫu ngữ pháp"),
            ("AcceptedAnswers", "Conditional", "Phân tách | khi QuestionType=FillBlank"),
            ("OrderingTokens / OrderingAnswer", "Conditional", "Phân tách | (tối thiểu 2 cụm) khi QuestionType=Ordering"),
        ], ["SingleChoice", "MultipleChoice", "FillBlank", "Ordering"]);
        AddDataSheet(wb, "GrammarQuestions", headers, [
            ["N4", "GrammarOrdering", "Ordering", "～てみる", "Thử làm gì", "Vて + みる", "Diễn tả hành động thử nghiệm", "食べてみてください。", "次の文の ★ に入る最もよいものを1つ選びなさい。", "", "", "", "", "", "", "この料理|食べて|おいしそう|みませんか", "おいしそう|この料理|食べて|みませんか", "おいしそう → この料理 → 食べて → みませんか.", "Đời sống", "JLPT N4 mẫu"]
        ]);
        return wb;
    }

    private static XLWorkbook BuildReadingWorkbook()
    {
        var wb = new XLWorkbook();
        AddGuideSheet(wb, "Reading Passage & Question Group Template", [
            ("Passages.PassageCode", "Required", "Mã định danh đoạn đọc duy nhất trong file (VD: P1)"),
            ("Passages.PassageText", "Required", "Toàn văn đoạn đọc tiếng Nhật (không copy lặp vào từng dòng câu hỏi)"),
            ("ReadingQuestions.PassageCode", "Required", "Tham chiếu Passages.PassageCode"),
            ("Passages.ImageUrl", "Optional", "Có thể để trống nếu đoạn đọc không có hình ảnh."),
        ], ["SingleChoice", "MultipleChoice"]);
        AddDataSheet(wb, "Passages", ["PassageCode", "LevelCode", "Title", "Topic", "PassageText", "Instructions", "ImageUrl", "Source"], [
            ["R-N4-01", "N4", "Thông báo thư viện trường", "Trường học", "来週の月曜日は休館日です。本は火曜日までに返してください。", "Đọc đoạn văn sau và trả lời các câu hỏi.", "", "Tài liệu biên soạn N4"]
        ]);
        AddDataSheet(wb, "ReadingQuestions", ["PassageCode", "QuestionGroupTitle", "GroupSortOrder", "QuestionPrompt", "QuestionType", "Option1", "Option2", "Option3", "Option4", "CorrectAnswer", "Explanation"], [
            ["R-N4-01", "Đọc hiểu ngắn", "1", "図書館が休みなのはいつですか。", "SingleChoice", "今週の月曜日", "来週の月曜日", "来週の火曜日", "今週の火曜日", "2", "Đoạn văn ghi rõ 来週の月曜日は休館日です."],
            ["R-N4-01", "Đọc hiểu ngắn", "2", "本はいつまでに返さなければなりませんか。", "SingleChoice", "月曜日まで", "火曜日まで", "水曜日まで", "日曜日まで", "2", "本は火曜日までに返してください."]
        ]);
        return wb;
    }

    private static XLWorkbook BuildListeningWorkbook()
    {
        var wb = new XLWorkbook();
        AddGuideSheet(wb, "Listening Group & Question Template", [
            ("ListeningGroups.GroupCode", "Required", "Mã nhóm nghe duy nhất (VD: L-N5-01)"),
            ("ListeningGroups.AudioUrl", "Required", "Bắt buộc trong file Excel: URL file .mp3/.wav/.m4a/.ogg đã upload."),
            ("ListeningGroups.MaxReplays", "Optional", "Số lần nghe tối đa (VD: 1 hoặc 2; bỏ trống = không giới hạn)"),
            ("ListeningQuestions.GroupCode", "Required", "Tham chiếu ListeningGroups.GroupCode"),
        ], ["SingleChoice", "VisualMultipleChoice"]);
        AddDataSheet(wb, "ListeningGroups", ["GroupCode", "LevelCode", "ListeningSection", "Title", "AudioUrl", "AudioDurationSeconds", "MaxReplays", "AllowPause", "Instructions", "Transcript", "Source"], [
            ["L-N5-01", "N5", "Mondai 1: Hiểu nhiệm vụ", "Hội thoại tại nhà ga", "", "45", "2", "TRUE", "Nghe đoạn hội thoại và chọn đáp án đúng.", "駅員：3番線から電車が出ます。", "Tài liệu tự biên"]
        ]);
        AddDataSheet(wb, "ListeningQuestions", ["GroupCode", "GroupSortOrder", "QuestionPrompt", "QuestionType", "Option1", "Option2", "Option3", "Option4", "Option1ImageUrl", "Option2ImageUrl", "Option3ImageUrl", "Option4ImageUrl", "CorrectAnswer", "Explanation"], [
            ["L-N5-01", "1", "電車は何番線から出ますか。", "SingleChoice", "1番線", "2番線", "3番線", "4番線", "", "", "", "", "3", "Trong đoạn thoại nhân viên nói 3番線から電車が出ます."]
        ]);
        return wb;
    }

    private static XLWorkbook BuildMixedPracticeWorkbook()
    {
        var wb = new XLWorkbook();
        AddGuideSheet(wb, "Mixed Practice Composition Template", [
            ("PracticeManifest", "Required", "Thông tin bộ đề luyện tập ghép nhiều kỹ năng"),
            ("QuestionSelection", "Required", "Chọn câu hỏi sẵn từ QuestionBankItemId hoặc thêm câu hỏi mới ở sheet Questions"),
        ], ["SingleChoice", "MultipleChoice", "FillBlank", "Ordering"]);
        AddDataSheet(wb, "PracticeManifest", ["Title", "LevelCode", "Description", "IncludedSkills", "TimeLimitMinutes", "ShowAnswersAfterSubmit", "ShowExplanationsAfterSubmit", "Source", "RightsStatus"], [
            ["Luyện tập tổng hợp N4 - Tuần 1", "N4", "Bộ đề ghép Từ vựng, Hán tự và Ngữ pháp", "Vocabulary,Kanji,Grammar", "20", "TRUE", "TRUE", "Biên soạn nội bộ", "Owned"]
        ]);
        AddDataSheet(wb, "QuestionSelection", ["SectionOrder", "SectionTitle", "SkillName", "DurationMinutes", "QuestionBankItemId", "QuestionCountFromBank"], [
            ["1", "Từ vựng & Hán tự", "Vocabulary", "10", "", "5"],
            ["2", "Ngữ pháp trọng tâm", "Grammar", "10", "", "5"]
        ]);
        return wb;
    }

    private static XLWorkbook BuildFullMockTestWorkbook()
    {
        var wb = new XLWorkbook();
        AddGuideSheet(wb, "Full Mock Test Exam Blueprint Template", [
            ("ExamBlueprint", "Required", "Khai báo đề thi thử JLPT hoàn chỉnh (Version, LevelCode, tổng thời lượng, chính sách chấm/xem kết quả)"),
            ("Sections", "Required", "Thứ tự phần thi, CertificateSectionCode chuẩn theo cấp độ và thời lượng từng phần"),
            ("Passages & Questions", "Optional/Required", "Nhóm đọc/nghe liên kết bằng PassageCode và danh sách câu hỏi"),
            ("Passages.AudioUrl (Listening)", "Required", "Mọi nhóm/đoạn Listening trong file Excel phải có AudioUrl hợp lệ."),
            ("Passages.ImageUrl", "Optional", "Có thể để trống nếu đoạn đọc/nghe không có hình ảnh."),
            ("Questions.ImageUrl", "Optional", "Có thể để trống nếu câu hỏi không có hình ảnh."),
        ], ["SingleChoice", "MultipleChoice", "FillBlank", "Ordering", "VisualMultipleChoice"]);
        AddDataSheet(wb, "ExamBlueprint", ["Title", "LevelCode", "ExamVersion", "Description", "TotalTimeLimitMinutes", "PassingRule", "ShowAnswersAfterSubmit", "Source", "RightsStatus"], [
            ["JLPT N4 Mock Exam Blueprint #1", "N4", "1", "Đề thi thử chuẩn cấu trúc JLPT N4", "125", "SectionAndTotalScore", "TRUE", "Nội bộ", "Owned"]
        ]);
        AddDataSheet(wb, "Sections", ["SectionOrder", "CertificateSectionCode", "Title", "SkillName", "DurationMinutes", "Instructions"], [
            ["1", "N4_VOCAB", "Từ vựng - Chữ viết", "Vocabulary", "25", "Chọn đáp án đúng nhất."],
            ["2", "N4_GRAMMAR", "Ngữ pháp", "Grammar", "55", "Hoàn thành các câu ngữ pháp."],
            ["3", "N4_READING", "Đọc hiểu", "Reading", "55", "Đọc đoạn văn và chọn đáp án đúng."],
            ["4", "N4_LISTENING", "Nghe hiểu", "Listening", "35", "Nghe file audio tối đa 2 lần."]
        ]);
        AddDataSheet(wb, "Passages", ["PassageCode", "SkillName", "Title", "PassageText", "AudioUrl", "AudioDurationSeconds", "MaxReplays", "Transcript"], [
            ["P-READ-1", "Reading", "Thư mời", "来月、大阪へ行きます。", "", "", "", ""],
            ["P-LIST-1", "Listening", "Nghe hiểu câu 1", "", "", "30", "1", "Audio sample transcript"]
        ]);
        AddDataSheet(wb, "Questions", ["SectionOrder", "PassageCode", "TemplateKind", "QuestionType", "QuestionPrompt", "Option1", "Option2", "Option3", "Option4", "CorrectAnswer", "Explanation", "Word", "Reading", "GrammarPattern", "OrderingTokens", "OrderingAnswer"], [
            ["1", "", "VocabReading", "SingleChoice", "「電車」の読み方はどれですか。", "でんしゃ", "てんしゃ", "でんじゃ", "てんじゃ", "1", "電車 = でんしゃ", "電車", "でんしゃ", "", "", ""]
        ]);
        return wb;
    }

    private static object BuildVocabularyJson() => new
    {
        schemaVersion = SchemaVersion,
        templateKind = "vocabulary",
        isSample = true,
        items = new[]
        {
            new
            {
                certificateLevelCode = "N5",
                skillName = "Vocabulary",
                templateKind = "VocabMeaning",
                questionType = "SingleChoice",
                questionText = "「毎日」の意味として正しいものはどれですか。",
                skillMetadata = new { word = "毎日", reading = "まいにち", meaning = "Mỗi ngày", exampleSentence = "毎日日本語を勉強します。" },
                options = new[]
                {
                    new { sortOrder = 1, optionText = "Mỗi ngày", isCorrect = true },
                    new { sortOrder = 2, optionText = "Mỗi tuần", isCorrect = false },
                    new { sortOrder = 3, optionText = "Hôm qua", isCorrect = false },
                    new { sortOrder = 4, optionText = "Ngày mai", isCorrect = false },
                },
                explanation = "毎日（まいにち）có nghĩa là mỗi ngày.",
                topic = "Thời gian",
                sourceType = "ImportJson"
            }
        }
    };

    private static object BuildKanjiJson() => new
    {
        schemaVersion = SchemaVersion,
        templateKind = "kanji",
        isSample = true,
        items = new[]
        {
            new
            {
                certificateLevelCode = "N5",
                skillName = "Kanji",
                templateKind = "KanjiReading",
                questionType = "SingleChoice",
                questionText = "次の文の「水」の読み方を選びなさい。：水を飲みます。",
                skillMetadata = new { kanjiCharacter = "水", meaning = "Nước", sinoVietnamese = "THỦY", onYomi = "スイ", kunYomi = "みず", strokeCount = 4 },
                options = new[]
                {
                    new { sortOrder = 1, optionText = "みず", isCorrect = true },
                    new { sortOrder = 2, optionText = "き", isCorrect = false },
                    new { sortOrder = 3, optionText = "ひ", isCorrect = false },
                    new { sortOrder = 4, optionText = "つち", isCorrect = false },
                },
                explanation = "Đứng một mình danh từ 水 đọc theo kun-yomi là みず."
            }
        }
    };

    private static object BuildGrammarJson() => new
    {
        schemaVersion = SchemaVersion,
        templateKind = "grammar",
        isSample = true,
        items = new[]
        {
            new
            {
                certificateLevelCode = "N4",
                skillName = "Grammar",
                templateKind = "GrammarFillBlank",
                questionType = "FillBlank",
                questionText = "日本語が上手に（　　）たいです。",
                skillMetadata = new { grammarPattern = "～たい", structure = "Vます + たい", acceptedAnswers = new[] { "なり" }, normalizeKana = true },
                explanation = "Vます + たい: 上手になりたいです."
            }
        }
    };

    private static object BuildReadingJson() => new
    {
        schemaVersion = SchemaVersion,
        templateKind = "reading",
        isSample = true,
        passages = new[]
        {
            new
            {
                passageCode = "READ-N4-1",
                certificateLevelCode = "N4",
                skillName = "Reading",
                title = "Thông báo nghỉ lễ",
                passageText = "明日は祝日なので、授業はありません。",
                questions = new[]
                {
                    new
                    {
                        questionText = "明日はどうなりますか。",
                        questionType = "SingleChoice",
                        options = new[]
                        {
                            new { sortOrder = 1, optionText = "Không có tiết học", isCorrect = true },
                            new { sortOrder = 2, optionText = "Kiểm tra cuối kỳ", isCorrect = false }
                        }
                    }
                }
            }
        }
    };

    private static object BuildListeningJson() => new
    {
        schemaVersion = SchemaVersion,
        templateKind = "listening",
        isSample = true,
        listeningGroups = new[]
        {
            new
            {
                groupCode = "LISTEN-N5-1",
                certificateLevelCode = "N5",
                skillName = "Listening",
                title = "Hội thoại ngắn 1",
                audioUrl = "",
                playbackPolicy = new { maxReplays = 2, allowPause = true, showTranscriptAfterSubmit = true },
                audioTranscript = "男：コーヒーを一つお願いします。",
                questions = new[]
                {
                    new
                    {
                        questionText = "男の人は何を注文しましたか。",
                        questionType = "SingleChoice",
                        options = new[]
                        {
                            new { sortOrder = 1, optionText = "Cà phê", isCorrect = true },
                            new { sortOrder = 2, optionText = "Trà xanh", isCorrect = false }
                        }
                    }
                }
            }
        }
    };

    private static object BuildMixedPracticeJson() => new
    {
        schemaVersion = SchemaVersion,
        templateKind = "mixed-practice",
        examKind = "MixedPractice",
        title = "Bộ đề luyện tập tổng hợp N4",
        certificateLevelCode = "N4",
        timeLimitMinutes = 25,
        includedSkills = new[] { "Vocabulary", "Grammar" },
        sections = new[]
        {
            new { sortOrder = 1, title = "Từ vựng", skillName = "Vocabulary", questionBankItemIds = Array.Empty<int>() },
            new { sortOrder = 2, title = "Ngữ pháp", skillName = "Grammar", questionBankItemIds = Array.Empty<int>() }
        }
    };

    private static object BuildFullMockTestJson() => new
    {
        schemaVersion = SchemaVersion,
        templateKind = "full-mock-test",
        examKind = "FullMockTest",
        title = "Đề thi thử JLPT N5 Blueprint v2",
        certificateLevelCode = "N5",
        version = 1,
        timeLimitMinutes = 90,
        sections = new[]
        {
            new { sortOrder = 1, certificateSectionCode = "N5_VOCAB", skillName = "Vocabulary", title = "Từ vựng", durationMinutes = 20 },
            new { sortOrder = 2, certificateSectionCode = "N5_GRAMMAR_READING", skillName = "Grammar", title = "Ngữ pháp", durationMinutes = 40 },
            new { sortOrder = 3, certificateSectionCode = "N5_GRAMMAR_READING", skillName = "Reading", title = "Đọc hiểu", durationMinutes = 0 },
            new { sortOrder = 4, certificateSectionCode = "N5_LISTENING", skillName = "Listening", title = "Nghe hiểu", durationMinutes = 30 }
        }
    };
}
