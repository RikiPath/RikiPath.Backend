using RikiPath.Domain.Enums;
using System;
using System.Collections.Generic;

namespace RikiPath.Domain.Entities
{
    /// <summary>Canonical SM-2 spaced-repetition flashcard owned by a Learner across Vocabulary, Kanji, Grammar, Lesson, Reading, Test, and Custom sources.</summary>
    public class ReviewCard : Base
    {
        public int Id { get; set; }

        public int UserId { get; set; }
        public UserAccount UserAccount { get; set; } = null!;

        public int? LearnerNoteEntryId { get; set; }
        public LearnerNoteEntry? LearnerNoteEntry { get; set; }
        public int? VocabularyId { get; set; }
        public Vocabulary? Vocabulary { get; set; }
        public int? KanjiId { get; set; }
        public Kanji? Kanji { get; set; }
        public int? GrammarPatternId { get; set; }
        public GrammarPattern? GrammarPattern { get; set; }
        public int? LessonId { get; set; }
        public Lesson? Lesson { get; set; }
        public int? SrsCardTemplateId { get; set; }
        public SrsCardTemplate? SrsCardTemplate { get; set; }

        public ReviewMode Mode { get; set; } = ReviewMode.Recognition;
        public SrsCardStatus Status { get; set; } = SrsCardStatus.Active;
        public SrsLearningState LearningState { get; set; } = SrsLearningState.New;

        public string CardType { get; set; } = "Vocabulary";
        public string SourceType { get; set; } = "Manual";
        public int? SourceId { get; set; }
        public int SourceVersion { get; set; } = 1;
        public string? SourceLabel { get; set; }
        public string NormalizedContentKey { get; set; } = string.Empty;
        public string? SourceLinksJson { get; set; }

        public string FrontText { get; set; } = string.Empty;
        public string BackText { get; set; } = string.Empty;
        public string? Reading { get; set; }
        public string? Meaning { get; set; }
        public string? ExampleSentence { get; set; }
        public string? ExampleMeaning { get; set; }
        public string? ContextSnippet { get; set; }
        public string? JlptLevel { get; set; }
        public string? AudioUrl { get; set; }
        public string? PersonalNote { get; set; }
        public string? AcceptedReadingsJson { get; set; }
        public string? SynonymsJson { get; set; }

        public double EaseFactor { get; set; } = 2.5;
        public int IntervalDays { get; set; } = 0;
        public int Repetitions { get; set; } = 0;
        public int Lapses { get; set; }
        public int CorrectCount { get; set; }
        public int IncorrectCount { get; set; }
        public int Revision { get; set; } = 1;

        public DateTime NextReviewDate { get; set; }
        public DateTime? LastReviewedAt { get; set; }
        public DateTime? SuspendedAt { get; set; }
        public DateTime? ArchivedAt { get; set; }
        public DateTime? MasteredAt { get; set; }

        public int? WritingScore { get; set; }
        public int? WritingCorrectStrokeCount { get; set; }
        public int? WritingTotalStrokeCount { get; set; }
        public string? WritingPracticeMode { get; set; }
        public DateTime? WritingScoreUpdatedAt { get; set; }

        public List<ReviewHistory>? ReviewHistories { get; set; }
    }
}
