using System.Text.Json;
using RikiPath.Application.Responses.LearningPaths;
using RikiPath.Application.Responses.PlacementTests;
using RikiPath.Domain.Entities;

namespace RikiPath.Application.Services;

public partial class LearningPathService
{
    private static List<LearningPathStudyTimeScenario> BuildStudyTimeScenarios(int requiredMinutes, int? availableDays,
        int minimum, int maximum, bool hasCompleteEvidence)
    {
        var candidates = new SortedSet<int> { minimum, maximum, 15, 30, 45, 60, 90, 120, 180, 240, 360, 480 };
        if (availableDays > 0)
        {
            var needed = (int)Math.Ceiling(requiredMinutes / (double)availableDays.Value / 5) * 5;
            if (needed is >= 15 and <= 480) candidates.Add(needed);
        }
        return candidates.Select(minutes =>
        {
            var days = (int)Math.Ceiling(requiredMinutes / (double)minutes);
            bool? fits = availableDays.HasValue
                ? days > availableDays.Value ? false : hasCompleteEvidence ? true : null : null;
            return new LearningPathStudyTimeScenario
            {
                MinutesPerDay = minutes, EstimatedStudyDays = days, CanMeetRequestedExamDate = fits,
                WithinRequestedRange = minutes >= minimum && minutes <= maximum,
                Comment = fits == true ? "Thời lượng có thể phù hợp theo ước tính nếu duy trì đều và tiến bộ như dự kiến; không bảo đảm đỗ."
                    : fits == false ? "Thời lượng chưa đủ theo ước tính trước mốc nghỉ ba ngày."
                    : "Chưa đủ dữ liệu hoặc chưa có ngày thi để kết luận khả năng kịp lịch."
            };
        }).ToList();
    }

    private static string BuildStudyTimeAdvice(List<LearningPathStudyTimeScenario> scenarios, int minimum, int maximum, int? suggested)
    {
        if (!scenarios.Any(x => x.CanMeetRequestedExamDate.HasValue))
            return "Cần ngày thi và bằng chứng của đủ bốn kỹ năng để so sánh thời lượng. Giữ kế hoạch trong khoảng phút đã chọn.";
        if (scenarios.Single(x => x.MinutesPerDay == minimum).CanMeetRequestedExamDate == true)
            return $"Mức {minimum} phút/ngày có thể đủ về thời gian theo ước tính; phần tăng đến {maximum} phút là tùy chọn. Kết quả thi còn phụ thuộc chất lượng ôn luyện và tiến bộ thực tế.";
        return suggested.HasValue
            ? $"Mức {minimum} phút/ngày chưa đủ theo ước tính. Nếu có thể duy trì khoảng {suggested} phút/ngày, mục tiêu có thể khả thi hơn trong quỹ thời gian còn lại. " +
              (suggested > maximum ? "Mức này vượt khoảng thời gian đã chọn; chỉ áp dụng khi bạn đồng ý điều chỉnh. " : "Mức này nằm trong khoảng thời gian bạn đã chọn. ") +
              "Không thể khẳng định chắc chắn đỗ chỉ bằng cách tăng số phút học."
            : "Theo dữ liệu hiện tại chưa tìm được mức học tối đa 480 phút/ngày có đủ cơ sở để kết luận kịp lịch. Cần bổ sung đánh giá hoặc cân nhắc ngày thi/mục tiêu khác.";
    }

    private static string OfficialExamStructure(string targetCode)
        => targetCode is "N2" or "N3" or "N1"
            ? "Ôn theo bốn kỹ năng: từ vựng (bao gồm cách đọc Kanji), ngữ pháp, đọc hiểu, nghe hiểu. Điểm JLPT thật gộp từ vựng/ngữ pháp, tách đọc và nghe; phải đạt cả tổng điểm và ngưỡng từng phần. Điểm Placement không phải điểm quy đổi JLPT."
            : "Ôn theo bốn kỹ năng: từ vựng (bao gồm cách đọc Kanji), ngữ pháp, đọc hiểu, nghe hiểu. JLPT N4/N5 gộp điểm kiến thức ngôn ngữ và đọc, tách nghe; phải đạt tổng điểm và ngưỡng từng phần. Điểm Placement không phải điểm quy đổi JLPT.";

    private static string SerializeLessonContent(Lesson lesson)
        => JsonSerializer.Serialize(new
        {
            kanji = (lesson.LessonKanjis ?? []).Where(x => !x.Kanji.IsDeleted)
                .Select(x => new { x.Kanji.Id, x.Kanji.Character, x.Kanji.Meaning, x.Kanji.SinoVietnamese,
                    x.Kanji.OnYomi, x.Kanji.KunYomi, x.Kanji.StrokeCount }),
            vocabulary = (lesson.LessonVocabularies ?? []).Where(x => !x.Vocabulary.IsDeleted)
                .Select(x => new { x.Vocabulary.Id, x.Vocabulary.Word, x.Vocabulary.Reading, x.Vocabulary.Meaning,
                    x.Vocabulary.ExampleSentence, x.Vocabulary.ExampleSentenceMeaning }),
            grammar = (lesson.LessonGrammars ?? []).Where(x => !x.GrammarPattern.IsDeleted)
                .Select(x => new { x.GrammarPattern.Id, x.GrammarPattern.Title, x.GrammarPattern.Structure,
                    x.GrammarPattern.UsageNotes, x.GrammarPattern.ExampleSentence, x.GrammarPattern.ExampleSentenceMeaning })
        }, StoredPathJsonOptions);

    private static List<LearningPathPhaseResponse> BuildPhases(DateTime today, int days)
    {
        var result = new List<LearningPathPhaseResponse>();
        void Add(string title, string focus, int start, int end)
        {
            if (end < start) return;
            result.Add(new LearningPathPhaseResponse
            {
                Title = title, Focus = focus, StartDay = start, EndDay = end,
                StartDate = today.AddDays(start - 1), EndDate = today.AddDays(end - 1)
            });
        }
        var reviewDays = Math.Min(7, Math.Max(1, days / 4));
        var foundationEnd = Math.Min(30, days - reviewDays);
        Add("Củng cố nền tảng và khắc phục phần yếu", "Ưu tiên những phần có điểm thấp trong bài đánh giá năng lực gần nhất.", 1, foundationEnd);
        Add("Phát triển kỹ năng và vận dụng", "Nâng dần độ khó, kết hợp ôn lặp lại Kanji, từ vựng, ngữ pháp với đọc và nghe.", foundationEnd + 1, days - reviewDays);
        Add("Tổng ôn trước kỳ thi", "Ôn lỗi thường gặp, luyện đề có giới hạn thời gian và giữ nhịp học vừa sức.", days - reviewDays + 1, days);
        return result;
    }
}
