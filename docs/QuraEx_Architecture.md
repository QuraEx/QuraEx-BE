**QuraEx**

Nền tảng sinh Test Case bằng AI

**TÀI LIỆU THIẾT KẾ KIẾN TRÚC HỆ THỐNG**

*Backend Microservice trên ASP.NET Core*

Phiên bản 1.0 — Bản thiết kế tái khởi động

Ngày: 03/06/2026

*Tài liệu nội bộ phục vụ họp nhóm*

# Mục lục

# 1. Tổng quan dự án

## 1.1. Bối cảnh

QuraEx là nền tảng tạo test case tự động từ user story bằng mô hình ngôn ngữ lớn (LLM). Dự án được tái khởi động từ một phiên bản trước đó gồm backend Spring Boot (Clean Architecture, monolith) và frontend Next.js. Định hướng mới giữ nguyên frontend và mobile, nhưng tái thiết kế toàn bộ backend theo kiến trúc microservice trên nền ASP.NET Core, với mục tiêu áp dụng các pattern hiện đại một cách bài bản.

## 1.2. Mục tiêu thiết kế

- Tái thiết kế backend thành hệ microservice chỉn chu, mỗi service là một bounded context rõ ràng.
- Áp dụng đầy đủ các pattern của hệ phân tán: database-per-service, gRPC, event-driven, Saga, Outbox, CQRS, Anti-Corruption Layer, Idempotency.
- Bổ sung tích hợp Jira hai chiều và năng lực thực thi test tự động (Playwright).
- Chuẩn hóa lại thuật ngữ kiểm thử theo ISTQB, sửa các lỗi gom nhầm khái niệm của phiên bản cũ.

## 1.3. Tech stack

| **Thành phần** | **Công nghệ** |
| --- | --- |
| Backend | ASP.NET Core (.NET 8+), microservice |
| Giao tiếp đồng bộ | gRPC |
| Giao tiếp bất đồng bộ | RabbitMQ + MassTransit |
| Cache & real-time | Redis (SignalR backplane) |
| API Gateway | Kong API Gateway (DB-less declarative, OpenResty/Nginx) |
| Database | Polyglot — PostgreSQL, MongoDB, Object storage |
| Observability | .NET Aspire + OpenTelemetry |
| Test automation | Playwright (trong container cô lập) |
| Frontend / Mobile | Next.js / giữ nguyên từ bản cũ |

# 2. Kiến trúc tổng thể

## 2.1. Nguyên tắc chia service

Hệ thống được chia theo bounded context (Domain-Driven Design), không chia theo tầng kỹ thuật hay theo bảng dữ liệu. Mỗi service sở hữu database riêng, deploy độc lập, và không service nào được đọc trực tiếp database của service khác.

## 2.2. Danh sách 8 service nghiệp vụ

| **#** | **Service** | **Vai trò** | **Database** |
| --- | --- | --- | --- |
| 1 | Identity | Authentication, user, JWT, OAuth | PostgreSQL |
| 2 | Workspace | Authorization theo project, workspace, membership | PostgreSQL |
| 3 | Authoring | User story, acceptance criteria, business rule | PostgreSQL |
| 4 | TestArtifact | Test case, suite, plan, run, defect | PostgreSQL |
| 5 | AI Generation | Router LLM 2 nguồn, Saga điều phối | PostgreSQL + Redis |
| 6 | Execution | Chạy Playwright tự động, lưu artifact | PostgreSQL + Object storage |
| 7 | Integration (Jira) | Anti-Corruption Layer, đồng bộ 2 chiều | PostgreSQL |
| 8 | Notification | Gửi mail / in-app / push | MongoDB |

## 2.3. Thành phần hạ tầng

Các thành phần sau là hạ tầng hỗ trợ, KHÔNG phải microservice nghiệp vụ và không sở hữu domain data:

- API Gateway (Kong, DB-less): điểm vào duy nhất, định tuyến, xác thực biên (JWT plugin), rate-limit, CORS. Chạy như một service hạ tầng riêng (image `kong` chính thức + `kong.yml` khai báo), không phải project .NET. Thay cho gateway YARP ở bản skeleton trước đó.
- RabbitMQ + MassTransit: message broker, outbox, saga, dead-letter queue.
- Redis: distributed cache, real-time (SignalR backplane), job status ephemeral.
- Observability (.NET Aspire + OpenTelemetry): traces, metrics, logs — lớp cắt ngang áp lên cả 8 service.

## 2.4. Nguyên tắc giao tiếp

- gRPC đồng bộ: chỉ dùng khi caller cần câu trả lời ngay để tiếp tục xử lý (ví dụ AI Generation lấy nội dung story từ Authoring). Dùng tiết kiệm.
- Event bất đồng bộ qua RabbitMQ: mặc định cho mọi tương tác 'đã xảy ra rồi, service khác tự phản ứng'. Đây là nơi eventual consistency vận hành.
- Read-model cục bộ (CQRS): mỗi service giữ bản sao nhẹ dữ liệu của service khác (vd membership\_snapshot, story\_snapshot), cập nhật từ event, để tránh gọi gRPC liên tục.

# 3. Quản lý danh tính và phân quyền (IAM)

