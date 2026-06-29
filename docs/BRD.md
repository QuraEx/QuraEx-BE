# QuraEx v2 — BRD & Work-Division Brief

> **Mục đích:** Tài liệu giao việc cho team backend. Mô tả app, các flow nghiệp vụ, phân công service cho từng người, và quy ước chung để mọi người dựng service song song.
> **Đây KHÔNG phải spec đầy đủ** — chi tiết requirement ở [`QuraEx_SRS.md`](./QuraEx_SRS.md), kiến trúc ở [`QuraEx_Architecture.md`](./QuraEx_Architecture.md), flow có sơ đồ ở [`service-flows-explainer.md`](./service-flows-explainer.md), DB ở [`database/conventions.md`](./database/conventions.md) + [`database/quraex.dbml`](./database/quraex.dbml).
> **Mỗi người đọc file riêng của mình:** [`team/Van.md`](./team/Van.md) · [`team/Minh.md`](./team/Minh.md) · [`team/Bao.md`](./team/Bao.md) · [`team/Giang.md`](./team/Giang.md).

---

## 1. App là gì

**QuraEx** = nền tảng dùng AI sinh test case từ user story. Vòng đời: tác giả viết user story + acceptance criteria → AI sinh test case → QA review/duyệt → tổ chức thành suite/plan → execute test run (Playwright) → ghi kết quả + defect → đồng bộ 2 chiều với Jira. Người dùng nhận thông báo qua in-app/email.

**Actor chính:** Workspace Owner/Admin, Project Editor, QA Engineer, External System (Jira).

---

## 2. Kiến trúc & tech-stack (tóm tắt)

| Lớp | Công nghệ |
|---|---|
| Runtime | **.NET 10** (SDK 10.0.300), Nullable enable, `TreatWarningsAsErrors=true` |
| Orchestration dev | **.NET Aspire 9.3.1** (AppHost + ServiceDefaults + ServiceDiscovery) |
| Kiến trúc service | Vertical Slice + CQRS/MediatR + Clean Architecture; mỗi service = `Api / Domain / Infrastructure / Contracts / Tests` |
| Mediator/Validation | MediatR 12.4.1, FluentValidation 11.11 |
| ORM/DB | EF Core 10 + Npgsql, **Postgres 17 DB-per-service** (mỗi service 1 container riêng) |
| Messaging | **MassTransit 8.4 + RabbitMQ**, Transactional Outbox + Idempotent consumer |
| AuthN/Z | **AWS Cognito** (managed — đăng ký/đăng nhập/cấp JWT) + JwtBearer (mọi service validate JWT Cognito qua JWKS) |
| Gateway | **YARP** (reverse proxy, validate JWT Cognito) — *không phải Kong, xem §10* |
| Telemetry | OpenTelemetry (OTLP) + HealthChecks |
| Test | xUnit + FluentAssertions + NSubstitute + **Testcontainers** (Postgres/RabbitMQ) |
| Polyglot | Notification = **MongoDB**; Execution = **MinIO** (object storage); AI Gen/Integration = MassTransit **saga state** |

Mọi service xài chung `building-blocks/` (Result, BaseEntity, Outbox, MassTransit config, exception middleware, ICurrentUser, MediatR behaviors) + `aspire/QuraEx.ServiceDefaults` (OTel, health, resilience, service discovery). **Không viết lại** — xem §7.

---

## 3. 8 service & phân công

| # | Service | Owner | FR | +Infra | Trạng thái code |
|---|---|---|---|---|---|
| 1 | Identity (AWS Cognito) | **Minh** | 01–05 | AWS Cognito | Stub (chỉ outbox) |
| 2 | Workspace | **Bảo** | 06–09 | — | Stub (chỉ outbox) |
| 3 | Authoring | **Giang** | 10–14 | — | ⭐ Gold, CRUD story xong |
| 4 | TestArtifact | **Giang** | 20–23 | — | Rỗng (scaffold) |
| 5 | AI Generation | **Văn** | 15–19 | saga | Rỗng (scaffold) |
| 6 | Execution | **Văn** | 24–28 | **MinIO** | Rỗng (scaffold) |
| 7 | Integration (Jira) | **Minh** | 29–31 | saga | Rỗng (scaffold) |
| 8 | Notification | **Bảo** | 32 | **MongoDB** | Rỗng (scaffold) |

**Tải:** Văn (AI Generation + Execution — nhóm AI/thực thi), Minh (Identity + Integration), Bảo (Workspace + Notification), Giang (Authoring + TestArtifact). Lead/infra (gateway, Aspire, CI) = Văn.

