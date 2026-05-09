# swagger-doc-tool

A .NET 10 CLI tool for generating API documentation from Swagger / OpenAPI JSON. It parses the source spec into a shared document model and renders Markdown, DOCX, and PDF outputs.

## Usage

```powershell
SwaggerDocTool swagger.json --format docx --output API_Documentation.docx
SwaggerDocTool swagger.json --format md --output API_Documentation.md
SwaggerDocTool swagger.json --format pdf --output API_Documentation.pdf
SwaggerDocTool swagger.json --format all --output .\docs
```

## Output Rules

- `--format docx|md|pdf`: `--output` must be a file path.
- `--format all`: `--output` must be a directory path.
- `all` mode writes files using the input swagger filename.
  - example: `swagger.json` -> `swagger.md`, `swagger.docx`, `swagger.pdf`

## SwaggerDocPreview Docker

Build the preview site image from the repository root:

```powershell
docker build -t swagger-doc-preview .
```

Run the container:

```powershell
docker run --rm -p 5194:8080 swagger-doc-preview
```

Open `http://localhost:5194` to use the Swagger JSON preview and export page.
