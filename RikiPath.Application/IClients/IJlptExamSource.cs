namespace RikiPath.Application.IClients;

public sealed record ImportedJlptExam(DateOnly Date, string SourceUrl);
public interface IJlptExamSource
{
    Task<IReadOnlyList<ImportedJlptExam>> FetchAsync(CancellationToken ct);
}
