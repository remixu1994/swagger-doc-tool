using SwaggerDocPreview.Models;

namespace SwaggerDocPreview.Services;

public interface ISwaggerPreviewStore
{
    string Save(string sourceName, string swaggerJson);
    SwaggerPreviewPayload? TryGet(string previewId);
}
