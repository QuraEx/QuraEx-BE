# Luồng xương sống — Generate Test Case (sequence diagram)

> Render: dán vào [mermaid.live](https://mermaid.live), hoặc xem trực tiếp trên GitHub (hỗ trợ Mermaid).
> Đây là luồng phức tạp nhất, hội tụ Saga · Outbox · gRPC · CQRS · Idempotency · SignalR.

```mermaid
sequenceDiagram
    autonumber
    actor User
    participant GW as Kong Gateway
    participant AU as Authoring
    participant AI as AI Generation
    participant LLM as LLM Router<br/>(OpenAI/Gemini)
    participant TA as TestArtifact
    participant IN as Integration (Jira)
    participant EX as Execution
    participant NO as Notification
    participant RT as Redis / SignalR

    User->>GW: POST /generate (storyId)
    GW->>AI: route (JWT đã verify ở biên)
    AI->>AI: tạo generation_job = PENDING
    AI-->>User: 202 Accepted + jobId
    AI->>RT: publish tiến độ job:{id}
    RT-->>User: realtime progress (SignalR)

    Note over AI,AU: gRPC sync — cần story ngay để build prompt
    AI->>AU: gRPC GetStory(storyId)
    AU-->>AI: story + acceptance criteria

    AI->>LLM: generate(prompt)
    alt nguồn chính lỗi/timeout
        LLM-->>AI: fallback sang nguồn thứ hai (circuit breaker)
    end
    LLM-->>AI: test cases (raw)

    Note over AI: 1 transaction — giải dual-write
    AI->>AI: job = COMPLETED + ghi Outbox(TestCasesGenerated)
    AI-)TA: event TestCasesGenerated (RabbitMQ)

    TA->>TA: dedup processed_messages (idempotent)
    TA->>TA: lưu test_case (generated_by_ai = true)
    TA-)AU: event TestCasesSaved
    TA-)IN: event TestCasesSaved

    par nhánh song song
        AU->>AU: cập nhật authoring_status của story
    and
        IN->>IN: nếu story có external_link → đẩy ngược Jira
    end

    Note over User,EX: Sau đó — chạy thực thi tự động
    User->>GW: POST /test-runs (suiteId)
    GW->>TA: route
    TA-)EX: event TestRunRequested
    EX->>EX: chạy Playwright (container cô lập)
    EX->>EX: lưu artifact (screenshot/video) → MinIO/S3
    EX-)NO: event TestRunCompleted
    NO-->>User: thông báo in-app / email
```

## Pattern thể hiện trong luồng

| Bước | Pattern | Vì sao |
|---|---|---|
| 4-5 | **202 + Redis/SignalR** | Không bắt user chờ LLM; theo dõi async. |
| 7-8 | **gRPC (sync)** | Caller cần story ngay để build prompt — dùng tiết kiệm. |
| 9-10 | **Strategy + Circuit breaker** | Router LLM fallback nguồn thứ hai khi lỗi. |
| 12-13 | **Transactional Outbox** | Giải dual-write: cập nhật DB + phát event atomically. |
| 14-16 | **Idempotency** | `processed_messages` chống xử lý trùng (at-least-once delivery). |
| 18-20 | **Choreography + read-model** | Service tự phản ứng event, không gọi chéo DB. |
| 24-28 | **Async execution + object storage** | Worker cô lập, DB chỉ giữ đường dẫn artifact. |
