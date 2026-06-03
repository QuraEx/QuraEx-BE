# QuraEx v2 Backend

.NET 10 microservices backend using Vertical Slice architecture, CQRS/MediatR,
MassTransit, YARP Gateway, PostgreSQL, RabbitMQ, Redis, and .NET Aspire.

## Quickstart

This is the normal path for a teammate who has just cloned the repo and wants
to run the backend locally.

### Step 1: Install Required Tools

| Tool | Version | Notes |
|------|---------|-------|
| .NET SDK | 10.0.300+ | Pinned by `global.json` |
| Docker Desktop | 4.x+ | Must be running before starting the app or tests |
| dotnet tools | From repo manifest | Restored with `dotnet tool restore` |

Check your .NET SDK:

```sh
dotnet --version
```

If Docker Desktop is installed but stopped, start it now.

### Step 2: Clone the Repo

```sh
git clone git@github.com:bavanchun/QuraEx-BE.git
cd QuraEx-BE/quraexv2
```

### Step 3: Restore Tools and Packages

```sh
dotnet tool restore
dotnet restore QuraEx.slnx
```

Expected result: restore exits with no errors.

You do not need a GitHub Packages token for the current local setup. The
internal QuraEx packages are referenced from source projects in this repo.

### Step 4: Run the Backend with Aspire

Aspire is the recommended local run mode. It starts:

- PostgreSQL
- RabbitMQ
- Redis
- Gateway
- Authoring API

Run:

```sh
dotnet run --project aspire/QuraEx.AppHost/QuraEx.AppHost.csproj
```

Expected result: the console prints an Aspire Dashboard URL like:

```text
Login to the dashboard at https://localhost:<port>/login?t=<token>
```

Open that URL. Wait until the Gateway and Authoring resources are running.

If you see a Docker warning, Docker Desktop is usually stopped or still waking
up. Start Docker Desktop, wait a minute, then rerun the command.

### Step 5: Verify the App is Alive

Use the URLs shown in the Aspire dashboard. The ports can change between runs.

From the Gateway resource, open or curl:

```sh
curl <gateway-url>/api/authoring/health
```

Expected result: HTTP `200 OK`.

From the Authoring resource, open or curl:

```sh
curl <authoring-url>/health
```

Expected result: HTTP `200 OK`.

The CRUD endpoints are protected by JWT, so a plain request to this endpoint
should return `401 Unauthorized`:

```sh
curl <gateway-url>/api/authoring/user-stories
```

That `401` is expected and means auth is active.

### Alternative: Run the Full Stack with Docker (no Aspire)

Aspire (Step 4) is the best inner-loop experience — hot reload, debugging, the
dashboard. But it runs the services as local projects, so behaviour can still
drift between machines. When you want a fully containerized run that is
identical on every machine (CI, demos, onboarding, "works on my machine"
debugging), use the Docker stack instead.

This mode builds an image per service from its `Dockerfile` and runs everything
— gateway, authoring, Postgres, RabbitMQ, Redis — in containers. No .NET SDK
required to *run* it.

```sh
# Build images and start the whole stack
make up

# Follow logs / stop / wipe data
make logs
make down
make clean      # also removes volumes (destroys local data)
```

`make up` is a thin wrapper over:

```sh
docker compose -f docker-compose.yml -f docker-compose.app.yml up --build -d
```

The two files split responsibilities on purpose:

- `docker-compose.yml` — backing infra only (use `make up-infra` to run just
  this and start the services yourself from the IDE / Aspire).
- `docker-compose.app.yml` — adds the containerized `gateway` + `authoring`.

Default ports (override in `.env`, see `.env.example`):

| Service | URL |
|---------|-----|
| Gateway (public entry) | http://localhost:8080 |
| Authoring (direct) | http://localhost:8081 |
| RabbitMQ management UI | http://localhost:15672 |

Verify the same way as Step 5:

```sh
curl http://localhost:8080/api/authoring/health   # -> Healthy
```

This stack runs in the `Development` environment for parity with Aspire: EF
migrations auto-apply on startup and the committed dev JWT public key is used.
It is meant for local / CI parity — production deployment uses real secrets and
a dedicated migration job, not this compose file.