IAM trong hệ thống được trải trên hai service tách biệt, không gộp làm một:

- Identity (Service 1) — Authentication: 'bạn là ai'. Quản lý user, mật khẩu, JWT, OAuth, 2FA.
- Workspace (Service 2) — Authorization: 'bạn được làm gì'. Quyền gắn với ngữ cảnh project (EDITOR/VIEWER), nên đặt cạnh dữ liệu membership.

Lý do tách: Identity thuần về danh tính, độc lập với nghiệp vụ. Authorization ở đây là context-based (quyền theo từng project), phụ thuộc dữ liệu membership của Workspace — nếu tách ra IAM riêng sẽ tạo coupling vòng.

## 3.1. Hỗ trợ từ ASP.NET Core

ASP.NET Core hỗ trợ phần lớn IAM ở mức dựng sẵn, giảm đáng kể code tự viết:

| **Tầng** | **Thành phần** | **Microsoft lo sẵn?** |
| --- | --- | --- |
| Quản lý user | ASP.NET Core Identity (UserManager, hash, 2FA) | Có |
| Phát hành token | Identity API endpoints (.NET 8) hoặc Duende IdentityServer | Có (có lựa chọn) |
| Verify token | JWT Bearer middleware | Có |
| Phân quyền theo project | Logic nghiệp vụ ở Workspace Service | Không — tự xây |

*Lưu ý: Duende IdentityServer đã chuyển sang thương mại. Với phạm vi đồ án, Identity API endpoints của .NET 8 là lựa chọn gọn và đủ vì hệ thống tự sở hữu cả frontend lẫn backend.*

# 4. Các luồng nghiệp vụ chính

Hệ thống có 32 luồng nghiệp vụ phủ 8 service. Tài liệu này trình bày chi tiết ba luồng xương sống; các luồng còn lại là biến thể đơn giản hơn.

## 4.1. Luồng generate test case (trái tim hệ thống)

Đây là luồng phức tạp nhất, hội tụ Saga, Outbox và event-driven. Sử dụng Saga điều phối tập trung (orchestration) đặt tại AI Generation Service.

### Các bước

1. Client gọi API generate qua Gateway. AI Generation tạo generation\_job (PENDING), trả 202 kèm jobId ngay. Client theo dõi tiến độ qua Redis Pub/Sub (SignalR).
2. AI Generation gọi gRPC sang Authoring lấy nội dung story + acceptance criteria (đồng bộ, vì cần ngay để build prompt).
3. Gọi LLM qua router: thử nguồn self-host (RunPod/vLLM) trước, nếu lỗi/timeout thì fallback sang third-party (OpenAI/Gemini/Claude). Áp dụng Strategy pattern + circuit breaker (Polly).
4. Ghi Outbox: trong cùng một transaction, cập nhật job = COMPLETED và ghi event TestCasesGenerated vào bảng outbox. Giải bài toán dual-write.
5. TestArtifact tiêu thụ event, lưu test case (generated\_by\_ai = true), bảo đảm idempotent qua processed\_messages, rồi phát tiếp TestCasesSaved.
6. Hai nhánh phản ứng song song: (5a) Authoring cập nhật trạng thái story; (5b) Integration đẩy ngược lên Jira nếu story có external\_link.

### Xử lý lỗi và bù trừ

- Lỗi lấy story: chưa có side-effect, chỉ đánh dấu job FAILED và báo client.
- Lỗi LLM: router fallback sang nguồn thứ hai; chỉ khi cả hai nguồn lỗi mới chuyển FAILED.
- Lỗi lưu TestArtifact: event trong outbox được redeliver (at-least-once), TestArtifact idempotent nên tự lành, không cần bù trừ thủ công.
- Nguyên tắc: thứ tự event đảm bảo không bao giờ có trạng thái nửa vời (story đánh dấu tested nhưng test case chưa lưu).

## 4.2. Luồng mời thành viên (Saga)

Saga đơn giản với trạng thái: Pending → AwaitingResponse → Accepted (hoặc Expired nếu quá 7 ngày). Khi timeout, saga chạy đường bù trừ: hủy invitation và gửi mail thông báo hết hạn. Token lưu dạng hash, không lưu thô.

## 4.3. Luồng import story từ Jira (inbound, ACL)

Jira gửi webhook → Integration Service verify chữ ký → dedup theo event\_id → ACL dịch payload Jira sang mô hình nội bộ sạch → upsert external\_link → phát event ExternalStoryImported → Authoring tạo story gắn external\_ref. Authoring không bao giờ biết cấu trúc Jira.

# 5. Chuẩn hóa thuật ngữ kiểm thử (ISTQB)

Phiên bản cũ liên tục gom nhiều trục khái niệm vào một field. Bảng dưới đối chiếu cũ và chuẩn đề xuất:

