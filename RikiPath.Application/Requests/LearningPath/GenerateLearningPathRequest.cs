namespace RikiPath.Application.Requests.LearningPath
{
    public class GenerateLearningPathRequest
    {
        public bool IsBeginner { get; set; } = true;
        public string? TargetCertification { get; set; }

        /// <summary>
        /// Số ngày của lộ trình học (Mặc định 7 ngày)
        /// </summary>
        public int DurationDays { get; set; } = 7;

        // Các tùy chọn linh hoạt (Chọn hoặc không chọn)
        public bool IncludeVocabulary { get; set; } = false;
        public bool IncludeKanji { get; set; } = false;
        public bool IncludeGrammar { get; set; } = false;
    }
}
