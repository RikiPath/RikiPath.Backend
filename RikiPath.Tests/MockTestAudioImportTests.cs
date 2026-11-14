using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Moq;
using RikiPath.Application.Handlers;
using RikiPath.Application.Helpers;
using RikiPath.Application.IServices;
using RikiPath.Application.Requests.Content;
using RikiPath.Application.Responses.Content;
using RikiPath.Application.Services;
using RikiPath.Domain.Entities;
using RikiPath.Domain.Enums;
using RikiPath.Infrastructure;

namespace RikiPath.Tests;

public sealed class MockTestAudioImportTests
{
    [Fact]
    public async Task Preview_keeps_grammar_and_reading_separate_under_the_combined_n5_section_code()
    {
        await using var context = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        context.CertificateLevels.Add(new CertificateLevel
        {
            Id = 81, Code = "N5", CertificateType = new CertificateType { Id = 80, Name = "JLPT", IsActive = true }
        });
        var sections = new (string Code, string Title)[]
        {
            ("N5_VOCAB", "Từ vựng"),
            ("N5_GRAMMAR_READING", "Kiến thức ngôn ngữ (Ngữ pháp) & Đọc hiểu"),
            ("N5_LISTENING", "Nghe hiểu")
        };
        context.CertificateSections.AddRange(sections.Select((section, index) =>
            new CertificateSection { Id = 82 + index, Code = section.Code, Name = section.Title, CertificateLevelId = 81 }));
        var questions = new (string Code, string Skill, string? Audio)[]
        {
            ("N5_VOCAB", "Vocabulary", null),
            ("N5_GRAMMAR_READING", "Grammar", null),
            ("N5_GRAMMAR_READING", "Reading", null),
            ("N5_LISTENING", "Listening", "https://example.test/n5.mp3")
        };
        context.LanguageSkills.AddRange(questions.Select((question, index) =>
            new LanguageSkill { Id = 90 + index, Name = question.Skill }));
        await context.SaveChangesAsync();

        var rows = questions.Select((question, index) => new ExcelRow
        {
            RowNumber = index + 2,
            Fields = new()
            {
                ["mocktesttitle"] = "Đề JLPT N5",
                ["certificatelevelcode"] = "N5",
                ["timelimitminutes"] = "90",
                ["sectioncode"] = question.Code,
                ["languageskillname"] = question.Skill,
                ["questionsortorder"] = (index + 1).ToString(),
                ["questiontext"] = $"Câu {index + 1}",
                ["questiontype"] = "SingleChoice",
                ["option1"] = "A",
                ["option2"] = "B",
                ["correctoptionnumber"] = "1",
                ["audiourl"] = question.Audio ?? string.Empty
            }
        }).ToList();
        var handler = new MockTestImportHandler(new UnitOfWork(context));
        var preview = Assert.Single((await handler.BuildDiffAsync(rows, default)).Rows);
        Assert.Empty(preview.Errors);
        Assert.Equal(ImportRowAction.New, preview.Action);
        var payload = JsonSerializer.Deserialize<MockTestImportPayloadResponse>(preview.PayloadJson!);
        Assert.NotNull(payload);
        Assert.Equal(questions.Select(question => (question.Code, question.Skill)),
            payload.Sections.Select(section => (section.CertificateSectionCode!, section.LanguageSkillName)));
        Assert.All(payload.Sections, section => Assert.Single(section.Questions));
    }

