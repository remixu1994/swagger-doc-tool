using SwaggerDocTool.Core;

namespace SwaggerDocTool.Renderers;

public interface IDocumentRenderer
{
    string Format { get; }

    void Render(ApiDocument document, string outputPath);
}
