using Microsoft.EntityFrameworkCore;
using RikiPath.Domain.Entities;
using RikiPath.Infrastructure;

namespace RikiPath.Tests;

public class TopicHomeworkRelationshipTests
{
    [Fact]
    public async Task Topic_groups_lessons_and_homework_can_share_multiple_lessons()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var context = new AppDbContext(options);
        var topic = new Topic { Title = "Greetings" };
        var first = new Lesson { Title = "Introduction", Topic = topic };
        var second = new Lesson { Title = "Conversation", Topic = topic };
        var assignment = new HomeworkAssignment { Title = "Practice both lessons" };
        var other = new HomeworkAssignment { Title = "Repeat introduction" };
        context.HomeworkAssignmentLessons.AddRange(
            new HomeworkAssignmentLesson { HomeworkAssignment = assignment, Lesson = first },
            new HomeworkAssignmentLesson { HomeworkAssignment = assignment, Lesson = second },
            new HomeworkAssignmentLesson { HomeworkAssignment = other, Lesson = first });
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var savedTopic = await context.Topics.Include(x => x.Lessons).SingleAsync();
        Assert.Equal(2, savedTopic.Lessons.Count);
        var savedAssignment = await context.HomeworkAssignments
            .Include(x => x.HomeworkAssignmentLessons).SingleAsync(x => x.Id == assignment.Id);
        Assert.Equal(2, savedAssignment.HomeworkAssignmentLessons.Count);
        Assert.Equal(2, await context.HomeworkAssignmentLessons.CountAsync(x => x.LessonId == first.Id));
    }

    [Fact]
    public void Join_key_prevents_duplicate_links_and_topic_is_optional_for_existing_lessons()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        using var context = new AppDbContext(options);
        var link = context.Model.FindEntityType(typeof(HomeworkAssignmentLesson))!;
        Assert.Equal(new[] { "HomeworkAssignmentId", "LessonId" },
            link.FindPrimaryKey()!.Properties.Select(x => x.Name));
        var lesson = context.Model.FindEntityType(typeof(Lesson))!;
        Assert.True(lesson.FindProperty(nameof(Lesson.TopicId))!.IsNullable);
        Assert.Null(context.Model.FindEntityType(typeof(HomeworkAssignment))!.FindProperty("LessonId"));
    }
}
