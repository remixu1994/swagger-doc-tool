using System.Net;
using System.Text.RegularExpressions;

namespace SwaggerDocTool.Core;

public static class TextNormalizeHelper
{
    public static string Normalize(string? htmlText)
    {
        var lines = NormalizeToLines(htmlText);
        return lines.Count == 0 ? "" : string.Join(Environment.NewLine, lines);
    }

    public static List<string> NormalizeToLines(string? htmlText)
    {
        if (string.IsNullOrWhiteSpace(htmlText))
        {
            return new List<string>();
        }

        var text = WebUtility.HtmlDecode(htmlText);

        text = Regex.Replace(text, @"<\s*br\s*/?\s*>", "\n", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"<\s*/\s*br\s*>", "\n", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"<\s*/?\s*div\s*>", "\n", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"<\s*/?\s*p\s*>", "\n", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"<[^>]+>", "", RegexOptions.IgnoreCase);
        text = text.Replace("\r\n", "\n").Replace("\r", "\n");
        text = Regex.Replace(text, @"\n{3,}", "\n\n");

        return text
            .Split('\n', StringSplitOptions.TrimEntries)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToList();
    }
}