| **Field / khái niệm** | **Bản cũ** | **Chuẩn đề xuất** |
| --- | --- | --- |
| Test case type | Positive / Negative / Boundary (trộn 2 trục) | Tách: polarity (POSITIVE/NEGATIVE) + design\_technique (BVA, EP, Decision Table...) |
| Test case priority | Thiếu | CRITICAL / HIGH / MEDIUM / LOW |
| Test case lifecycle | Thiếu | DRAFT / REVIEW / APPROVED / DEPRECATED |
| Test result | PASSED / FAILED | Bổ sung BLOCKED / SKIPPED / NOT\_RUN / IN\_PROGRESS |
| Story status | DRAFT / READY / TESTED (trộn việc test) | authoring\_status riêng; 'đã test' là thông tin dẫn xuất |
| Test run status | Gộp vào test plan | PLANNED / IN\_PROGRESS / COMPLETED / ABORTED |
| Defect / Bug | Không có | Thực thể mới: severity + status, cầu nối đẩy Jira |

## 5.1. Cấu trúc thực thể đúng

- Test Plan: tài liệu chiến lược cấp project (1 văn bản: scope, mục tiêu, rủi ro).
- Test Suite: tập hợp test case gom theo chủ đề.
- Test Run (Execution): một lần thực thi một suite tại một thời điểm.
- Test Run Result: kết quả pass/fail/blocked của từng test case trong một run.

*Lỗi của bản cũ: test\_plan + test\_plan\_item thực chất hành xử như Test Run. Đã tách lại cho đúng.*

# 6. Thực thi test tự động (Execution Service)

Đây là năng lực mới so với bản cũ (vốn chỉ đánh dấu pass/fail thủ công). Execution Service điều khiển Chrome qua Playwright để chạy test theo từng step.

## 6.1. Ba mức độ tự động hóa

| **Mức** | **Mô tả** | **Đánh giá** |
| --- | --- | --- |
| 1 | QA tự viết script Playwright, service chỉ chạy | Dễ, chắc ăn — nền bắt buộc |
| 2 | LLM sinh script từ mô tả + thông tin UI (DOM/selector) | Vừa sức đồ án — điểm nhấn AI |
| 3 | Agent tự khám phá UI và thao tác | Rất khó, dễ vỡ — vượt phạm vi |

Khuyến nghị: làm Mức 1 chạy chắc chắn trước, rồi nâng lên Mức 2 nếu còn thời gian. Không nhắm Mức 3 cho đồ án.

## 6.2. Thiết kế

- Chạy bất đồng bộ hoàn toàn: client nhận runId ngay, theo dõi qua Redis/SignalR.
- Worker pool tách khỏi API: mỗi worker chạy browser trong container cô lập (sandbox), giới hạn CPU/RAM/timeout — bắt buộc về bảo mật.
- Đầu ra phong phú: screenshot từng step, video, trace, log — lưu object storage, DB chỉ giữ đường dẫn.
- Khi FAIL: phát event tạo defect, có thể đẩy ngược Jira — khép kín vòng đời QA.

# 7. Thiết kế Database từng service

Mỗi service một database riêng (polyglot). Hai mẫu hình lặp lại: mọi service phát event đều có bảng outbox; mọi service tiêu thụ event đều có processed\_messages (idempotency). Các \*\_snapshot là read-model nhận từ event service khác. Lưu ý: user\_id ở mọi nơi là UUID tham chiếu, KHÔNG phải khóa ngoại xuyên service.

## 7.1. Identity Service (PostgreSQL)

| **Bảng** | **Cột chính / ghi chú** |
| --- | --- |
| user | id (PK), email (UK), password\_hash (Argon2id), display\_name, status, created\_at |
| refresh\_token | id (PK), user\_id, token\_hash (lưu hash, không lưu thô), expires\_at, revoked |
| user\_mfa | id (PK), user\_id, secret, enabled |
| outbox\_message | id (PK), type, payload (jsonb), processed\_at |

## 7.2. Workspace Service (PostgreSQL)

| **Bảng** | **Cột chính / ghi chú** |
| --- | --- |
| workspace | id (PK), name, owner\_user\_id, type (PERSONAL/TEAM) |
| project | id (PK), workspace\_id (FK), name, project\_key (UK) |
| workspace\_member | id (PK), workspace\_id (FK), user\_id, role (OWNER/ADMIN/MEMBER) |
| project\_member | id (PK), project\_id (FK), user\_id, role (EDITOR/VIEWER) |
| invitation | id (PK), project\_id (FK), email, token\_hash, status, expires\_at |
| outbox\_message | id (PK), type, payload |

## 7.3. Authoring Service (PostgreSQL)

| **Bảng** | **Cột chính / ghi chú** |
| --- | --- |
| user\_story | id (PK), project\_id, title, as\_a/i\_want\_to/so\_that, description, authoring\_status, external\_ref |
| acceptance\_criteria | id (PK), user\_story\_id (FK), parent\_id (FK, phân cấp), content, order\_no, completed |
| business\_rule | id (PK), user\_story\_id (FK), content |
| membership\_snapshot | read-model: project\_id, user\_id, role (từ event Workspace) |
| outbox + processed\_messages | hạ tầng event |

## 7.4. TestArtifact Service (PostgreSQL)

