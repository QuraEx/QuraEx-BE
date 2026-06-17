# QuraEx v2 Backend

.NET 10 microservices backend using Vertical Slice architecture, CQRS/MediatR,
MassTransit, an API Gateway (YARP today — migrating to Kong DB-less, lead-owned),
PostgreSQL, RabbitMQ, Redis, and .NET Aspire.

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
git clone git@github.com:quraex/QuraEx-BE.git
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

> Prefer a fully containerized run (no .NET SDK, identical on every machine)?
> See [Run Modes](#run-modes) → *Docker full stack*.

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

## Quickstart (Docker — no .NET SDK required)

Fastest path for frontend devs, QA, and onboarding. No SDK install needed.

**Prerequisite:** The committed `.env` is dotenvx ciphertext. A plain
`docker compose up` without the private key will fail — that is by design.
Get `DOTENV_PRIVATE_KEY` from the team lead via a secure channel (password
manager or encrypted DM) before running the stack.

```sh
# 1. Install dotenvx (or use npx — no global install required)
npm install -g @dotenvx/dotenvx
# -or- skip this; every command below works with npx @dotenvx/dotenvx@latest

# 2. Get the private key from the team lead and export it
export DOTENV_PRIVATE_KEY='<key-from-lead>'
# Add to ~/.zshrc or ~/.bashrc to persist across sessions

# 3. Clone and enter the repo
git clone git@github.com:quraex/QuraEx-BE.git
cd QuraEx-BE/quraexv2

# 4. Start the full stack (first run builds images — takes a few minutes)
dotenvx run -- docker compose up -d --build
# -or- make up

# 5. Verify
curl http://localhost:8080/api/authoring/health   # -> 200 OK
```

> A clone WITHOUT `DOTENV_PRIVATE_KEY` cannot run the stack — the committed
> `.env` is encrypted ciphertext. Get the key from the lead before proceeding.
> See [DOTENVX_QUICK_START.md](./DOTENVX_QUICK_START.md) for full setup,
> daily workflow, key rotation, and the admin edit→encrypt→commit cycle.

`docker compose up -d` auto-merges `docker-compose.override.yml` (application
services) on top of `docker-compose.yml` (infra) — no `-f` flags needed.

- Subsequent runs: drop `--build` unless code changed
  (`dotenvx run -- docker compose up -d`)
- Stop the stack: `make down`  or  `dotenvx run -- docker compose down`
- Wipe data volumes too: `make clean`  or  `dotenvx run -- docker compose down -v`
- Follow logs: `make logs`  or  `dotenvx run -- docker compose logs -f`

> Backend devs doing active feature work should use **Aspire** (see Quickstart
> above) for hot reload, the observability dashboard, and debugger attach.
> Aspire manages its own service configs and is unaffected by the `.env` file.

## Run Modes

Four ways to run the backend locally. The Quickstart above uses **Aspire** — the
others are for specific needs.

| Mode | Command | When to use |
|------|---------|-------------|
| Aspire (default) | `dotnet run --project aspire/QuraEx.AppHost/QuraEx.AppHost.csproj` | Daily dev — hot reload, debugging, observability dashboard |
| Docker full stack | `dotenvx run -- docker compose up -d --build` or `make up` | Identical environment on every machine — demos, onboarding, FE/QA; no .NET SDK needed |
| Infra only + IDE | `make up-infra` | Run services from your IDE/Aspire against containerized Postgres/RabbitMQ/Redis |
| Single service | see below | Run one service manually, without the Gateway |

### Docker full stack

Builds an image per service and runs everything — gateway, authoring, identity,
workspace, Postgres, RabbitMQ, Redis — in containers. No .NET SDK required.

```sh
dotenvx run -- docker compose up -d --build   # or: make up
dotenvx run -- docker compose logs -f         # or: make logs
dotenvx run -- docker compose down            # or: make down
dotenvx run -- docker compose down -v         # or: make clean (destroys local data)
```

> Requires `DOTENV_PRIVATE_KEY` in the environment — see
> [DOTENVX_QUICK_START.md](./DOTENVX_QUICK_START.md).

`docker-compose.yml` contains backing infra only; `docker-compose.override.yml`
adds the containerized app services. Docker Compose v2 auto-merges the override
file on plain `docker compose up`, so no `-f` flags are needed.

Default ports (override via `.env`, see `.env.example`):

| Service | URL |
|---------|-----|
| Gateway (public entry) | http://localhost:8080 |
| Authoring (direct) | http://localhost:8081 |
| Identity (direct) | http://localhost:8082 |
| Workspace (direct) | http://localhost:8083 |
| RabbitMQ management UI | http://localhost:15672 |

```sh
curl http://localhost:8080/api/authoring/health   # -> Healthy
curl http://localhost:8080/api/identity/health    # -> Healthy
curl http://localhost:8080/api/workspace/health   # -> Healthy
```

This stack runs the `Development` environment for parity with Aspire (migrations
auto-apply, committed dev JWT public key). It is local/CI parity only — see
[`deploy/README.md`](./deploy/README.md) for production.

### Single service (no Aspire, no Gateway)

```sh
docker compose up -d postgres-authoring rabbitmq redis

dotnet run \
  --project services/authoring/QuraEx.Authoring.Api/QuraEx.Authoring.Api.csproj \
  --configuration Development
```

Authoring uses the fixed local URL from its launch profile:

```sh
curl http://localhost:5057/health   # -> HTTP 200 OK
```

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
│   └── QuraEx.Gateway/                 # API Gateway: YARP reverse proxy + JWT today;
│                                       #   decided target is Kong DB-less (lead-owned migration)
├── services/
│   └── authoring/                      # Reference service implementation
├── deploy/                             # Production deploy (compose, runbook, droplet setup)
├── .github/
│   └── workflows/ci.yml
├── Directory.Build.props
├── Directory.Packages.props
├── global.json
├── Makefile                            # make up / up-infra / down / logs / clean
├── docker-compose.yml                  # backing infra (Postgres/RabbitMQ/Redis)
└── docker-compose.override.yml         # app services (auto-merged by docker compose up)
```

> Design docs (architecture, SRS, DB conventions, DBML, task board) live in
> [`docs/`](./docs/) and are tracked in the repo — see [Documentation](#documentation).

## Branch Flow

```text
feature/<name> -> PR -> dev -> release -> main
```

- `main` is protected: the `CI Gate` status check must be green (strict, branch
  up to date). No approval is required on this solo repo; force-push and branch
  deletion are blocked.
- `dev` is the integration branch.
- Commits and PR titles follow Conventional Commits:
  `feat|fix|refactor|test|build|ci|perf|revert|style`.

## Adding a New Service

Use the Authoring service as the reference implementation and follow the
service recipe in [CONTRIBUTING.md](./CONTRIBUTING.md).

## Task Board

Remaining services, owners, build order, dependencies, and event contracts are
tracked in [`docs/TASKS.md`](./docs/TASKS.md). Claim a service there before starting.

## Documentation

Everything a teammate needs lives in the repo. Read in this order:

| # | Doc | Read it to… |
|---|-----|-------------|
| 1 | This README | Get the stack running locally (Aspire or Docker). |
| 2 | [`CONTRIBUTING.md`](./CONTRIBUTING.md) | Learn commit/PR rules, the 7-step DB flow, and how to scaffold a new service. |
| 3 | [`docs/TASKS.md`](./docs/TASKS.md) | Pick a service to build — owners, build order, event contracts. |
| 4 | [`docs/database/conventions.md`](./docs/database/conventions.md) | Follow the authoritative DB rules (naming, soft-delete, outbox) — same for all services. |
| 5 | [`docs/database/quraex.dbml`](./docs/database/quraex.dbml) | The master schema (source of truth). Render at [dbdiagram.io](https://dbdiagram.io). |
| 6 | [`docs/QuraEx_Architecture.md`](./docs/QuraEx_Architecture.md) | Understand the system design and service boundaries. |
| 7 | [`docs/QuraEx_SRS.md`](./docs/QuraEx_SRS.md) | Understand product requirements and scope. |
| — | [`DOTENVX_QUICK_START.md`](./DOTENVX_QUICK_START.md) | Set up encrypted secrets for the Docker stack. |
| — | [`deploy/README.md`](./deploy/README.md) | Production deploy (droplet + Cloudflare Tunnel). |

**New teammate? Start here:** README §Quickstart → `CONTRIBUTING.md` → claim a service in `docs/TASKS.md` → build it following `docs/database/conventions.md`.
