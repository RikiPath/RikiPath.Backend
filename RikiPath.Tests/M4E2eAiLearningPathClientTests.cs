using System.Text.Json;
using RikiPath.Infrastructure.Clients;

namespace RikiPath.Tests;

public sealed class M4E2eAiLearningPathClientTests
{
    [Fact]
    public async Task Stub_returns_labeled_schedule_with_only_server_catalog_ids()
    {
        var client = new M4E2eAiLearningPathClient();
        const string prompt = """
            - id=3101 | Real N5 lesson | progress 0%
            - id=3102 | Real N5 lesson 2 | progress 0%
            Chia lộ trình thành ĐÚNG 2 NGÀY liên tục (day = 1 đến day = 2).
            """;

        var json = await client.GenerateLearningPathJsonAsync(
            userId: 11,
            prompt,
            CancellationToken.None);

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        Assert.StartsWith(
            "[M4E2E TEST STUB — NOT REAL AI OUTPUT]",
            root.GetProperty("summary").GetString());
        var schedules = root.GetProperty("dailySchedules").EnumerateArray().ToArray();
        Assert.Equal(2, schedules.Length);
        Assert.Equal(new[] { 1, 2 }, schedules.Select(day => day.GetProperty("day").GetInt32()));
        Assert.All(schedules, day =>
        {
            var id = day.GetProperty("lessonIds")[0].GetInt32();
            Assert.Contains(id, new[] { 3101, 3102 });
        });
    }

    [Theory]
    [InlineData("No plan-day count.\n- id=3101 | Lesson")]
    [InlineData("ĐÚNG 2 NGÀY liên tục but no catalog lesson")]
    public async Task Stub_fails_closed_when_prompt_lacks_required_server_data(string prompt)
    {
        var client = new M4E2eAiLearningPathClient();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.GenerateLearningPathJsonAsync(11, prompt, CancellationToken.None));
    }
}
