namespace SwaggerDocPreview.Models;

public sealed class PreviewDocumentResult
{
    public string SourceName { get; init; } = "";
    public string DocumentTitle { get; init; } = "";
    public string Markdown { get; init; } = "";
    public string PreviewHtml { get; init; } = "";
    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();
}
