# Contributing to QuraEx v2

## Setup (first-time)

```sh
# 1. Clone
git clone git@github.com:bavanchun/QuraEx-BE.git
cd QuraEx-BE/quraexv2

# 2. Restore — installs Husky hooks automatically
dotnet tool restore
dotnet restore QuraEx.slnx

# 3. Verify hooks are wired
cat .husky/pre-commit   # should show gitleaks + dotnet format checks
```

GitHub Packages token is only needed when consuming published `QuraEx.*`
packages from another repository. The current workspace restores local QuraEx
projects from source.

---

## Commit & PR rules

- **Conventional Commits** — enforced by `commit-msg` hook and CI PR-title lint:
  ```
  feat(authoring): add acceptance criteria endpoints
  fix(gateway): handle null JWT audience claim
  build(deps): bump MassTransit to 8.5.0
  ```
  Allowed types: `feat | fix | refactor | test | build | ci | perf | revert | style`
  **Not allowed:** `chore`, `docs` (per project convention).

- **Branch flow:**
  ```
  feature/<slug>  →  PR → dev  →  release  →  main
  ```
  Never push directly to `main` — branch protection enforces CI green + 1 review.

- **PR title** must follow Conventional Commits (CI checks this automatically).

---

## 7-step golden DB flow

> Full naming rules and conventions: [`docs/database/conventions.md`](./docs/database/conventions.md)

Every schema change follows this sequence:

1. **DBML first** — update `docs/database/quraex.dbml` with the new tables/columns.
   Keep the DB as the source of truth; entity classes are derived from DBML, not the reverse.

2. **Domain entity** — create/update the entity in `services/<svc>/QuraEx.<Svc>.Domain/Entities/`.
   - Extend `SoftDeletableAggregate` if the table has `deleted_at`; `AggregateRoot` otherwise.
   - Use `Guid.CreateVersion7()` for PKs (in `BaseEntity`).
   - Emit domain events via `AddDomainEvent(new XxxEvent(...))`.

3. **EF configuration** — add/update `IEntityTypeConfiguration<T>` in
   `services/<svc>/QuraEx.<Svc>.Infrastructure/EntityConfigurations/`.
   - Apply `HasAnnotation("Npgsql:UseXminAsConcurrencyToken", true)` for optimistic concurrency.
   - Soft-delete filter applied automatically for `ISoftDeletable` implementors.
   - Enum columns → `HasConversion<string>()` (configured globally in `ApplyQuraExConventions`).

4. **EF migration** — generate with the EF CLI:
   ```sh
   dotnet ef migrations add <PascalCaseName> \
     --project services/<svc>/QuraEx.<Svc>.Infrastructure \
     --startup-project services/<svc>/QuraEx.<Svc>.Api
   ```
   Review the generated `.cs` file — confirm snake_case column names, correct FK/index names.

5. **SQL review** — read the generated `Up()` method against the DBML.
   Check: table names, column types, `bigserial` for `seq`, soft-delete indexes.

6. **Apply locally** — Aspire/dev auto-applies via `MigrationHostedService` on startup.
   For manual apply:
   ```sh
   dotnet ef database update \
     --project services/<svc>/QuraEx.<Svc>.Infrastructure \
     --startup-project services/<svc>/QuraEx.<Svc>.Api
   ```

7. **PR** — CI runs `dotnet ef migrations has-pending-model-changes` — fails if you forgot to
   generate a migration after changing an entity.

---

## Add a new service

**The recommended way is to use the service scaffolder script.** This keeps all stubs
consistent and authoritative.

### Step-by-step (script-first)

**1. Run the scaffolder**

```sh
./scripts/new-service.sh <ServiceName>
# e.g., ./scripts/new-service.sh MyFeature
```

The script outputs the exact next steps, including:
- `Projects.QuraEx_<Name>_Api` identifier for AppHost
- Gateway route template (with `AuthorizationPolicy`)
- `ci.yml` matrix row format
- DBML anchor for the schema section

**2. Wire in AppHost**

Add the ProjectReferences to `aspire/QuraEx.AppHost/QuraEx.AppHost.csproj`:

```xml
<ProjectReference Include="..\..\services\<svc>\QuraEx.<Svc>.Api\QuraEx.<Svc>.Api.csproj" />
```

Then in `aspire/QuraEx.AppHost/Program.cs`, add per-service Postgres + messaging refs:

```csharp
var postgres<Svc> = builder.AddPostgres("postgres-<svc>").WithPgAdmin();

var <svc> = builder
    .AddProject<Projects.QuraEx_<Svc>_Api>("<svc>")
    .WithReference(postgres<Svc>)
    .WithReference(rabbitmq)
    .WithReference(redis)
    .WaitFor(postgres<Svc>)
    .WaitFor(rabbitmq);

_ = gateway.WithReference(<svc>);
```

Then rebuild AppHost to materialize the `Projects.*` type.

**3. Add Gateway route**

