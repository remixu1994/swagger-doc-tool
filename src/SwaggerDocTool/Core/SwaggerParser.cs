using Microsoft.OpenApi.Models;

namespace SwaggerDocTool.Core;

public static class SwaggerParser
{
    public static ApiDocument Parse(OpenApiDocument document)
    {
        var apiDocument = new ApiDocument
        {
            Title = TextNormalizeHelper.Normalize(document.Info?.Title),
            Version = TextNormalizeHelper.Normalize(document.Info?.Version),
            Description = TextNormalizeHelper.Normalize(document.Info?.Description)
        };

        foreach (var pathItem in document.Paths)
        {
            foreach (var operationItem in pathItem.Value.Operations)
            {
                var operation = operationItem.Value;

                apiDocument.Endpoints.Add(new ApiEndpoint
                {
                    Tag = operation.Tags.FirstOrDefault()?.Name ?? "default",
                    Method = operationItem.Key.ToString().ToUpperInvariant(),
                    Path = pathItem.Key,
                    Summary = TextNormalizeHelper.Normalize(operation.Summary),
                    Description = TextNormalizeHelper.Normalize(operation.Description),
                    Parameters = ParseParameters(operation.Parameters),
                    RequestBodies = ParseRequestBodies(operation.RequestBody, document),
                    Responses = ParseResponses(operation.Responses, document)
                });
            }
        }

        return apiDocument;
    }

    private static List<ApiParameter> ParseParameters(IList<OpenApiParameter> parameters)
    {
        var result = new List<ApiParameter>();

        foreach (var parameter in parameters)
        {
            result.Add(new ApiParameter
            {
                Name = parameter.Name ?? "",
                Location = parameter.In?.ToString() ?? "",
                Type = SchemaParser.GetSchemaType(parameter.Schema),
                Required = parameter.Required,
                Description = TextNormalizeHelper.Normalize(parameter.Description)
            });
        }

        return result;
    }

    private static List<ApiRequestBody> ParseRequestBodies(OpenApiRequestBody? requestBody, OpenApiDocument document)
    {
        var result = new List<ApiRequestBody>();

        if (requestBody == null)
        {
            return result;
        }

        if (requestBody.Content.Count == 0)
        {
            result.Add(new ApiRequestBody());
            return result;
        }

        foreach (var content in requestBody.Content)
        {
            result.Add(new ApiRequestBody
            {
                ContentType = content.Key,
                Fields = SchemaParser.ParseFields(content.Value.Schema, document)
            });
        }

        return result;
    }

    private static List<ApiResponse> ParseResponses(OpenApiResponses responses, OpenApiDocument document)
    {
        var result = new List<ApiResponse>();

        foreach (var responseItem in responses)
        {
            var response = responseItem.Value;

            if (response.Content.Count == 0)
            {
                result.Add(new ApiResponse
                {
                    StatusCode = responseItem.Key,
                    Description = TextNormalizeHelper.Normalize(response.Description)
                });

                continue;
            }

            foreach (var content in response.Content)
            {
                result.Add(new ApiResponse
                {
                    StatusCode = responseItem.Key,
                    Description = TextNormalizeHelper.Normalize(response.Description),
                    ContentType = content.Key,
                    Fields = SchemaParser.ParseFields(content.Value.Schema, document)
                });
            }
        }

        return result;
    }
}
