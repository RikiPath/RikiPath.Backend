using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RikiPath.Application;
using RikiPath.Application.IServices;
using RikiPath.Domain.Entities;
using RikiPath.Domain.Enums;

namespace RikiPath.WebApi.Controllers;

/// <summary>API để Content Author quản lý lesson links, đề thi, câu hỏi và lựa chọn đáp án.</summary>
[ApiController]
[Route("api/content-author")]
[Authorize(Roles = "ContentAuthor")]
public class ContentAuthorStructureController(IUnitOfWork uow, IClaimService claims) : ControllerBase
{
    /// <summary>Liên kết Kanji đã có vào Lesson của tác giả hiện tại.</summary>
    [HttpPost("lessons/{lessonId:int}/kanji/{kanjiId:int}")]
    public async Task<IActionResult> AddLessonKanji(int lessonId, int kanjiId, CancellationToken ct)
    {
        if (!await CanEditLesson(lessonId, ct)) return NotFound();
        var bank = await uow.Kanjis.GetByIdAsync(kanjiId, ct);
        if (bank is null || bank.IsDeleted || (bank.Status != ContentStatus.Published && bank.ContentAuthorId != claims.GetUserClaim().Id)) return BadRequest(new { error = "Kanji không tồn tại hoặc chưa được duyệt." });
        if (await uow.LessonKanjis.AnyAsync(x => x.LessonId == lessonId && x.KanjiId == kanjiId, ct)) return Conflict();
        await uow.LessonKanjis.AddAsync(new LessonKanji { LessonId = lessonId, KanjiId = kanjiId }, ct); await uow.SaveChangesAsync(ct); return NoContent();
    }

    /// <summary>Gỡ liên kết Kanji khỏi Lesson của tác giả hiện tại.</summary>
    [HttpDelete("lessons/{lessonId:int}/kanji/{kanjiId:int}")]
    public async Task<IActionResult> RemoveLessonKanji(int lessonId, int kanjiId, CancellationToken ct)
    {
        if (!await CanEditLesson(lessonId, ct)) return NotFound();
        var row = await uow.LessonKanjis.FirstOrDefaultAsync(x => x.LessonId == lessonId && x.KanjiId == kanjiId, ct);
        if (row is null) return NotFound(); uow.LessonKanjis.Remove(row); await uow.SaveChangesAsync(ct); return NoContent();
    }

    /// <summary>Liên kết Vocabulary đã có vào Lesson của tác giả hiện tại.</summary>
    [HttpPost("lessons/{lessonId:int}/vocabulary/{vocabularyId:int}")]
    public async Task<IActionResult> AddLessonVocabulary(int lessonId, int vocabularyId, CancellationToken ct)
    {
        if (!await CanEditLesson(lessonId, ct)) return NotFound();
        var bank = await uow.Vocabularies.GetByIdAsync(vocabularyId, ct);
        if (bank is null || bank.IsDeleted || (bank.Status != ContentStatus.Published && bank.ContentAuthorId != claims.GetUserClaim().Id)) return BadRequest(new { error = "Vocabulary không tồn tại hoặc chưa được duyệt." });
        if (await uow.LessonVocabularies.AnyAsync(x => x.LessonId == lessonId && x.VocabularyId == vocabularyId, ct)) return Conflict();
        await uow.LessonVocabularies.AddAsync(new LessonVocabulary { LessonId = lessonId, VocabularyId = vocabularyId }, ct); await uow.SaveChangesAsync(ct); return NoContent();
    }

    /// <summary>Gỡ liên kết Vocabulary khỏi Lesson của tác giả hiện tại.</summary>
    [HttpDelete("lessons/{lessonId:int}/vocabulary/{vocabularyId:int}")]
    public async Task<IActionResult> RemoveLessonVocabulary(int lessonId, int vocabularyId, CancellationToken ct)
    {
        if (!await CanEditLesson(lessonId, ct)) return NotFound();
        var row = await uow.LessonVocabularies.FirstOrDefaultAsync(x => x.LessonId == lessonId && x.VocabularyId == vocabularyId, ct);
        if (row is null) return NotFound(); uow.LessonVocabularies.Remove(row); await uow.SaveChangesAsync(ct); return NoContent();
    }

