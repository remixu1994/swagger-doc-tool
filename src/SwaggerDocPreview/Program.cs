using System.Text;
using SwaggerDocPreview.Models;
using SwaggerDocPreview.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();
builder.Services.AddSingleton<ISwaggerPreviewStore, InMemorySwaggerPreviewStore>();
builder.Services.AddSingleton<SwaggerConversionService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseRouting();

app.UseAuthorization();

app.MapStaticAssets();
app.MapRazorPages()
   .WithStaticAssets();

app.MapGet("/", () => Results.Redirect("/swagger"));

app.MapPost("/swagger/preview", async (HttpContext context, ISwaggerPreviewStore store) =>
{
    var form = await context.Request.ReadFormAsync();
    string sourceName;
    string swaggerJson;

    if (form.Files["swaggerFile"] is { Length: > 0 } file)
    {
        using var reader = new StreamReader(file.OpenReadStream(), Encoding.UTF8, true);
        swaggerJson = await reader.ReadToEndAsync();
        sourceName = Path.GetFileNameWithoutExtension(file.FileName);
    }
    else
    {
        swaggerJson = form["swaggerJson"].ToString();
        sourceName = string.IsNullOrWhiteSpace(form["sourceName"].ToString()) ? "inline" : form["sourceName"].ToString();
    }

    if (string.IsNullOrWhiteSpace(swaggerJson))
    {
        return Results.BadRequest(new { error = "Provide swagger JSON." });
    }

    // Validate JSON by parsing
    try
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(swaggerJson));
        var doc = new Microsoft.OpenApi.Readers.OpenApiStreamReader().Read(stream, out var diagnostic);
        if (doc == null || (diagnostic.Errors.Count > 0 && doc.Paths.Count == 0))
        {
            return Results.BadRequest(new { error = "Invalid OpenAPI JSON." });
        }
    }
    catch
    {
        return Results.BadRequest(new { error = "Invalid JSON format." });
    }

    var previewId = store.Save(sourceName, swaggerJson);
    return Results.Redirect($"/swagger?preview={previewId}");
});

app.MapGet("/swagger/spec/{id}.json", (string id, ISwaggerPreviewStore store) =>
{
    var payload = store.TryGet(id);
    if (payload == null)
    {
        return Results.NotFound();
    }

    return Results.Text(payload.SwaggerJson, "application/json");
});

app.MapPost("/swagger/download", async (HttpContext context, ISwaggerPreviewStore store, SwaggerConversionService conversionService) =>
{
    var form = await context.Request.ReadFormAsync();
    var previewId = form["previewId"].ToString();
    var formatStr = form["format"].ToString();

    var payload = store.TryGet(previewId);
    if (payload == null)
    {
        return Results.NotFound();
    }

    if (!Enum.TryParse<DownloadFormat>(formatStr, true, out var format))
    {
        return Results.BadRequest(new { error = "Invalid format." });
    }

    var result = conversionService.Convert(payload.SwaggerJson, payload.SourceName, format);
    return Results.File(result.Data, result.ContentType, result.FileName);
});

app.MapPost("/swagger/download-all", async (HttpContext context, ISwaggerPreviewStore store, SwaggerConversionService conversionService) =>
{
    var form = await context.Request.ReadFormAsync();
    var previewId = form["previewId"].ToString();

    var payload = store.TryGet(previewId);
    if (payload == null)
    {
        return Results.NotFound();
    }

    var zipData = conversionService.GenerateAllZip(payload.SwaggerJson, payload.SourceName, out var zipFileName);
    return Results.File(zipData, "application/zip", zipFileName);
});

app.Run();
