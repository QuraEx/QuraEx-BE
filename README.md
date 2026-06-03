# QuraEx v2 Backend

> Quickstart, architecture overview, and developer guide — see [CONTRIBUTING.md](./CONTRIBUTING.md) (filled in Phase 7).

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) (pinned via `global.json`)
- Docker Desktop (Aspire / docker-compose infra)
- [Aspire CLI workload](https://learn.microsoft.com/en-us/dotnet/aspire/fundamentals/setup-tooling): `dotnet workload install aspire`
- GitHub Packages PAT (`read:packages`) — see [CONTRIBUTING.md](./CONTRIBUTING.md) for nuget.config setup

## Quick Start

```bash
# 1. Restore tools (Husky hooks install automatically)
dotnet tool restore

# 2. Set GitHub Packages token
export GITHUB_PACKAGES_TOKEN=<your-pat>   # Linux/macOS
# $env:GITHUB_PACKAGES_TOKEN = "<your-pat>"  # Windows PowerShell

# 3. Run everything
aspire run --project aspire/QuraEx.AppHost

# --- OR without Aspire ---
docker compose up -d          # start infra
dotnet run --project services/authoring/QuraEx.Authoring.Api
```

## Repo Layout

```
quraexv2/
├─ aspire/            .NET Aspire AppHost + ServiceDefaults
├─ gateway/           YARP reverse proxy (public entry point)
├─ building-blocks/   Shared NuGet packages (QuraEx.BuildingBlocks*)
├─ services/
│  └─ authoring/      Gold-standard service — copy this for new services
└─ docs/              Architecture docs, DB conventions, DBML
```

## Ports (local)

| Service          | Port  |
|------------------|-------|
| Gateway          | 5000  |
| Authoring API    | 5100  |
| Aspire Dashboard | 18888 |
| PostgreSQL       | 5432+ |
| RabbitMQ mgmt    | 15672 |
| Redis            | 6379  |
