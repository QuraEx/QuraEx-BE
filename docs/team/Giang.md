# Giang — Authoring + TestArtifact

> Đầu việc xếp **theo thứ tự ưu tiên — làm từ trên xuống**. 🔴 làm trước/gấp · 🟠 tiếp theo · 🟡 sau cùng. Chi tiết kỹ thuật ở [`../BRD.md`](../BRD.md).

## Bạn làm gì?
| Service | Nhiệm vụ | Flow chủ trì | Trạng thái |
|---|---|---|---|
| **Authoring** | Quản lý user story + acceptance criteria | C | CRUD story **đã xong** (mẫu gold) |
| **TestArtifact** | Quản lý test case, suite, test run | E | Chưa có gì |

> Authoring là service **mẫu (gold)** — cả team copy theo. Bạn vừa maintain vừa học pattern từ đây để dựng TestArtifact.

---

## 🔴 Ưu tiên 1 — Đọc mẫu + bổ sung Authoring
- [ ] Đọc kỹ code `services/authoring/` để nắm pattern (đây là mẫu cho TestArtifact).
- [ ] Thêm API **Acceptance Criteria** (thêm/sửa/xóa) — bảng đã có, chỉ thiếu API.
- [ ] Thêm API **Business Rule** — copy y cách `UserStory`.

## 🔴 Ưu tiên 2 — Dựng khung TestArtifact
- [ ] `./scripts/new-service.sh TestArtifact` → nối AppHost/gateway/CI.
- [ ] Tạo bảng `test_case`, `test_suite`, `test_run` (`quraex.dbml` group 5).
- [ ] API CRUD **test case** (thêm/sửa/xóa/xem).
- [ ] Chạy thử service lên `/health` qua gateway.

## 🟠 Ưu tiên 3 — Chốt contract event (người khác chờ)
- [ ] Chốt tên + các trường của **`TestRunRequested`** (Văn/Execution chờ) và **`TestCasesSaved`**.
- [ ] Chốt với **Văn** các trường của **`TestCasesGenerated`** (bạn nhận để lưu test case).
- [ ] Báo Văn ngay khi xong (chưa cần code chạy, chỉ cần shape).

## 🟠 Ưu tiên 4 — TestArtifact nối event
- [ ] **Nhận `TestCasesGenerated`** (từ Văn) → lưu test case dạng **draft** (idempotent).
- [ ] **Nhận `UserStoryCreated`** (từ Authoring) → tạo sẵn bộ test case rỗng.
- [ ] **Phát `TestRunRequested`** khi user bấm chạy test → Văn/Execution nhận.
- [ ] **Nhận `TestRunCompleted`** (từ Văn) → cập nhật kết quả per-case.
- [ ] **Nhận `MembershipChanged`** (từ Bảo) → cập nhật `membership_snapshot` (check quyền).

## 🟠 Ưu tiên 5 — Authoring nâng cao
- [ ] API đổi trạng thái story (DRAFT → READY → APPROVED).
- [ ] Phát đủ 3 event story: Created / Updated / Deleted (hiện mới có Created).
- [ ] Mở **gRPC server** trả story + AC cho AI Generation — chốt `.proto` với **Văn**.

## 🟡 Ưu tiên 6 — Hoàn thiện
- [ ] TestArtifact: phân loại test case theo **ISTQB** (FR-21), tổ chức **suite** (FR-22), **test plan** (FR-23).
- [ ] Authoring: AI refine câu chữ story (FR-14, phối hợp Văn).

---

## ✅ Mốc "dựng xong" (Ưu tiên 1–2)
API Acceptance Criteria của Authoring chạy; service TestArtifact build + CRUD test case chạy.

📎 Chi tiết: [BRD](../BRD.md) · mẫu code: `services/authoring/`
🗂️ **Xem `quraex.dbml`:** mở https://dbdiagram.io → New diagram → dán nội dung `docs/database/quraex.dbml` (tìm group của bạn). HD: [BRD §6](../BRD.md).