| **Bảng** | **Cột chính / ghi chú** |
| --- | --- |
| test\_case | id (PK), story\_id, ac\_id, title, preconditions, steps, expected\_result, polarity, design\_technique, priority, lifecycle\_status, generated\_by\_ai |
| test\_suite / test\_suite\_item | gom test case theo chủ đề (N:M) |
| test\_plan | id (PK), project\_id, name, description — chiến lược cấp project |
| test\_run | id (PK), suite\_id (FK), status, started\_at, finished\_at |
| test\_run\_result | id (PK), run\_id (FK), test\_case\_id, result (PASS/FAIL/BLOCKED...) |
| defect | id (PK), run\_result\_id (FK), severity, status — cầu nối đẩy Jira |
| story\_snapshot | read-model từ Authoring |
| outbox + processed\_messages | hạ tầng event |

## 7.5. AI Generation Service (PostgreSQL + Redis)

| **Bảng** | **Cột chính / ghi chú** |
| --- | --- |
| generation\_job | id (PK), story\_id, job\_type (refine/ac/tc), status, llm\_source, created\_at |
| llm\_provider\_config | id (PK), project\_id, preferred\_source, model, temperature, max\_tokens |
| Redis (state nóng) | job:{id} tiến độ real-time, Pub/Sub xuống SignalR |
| outbox + saga\_state | hạ tầng event + Saga (MassTransit) |

## 7.6. Execution Service (PostgreSQL + Object storage)

| **Bảng** | **Cột chính / ghi chú** |
| --- | --- |
| exec\_run | id (PK), suite\_id, environment, status, triggered\_by, started\_at, finished\_at |
| exec\_result | id (PK), run\_id (FK), test\_case\_id, status, duration, error\_message |
| exec\_artifact | id (PK), result\_id (FK), type (screenshot/video/trace), storage\_path |
| test\_script | id (PK), test\_case\_id, framework (Playwright), script\_content, generated\_by\_ai |
| outbox + processed\_messages | hạ tầng event |

## 7.7. Integration (Jira) Service (PostgreSQL)

| **Bảng** | **Cột chính / ghi chú** |
| --- | --- |
| jira\_connection | id (PK), project\_id, oauth\_token, refresh\_token, jira\_site\_id, status |
| external\_link | id (PK), provider, internal\_type, internal\_id, external\_id, external\_key, last\_synced\_at, sync\_status |
| outbound\_sync\_job | id (PK), external\_link\_id (FK), payload, status, retry\_count |
| processed\_webhook | event\_id (PK, dedup), received\_at |
| outbox + saga\_state | hạ tầng event + outbound saga |

## 7.8. Notification Service (MongoDB)

| **Bảng** | **Cột chính / ghi chú** |
| --- | --- |
| notifications | \_id, user\_id, type, payload (object linh hoạt), channel, status, read, created\_at |
| notification\_preferences | \_id, user\_id, type, email\_enabled, push\_enabled |
| processed\_messages | message\_id (idempotency), processed\_at |

# 8. Kiến trúc bên trong mỗi service

Đây là cách tổ chức code bên trong một microservice (khác với kiến trúc hệ thống microservice ở phần 2). Công thức áp dụng: Vertical Slice + CQRS/MediatR trên nền nguyên tắc phụ thuộc của Clean Architecture. Ba thứ này không loại trừ nhau mà chồng lớp với nhau.

## 8.1. Ba lớp khái niệm

- Clean Architecture — quy định CHIỀU PHỤ THUỘC: Infrastructure phụ thuộc Application, Application phụ thuộc Domain. Mũi tên luôn hướng vào trong, không bao giờ ngược lại. Domain (entity, business rule) ở trong cùng, không phụ thuộc framework.
- CQRS qua MediatR — quy định CÁCH REQUEST CHẢY QUA: mỗi thao tác là một Command (ghi) hoặc Query (đọc) riêng, mỗi cái một Handler. Pipeline behavior lo validation, logging, transaction tập trung.
- Vertical Slice — quy định CÁCH BÀY THƯ MỤC: gom mọi thứ của một feature (command + handler + validator + endpoint) vào cùng một folder, thay vì xé theo tầng kỹ thuật. Sửa một feature chỉ mở một folder.

*Câu để nhớ: phụ thuộc hướng vào trong, code gom theo feature, request đi qua mediator.*

## 8.2. Cấu trúc thư mục chuẩn cho một service

Mỗi service là một solution gồm các project sau (lấy TestArtifact làm ví dụ):

TestArtifact.Api/ # Project khởi chạy (host)  
 Program.cs # DI, middleware, MassTransit, đăng ký endpoint  
 Features/ # ⭐ Vertical slices - gom theo feature  
 TestCases/  
 CreateTestCase/  
 CreateTestCaseCommand.cs # Command (input)  
 CreateTestCaseHandler.cs # Handler (xử lý)  
 CreateTestCaseValidator.cs # FluentValidation  
 CreateTestCaseEndpoint.cs # Minimal API endpoint  
 GetTestCases/  
 GetTestCasesQuery.cs  
 GetTestCasesHandler.cs  
 GetTestCasesEndpoint.cs  
 UpdateTestCase/ DeleteTestCase/ ...  
 Extensions/ # DI extension, mapping  

TestArtifact.Domain/ # Tầng trong cùng - thuần C#  
 Entities/ TestCase.cs # Entity + business rule  
 Enums/ Polarity.cs ...  
 Events/ TestCasesSaved.cs # Domain/integration event  