In `gateway/QuraEx.Gateway/appsettings.json` → `ReverseProxy.Routes`, add routes for
the main API endpoint and `/health`:

```json
{
  "<svc>-route": {
    "ClusterId": "<svc>-cluster",
    "AuthorizationPolicy": "default",
    "Match": { "Path": "/api/<svc>/{**catch-all}" },
    "Transforms": [ { "PathPattern": "/api/{**catch-all}" } ]
  },
  "<svc>-health-route": {
    "ClusterId": "<svc>-cluster",
    "Match": { "Path": "/api/<svc>/health" },
    "Transforms": [ { "PathPattern": "/health" } ]
  }
}
```

And in `Clusters`:

```json
{
  "<svc>-cluster": {
    "Destinations": {
      "<svc>": { "Address": "http://<svc>" }
    }
  }
}
```

**Note:** If the new service is an auth server (like Identity), set `"AuthorizationPolicy": "anonymous"`
on the main route to bypass authentication checks.

**4. Update CI**

In `.github/workflows/ci.yml`, add rows to the `build-test` matrix:

```yaml
- service: <svc>
  project: services/<svc>/QuraEx.<Svc>.Api/QuraEx.<Svc>.Api.csproj
  test-project: services/<svc>/QuraEx.<Svc>.Tests/QuraEx.<Svc>.Tests.csproj
  infra-project: services/<svc>/QuraEx.<Svc>.Infrastructure
  coverage-threshold: 0   # stub has only the /health smoke test; raise to 60 with the first real feature
  changed: ${{ needs.changes.outputs.<svc> }}
```

And add to path-filter outputs (in the `changes` job):

```yaml
<svc>: ${{ steps.filter.outputs.<svc> }}
```

And filters section:

```yaml
<svc>:
  - 'services/<svc>/**'
  - 'building-blocks/**'
  - 'Directory.Build.props'
  - 'Directory.Packages.props'
  - '.github/workflows/ci.yml'
```

And add to `publish-images` matrix (if the service has a `Dockerfile`):

```yaml
- name: <svc>
  dockerfile: services/<svc>/QuraEx.<Svc>.Api/Dockerfile
```

**5. Update CODEOWNERS**

In `.github/CODEOWNERS`, add a line for the new service:

```
/services/<svc>/    @your-github-handle
```

For auth-critical services (like Identity), add a lead-review annotation above the entry.

**6. Implement your DBML + entities**

Follow the 7-step golden DB flow above. Start with the DBML section for your service,
then generate the domain entity and migration.

**7. Write integration tests**

The scaffolder creates stub test projects with `/health` endpoint assertions.
Add domain-specific tests following the pattern in the generated `HealthEndpointTests.cs`.

---

## Add a new service (manual fallback)

If you prefer to hand-craft the service from scratch (not recommended — the script is
authoritative), copy the Authoring service as your template:

```sh
cp -r services/authoring services/<svc>
# Rename each project folder and .csproj to QuraEx.<Svc>.*
# Update namespaces (find-replace QuraEx.Authoring → QuraEx.<Svc>)
dotnet sln QuraEx.slnx add \
  services/<svc>/QuraEx.<Svc>.Domain/QuraEx.<Svc>.Domain.csproj \
  services/<svc>/QuraEx.<Svc>.Contracts/QuraEx.<Svc>.Contracts.csproj \
  services/<svc>/QuraEx.<Svc>.Infrastructure/QuraEx.<Svc>.Infrastructure.csproj \
  services/<svc>/QuraEx.<Svc>.Api/QuraEx.<Svc>.Api.csproj \
  services/<svc>/QuraEx.<Svc>.Tests/QuraEx.<Svc>.Tests.csproj
```

Then follow steps 2–7 above. **Always regenerate, never hand-patch** — if the
scaffolder output drifts from reality, fix the template in `scripts/new-service.sh` and
regenerate the service, rather than manually patching the output.

---

## Running a specific service locally (without Aspire)

```sh
# Start infrastructure
docker compose up -d postgres-<svc> rabbitmq redis

# Run the service (set connection strings via user-secrets or env vars)
dotnet user-secrets set "ConnectionStrings:postgres-<svc>" \
  "Host=localhost;Database=<svc>;Username=postgres;Password=postgres" \
  --project services/<svc>/QuraEx.<Svc>.Api

dotnet run --project services/<svc>/QuraEx.<Svc>.Api
```

---

## CI overview

The `ci.yml` pipeline:

1. **`changes`** — dorny/paths-filter: only builds services whose files changed
2. **`build-test`** — per-service matrix: `dotnet build` (warnings-as-errors) → Testcontainers tests → migration drift check
3. **`pr-title`** — Conventional Commits lint on the PR title
4. **`security`** — gitleaks secret scan + CodeQL + vulnerable package check
5. **`pack-push`** — packs and pushes BuildingBlocks to GitHub Packages on merge to `main`

**All checks must pass before a PR can merge to `main`.**
