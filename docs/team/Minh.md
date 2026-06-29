# Minh — Identity (AWS Cognito) + Integration (Jira)

> Đầu việc xếp **theo thứ tự ưu tiên — làm từ trên xuống**. 🔴 làm trước/gấp · 🟠 tiếp theo · 🟡 sau · 🔵 cuối cùng. Chi tiết kỹ thuật ở [`../BRD.md`](../BRD.md).

## Bạn làm gì?
| Service | Nhiệm vụ | Flow chủ trì | Trạng thái |
|---|---|---|---|
| **Identity** | Đăng ký / đăng nhập / cấp token — **dùng AWS Cognito** | A | Mới có khung |
| **Integration** | Đồng bộ Jira 2 chiều | C (tham gia) | Chưa có gì |

> 🔑 IAM dùng **AWS Cognito** (managed) — Cognito lo đăng ký/đăng nhập/cấp token, **không tự dựng OpenIddict + bảng user**. ⭐ Cả team cần token từ Cognito → ưu tiên 1–2 làm sớm.

---

## 🔴 Ưu tiên 1 — Dựng AWS Cognito
- [ ] Tạo **Cognito User Pool**: bật đăng ký + đăng nhập bằng email + mật khẩu.
- [ ] Tạo **App Client**, lấy `User Pool ID`, `Client ID`, `region`, domain.
- [ ] Test: đăng nhập thử trên Cognito lấy được JWT.

## 🔴 Ưu tiên 2 — Nối token vào hệ thống
- [ ] Cấu hình service validate JWT **do Cognito phát** (issuer + JWKS: `https://cognito-idp.<region>.amazonaws.com/<pool-id>`).
- [ ] Lưu config (Pool ID, Client ID, region) vào **biến môi trường**, **không commit** git.
- [ ] **Báo Văn** thông tin issuer/JWKS để Văn chỉnh gateway.
- [ ] Test: lấy token Cognito → gọi 1 API qua gateway OK.

## 🟠 Ưu tiên 3 — Service Identity mỏng (đã chốt: Cách A)
> Giữ service Identity nhưng chỉ làm lớp mỏng — schema đã cập nhật ở `quraex.dbml` group 1.
- [ ] Tạo bảng `user_profile` (id = Cognito `sub`, email, display_name, status).
- [ ] Đồng bộ user từ Cognito về `user_profile` — qua **Cognito post-confirmation trigger** hoặc lần đầu user gọi API có token.
- [ ] **Phát event `UserRegistered`** (Workspace + Notification nhận) sau khi tạo profile.

## 🟡 Ưu tiên 4 — Hoàn thiện auth (phần lớn là cấu hình Cognito)
- [ ] Quên mật khẩu (FR-05). Nếu kịp: đăng nhập Google/OAuth (FR-03), 2FA (FR-04).

## 🔵 Ưu tiên 5 — Integration / Jira (làm cuối — Pha 3)
- [ ] `./scripts/new-service.sh Integration` → nối AppHost/gateway/CI.
- [ ] Tạo bảng kết nối Jira (`quraex.dbml` group 7); token Jira **mã hóa**, không commit.
- [ ] **Webhook inbound:** nhận webhook Jira → kiểm tra chữ ký → bỏ trùng → **dịch dữ liệu Jira sang model nội bộ (ACL)** → báo Authoring tạo story.
- [ ] **Outbound:** khi có story / test case mới → đẩy lên Jira (tự thử lại nếu lỗi tạm) → phát `JiraIssueLinked`.

---

## ✅ Mốc quan trọng
Ưu tiên 1–2 xong = **cả team chuyển sang token thật** được (Pha 1). Cố gắng xong sớm.

📎 Chi tiết: [BRD](../BRD.md) · mẫu code: `services/authoring/`
🗂️ **Xem `quraex.dbml`:** mở https://dbdiagram.io → New diagram → dán nội dung `docs/database/quraex.dbml` (tìm group của bạn). HD: [BRD §6](../BRD.md).
