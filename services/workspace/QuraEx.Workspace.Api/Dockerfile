# syntax=docker/dockerfile:1
#
# Multi-stage image for the Workspace API.
#
# Build context MUST be the repository root: this service references projects
# across services/, building-blocks/ and aspire/, and depends on the root
# global.json / nuget.config / Directory.*.props. Build with:
#   docker build -f services/workspace/QuraEx.Workspace.Api/Dockerfile -t quraex-workspace .

# ── Build stage ───────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Central build config first so the restore layer stays cached until a manifest
# or package version actually changes. .editorconfig drives StyleCop/Sonar rules
# (build is warnings-as-errors) — without it analyzers fall back to strict defaults
# and the publish fails.
COPY global.json nuget.config Directory.Build.props Directory.Packages.props .editorconfig ./

# Project manifests for the full dependency graph of the API (restore needs only these).
COPY services/workspace/QuraEx.Workspace.Api/QuraEx.Workspace.Api.csproj services/workspace/QuraEx.Workspace.Api/
COPY services/workspace/QuraEx.Workspace.Domain/QuraEx.Workspace.Domain.csproj services/workspace/QuraEx.Workspace.Domain/
COPY services/workspace/QuraEx.Workspace.Contracts/QuraEx.Workspace.Contracts.csproj services/workspace/QuraEx.Workspace.Contracts/
COPY services/workspace/QuraEx.Workspace.Infrastructure/QuraEx.Workspace.Infrastructure.csproj services/workspace/QuraEx.Workspace.Infrastructure/
COPY aspire/QuraEx.ServiceDefaults/QuraEx.ServiceDefaults.csproj aspire/QuraEx.ServiceDefaults/
COPY building-blocks/QuraEx.BuildingBlocks/QuraEx.BuildingBlocks.csproj building-blocks/QuraEx.BuildingBlocks/
COPY building-blocks/QuraEx.BuildingBlocks.Messaging/QuraEx.BuildingBlocks.Messaging.csproj building-blocks/QuraEx.BuildingBlocks.Messaging/

# QuraEx.* are project references, not packages — restore hits nuget.org only,
# so no GitHub Packages token is required here.
RUN dotnet restore services/workspace/QuraEx.Workspace.Api/QuraEx.Workspace.Api.csproj

# Sources, then publish a framework-dependent app (UseAppHost=false: no native launcher).
COPY services/ services/
COPY building-blocks/ building-blocks/
COPY aspire/QuraEx.ServiceDefaults/ aspire/QuraEx.ServiceDefaults/
RUN dotnet publish services/workspace/QuraEx.Workspace.Api/QuraEx.Workspace.Api.csproj \
    -c Release -o /app/publish --no-restore /p:UseAppHost=false

# ── Runtime stage ─────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# curl backs the container HEALTHCHECK below.
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish ./

# Kestrel listens on 8080 inside the container; host port is mapped by compose.
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

# Run as the non-root user shipped with the .NET runtime image.
USER $APP_UID

HEALTHCHECK --interval=10s --timeout=3s --start-period=30s --retries=5 \
    CMD curl -fsS http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "QuraEx.Workspace.Api.dll"]
