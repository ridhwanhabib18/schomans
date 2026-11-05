# The API image. Build from the repository root:  docker build -f src/SchoMans.Api/Dockerfile -t schomans-api .
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first, so a code change does not download the packages again.
COPY global.json nuget.config Directory.Build.props Directory.Packages.props ./
COPY src/SchoMans.Domain/SchoMans.Domain.csproj src/SchoMans.Domain/
COPY src/SchoMans.Application/SchoMans.Application.csproj src/SchoMans.Application/
COPY src/SchoMans.Infrastructure/SchoMans.Infrastructure.csproj src/SchoMans.Infrastructure/
COPY src/SchoMans.Api/SchoMans.Api.csproj src/SchoMans.Api/
RUN dotnet restore src/SchoMans.Api/SchoMans.Api.csproj

COPY src src
RUN dotnet publish src/SchoMans.Api/SchoMans.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
# QuestPDF draws with Skia, which needs fontconfig on Linux.
RUN apt-get update \
    && apt-get install -y --no-install-recommends libfontconfig1 \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app .
USER app
# The image listens on 8080 (ASPNETCORE_HTTP_PORTS); the reverse proxy is the only thing that reaches it.
EXPOSE 8080
ENTRYPOINT ["dotnet", "SchoMans.Api.dll"]
