using System.Text;
using Markdig;
using Microsoft.OpenApi.Readers;
using SwaggerDocPreview.Models;
using SwaggerDocTool.Core;
using SwaggerDocTool.Renderers;

namespace SwaggerDocPreview.Services;

public sealed class SwaggerPreviewService
{
    private readonly MarkdownRenderer _markdownRenderer = new();
    private readonly MarkdownPipeline _markdownPipeline = new MarkdownPipelineBuilder()
        .UsePipeTables()
        .Build();

    public PreviewDocumentResult BuildPreview(string swaggerJson, string sourceName)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(swaggerJson));
        var openApiDocument = new OpenApiStreamReader().Read(stream, out var diagnostic);

        if (openApiDocument == null)
        {
            throw new InvalidOperationException("Failed to parse the OpenAPI document.");
        }

        if (diagnostic.Errors.Count > 0 && openApiDocument.Paths.Count == 0)
        {
            throw new InvalidOperationException(string.Join(Environment.NewLine, diagnostic.Errors.Select(error => error.Message)));
        }

        var apiDocument = SwaggerParser.Parse(openApiDocument);
        var markdown = _markdownRenderer.RenderToString(apiDocument);
        var previewHtml = Markdown.ToHtml(markdown, _markdownPipeline);

        return new PreviewDocumentResult
        {
            SourceName = sourceName,
            DocumentTitle = string.IsNullOrWhiteSpace(apiDocument.Title) ? sourceName : apiDocument.Title,
            Markdown = markdown,
            PreviewHtml = previewHtml,
            Warnings = diagnostic.Errors.Select(error => error.Message).ToArray()
        };
    }
}