    /// <summary>Liên kết Grammar Pattern đã có vào Lesson của tác giả hiện tại.</summary>
    [HttpPost("lessons/{lessonId:int}/grammar/{grammarPatternId:int}")]
    public async Task<IActionResult> AddLessonGrammar(int lessonId, int grammarPatternId, CancellationToken ct)
    {
        if (!await CanEditLesson(lessonId, ct)) return NotFound();
        var bank = await uow.GrammarPatterns.GetByIdAsync(grammarPatternId, ct);
        if (bank is null || bank.IsDeleted || (bank.Status != ContentStatus.Published && bank.ContentAuthorId != claims.GetUserClaim().Id)) return BadRequest(new { error = "Grammar pattern không tồn tại hoặc chưa được duyệt." });
        if (await uow.LessonGrammars.AnyAsync(x => x.LessonId == lessonId && x.GrammarPatternId == grammarPatternId, ct)) return Conflict();
        await uow.LessonGrammars.AddAsync(new LessonGrammar { LessonId = lessonId, GrammarPatternId = grammarPatternId }, ct); await uow.SaveChangesAsync(ct); return NoContent();
    }

    /// <summary>Gỡ liên kết Grammar Pattern khỏi Lesson của tác giả hiện tại.</summary>
    [HttpDelete("lessons/{lessonId:int}/grammar/{grammarPatternId:int}")]
    public async Task<IActionResult> RemoveLessonGrammar(int lessonId, int grammarPatternId, CancellationToken ct)
    {
        if (!await CanEditLesson(lessonId, ct)) return NotFound();
        var row = await uow.LessonGrammars.FirstOrDefaultAsync(x => x.LessonId == lessonId && x.GrammarPatternId == grammarPatternId, ct);
        if (row is null) return NotFound(); uow.LessonGrammars.Remove(row); await uow.SaveChangesAsync(ct); return NoContent();
    }

    /// <summary>Tạo section mới trong Mock Test của tác giả hiện tại.</summary>
    [HttpPost("mock-tests/{testId:int}/sections")]
    public async Task<IActionResult> CreateMockSection(int testId, [FromBody] MockSectionRequest r, CancellationToken ct)
    {
        if (!await CanEditTest(testId, ct)) return NotFound(new { error = "Không tìm thấy bài thi có thể chỉnh sửa." });
        if (string.IsNullOrWhiteSpace(r.Title) || r.LanguageSkillId <= 0) return BadRequest(new { error = "Title và LanguageSkillId là bắt buộc." });
        var row = new MockTestSection { MockTestId = testId, Title = r.Title.Trim(), SortOrder = r.SortOrder, LanguageSkillId = r.LanguageSkillId };
        await uow.MockTestSections.AddAsync(row, ct); await uow.SaveChangesAsync(ct); return Created($"/api/content-author/mock-tests/{testId}/sections/{row.Id}", row);
    }

    /// <summary>Cập nhật tiêu đề, kỹ năng và thứ tự của section trong Mock Test.</summary>
    [HttpPut("mock-tests/{testId:int}/sections/{id:int}")]
    public async Task<IActionResult> UpdateMockSection(int testId, int id, [FromBody] MockSectionRequest r, CancellationToken ct)
    {
        var row = await uow.MockTestSections.GetByIdAsync(id, ct);
        if (row is null || row.MockTestId != testId || !await CanEditTest(testId, ct)) return NotFound();
        if (string.IsNullOrWhiteSpace(r.Title) || r.LanguageSkillId <= 0) return BadRequest(new { error = "Title và LanguageSkillId là bắt buộc." });
        row.Title = r.Title.Trim(); row.SortOrder = r.SortOrder; row.LanguageSkillId = r.LanguageSkillId; row.ModifiedDate = DateTime.UtcNow;
        uow.MockTestSections.Update(row); await uow.SaveChangesAsync(ct); return Ok(row);
    }

    /// <summary>Xóa section khỏi Mock Test đang ở trạng thái có thể chỉnh sửa.</summary>
    [HttpDelete("mock-tests/{testId:int}/sections/{id:int}")]
    public async Task<IActionResult> DeleteMockSection(int testId, int id, CancellationToken ct)
    {
        var row = await uow.MockTestSections.GetByIdAsync(id, ct);
        if (row is null || row.MockTestId != testId || !await CanEditTest(testId, ct)) return NotFound();
        uow.MockTestSections.Remove(row); await uow.SaveChangesAsync(ct); return NoContent();
    }

