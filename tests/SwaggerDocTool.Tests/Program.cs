using System.Text;
using System.Reflection;
using Markdig;
using Microsoft.OpenApi.Readers;
using SwaggerDocTool.Core;
using SwaggerDocTool.Renderers;

namespace SwaggerDocTool.Tests;

internal static class Program
{
    public static int Main()
    {
        var tests = new (string Name, Action Run)[]
        {
            ("SwaggerParser_ConvertsMetadataAndEndpoints", SwaggerParser_ConvertsMetadataAndEndpoints),
            ("SchemaParser_FlattensNestedObjectsAndArrays", SchemaParser_FlattensNestedObjectsAndArrays),
            ("MarkdownRenderer_RendersExpectedSections", MarkdownRenderer_RendersExpectedSections),
            ("MarkdownRenderer_PreservesBreakMarkupInTableCells", MarkdownRenderer_PreservesBreakMarkupInTableCells),
            ("PdfRenderer_PreservesEndpointListItems", PdfRenderer_PreservesEndpointListItems),
            ("Program_AllFormat_CreatesExpectedFiles", Program_AllFormat_CreatesExpectedFiles),
            ("Program_InvalidFormat_ReturnsNonZero", Program_InvalidFormat_ReturnsNonZero)
        };

        var failures = new List<string>();

        foreach (var test in tests)
        {
            try
            {
                test.Run();
                Console.WriteLine($"PASS {test.Name}");
            }
            catch (Exception exception)
            {
                failures.Add($"{test.Name}: {exception.Message}");
                Console.Error.WriteLine($"FAIL {test.Name}");
                Console.Error.WriteLine(exception.Message);
            }
        }

        if (failures.Count == 0)
        {
            Console.WriteLine($"Passed {tests.Length} tests.");
            return 0;
        }

        Console.Error.WriteLine($"Failed {failures.Count} of {tests.Length} tests.");
        return 1;
    }

    private static void SwaggerParser_ConvertsMetadataAndEndpoints()
    {
        using var stream = CreateSampleOpenApiStream();
        var openApiDocument = new OpenApiStreamReader().Read(stream, out _);
        var document = SwaggerParser.Parse(openApiDocument);

        AssertEqual("Sample API", document.Title, "Title should be parsed.");
        AssertEqual("v1", document.Version, "Version should be parsed.");
        AssertEqual("Sample description", document.Description, "Description should be normalized.");
        AssertEqual(2, document.Endpoints.Count, "Expected two endpoints.");

        var listUsers = document.Endpoints.Single(endpoint => endpoint.Method == "GET");
        AssertEqual("Users", listUsers.Tag, "Tag should be parsed.");
        AssertEqual("/users", listUsers.Path, "Path should be parsed.");
        AssertEqual(1, listUsers.Parameters.Count, "Expected one query parameter.");
        AssertEqual(1, listUsers.Responses.Count, "Expected one response.");
        AssertEqual("application/json", listUsers.Responses[0].ContentType, "Response content type should be preserved.");
    }

    private static void SchemaParser_FlattensNestedObjectsAndArrays()
    {
        using var stream = CreateSampleOpenApiStream();
        var openApiDocument = new OpenApiStreamReader().Read(stream, out _);
        var createUser = openApiDocument.Paths["/users"].Operations.Values.Single(operation => operation.Summary == "Create user");
        var schema = createUser.RequestBody!.Content["application/json"].Schema;
        var fields = SchemaParser.ParseFields(schema, openApiDocument);

        AssertTrue(fields.Any(field => field.Name == "name" && field.Required), "Required scalar field should be present.");
        AssertTrue(fields.Any(field => field.Name == "profile" && field.Type == "object"), "Nested object field should be present.");
        AssertTrue(fields.Any(field => field.Name == "profile.email" && field.Type == "string(email)"), "Nested scalar field should be flattened.");
        AssertTrue(fields.Any(field => field.Name == "roles" && field.Type == "array<string>"), "Array field should be present.");
        AssertTrue(fields.Any(field => field.Name == "addresses[].street" && field.Required), "Nested array object field should be flattened.");
    }

