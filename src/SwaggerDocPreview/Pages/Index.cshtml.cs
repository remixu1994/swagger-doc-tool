using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SwaggerDocPreview.Services;

namespace SwaggerDocPreview.Pages;

public sealed class IndexModel : PageModel
{
    private readonly ISwaggerPreviewStore _store;

    public IndexModel(ISwaggerPreviewStore store)
    {
        _store = store;
    }

    [BindProperty]
    public string SwaggerJson { get; set; } = SampleJson;

    [BindProperty]
    public IFormFile? SwaggerFile { get; set; }

    public string? PreviewId { get; private set; }
    public string? SourceName { get; private set; }
    public string? ErrorMessage { get; private set; }

    public void OnGet()
    {
        var previewId = Request.Query["preview"].ToString();
        if (!string.IsNullOrEmpty(previewId))
        {
            var payload = _store.TryGet(previewId);
            if (payload != null)
            {
                PreviewId = payload.PreviewId;
                SourceName = payload.SourceName;
                SwaggerJson = payload.SwaggerJson;
            }
            else
            {
                ErrorMessage = "Preview not found or expired.";
            }
        }
    }

    private const string SampleJson =
        """
        {
          "openapi": "3.0.1",
          "info": {
            "title": "Sample API",
            "version": "v1",
            "description": "Paste your OpenAPI JSON above to preview."
          },
          "paths": {
            "/users": {
              "get": {
                "tags": [ "Users" ],
                "summary": "List users",
                "description": "Returns all users.",
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
                          "items": { "type": "object", "properties": { "id": { "type": "string" }, "name": { "type": "string" } } }
                        }
                      }
                    }
                  }
                }
              }
            }
          }
        }
        """;
}