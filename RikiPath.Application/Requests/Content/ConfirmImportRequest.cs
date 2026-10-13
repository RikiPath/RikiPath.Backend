namespace RikiPath.Application.Requests.Content
{
    public class ConfirmImportRequest
    {
        public string? SourceFileName { get; set; }
        public List<ConfirmImportRowRequest> Rows { get; set; } = new();
    }
}
