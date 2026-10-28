using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using RikiPath.Application.IClients;

namespace RikiPath.Infrastructure.Clients;

/// <summary>
/// Deterministic, non-model response used only by the isolated M4E2E environment.
/// Every lesson ID is read from the server-built catalog prompt and the result is
/// labeled as a test stub so it cannot be mistaken for real model output.
/// </summary>
public sealed class M4E2eAiLearningPathClient : IAiLearningPathClient
{
    private static readonly Regex DayCountPattern = new(
        @"ĐÚNG\s+(?<days>\d+)\s+NGÀY liên tục",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
    private static readonly Regex LessonIdPattern = new(
        @"(?m)^-\s+id=(?<id>\d+)\s+\|",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public Task<string> GenerateLearningPathJsonAsync(
        int userId,
        string prompt,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var dayMatch = DayCountPattern.Match(prompt);
        if (!dayMatch.Success ||
            !int.TryParse(dayMatch.Groups["days"].Value, NumberStyles.None,
                CultureInfo.InvariantCulture, out var dayCount) ||
            dayCount <= 0)
        {
            throw new InvalidOperationException(
                "M4E2E AI stub requires the server's explicit plan-day count.");
        }

        var lessonIds = LessonIdPattern.Matches(prompt)
            .Select(match => int.Parse(match.Groups["id"].Value, CultureInfo.InvariantCulture))
            .Distinct()
            .ToArray();
        if (lessonIds.Length == 0)
        {
            throw new InvalidOperationException(
                "M4E2E AI stub requires at least one real lesson ID in the server catalog.");
        }

        var schedules = Enumerable.Range(1, dayCount)
            .Select(day => new
            {
                day,
                title = $"M4E2E test schedule — Day {day}",
                focus = "Deterministic test-only response; not a real AI recommendation.",
                lessonIds = new[] { lessonIds[(day - 1) % lessonIds.Length] },
            });

        var response = new
        {
            summary = "[M4E2E TEST STUB — NOT REAL AI OUTPUT] Deterministic integration-test response.",
            strengths = new[] { "M4E2E test data" },
            weaknesses = new[] { "Not assessed by an AI model" },
            feasibility = "Test-only deterministic response; no model was called.",
            focusTopics = new[] { "M4E2E deterministic fixture" },
            dailySchedules = schedules,
        };

        return Task.FromResult(JsonSerializer.Serialize(response));
    }
}
