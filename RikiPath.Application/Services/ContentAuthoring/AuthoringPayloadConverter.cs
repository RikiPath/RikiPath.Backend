using System.Text.Json;
using RikiPath.Application.DTOs.ContentAuthoring;
using RikiPath.Application.Responses.Content;
using RikiPath.Domain.Entities;

namespace RikiPath.Application.Services.ContentAuthoring;

/// <summary>Chuyển đổi các draft câu hỏi/đề thi về payload chuẩn hóa dùng chung cho ContentApprovalService.</summary>
public static class AuthoringPayloadConverter
{
    public static MockTestImportPayloadResponse ToMockTestImportPayload(
        string title,
        string? description,
        int timeLimitMinutes,
        CertificateLevel level,
        IReadOnlyList<(string SectionTitle, int SortOrder, LanguageSkill Skill, CertificateSection CertSection, List<Requests.ContentAuthoring.ChoiceQuestionDraft> Questions)> sections)
    {
        var globalSort = 1;
        var mappedSections = new List<MockTestSectionPayloadResponse>();

        foreach (var sec in sections.OrderBy(s => s.SortOrder))
        {
            var mappedQuestions = new List<MockQuestionPayloadResponse>();
            foreach (var q in sec.Questions)
            {
                var interactionData = !string.IsNullOrWhiteSpace(q.AudioScript)
                    ? JsonSerializer.SerializeToElement(new { transcript = q.AudioScript })
                    : (JsonElement?)null;

                mappedQuestions.Add(new MockQuestionPayloadResponse
                {
                    SortOrder = globalSort++,
                    QuestionText = q.QuestionText.Trim(),
                    QuestionType = CanonicalQuestionTypes.SingleChoice,
                    InteractionData = interactionData,
                    Explanation = string.IsNullOrWhiteSpace(q.Explanation) ? null : q.Explanation.Trim(),
                    Options = q.Options.Select((o, idx) => new ImportOptionPayloadResponse
                    {
                        SortOrder = idx + 1,
                        OptionText = o.Text.Trim(),
                        IsCorrect = o.IsCorrect,
                    }).ToList(),
                });
            }

            mappedSections.Add(new MockTestSectionPayloadResponse
            {
                Title = string.IsNullOrWhiteSpace(sec.SectionTitle) ? sec.CertSection.Name : sec.SectionTitle.Trim(),
                SortOrder = sec.SortOrder,
                LanguageSkillId = sec.Skill.Id,
                LanguageSkillName = sec.Skill.Name,
                CertificateSectionCode = sec.CertSection.Code,
                Questions = mappedQuestions,
            });
        }

        return new MockTestImportPayloadResponse
        {
            Title = title.Trim(),
            Description = description?.Trim(),
            TimeLimitMinutes = timeLimitMinutes,
            IsPlacementAssessment = false,
            CertificateLevelCode = level.Code,
            CertificateLevelId = level.Id,
            Sections = mappedSections,
        };
    }

    public static PracticeExerciseImportPayloadResponse ToPracticeExerciseImportPayload(
        string title,
        string? description,
        string lessonTitle,
        int? lessonId,
        int certificateLevelId,
        LanguageSkill skill,
        IReadOnlyList<Requests.ContentAuthoring.ChoiceQuestionDraft> questions)
    {
        return new PracticeExerciseImportPayloadResponse
        {
            Title = title.Trim(),
            Description = description?.Trim(),
            LessonTitle = lessonTitle.Trim(),
            LessonId = lessonId,
            CertificateLevelId = certificateLevelId,
            LanguageSkillId = skill.Id,
            LanguageSkillName = skill.Name,
            Questions = questions.Select((q, idx) => new PracticeQuestionPayloadResponse
            {
                SortOrder = idx + 1,
                QuestionText = q.QuestionText.Trim(),
                Explanation = string.IsNullOrWhiteSpace(q.Explanation) ? null : q.Explanation.Trim(),
                Options = q.Options.Select((o, oIdx) => new ImportOptionPayloadResponse
                {
                    SortOrder = oIdx + 1,
                    OptionText = o.Text.Trim(),
                    IsCorrect = o.IsCorrect,
                }).ToList(),
            }).ToList(),
        };
    }
}
