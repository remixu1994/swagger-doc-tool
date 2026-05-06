namespace SwaggerDocTool.Core;

public sealed class ApiDocument
{
    public string Title { get; set; } = "";
    public string Version { get; set; } = "";
    public string Description { get; set; } = "";
    public List<ApiEndpoint> Endpoints { get; set; } = new();
}

public sealed class ApiEndpoint
{
    public string Tag { get; set; } = "";
    public string Method { get; set; } = "";
    public string Path { get; set; } = "";
    public string Summary { get; set; } = "";
    public string Description { get; set; } = "";
    public List<ApiParameter> Parameters { get; set; } = new();
    public List<ApiRequestBody> RequestBodies { get; set; } = new();
    public List<ApiResponse> Responses { get; set; } = new();
}

public sealed class ApiParameter
{
    public string Name { get; set; } = "";
    public string Location { get; set; } = "";
    public string Type { get; set; } = "";
    public bool Required { get; set; }
    public string Description { get; set; } = "";
}

public sealed class ApiRequestBody
{
    public string ContentType { get; set; } = "";
    public List<ApiField> Fields { get; set; } = new();
}

public sealed class ApiResponse
{
    public string StatusCode { get; set; } = "";
    public string Description { get; set; } = "";
    public string ContentType { get; set; } = "";
    public List<ApiField> Fields { get; set; } = new();
}

public sealed class ApiField
{
    public string Name { get; set; } = "";
    public string Type { get; set; } = "";
    public bool Required { get; set; }
    public string Description { get; set; } = "";
}
