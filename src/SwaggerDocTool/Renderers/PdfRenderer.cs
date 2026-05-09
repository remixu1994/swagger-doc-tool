using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Markdig;
using Markdig.Extensions.Tables;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SwaggerDocTool.Core;

namespace SwaggerDocTool.Renderers;

public sealed class PdfRenderer : IDocumentRenderer
{
    private readonly MarkdownRenderer _markdownRenderer;
    private readonly MarkdownPipeline _markdownPipeline;

    public PdfRenderer(MarkdownRenderer markdownRenderer)
    {
        _markdownRenderer = markdownRenderer;
        _markdownPipeline = new MarkdownPipelineBuilder()
            .UsePipeTables()
            .Build();

        QuestPDF.Settings.License = LicenseType.Community;
    }

    public string Format => "pdf";

    public void Render(ApiDocument document, string outputPath)
    {
        EnsureParentDirectory(outputPath);

        var markdown = _markdownRenderer.RenderToString(document);
        var markdownDocument = Markdown.Parse(markdown, _markdownPipeline);
        var blocks = ToBlocks(markdownDocument);

        Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Margin(32);
                    page.Size(PageSizes.A4);
                    page.DefaultTextStyle(style => style.FontFamily("Arial").FontSize(10));
                    page.Content().Column(column =>
                    {
                        column.Spacing(8);

                        foreach (var block in blocks)
                        {
                            column.Item().Element(item => RenderBlock(item, block));
                        }
                    });
                });
            })
            .GeneratePdf(outputPath);
    }

    public void Render(ApiDocument document, Stream stream)
    {
        var markdown = _markdownRenderer.RenderToString(document);
        var markdownDocument = Markdown.Parse(markdown, _markdownPipeline);
        var blocks = ToBlocks(markdownDocument);

        Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Margin(32);
                    page.Size(PageSizes.A4);
                    page.DefaultTextStyle(style => style.FontFamily("Arial").FontSize(10));
                    page.Content().Column(column =>
                    {
                        column.Spacing(8);

                        foreach (var block in blocks)
                        {
                            column.Item().Element(item => RenderBlock(item, block));
                        }
                    });
                });
            })
            .GeneratePdf(stream);
    }

    private static IReadOnlyList<PdfBlock> ToBlocks(MarkdownDocument markdownDocument)
    {
        var blocks = new List<PdfBlock>();

        foreach (var block in markdownDocument)
        {
            switch (block)
            {
                case HeadingBlock headingBlock:
                    blocks.Add(new PdfHeadingBlock(headingBlock.Level, ExtractInlineText(headingBlock.Inline)));
                    break;
                case ParagraphBlock paragraphBlock:
                    blocks.Add(new PdfParagraphBlock(ExtractInlineText(paragraphBlock.Inline)));
                    break;
                case ListBlock listBlock:
                    blocks.AddRange(ToListBlocks(listBlock, 0));
                    break;
                case Table table:
                    blocks.Add(ToTableBlock(table));
                    break;
            }
        }

        return blocks;
    }

    private static PdfTableBlock ToTableBlock(Table table)
    {
        var rows = new List<IReadOnlyList<string>>();
        IReadOnlyList<string> header = Array.Empty<string>();

        foreach (var rowObject in table)
        {
            if (rowObject is not TableRow row)
            {
                continue;
            }

            var cells = new List<string>();

            foreach (var cellObject in row)
            {
                if (cellObject is not TableCell cell)
                {
                    continue;
                }

                cells.Add(ExtractTableCellText(cell));
            }

            if (row.IsHeader)
            {
                header = cells;
            }
            else
            {
                rows.Add(cells);
            }
        }

        return new PdfTableBlock(header, rows);
    }

    private static IReadOnlyList<PdfBlock> ToListBlocks(ListBlock listBlock, int level)
    {
        var blocks = new List<PdfBlock>();
        var index = 1;

        foreach (var itemObject in listBlock)
        {
            if (itemObject is not ListItemBlock item)
            {
                continue;
            }

            var marker = listBlock.IsOrdered ? $"{index}." : "•";

            foreach (var childBlock in item)
            {
                switch (childBlock)
                {
                    case ParagraphBlock paragraphBlock:
                        blocks.Add(new PdfListItemBlock(level, marker, ExtractInlineText(paragraphBlock.Inline)));
                        break;
                    case HeadingBlock headingBlock:
                        blocks.Add(new PdfListItemBlock(level, marker, ExtractInlineText(headingBlock.Inline)));
                        break;
                    case ListBlock nestedList:
                        blocks.AddRange(ToListBlocks(nestedList, level + 1));
                        break;
                }
            }

            index++;
        }

        return blocks;
    }

    private static string ExtractTableCellText(TableCell cell)
    {
        var builder = new StringBuilder();

        foreach (var block in cell)
        {
            switch (block)
            {
                case ParagraphBlock paragraphBlock:
                    AppendWithLineBreak(builder, ExtractInlineText(paragraphBlock.Inline));
                    break;
                case HeadingBlock headingBlock:
                    AppendWithLineBreak(builder, ExtractInlineText(headingBlock.Inline));
                    break;
            }
        }

        return builder.ToString().Trim();
    }

    private static void AppendWithLineBreak(StringBuilder builder, string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        if (builder.Length > 0)
        {
            builder.AppendLine();
        }

        builder.Append(text);
    }

    private static string ExtractInlineText(ContainerInline? inlineContainer)
    {
        if (inlineContainer == null)
        {
            return "";
        }

        var builder = new StringBuilder();

        for (Inline? inline = inlineContainer.FirstChild; inline != null; inline = inline.NextSibling)
        {
            switch (inline)
            {
                case LiteralInline literalInline:
                    builder.Append(literalInline.Content.ToString());
                    break;
                case LineBreakInline:
                    builder.AppendLine();
                    break;
                case CodeInline codeInline:
                    builder.Append(codeInline.Content);
                    break;
                case HtmlInline htmlInline:
                    AppendHtmlInline(builder, htmlInline.Tag);
                    break;
                case LinkInline linkInline:
                    builder.Append(ExtractInlineText(linkInline));
                    break;
                case EmphasisInline emphasisInline:
                    builder.Append(ExtractInlineText(emphasisInline));
                    break;
                case ContainerInline childContainer:
                    builder.Append(ExtractInlineText(childContainer));
                    break;
            }
        }

        return NormalizeExtractedText(builder.ToString());
    }

    private static void AppendHtmlInline(StringBuilder builder, string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return;
        }

        if (Regex.IsMatch(html, @"<\s*br\s*/?\s*>", RegexOptions.IgnoreCase))
        {
            builder.AppendLine();
        }
    }

    private static string NormalizeExtractedText(string text)
    {
        return text
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace("\r", "\n", StringComparison.Ordinal)
            .Trim();
    }

    private static void RenderBlock(IContainer container, PdfBlock block)
    {
        switch (block)
        {
            case PdfHeadingBlock heading:
                RenderHeading(container, heading);
                break;
            case PdfParagraphBlock paragraph:
                container.Text(paragraph.Text);
                break;
            case PdfListItemBlock listItem:
                RenderListItem(container, listItem);
                break;
            case PdfTableBlock table:
                RenderTable(container, table);
                break;
            default:
                container.Text(string.Empty);
                break;
        }
    }

    private static void RenderHeading(IContainer container, PdfHeadingBlock heading)
    {
        var fontSize = heading.Level switch
        {
            1 => 20,
            2 => 16,
            3 => 14,
            _ => 12
        };

        container.PaddingTop(heading.Level == 1 ? 0 : 8)
            .Text(heading.Text)
            .FontSize(fontSize)
            .SemiBold();
    }

    private static void RenderTable(IContainer container, PdfTableBlock table)
    {
        if (table.Header.Count == 0)
        {
            container.Text("None");
            return;
        }

        container.Table(tableDescriptor =>
        {
            tableDescriptor.ColumnsDefinition(columns =>
            {
                for (var index = 0; index < table.Header.Count; index++)
                {
                    columns.RelativeColumn();
                }
            });

            tableDescriptor.Header(header =>
            {
                foreach (var headerCell in table.Header)
                {
                    header.Cell().Element(ApplyHeaderCellStyle).Text(headerCell).SemiBold();
                }
            });

            foreach (var row in table.Rows)
            {
                for (var index = 0; index < table.Header.Count; index++)
                {
                    var cell = index < row.Count ? row[index] : "";
                    tableDescriptor.Cell().Element(ApplyBodyCellStyle).Text(string.IsNullOrWhiteSpace(cell) ? "None" : cell);
                }
            }
        });
    }

    private static IContainer ApplyHeaderCellStyle(IContainer container)
    {
        return container
            .Border(1)
            .Background(Colors.Grey.Lighten3)
            .Padding(4);
    }

    private static IContainer ApplyBodyCellStyle(IContainer container)
    {
        return container
            .Border(1)
            .Padding(4);
    }

    private static void RenderListItem(IContainer container, PdfListItemBlock listItem)
    {
        container
            .PaddingLeft(listItem.Level * 14)
            .Text($"{listItem.Marker} {listItem.Text}");
    }

    private static void EnsureParentDirectory(string outputPath)
    {
        var directory = Path.GetDirectoryName(outputPath);

        if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }

    private abstract record PdfBlock;

    private sealed record PdfHeadingBlock(int Level, string Text) : PdfBlock;

    private sealed record PdfParagraphBlock(string Text) : PdfBlock;

    private sealed record PdfListItemBlock(int Level, string Marker, string Text) : PdfBlock;

    private sealed record PdfTableBlock(IReadOnlyList<string> Header, IReadOnlyList<IReadOnlyList<string>> Rows) : PdfBlock;
}