    [Theory]
    [InlineData("LISTENING", "SingleChoice", "", "thiếu AudioUrl")]
    [InlineData("LISTENING", "SingleChoice", "audio.mp3", "http/https")]
    [InlineData("LISTENING", "SingleChoice", "https://example.test/audio.wav", "file .mp3")]
    [InlineData("READING", "Listening", "", "thiếu AudioUrl")]
    [InlineData("LISTENING", "SingleChoice", "https://example.test/audio.mp3", null)]
    [InlineData("READING", "SingleChoice", "", null)]
    public async Task Preview_and_confirm_enforce_listening_audio(
        string skill, string questionType, string audio, string? expectedError)
    {
        await using var context = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        context.CertificateLevels.Add(new CertificateLevel
        {
            Id = 71, Code = "N2", CertificateType = new CertificateType { Id = 70, Name = "JLPT", IsActive = true }
        });
        var skillCodes = new[] { skill, "VOCABULARY", "GRAMMAR", "READING", "LISTENING" }.Distinct().ToArray();
        context.CertificateSections.AddRange(skillCodes.Select((code, index) =>
            new CertificateSection { Id = 72 + index, Code = code, Name = code, CertificateLevelId = 71 }));
        context.LanguageSkills.AddRange(skillCodes.Select((code, index) =>
            new LanguageSkill { Id = 73 + index, Name = code }));
        await context.SaveChangesAsync();
        var unitOfWork = new UnitOfWork(context);
        var handler = new MockTestImportHandler(unitOfWork);
        var row = new ExcelRow
        {
            RowNumber = 7,
            Fields = new()
            {
                ["mocktesttitle"] = "Đề kiểm tra audio",
                ["certificatelevelcode"] = "N2",
                ["timelimitminutes"] = "10",
                ["sectioncode"] = skill,
                ["questionsortorder"] = "1",
                ["questiontext"] = "Chọn đáp án",
                ["questiontype"] = questionType,
                ["option1"] = "A",
                ["option2"] = "B",
                ["correctoptionnumber"] = "1",
                ["audiourl"] = audio,
            }
        };
        var rows = new List<ExcelRow> { row };
        foreach (var code in skillCodes.Skip(1))
        {
            var fields = new Dictionary<string, string>(row.Fields)
            {
                ["sectioncode"] = code,
                ["questionsortorder"] = (rows.Count + 1).ToString(),
                ["questiontype"] = "SingleChoice",
                ["audiourl"] = code == "LISTENING" ? "https://example.test/audio.mp3?version=1" : "",
            };
            rows.Add(new ExcelRow { RowNumber = 7 + rows.Count, Fields = fields });
        }
        var preview = Assert.Single((await handler.BuildDiffAsync(rows, default)).Rows);
        if (expectedError is null)
        {
            Assert.Equal(ImportRowAction.New, preview.Action);
            var incomplete = Assert.Single((await handler.BuildDiffAsync([row], default)).Rows);
            Assert.Contains(incomplete.Errors, error => error.Contains("thiếu phần có câu hỏi"));
            if (skill == "LISTENING")
            {
                var localPath = Path.Combine(Path.GetTempPath(), "N2.mp3");
                var localImage = Path.Combine(Path.GetTempPath(), "option.png");
                using var workbook = new ClosedXML.Excel.XLWorkbook();
                var sheet = workbook.AddWorksheet("Mock Test");
                var headers = row.Fields.Keys.Concat(["imageurl", "option1imageurl"]).ToArray();
                for (var column = 0; column < headers.Length; column++)
                {
                    var key = headers[column];
                    sheet.Cell(1, column + 1).Value = key;
                    for (var index = 0; index < rows.Count; index++)
                        sheet.Cell(index + 2, column + 1).Value = (index, key) switch
                        {
                            (0, "audiourl") => localPath,
                            (0, "imageurl" or "option1imageurl") => localImage,
                            (0, "questiontext" or "option1") => "",
                            _ => rows[index].Fields.GetValueOrDefault(key) ?? "",
                        };
                }
                using var excel = new MemoryStream();
                workbook.SaveAs(excel);
                excel.Position = 0;
                var importer = new ExcelContentImportService(unitOfWork, [handler]);
                var localPreview = await importer.PreviewAsync(excel, "mock-test.xlsx", ImportEntityType.MockTest, default,
                    (path, _) =>
                    {
                        Assert.Equal(localPath, path);
                        return Task.FromResult("https://example.test/uploaded.mp3");
                    }, (path, _) =>
                    {
                        Assert.Equal(localImage, path);
                        return Task.FromResult("https://example.test/uploaded.png");
                    });
                var imported = Assert.Single(Assert.Single(localPreview.Result!.Sheets).Rows);
                Assert.Equal(ImportRowAction.New, imported.Action);
                Assert.Contains("https://example.test/uploaded.mp3", imported.PayloadJson);
                var payloadWithImage = JsonSerializer.Deserialize<MockTestImportPayloadResponse>(imported.PayloadJson!);
                var question = payloadWithImage!.Sections.SelectMany(section => section.Questions)
                    .Single(item => item.AudioUrl == "https://example.test/uploaded.mp3");
                Assert.Equal("https://example.test/uploaded.png", question.ImageUrl);
                Assert.Equal("", question.Options[0].OptionText);
                Assert.Equal("https://example.test/uploaded.png", question.Options[0].ImageUrl);

                var saved = await importer.ConfirmAsync(new ConfirmImportRequest
                {
                    SourceFileName = "mock-test.xlsx",
                    Rows = [new()
                    {
                        EntityType = ImportEntityType.MockTest,
                        Action = imported.Action,
                        RowKey = imported.RowKey,
                        SourceRowNumber = imported.SourceRowNumber,
                        PayloadJson = imported.PayloadJson!
                    }]
                }, 1, default);
                Assert.True(saved.IsSuccess, saved.ErrorMessage);
                var claims = Mock.Of<IClaimService>(service => service.GetUserClaim() == new ClaimDTO { Id = 1, Role = Role.ContentAuthor });
                var submitted = await new ManualContentService(unitOfWork, claims).SubmitBatchAsync(saved.Result!.BatchId, default);
                Assert.True(submitted.IsSuccess, submitted.ErrorMessage);
                Assert.Equal(1, submitted.Result);
                Assert.Equal(ContentApprovalStatus.PendingReview, (await context.ContentApprovalRequests.SingleAsync()).Status);
            }
            return;
        }

        Assert.Equal(ImportRowAction.Error, preview.Action);
        Assert.Contains(preview.Errors, error => error.Contains("Dòng 7") && error.Contains(expectedError));
        var payload = new MockTestImportPayloadResponse
        {
            Sections = [new()
            {
                Title = "Phần 1",
                LanguageSkillId = 73,
                Questions = [new() { SortOrder = 1, QuestionType = questionType, AudioUrl = audio }]
            }]
        };
        var service = new ExcelContentImportService(unitOfWork, [handler]);
        var confirm = await service.ConfirmAsync(new ConfirmImportRequest
        {
            Rows = [new()
            {
                EntityType = ImportEntityType.MockTest,
                Action = ImportRowAction.New,
                RowKey = "Đề kiểm tra audio / N2",
                SourceRowNumber = 7,
                PayloadJson = JsonSerializer.Serialize(payload),
            }]
        }, 1, default);
        Assert.False(confirm.IsSuccess);
        Assert.Contains(expectedError, confirm.ErrorMessage);
        Assert.Empty(await context.ContentApprovalRequests.ToListAsync());
    }
}
