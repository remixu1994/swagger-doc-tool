namespace SwaggerDocPreview.Models;

public sealed class ConversionResult
{
    public required byte[] Data { get; init; }
    public required string ContentType { get; init; }
    public required string FileName { get; init; }
}
