# QuraEx v2 — Database Conventions & Golden Flow

Authoritative DB rules for all 8 services. Source of truth = [`quraex.dbml`](./quraex.dbml) (render at dbdiagram.io) + per-service EF migrations. Every teammate follows this verbatim — no per-service deviation.

## 1. Cross-cutting conventions (every PostgreSQL service)

| Rule | Standard |
|---|---|
| Naming | `snake_case` for all tables/columns (`EFCore.NamingConventions` → `UseSnakeCaseNamingConvention()`). C# stays PascalCase. |
| Primary key | `id uuid` — generated **UUIDv7** (`Guid.CreateVersion7()`, **.NET 10**), never random v4 (index locality). |
| Enums | Stored as **varchar** via `HasConversion<string>()`. Readable in DB (`WHERE polarity='POSITIVE'`). Optional `CHECK` for integrity. **No** Postgres native enum (migration pain). |
| Timestamps | `created_at`, `updated_at` = `timestamptz`, set in Domain (UTC). |
| Audit | `created_by`/`updated_by` (uuid) on **business aggregates** only — not on infra tables (outbox/processed/snapshot). |
| Soft-delete | `deleted_at timestamptz NULL`. EF global query filter applied **only to entities implementing `ISoftDeletable`** (opt-in marker — NOT blanket-by-convention). **Infra tables (`*_outbox_message`, `*_processed_message`, `*_snapshot`) are EXCLUDED** (no `deleted_at`) so the outbox relay query keeps no spurious `deleted_at` predicate. ⚠️ **Unique columns MUST use partial unique index** `... WHERE deleted_at IS NULL` (e.g. `email`, `project_key`), declared **per-entity in its `IEntityTypeConfiguration`** (the convention helper cannot infer which columns are unique). |
| Postgres extensions | Extensions like `citext` are NOT auto-created by EF — declare `HasPostgresExtension("citext")` in the DbContext so migrations emit `CREATE EXTENSION`. |
| Secrets at rest | Tokens/secrets (e.g. `jira_connection.oauth_token`) use **envelope encryption with a KMS/Key Vault DEK** — the encryption key is NEVER in repo/config; documented rotation. "encrypted" in the schema = this scheme, not an app-config key. (Credentials/MFA secrets are NOT stored here — AWS Cognito owns them.) |
| Concurrency | `xmin` system column as EF token (`UseXminAsConcurrencyToken()`) — zero extra column. Apply on aggregates with concurrent edits. |
| Migration history | default `__EFMigrationsHistory` per DB (separate DBs → no clash). |

**Event infrastructure (repeats identically):**
- `*_outbox_message` on **every producer** — dual-write solved (write entity + outbox in one transaction). Columns include `seq bigserial` (ordering), `attempt_count`, `last_error`. **Relay = MassTransit Transactional Outbox** (`AddEntityFrameworkOutbox` + `UseBusOutbox`, `FOR UPDATE SKIP LOCKED`) — never leave the drain mechanism undefined.
- `*_processed_message` on **every consumer** — idempotency for at-least-once delivery. **Retention job** prunes `WHERE processed_at < now() - interval '7d'` (broker redelivery window ≪ 7d) to bound growth.
- Consumers use **retry (bounded exponential) + scheduled redelivery + dead-letter (`_error`)** with an alert on dead-letter depth — a poison message must not stall a read-model forever.
- `*_snapshot` = read-model populated from another service's events (e.g. `membership_snapshot`, `story_snapshot`).
- Cross-service ids (`user_id`, `project_id`, `story_id`, …) are **bare UUID references — NEVER a cross-service FK**.

**Polyglot exceptions:**
- **Notification = MongoDB** → collections, **camelCase** fields, no EF migrations (init indexes via script).
- **Execution** → object storage (**MinIO** local) for artifacts; DB stores `storage_path` only.
- **AI Generation / Integration** → `*_saga_state` table (MassTransit saga persistence).

## 2. IAM model (AWS Cognito + thin Identity service)

- **AWS Cognito (managed)** = authentication: registration, login, password, email verification, MFA, social/OAuth sign-in, and OIDC token issuance (JWT). No credential or token tables in our DB.
- **Identity service** = thin local layer only: a `user_profile` table mirrored from Cognito (synced via a Cognito post-confirmation trigger or on first authenticated request) + emits `UserRegistered`. The user id used everywhere = Cognito `sub` (bare UUID), no cross-service FK.
- **JWT validation:** gateway + every service validate the Cognito JWT via JWKS (`https://cognito-idp.<region>.amazonaws.com/<pool-id>`). Global roles = Cognito groups (carried as a JWT claim).
- Project/workspace authorization lives in **Workspace** service (`workspace_member` OWNER/ADMIN/MEMBER, `project_member` EDITOR/VIEWER), mirrored to other services via `membership_snapshot`.

## 3. ⭐ Golden flow — add/change a table (every teammate, every service)

1. **Design** — edit [`quraex.dbml`](./quraex.dbml); preview ERD on dbdiagram.io; review with lead.
2. **Domain** — write/modify rich entity (private setters, factory `Create()`, invariants inside entity).
3. **Config** — `IEntityTypeConfiguration<T>` in Infrastructure (Fluent API; snake_case mapping; enum `HasConversion<string>()`; soft-delete `HasQueryFilter`).
4. **Migration** — `dotnet ef migrations add <Name> -p <Svc>.Infrastructure -s <Svc>.Api`.
5. **Review SQL** — read generated migration, diff against DBML. **Gate: do not merge if they diverge.**
6. **Apply** — startup `Migrate()` ONLY under a hard `IHostEnvironment.IsDevelopment()` guard, wrapped in `pg_advisory_lock` (multi-replica race) with `EnableRetryOnFailure` (cold-DB boot); AppHost `WaitFor(postgres)`. **Prod = dedicated migration job/init-container**; the app's runtime DB user gets **DML-only** (no DDL). CI uses Testcontainers.
7. **Commit** — entity + config + migration + DBML update in **one PR**.

## 4. CI/CD policy gates (professional setup)

**Local (Husky.NET — block before push):**
- `dotnet format --verify-no-changes` (style gate)
- Conventional Commits lint on `commit-msg` (reject bad format)
- `gitleaks` secret scan (pre-commit)

**PR / CI (GitHub Actions — red = cannot merge):**
- Build **path-filtered** (only changed service) + **Roslyn analyzers + `TreatWarningsAsErrors`** (StyleCop/Sonar via `.editorconfig`)
- `dotnet test` with **Testcontainers** (real Postgres/RabbitMQ) + coverage threshold (Coverlet)
- `gitleaks` + **CodeQL** + NuGet vulnerability scan
- PR title Conventional-Commits lint

**Accountability ("who broke it"):**
- **Branch protection** on `main`: no direct push, required PR review + green status checks → a violating push fails the author's PR.
- **CODEOWNERS** maps each service folder to its owner → auto-reviewer + clear ownership.
- Conventional commits + `git blame` → traceable.

## Open questions
- `invitation` is project-scoped only (carries `role`). Workspace-level invitation (ADMIN/MEMBER) deferred — confirm acceptable for MVP.
