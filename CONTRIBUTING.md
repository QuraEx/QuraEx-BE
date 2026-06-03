# Contributing to QuraEx v2

## Setup (first-time)

```sh
# 1. Clone
git clone git@github.com:bavanchun/QuraEx-BE.git
cd QuraEx-BE/quraexv2

# 2. Set GitHub Packages token (needed before dotnet restore)
export GITHUB_PACKAGES_TOKEN=ghp_YOUR_PAT_HERE   # read:packages scope

# 3. Restore — installs Husky hooks automatically
dotnet tool restore
dotnet restore

# 4. Verify hooks are wired
cat .husky/pre-commit   # should show gitleaks + dotnet format checks
```

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

Copy the Authoring service as your template — it demonstrates the full 4-project layout,
CQRS vertical slices, Outbox messaging, and Testcontainers integration tests.

### Step-by-step

**1. Copy and rename the projects**

```sh
cp -r services/authoring services/<svc>
# Rename each project folder and .csproj to QuraEx.<Svc>.*
# Update namespaces (find-replace QuraEx.Authoring → QuraEx.<Svc>)
```

**2. Add to solution**

```sh
dotnet sln QuraEx.slnx add \
  services/<svc>/QuraEx.<Svc>.Domain/QuraEx.<Svc>.Domain.csproj \
  services/<svc>/QuraEx.<Svc>.Contracts/QuraEx.<Svc>.Contracts.csproj \
  services/<svc>/QuraEx.<Svc>.Infrastructure/QuraEx.<Svc>.Infrastructure.csproj \
  services/<svc>/QuraEx.<Svc>.Api/QuraEx.<Svc>.Api.csproj \
  services/<svc>/QuraEx.<Svc>.Tests/QuraEx.<Svc>.Tests.csproj
```

**3. Wire in AppHost**

In `aspire/QuraEx.AppHost/Program.cs`:

```csharp
var postgres<Svc> = builder.AddPostgres("postgres-<svc>");

var <svc> = builder
    .AddProject<Projects.QuraEx_<Svc>_Api>("<svc>")
    .WithReference(postgres<Svc>)
    .WithReference(rabbitmq)
    .WithReference(redis)
    .WaitFor(postgres<Svc>)
    .WaitFor(rabbitmq);

// If other services call into this one:
// gateway.WithReference(<svc>);
```

**4. Add Gateway route**

In `gateway/QuraEx.Gateway/appsettings.json` → `ReverseProxy.Routes`:

```json
{
  "<svc>-route": {
    "ClusterId": "<svc>-cluster",
    "Match": { "Path": "/api/<resource>/{**catch-all}" }
  }
}
```

And in `Clusters`:

```json
{
  "<svc>-cluster": {
    "Destinations": {
      "default": { "Address": "http://<svc>" }
    }
  }
}
```

**5. Update CODEOWNERS**

In `.github/CODEOWNERS`:

```
/services/<svc>/    @your-github-handle
```

**6. Implement your DBML + entities**

Follow the 7-step golden DB flow above. Start with the DBML section for your service,
then generate the domain entity and migration.

**7. Write integration tests**

Copy `QuraEx.Authoring.Tests/Integration/AuthoringApiFactory.cs` and `UserStoryApiTests.cs`.
Rename, replace endpoint paths, and update the factory's DbContext type reference.

---

## Running a specific service locally (without Aspire)

```sh
# Start infrastructure
docker compose up -d postgres-authoring rabbitmq redis

# Run the service (set connection strings via user-secrets or env vars)
dotnet user-secrets set "ConnectionStrings:postgres-authoring" \
  "Host=localhost;Database=authoring;Username=postgres;Password=postgres" \
  --project services/authoring/QuraEx.Authoring.Api

dotnet run --project services/authoring/QuraEx.Authoring.Api
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