    private static void MarkdownRenderer_RendersExpectedSections()
    {
        using var stream = CreateSampleOpenApiStream();
        var openApiDocument = new OpenApiStreamReader().Read(stream, out _);
        var document = SwaggerParser.Parse(openApiDocument);
        var renderer = new MarkdownRenderer();
        var markdown = renderer.RenderToString(document);

        AssertContains("# API Documentation", markdown, "Markdown title should exist.");
        AssertContains("## 1. Users", markdown, "Markdown group heading should exist.");
        AssertContains("### 1.1 List users", markdown, "Endpoint heading should exist.");
        AssertContains("| Name | Location | Type | Required | Description |", markdown, "Parameter table should exist.");
        AssertContains("| addresses[].street | string | Yes | None |", markdown, "Flattened field row should exist.");
        AssertContains("- Content Type: application/json", markdown, "Content type should be rendered.");
    }

    private static void MarkdownRenderer_PreservesBreakMarkupInTableCells()
    {
        using var stream = CreateSampleOpenApiStream();
        var openApiDocument = new OpenApiStreamReader().Read(stream, out _);
        var document = SwaggerParser.Parse(openApiDocument);
        var renderer = new MarkdownRenderer();
        var markdown = renderer.RenderToString(document);

        AssertContains("Business code.<br/>200: success.<br/>400: Bad Request.<br/>500: Internal Server Errors.", markdown, "Markdown should preserve <br/> markers in table cells.");
    }

