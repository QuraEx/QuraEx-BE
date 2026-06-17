# QuraEx v2 — Remaining Services Task Board

> Build order: each service depends on the ones above it.
> Each service gets its own brainstorm → plan → cook cycle following the Authoring template.
> New services are scaffolded with `./scripts/new-service.sh <PascalName>` (see CONTRIBUTING → "Add a new service").
>
> **Identity (1) and Workspace (2) skeletons are already pre-generated** — compilable stubs
> wired into AppHost, gateway, and CI. Their owners start directly at the DBML/entity step.
> The remaining services (3–7) are scaffolded on demand with the script.

## Service build order

| # | Service | Owner | Depends on | +Infra | Produces events | Consumes events |
|---|---------|-------|-----------|--------|----------------|----------------|
| 1 | [Identity](#1-identity) | @bavanchun | — (unblocks all) | — | `UserRegistered`, `UserUpdated` | — |
| 2 | [Workspace](#2-workspace) | @bavanchun | Identity | — | `MembershipChanged`, `ProjectCreated` | `UserRegistered` |
| 3 | [TestArtifact](#3-testartifact) | Dev B | Authoring, Workspace | — | `TestCasesSaved`, `TestRunRequested` | `UserStoryCreated`, `MembershipChanged` |
| 4 | [AI Generation](#4-ai-generation) | Dev C | Authoring, TestArtifact | — | `TestCasesGenerated` | `TestRunRequested` |
| 5 | [Execution](#5-execution) | Dev D | TestArtifact, AI Generation | +MinIO | `TestRunCompleted` | `TestRunRequested`, `TestCasesGenerated` |
| 6 | [Integration (Jira)](#6-integration-jira) | Dev E | Workspace, Authoring | — | `JiraIssueLinked` | `UserStoryCreated`, `ProjectCreated` |
| 7 | [Notification](#7-notification) | Dev E | Identity, Workspace | +MongoDB | — | `TestRunCompleted`, `MembershipChanged` |

> **Owners là placeholder** (Dev B–E) — lead điền tên thật khi chia việc. Integration + Notification gộp 1 owner vì cả hai chỉ subscribe event, làm sau cùng / bản tối giản.
>
> **3 pha chống block nhau** (chi tiết: `plans/260617-team-onboarding-architecture-decisions/brainstorm-summary.md`):
> - **Pha 0 (song song, không block):** skeleton đã commit *dev JWT public key* → mỗi owner làm DBML entity + EF migration + CRUD slice (theo mẫu Authoring), test ngay mà không cần đợi Identity live.
> - **Pha 1:** Identity live (OpenIddict, federation-ready cho AWS Cognito sau) → token thật.
> - **Pha 2:** wire Outbox/consumer giữa các service.
> - **Pha 3:** Integration + Notification.
>
> **Gateway = Kong DB-less** (lead handle, team không đụng). **Cloud đích = AWS** (EKS + RDS + S3 + Secrets Manager); S3/IAM là thiết kế giấy, code local dùng MinIO.

---

## 1. Identity

**Priority:** P0 — unblocks real gateway auth (currently using dev RS256 signing key)

**What it does:** OpenIddict authorization server + ASP.NET Identity user store.
Issues RS256 JWTs the gateway and every service already validate.

**DB group:** `identity` in DBML

**Key deliverables:**
- `POST /connect/token` — password + client_credentials grant
- `POST /register` — creates user, emits `UserRegistered`
- PKCE flow for future SPA client
- Replace dev signing key: gateway and services read public JWKS from Identity's `/.well-known/openid-configuration`

**Notes:**
- Use `OpenIddict.AspNetCore` + `OpenIddict.EntityFrameworkCore` (already in CPM)
- Store signing key in user-secrets / env only — never committed
- Once live, remove the hardcoded dev public key from `appsettings.Development.json`

---

## 2. Workspace

**Priority:** P1 — Authoring already has a `membership_snapshot` table waiting for this

**What it does:** Projects and team membership management.
Emits `MembershipChanged` which Authoring consumes to maintain its read-model snapshot.

**DB group:** `workspace` in DBML

**Key deliverables:**
- `POST /api/projects` — create project
- `POST /api/projects/{id}/members` — invite member, emits `MembershipChanged`
- Consumer: `UserRegistered` → seed user profile
- Authoring consumer for `MembershipChanged` → update `membership_snapshot`

**Notes:**
- `invitation` is project-scoped (per architecture decision)
- Authoring's consumer of `MembershipChanged` should be added in the Authoring service alongside Workspace development

---

## 3. TestArtifact

**Priority:** P2

**What it does:** Stores test cases and manages test run lifecycle.
Consumes Authoring story events; emits `TestRunRequested` to trigger AI generation or execution.

**DB group:** `test_artifact` in DBML

**Key deliverables:**
- CRUD for test cases (linked to user stories via `story_id`)
- `POST /api/test-runs` — kicks off a run, emits `TestRunRequested`
- Consumer: `UserStoryCreated` → create placeholder test case set
- Consumer: `MembershipChanged` → update local membership snapshot

---

## 4. AI Generation

**Priority:** P2 (parallel with Execution)

**What it does:** Saga that calls Authoring via gRPC to fetch story context,
runs an LLM to generate test cases, and emits `TestCasesGenerated`.

**DB group:** `ai_generation` in DBML

**Key deliverables:**
- Saga: `TestRunRequested` → fetch story (gRPC to Authoring) → generate → emit `TestCasesGenerated`
- gRPC contract in `QuraEx.Authoring.Contracts` (add `grpc/` to Contracts project)
- LLM client (configurable provider — OpenAI or Gemini)
- Retry + timeout saga compensation

**Notes:**
- gRPC to Authoring requires adding `Grpc.AspNetCore` to AppHost wiring
- Authoring Api needs a gRPC endpoint added (update Authoring contracts + Api)

---

## 5. Execution

**Priority:** P3

**What it does:** Runs test cases (unit, API, UI) and stores results + artifacts in MinIO.

**+Infra needed:** MinIO — add to `AppHost`:
```csharp
var minio = builder.AddContainer("minio", "minio/minio")
    .WithArgs("server", "/data", "--console-address", ":9001");
```

**DB group:** `execution` in DBML

**Key deliverables:**
- Consumer: `TestRunRequested` / `TestCasesGenerated` → execute → upload artifacts to MinIO
- `GET /api/test-runs/{id}/results` — results + artifact presigned URLs
- Emits `TestRunCompleted`

---

## 6. Integration (Jira)

**Priority:** P3 (parallel with Execution)

**What it does:** ACL service — maps QuraEx stories to Jira issues bidirectionally.
Outbound saga for Jira webhook delivery.

**DB group:** `integration` in DBML

**Key deliverables:**
- `POST /api/integrations/jira` — configure workspace Jira credentials (stored encrypted)
- Consumer: `UserStoryCreated` → create/link Jira issue
- Jira webhook receiver → update Authoring `external_ref`
- Emits `JiraIssueLinked`

**Notes:**
- Jira API token stored encrypted at rest (key in user-secrets / KMS — see `conventions.md` §security)
- Outbound saga handles retries on Jira API transient failures

---

## 7. Notification

**Priority:** P4

**What it does:** Fan-out service. Consumes completion and membership events,
sends in-app + email notifications. Uses MongoDB for flexible notification schema.

**+Infra needed:** MongoDB — add to `AppHost`:
```csharp
var mongo = builder.AddMongoDB("mongo");
```

**Key deliverables:**
- Consumer: `TestRunCompleted` → notify story owner
- Consumer: `MembershipChanged` → notify invited member
- `GET /api/notifications` — user notification feed (read-only, MongoDB)
- Push notifications (future: WebSocket or SSE)

---

## Infra additions summary

| When | What to add to AppHost | Who adds it |
|------|----------------------|-------------|
| Service 5 (Execution) | MinIO container | Execution owner |
| Service 7 (Notification) | MongoDB container | Notification owner |
