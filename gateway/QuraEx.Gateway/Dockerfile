# syntax=docker/dockerfile:1
#
# Multi-stage image for the YARP gateway.
#
# Build context MUST be the repository root: this project has a cross-directory
# ProjectReference (aspire/QuraEx.ServiceDefaults) and depends on the root
# global.json / nuget.config / Directory.*.props for SDK pinning + central
# package versions. Build with:
#   docker build -f gateway/QuraEx.Gateway/Dockerfile -t quraex-gateway .

# ── Build stage ───────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Central build config first so the restore layer stays cached until a manifest
# or package version actually changes. .editorconfig drives StyleCop/Sonar rules
# (build is warnings-as-errors) — without it analyzers fall back to strict defaults
# and the publish fails.
COPY global.json nuget.config Directory.Build.props Directory.Packages.props .editorconfig ./

# Project manifests for the gateway's dependency graph (restore needs only these).
COPY gateway/QuraEx.Gateway/QuraEx.Gateway.csproj gateway/QuraEx.Gateway/
COPY aspire/QuraEx.ServiceDefaults/QuraEx.ServiceDefaults.csproj aspire/QuraEx.ServiceDefaults/

# QuraEx.* are project references, not packages — restore hits nuget.org only,
# so no GitHub Packages token is required here.
RUN dotnet restore gateway/QuraEx.Gateway/QuraEx.Gateway.csproj

# Sources, then publish a framework-dependent app (UseAppHost=false: no native launcher).
COPY gateway/ gateway/
COPY aspire/QuraEx.ServiceDefaults/ aspire/QuraEx.ServiceDefaults/
RUN dotnet publish gateway/QuraEx.Gateway/QuraEx.Gateway.csproj \
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

HEALTHCHECK --interval=10s --timeout=3s --start-period=20s --retries=5 \
    CMD curl -fsS http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "QuraEx.Gateway.dll"]