> ⚠️ **gRPC:** AI Generation (Văn) gọi Authoring (Giang) lấy nội dung story → 2 người chốt `.proto` chung sớm.

---

## 4. 6 flow nghiệp vụ (A–F)

Ký hiệu: ⭐ = service chủ trì. Liên kết giữa service = **event qua Outbox** (trừ khi ghi gRPC).

### Flow A · Đăng ký / Đăng nhập — ⭐ Identity (AWS Cognito)
**AWS Cognito** lo đăng ký/đăng nhập + cấp JWT. Gateway + mọi service validate JWT Cognito qua **JWKS** (`https://cognito-idp.<region>.amazonaws.com/<pool-id>`). Service Identity nội bộ (nếu giữ) đồng bộ user về `profile` + emit `UserRegistered` → Workspace seed profile. **FR-01..05.**

### Flow B · Mời thành viên — ⭐ Workspace
Admin mời email + role → Workspace tạo `invitation` (Pending, token hash), chạy **saga** AwaitingResponse → emit `MembershipChanged` → Notification gửi email. Invitee accept trong **7 ngày** → tạo `project_member`, emit `MembershipChanged` lần 2. Timeout/decline/revoke → trạng thái terminal (saga compensation). Authoring/TestArtifact cập nhật `membership_snapshot`. **FR-07..09.**

### Flow C · Viết story / Import Jira — ⭐ Authoring
Editor tạo story (as-a/I-want/so-that) + AC phân cấp + business rule → persist (authoring status) → emit `UserStoryCreated` → TestArtifact tạo placeholder set + Integration link Jira. **Inbound:** Integration (ACL) verify+dịch webhook Jira → emit import → Authoring tạo story với `external_ref`. **FR-10..14, 29..31.**

### Flow D · Sinh test case bằng AI — ⭐ AI Generation
QA request generate → trả `202 + jobId` ngay (async) → AI Gen lấy story+AC từ Authoring qua **gRPC** → gọi LLM (primary, fallback secondary qua circuit breaker) → persist job + emit `TestCasesGenerated` trong 1 transaction → TestArtifact lưu draft (idempotent). Progress real-time qua Redis/SignalR. **FR-15..19.**

### Flow E · Chạy test run — ⭐ Execution
QA start run cho suite (manual/automated) → TestArtifact tạo run record + emit `TestRunRequested` → Execution chạy Playwright trong **sandbox cô lập, giới hạn tài nguyên** → ghi kết quả per-case (passed/failed/blocked/skipped/not_run) + artifact (screenshot/video/trace) vào **MinIO** (DB chỉ lưu `storage_path`) → emit `TestRunCompleted` → TestArtifact cập nhật kết quả + Notification báo user. FAIL → raise defect (optional push Jira). **FR-24..28.**

### Flow F · Thông báo — ⭐ Notification
Pure consumer: nhận `TestRunCompleted` + `MembershipChanged` → đọc `notification_preferences` → ghi **MongoDB** → gửi in-app/email. **FR-32.**

### Ma trận service × flow (⭐ chủ trì · ✓ tham gia)
| Service · Owner | A | B | C | D | E | F |
|---|:-:|:-:|:-:|:-:|:-:|:-:|
| Identity · **Minh** | ⭐ | | | | | |
| Workspace · **Bảo** | ✓ | ⭐ | | | | |
| Authoring · **Giang** | | | ⭐ | ✓ gRPC | | |
| TestArtifact · **Giang** | | | ✓ | ✓ | ⭐ | |
| AI Generation · **Văn** | | | | ⭐ | | |
| Execution · **Văn** | | | | | ⭐ | |
| Integration · **Minh** | | | ✓ | ✓ | (defect) | |
| Notification · **Bảo** | | ✓ | | | ✓ | ⭐ |

**Flow nào ai lo:** A→Minh · B→Bảo · C→Giang(+Minh Jira) · D→Văn(+Giang gRPC) · E→Văn(+Giang) · F→Bảo.

---

## 5. Event catalog (producer → consumer)

| Event | Producer | Consumer | Flow |
|---|---|---|---|
| `UserRegistered` | Identity | Workspace, Notification | A |
| `MembershipChanged` | Workspace | Authoring, TestArtifact, Notification | B |
| `ProjectCreated` | Workspace | Integration | B |
| `UserStoryCreated` | Authoring | TestArtifact, Integration | C |
| `TestCasesGenerated` | AI Generation | TestArtifact, (Integration) | D |
| `TestCasesSaved` | TestArtifact | Authoring, Integration | D |
| `TestRunRequested` | TestArtifact | Execution, AI Generation | E |
| `TestRunCompleted` | Execution | TestArtifact, Notification | E |
| `JiraIssueLinked` | Integration | — | C |

