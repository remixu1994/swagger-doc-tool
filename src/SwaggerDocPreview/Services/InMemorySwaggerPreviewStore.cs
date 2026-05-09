using System.Collections.Concurrent;
using SwaggerDocPreview.Models;

namespace SwaggerDocPreview.Services;

public sealed class InMemorySwaggerPreviewStore : ISwaggerPreviewStore
{
    private readonly ConcurrentDictionary<string, SwaggerPreviewPayload> _store = new();
    private readonly ConcurrentQueue<string> _order = new();

    public string Save(string sourceName, string swaggerJson)
    {
        var previewId = Guid.NewGuid().ToString("N")[..12];
        var payload = new SwaggerPreviewPayload
        {
            PreviewId = previewId,
            SourceName = sourceName,
            SwaggerJson = swaggerJson,
            CreatedAt = DateTimeOffset.UtcNow
        };

        _store[previewId] = payload;
        _order.Enqueue(previewId);

        while (_order.Count > 100)
        {
            if (_order.TryDequeue(out var oldest))
            {
                _store.TryRemove(oldest, out _);
            }
        }

        return previewId;
    }

    public SwaggerPreviewPayload? TryGet(string previewId)
    {
        return _store.TryGetValue(previewId, out var payload) ? payload : null;
    }
}
