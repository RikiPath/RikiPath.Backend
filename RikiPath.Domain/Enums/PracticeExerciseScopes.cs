namespace RikiPath.Domain.Enums;

public static class PracticeExerciseScopes
{
    public const string LessonQuickCheck = "LessonQuickCheck";
    public const string GroupQuiz = "GroupQuiz";
    public const string StageAssessment = "StageAssessment";

    public static readonly string[] All =
    [
        LessonQuickCheck,
        GroupQuiz,
        StageAssessment,
    ];

    public static string Normalize(string? value) => (value ?? string.Empty).Trim().ToUpperInvariant() switch
    {
        "LESSONQUICKCHECK" or "LESSON_QUICK_CHECK" or "QUICKCHECK" or "LESSON" => LessonQuickCheck,
        "GROUPQUIZ" or "GROUP_QUIZ" or "TOPICQUIZ" or "CHAPTERQUIZ" => GroupQuiz,
        "STAGEASSESSMENT" or "STAGE_ASSESSMENT" or "LEVELASSESSMENT" => StageAssessment,
        _ => LessonQuickCheck,
    };
}