    private static void PdfRenderer_PreservesEndpointListItems()
    {
        using var stream = CreateSampleOpenApiStream();
        var openApiDocument = new OpenApiStreamReader().Read(stream, out _);
        var document = SwaggerParser.Parse(openApiDocument);
        var markdownRenderer = new MarkdownRenderer();
        var pdfRenderer = new PdfRenderer(markdownRenderer);
        var markdown = markdownRenderer.RenderToString(document);

        var pipelineField = typeof(PdfRenderer).GetField("_markdownPipeline", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Failed to locate PDF markdown pipeline.");
        var pipeline = (MarkdownPipeline)pipelineField.GetValue(pdfRenderer)!;
        var markdownDocument = Markdown.Parse(markdown, pipeline);

        var toBlocksMethod = typeof(PdfRenderer).GetMethod("ToBlocks", BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Failed to locate PDF block converter.");
        var blocks = ((IEnumerable<object>)toBlocksMethod.Invoke(null, new object[] { markdownDocument })!).ToList();

        var listItemTexts = blocks
            .Where(block => block.GetType().Name == "PdfListItemBlock")
            .Select(block => (string)(block.GetType().GetProperty("Text")?.GetValue(block) ?? ""))
            .ToList();

        AssertTrue(listItemTexts.Contains("Endpoint: /users"), "PDF block conversion should preserve endpoint list items.");
        AssertTrue(listItemTexts.Contains("Method: GET"), "PDF block conversion should preserve method list items.");
    }

    private static void Program_AllFormat_CreatesExpectedFiles()
    {
        var tempDirectory = CreateTempDirectory();

        try
        {
            var inputPath = Path.Combine(tempDirectory, "sample-openapi.json");
            var outputDirectory = Path.Combine(tempDirectory, "docs");
            File.WriteAllText(inputPath, SampleOpenApiJson, new UTF8Encoding(false));

            var exitCode = InvokeCli(new[]
            {
                inputPath,
                "--format",
                "all",
                "--output",
                outputDirectory
            });

            AssertEqual(0, exitCode, "CLI should succeed.");
            AssertTrue(File.Exists(Path.Combine(outputDirectory, "sample-openapi.md")), "Markdown output should exist.");
            AssertTrue(File.Exists(Path.Combine(outputDirectory, "sample-openapi.docx")), "DOCX output should exist.");
            AssertTrue(File.Exists(Path.Combine(outputDirectory, "sample-openapi.pdf")), "PDF output should exist.");
            AssertTrue(new FileInfo(Path.Combine(outputDirectory, "sample-openapi.pdf")).Length > 0, "PDF output should be non-empty.");
        }
        finally
        {
            DeleteDirectory(tempDirectory);
        }
    }

    private static void Program_InvalidFormat_ReturnsNonZero()
    {
        var tempDirectory = CreateTempDirectory();

        try
        {
            var inputPath = Path.Combine(tempDirectory, "sample-openapi.json");
            File.WriteAllText(inputPath, SampleOpenApiJson, new UTF8Encoding(false));

            var exitCode = InvokeCli(new[]
            {
                inputPath,
                "--format",
                "html",
                "--output",
                Path.Combine(tempDirectory, "api.html")
            });

            AssertTrue(exitCode != 0, "CLI should reject unsupported formats.");
        }
        finally
        {
            DeleteDirectory(tempDirectory);
        }
    }

    private static MemoryStream CreateSampleOpenApiStream()
    {
        return new MemoryStream(Encoding.UTF8.GetBytes(SampleOpenApiJson));
    }

    private static string CreateTempDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "SwaggerDocToolTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }

    private static void DeleteDirectory(string directory)
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, true);
        }
    }

    private static int InvokeCli(string[] args)
    {
        var originalOut = Console.Out;
        var originalError = Console.Error;

        try
        {
            using var stdout = new StringWriter();
            using var stderr = new StringWriter();
            Console.SetOut(stdout);
            Console.SetError(stderr);
            return SwaggerDocTool.Program.Main(args);
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
        }
    }

    private static void AssertEqual<T>(T expected, T actual, string message)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new InvalidOperationException($"{message} Expected: {expected}; Actual: {actual}");
        }
    }

    private static void AssertTrue(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private static void AssertContains(string expectedSubstring, string actualText, string message)
    {
        if (!actualText.Contains(expectedSubstring, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"{message} Missing substring: {expectedSubstring}");
        }
    }

    private const string SampleOpenApiJson =
        """
        {
          "openapi": "3.0.1",
          "info": {
            "title": "Sample API",
            "version": "v1",
            "description": "<p>Sample description</p>"
          },
          "paths": {
            "/users": {
              "get": {
                "tags": [ "Users" ],
                "summary": "List users",
                "description": "Returns all users",
                "parameters": [
                  {
                    "name": "page",
                    "in": "query",
                    "description": "Page number",
                    "required": false,
                    "schema": { "type": "integer", "format": "int32" }
                  }
                ],
                "responses": {
                  "200": {
                    "description": "OK",
                    "content": {
                      "application/json": {
                        "schema": {
                          "type": "array",
                          "items": {
                            "type": "object",
                            "required": [ "id" ],
                            "properties": {
                              "id": { "type": "string" },
                              "name": { "type": "string", "description": "User name" },
                              "code": { "type": "string", "description": "Business code.\n200: success.\n400: Bad Request.\n500: Internal Server Errors." }
                            }
                          }
                        }
                      }
                    }
                  }
                }
              },
              "post": {
                "tags": [ "Users" ],
                "summary": "Create user",
                "requestBody": {
                  "content": {
                    "application/json": {
                      "schema": {
                        "type": "object",
                        "required": [ "name" ],
                        "properties": {
                          "name": { "type": "string" },
                          "profile": {
                            "type": "object",
                            "properties": {
                              "email": { "type": "string", "format": "email" }
                            }
                          },
                          "roles": {
                            "type": "array",
                            "items": { "type": "string" }
                          },
                          "addresses": {
                            "type": "array",
                            "items": {
                              "type": "object",
                              "required": [ "street" ],
                              "properties": {
                                "street": { "type": "string" }
                              }
                            }
                          }
                        }
                      }
                    }
                  }
                },
                "responses": {
                  "201": {
                    "description": "Created"
                  }
                }
              }
            }
          }
        }
        """;
}
