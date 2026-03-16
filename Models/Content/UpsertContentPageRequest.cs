namespace pqy_server.Models.Content
{
    public class UpsertContentPageRequest
    {
        public string Title { get; set; } = string.Empty;
        public string? ContentHtml { get; set; }
        public string? ContentJson { get; set; }
        public bool IsPublished { get; set; } = true;
    }
}
