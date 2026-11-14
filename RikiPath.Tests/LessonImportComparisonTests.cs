using Microsoft.EntityFrameworkCore;
using RikiPath.Application.Handlers;
using RikiPath.Application.Helpers;
using RikiPath.Domain.Entities;
using RikiPath.Domain.Enums;
using RikiPath.Infrastructure;

namespace RikiPath.Tests;

public sealed class LessonImportComparisonTests
{
    [Fact]
    public async Task Existing_duplicate_is_reported_with_ids_and_unique_match_is_unchanged()
    {
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        db.CertificateTypes.Add(new CertificateType { Id = 41, Name = "JLPT" });
        db.CertificateLevels.AddRange(
            new CertificateLevel { Id = 42, Code = "N5", CertificateTypeId = 41 },
            new CertificateLevel { Id = 44, Code = "N4", CertificateTypeId = 41 });
        db.LanguageSkills.Add(new LanguageSkill { Id = 43, Name = "Vocabulary" });
        db.Lessons.AddRange(
            new Lesson { Id = 72, Title = "Chào hỏi", CertificateLevelId = 42, LanguageSkillId = 43 },
            new Lesson { Id = 71, Title = "Chào hỏi", CertificateLevelId = 42, LanguageSkillId = 43 },
            new Lesson { Id = 73, Title = "Chào hỏi", CertificateLevelId = 44, LanguageSkillId = 43 },
            new Lesson { Id = 74, Title = "Chào hỏi", CertificateLevelId = 42, LanguageSkillId = 43, IsDeleted = true });
        await db.SaveChangesAsync();
        var handler = new LessonImportHandler(new UnitOfWork(db));

        var result = await handler.BuildDiffAsync([Row(2, "N5"), Row(3, "N4")], default);

        Assert.Equal(1, result.ErrorCount);
        Assert.Equal(1, result.UnchangedCount);
        Assert.Equal(ImportRowAction.Error, result.Rows[0].Action);
        Assert.Contains("ID #71, #72", Assert.Single(result.Rows[0].Errors));
        Assert.Null(result.Rows[0].PayloadJson);
        Assert.Equal(73, result.Rows[1].TargetEntityId);
    }

    private static ExcelRow Row(int number, string level) => new()
    {
        RowNumber = number,
        Fields = new()
        {
            ["lessontitle"] = "Chào hỏi",
            ["certificatelevelcode"] = level,
            ["languageskillname"] = "Vocabulary",
        },
    };
}
