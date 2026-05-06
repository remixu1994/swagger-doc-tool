using SwaggerDocTool.Core;

namespace SwaggerDocTool.Renderers;

internal interface IDocumentRenderer
{
    string Format { get; }

    void Render(ApiDocument document, string outputPath);
}
