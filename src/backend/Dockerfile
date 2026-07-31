# syntax=docker/dockerfile:1
#
# Build context is the repo root (see docker-compose.yml's `build.context: .`), because this
# image needs both src/backend (the .NET 10 host) and src/frontend (the Angular 22 SPA) - ADR-002
# requires a single deployable app container, so the Angular build has to land inside this image
# rather than being served by a second container.

# ---- Stage 1: Angular production build ----
FROM node:22-alpine AS frontend-build
WORKDIR /frontend
COPY src/frontend/package.json src/frontend/package-lock.json ./
RUN npm ci
COPY src/frontend/ .
RUN npm run build

# ---- Stage 2: .NET publish ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS backend-build
WORKDIR /backend
COPY src/backend/GitCrawler.sln .
COPY src/backend/Directory.Build.props .
COPY src/backend/GitCrawler.Api/GitCrawler.Api.csproj GitCrawler.Api/
COPY src/backend/tests/GitCrawler.Api.Tests/GitCrawler.Api.Tests.csproj tests/GitCrawler.Api.Tests/
RUN dotnet restore GitCrawler.sln
COPY src/backend/ .
RUN dotnet publish GitCrawler.Api/GitCrawler.Api.csproj -c Release -o /app/publish --no-restore

# Angular build output copied into the ASP.NET Core host's static file root (AC4 / TC-003-03).
# This is the mechanism for the image build; GitCrawler.Api.csproj's BuildAngularApp/CopyAngularApp
# MSBuild targets do the equivalent for a local `dotnet publish` outside Docker.
COPY --from=frontend-build /frontend/dist/dashboard/browser /app/publish/wwwroot

# ---- Stage 3: runtime ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# curl is required for the container HEALTHCHECK below; the base runtime image doesn't include it.
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

COPY --from=backend-build /app/publish .

ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

HEALTHCHECK --interval=30s --timeout=5s --start-period=20s --retries=3 \
    CMD curl -f http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "GitCrawler.Api.dll"]
