using RikiPath.Domain.Entities;
using RikiPath.Domain.Enums;

namespace RikiPath.Infrastructure.Repositories;

internal static class PublicContentQueryExtensions
{
    public static IQueryable<T> WherePubliclyVisible<T>(this IQueryable<T> query)
        where T : Base
        => query.Where(item => !item.IsDeleted);

    public static IQueryable<Vocabulary> WherePubliclyVisible(this IQueryable<Vocabulary> query)
        => query.Where(item => !item.IsDeleted && item.Status == ContentStatus.Published);

    public static IQueryable<Kanji> WherePubliclyVisible(this IQueryable<Kanji> query)
        => query.Where(item => !item.IsDeleted && item.Status == ContentStatus.Published);

    public static IQueryable<GrammarPattern> WherePubliclyVisible(this IQueryable<GrammarPattern> query)
        => query.Where(item => !item.IsDeleted && item.Status == ContentStatus.Published);

    public static IQueryable<Lesson> WherePubliclyVisible(this IQueryable<Lesson> query)
        => query.Where(item => !item.IsDeleted && item.Status == ContentStatus.Published);
}
