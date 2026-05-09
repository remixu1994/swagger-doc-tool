using System.IO;
using System.Text;
using Microsoft.OpenApi.Models;
using Microsoft.OpenApi.Readers;
using SwaggerDocPreview.Models;
using SwaggerDocTool.Core;
using SwaggerDocTool.Renderers;

namespace SwaggerDocPreview.Services;

public sealed class SwaggerConversionService
{
    private readonly MarkdownRenderer _markdownRenderer = new();

    public ConversionResult Convert(string swaggerJson, string sourceName, DownloadFormat format)
    {
        var openApiDocument = ParseJson(swaggerJson);
        var apiDocument = SwaggerParser.Parse(openApiDocument);

        return format switch
        {
            DownloadFormat.Md => GenerateMarkdown(apiDocument, sourceName),
            DownloadFormat.Docx => GenerateDocx(apiDocument, sourceName),
            DownloadFormat.Pdf => GeneratePdf(apiDocument, sourceName),
            _ => throw new ArgumentOutOfRangeException(nameof(format))
        };
    }

    public byte[] GenerateAllZip(string swaggerJson, string sourceName, out string zipFileName)
    {
        var openApiDocument = ParseJson(swaggerJson);
        var apiDocument = SwaggerParser.Parse(openApiDocument);

        var baseName = SanitizeFileName(sourceName);
        zipFileName = $"{baseName}.zip";

        using var zipStream = new MemoryStream();
        using (var archive = new System.IO.Compression.ZipArchive(zipStream, System.IO.Compression.ZipArchiveMode.Create, leaveOpen: true))
        {
            AddEntry(archive, $"{baseName}.md", GenerateMarkdown(apiDocument, sourceName).Data);
            AddEntry(archive, $"{baseName}.docx", GenerateDocx(apiDocument, sourceName).Data);
            AddEntry(archive, $"{baseName}.pdf", GeneratePdf(apiDocument, sourceName).Data);
        }

        return zipStream.ToArray();
    }

    private OpenApiDocument ParseJson(string swaggerJson)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(swaggerJson));
        var openApiDocument = new OpenApiStreamReader().Read(stream, out var diagnostic);

        if (openApiDocument == null || diagnostic.Errors.Count > 0 && openApiDocument.Paths.Count == 0)
        {
            throw new InvalidOperationException("Failed to parse the OpenAPI document.");
        }

        return openApiDocument;
    }

    private ConversionResult GenerateMarkdown(ApiDocument document, string sourceName)
    {
        var markdown = _markdownRenderer.RenderToString(document);
        var bytes = Encoding.UTF8.GetBytes(markdown);
        var fileName = $"{SanitizeFileName(sourceName)}.md";
        return new ConversionResult { Data = bytes, ContentType = "text/markdown", FileName = fileName };
    }

    private ConversionResult GenerateDocx(ApiDocument document, string sourceName)
    {
        using var stream = new MemoryStream();
        var renderer = new DocxRenderer();
        renderer.Render(document, stream);
        var fileName = $"{SanitizeFileName(sourceName)}.docx";
        return new ConversionResult { Data = stream.ToArray(), ContentType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document", FileName = fileName };
    }

    private ConversionResult GeneratePdf(ApiDocument document, string sourceName)
    {
        using var stream = new MemoryStream();
        var renderer = new PdfRenderer(_markdownRenderer);
        renderer.Render(document, stream);
        var fileName = $"{SanitizeFileName(sourceName)}.pdf";
        return new ConversionResult { Data = stream.ToArray(), ContentType = "application/pdf", FileName = fileName };
    }

    private static void AddEntry(System.IO.Compression.ZipArchive archive, string name, byte[] data)
    {
        var entry = archive.CreateEntry(name, System.IO.Compression.CompressionLevel.Optimal);
        using var entryStream = entry.Open();
        entryStream.Write(data, 0, data.Length);
    }

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return string.IsNullOrWhiteSpace(name) ? "swagger" : invalid.Aggregate(name, (current, c) => current.Replace(c, '_'));
    }
}
