using System.IO;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using SwaggerDocTool.Core;

namespace SwaggerDocTool.Renderers;

public sealed class DocxRenderer : IDocumentRenderer
{
    public string Format => "docx";

    public void Render(ApiDocument document, string outputPath)
    {
        EnsureParentDirectory(outputPath);
        using var stream = new FileStream(outputPath, FileMode.Create, FileAccess.ReadWrite, FileShare.None);
        Render(document, stream);
    }

    public void Render(ApiDocument document, Stream stream)
    {
        using var wordDocument = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document);
        var mainPart = wordDocument.AddMainDocumentPart();
        mainPart.Document = new Document();

        var body = new Body();
        body.Append(CreateTitle("API Documentation"));
        body.Append(CreateParagraph($"System Name: {ValueOrNone(document.Title)}"));
        body.Append(CreateParagraph($"Version: {ValueOrNone(document.Version)}"));
        body.Append(CreateParagraph($"Description: {ValueOrNone(document.Description)}"));
        body.Append(CreateParagraph(""));

        var groupedEndpoints = document.Endpoints.GroupBy(endpoint => string.IsNullOrWhiteSpace(endpoint.Tag) ? "default" : endpoint.Tag);
        var groupIndex = 1;

        foreach (var group in groupedEndpoints)
        {
            body.Append(CreateHeading1($"{groupIndex}. {group.Key}"));

            var endpointIndex = 1;

            foreach (var endpoint in group)
            {
                body.Append(CreateHeading2($"{groupIndex}.{endpointIndex} {ValueOrFallback(endpoint.Summary, endpoint.Path)}"));
                body.Append(CreateParagraph($"Endpoint: {endpoint.Path}"));
                body.Append(CreateParagraph($"Method: {endpoint.Method}"));
                body.Append(CreateParagraph($"Description: {ValueOrNone(endpoint.Description)}"));

                body.Append(CreateHeading3("Request Parameters"));
                AppendParameterBlock(body, endpoint.Parameters);

                body.Append(CreateHeading3("Request Body"));
                AppendRequestBodyBlock(body, endpoint.RequestBodies);

                body.Append(CreateHeading3("Responses"));
                AppendResponseBlock(body, endpoint.Responses);

                body.Append(CreateParagraph(""));
                endpointIndex++;
            }

            groupIndex++;
        }

