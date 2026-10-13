namespace RikiPath.Domain.Enums
{
    /// <summary>Kết quả so khớp 1 dòng Excel với dữ liệu hiện có trong DB (Step 1 - Dry Run).</summary>
    public enum ImportRowAction
    {
        New,
        Modified,
        Unchanged,
        Error,
    }
}