> Tên event là quy ước; **contract record cụ thể** (field) do producer định nghĩa trong `*.Contracts`. Mẫu sẵn: `services/authoring/QuraEx.Authoring.Contracts/{StoryChanged,MembershipChanged}.cs`. **Producer chốt shape sớm (contract-first), thông báo consumer.**

---

## 6. DB golden-flow — mọi người làm y hệt

Source of truth = `database/quraex.dbml` + EF migration mỗi service. Quy ước đầy đủ ở [`database/conventions.md`](./database/conventions.md). Tóm tắt:

### Cách xem file `quraex.dbml` (sơ đồ ERD)
File `.dbml` là text mô tả schema — để xem dạng sơ đồ trực quan:
- **Cách 1 — dbdiagram.io (dễ nhất, không cài gì):** mở https://dbdiagram.io → bấm **New diagram** → mở `docs/database/quraex.dbml`, copy toàn bộ nội dung → dán vào ô bên trái → sơ đồ ERD hiện bên phải. Tìm group của mình (vd `// 5 · TestArtifact`).
- **Cách 2 — VSCode extension:** cài extension **"DBML"** (`matt-meyers.vscode-dbml`) để highlight cú pháp; muốn preview sơ đồ thì cài **"ERD Editor"** rồi mở file `.dbml`.
- **Cách 3 — CLI (nếu cần xuất SQL):** `npm i -g @dbml/cli` → `dbml2sql docs/database/quraex.dbml --postgres` để xem SQL tương ứng.
- File ERD dựng sẵn: [`database/QuraEx_DB_ERD.drawio`](./database/QuraEx_DB_ERD.drawio) (mở bằng draw.io / extension Draw.io trong VSCode).


**Cross-cutting (mọi service Postgres):**
- PK `id uuid` = **UUIDv7** (`Guid.CreateVersion7()`), snake_case bảng/cột, enum lưu **varchar** (`HasConversion<string>()`).
- `created_at/updated_at` timestamptz UTC; `created_by/updated_by` chỉ trên business aggregate.
- Soft-delete = **opt-in `ISoftDeletable`** (`deleted_at`); infra table (`*_outbox_message`, `*_processed_message`, `*_snapshot`) **KHÔNG** soft-delete. Cột unique phải dùng **partial unique index** `WHERE deleted_at IS NULL`.
- Cross-service id (`user_id`, `project_id`, …) = **bare UUID, KHÔNG FK chéo service**.
- Event infra: `*_outbox_message` (producer) + `*_processed_message` (consumer, retention 7d) — đã có sẵn trong stub.

**7 bước thêm/sửa bảng:** ① sửa `quraex.dbml` → preview ERD → review với lead · ② viết entity (private setter, factory `Create()`, invariant trong entity) · ③ `IEntityTypeConfiguration<T>` (Fluent, snake_case, enum varchar, soft-delete filter) · ④ `dotnet ef migrations add <Name>` · ⑤ **đọc SQL migration, diff với DBML — lệch thì KHÔNG merge** · ⑥ apply: startup `Migrate()` chỉ trong `IsDevelopment()` guard + `pg_advisory_lock` + retry (đã wire sẵn) · ⑦ commit entity+config+migration+DBML trong **1 PR**.

---

## 7. Building blocks dùng chung (KHÔNG viết lại)

`building-blocks/QuraEx.BuildingBlocks`:
- `Result<T>` / `Error` / `ErrorType` — success-or-error, `.Match()`. Handler trả `Result<T>` → endpoint map sang `Results.Ok/Problem`.
- `BaseEntity` (UUIDv7, domain events), `AggregateRoot`, `ISoftDeletable`, `SoftDeletableAggregate`.
- `DomainEvent` (in-process, MediatR), dispatch sau commit.
- `AddBuildingBlocks(markers)` — đăng ký MediatR + behaviors (**validation → logging → transaction**) + `ICurrentUser`.
- `UseQuraExExceptionHandling()` — exception → RFC7807 ProblemDetails (NotFound→404, Validation→422, Concurrency→409).
- `ICurrentUser` — lấy `UserId` từ claim `sub`.
- `OutboxMessage` / `ProcessedMessage` + `AddProcessedMessageRetention()`; `EfConventions.ApplyQuraExConventions()` + `HasPartialUniqueIndex()`.

