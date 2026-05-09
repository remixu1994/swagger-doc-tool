using System.Text;
using System.Xml.Linq;
using SwaggerDocPreview.Models;
using SwaggerDocPreview.Services;

var builder = WebApplication.CreateBuilder(args);
var seoPageRoutes = new[]
{
    "/swagger-json-preview",
    "/swagger-json-export",
    "/openapi-json-export",
    "/swagger-to-markdown",
    "/swagger-to-pdf",
    "/swagger-to-docx",
    "/zh/swagger",
    "/zh/swagger-json-export",
    "/zh/openapi-json-export",
    "/zh/swagger-to-pdf"
};

builder.Services.AddRazorPages(options =>
{
    options.Conventions.AddPageRoute("/Index", "swagger");

    foreach (var route in seoPageRoutes)
    {
        options.Conventions.AddPageRoute("/Index", route.TrimStart('/'));
    }
});
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

app.MapGet("/sitemap.xml", (HttpContext context) =>
{
    var baseUrl = $"{context.Request.Scheme}://{context.Request.Host}";
    var sitemapPaths = new[] { "/", "/swagger" }.Concat(seoPageRoutes);
    var document = new XDocument(
        new XDeclaration("1.0", "utf-8", null),
        new XElement(
            XName.Get("urlset", "http://www.sitemaps.org/schemas/sitemap/0.9"),
            sitemapPaths.Select((path, index) => CreateSitemapUrl(
                baseUrl,
                path,
                "weekly",
                index == 0 ? "1.0" : "0.8"))));

    return Results.Text(document.ToString(SaveOptions.DisableFormatting), "application/xml", Encoding.UTF8);
});

app.MapGet("/robots.txt", (HttpContext context) =>
{
    var baseUrl = $"{context.Request.Scheme}://{context.Request.Host}";
    var robots = string.Join(
        "\n",
        "User-agent: *",
        "Allow: /",
        $"Sitemap: {baseUrl}/sitemap.xml",
        "");

    return Results.Text(robots, "text/plain", Encoding.UTF8);
});

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

static XElement CreateSitemapUrl(string baseUrl, string path, string changeFrequency, string priority)
{
    XNamespace sitemap = "http://www.sitemaps.org/schemas/sitemap/0.9";

    return new XElement(
        sitemap + "url",
        new XElement(sitemap + "loc", $"{baseUrl}{path}"),
        new XElement(sitemap + "changefreq", changeFrequency),
        new XElement(sitemap + "priority", priority));
}
