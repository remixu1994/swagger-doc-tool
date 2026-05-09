using System.Text;
using SwaggerDocTool.Core;

namespace SwaggerDocTool.Renderers;

public sealed class MarkdownRenderer : IDocumentRenderer
{
    public string Format => "md";

    public void Render(ApiDocument document, string outputPath)
    {
        EnsureParentDirectory(outputPath);
        File.WriteAllText(outputPath, RenderToString(document), Encoding.UTF8);
    }

    public string RenderToString(ApiDocument document)
    {
        var builder = new StringBuilder();

        builder.AppendLine("# API Documentation");
        builder.AppendLine();
        builder.AppendLine($"- System Name: {ValueOrNone(document.Title)}");
        builder.AppendLine($"- Version: {ValueOrNone(document.Version)}");
        builder.AppendLine($"- Description: {ValueOrNone(document.Description)}");
        builder.AppendLine();

        var groupedEndpoints = document.Endpoints.GroupBy(endpoint => string.IsNullOrWhiteSpace(endpoint.Tag) ? "default" : endpoint.Tag);
        var groupIndex = 1;

        foreach (var group in groupedEndpoints)
        {
            builder.AppendLine($"## {groupIndex}. {group.Key}");
            builder.AppendLine();

            var endpointIndex = 1;

            foreach (var endpoint in group)
            {
                builder.AppendLine($"### {groupIndex}.{endpointIndex} {ValueOrFallback(endpoint.Summary, endpoint.Path)}");
                builder.AppendLine();
                builder.AppendLine($"- Endpoint: `{endpoint.Path}`");
                builder.AppendLine($"- Method: `{endpoint.Method}`");
                builder.AppendLine($"- Description: {ValueOrNone(endpoint.Description)}");
                builder.AppendLine();

                builder.AppendLine("#### Request Parameters");
                builder.AppendLine();
                AppendParameterTable(builder, endpoint.Parameters);

                builder.AppendLine("#### Request Body");
                builder.AppendLine();
                AppendRequestBodies(builder, endpoint.RequestBodies);

                builder.AppendLine("#### Responses");
                builder.AppendLine();
                AppendResponses(builder, endpoint.Responses);

                endpointIndex++;
            }

            groupIndex++;
        }

        return builder.ToString().TrimEnd() + Environment.NewLine;
    }

    private static void AppendRequestBodies(StringBuilder builder, List<ApiRequestBody> requestBodies)
    {
        if (requestBodies.Count == 0)
        {
            builder.AppendLine("None");
            builder.AppendLine();
            return;
        }

        foreach (var requestBody in requestBodies)
        {
            builder.AppendLine($"- Content Type: {ValueOrNone(requestBody.ContentType)}");
            builder.AppendLine();
            AppendFieldTable(builder, requestBody.Fields);
        }
    }

    private static void AppendResponses(StringBuilder builder, List<ApiResponse> responses)
    {
        if (responses.Count == 0)
        {
            builder.AppendLine("None");
            builder.AppendLine();
            return;
        }

        foreach (var response in responses)
        {
            builder.AppendLine($"- Status Code: {ValueOrNone(response.StatusCode)}");
            builder.AppendLine($"- Description: {ValueOrNone(response.Description)}");
            builder.AppendLine($"- Content Type: {ValueOrNone(response.ContentType)}");
            builder.AppendLine();
            AppendFieldTable(builder, response.Fields);
        }
    }

    private static void AppendParameterTable(StringBuilder builder, List<ApiParameter> parameters)
    {
        if (parameters.Count == 0)
        {
            builder.AppendLine("None");
            builder.AppendLine();
            return;
        }

        builder.AppendLine("| Name | Location | Type | Required | Description |");
        builder.AppendLine("| --- | --- | --- | --- | --- |");

        foreach (var parameter in parameters)
        {
            builder.AppendLine(
                $"| {EscapeCell(parameter.Name)} | {EscapeCell(parameter.Location)} | {EscapeCell(parameter.Type)} | {FormatRequired(parameter.Required)} | {EscapeCell(parameter.Description)} |");
        }

        builder.AppendLine();
    }

    private static void AppendFieldTable(StringBuilder builder, List<ApiField> fields)
    {
        if (fields.Count == 0)
        {
            builder.AppendLine("None");
            builder.AppendLine();
            return;
        }

        builder.AppendLine("| Name | Type | Required | Description |");
        builder.AppendLine("| --- | --- | --- | --- |");

        foreach (var field in fields)
        {
            builder.AppendLine(
                $"| {EscapeCell(field.Name)} | {EscapeCell(field.Type)} | {FormatRequired(field.Required)} | {EscapeCell(field.Description)} |");
        }

        builder.AppendLine();
    }

    private static string EscapeCell(string? value)
    {
        return ValueOrNone(value)
            .Replace("|", "\\|", StringComparison.Ordinal)
            .Replace(Environment.NewLine, "<br/>", StringComparison.Ordinal)
            .Replace("\n", "<br/>", StringComparison.Ordinal);
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
