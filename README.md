# QuraEx v2 Backend

.NET 10 microservices — Vertical Slice + CQRS/MediatR + MassTransit Transactional Outbox + YARP Gateway + .NET Aspire.

## Prerequisites

| Tool | Version | How to get |
|------|---------|------------|
| .NET SDK | 10.0.300 | https://dotnet.microsoft.com/download (pinned in `global.json`) |
| Docker Desktop | 4.x+ | https://www.docker.com/products/docker-desktop |
| Aspire workload | 9.3.1 | `dotnet workload install aspire` |
| dotnet-ef | 10.0.0 | `dotnet tool restore` (auto from `.config/dotnet-tools.json`) |

### GitHub Packages — set up BEFORE `dotnet restore`

Internal `QuraEx.*` packages are published to GitHub Packages (required for NuGet restore):

```sh
# 1. Create a GitHub PAT with read:packages scope
#    Settings → Developer settings → Personal access tokens → Tokens (classic)

# 2. Export it (add to ~/.zshrc or ~/.bashrc for persistence)
export GITHUB_PACKAGES_TOKEN=ghp_YOUR_PAT_HERE   # macOS/Linux
# $env:GITHUB_PACKAGES_TOKEN="ghp_..."           # Windows PowerShell

# 3. Verify
dotnet nuget list source   # should show 'github' as Enabled
```

> **Fork-PR note:** External forks cannot read GitHub Packages with the auto-provided `GITHUB_TOKEN`.
> CI for forks must be triggered by a maintainer. Core team is unaffected.

## Run with Aspire (recommended)

```sh
# From quraexv2/
dotnet tool restore          # restores Husky, dotnet-ef
dotnet restore               # NuGet packages (needs GITHUB_PACKAGES_TOKEN)
dotnet run --project aspire/QuraEx.AppHost
```

Aspire starts everything and opens the OTel dashboard automatically:

| Resource | URL |
|----------|-----|
| OTel Dashboard | http://localhost:18888 |
| Gateway (YARP) | http://localhost:5000 |
| Authoring API | proxied via gateway `/api/user-stories` |
| pgAdmin | http://localhost:5050 |
| RabbitMQ Management | http://localhost:15672 (guest / guest) |
| Redis | localhost:6379 |

### Dev JWT — local API calls

The gateway and every service validate RS256 JWTs independently. For local testing, set the signing key via user-secrets:

```sh
dotnet user-secrets set "Jwt:PrivateKeyPem" "$(cat dev-private-key.pem)" \
  --project gateway/QuraEx.Gateway

dotnet user-secrets set "Jwt:PrivateKeyPem" "$(cat dev-private-key.pem)" \
  --project services/authoring/QuraEx.Authoring.Api
```

The matching **public** key is committed in `gateway/QuraEx.Gateway/appsettings.Development.json` — safe to commit (public key only, no signing capability).

## Run with Docker Compose (non-Aspire)

```sh
docker compose up -d   # starts Postgres, RabbitMQ, Redis
dotnet run --project services/authoring/QuraEx.Authoring.Api \
  --configuration Development
```

## Run tests

Testcontainers spins up a real Postgres container per test run — Docker must be running:

```sh
dotnet test services/authoring/QuraEx.Authoring.Tests
```

## Repo layout

```
quraexv2/
├── aspire/
│   ├── QuraEx.AppHost/               # Aspire orchestration (service graph, infra wiring)
│   └── QuraEx.ServiceDefaults/       # OTel, health checks, service discovery shared config
├── building-blocks/
│   ├── QuraEx.BuildingBlocks/        # Result<T>, BaseEntity, MediatR behaviors, EF conventions
│   └── QuraEx.BuildingBlocks.Messaging/ # MassTransit setup, idempotent consumer
├── gateway/
│   └── QuraEx.Gateway/               # YARP reverse proxy, RS256 JWT edge auth, rate limiting
├── services/
│   └── authoring/                    # Gold-standard service — clone this for new services
│       ├── QuraEx.Authoring.Domain/
│       ├── QuraEx.Authoring.Contracts/
│       ├── QuraEx.Authoring.Infrastructure/
│       ├── QuraEx.Authoring.Api/
│       └── QuraEx.Authoring.Tests/
├── docs/
│   └── database/
│       ├── quraex.dbml               # Canonical DB schema (source of truth)
│       └── conventions.md            # DB golden flow + naming rules
├── .github/
│   ├── workflows/ci.yml              # Path-filter build-test, security scan, pack-push
│   ├── CODEOWNERS                    # Auto-review routing per service
│   └── pull_request_template.md
├── Directory.Build.props             # Solution-wide compiler + analyzer settings
├── Directory.Packages.props          # Central Package Management (CPM)
├── global.json                       # SDK + Aspire workload version pins
└── docker-compose.yml                # Non-Aspire dev infra
```

## Branch flow

```
feature/<name>  →  PR (requires CI green + 1 review)  →  dev  →  release  →  main
```

- **`main`** — protected; no direct push; requires all CI checks green + 1 approving review
- **`dev`** — integration branch; merge features here first
- Commits and PR titles follow [Conventional Commits](https://www.conventionalcommits.org/)
  (`feat|fix|refactor|test|build|ci|perf|revert|style`)

## Adding a new service

See **[CONTRIBUTING.md](./CONTRIBUTING.md)** — "Add a new service" recipe and 7-step golden DB flow.

## Task board

See **[docs/TASKS.md](./docs/TASKS.md)** — remaining 7 services with owners, dependencies, and event contracts.
