namespace RikiPath.Application.Responses.LearningPaths
{
    public class LearningPathResponse
    {
        public int PathId { get; set; }
        public int Version { get; set; }
        public string Status { get; set; } = "ACTIVE";
        public string Horizon { get; set; } = "long_term";
        public int PlanDurationDays { get; set; }
        public DateTime? TargetExamDate { get; set; }
        public int? DaysUntilExam { get; set; }
        public bool? HasTargetExamDate { get; set; }
        public int? StartingJlptLevelId { get; set; }
        public int? TargetJlptLevelId { get; set; }
        public int? BaseActivePathId { get; set; }
        public DateTime? PreviewExpiresAt { get; set; }
        public DateTime? ConfirmedAt { get; set; }
        public int UserId { get; set; }
        public DateTime GeneratedAt { get; set; }
        public string Summary { get; set; } = string.Empty;
        public List<string> Strengths { get; set; } = new();
        public List<string> Weaknesses { get; set; } = new();
        public string Feasibility { get; set; } = string.Empty;
        public int? PlacementAttemptId { get; set; }
        public DateTime? PlacementSubmittedAt { get; set; }
        public string Readiness { get; set; } = string.Empty;
        public int DailyStudyMinutes { get; set; }
        public int DailyStudyMinutesMin { get; set; }
        public int DailyStudyMinutesMax { get; set; }
        public int? CertificateTypeId { get; set; }
        public string CertificateTypeName { get; set; } = "JLPT";
        public int? TargetCertificateLevelId { get; set; }
        public int TotalStudyDays { get; set; }
        public DateTime? DetailedPlanEndDate { get; set; }
        public int? SuggestedDailyStudyMinutes { get; set; }
        public bool RequiresMoreDailyStudyTime { get; set; }
        public List<LearningPathStudyTimeScenario> StudyTimeScenarios { get; set; } = new();
        public string StudyTimeAdvice { get; set; } = string.Empty;
        public string OfficialExamStructureNote { get; set; } = string.Empty;
        public int? AvailableStudyDays { get; set; }
        public DateTime? PlanningExamDate { get; set; }
        public bool IsAlternativePlan { get; set; }
        /// <summary>Exclusive study deadline: the first of three rest days before the planned exam.</summary>
        public DateTime? StudyDeadline { get; set; }
        public DateTime? LastStudyDate { get; set; }
        public DateTime? RestStartDate { get; set; }
        public List<LearningPathSectionAssessmentResponse> SectionAssessments { get; set; } = new();
        public List<LearningPathPhaseResponse> Phases { get; set; } = new();
        public List<string> ContentCoverageWarnings { get; set; } = new();
        public string EstimationNote { get; set; } = "Thời lượng là ước tính lập kế hoạch từ bài đánh giá và thời gian học mỗi ngày, không phải cam kết đỗ hoặc xác suất đỗ JLPT.";

        /// <summary>true = ngày thi có vẻ khả thi; false = khuyến nghị dời lịch; null = chưa đặt ngày thi.</summary>
        public bool? IsExamDateRealistic { get; set; }

        /// <summary>Số ngày ôn tối thiểu hệ thống ước tính theo năng lực hiện tại.</summary>
        public int RecommendedStudyDays { get; set; }

        /// <summary>Ngày thi gợi ý. Chỉ có giá trị khi IsExamDateRealistic = false.</summary>
        public DateTime? SuggestedExamDate { get; set; }

        public List<string> FocusTopics { get; set; } = new();
        public List<DailyScheduleResponse> DailySchedules { get; set; } = new();
    }
}
