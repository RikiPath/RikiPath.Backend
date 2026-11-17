using RikiPath.Infrastructure.Services;

namespace RikiPath.Tests;

public sealed class JlptExamSourceTests
{
    [Fact] public void Reads_explicit_dates_including_image_alt_text_without_guessing_next_year()
    {
        var rows = JlptExamSource.Parse("<img alt='First test: Sunday, July 5, 2026'><img alt='Second test: Sunday, December 6, 2026'>July 5, 2026");
        Assert.Equal([new DateOnly(2026, 7, 5), new DateOnly(2026, 12, 6)], rows.Select(x => x.Date));
        Assert.All(rows, x => Assert.Equal(JlptExamSource.SourceUrl, x.SourceUrl));
        Assert.Empty(JlptExamSource.Parse("First Sunday of July; dates to be confirmed"));
    }
}
