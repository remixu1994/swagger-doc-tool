using Microsoft.OpenApi.Readers;
using SwaggerDocTool.Core;
using SwaggerDocTool.Renderers;

namespace SwaggerDocTool;

internal static class Program
{
    public static int Main(string[] args)
    {
        if (!TryParseOptions(args, out var options, out var errorMessage))
        {
            Console.Error.WriteLine(errorMessage);
            PrintUsage();
            return 1;
        }

        if (!File.Exists(options.InputPath))
        {
            Console.Error.WriteLine($"Input file does not exist: {options.InputPath}");
            return 1;
        }

        try
        {
            using var stream = File.OpenRead(options.InputPath);
            var openApiDocument = new OpenApiStreamReader().Read(stream, out var diagnostic);

            if (openApiDocument == null)
            {
                Console.Error.WriteLine("Failed to parse the OpenAPI document.");
                return 1;
            }

            if (diagnostic.Errors.Count > 0 && openApiDocument.Paths.Count == 0)
            {
                foreach (var error in diagnostic.Errors)
                {
                    Console.Error.WriteLine($"OpenAPI parse error: {error.Message}");
                }

                return 1;
            }

            var apiDocument = SwaggerParser.Parse(openApiDocument);
            var generatedFiles = GenerateOutputs(apiDocument, options);

            foreach (var generatedFile in generatedFiles)
            {
                Console.WriteLine($"Generated: {generatedFile}");
            }

            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception.Message);
            return 1;
        }
    }

    private static List<string> GenerateOutputs(ApiDocument document, CommandLineOptions options)
    {
        var markdownRenderer = new MarkdownRenderer();
        var renderers = new Dictionary<OutputFormat, IDocumentRenderer>
        {
            [OutputFormat.Docx] = new DocxRenderer(),
            [OutputFormat.Md] = markdownRenderer,
            [OutputFormat.Pdf] = new PdfRenderer(markdownRenderer)
        };

        if (options.Format == OutputFormat.All)
        {
            EnsureOutputDirectory(options.OutputPath);

            var generatedFiles = new List<string>();

            foreach (var item in AllOutputFiles(options.InputPath, options.OutputPath))
            {
                renderers[item.Format].Render(document, item.Path);
                generatedFiles.Add(item.Path);
            }

            return generatedFiles;
        }

        EnsureSingleFileOutputPath(options.OutputPath);
        renderers[options.Format].Render(document, options.OutputPath);
        return new List<string> { options.OutputPath };
    }

    private static IEnumerable<(OutputFormat Format, string Path)> AllOutputFiles(string inputPath, string outputDirectory)
    {
        var baseName = Path.GetFileNameWithoutExtension(inputPath);

        if (string.IsNullOrWhiteSpace(baseName))
        {
            baseName = "api-documentation";
        }

        yield return (OutputFormat.Md, Path.Combine(outputDirectory, $"{baseName}.md"));
        yield return (OutputFormat.Docx, Path.Combine(outputDirectory, $"{baseName}.docx"));
        yield return (OutputFormat.Pdf, Path.Combine(outputDirectory, $"{baseName}.pdf"));
    }

    private static void EnsureOutputDirectory(string outputPath)
    {
        if (File.Exists(outputPath))
        {
            throw new InvalidOperationException("--output must be a directory path when --format all is used.");
        }

        Directory.CreateDirectory(outputPath);
    }

    private static void EnsureSingleFileOutputPath(string outputPath)
    {
        if (Directory.Exists(outputPath))
        {
            throw new InvalidOperationException("--output must be a file path when using docx, md, or pdf.");
        }

        var directory = Path.GetDirectoryName(outputPath);

        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }

    private static bool TryParseOptions(string[] args, out CommandLineOptions options, out string errorMessage)
    {
        options = new CommandLineOptions("", OutputFormat.Unknown, "");
        errorMessage = "";

        if (args.Length == 0)
        {
            errorMessage = "Missing required arguments.";
            return false;
        }

        var inputPath = "";
        var formatValue = "";
        var outputPath = "";

        for (var index = 0; index < args.Length; index++)
        {
            var argument = args[index];

            if (!argument.StartsWith("-", StringComparison.Ordinal))
            {
                if (!string.IsNullOrWhiteSpace(inputPath))
                {
                    errorMessage = $"Unexpected argument: {argument}";
                    return false;
                }

                inputPath = argument;
                continue;
            }

            switch (argument)
            {
                case "--format":
                    if (!TryReadOptionValue(args, ref index, out formatValue))
                    {
                        errorMessage = "Missing value for --format.";
                        return false;
                    }

                    break;
                case "--output":
                    if (!TryReadOptionValue(args, ref index, out outputPath))
                    {
                        errorMessage = "Missing value for --output.";
                        return false;
                    }

                    break;
                default:
                    errorMessage = $"Unknown option: {argument}";
                    return false;
            }
        }

        if (string.IsNullOrWhiteSpace(inputPath))
        {
            errorMessage = "Missing required inputPath.";
            return false;
        }

        if (!TryParseFormat(formatValue, out var format))
        {
            errorMessage = "Invalid or missing --format. Supported values: docx, md, pdf, all.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(outputPath))
        {
            errorMessage = "Missing required --output.";
            return false;
        }

        options = new CommandLineOptions(inputPath, format, outputPath);
        return true;
    }

    private static bool TryReadOptionValue(string[] args, ref int index, out string value)
    {
        value = "";

        if (index + 1 >= args.Length)
        {
            return false;
        }

        index++;
        value = args[index];
        return true;
    }

    private static bool TryParseFormat(string value, out OutputFormat format)
    {
        format = value.ToLowerInvariant() switch
        {
            "docx" => OutputFormat.Docx,
            "md" => OutputFormat.Md,
            "pdf" => OutputFormat.Pdf,
            "all" => OutputFormat.All,
            _ => OutputFormat.Unknown
        };

        return format != OutputFormat.Unknown;
    }

    private static void PrintUsage()
    {
        Console.Error.WriteLine("Usage:");
        Console.Error.WriteLine("  SwaggerDocTool <inputPath> --format <docx|md|pdf|all> --output <path>");
    }

    private sealed record CommandLineOptions(string InputPath, OutputFormat Format, string OutputPath);

    private enum OutputFormat
    {
        Unknown = 0,
        Docx = 1,
        Md = 2,
        Pdf = 3,
        All = 4
    }
}
