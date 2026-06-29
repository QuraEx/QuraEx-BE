# Văn (Lead) — AI Generation + Execution

> Đầu việc xếp **theo thứ tự ưu tiên — làm từ trên xuống**. 🔴 làm trước/gấp · 🟠 tiếp theo · 🟡 sau cùng. Chi tiết kỹ thuật ở [`../BRD.md`](../BRD.md).

## Bạn làm gì?
| Service | Nhiệm vụ | Flow chủ trì | Trạng thái |
|---|---|---|---|
| **AI Generation** | Dùng LLM sinh test case | D | Chưa có gì |
| **Execution** | Chạy test run (Playwright) + lưu kết quả | E | Chưa có gì |
| (Lead) | Gateway, Aspire, CI, review PR | — | Đang chạy |

---

## 🔴 Ưu tiên 1 — Việc lead làm ngay (cả team chờ)
- [ ] Đồng bộ branch `dev` ← `main`, báo team khi xong (team chưa base feature được tới khi xong).

## 🔴 Ưu tiên 2 — Dựng khung AI Generation
- [ ] `./scripts/new-service.sh AiGeneration` → sinh service.
- [ ] Nối AppHost + gateway + CI (script in sẵn đoạn cần dán).
- [ ] Tạo bảng "job sinh test case" trong DB (`quraex.dbml` group 4).
- [ ] Chạy thử service lên `/health` qua gateway.

## 🔴 Ưu tiên 3 — Dựng khung Execution
- [ ] `./scripts/new-service.sh Execution` → nối AppHost/gateway/CI.
- [ ] Thêm **MinIO** vào AppHost (mẫu trong `docs/TASKS.md`).
- [ ] Tạo bảng `test_run` + kết quả per-case + `artifact` (`quraex.dbml` group 6). DB chỉ lưu đường dẫn, file ở MinIO.
- [ ] Chạy thử `/health` + MinIO lên.

## 🟠 Ưu tiên 4 — AI Generation chạy thật
- [ ] Chốt `.proto` với **Giang** (Authoring) → gọi gRPC lấy nội dung story + AC.
- [ ] Gọi LLM nhà cung cấp chính, lỗi thì tự chuyển sang phụ (fallback / circuit breaker).
- [ ] Chạy bất đồng bộ: API trả `202 + jobId` ngay; báo tiến độ real-time (Redis).
- [ ] Sinh xong → **phát event `TestCasesGenerated`** (Giang/TestArtifact nhận). Ghi lại provider + model đã dùng (FR-19).

## 🟠 Ưu tiên 5 — Execution chạy thật
- [ ] **Nhận event `TestRunRequested`** (từ TestArtifact) → chạy test.
- [ ] Chạy **Playwright trong sandbox cô lập, giới hạn tài nguyên**; timeout → kết quả `blocked`.
- [ ] Lưu ảnh/video/trace lên **MinIO**, ghi kết quả per-case.
- [ ] **Phát event `TestRunCompleted`**. FAIL → tạo defect (sau này push Jira qua Minh).

## 🟡 Ưu tiên 6 — Hạ tầng (Pha sau)
- [ ] Khi Minh có Cognito: chỉnh **gateway** validate JWT Cognito theo issuer Minh cung cấp.
- [ ] Hoàn thiện test (Testcontainers) + review PR cả team theo CODEOWNERS.

---

## ✅ Mốc "dựng xong" (Ưu tiên 1–3)
2 service build + chạy `/health` qua gateway; MinIO lên; các bảng đã tạo.

📎 Chi tiết: [BRD](../BRD.md) · mẫu code: `services/authoring/`
🗂️ **Xem `quraex.dbml`:** mở https://dbdiagram.io → New diagram → dán nội dung `docs/database/quraex.dbml` (tìm group của bạn). HD: [BRD §6](../BRD.md).