TestArtifact.Infrastructure/ # Tầng ngoài cùng  
 Persistence/  
 AppDbContext.cs # EF Core DbContext  
 Configurations/ # Entity type config  
 Migrations/  
 Repositories/ # (tuỳ chọn) repository  
 Messaging/ OutboxPublisher.cs # MassTransit consumer/producer  

TestArtifact.Contracts/ # DTO + event chia sẻ (nếu cần)

## 8.3. Luồng một request đi qua các tầng

1. HTTP request vào Endpoint (Api) — chỉ nhận input, không chứa business logic.
2. Endpoint gửi Command/Query qua MediatR (ISender.Send).
3. Pipeline behavior chạy trước handler: ValidationBehavior (FluentValidation), LoggingBehavior, TransactionBehavior.
4. Handler thực thi use case: gọi Domain entity, dùng DbContext/repository (Infrastructure) qua interface.
5. Nếu có thay đổi cần thông báo: ghi event vào Outbox trong cùng transaction.
6. Handler trả về Result; Endpoint map sang HTTP response (200/201/404...).

# 9. Template CRUD mẫu (cho team)

Đây là bộ khung CRUD đầy đủ cho một feature, dùng TestCase làm ví dụ. Team copy nguyên mẫu này cho mọi entity mới, chỉ đổi tên. Mọi service đều theo cùng một khuôn để code nhất quán.

## 9.1. Domain — Entity

// TestArtifact.Domain/Entities/TestCase.cs  
namespace TestArtifact.Domain.Entities;  

public class TestCase  
{  
 public Guid Id { get; private set; }  
 public Guid StoryId { get; private set; }  
 public string Title { get; private set; } = default!;  
 public string? Steps { get; private set; }  
 public string? ExpectedResult { get; private set; }  
 public Polarity Polarity { get; private set; }  
 public bool GeneratedByAi { get; private set; }  
 public DateTime CreatedAt { get; private set; }  

 private TestCase() { } // EF Core  

 public static TestCase Create(Guid storyId, string title,  
 string? steps, string? expected, Polarity polarity, bool byAi)  
 {  
 return new TestCase {  
 Id = Guid.NewGuid(),  
 StoryId = storyId, Title = title,  
 Steps = steps, ExpectedResult = expected,  
 Polarity = polarity, GeneratedByAi = byAi,  
 CreatedAt = DateTime.UtcNow  
 };  
 }  

 public void Update(string title, string? steps, string? expected)  
 {  
 Title = title; Steps = steps; ExpectedResult = expected;  
 }  
}

## 9.2. Create — Command + Handler + Validator + Endpoint

CREATE (ghi). Một slice gồm 4 file:

// CreateTestCaseCommand.cs  
public record CreateTestCaseCommand(  
 Guid StoryId, string Title, string? Steps,  
 string? ExpectedResult, Polarity Polarity) : IRequest<Guid>;  

// CreateTestCaseValidator.cs  
public class CreateTestCaseValidator  
 : AbstractValidator<CreateTestCaseCommand>  
{  
 public CreateTestCaseValidator() {  
 RuleFor(x => x.Title).NotEmpty().MaximumLength(500);  
 RuleFor(x => x.StoryId).NotEmpty();  
 }  
}  

// CreateTestCaseHandler.cs  
public class CreateTestCaseHandler  
 : IRequestHandler<CreateTestCaseCommand, Guid>  
{  
 private readonly AppDbContext \_db;  
 public CreateTestCaseHandler(AppDbContext db) => \_db = db;  

 public async Task<Guid> Handle(  
 CreateTestCaseCommand c, CancellationToken ct)  
 {  
 var entity = TestCase.Create(  
 c.StoryId, c.Title, c.Steps,  
 c.ExpectedResult, c.Polarity, byAi: false);  
 \_db.TestCases.Add(entity);  
 await \_db.SaveChangesAsync(ct);  
 return entity.Id;  
 }  
}  

// CreateTestCaseEndpoint.cs  
app.MapPost("/api/v1/test-cases", async (  
 CreateTestCaseCommand cmd, ISender sender) =>  
{  
 var id = await sender.Send(cmd);  
 return Results.Created($"/api/v1/test-cases/{id}", id);  
})  
.RequireAuthorization()  
.WithTags("TestCases");

## 9.3. Read — Query + Handler (đọc, có phân trang)

// GetTestCasesQuery.cs  
public record GetTestCasesQuery(Guid StoryId, int Page, int Size)  
 : IRequest<PagedResult<TestCaseDto>>;  

public record TestCaseDto(Guid Id, string Title,  
 string? Steps, bool GeneratedByAi);  