    /// <summary>Tạo câu hỏi trong một section của Mock Test.</summary>
    [HttpPost("mock-test-sections/{sectionId:int}/questions")]
    public async Task<IActionResult> CreateMockQuestion(int sectionId, [FromBody] MockQuestionRequest r, CancellationToken ct)
    {
        var section = await uow.MockTestSections.GetByIdAsync(sectionId, ct);
        if (section is null || !await CanEditTest(section.MockTestId, ct)) return NotFound();
        if (string.IsNullOrWhiteSpace(r.QuestionText)) return BadRequest(new { error = "QuestionText là bắt buộc." });
        var row = new MockQuestion { MockTestSectionId = sectionId, QuestionText = r.QuestionText.Trim(), Explanation = r.Explanation, AudioUrl = r.AudioUrl, ImageUrl = r.ImageUrl, SortOrder = r.SortOrder };
        await uow.MockQuestions.AddAsync(row, ct); await uow.SaveChangesAsync(ct); return Created($"/api/content-author/mock-questions/{row.Id}", row);
    }

    /// <summary>Cập nhật nội dung và thứ tự của câu hỏi Mock Test.</summary>
    [HttpPut("mock-questions/{id:int}")]
    public async Task<IActionResult> UpdateMockQuestion(int id, [FromBody] MockQuestionRequest r, CancellationToken ct)
    {
        var row = await uow.MockQuestions.GetByIdAsync(id, ct);
        var section = row is null ? null : await uow.MockTestSections.GetByIdAsync(row.MockTestSectionId, ct);
        if (row is null || section is null || !await CanEditTest(section.MockTestId, ct)) return NotFound();
        if (string.IsNullOrWhiteSpace(r.QuestionText)) return BadRequest(new { error = "QuestionText là bắt buộc." });
        row.QuestionText = r.QuestionText.Trim(); row.Explanation = r.Explanation; row.AudioUrl = r.AudioUrl; row.ImageUrl = r.ImageUrl; row.SortOrder = r.SortOrder; row.ModifiedDate = DateTime.UtcNow;
        uow.MockQuestions.Update(row); await uow.SaveChangesAsync(ct); return Ok(row);
    }

    /// <summary>Xóa câu hỏi Mock Test đang thuộc nội dung có thể chỉnh sửa.</summary>
    [HttpDelete("mock-questions/{id:int}")]
    public async Task<IActionResult> DeleteMockQuestion(int id, CancellationToken ct)
    {
        var row = await uow.MockQuestions.GetByIdAsync(id, ct);
        var section = row is null ? null : await uow.MockTestSections.GetByIdAsync(row.MockTestSectionId, ct);
        if (row is null || section is null || !await CanEditTest(section.MockTestId, ct)) return NotFound();
        uow.MockQuestions.Remove(row); await uow.SaveChangesAsync(ct); return NoContent();
    }

    /// <summary>Tạo lựa chọn đáp án cho câu hỏi Mock Test.</summary>
    [HttpPost("mock-questions/{questionId:int}/options")]
    public async Task<IActionResult> CreateMockOption(int questionId, [FromBody] OptionRequest r, CancellationToken ct)
    {
        var question = await uow.MockQuestions.GetByIdAsync(questionId, ct);
        var section = question is null ? null : await uow.MockTestSections.GetByIdAsync(question.MockTestSectionId, ct);
        if (question is null || section is null || !await CanEditTest(section.MockTestId, ct)) return NotFound();
        if (string.IsNullOrWhiteSpace(r.OptionText)) return BadRequest(new { error = "OptionText là bắt buộc." });
        var row = new MockQuestionOption { MockQuestionId = questionId, OptionText = r.OptionText.Trim(), IsCorrect = r.IsCorrect, SortOrder = r.SortOrder };
        await uow.MockQuestionOptions.AddAsync(row, ct); await uow.SaveChangesAsync(ct); return Created($"/api/content-author/mock-question-options/{row.Id}", row);
    }