`building-blocks/QuraEx.BuildingBlocks.Messaging`:
- `IntegrationEvent` — base record cho event chéo service.
- `AddQuraExMessaging<TDbContext>(cfg, serviceName, markers)` — 1 call: MassTransit + RabbitMQ + EF Outbox + auto-discover consumer + retry/redelivery/dead-letter + idempotent filter.
- `IdempotentConsumerFilter<T>` — dedup qua `processed_message`.

`aspire/QuraEx.ServiceDefaults` — `AddServiceDefaults()` (OTel, resilience, service discovery) + `MapDefaultEndpoints()` (`/alive`, `/health`).

---

## 8. Definition of Done (mỗi service)

Một service coi là xong 1 slice khi:
- [ ] Entity + `IEntityTypeConfiguration` + EF migration (đã đọc SQL, khớp DBML).
- [ ] Migration auto-apply ở dev (qua `MigrationHostedService` sẵn có).
- [ ] CRUD/command slice chạy được **qua gateway** (`/api/<svc>/...`), có `.RequireAuthorization()`.
- [ ] Validator FluentValidation cho command có input.
- [ ] Emit/consume event qua **Outbox** (nếu flow yêu cầu), consumer idempotent.
- [ ] **Testcontainers integration test** xanh (CRUD happy path + 401 + health + assert event publish).
- [ ] CI xanh (build path-filtered + analyzers + test + gitleaks).
- [ ] Theo đúng pattern Authoring (xem checklist trong file thành viên).

---

## 9. Phasing chống block & quy ước git

**Pha 0 (song song, KHÔNG block):** mọi người scaffold service + làm DBML entity + migration + CRUD slice, test bằng **dev-JWT** (public key dùng chung đã có trong `appsettings.Development.json`) — không chờ Cognito. Minh setup AWS Cognito User Pool song song.
**Pha 1:** chuyển toàn team sang **JWT thật từ AWS Cognito** (gateway + mọi service trỏ JWKS Cognito; Văn chỉnh gateway theo issuer Minh cung cấp).
**Pha 2:** wire Outbox/consumer giữa các service (event thật chạy).
**Pha 3:** Integration (Jira) + Notification (đều chỉ subscribe, làm cuối).

**Đường găng:** Identity (Minh) → Workspace (Bảo) → TestArtifact (Giang) → AI Gen (Văn) → Execution (Văn).

**Git:** `feature/* → PR → dev`; release `dev → main` (main protected). Conventional Commits (hook `commit-msg` enforce, không `wip`/AI refs). CODEOWNERS map mỗi folder service → owner = auto-reviewer.

---

## 10. ⚠️ Cảnh báo bàn giao (đọc kỹ)

1. **Branch `dev` đang là bản rỗng** (`40359fb` init skeleton, 0 file code). Code thật ở `main`. Lead (Văn) sẽ sync `dev` về `main` trước khi team bắt đầu — **đừng base feature lên `dev` cho tới khi được báo đã sync.**
2. **Gateway = YARP, KHÔNG phải Kong.** Vài chỗ docs cũ (onboarding/TASKS) ghi "Kong DB-less" — đó là định hướng cũ, **chưa/không triển khai**. Hiện tại + phase này dùng YARP (.NET reverse proxy, file `gateway/QuraEx.Gateway/`). Không đi tìm config Kong.
3. **Cloud đích = AWS** (EKS+RDS+S3+Secrets Manager) phần lớn còn là **thiết kế giấy**; deploy hiện tại = DigitalOcean droplet (docker-compose.prod), code local S3 = **MinIO**. **Ngoại lệ:** IAM **dùng AWS Cognito thật ngay** (xem [`team/Minh.md`](./team/Minh.md)). Ngoài auth, đừng hardcode SDK AWS lúc dựng service.
4. **MinIO (Execution) + MongoDB (Notification) chưa wire** vào Aspire/compose — owner tự thêm theo snippet trong `TASKS.md` khi tới pha.

---

## Quyết định đã chốt
- **Auth = AWS Cognito** (managed) thay cho OpenIddict/ASP.NET Identity.
- **Service Identity = Cách A** (giữ mỏng): bảng `user_profile` đồng bộ từ Cognito + emit `UserRegistered`. User id mọi nơi = Cognito `sub`. → `quraex.dbml` group 1 + `conventions.md` §2 đã cập nhật.

## Câu hỏi chưa chốt
- `invitation` mới ở mức project-scoped (mang `role`); invitation cấp workspace (ADMIN/MEMBER) hoãn — xác nhận OK cho MVP.
