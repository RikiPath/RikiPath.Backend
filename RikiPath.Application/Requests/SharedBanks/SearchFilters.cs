namespace RikiPath.Application.Requests.SharedBanks
{
    public abstract class PagedFilterBase
    {
        public int? JlptLevelId { get; set; }
        public string? Keyword { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
    }

    public class VocabularySearchFilter : PagedFilterBase
    {
    }

    public class KanjiSearchFilter : PagedFilterBase
    {
        public int? MinStrokeCount { get; set; }
        public int? MaxStrokeCount { get; set; }
    }

    public class GrammarSearchFilter : PagedFilterBase
    {
    }
}
