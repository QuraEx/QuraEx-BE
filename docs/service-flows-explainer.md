# QuraEx Backend — Giải thích Service & Luồng nghiệp vụ (tài liệu dạy team)

> Mục đích: tài liệu để lead trình bày cho cả nhóm hiểu **mỗi service làm gì · DB gánh nhiệm vụ gì · các luồng chạy ra sao**.
> Render Mermaid: xem trực tiếp trên GitHub, hoặc dán vào [mermaid.live](https://mermaid.live).
> Nguồn gốc: `docs/QuraEx_Architecture.md` (§4 luồng, §7 DB) + `docs/TASKS.md` (event contracts).

---

## 0. Cách đọc tài liệu này

Hệ thống = **8 microservice nghiệp vụ** + hạ tầng (Kong gateway, RabbitMQ, Redis). Mỗi service:
- **Sở hữu 1 DB riêng** — không service nào đọc DB service khác.
- **Giao tiếp 2 kiểu**: gRPC (đồng bộ, hiếm) + event qua RabbitMQ (bất đồng bộ, mặc định).
- **Tổ chức code giống nhau**: Vertical Slice + CQRS/MediatR + Clean Architecture.

### Mô hình tinh thần — 3 pattern lặp lại ở MỌI service (phải hiểu trước)

Trước khi đi vào từng service, team phải thấm 3 thứ — vì chúng giải thích **vì sao mọi DB đều có mấy bảng giống nhau**:

| Pattern | Bảng | Giải quyết vấn đề gì |
|---|---|---|
| **Transactional Outbox** | `*_outbox_message` | Khi vừa ghi DB vừa muốn phát event: nếu ghi DB xong mà gửi RabbitMQ lỗi (hoặc ngược lại) → lệch dữ liệu (*dual-write*). Giải: ghi event vào bảng outbox **trong cùng transaction với dữ liệu**, một relay đọc outbox bắn lên RabbitMQ sau. |
| **Idempotency** | `*_processed_message` | RabbitMQ giao *at-least-once* → 1 event có thể tới 2 lần. Giải: trước khi xử lý, check message_id đã có trong `processed_message` chưa; có rồi thì bỏ qua. |
| **Read-model / Snapshot** | `*_snapshot` | Service B cần dữ liệu của service A nhưng không được đọc DB A. Giải: A phát event, B lưu bản sao nhẹ (`membership_snapshot`, `story_snapshot`) và tự cập nhật từ event. |

```mermaid
flowchart LR
    subgraph S["1 transaction trong service"]
        W[Ghi dữ liệu nghiệp vụ] --- O[(outbox_message)]
    end
    O -->|relay đọc & bắn| MQ[(RabbitMQ)]
    MQ -->|at-least-once| C{processed_message<br/>đã xử lý?}
    C -->|chưa| H[Xử lý + lưu snapshot]
    C -->|rồi| X[Bỏ qua - idempotent]
```

---

## 1. Bản đồ hệ thống (system context)

```mermaid
flowchart TB
    FE["Client / Frontend (Next.js)"] --> GW["Kong Gateway<br/>(JWT verify · route · rate-limit)"]

    GW --> ID[Identity]
    GW --> WS[Workspace]
    GW --> AU[Authoring]
    GW --> TA[TestArtifact]
    GW --> AI[AI Generation]
    GW --> EX[Execution]
    GW --> IN[Integration/Jira]

    ID --- dbID[(PostgreSQL)]
    WS --- dbWS[(PostgreSQL)]
    AU --- dbAU[(PostgreSQL)]
    TA --- dbTA[(PostgreSQL)]
    AI --- dbAI[(PostgreSQL + Redis)]
    EX --- dbEX[(PostgreSQL + MinIO/S3)]
    IN --- dbIN[(PostgreSQL)]
    NO[Notification] --- dbNO[(MongoDB)]

    ID -. events .-> MQ[(RabbitMQ)]
    WS -. events .-> MQ
    AU -. events .-> MQ
    TA -. events .-> MQ
    AI -. events .-> MQ
    EX -. events .-> MQ
    IN -. events .-> MQ
    MQ -. events .-> NO

    AI -->|gRPC sync| AU
    EXT["Jira (hệ ngoài)"] <-->|webhook / REST| IN
    LLM["LLM (OpenAI/Gemini)"] <--> AI
```

**Đọc sơ đồ:** chỉ **Kong** lộ ra internet. Frontend luôn gọi qua Kong, không bao giờ gọi thẳng service. Service nói chuyện với nhau **chủ yếu qua RabbitMQ** (đường nét đứt), chỉ AI Gen → Authoring dùng gRPC (đường liền) vì cần trả lời ngay.

---

## 2. Từng service: vai trò · DB · event vào/ra

> Quy ước: **user_id ở mọi service là UUID tham chiếu**, KHÔNG phải khóa ngoại xuyên service.

### 2.1 Identity (PostgreSQL) — *"Bạn là ai"* (Authentication)
Quản lý danh tính: user, mật khẩu, JWT, OAuth, 2FA. Tự host OIDC bằng **OpenIddict**, phát RS256 JWT + JWKS để Kong và mọi service validate.

| Bảng | Nhiệm vụ |
|---|---|
| `app_user` | tài khoản: email (UK), `password_hash` (Argon2id), display_name, status |
| `refresh_token` | lưu **hash** token (không lưu thô), expires_at, revoked |
| `user_mfa` | secret 2FA, enabled |
| `openiddict_application/authorization/scope/token` | hạ tầng OIDC: client, grant, scope, token |
| `identity_outbox_message` | phát event ra ngoài |

- **Phát:** `UserRegistered`, `UserUpdated`
- **Nhận:** — (không phụ thuộc ai → vì vậy build đầu tiên)

### 2.2 Workspace (PostgreSQL) — *"Bạn được làm gì"* (Authorization)
Phân quyền **theo ngữ cảnh project**: workspace, project, membership, mời thành viên. Tách khỏi Identity vì quyền ở đây phụ thuộc dữ liệu membership.

| Bảng | Nhiệm vụ |
|---|---|
| `workspace` | không gian làm việc, type PERSONAL/TEAM, owner_user_id |
| `project` | dự án trong workspace, `project_key` (UK) |
| `workspace_member` | vai trò cấp workspace: OWNER/ADMIN/MEMBER |
| `project_member` | vai trò cấp project: EDITOR/VIEWER |
| `invitation` | lời mời theo project: email, `token_hash`, status, expires_at |
| `workspace_outbox_message` | phát event |

- **Phát:** `MembershipChanged`, `ProjectCreated`
- **Nhận:** `UserRegistered` (seed profile user)

### 2.3 Authoring (PostgreSQL) — Nguồn yêu cầu ✅ *service mẫu*
Quản lý user story + acceptance criteria + business rule. Là service tham chiếu để mọi service khác copy pattern.

| Bảng | Nhiệm vụ |
|---|---|
| `user_story` | story: title, as_a/i_want_to/so_that, authoring_status, `external_ref` (link Jira) |
| `acceptance_criteria` | tiêu chí, phân cấp (`parent_id`), order_no, completed |
| `business_rule` | quy tắc nghiệp vụ gắn story |
| `membership_snapshot` | **read-model** quyền từ Workspace (check quyền không gọi chéo) |
| `authoring_outbox_message` + `authoring_processed_message` | hạ tầng event |

- **Phát:** `UserStoryCreated`
- **Nhận:** `MembershipChanged` (cập nhật snapshot)
- **Đặc biệt:** mở 1 endpoint **gRPC** để AI Gen lấy nội dung story.

### 2.4 TestArtifact (PostgreSQL) — Kho test + vòng đời chạy
Lưu test case và quản lý vòng đời test run.

| Bảng | Nhiệm vụ |
|---|---|
| `test_case` | steps, expected, `polarity` (POSITIVE/NEGATIVE), `design_technique` (BVA/EP/Decision Table), priority, lifecycle_status, `generated_by_ai` |
| `test_suite` / `test_suite_item` | gom test case theo chủ đề (N:M) |
| `test_plan` | tài liệu chiến lược cấp project (scope/mục tiêu/rủi ro) |
| `test_run` | một lần chạy 1 suite tại 1 thời điểm |
| `test_run_result` | kết quả từng case: PASS/FAIL/BLOCKED/SKIPPED/NOT_RUN/IN_PROGRESS |
| `defect` | bug: severity + status — cầu nối đẩy Jira |
| `story_snapshot` | read-model story từ Authoring |
| outbox + processed | hạ tầng event |

- **Phát:** `TestCasesSaved`, `TestRunRequested`
- **Nhận:** `UserStoryCreated`, `MembershipChanged`

### 2.5 AI Generation (PostgreSQL + Redis) — Bộ não sinh test (Saga)
Saga điều phối: lấy story (gRPC) → gọi LLM router → phát test case.

| Bảng / store | Nhiệm vụ |
|---|---|
| `generation_job` | job: story_id, job_type (refine/ac/tc), status, llm_source |
| `llm_provider_config` | cấu hình LLM theo project: preferred_source, model, temperature, max_tokens |
| `ai_saga_state` | trạng thái Saga (MassTransit) |
| **Redis** | `job:{id}` tiến độ real-time, Pub/Sub xuống SignalR |
| `ai_outbox_message` + `ai_processed_message` | hạ tầng event |

- **Phát:** `TestCasesGenerated`
- **Nhận:** `TestRunRequested`
- **gRPC ra:** gọi Authoring lấy story.

### 2.6 Execution (PostgreSQL + Object storage) — Chạy test thật
Điều khiển Playwright chạy test trong container cô lập.

| Bảng | Nhiệm vụ |
|---|---|
| `exec_run` | environment, status, triggered_by, started/finished |
| `exec_result` | kết quả từng case: status, duration, error_message |
| `exec_artifact` | type (screenshot/video/trace), `storage_path` — **chỉ lưu path**, file ở MinIO/S3 |
| `test_script` | framework (Playwright), script_content, `generated_by_ai` |
| outbox + processed | hạ tầng event |

- **Phát:** `TestRunCompleted`
- **Nhận:** `TestRunRequested`, `TestCasesGenerated`
- **Bảo mật bắt buộc:** worker chạy browser trong sandbox, giới hạn CPU/RAM/timeout.

### 2.7 Integration / Jira (PostgreSQL) — Cổng nối hệ ngoài (ACL)
Anti-Corruption Layer: đồng bộ 2 chiều Jira, cô lập cấu trúc Jira khỏi nội bộ.

| Bảng | Nhiệm vụ |
|---|---|
| `jira_connection` | oauth_token/refresh_token, jira_site_id theo project, status |
| `external_link` | ánh xạ internal_id ↔ external_id/key, last_synced_at, sync_status |
| `outbound_sync_job` | hàng đợi đẩy ngược Jira, payload, retry_count |
| `processed_webhook` | dedup webhook theo `event_id` |
| outbox + saga | hạ tầng event + outbound saga |

- **Phát:** `JiraIssueLinked`
- **Nhận:** `UserStoryCreated`, `ProjectCreated`

### 2.8 Notification (MongoDB) — Loa thông báo (fan-out)
Nhận event hoàn tất → gửi in-app/email/push. Dùng Mongo vì payload linh hoạt.

| Collection | Nhiệm vụ |
|---|---|
| `notifications` | user_id, type, payload (object linh hoạt), channel, status, read |
| `notification_preferences` | user bật/tắt email/push theo type |
| `processed_messages` | idempotency |

- **Phát:** —
- **Nhận:** `TestRunCompleted`, `MembershipChanged`

---

## 3. Sáu luồng nghiệp vụ chính

Mỗi luồng: *kích hoạt → service tham gia → event → kết thúc*, kèm sequence diagram.

### Luồng A — Đăng ký & đăng nhập

```mermaid
sequenceDiagram
    autonumber
    actor User
    participant GW as Kong Gateway
    participant ID as Identity
    participant WS as Workspace
    User->>GW: POST /register (email, password)
    GW->>ID: route (anonymous)
    ID->>ID: tạo app_user, hash Argon2id
    ID->>ID: ghi Outbox(UserRegistered)
    ID-->>User: 201 Created
    ID-)WS: event UserRegistered
    WS->>WS: seed user profile (idempotent)

    User->>GW: POST /connect/token (login)
    GW->>ID: route
    ID-->>User: RS256 JWT (access + refresh)
    Note over GW,ID: Mọi request sau: Kong verify JWT qua JWKS của Identity
```
**DB chạm:** `app_user`, `refresh_token`, `identity_outbox_message` → `workspace_member`. **Pattern:** Outbox + idempotent consumer.

### Luồng B — Tạo project & mời thành viên (Saga đơn giản)

```mermaid
sequenceDiagram
    autonumber
    actor Owner
    participant GW as Kong Gateway
    participant WS as Workspace
    participant NO as Notification
    actor Invitee
    Owner->>GW: POST /projects
    GW->>WS: route (JWT verified)
    WS->>WS: tạo project + ghi Outbox(ProjectCreated)
    WS-->>Owner: 201 Created

    Owner->>GW: POST /projects/{id}/members (email)
    GW->>WS: route
    WS->>WS: tạo invitation (token_hash, status=Pending)
    WS->>WS: Saga: Pending -> AwaitingResponse
    WS-)NO: event MembershipChanged (mời)
    NO-->>Invitee: email lời mời (kèm token)

    alt Invitee chấp nhận trong 7 ngày
        Invitee->>GW: POST /invitations/accept (token)
        GW->>WS: route
        WS->>WS: Saga -> Accepted, tạo project_member
        WS-)NO: MembershipChanged (đã vào nhóm)
    else Quá 7 ngày
        WS->>WS: Saga -> Expired (đường bù trừ)
        WS->>WS: hủy invitation
        WS-)NO: thông báo hết hạn
    end
```
**Trạng thái Saga:** `Pending → AwaitingResponse → Accepted` (hoặc `Expired`). Token mời lưu **hash**, không lưu thô. **DB chạm:** `project`, `invitation`, `project_member`, outbox. **Pattern:** Saga + compensation (timeout).

### Luồng C — Viết user story & import từ Jira

```mermaid
sequenceDiagram
    autonumber
    actor User
    participant GW as Kong Gateway
    participant AU as Authoring
    participant TA as TestArtifact
    participant IN as Integration
    participant EXT as Jira

    rect rgb(20,40,60)
    Note over User,IN: Nội bộ — tạo story trong app
    User->>GW: POST /user-stories
    GW->>AU: route
    AU->>AU: tạo user_story + Outbox(UserStoryCreated)
    AU-->>User: 201 Created
    AU-)TA: UserStoryCreated -> tạo placeholder test set
    AU-)IN: UserStoryCreated -> tạo/link Jira issue
    end

    rect rgb(50,30,20)
    Note over IN,AU: Inbound từ Jira — ACL
    EXT-->>IN: webhook (Jira issue created)
    IN->>IN: verify chữ ký + dedup processed_webhook
    IN->>IN: ACL dịch payload Jira -> mô hình nội bộ sạch
    IN->>IN: upsert external_link
    IN-)AU: ExternalStoryImported
    AU->>AU: tạo story gắn external_ref
    end
```
**Điểm vàng để giải thích:** Authoring **không bao giờ** biết cấu trúc JSON của Jira — Integration (ACL) dịch sạch trước. Đổi provider khác (Azure DevOps…) chỉ sửa Integration. **DB chạm:** `user_story`, `external_link`, `processed_webhook`.

### Luồng D — Generate test case ⭐ (trái tim, chạm mọi pattern)

```mermaid
sequenceDiagram
    autonumber
    actor User
    participant GW as Kong Gateway
    participant AI as AI Generation
    participant AU as Authoring
    participant LLM as LLM Router
    participant TA as TestArtifact
    participant RT as Redis/SignalR
    User->>GW: POST /generate (storyId)
    GW->>AI: route
    AI->>AI: tạo generation_job = PENDING
    AI-->>User: 202 Accepted + jobId
    AI->>RT: publish tiến độ job:{id}
    RT-->>User: realtime progress (SignalR)
    AI->>AU: gRPC GetStory(storyId)
    AU-->>AI: story + acceptance criteria
    AI->>LLM: generate(prompt)
    alt nguồn chính lỗi/timeout
        LLM-->>AI: fallback nguồn 2 (circuit breaker)
    end
    LLM-->>AI: test cases
    Note over AI: 1 transaction (giải dual-write)
    AI->>AI: job=COMPLETED + Outbox(TestCasesGenerated)
    AI-)TA: TestCasesGenerated
    TA->>TA: dedup processed_message (idempotent)
    TA->>TA: lưu test_case (generated_by_ai=true)
    TA-)AU: TestCasesSaved
    par song song
        AU->>AU: cập nhật authoring_status
    and
        TA-)IN: (nếu story có link) đẩy Jira
    end
```
**Pattern thể hiện:** 202+SignalR (async UX) · gRPC (sync khi cần ngay) · Strategy+circuit breaker (LLM router) · Outbox (dual-write) · Idempotency · choreography. Chi tiết: `docs/diagrams/generate-test-case-backbone-flow.md`.

### Luồng E — Thực thi test tự động (Playwright)

```mermaid
sequenceDiagram
    autonumber
    actor User
    participant GW as Kong Gateway
    participant TA as TestArtifact
    participant EX as Execution
    participant OBJ as MinIO/S3
    participant NO as Notification
    User->>GW: POST /test-runs (suiteId)
    GW->>TA: route
    TA->>TA: tạo test_run + Outbox(TestRunRequested)
    TA-->>User: 202 + runId
    TA-)EX: TestRunRequested
    EX->>EX: dedup + tạo exec_run
    loop mỗi test case trong suite
        EX->>EX: chạy Playwright (container cô lập, giới hạn CPU/RAM/timeout)
        EX->>OBJ: upload screenshot/video/trace
        EX->>EX: lưu exec_result + exec_artifact(storage_path)
    end
    EX->>EX: Outbox(TestRunCompleted)
    EX-)TA: TestRunCompleted -> cập nhật test_run_result
    EX-)NO: TestRunCompleted
    NO-->>User: thông báo kết quả
    opt có case FAIL
        EX-)TA: tạo defect -> có thể đẩy Jira
    end
```
**Nhấn mạnh:** browser chạy trong **sandbox cô lập** (bảo mật bắt buộc); DB chỉ giữ **đường dẫn** artifact, file nằm ở object storage. **DB chạm:** `exec_run/result/artifact`, `test_run_result`, `defect`.

### Luồng F — Thông báo (fan-out)

```mermaid
sequenceDiagram
    autonumber
    participant MQ as RabbitMQ
    participant NO as Notification
    actor User
    MQ-)NO: TestRunCompleted
    MQ-)NO: MembershipChanged
    NO->>NO: dedup processed_messages
    NO->>NO: đọc notification_preferences (email/push on?)
    NO->>NO: lưu notifications (MongoDB)
    NO-->>User: in-app + email (theo preference)
```
**DB chạm:** `notifications`, `notification_preferences` (MongoDB). **Pattern:** pure consumer, không phát event, không nằm trên đường chính → làm sau cùng được.

---

## 3.7 State diagram — vòng đời 2 thực thể có trạng thái phức tạp

Hai chỗ team hay nhầm "trạng thái chạy lung tung" — vẽ rõ vòng đời để thấy nó có kỷ luật.

### Saga mời thành viên (`invitation` trong Workspace)

```mermaid
stateDiagram-v2
    [*] --> Pending: owner tạo lời mời<br/>(token_hash, chưa gửi)
    Pending --> AwaitingResponse: phát MembershipChanged<br/>Notification gửi mail
    AwaitingResponse --> Accepted: invitee bấm accept<br/>(token hợp lệ, ≤ 7 ngày)
    AwaitingResponse --> Declined: invitee từ chối
    AwaitingResponse --> Expired: quá 7 ngày<br/>(saga timeout)
    AwaitingResponse --> Revoked: owner thu hồi

    Accepted --> [*]: tạo project_member<br/>phát MembershipChanged
    Declined --> [*]
    Expired --> [*]: đường bù trừ —<br/>hủy invitation + mail báo hết hạn
    Revoked --> [*]: hủy invitation

    note right of Expired
        Compensation: saga tự chạy
        khi hết hạn, không cần
        thao tác thủ công
    end note
```

**Điểm dạy:**
- Token mời lưu **hash**, so khi accept thì hash lại để đối chiếu — DB rò rỉ cũng không dùng được token.
- `Expired` là **đường bù trừ tự động** của Saga (timeout 7 ngày), minh họa rõ "compensation" mà không cần distributed transaction.
- Chỉ `Accepted` mới sinh `project_member` + phát `MembershipChanged` lần 2 (để các service cập nhật snapshot).

### Vòng đời `generation_job` (AI Generation)

```mermaid
stateDiagram-v2
    [*] --> PENDING: client gọi /generate<br/>trả 202 + jobId ngay

    PENDING --> FETCHING_STORY: saga bắt đầu
    FETCHING_STORY --> GENERATING: gRPC lấy story OK
    FETCHING_STORY --> FAILED: lấy story lỗi<br/>(chưa có side-effect)

    GENERATING --> PERSISTING: LLM trả test cases
    state GENERATING {
        [*] --> Source1: thử self-host/nguồn chính
        Source1 --> Source2: lỗi/timeout<br/>(circuit breaker)
        Source1 --> done: OK
        Source2 --> done: OK
        Source2 --> bothFailed: cả 2 nguồn lỗi
    }
    GENERATING --> FAILED: cả 2 nguồn LLM lỗi

    PERSISTING --> COMPLETED: 1 transaction —<br/>job=COMPLETED + Outbox(TestCasesGenerated)

    COMPLETED --> [*]
    FAILED --> [*]: đánh dấu FAILED + báo client qua SignalR

    note right of PERSISTING
        Nếu TestArtifact lưu lỗi:
        event trong Outbox được redeliver
        (at-least-once), TestArtifact
        idempotent nên tự lành —
        KHÔNG cần bù trừ thủ công
    end note
```

**Điểm dạy (theo Architecture §4.1):**
- Client thấy job qua **Redis/SignalR**: `PENDING → GENERATING → COMPLETED` chạy realtime, không phải refresh.
- **3 mức lỗi xử lý khác nhau**: ① lấy story lỗi = chưa side-effect → FAILED gọn; ② LLM lỗi = fallback nguồn 2, chỉ FAILED khi cả 2 chết; ③ lưu TestArtifact lỗi = redeliver + idempotent tự lành.
- `COMPLETED` chỉ đạt được khi **Outbox đã ghi trong cùng transaction** → không bao giờ có "job xong nhưng event mất".

---

## 4. Bản đồ "service nào nhúng vào luồng nào" (tra nhanh)

| Service | A Login | B Invite | C Story/Jira | D Generate | E Execute | F Notify |
|---|:--:|:--:|:--:|:--:|:--:|:--:|
| Identity | ⭐ | | | | | |
| Workspace | ✓ | ⭐ | | | | |
| Authoring | | | ⭐ | ✓ (gRPC) | | |
| TestArtifact | | | ✓ | ✓ | ⭐ | |
| AI Generation | | | | ⭐ | | |
| Execution | | | | | ⭐ | |
| Integration | | | ✓ | ✓ | (defect) | |
| Notification | | ✓ | | | ✓ | ⭐ |

⭐ = chủ đạo · ✓ = tham gia

---

## 5. Talking points — 5 câu chốt khi trình bày

1. **"Microservice = độc lập lúc deploy, không phải nhiều repo."** Ta là monorepo, mỗi service vẫn deploy riêng.
2. **"Mỗi service 1 DB, không đọc chéo."** Cần data người khác → snapshot từ event. Đây là lý do có `*_snapshot`.
3. **"Outbox + processed_message ở mọi nơi"** là cách giữ nhất quán không cần distributed transaction.
4. **"gRPC hiếm, event là mặc định."** Chỉ dùng gRPC khi cần câu trả lời ngay (D bước lấy story).
5. **"Eventual consistency."** Story "đã có test case" sau vài giây — chấp nhận trễ nhẹ, đổi lại hệ chịu lỗi tốt và scale độc lập.

---

## 6. Liên kết tài liệu

- `docs/QuraEx_Architecture.md` — thiết kế hệ thống đầy đủ (11 chương)
- `docs/database/quraex.dbml` — schema tổng 8 service (source of truth)
- `docs/database/conventions.md` — quy tắc DB chung
- `docs/diagrams/generate-test-case-backbone-flow.md` — sequence luồng D chi tiết
- `docs/TASKS.md` — build order + event contracts + owner
- `docs/onboarding-deck.html` — slide trình chiếu