// GetTestCasesHandler.cs  
public class GetTestCasesHandler  
 : IRequestHandler<GetTestCasesQuery, PagedResult<TestCaseDto>>  
{  
 private readonly AppDbContext \_db;  
 public GetTestCasesHandler(AppDbContext db) => \_db = db;  

 public async Task<PagedResult<TestCaseDto>> Handle(  
 GetTestCasesQuery q, CancellationToken ct)  
 {  
 var query = \_db.TestCases  
 .AsNoTracking() // đọc thì không track  
 .Where(x => x.StoryId == q.StoryId);  

 var total = await query.CountAsync(ct);  
 var items = await query  
 .OrderByDescending(x => x.CreatedAt)  
 .Skip((q.Page - 1) \* q.Size).Take(q.Size)  
 .Select(x => new TestCaseDto(  
 x.Id, x.Title, x.Steps, x.GeneratedByAi))  
 .ToListAsync(ct);  

 return new PagedResult<TestCaseDto>(items, total, q.Page, q.Size);  
 }  
}  

// Endpoint  
app.MapGet("/api/v1/test-cases", async (  
 Guid storyId, int page, int size, ISender sender) =>  
 Results.Ok(await sender.Send(  
 new GetTestCasesQuery(storyId, page, size))));

## 9.4. Update & Delete

// UpdateTestCaseCommand.cs  
public record UpdateTestCaseCommand(Guid Id, string Title,  
 string? Steps, string? ExpectedResult) : IRequest;  

// UpdateTestCaseHandler.cs  
public async Task Handle(UpdateTestCaseCommand c, CancellationToken ct)  
{  
 var e = await \_db.TestCases.FindAsync([c.Id], ct)  
 ?? throw new NotFoundException(nameof(TestCase), c.Id);  
 e.Update(c.Title, c.Steps, c.ExpectedResult);  
 await \_db.SaveChangesAsync(ct);  
}  

// DeleteTestCaseHandler.cs  
public async Task Handle(DeleteTestCaseCommand c, CancellationToken ct)  
{  
 var e = await \_db.TestCases.FindAsync([c.Id], ct)  
 ?? throw new NotFoundException(nameof(TestCase), c.Id);  
 \_db.TestCases.Remove(e);  
 await \_db.SaveChangesAsync(ct);  
}

## 9.5. Pipeline behavior (dùng chung mọi handler)

Validation và transaction được xử lý tập trung qua MediatR pipeline, không lặp lại trong từng handler:

// ValidationBehavior.cs - chạy trước mọi handler  
public class ValidationBehavior<TReq, TRes>  
 : IPipelineBehavior<TReq, TRes> where TReq : notnull  
{  
 private readonly IEnumerable<IValidator<TReq>> \_validators;  
 public ValidationBehavior(IEnumerable<IValidator<TReq>> v)  
 => \_validators = v;  

 public async Task<TRes> Handle(TReq req,  
 RequestHandlerDelegate<TRes> next, CancellationToken ct)  
 {  
 foreach (var v in \_validators) {  
 var r = await v.ValidateAsync(req, ct);  
 if (!r.IsValid) throw new ValidationException(r.Errors);  
 }  
 return await next();  
 }  
}  

// Program.cs - đăng ký  
builder.Services.AddMediatR(cfg =>  
 cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));  
builder.Services.AddValidatorsFromAssembly(typeof(Program).Assembly);  
builder.Services.AddTransient(typeof(IPipelineBehavior<,>),  
 typeof(ValidationBehavior<,>));

## 9.6. Quy ước team bắt buộc

- Mỗi feature = một folder con trong Features/, chứa đủ command/query + handler + validator + endpoint.
- Command/Query là record, kế thừa IRequest. Không bao giờ đặt business logic trong Endpoint.
- Query luôn dùng AsNoTracking() và Select sang DTO — không trả Entity ra ngoài.
- Mọi validation qua FluentValidation + ValidationBehavior, không validate thủ công trong handler.
- Handler nhận CancellationToken và truyền xuống mọi lời gọi async.
- Lỗi không tìm thấy ném NotFoundException; một middleware chung map exception sang HTTP status.
- Thao tác ghi cần thông báo service khác: ghi Outbox trong cùng transaction, không gọi gRPC/HTTP trực tiếp.

# 10. DevOps, hạ tầng và triển khai

Với hệ 8 microservice, DevOps không phải phần phụ — nó quyết định hệ có vận hành được không. Chiến lược: ba môi trường với ba công cụ, mỗi cái một vai trò không chồng chéo.

## 10.1. Ba môi trường

| **Môi trường** | **Công cụ** | **Vai trò** |
| --- | --- | --- |
| Dev (máy lập trình viên) | .NET Aspire AppHost (`aspire/QuraEx.AppHost`) | 1 lệnh chạy service + Postgres + RabbitMQ + Redis, kèm dashboard observability; hot reload, debug F5 |
| Chạy chung / demo / CI parity | Docker Compose (full stack) | Toàn bộ stack trong container, đồng nhất tuyệt đối trên mọi máy — không cần .NET SDK để chạy |
| Production | Kubernetes (Azure AKS) | Tự scale, health check, rolling update, managed services |

*Điểm cần làm rõ: Aspire KHÔNG thay thế Kubernetes. Aspire lo dev-time (chạy nhanh, debug, dashboard); k8s lo production. Aspire thậm chí sinh được manifest deploy — hai công cụ nối tiếp nhau, không cạnh tranh.*

**Hai chế độ chạy local (đã hiện thực):**

