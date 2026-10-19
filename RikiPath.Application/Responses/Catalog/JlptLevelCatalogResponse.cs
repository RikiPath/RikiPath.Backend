namespace RikiPath.Application.Responses.Catalog
{
    public sealed class JlptLevelCatalogResponse
    {
        public int Id { get; init; }
        public string Code { get; init; } = string.Empty;
        public string? Description { get; init; }
        public int SortOrder { get; init; }
    }
}