### Step 6: Run Tests

Keep Docker Desktop running. Tests use Testcontainers and start their own
Postgres container.

```sh
dotnet test services/authoring/QuraEx.Authoring.Tests/QuraEx.Authoring.Tests.csproj
```

Expected result: all Authoring tests pass.

### Step 7: Run the Same Checks CI Cares About

Run these before opening a PR:

```sh
dotnet build QuraEx.slnx --no-restore -warnaserror

dotnet ef migrations has-pending-model-changes \
  --project services/authoring/QuraEx.Authoring.Infrastructure \
  --startup-project services/authoring/QuraEx.Authoring.Api \
  --no-build

dotnet list QuraEx.slnx package --vulnerable --include-transitive
```

Expected result:

- Build succeeds with `0 Warning(s)` and `0 Error(s)`.
- EF says no model changes since the last migration.
- Vulnerability scan reports no vulnerable packages.

## Alternative: Run Authoring Without Aspire

Use this only when you want to run one service manually. This does not start
the Gateway.

```sh
docker compose up -d postgres-authoring rabbitmq redis

dotnet run \
  --project services/authoring/QuraEx.Authoring.Api/QuraEx.Authoring.Api.csproj \
  --configuration Development
```

Authoring uses the fixed local URL from its launch profile:

```sh
curl http://localhost:5057/health
```

Expected result: HTTP `200 OK`.

## JWT Notes for Local API Calls

Gateway and services validate RS256 tokens with `Jwt:PublicKeyPem`.

- Health endpoints do not need a token.
- User story CRUD endpoints require a valid token.
- The development public key is committed because it cannot sign tokens.
- Never commit a private signing key.

To override the validation key locally:

```sh
dotnet user-secrets set "Jwt:PublicKeyPem" "$(cat dev-public-key.pem)" \
  --project gateway/QuraEx.Gateway/QuraEx.Gateway.csproj

dotnet user-secrets set "Jwt:PublicKeyPem" "$(cat dev-public-key.pem)" \
  --project services/authoring/QuraEx.Authoring.Api/QuraEx.Authoring.Api.csproj
```

## Quick Troubleshooting

| Problem | What to do |
|---------|------------|
| `dotnet restore` fails because SDK is missing | Install .NET SDK 10.0.300+ and rerun `dotnet --version`. |
| Aspire says Docker is unhealthy | Start Docker Desktop and wait until it is fully running. |
| HTTPS certificate warning | Run `dotnet dev-certs https --trust`. |
| `/api/authoring/user-stories` returns `401` | This is expected without JWT. Use `/api/authoring/health` for a no-auth check. |
| Tests fail before hitting assertions | Make sure Docker Desktop is running; Testcontainers needs it. |

## Repo Layout

```text
quraexv2/
├── aspire/
│   ├── QuraEx.AppHost/                 # Aspire orchestration
│   └── QuraEx.ServiceDefaults/         # OTel, health, service discovery
├── building-blocks/
│   ├── QuraEx.BuildingBlocks/          # Result<T>, entities, MediatR, EF conventions
│   └── QuraEx.BuildingBlocks.Messaging/
├── gateway/
│   └── QuraEx.Gateway/                 # YARP reverse proxy and JWT validation
├── services/
│   └── authoring/                      # Reference service implementation
├── docs/
│   └── database/                       # DBML and DB conventions
├── .github/
│   └── workflows/ci.yml
├── Directory.Build.props
├── Directory.Packages.props
├── global.json
└── docker-compose.yml
```

## Branch Flow

```text
feature/<name> -> PR -> dev -> release -> main
```

- `main` is protected and requires green CI plus review.
- `dev` is the integration branch.
- Commits and PR titles follow Conventional Commits:
  `feat|fix|refactor|test|build|ci|perf|revert|style`.

## Adding a New Service

Use the Authoring service as the reference implementation and follow the
service recipe in [CONTRIBUTING.md](./CONTRIBUTING.md).

## Task Board

See [docs/TASKS.md](./docs/TASKS.md) for remaining services, dependencies, and
event contracts.