        mainPart.Document.Append(body);
        mainPart.Document.Save();
    }

    private static void AppendParameterBlock(Body body, List<ApiParameter> parameters)
    {
        if (parameters.Count == 0)
        {
            body.Append(CreateParagraph("None"));
            return;
        }

        body.Append(CreateParameterTable(parameters));
    }

    private static void AppendRequestBodyBlock(Body body, List<ApiRequestBody> requestBodies)
    {
        if (requestBodies.Count == 0)
        {
            body.Append(CreateParagraph("None"));
            return;
        }

        foreach (var requestBody in requestBodies)
        {
            body.Append(CreateParagraph($"Content Type: {ValueOrNone(requestBody.ContentType)}", true));

            if (requestBody.Fields.Count == 0)
            {
                body.Append(CreateParagraph("None"));
                continue;
            }

            body.Append(CreateFieldTable(requestBody.Fields));
        }
    }

    private static void AppendResponseBlock(Body body, List<ApiResponse> responses)
    {
        if (responses.Count == 0)
        {
            body.Append(CreateParagraph("None"));
            return;
        }

        foreach (var response in responses)
        {
            body.Append(CreateParagraph($"Status Code: {ValueOrNone(response.StatusCode)}", true));
            body.Append(CreateParagraph($"Description: {ValueOrNone(response.Description)}"));
            body.Append(CreateParagraph($"Content Type: {ValueOrNone(response.ContentType)}"));

            if (response.Fields.Count == 0)
            {
                body.Append(CreateParagraph("None"));
                continue;
            }

            body.Append(CreateFieldTable(response.Fields));
        }
    }

    private static Paragraph CreateTitle(string text)
    {
        return CreateParagraph(text, true, "32", JustificationValues.Center);
    }

    private static Paragraph CreateHeading1(string text)
    {
        return CreateParagraph(text, true, "28");
    }

    private static Paragraph CreateHeading2(string text)
    {
        return CreateParagraph(text, true, "24");
    }

    private static Paragraph CreateHeading3(string text)
    {
        return CreateParagraph(text, true, "22");
    }

    private static Table CreateParameterTable(List<ApiParameter> parameters)
    {
        var table = CreateBaseTable();
        table.Append(CreateTableRow(true, "Name", "Location", "Type", "Required", "Description"));

        foreach (var parameter in parameters)
        {
            table.Append(CreateTableRow(
                false,
                parameter.Name,
                parameter.Location,
                parameter.Type,
                FormatRequired(parameter.Required),
                parameter.Description));
        }

        return table;
    }

    private static Table CreateFieldTable(List<ApiField> fields)
    {
        var table = CreateBaseTable();
        table.Append(CreateTableRow(true, "Name", "Type", "Required", "Description"));

        foreach (var field in fields)
        {
            table.Append(CreateTableRow(
                false,
                field.Name,
                field.Type,
                FormatRequired(field.Required),
                field.Description));
        }

        return table;
    }

    private static Table CreateBaseTable()
    {
        var table = new Table();

        table.AppendChild(new TableProperties(
            new TableBorders(
                new TopBorder { Val = BorderValues.Single, Size = 6 },
                new BottomBorder { Val = BorderValues.Single, Size = 6 },
                new LeftBorder { Val = BorderValues.Single, Size = 6 },
                new RightBorder { Val = BorderValues.Single, Size = 6 },
                new InsideHorizontalBorder { Val = BorderValues.Single, Size = 6 },
                new InsideVerticalBorder { Val = BorderValues.Single, Size = 6 }),
            new TableWidth
            {
                Type = TableWidthUnitValues.Pct,
                Width = "5000"
            }));

        return table;
    }

    private static TableRow CreateTableRow(bool isHeader, params string[] values)
    {
        var row = new TableRow();

        foreach (var value in values)
        {
            row.Append(CreateCell(value, isHeader));
        }

        return row;
    }

    private static TableCell CreateCell(string? value, bool isHeader = false)
    {
        var lines = TextNormalizeHelper.NormalizeToLines(value);
        var paragraph = new Paragraph();

        if (lines.Count == 0)
        {
            lines.Add("");
        }

        for (int index = 0; index < lines.Count; index++)
        {
            var runProperties = new RunProperties
            {
                RunFonts = new RunFonts
                {
                    Ascii = "Microsoft YaHei",
                    HighAnsi = "Microsoft YaHei",
                    EastAsia = "Microsoft YaHei"
                },
                FontSize = new FontSize { Val = "18" }
            };

            if (isHeader)
            {
                runProperties.Append(new Bold());
            }

            var run = new Run();
            run.Append(runProperties);
            run.Append(new Text(lines[index]) { Space = SpaceProcessingModeValues.Preserve });
            paragraph.Append(run);

            if (index < lines.Count - 1)
            {
                paragraph.Append(new Run(new Break()));
            }
        }

        var cell = new TableCell(paragraph);
        cell.Append(new TableCellProperties(
            new TableCellWidth { Type = TableWidthUnitValues.Auto },
            new TableCellVerticalAlignment { Val = TableVerticalAlignmentValues.Top }));

        return cell;
    }

    private static Paragraph CreateParagraph(
        string text,
        bool bold = false,
        string fontSize = "20",
        JustificationValues? justification = null)
    {
        var lines = TextNormalizeHelper.NormalizeToLines(text);
        var paragraph = new Paragraph();
        var paragraphProperties = new ParagraphProperties();

        if (justification != null)
        {
            paragraphProperties.Append(new Justification { Val = justification });
        }

        paragraph.Append(paragraphProperties);

        if (lines.Count == 0)
        {
            lines.Add("");
        }

        for (int index = 0; index < lines.Count; index++)
        {
            var runProperties = new RunProperties
            {
                FontSize = new FontSize { Val = fontSize },
                RunFonts = new RunFonts
                {
                    Ascii = "Microsoft YaHei",
                    HighAnsi = "Microsoft YaHei",
                    EastAsia = "Microsoft YaHei"
                }
            };

            if (bold)
            {
                runProperties.Append(new Bold());
            }

            var run = new Run();
            run.Append(runProperties);
            run.Append(new Text(lines[index]) { Space = SpaceProcessingModeValues.Preserve });
            paragraph.Append(run);

            if (index < lines.Count - 1)
            {
                paragraph.Append(new Run(new Break()));
            }
        }

        return paragraph;
    }

    private static string FormatRequired(bool required)
    {
        return required ? "Yes" : "No";
    }

    private static string ValueOrNone(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? "None" : value;
    }

    private static string ValueOrFallback(string? value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value;
    }

    private static void EnsureParentDirectory(string outputPath)
    {
        var directory = Path.GetDirectoryName(outputPath);

        if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }
}