    /// <summary>Cập nhật nội dung, cờ đáp án đúng và thứ tự của lựa chọn Mock Test.</summary>
    [HttpPut("mock-question-options/{id:int}")]
    public async Task<IActionResult> UpdateMockOption(int id, [FromBody] OptionRequest r, CancellationToken ct)
    {
        var row = await uow.MockQuestionOptions.GetByIdAsync(id, ct);
        var question = row is null ? null : await uow.MockQuestions.GetByIdAsync(row.MockQuestionId, ct);
        var section = question is null ? null : await uow.MockTestSections.GetByIdAsync(question.MockTestSectionId, ct);
        if (row is null || section is null || !await CanEditTest(section.MockTestId, ct)) return NotFound();
        if (string.IsNullOrWhiteSpace(r.OptionText)) return BadRequest(new { error = "OptionText là bắt buộc." });
        row.OptionText = r.OptionText.Trim(); row.IsCorrect = r.IsCorrect; row.SortOrder = r.SortOrder; row.ModifiedDate = DateTime.UtcNow;
        uow.MockQuestionOptions.Update(row); await uow.SaveChangesAsync(ct); return Ok(row);
    }

    /// <summary>Xóa lựa chọn đáp án khỏi câu hỏi Mock Test.</summary>
    [HttpDelete("mock-question-options/{id:int}")]
    public async Task<IActionResult> DeleteMockOption(int id, CancellationToken ct)
    {
        var row = await uow.MockQuestionOptions.GetByIdAsync(id, ct);
        var question = row is null ? null : await uow.MockQuestions.GetByIdAsync(row.MockQuestionId, ct);
        var section = question is null ? null : await uow.MockTestSections.GetByIdAsync(question.MockTestSectionId, ct);
        if (row is null || section is null || !await CanEditTest(section.MockTestId, ct)) return NotFound();
        uow.MockQuestionOptions.Remove(row); await uow.SaveChangesAsync(ct); return NoContent();
    }

    /// <summary>Tạo câu hỏi cho Practice Exercise của tác giả hiện tại.</summary>
    [HttpPost("practice-exercises/{exerciseId:int}/questions")]
    public async Task<IActionResult> CreatePracticeQuestion(int exerciseId, [FromBody] PracticeQuestionRequest r, CancellationToken ct)
    {
        if (!await CanEditExercise(exerciseId, ct)) return NotFound();
        if (string.IsNullOrWhiteSpace(r.QuestionText)) return BadRequest(new { error = "QuestionText là bắt buộc." });
        var row = new PracticeQuestion { PracticeExerciseId = exerciseId, QuestionText = r.QuestionText.Trim(), Explanation = r.Explanation, SortOrder = r.SortOrder };
        await uow.PracticeQuestions.AddAsync(row, ct); await uow.SaveChangesAsync(ct); return Created($"/api/content-author/practice-questions/{row.Id}", row);
    }

    /// <summary>Cập nhật nội dung và thứ tự câu hỏi Practice Exercise.</summary>
    [HttpPut("practice-questions/{id:int}")]
    public async Task<IActionResult> UpdatePracticeQuestion(int id, [FromBody] PracticeQuestionRequest r, CancellationToken ct)
    {
        var row = await uow.PracticeQuestions.GetByIdAsync(id, ct);
        if (row is null || !await CanEditExercise(row.PracticeExerciseId, ct)) return NotFound();
        if (string.IsNullOrWhiteSpace(r.QuestionText)) return BadRequest(new { error = "QuestionText là bắt buộc." });
        row.QuestionText = r.QuestionText.Trim(); row.Explanation = r.Explanation; row.SortOrder = r.SortOrder; row.ModifiedDate = DateTime.UtcNow;
        uow.PracticeQuestions.Update(row); await uow.SaveChangesAsync(ct); return Ok(row);
    }

    /// <summary>Xóa câu hỏi khỏi Practice Exercise đang có thể chỉnh sửa.</summary>
    [HttpDelete("practice-questions/{id:int}")]
    public async Task<IActionResult> DeletePracticeQuestion(int id, CancellationToken ct)
    {
        var row = await uow.PracticeQuestions.GetByIdAsync(id, ct);
        if (row is null || !await CanEditExercise(row.PracticeExerciseId, ct)) return NotFound();
        uow.PracticeQuestions.Remove(row); await uow.SaveChangesAsync(ct); return NoContent();
    }

