FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY src/SwaggerDocTool/SwaggerDocTool.csproj src/SwaggerDocTool/
COPY src/SwaggerDocPreview/SwaggerDocPreview.csproj src/SwaggerDocPreview/
RUN dotnet restore src/SwaggerDocPreview/SwaggerDocPreview.csproj

COPY src/ src/
RUN dotnet publish src/SwaggerDocPreview/SwaggerDocPreview.csproj \
    --configuration Release \
    --output /app/publish \
    --no-restore \
    -p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "SwaggerDocPreview.dll"]