| Chế độ | Lệnh | Khi nào dùng |
| --- | --- | --- |
| Aspire (service như project) | `dotnet run --project aspire/QuraEx.AppHost/QuraEx.AppHost.csproj` | Phát triển hằng ngày — DX tốt nhất |
| Docker full stack | `make up` | Cần môi trường giống hệt nhau cho cả nhóm, demo, hoặc gỡ lỗi "works on my machine" |
| Chỉ hạ tầng (chạy service từ IDE) | `make up-infra` | Muốn debug service trong IDE nhưng dùng DB/broker trong container |

Compose tách 2 file theo trách nhiệm: `docker-compose.yml` (chỉ backing infra — Postgres/RabbitMQ/Redis) + `docker-compose.app.yml` (overlay thêm `gateway` Kong (image `kong`, mount `kong.yml`) + `authoring`). `make up` gộp cả hai. Stack chạy ở môi trường `Development` để khớp Aspire: migration tự apply, dùng JWT public key dev đã commit. Đây là local/CI parity — production dùng secret thật + migration job riêng.

## 10.2. Đóng gói (containerization)

- Mỗi service có một Dockerfile riêng, build thành một image độc lập. Dùng multi-stage build để image production nhỏ gọn (chỉ chứa runtime, không chứa SDK).
- CI chỉ build lại service nào thay đổi (path filter), không build cả 8 mỗi lần — tiết kiệm thời gian khi monorepo lớn.

*Lưu ý gateway: API Gateway nay là Kong (DB-less) — chạy từ image `kong` chính thức, cấu hình bằng `kong.yml` khai báo (routes/services + plugin JWT, rate-limiting, CORS), KHÔNG build từ Dockerfile .NET. Các đặc điểm Dockerfile dưới đây áp dụng cho 8 service nghiệp vụ; lấy Authoring làm ví dụ.*

**Đặc điểm Dockerfile đã hiện thực** (`services/authoring/QuraEx.Authoring.Api/Dockerfile`):

- **Build context = repo root**: service dùng ProjectReference chéo thư mục (`aspire/`, `building-blocks/`) + central config (`global.json`, `Directory.*.props`, `nuget.config`) ở root nên context phải là root, không phải thư mục service.
- **Phải COPY `.editorconfig`**: build bật warnings-as-errors qua StyleCop/Sonar — thiếu `.editorconfig` thì analyzer chạy mặc định strict và publish fail.
- Restore chỉ chạm nuget.org (QuraEx.* là ProjectReference, không phải package) → **không cần GitHub Packages token** trong Docker.
- Runtime: chạy non-root (`USER $APP_UID`), `HEALTHCHECK` qua `curl /health`, Kestrel nghe cổng 8080.
- `.dockerignore` ở root loại `bin/obj/.git/...` để context gọn và không dính artifact build cũ.

Dockerfile mẫu (multi-stage, .NET 10, context = repo root):

# build stage  
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build  
WORKDIR /src  
COPY global.json nuget.config Directory.Build.props Directory.Packages.props .editorconfig ./  
COPY services/authoring/QuraEx.Authoring.Api/QuraEx.Authoring.Api.csproj services/authoring/QuraEx.Authoring.Api/  
COPY aspire/QuraEx.ServiceDefaults/QuraEx.ServiceDefaults.csproj aspire/QuraEx.ServiceDefaults/  
RUN dotnet restore services/authoring/QuraEx.Authoring.Api/QuraEx.Authoring.Api.csproj  
COPY services/authoring/ services/authoring/  
COPY aspire/QuraEx.ServiceDefaults/ aspire/QuraEx.ServiceDefaults/  
RUN dotnet publish services/authoring/QuraEx.Authoring.Api/QuraEx.Authoring.Api.csproj \  
 -c Release -o /app/publish --no-restore /p:UseAppHost=false  

# runtime stage (nhỏ gọn, non-root)  
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime  
WORKDIR /app  
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*  
COPY --from=build /app/publish ./  
ENV ASPNETCORE_HTTP_PORTS=8080  
EXPOSE 8080  
USER $APP_UID  
HEALTHCHECK CMD curl -fsS http://localhost:8080/health || exit 1  
ENTRYPOINT ["dotnet", "QuraEx.Authoring.Api.dll"]

## 10.3. CI/CD — GitHub Actions

Pipeline (`.github/workflows/ci.yml`) chạy khi push lên `main` hoặc mở PR vào `main`. Integration test dùng Testcontainers (spin Postgres thật trong container lúc test rồi xoá), không mock.

**Các job hiện hành** (đã hiện thực, không chỉ là mẫu):

| Job | Vai trò |
| --- | --- |
| `changes` (Detect changes) | Path filter (dorny/paths-filter) — quyết định service nào thay đổi để chỉ build phần đó |
| `build-test` (matrix) | Build (warnings-as-errors) + integration test (Testcontainers) + EF migration drift check `-c Release` cho mỗi service đổi; chỉ chạy khi `any-cs == true` |
| `security` (Security scan) | gitleaks (secret scan) + CodeQL (csharp) + `dotnet list package --vulnerable` |
| `pr-title` (Lint PR title) | Kiểm tra tiêu đề PR theo Conventional Commits (chỉ trên `pull_request`) |
| `ci-gate` (**CI Gate**) | Job tổng hợp — xem mục bên dưới |
| `pack-push` | Đóng gói + đẩy BuildingBlocks lên GitHub Packages (chỉ khi push `main` và BuildingBlocks đổi) |