    /// <summary>Tạo lựa chọn đáp án cho câu hỏi Practice Exercise.</summary>
    [HttpPost("practice-questions/{questionId:int}/options")]
    public async Task<IActionResult> CreatePracticeOption(int questionId, [FromBody] OptionRequest r, CancellationToken ct)
    {
        var question = await uow.PracticeQuestions.GetByIdAsync(questionId, ct);
        if (question is null || !await CanEditExercise(question.PracticeExerciseId, ct)) return NotFound();
        if (string.IsNullOrWhiteSpace(r.OptionText)) return BadRequest(new { error = "OptionText là bắt buộc." });
        var row = new PracticeQuestionOption { PracticeQuestionId = questionId, OptionText = r.OptionText.Trim(), IsCorrect = r.IsCorrect, SortOrder = r.SortOrder };
        await uow.PracticeQuestionOptions.AddAsync(row, ct); await uow.SaveChangesAsync(ct); return Created($"/api/content-author/practice-question-options/{row.Id}", row);
    }

    /// <summary>Cập nhật lựa chọn đáp án của Practice Exercise.</summary>
    [HttpPut("practice-question-options/{id:int}")]
    public async Task<IActionResult> UpdatePracticeOption(int id, [FromBody] OptionRequest r, CancellationToken ct)
    {
        var row = await uow.PracticeQuestionOptions.GetByIdAsync(id, ct);
        var question = row is null ? null : await uow.PracticeQuestions.GetByIdAsync(row.PracticeQuestionId, ct);
        if (row is null || question is null || !await CanEditExercise(question.PracticeExerciseId, ct)) return NotFound();
        if (string.IsNullOrWhiteSpace(r.OptionText)) return BadRequest(new { error = "OptionText là bắt buộc." });
        row.OptionText = r.OptionText.Trim(); row.IsCorrect = r.IsCorrect; row.SortOrder = r.SortOrder; row.ModifiedDate = DateTime.UtcNow;
        uow.PracticeQuestionOptions.Update(row); await uow.SaveChangesAsync(ct); return Ok(row);
    }

    /// <summary>Xóa lựa chọn đáp án khỏi câu hỏi Practice Exercise.</summary>
    [HttpDelete("practice-question-options/{id:int}")]
    public async Task<IActionResult> DeletePracticeOption(int id, CancellationToken ct)
    {
        var row = await uow.PracticeQuestionOptions.GetByIdAsync(id, ct);
        var question = row is null ? null : await uow.PracticeQuestions.GetByIdAsync(row.PracticeQuestionId, ct);
        if (row is null || question is null || !await CanEditExercise(question.PracticeExerciseId, ct)) return NotFound();
        uow.PracticeQuestionOptions.Remove(row); await uow.SaveChangesAsync(ct); return NoContent();
    }

    private async Task<bool> CanEditTest(int id, CancellationToken ct)
    {
        var entity = await uow.MockTests.GetByIdAsync(id, ct);
        return entity is not null && !entity.IsDeleted && entity.ContentAuthorId == claims.GetUserClaim().Id && entity.Status is ContentStatus.Draft or ContentStatus.Rejected;
    }
    private async Task<bool> CanEditExercise(int id, CancellationToken ct)
    {
        var entity = await uow.PracticeExercises.GetByIdAsync(id, ct);
        return entity is not null && !entity.IsDeleted && entity.ContentAuthorId == claims.GetUserClaim().Id && entity.Status is ContentStatus.Draft or ContentStatus.Rejected;
    }
    private async Task<bool> CanEditLesson(int id, CancellationToken ct)
    {
        var entity = await uow.Lessons.GetByIdAsync(id, ct);
        return entity is not null && !entity.IsDeleted && entity.ContentAuthorId == claims.GetUserClaim().Id && entity.Status is ContentStatus.Draft or ContentStatus.Rejected;
    }
}

public sealed class MockSectionRequest { public string Title { get; set; } = string.Empty; public int SortOrder { get; set; } public int LanguageSkillId { get; set; } }
public sealed class MockQuestionRequest { public string QuestionText { get; set; } = string.Empty; public string? Explanation { get; set; } public string? AudioUrl { get; set; } public string? ImageUrl { get; set; } public int SortOrder { get; set; } }
public sealed class PracticeQuestionRequest { public string QuestionText { get; set; } = string.Empty; public string? Explanation { get; set; } public int SortOrder { get; set; } }
public sealed class OptionRequest { public string OptionText { get; set; } = string.Empty; public bool IsCorrect { get; set; } public int SortOrder { get; set; } }
