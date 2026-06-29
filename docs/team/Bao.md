# Bảo — Workspace + Notification

> Đầu việc xếp **theo thứ tự ưu tiên — làm từ trên xuống**. 🔴 làm trước/gấp · 🟠 tiếp theo · 🔵 cuối cùng. Chi tiết kỹ thuật ở [`../BRD.md`](../BRD.md).

## Bạn làm gì?
| Service | Nhiệm vụ | Flow chủ trì | Trạng thái |
|---|---|---|---|
| **Workspace** | Workspace/project, mời thành viên, phân quyền | B | Mới có khung |
| **Notification** | Gửi thông báo in-app/email | F | Chưa có gì |

---

## 🔴 Ưu tiên 1 — Dựng DB Workspace
- [ ] Tạo bảng `workspace`, `project`, `workspace_member`, `project_member`, `invitation` (`quraex.dbml` group 2).
- [ ] `project_key` dùng **partial unique** `WHERE deleted_at IS NULL` (quy ước DB).

## 🔴 Ưu tiên 2 — Workspace CRUD cơ bản
- [ ] API tạo **workspace** + **project**.
- [ ] API **mời thành viên** (bản đơn giản — chưa cần hết hạn tự động).
- [ ] Chạy thử service lên `/health` qua gateway.

## 🟠 Ưu tiên 3 — Phân quyền + mời nâng cao
- [ ] **RBAC đầy đủ:** workspace (Owner/Admin/Member) + project (Editor/Viewer); chặn truy cập sai quyền.
- [ ] **Mời hết hạn 7 ngày (saga):** gửi lời mời → chờ → tự hết hạn / hủy nếu không phản hồi (compensation).
- [ ] Xóa thành viên / đổi role.

## 🟠 Ưu tiên 4 — Phát/nhận event
- [ ] **Phát `MembershipChanged`** (đổi thành viên) + **`ProjectCreated`** (tạo project). Chốt shape `MembershipChanged` sớm — **báo Giang** (Authoring + TestArtifact đều nhận).
- [ ] **Nhận `UserRegistered`** (từ Identity của Minh) → tạo hồ sơ thành viên (idempotent).

## 🔵 Ưu tiên 5 — Notification (làm cuối — Pha 3)
- [ ] `./scripts/new-service.sh Notification --db mongo` → ⚠️ phần lưu trữ baked vẫn kiểu Postgres, **thay thủ công sang MongoDB**.
- [ ] Thêm **MongoDB** vào AppHost (mẫu trong `docs/TASKS.md`); collection camelCase.
- [ ] **Nhận** `TestRunCompleted` (từ Văn) + `MembershipChanged` (chính bạn) → đọc `notification_preferences` → ghi Mongo → gửi in-app/email.
- [ ] API `GET /api/notifications` đọc từ Mongo.

---

## ✅ Mốc "dựng xong" (Ưu tiên 1–2)
Tạo được workspace/project + mời thành viên cơ bản; service lên `/health`.

📎 Chi tiết: [BRD](../BRD.md) · mẫu code: `services/authoring/`
🗂️ **Xem `quraex.dbml`:** mở https://dbdiagram.io → New diagram → dán nội dung `docs/database/quraex.dbml` (tìm group của bạn). HD: [BRD §6](../BRD.md).