**CI Gate — required status check duy nhất.** Vấn đề: với path filter, PR chỉ đổi docs/infra (không có `.cs`) sẽ làm `build-test`/`security` **skip** → các context đó không bao giờ report → branch protection treo PR ở trạng thái `BLOCKED` vĩnh viễn. Lời giải: thêm một job `CI Gate` (`needs: [changes, build-test, security]`, `if: always()`) — pass khi mọi job gating đều **success hoặc skipped**, chỉ fail khi có job thật sự failure/cancel. Đặt `CI Gate` làm required check duy nhất thay cho 4 context rời → PR docs/infra merge sạch, mà lỗi build/security thật vẫn chặn.

**Branch protection `main` (đã cấu hình):**

- Required status check: chỉ `CI Gate` (strict — branch phải up-to-date trước merge).
- `required_approving_review_count: 0` — repo cá nhân, không cần approval (tác giả không thể tự approve PR của mình trên GitHub).
- `enforce_admins: false`, nhưng vẫn cấm force-push và xoá nhánh `main`.

**Bảo vệ secret 2 lớp:**

- Local: Husky `.husky/pre-commit` chạy `gitleaks git --staged` — chặn secret trước khi commit (cộng `dotnet format` check cho file `.cs` staged).
- CI: job `security` chạy gitleaks trên toàn diff PR. Allowlist placeholder ở `.gitleaks.toml`.

Phần đóng gói image + deploy lên registry/k8s (mục 10.4) là mục tiêu production — pipeline hiện tại dừng ở build/test/security/pack; chưa wire bước docker-push + azure/k8s-deploy.

## 10.4. Triển khai cloud

- Cloud chính: Azure (tích hợp .NET sâu nhất — AKS + Azure Container Registry + Azure Database for PostgreSQL liền mạch; Aspire deploy thẳng qua azd). AWS làm được nhưng tốn wiring hơn.
- Domain quraex.com chỉ trỏ vào MỘT điểm: API Gateway (và frontend). Các service nội bộ KHÔNG lộ ra internet — chỉ Gateway có public endpoint.
- TLS/HTTPS kết thúc ở ingress của Kubernetes (hoặc ở Gateway). Đây là ranh giới bảo mật quan trọng.
- Database, Redis, RabbitMQ nên dùng managed service của cloud (Azure Database for PostgreSQL, Azure Cache for Redis...) thay vì tự host trong cluster — giảm gánh vận hành.
- Quản lý cấu hình & secret: dùng Kubernetes Secret + Azure Key Vault, không hard-code trong image hay repo.

## 10.5. Lưu ý phạm vi (quan trọng cho đồ án)

Kubernetes production thật là phần tốn công nhất của cả dự án, nhiều hơn cả việc code service. Khuyến nghị thực tế cho phạm vi đồ án:

- Làm chắc: .NET Aspire + Docker Compose + GitHub Actions CI — demo được ngay và đủ ấn tượng để thể hiện năng lực DevOps.
- Cân nhắc: Kubernetes + deploy cloud thật cho 1–2 service tiêu biểu để chứng minh 'biết làm', thay vì deploy trọn 8 service lên k8s production (chi phí cloud và thời gian wiring rất lớn).
- Thiết kế đầy đủ trên giấy (manifest, kiến trúc cluster) cho cả 8 service để trình bày, kể cả khi chỉ deploy thật một phần.

# 11. Các quyết định kiến trúc đã chốt

| **Quyết định** | **Lựa chọn** | **Lý do** |
| --- | --- | --- |
| Kiểu kiến trúc | Microservice (8 service) | Yêu cầu môn học + mục tiêu trưng bày năng lực |
| Database | Polyglot, mỗi service 1 DB, không đọc chéo | Đúng nguyên tắc database-per-service |
| Giao tiếp | gRPC (sync) + RabbitMQ (async) | Mỗi cái một vai trò rõ ràng |
| Saga | Orchestration cho flow phức tạp, choreography cho flow đơn giản | Cân bằng giữa kiểm soát và độ nhẹ |
| LLM | Kết hợp self-host + third-party (router fallback) | Độ sẵn sàng cao, linh hoạt theo project |
| Jira | Service riêng + Anti-Corruption Layer | Cô lập hệ ngoài, dễ mở rộng provider khác |
| Pattern trưng bày | Saga, Outbox, CQRS, Idempotency, ACL | Thể hiện năng lực hệ phân tán |

## 11.1. Lưu ý về phạm vi

8 service cùng execution tự động Mức 2, defect tracking và Jira hai chiều là khối lượng lớn cho một đồ án. Về kiến trúc thì hoàn chỉnh, nhưng về code thì cần phân định rõ: phần BẮT BUỘC code-để-demo, và phần thiết kế-đầy-đủ-trên-giấy để trình bày. Notification và Integration là hai service có thể làm sau cùng (hoặc bản tối giản) vì chúng chỉ subscribe event, không nằm trên đường đi chính của luồng tạo test case.