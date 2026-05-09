namespace SwaggerDocPreview.Models;

public sealed class SwaggerPreviewPayload
{
    public required string PreviewId { get; init; }
    public required string SourceName { get; init; }
    public required string SwaggerJson { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
}
