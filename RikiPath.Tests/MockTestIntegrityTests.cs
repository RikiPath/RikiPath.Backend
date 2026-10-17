using System.Linq.Expressions;
using System.Net;
using Moq;
using RikiPath.Application;
using RikiPath.Application.IRepositories;
using RikiPath.Application.IServices;
using RikiPath.Application.Requests.MockTests;
using RikiPath.Application.Services;
using RikiPath.Domain.Entities;
using RikiPath.Domain.Enums;

namespace RikiPath.Tests;

public sealed class MockTestIntegrityTests
{
    [Fact]
    public async Task Start_valid_learner_creates_attempt()
    {
        var fixture = CreateFixture();

        var response = await fixture.Service.StartAttemptAsync(fixture.Test.Id);

        Assert.True(response.IsSuccess);
        Assert.Equal((int)HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(fixture.Attempt.Id, response.Result!.AttemptId);
    }

    [Fact]
    public async Task Start_returns_real_question_ids()
    {
        var fixture = CreateFixture();

        var response = await fixture.Service.StartAttemptAsync(fixture.Test.Id);

        Assert.Equal([11, 12, 21], response.Result!.Questions.Select(question => question.QuestionId));
    }

    [Fact]
    public async Task Start_returns_real_option_ids()
    {
        var fixture = CreateFixture();

        var response = await fixture.Service.StartAttemptAsync(fixture.Test.Id);

        Assert.Equal([111, 112], response.Result!.Questions[0].Options.Select(option => option.OptionId));
    }

    [Fact]
    public async Task Start_snapshots_only_questions_from_requested_test()
    {
        var fixture = CreateFixture();

        await fixture.Service.StartAttemptAsync(fixture.Test.Id);

        Assert.Equal(3, fixture.AddedSnapshots.Count);
        Assert.All(
            fixture.AddedSnapshots,
            answer => Assert.Equal(fixture.Test.Id, fixture.QuestionById(answer.MockQuestionId).MockTestSection.MockTestId));
    }

    [Fact]
    public async Task Start_preserves_section_question_and_option_ordering()
    {
        var fixture = CreateFixture();

        var response = await fixture.Service.StartAttemptAsync(fixture.Test.Id);

        Assert.Equal([11, 12, 21], response.Result!.Questions.Select(question => question.QuestionId));
        Assert.Equal([111, 112], response.Result.Questions[0].Options.Select(option => option.OptionId));
        Assert.Equal([1, 1, 2], response.Result.Questions.Select(question => question.SectionOrder));
    }

    [Fact]
    public async Task Owner_can_read_summary_result()
    {
        var fixture = CreateFixture();
        fixture.Attempt.IsCompleted = true;
        fixture.Attempt.TotalScore = 66.67;
        fixture.ConfigureSummary();

        var response = await fixture.Service.GetAttemptResultAsync(fixture.Attempt.Id);

        Assert.True(response.IsSuccess);
        Assert.Equal(fixture.Test.Id, response.Result!.MockTestId);
    }

    [Fact]
    public async Task Learner_cannot_read_another_learners_attempt()
    {
        var fixture = CreateFixture();
        fixture.Attempt.UserId = 99;
        fixture.Attempt.IsCompleted = true;
        fixture.MockTestAttempts
            .Setup(repository => repository.GetByIdAsync(fixture.Attempt.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(fixture.Attempt);

        var response = await fixture.Service.GetAttemptResultAsync(fixture.Attempt.Id);

        Assert.False(response.IsSuccess);
        Assert.Equal((int)HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Learner_cannot_submit_another_learners_attempt()
    {
        var fixture = CreateFixture();
        fixture.ConfigureSubmission(ownerId: 99);

        var response = await fixture.Service.SubmitAttemptAsync(fixture.Attempt.Id, fixture.ValidSubmission());

        Assert.Equal((int)HttpStatusCode.Forbidden, response.StatusCode);
        fixture.UnitOfWork.Verify(unit => unit.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Submit_accepts_question_from_attempt_snapshot()
    {
        var fixture = CreateFixture();
        fixture.ConfigureSubmission();

        var response = await fixture.Service.SubmitAttemptAsync(
            fixture.Attempt.Id,
            new SubmitAttemptRequest { Answers = [new AnswerSubmissionItem { QuestionId = 11, SelectedOptionId = 112 }] });

        Assert.True(response.IsSuccess);
    }

    [Fact]
    public async Task Submit_rejects_question_from_another_test()
    {
        var fixture = CreateFixture();
        fixture.ConfigureSubmission();

        var response = await fixture.Service.SubmitAttemptAsync(
            fixture.Attempt.Id,
            new SubmitAttemptRequest { Answers = [new AnswerSubmissionItem { QuestionId = 999, SelectedOptionId = null }] });

        Assert.Equal((int)HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("không thuộc lượt làm bài", response.ErrorMessage);
    }

    [Fact]
    public async Task Submit_accepts_option_from_matching_question()
    {
        var fixture = CreateFixture();
        fixture.ConfigureSubmission();

        var response = await fixture.Service.SubmitAttemptAsync(
            fixture.Attempt.Id,
            new SubmitAttemptRequest { Answers = [new AnswerSubmissionItem { QuestionId = 11, SelectedOptionId = 112 }] });

        Assert.True(response.IsSuccess);
        Assert.True(fixture.SnapshotByQuestionId(11).IsCorrect);
        Assert.Equal(112, fixture.SnapshotByQuestionId(11).SelectedOptionId);
    }

    [Fact]
    public async Task Submit_rejects_option_from_different_question()
    {
        var fixture = CreateFixture();
        fixture.ConfigureSubmission();

        var response = await fixture.Service.SubmitAttemptAsync(
            fixture.Attempt.Id,
            new SubmitAttemptRequest { Answers = [new AnswerSubmissionItem { QuestionId = 11, SelectedOptionId = 122 }] });

        Assert.Equal((int)HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("không thuộc câu hỏi", response.ErrorMessage);
    }

    [Fact]
    public async Task Submit_rejects_duplicate_question_entries()
    {
        var fixture = CreateFixture();

        var response = await fixture.Service.SubmitAttemptAsync(
            fixture.Attempt.Id,
            new SubmitAttemptRequest
            {
                Answers =
                [
                    new AnswerSubmissionItem { QuestionId = 11, SelectedOptionId = 111 },
                    new AnswerSubmissionItem { QuestionId = 11, SelectedOptionId = 112 },
                ],
            });

        Assert.Equal((int)HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("lặp câu hỏi", response.ErrorMessage);
        fixture.UnitOfWork.Verify(
            unit => unit.ExecuteInTransactionAsync(
                It.IsAny<Func<CancellationToken, Task>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Active_attempt_submits_successfully()
    {
        var fixture = CreateFixture();
        fixture.ConfigureSubmission();

        var response = await fixture.Service.SubmitAttemptAsync(fixture.Attempt.Id, fixture.ValidSubmission());

        Assert.True(response.IsSuccess);
        Assert.True(fixture.Attempt.IsCompleted);
        Assert.NotNull(fixture.Attempt.SubmittedAt);
        Assert.Equal(1, fixture.SaveCount);
    }

    [Fact]
    public async Task Completed_attempt_cannot_be_regraded()
    {
        var fixture = CreateFixture();
        fixture.ConfigureSubmission(completed: true);

        var response = await fixture.Service.SubmitAttemptAsync(fixture.Attempt.Id, fixture.ValidSubmission());

        Assert.Equal((int)HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("đã được nộp", response.ErrorMessage);
        Assert.Equal(0, fixture.SaveCount);
    }

    [Fact]
    public async Task Retry_does_not_duplicate_logical_results()
    {
        var fixture = CreateFixture();
        fixture.ConfigureSubmission();

        var first = await fixture.Service.SubmitAttemptAsync(fixture.Attempt.Id, fixture.ValidSubmission());
        var second = await fixture.Service.SubmitAttemptAsync(fixture.Attempt.Id, fixture.ValidSubmission());

        Assert.True(first.IsSuccess);
        Assert.Equal((int)HttpStatusCode.BadRequest, second.StatusCode);
        Assert.Equal(1, fixture.SectionResultWriteCount);
        Assert.Equal(1, fixture.SaveCount);
    }

    [Fact]
    public async Task Attempt_within_time_limit_is_accepted()
    {
        var fixture = CreateFixture();
        fixture.ConfigureSubmission(startedAt: DateTime.UtcNow.AddMinutes(-29));

        var response = await fixture.Service.SubmitAttemptAsync(fixture.Attempt.Id, fixture.ValidSubmission());

        Assert.True(response.IsSuccess);
    }

    [Fact]
    public async Task Expired_attempt_is_rejected_by_server_clock()
    {
        var fixture = CreateFixture();
        fixture.ConfigureSubmission(startedAt: DateTime.UtcNow.AddMinutes(-31));

        var response = await fixture.Service.SubmitAttemptAsync(fixture.Attempt.Id, fixture.ValidSubmission());

        Assert.Equal((int)HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("hết thời gian", response.ErrorMessage);
        Assert.Equal(0, fixture.SaveCount);
    }

    [Fact]
    public async Task Summary_result_remains_valid()
    {
        var fixture = CreateFixture();
        fixture.Attempt.IsCompleted = true;
        fixture.Attempt.TotalScore = 75;
        fixture.ConfigureSummary();

        var response = await fixture.Service.GetAttemptResultAsync(fixture.Attempt.Id);

        Assert.True(response.IsSuccess);
        Assert.Equal("completed", response.Result!.Status);
        Assert.Equal(75, response.Result.TotalScore);
        Assert.Single(response.Result.Sections);
    }

    [Fact]
    public async Task Detailed_result_remains_valid()
    {
        var fixture = CreateFixture();
        fixture.Attempt.IsCompleted = true;
        fixture.Attempt.TotalScore = 66.67;
        fixture.ConfigureDetailedResult();

        var response = await fixture.Service.GetDetailedResultAsync(fixture.Attempt.Id);

        Assert.True(response.IsSuccess);
        Assert.Equal(fixture.Test.Id, response.Result!.MockTestId);
        Assert.Equal(3, response.Result.Questions.Count);
        Assert.Equal(2, response.Result.Questions[0].Options.Count);
    }

    [Fact]
    public async Task Grading_failure_occurs_inside_transaction_and_leaks_no_internal_error()
    {
        var fixture = CreateFixture();
        fixture.ConfigureSubmission();
        fixture.UnitOfWork
            .Setup(unit => unit.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("database detail must stay private"));

        var response = await fixture.Service.SubmitAttemptAsync(fixture.Attempt.Id, fixture.ValidSubmission());

        Assert.Equal((int)HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Null(response.Errors);
        Assert.DoesNotContain("database detail", response.ErrorMessage);
        fixture.UnitOfWork.Verify(
            unit => unit.ExecuteInTransactionAsync(
                It.IsAny<Func<CancellationToken, Task>>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private static Fixture CreateFixture()
    {
        var unitOfWork = new Mock<IUnitOfWork>();
        var claims = new Mock<IClaimService>();
        var mockTests = new Mock<IMockTestRepository>();
        var attempts = new Mock<IMockTestAttemptRepository>();
        var answers = new Mock<IMockTestAnswerRepository>();
        var sectionResults = new Mock<IMockTestSectionResultRepository>();

        var test = BuildTest();
        var attempt = new MockTestAttempt
        {
            Id = 500,
            UserId = 7,
            StartedAt = DateTime.UtcNow.AddMinutes(-1),
            IsCompleted = false,
            TotalScore = 0,
        };

        claims.Setup(service => service.GetUserClaim())
            .Returns(new ClaimDTO { Id = 7, Role = Role.Learner });
        unitOfWork.SetupGet(unit => unit.MockTests).Returns(mockTests.Object);
        unitOfWork.SetupGet(unit => unit.MockTestAttempts).Returns(attempts.Object);
        unitOfWork.SetupGet(unit => unit.MockTestAnswers).Returns(answers.Object);
        unitOfWork.SetupGet(unit => unit.MockTestSectionResults).Returns(sectionResults.Object);
        unitOfWork
            .Setup(unit => unit.ExecuteInTransactionAsync(
                It.IsAny<Func<CancellationToken, Task>>(), It.IsAny<CancellationToken>()))
            .Returns((Func<CancellationToken, Task> operation, CancellationToken token) => operation(token));

        var fixture = new Fixture(
            new MockTestService(unitOfWork.Object, claims.Object),
            unitOfWork,
            mockTests,
            attempts,
            answers,
            sectionResults,
            test,
            attempt);

        mockTests
            .Setup(repository => repository.GetWithSectionsAndQuestionsAsync(
                test.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(test);
        attempts
            .Setup(repository => repository.FindAsync(
                It.IsAny<Expression<Func<MockTestAttempt, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        answers
            .Setup(repository => repository.FindAsync(
                It.IsAny<Expression<Func<MockTestAnswer, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        attempts
            .Setup(repository => repository.AddAsync(
                It.IsAny<MockTestAttempt>(), It.IsAny<CancellationToken>()))
            .Callback((MockTestAttempt entity, CancellationToken _) => entity.Id = attempt.Id)
            .Returns((MockTestAttempt entity, CancellationToken _) => Task.FromResult(entity));
        answers
            .Setup(repository => repository.AddRangeAsync(
                It.IsAny<IEnumerable<MockTestAnswer>>(), It.IsAny<CancellationToken>()))
            .Callback((IEnumerable<MockTestAnswer> entities, CancellationToken _) =>
                fixture.AddedSnapshots.AddRange(entities))
            .Returns(Task.CompletedTask);
        unitOfWork
            .Setup(unit => unit.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Callback(() => fixture.SaveCount++)
            .ReturnsAsync(1);
        sectionResults
            .Setup(repository => repository.AddRangeAsync(
                It.IsAny<IEnumerable<MockTestSectionResult>>(), It.IsAny<CancellationToken>()))
            .Callback((IEnumerable<MockTestSectionResult> entities, CancellationToken _) =>
            {
                fixture.SectionResultWriteCount++;
                fixture.AddedSectionResults.AddRange(entities);
            })
            .Returns(Task.CompletedTask);

        return fixture;
    }

    private static MockTest BuildTest()
    {
        var test = new MockTest { Id = 10, Title = "N4", TimeLimitMinutes = 30 };
        var vocabulary = new LanguageSkill { Id = 1, Name = "Vocabulary" };
        var reading = new LanguageSkill { Id = 2, Name = "Reading" };
        var secondSection = new MockTestSection
        {
            Id = 102,
            Title = "Reading",
            SortOrder = 2,
            MockTestId = test.Id,
            MockTest = test,
            LanguageSkill = reading,
            Questions = [],
        };
        var firstSection = new MockTestSection
        {
            Id = 101,
            Title = "Vocabulary",
            SortOrder = 1,
            MockTestId = test.Id,
            MockTest = test,
            LanguageSkill = vocabulary,
            Questions = [],
        };

        firstSection.Questions =
        [
            BuildQuestion(12, 2, firstSection, (122, 2, true), (121, 1, false)),
            BuildQuestion(11, 1, firstSection, (112, 2, true), (111, 1, false)),
        ];
        secondSection.Questions =
        [
            BuildQuestion(21, 1, secondSection, (212, 2, false), (211, 1, true)),
        ];
        test.Sections = [secondSection, firstSection];
        return test;
    }

    private static MockQuestion BuildQuestion(
        int id,
        int order,
        MockTestSection section,
        params (int Id, int Order, bool IsCorrect)[] options)
    {
        var question = new MockQuestion
        {
            Id = id,
            QuestionText = $"Question {id}",
            SortOrder = order,
            MockTestSectionId = section.Id,
            MockTestSection = section,
        };
        question.Options = options.Select(option => new MockQuestionOption
        {
            Id = option.Id,
            OptionText = $"Option {option.Id}",
            SortOrder = option.Order,
            IsCorrect = option.IsCorrect,
            MockQuestionId = question.Id,
            MockQuestion = question,
        }).ToList();
        return question;
    }

    private sealed class Fixture(
        MockTestService service,
        Mock<IUnitOfWork> unitOfWork,
        Mock<IMockTestRepository> mockTests,
        Mock<IMockTestAttemptRepository> mockTestAttempts,
        Mock<IMockTestAnswerRepository> mockTestAnswers,
        Mock<IMockTestSectionResultRepository> mockTestSectionResults,
        MockTest test,
        MockTestAttempt attempt)
    {
        public MockTestService Service { get; } = service;
        public Mock<IUnitOfWork> UnitOfWork { get; } = unitOfWork;
        public Mock<IMockTestRepository> MockTests { get; } = mockTests;
        public Mock<IMockTestAttemptRepository> MockTestAttempts { get; } = mockTestAttempts;
        public Mock<IMockTestAnswerRepository> MockTestAnswers { get; } = mockTestAnswers;
        public Mock<IMockTestSectionResultRepository> MockTestSectionResults { get; } = mockTestSectionResults;
        public MockTest Test { get; } = test;
        public MockTestAttempt Attempt { get; } = attempt;
        public List<MockTestAnswer> AddedSnapshots { get; } = [];
        public List<MockTestSectionResult> AddedSectionResults { get; } = [];
        public List<MockTestAnswer> SubmissionSnapshots { get; private set; } = [];
        public int SaveCount { get; set; }
        public int SectionResultWriteCount { get; set; }

        public MockQuestion QuestionById(int questionId) => Test.Sections!
            .SelectMany(section => section.Questions!)
            .Single(question => question.Id == questionId);

        public MockTestAnswer SnapshotByQuestionId(int questionId) =>
            SubmissionSnapshots.Single(answer => answer.MockQuestionId == questionId);

        public SubmitAttemptRequest ValidSubmission() => new()
        {
            Answers =
            [
                new AnswerSubmissionItem { QuestionId = 11, SelectedOptionId = 112 },
                new AnswerSubmissionItem { QuestionId = 12, SelectedOptionId = 121 },
                new AnswerSubmissionItem { QuestionId = 21, SelectedOptionId = null },
            ],
        };

        public void ConfigureSubmission(
            int ownerId = 7,
            bool completed = false,
            DateTime? startedAt = null)
        {
            Attempt.UserId = ownerId;
            Attempt.IsCompleted = completed;
            Attempt.StartedAt = startedAt ?? DateTime.UtcNow.AddMinutes(-1);
            SubmissionSnapshots = Test.Sections!
                .SelectMany(section => section.Questions!)
                .Select(question => new MockTestAnswer
                {
                    Id = 1_000 + question.Id,
                    MockTestAttemptId = Attempt.Id,
                    MockTestAttempt = Attempt,
                    MockQuestionId = question.Id,
                    MockQuestion = question,
                    SelectedOptionId = null,
                    IsCorrect = false,
                })
                .ToList();

            MockTestAttempts
                .Setup(repository => repository.GetForSubmissionAsync(
                    Attempt.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Attempt);
            MockTestAnswers
                .Setup(repository => repository.GetByAttemptIdAsync(
                    Attempt.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(SubmissionSnapshots);
        }

        public void ConfigureSummary()
        {
            MockTestAttempts
                .Setup(repository => repository.GetByIdAsync(
                    Attempt.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Attempt);
            MockTestSectionResults
                .Setup(repository => repository.GetByAttemptIdAsync(
                    Attempt.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(
                [
                    new MockTestSectionResult
                    {
                        MockTestAttemptId = Attempt.Id,
                        MockTestSectionId = Test.Sections![1].Id,
                        MockTestSection = Test.Sections[1],
                        CorrectCount = 1,
                        TotalCount = 2,
                        ScorePercent = 50,
                    },
                ]);
        }

        public void ConfigureDetailedResult()
        {
            ConfigureSubmission(completed: true);
            Attempt.TotalScore = 66.67;
            MockTestAttempts
                .Setup(repository => repository.GetByIdAsync(
                    Attempt.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Attempt);
        }
    }
}
