using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using RikiPath.Application.DTOs.ContentAuthoring;
using RikiPath.Application.Helpers;

namespace RikiPath.Application.Services.ContentAuthoring;

/// <summary>Bộ kiểm tra hợp lệ chuẩn hóa dùng chung cho Biên soạn thủ công, Trợ lý AI, Nhập liệu đa định dạng, Exam Builder và Kiểm duyệt.</summary>
public static partial class CanonicalQuestionValidator
{
    public static readonly HashSet<string> SupportedJlptLevelCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        "N5", "N4", "N3", "N2"
    };

    private static readonly char[] FormulaTriggerChars = ['=', '+', '-', '@', '\t', '\r'];

    [GeneratedRegex(@"\s+")]
    private static partial Regex MultiSpaceRegex();

    [GeneratedRegex(@"[\u3040-\u309F\u30A0-\u30FF]")]
    private static partial Regex KanaRegex();

    [GeneratedRegex(@"[\u4E00-\u9FFF]")]
    private static partial Regex KanjiRegex();

    [GeneratedRegex(@"[\u3040-\u30FF\u4E00-\u9FFF]")]
    private static partial Regex JapaneseRegex();

    public static CanonicalValidationResult ValidateQuestion(
        CanonicalQuestionDto question,
        bool forSubmitOrPublish,
        IReadOnlySet<string>? dynamicAllowedLevels = null,
        int? rowNumber = null)
    {
        var result = new CanonicalValidationResult();
        var allowedLevels = dynamicAllowedLevels ?? SupportedJlptLevelCodes;

        var level = (question.CertificateLevelCode ?? string.Empty).Trim().ToUpperInvariant();
        if (string.IsNullOrEmpty(level) && question.CertificateLevelId <= 0)
        {
            result.Errors.Add(Issue("certificateLevelCode", rowNumber, "Error", "LEVEL_REQUIRED",
                "Thiếu cấp độ JLPT (hỗ trợ N5, N4, N3, N2)."));
        }
        else if (!string.IsNullOrEmpty(level) && !allowedLevels.Contains(level))
        {
            result.Errors.Add(Issue("certificateLevelCode", rowNumber, "Error", "LEVEL_UNSUPPORTED",
                $"Cấp độ '{level}' chưa được hỗ trợ trong hệ thống."));
        }

        var skill = NormalizeSkillName(question.SkillName);
        if (string.IsNullOrEmpty(skill) && question.LanguageSkillId <= 0)
        {
            result.Errors.Add(Issue("skillName", rowNumber, "Error", "SKILL_REQUIRED",
                "Thiếu kỹ năng (Vocabulary, Kanji, Grammar, Reading hoặc Listening)."));
        }

        var questionType = NormalizeQuestionType(question.QuestionType);
        if (string.IsNullOrEmpty(questionType) || !CanonicalQuestionTypes.Supported.Contains(questionType))
        {
            result.Errors.Add(Issue("questionType", rowNumber, "Error", "QUESTION_TYPE_UNSUPPORTED",
                $"Loại câu hỏi '{question.QuestionType}' chưa được hỗ trợ chấm điểm đầy đủ."));
            result.CanSaveDraft = false;
            result.CanPublish = false;
            return result;
        }
        question.QuestionType = questionType;

        if (string.IsNullOrWhiteSpace(question.TemplateKind))
        {
            question.TemplateKind = InferDefaultTemplateKind(skill, questionType);
        }
        else if (!SkillTemplateKinds.All.Contains(question.TemplateKind.Trim()))
        {
            result.Errors.Add(Issue("templateKind", rowNumber, "Error", "TEMPLATE_KIND_INVALID",
                $"Mẫu nội dung '{question.TemplateKind}' không hợp lệ."));
        }

        if (string.IsNullOrWhiteSpace(question.QuestionText))
        {
            result.Errors.Add(Issue("questionText", rowNumber, "Error", "QUESTION_TEXT_REQUIRED",
                "Nội dung câu hỏi (QuestionPrompt) không được để trống."));
        }

        ValidateAnswerStructure(question, questionType, result, rowNumber);
        ValidateSkillSpecificRules(question, skill, forSubmitOrPublish, result, rowNumber);
        ValidateMediaUrls(question, result, rowNumber);

        if (forSubmitOrPublish && JlptBlueprintPolicy.IsRightsBlocked(question.RightsStatus))
        {
            result.Errors.Add(Issue(
                "rightsStatus",
                rowNumber,
                "Error",
                "PUBLISH_BLOCKED_RIGHTS",
                $"Câu hỏi có trạng thái bản quyền '{JlptBlueprintPolicy.NormalizeRightsStatus(question.RightsStatus)}' nên chưa thể gửi duyệt/xuất bản."));
        }

        result.ContentFingerprint = ComputeQuestionFingerprint(question);
        result.CanSaveDraft = result.Errors.Count == 0;
        result.CanPublish = result.Errors.Count == 0 && !result.IncompleteMedia;
        return result;
    }

    private static void ValidateAnswerStructure(
        CanonicalQuestionDto question,
        string questionType,
        CanonicalValidationResult result,
        int? rowNumber)
    {
        var meta = question.SkillMetadata;

        if (string.Equals(questionType, CanonicalQuestionTypes.Ordering, StringComparison.OrdinalIgnoreCase))
        {
            var tokens = meta?.OrderingTokens?.Where(t => !string.IsNullOrWhiteSpace(t)).Select(t => t.Trim()).ToArray() ?? [];
            var answer = meta?.OrderingAnswer?.Where(t => !string.IsNullOrWhiteSpace(t)).Select(t => t.Trim()).ToArray() ?? [];
            if (tokens.Length < 2)
            {
                result.Errors.Add(Issue("orderingTokens", rowNumber, "Error", "ORDERING_TOKENS_INVALID",
                    "Câu sắp xếp câu (Sentence Arrangement) cần ít nhất 2 cụm từ."));
                return;
            }
            if (answer.Length != tokens.Length || !SentenceOrdering.IsSubsequenceOfChoices(tokens, answer))
            {
                result.Errors.Add(Issue("orderingAnswer", rowNumber, "Error", "ORDERING_ANSWER_INVALID",
                    "Thứ tự đáp án sắp xếp phải sử dụng đúng tập các cụm từ đã khai báo."));
            }
            return;
        }

        if (string.Equals(questionType, CanonicalQuestionTypes.FillBlank, StringComparison.OrdinalIgnoreCase))
        {
            var accepted = meta?.AcceptedAnswers?.Where(a => !string.IsNullOrWhiteSpace(a)).Select(a => a.Trim()).ToList() ?? [];
            if (accepted.Count == 0)
            {
                // Cho phép suy ra từ Options đánh dấu đúng nếu tác giả nhập qua danh sách phương án
                accepted = question.Options
                    .Where(o => o.IsCorrect && !string.IsNullOrWhiteSpace(o.OptionText))
                    .Select(o => o.OptionText.Trim())
                    .Distinct(StringComparer.Ordinal)
                    .ToList();
                if (accepted.Count > 0)
                {
                    question.SkillMetadata ??= new CanonicalSkillMetadataDto();
                    question.SkillMetadata.AcceptedAnswers = accepted;
                }
            }

            if (accepted.Count == 0)
            {
                result.Errors.Add(Issue("acceptedAnswers", rowNumber, "Error", "FILL_BLANK_ANSWER_REQUIRED",
                    "Câu điền từ (Fill in the Blank) cần ít nhất 1 đáp án được chấp nhận."));
            }
            return;
        }

        var options = (question.Options ?? [])
            .Where(o => !string.IsNullOrWhiteSpace(o.OptionText) || !string.IsNullOrWhiteSpace(o.ImageUrl))
            .ToList();

        if (options.Count < 2)
        {
            result.Errors.Add(Issue("options", rowNumber, "Error", "OPTIONS_TOO_FEW",
                "Câu trắc nghiệm cần tối thiểu 2 lựa chọn hợp lệ."));
            return;
        }

        if (string.Equals(questionType, CanonicalQuestionTypes.VisualMultipleChoice, StringComparison.OrdinalIgnoreCase) &&
            options.Any(o => string.IsNullOrWhiteSpace(o.ImageUrl)))
        {
            result.Errors.Add(Issue("options", rowNumber, "Error", "VISUAL_OPTION_IMAGE_REQUIRED",
                "Câu trắc nghiệm hình ảnh yêu cầu mỗi lựa chọn phải có URL ảnh hợp lệ."));
        }

        var seenTexts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var opt in options)
        {
            var textKey = NormalizeText(opt.OptionText);
            if (!string.IsNullOrEmpty(textKey) && !seenTexts.Add(textKey))
            {
                result.Warnings.Add(Issue("options", rowNumber, "Warning", "DUPLICATE_OPTION",
                    $"Có lựa chọn bị trùng nội dung: '{opt.OptionText}'."));
            }
            if (!string.IsNullOrWhiteSpace(opt.ImageUrl) && !AllowedMediaUrlPolicy.IsApprovedMediaUrl(opt.ImageUrl))
            {
                result.Errors.Add(Issue("options.imageUrl", rowNumber, "Error", "OPTION_IMAGE_URL_INVALID",
                    $"URL hình ảnh của đáp án '{opt.OptionText}' không thuộc nguồn storage hợp lệ."));
            }
        }

        var correctCount = options.Count(o => o.IsCorrect);
        if (string.Equals(questionType, CanonicalQuestionTypes.MultipleChoice, StringComparison.OrdinalIgnoreCase))
        {
            if (correctCount < 1)
            {
                result.Errors.Add(Issue("options", rowNumber, "Error", "MULTIPLE_CHOICE_NO_CORRECT",
                    "Câu chọn nhiều đáp án phải có ít nhất 1 đáp án đúng."));
            }
        }
        else if (correctCount != 1)
        {
            result.Errors.Add(Issue("options", rowNumber, "Error", "SINGLE_CHOICE_CORRECT_COUNT",
                $"Câu trắc nghiệm đơn phải có đúng 1 đáp án đúng (hiện có {correctCount})."));
        }
    }

    private static void ValidateSkillSpecificRules(
        CanonicalQuestionDto question,
        string skill,
        bool forSubmitOrPublish,
        CanonicalValidationResult result,
        int? rowNumber)
    {
        var meta = question.SkillMetadata;

        switch (skill)
        {
            case "Vocabulary":
                if (string.Equals(question.TemplateKind, SkillTemplateKinds.VocabReading, StringComparison.OrdinalIgnoreCase) &&
                    meta is not null && !string.IsNullOrWhiteSpace(meta.Reading) && !KanaRegex().IsMatch(meta.Reading))
                {
                    result.Errors.Add(Issue("skillMetadata.reading", rowNumber, "Error", "VOCAB_READING_NOT_KANA",
                        "Cách đọc từ vựng (Reading/Kana) phải chứa ký tự Hiragana hoặc Katakana."));
                }
                if (meta is not null && string.IsNullOrWhiteSpace(meta.Word) &&
                    (string.Equals(question.TemplateKind, SkillTemplateKinds.VocabMeaning, StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(question.TemplateKind, SkillTemplateKinds.VocabReading, StringComparison.OrdinalIgnoreCase)))
                {
                    result.Warnings.Add(Issue("skillMetadata.word", rowNumber, "Warning", "VOCAB_WORD_MISSING",
                        "Nên điền từ vựng gốc (Word/Phrase) để liên kết dữ liệu ngân hàng câu hỏi."));
                }
                break;

            case "Kanji":
                if (meta is not null && !string.IsNullOrWhiteSpace(meta.KanjiCharacter) && !KanjiRegex().IsMatch(meta.KanjiCharacter))
                {
                    result.Errors.Add(Issue("skillMetadata.kanjiCharacter", rowNumber, "Error", "KANJI_CHAR_INVALID",
                        "Trường Kanji phải chứa ký tự Hán tự hợp lệ."));
                }
                if (meta?.StrokeCount is <= 0 or > 40)
                {
                    result.Errors.Add(Issue("skillMetadata.strokeCount", rowNumber, "Error", "KANJI_STROKES_INVALID",
                        "Số nét Kanji phải trong khoảng 1–40."));
                }
                break;

            case "Grammar":
                if (meta is not null && string.IsNullOrWhiteSpace(meta.GrammarPattern))
                {
                    result.Warnings.Add(Issue("skillMetadata.grammarPattern", rowNumber, "Warning", "GRAMMAR_PATTERN_RECOMMENDED",
                        "Nên ghi rõ mẫu ngữ pháp (Grammar Pattern) để phân loại chính xác."));
                }
                break;

            case "Reading":
                var hasPassageText = !string.IsNullOrWhiteSpace(question.Passage?.PassageText);
                if (!question.PassageId.HasValue && !hasPassageText)
                {
                    result.Errors.Add(Issue("passage", rowNumber, "Error", "READING_PASSAGE_REQUIRED",
                        "Câu hỏi Đọc hiểu phải liên kết tới một đoạn đọc (Reading Passage) hoặc nhóm câu hỏi đọc."));
                }
                break;

            case "Listening":
                var effectiveAudio = !string.IsNullOrWhiteSpace(question.AudioUrl)
                    ? question.AudioUrl
                    : question.Passage?.AudioUrl;
                if (string.IsNullOrWhiteSpace(effectiveAudio))
                {
                    result.IncompleteMedia = true;
                    if (forSubmitOrPublish)
                    {
                        result.Errors.Add(Issue("audioUrl", rowNumber, "Error", "LISTENING_AUDIO_REQUIRED",
                            "Câu hỏi Nghe hiểu bắt buộc có tệp âm thanh hợp lệ trước khi gửi duyệt hoặc xuất bản."));
                    }
                    else
                    {
                        result.Warnings.Add(Issue("audioUrl", rowNumber, "Warning", "MISSING_LISTENING_AUDIO",
                            "Câu Nghe hiểu chưa có file âm thanh — chỉ được lưu Nháp chưa hoàn chỉnh, không thể gửi duyệt hoặc xuất bản."));
                    }
                }
                else if (!AllowedMediaUrlPolicy.IsApprovedMediaUrl(effectiveAudio) ||
                         !AllowedMediaUrlPolicy.HasSupportedAudioExtension(effectiveAudio))
                {
                    result.Errors.Add(Issue("audioUrl", rowNumber, "Error", "LISTENING_AUDIO_URL_INVALID",
                        "AudioUrl của câu Nghe hiểu phải là URL media hợp lệ (.mp3, .wav, .m4a, .ogg)."));
                }

                if (question.PlaybackPolicy is { MaxReplays: <= 0 })
                {
                    result.Errors.Add(Issue("playbackPolicy.maxReplays", rowNumber, "Error", "PLAYBACK_REPLAYS_INVALID",
                        "Số lần phát tối đa (MaxReplays) phải >= 1 (hoặc để trống nếu không giới hạn)."));
                }
                break;
        }
    }

    private static void ValidateMediaUrls(
        CanonicalQuestionDto question,
        CanonicalValidationResult result,
        int? rowNumber)
    {
        if (!string.IsNullOrWhiteSpace(question.ImageUrl) && !AllowedMediaUrlPolicy.IsApprovedMediaUrl(question.ImageUrl))
        {
            result.Errors.Add(Issue("imageUrl", rowNumber, "Error", "IMAGE_URL_INVALID",
                "ImageUrl phải trỏ tới file ảnh đã lưu trên storage hợp lệ."));
        }
        if (!string.IsNullOrWhiteSpace(question.AudioUrl) && !AllowedMediaUrlPolicy.IsApprovedMediaUrl(question.AudioUrl))
        {
            result.Errors.Add(Issue("audioUrl", rowNumber, "Error", "AUDIO_URL_INVALID",
                "AudioUrl phải trỏ tới file âm thanh đã lưu trên storage hợp lệ."));
        }
    }

    public static CanonicalValidationResult ValidatePassage(CanonicalPassageDto passage, bool forSubmitOrPublish)
    {
        var result = new CanonicalValidationResult();
        if (string.IsNullOrWhiteSpace(passage.Title))
            result.Errors.Add(Issue("title", null, "Error", "PASSAGE_TITLE_REQUIRED", "Tiêu đề nhóm/đoạn không được để trống."));
        if (string.IsNullOrWhiteSpace(passage.CertificateLevelCode) && passage.CertificateLevelId <= 0)
            result.Errors.Add(Issue("certificateLevelCode", null, "Error", "LEVEL_REQUIRED", "Thiếu cấp độ JLPT."));

        var skill = NormalizeSkillName(passage.SkillName);
        if (skill == "Reading")
        {
            if (string.IsNullOrWhiteSpace(passage.PassageText))
                result.Errors.Add(Issue("passageText", null, "Error", "PASSAGE_TEXT_REQUIRED", "Nội dung đoạn đọc không được để trống."));
        }
        else if (skill == "Listening")
        {
            if (string.IsNullOrWhiteSpace(passage.AudioUrl))
            {
                result.IncompleteMedia = true;
                if (forSubmitOrPublish)
                    result.Errors.Add(Issue("audioUrl", null, "Error", "LISTENING_AUDIO_REQUIRED", "Nhóm Nghe hiểu cần file audio hợp lệ trước khi gửi duyệt hoặc xuất bản."));
                else
                    result.Warnings.Add(Issue("audioUrl", null, "Warning", "MISSING_LISTENING_AUDIO", "Nhóm Nghe hiểu chưa có audio — chỉ được lưu Nháp chưa hoàn chỉnh."));
            }
            else if (!AllowedMediaUrlPolicy.IsApprovedMediaUrl(passage.AudioUrl) ||
                     !AllowedMediaUrlPolicy.HasSupportedAudioExtension(passage.AudioUrl))
            {
                result.Errors.Add(Issue("audioUrl", null, "Error", "LISTENING_AUDIO_URL_INVALID", "AudioUrl không hợp lệ."));
            }
        }
        else
        {
            result.Errors.Add(Issue("skillName", null, "Error", "PASSAGE_SKILL_INVALID", "Nhóm/Đoạn dùng chung chỉ áp dụng cho Đọc hiểu hoặc Nghe hiểu."));
        }

        if (!string.IsNullOrWhiteSpace(passage.ImageUrl) && !AllowedMediaUrlPolicy.IsApprovedMediaUrl(passage.ImageUrl))
            result.Errors.Add(Issue("imageUrl", null, "Error", "IMAGE_URL_INVALID", "ImageUrl không hợp lệ."));

        result.ContentFingerprint = ComputePassageFingerprint(passage);
        result.CanSaveDraft = result.Errors.Count == 0;
        result.CanPublish = result.Errors.Count == 0 && !result.IncompleteMedia;
        return result;
    }

    public static string ComputeQuestionFingerprint(CanonicalQuestionDto q)
    {
        var skill = NormalizeSkillName(q.SkillName);
        var level = (q.CertificateLevelCode ?? string.Empty).Trim().ToUpperInvariant();
        var stem = NormalizeText(q.QuestionText);
        var options = string.Join("|", (q.Options ?? [])
            .Select(o => NormalizeText(o.OptionText))
            .Where(s => s.Length > 0)
            .OrderBy(s => s, StringComparer.Ordinal));
        var extra = q.QuestionType switch
        {
            CanonicalQuestionTypes.FillBlank => string.Join("|", (q.SkillMetadata?.AcceptedAnswers ?? []).Select(NormalizeText)),
            CanonicalQuestionTypes.Ordering => string.Join("|", (q.SkillMetadata?.OrderingTokens ?? []).Select(NormalizeText)),
            _ => options
        };
        var raw = $"{level}::{skill}::{q.QuestionType}::{stem}::{extra}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw)));
    }

    public static string ComputePassageFingerprint(CanonicalPassageDto p)
    {
        var raw = $"{p.CertificateLevelCode?.Trim().ToUpperInvariant()}::{NormalizeSkillName(p.SkillName)}::{NormalizeText(p.Title)}::{NormalizeText(p.PassageText ?? p.AudioUrl)}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw)));
    }

    public static double ComputeSimilarityScore(string a, string b)
    {
        var tokensA = Tokenize(a);
        var tokensB = Tokenize(b);
        if (tokensA.Count == 0 || tokensB.Count == 0) return 0;
        var intersection = tokensA.Count(tokensB.Contains);
        var union = tokensA.Count + tokensB.Count - intersection;
        return union == 0 ? 0 : Math.Round((double)intersection / union, 3);
    }

    public static string NormalizeText(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;
        return MultiSpaceRegex().Replace(input.Trim(), " ").ToLowerInvariant();
    }

    public static string NormalizeSkillName(string? skillName)
    {
        var clean = (skillName ?? string.Empty).Trim();
        if (clean.Contains("vocab", StringComparison.OrdinalIgnoreCase) || clean.Contains("từ vựng", StringComparison.OrdinalIgnoreCase))
            return "Vocabulary";
        if (clean.Contains("kanji", StringComparison.OrdinalIgnoreCase) || clean.Contains("hán", StringComparison.OrdinalIgnoreCase))
            return "Kanji";
        if (clean.Contains("gram", StringComparison.OrdinalIgnoreCase) || clean.Contains("ngữ pháp", StringComparison.OrdinalIgnoreCase))
            return "Grammar";
        if (clean.Contains("read", StringComparison.OrdinalIgnoreCase) || clean.Contains("đọc", StringComparison.OrdinalIgnoreCase))
            return "Reading";
        if (clean.Contains("listen", StringComparison.OrdinalIgnoreCase) || clean.Contains("nghe", StringComparison.OrdinalIgnoreCase))
            return "Listening";
        return clean;
    }

    public static string NormalizeQuestionType(string? raw)
    {
        var clean = (raw ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(clean)) return CanonicalQuestionTypes.SingleChoice;
        if (clean.Equals("MultipleChoice", StringComparison.OrdinalIgnoreCase) ||
            clean.Equals("MCQ", StringComparison.OrdinalIgnoreCase))
            return CanonicalQuestionTypes.SingleChoice; // JLPT default single correct option unless multiple explicitly chosen
        if (clean.Equals("MultiSelect", StringComparison.OrdinalIgnoreCase) ||
            clean.Equals("MultipleSelect", StringComparison.OrdinalIgnoreCase))
            return CanonicalQuestionTypes.MultipleChoice;
        if (clean.Equals("SentenceArrangement", StringComparison.OrdinalIgnoreCase) ||
            clean.Equals("SentenceOrdering", StringComparison.OrdinalIgnoreCase) ||
            clean.Equals("Ordering", StringComparison.OrdinalIgnoreCase))
            return CanonicalQuestionTypes.Ordering;
        if (clean.Equals("FillInTheBlank", StringComparison.OrdinalIgnoreCase) ||
            clean.Equals("FillBlank", StringComparison.OrdinalIgnoreCase))
            return CanonicalQuestionTypes.FillBlank;
        if (clean.Equals("VisualMultipleChoice", StringComparison.OrdinalIgnoreCase))
            return CanonicalQuestionTypes.VisualMultipleChoice;
        return CanonicalQuestionTypes.Supported.FirstOrDefault(x => x.Equals(clean, StringComparison.OrdinalIgnoreCase)) ?? clean;
    }

    public static string InferDefaultTemplateKind(string skill, string questionType) => (skill, questionType) switch
    {
        ("Vocabulary", CanonicalQuestionTypes.FillBlank) => SkillTemplateKinds.VocabContextFill,
        ("Vocabulary", _) => SkillTemplateKinds.VocabMeaning,
        ("Kanji", _) => SkillTemplateKinds.KanjiReading,
        ("Grammar", CanonicalQuestionTypes.Ordering) => SkillTemplateKinds.GrammarOrdering,
        ("Grammar", CanonicalQuestionTypes.FillBlank) => SkillTemplateKinds.GrammarFillBlank,
        ("Grammar", _) => SkillTemplateKinds.GrammarMultipleChoice,
        ("Reading", _) => SkillTemplateKinds.ReadingPassageMcq,
        ("Listening", CanonicalQuestionTypes.VisualMultipleChoice) => SkillTemplateKinds.ListeningVisualMcq,
        ("Listening", _) => SkillTemplateKinds.ListeningAudioMcq,
        _ => SkillTemplateKinds.VocabMeaning
    };

    /// <summary>Chống lỗi Formula Injection khi xuất CSV/Excel.</summary>
    public static string SanitizeSpreadsheetCell(string? value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        var trimmedStart = value.TrimStart();
        if (trimmedStart.Length > 0 && FormulaTriggerChars.Contains(trimmedStart[0]))
            return "'" + value;
        return value;
    }

    public static bool ContainsJapaneseText(string? value) => !string.IsNullOrWhiteSpace(value) && JapaneseRegex().IsMatch(value);

    private static HashSet<string> Tokenize(string value)
    {
        var norm = NormalizeText(value);
        if (string.IsNullOrEmpty(norm)) return [];
        var set = new HashSet<string>(norm.Split([' ', '、', '。', '，', ',', '.', '?', '？', '!', '！', '(', ')', '「', '」'], StringSplitOptions.RemoveEmptyEntries), StringComparer.Ordinal);
        // Thêm bigram ký tự tiếng Nhật để bắt trùng câu không có khoảng trắng
        for (var i = 0; i < norm.Length - 1; i++)
        {
            if (!char.IsWhiteSpace(norm[i]) && !char.IsWhiteSpace(norm[i + 1]))
                set.Add(norm.Substring(i, 2));
        }
        return set;
    }

    private static CanonicalValidationIssue Issue(string field, int? rowNumber, string severity, string code, string message)
        => new(field, rowNumber, severity, code, message);
}
