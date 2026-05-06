using Microsoft.OpenApi.Models;

namespace SwaggerDocTool.Core;

public static class SchemaParser
{
    public static List<ApiField> ParseFields(
        OpenApiSchema? schema,
        OpenApiDocument document,
        string prefix = "",
        HashSet<string>? visited = null)
    {
        var result = new List<ApiField>();

        if (schema == null)
        {
            return result;
        }

        visited ??= new HashSet<string>(StringComparer.Ordinal);
        schema = ResolveSchema(schema, document, visited);

        if (schema.Type == "array" && schema.Items != null)
        {
            var itemSchema = ResolveSchema(schema.Items, document, visited);

            if (!string.IsNullOrWhiteSpace(prefix))
            {
                result.Add(new ApiField
                {
                    Name = prefix,
                    Type = GetSchemaType(schema),
                    Required = false,
                    Description = TextNormalizeHelper.Normalize(schema.Description)
                });
            }

            if (itemSchema.Properties.Any())
            {
                var nestedPrefix = string.IsNullOrWhiteSpace(prefix) ? "" : prefix + "[]";
                result.AddRange(ParseFields(itemSchema, document, nestedPrefix, visited));
                return result;
            }

            if (string.IsNullOrWhiteSpace(prefix))
            {
                result.Add(new ApiField
                {
                    Name = "(root)",
                    Type = GetSchemaType(schema),
                    Required = false,
                    Description = TextNormalizeHelper.Normalize(schema.Description)
                });
            }

            return result;
        }

        if (schema.Properties == null || schema.Properties.Count == 0)
        {
            result.Add(new ApiField
            {
                Name = string.IsNullOrWhiteSpace(prefix) ? "(root)" : prefix,
                Type = GetSchemaType(schema),
                Required = false,
                Description = TextNormalizeHelper.Normalize(schema.Description)
            });

            return result;
        }

        foreach (var property in schema.Properties)
        {
            var propertySchema = ResolveSchema(property.Value, document, visited);
            var propertyName = string.IsNullOrWhiteSpace(prefix)
                ? property.Key
                : $"{prefix}.{property.Key}";
            var isRequired = schema.Required != null && schema.Required.Contains(property.Key);

            if (propertySchema.Type == "object" && propertySchema.Properties.Any())
            {
                result.Add(new ApiField
                {
                    Name = propertyName,
                    Type = "object",
                    Required = isRequired,
                    Description = TextNormalizeHelper.Normalize(propertySchema.Description)
                });

                result.AddRange(ParseFields(propertySchema, document, propertyName, visited));
                continue;
            }

            if (propertySchema.Type == "array")
            {
                result.Add(new ApiField
                {
                    Name = propertyName,
                    Type = GetSchemaType(propertySchema),
                    Required = isRequired,
                    Description = TextNormalizeHelper.Normalize(propertySchema.Description)
                });

                if (propertySchema.Items != null)
                {
                    var itemSchema = ResolveSchema(propertySchema.Items, document, visited);

                    if (itemSchema.Properties.Any())
                    {
                        result.AddRange(ParseFields(itemSchema, document, propertyName + "[]", visited));
                    }
                }

                continue;
            }

            result.Add(new ApiField
            {
                Name = propertyName,
                Type = GetSchemaType(propertySchema),
                Required = isRequired,
                Description = TextNormalizeHelper.Normalize(propertySchema.Description)
            });
        }

        return result;
    }

    public static string GetSchemaType(OpenApiSchema? schema)
    {
        if (schema == null)
        {
            return "";
        }

        if (!string.IsNullOrEmpty(schema.Reference?.Id))
        {
            return schema.Reference.Id;
        }

        if (schema.Type == "array")
        {
            return $"array<{GetSchemaType(schema.Items)}>";
        }

        if (schema.Type == "object")
        {
            return "object";
        }

        if (!string.IsNullOrEmpty(schema.Format))
        {
            return $"{schema.Type}({schema.Format})";
        }

        return schema.Type ?? "";
    }

    private static OpenApiSchema ResolveSchema(
        OpenApiSchema schema,
        OpenApiDocument document,
        HashSet<string> visited)
    {
        if (schema.Reference == null || string.IsNullOrWhiteSpace(schema.Reference.Id))
        {
            return schema;
        }

        var referenceId = schema.Reference.Id;

        if (!visited.Add(referenceId))
        {
            return schema;
        }

        if (document.Components.Schemas.TryGetValue(referenceId, out var resolvedSchema))
        {
            return resolvedSchema;
        }

        return schema;
    }
}
