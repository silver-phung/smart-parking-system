# SMART PARKING SYSTEM – ACCOUNTS & AUTHENTICATION ERD
## SƠ ĐỒ QUAN HỆ THỰC THỂ CHI TIẾT PHÂN HỆ TÀI KHOẢN & XÁC THỰC

- **Dự án:** Smart Parking System (FPT Software OJT)
- **Tài liệu:** Task 02B & Task 02D – Thiết kế ERD Chi tiết Phân hệ Accounts (Bản cập nhật xác thực chính thức)
- **Hệ quản trị CSDL:** PostgreSQL (Neon Serverless Postgres)
- **Trạng thái:** Phương án thiết kế đề xuất cập nhật theo quyết định chính thức của Owner Phụng.
- **Đánh dấu kiểm toán:** Toàn bộ nội dung cập nhật mới được gắn cờ **`[NEEDS DELTA AUDIT]`** để Agent Cursor kiểm toán bổ sung trước khi triển khai code.

---

## 1. QUYẾT ĐỊNH NGHIỆP VỤ XÁC THỰC CHÍNH THỨC (CONFIRMED BY OWNER)

1. **Đăng ký (Registration):** Người dùng đăng ký bằng **Username + Email + Password** (kèm họ tên, số điện thoại). `username` là tên đăng nhập riêng biệt, không phải email. `email` bắt buộc phải là email thật và phải được xác minh bằng mã gửi qua email.
2. **Đăng nhập (Login):** Đăng nhập bằng **Username + Password**. Trong phiên bản này, **không sử dụng Email làm định danh đăng nhập**. Tài khoản chưa hoàn tất xác minh email (`is_email_verified = FALSE`) **tuyệt đối không được phép đăng nhập**.
3. **Google Login:** Xếp loại **OPTIONAL / FUTURE** (chưa triển khai trong Sprint 3, không thêm cột/bảng OAuth làm ảnh hưởng tiến độ).

---

## 2. SƠ ĐỒ ERD CHI TIẾT 5 BẢNG ACCOUNTS (MERMAID ERD)

```mermaid
erDiagram
    users ||--o{ user_roles : "được gán vai trò (1-N)"
    roles ||--o{ user_roles : "chứa các tài khoản (1-N)"
    users ||--o{ refresh_tokens : "sở hữu phiên đăng nhập (1-N)"
    users ||--o{ user_tokens : "nhận mã xác minh / reset pass (1-N)"
    users ||--o{ users : "phê duyệt tài khoản (approved_by)"

    users {
        BIGINT id PK "Khóa chính sinh tự động (IDENTITY)"
        VARCHAR(50) username UK "[NEEDS DELTA AUDIT] Tên đăng nhập duy nhất (Dùng đăng nhập)"
        VARCHAR(255) email UK "Email thật duy nhất (Dùng nhận mã xác minh, không dùng đăng nhập)"
        VARCHAR(255) password_hash "Băm mật khẩu an toàn (Nội bộ - Không trả ra API)"
        VARCHAR(100) full_name "Họ và tên người dùng (User được phép sửa)"
        VARCHAR(20) phone_number "Số điện thoại liên hệ (User được phép sửa)"
        VARCHAR(500) avatar_url "Đường dẫn ảnh đại diện (Chỉ Server ghi khi upload)"
        BOOLEAN is_active "Trạng thái tài khoản (Hệ thống / Admin sửa)"
        BOOLEAN is_email_verified "Đã xác minh email thật qua mã hay chưa (Bắt buộc TRUE để login)"
        VARCHAR(20) approval_status "Trạng thái duyệt: PENDING, APPROVED, REJECTED"
        BIGINT approved_by FK "ID của Admin phê duyệt (Nullable)"
        TIMESTAMPTZ approved_at "Thời điểm phê duyệt (Nullable)"
        INT failed_login_attempts "Số lần nhập sai pass liên tiếp (Nội bộ)"
        TIMESTAMPTZ lockout_end_at "Hết hạn khóa tạm thời (Nội bộ)"
        TIMESTAMPTZ created_at "Thời điểm tạo tài khoản (Hệ thống)"
        TIMESTAMPTZ updated_at "Thời điểm cập nhật gần nhất (Hệ thống)"
    }

    roles {
        INT id PK "Khóa chính vai trò (IDENTITY)"
        VARCHAR(50) code UK "Mã chuẩn hóa: ADMIN, OPERATOR, CUSTOMER"
        VARCHAR(100) name "Tên hiển thị tiếng Việt của vai trò"
        VARCHAR(255) description "Mô tả chi tiết quyền hạn của vai trò"
        TIMESTAMPTZ created_at "Thời điểm tạo vai trò"
    }

    user_roles {
        BIGINT user_id PK,FK "Tham chiếu users.id"
        INT role_id PK,FK "Tham chiếu roles.id"
        TIMESTAMPTZ assigned_at "Thời điểm gán vai trò"
        BIGINT assigned_by FK "ID Admin thực hiện gán quyền (Nullable)"
    }

    refresh_tokens {
        BIGINT id PK "Khóa chính bản ghi token"
        BIGINT user_id FK "Tham chiếu users.id"
        VARCHAR(64) token_hash UK "Băm SHA-256 của Refresh Token (Không lưu chuỗi thô)"
        VARCHAR(255) user_agent "Thông tin trình duyệt / thiết bị đăng nhập"
        VARCHAR(45) ip_address "Địa chỉ IP client khi đăng nhập"
        TIMESTAMPTZ expires_at "Thời điểm hết hạn của token (Đề xuất: 7 ngày)"
        BOOLEAN is_revoked "Đã bị thu hồi / đăng xuất hay chưa"
        TIMESTAMPTZ revoked_at "Thời điểm thu hồi token (Nullable)"
        TIMESTAMPTZ created_at "Thời điểm cấp phát token"
    }

    user_tokens {
        BIGINT id PK "Khóa chính bản ghi mã xác minh"
        BIGINT user_id FK "Tham chiếu users.id"
        VARCHAR(50) token_type "Loại mã: EMAIL_VERIFICATION, PASSWORD_RESET"
        VARCHAR(64) token_hash UK "Băm SHA-256 của mã xác minh gửi qua email"
        INT attempts_count "[NEEDS DELTA AUDIT] Số lần nhập sai mã (Tối đa 5 lần chống brute-force)"
        TIMESTAMPTZ expires_at "Thời điểm hết hạn của mã (Đề xuất: 10-15 phút)"
        BOOLEAN is_used "Đã sử dụng hay chưa (Mặc định: FALSE)"
        TIMESTAMPTZ used_at "Thời điểm mã được sử dụng (Nullable)"
        TIMESTAMPTZ created_at "Thời điểm phát hành mã"
    }
```

---

## 3. PHÂN TÍCH THUỘC TÍNH VÀ QUY TẮC DỮ LIỆU [NEEDS DELTA AUDIT]

### 3.1. Bảng `users` – Phân định rõ ràng giữa `username` và `email`
- **`username` (Tên đăng nhập):**
  - Định dạng: Chuỗi ký tự từ 3 đến 50 ký tự, cho phép chữ cái thường (`a-z`), chữ số (`0-9`), dấu gạch dưới (`_`) và dấu chấm (`.`).
  - Chuẩn hóa: Bắt buộc chuyển về chữ thường không dấu (`username = username.Trim().ToLowerInvariant()`) trước khi lưu và trước khi kiểm tra đăng nhập.
  - Ràng buộc: `NOT NULL`, `UNIQUE` (`uq_users_username`). Không cho phép người dùng tự đổi `username` sau khi tạo.
  - Sử dụng làm định danh duy nhất để đối soát mật khẩu trong luồng Đăng nhập.
- **`email` (Địa chỉ email thật):**
  - Định dạng: Chuỗi chuẩn RFC 5322 (`VARCHAR(255)`).
  - Chuẩn hóa: `email = email.Trim().ToLowerInvariant()`.
  - Ràng buộc: `NOT NULL`, `UNIQUE` (`uq_users_email`).
  - Mục đích: Dùng để nhận mã xác minh OTP qua dịch vụ gửi email thật (SMTP/MailKit/SendGrid). **Không dùng làm định danh đăng nhập trong phiên bản này**.
- **Điều kiện tiên quyết đăng nhập (Login Invariant):**
  - Mọi tài khoản muốn đăng nhập thành công bắt buộc phải thỏa mãn đồng thời:
    $$\text{Đăng nhập thành công} \iff \text{Mật khẩu khớp} \land \text{is\_email\_verified} = \text{TRUE} \land \text{is\_active} = \text{TRUE} \land (\text{approval\_status} = \text{'APPROVED'} \lor \text{Role} = \text{'CUSTOMER'})$$

### 3.2. Bảng `user_tokens` – Cơ chế xác thực mã an toàn qua email thật
- **Bản chất mã xác minh:** Mã số gồm 6 chữ số ngẫu nhiên sinh bằng bộ sinh số ngẫu nhiên an toàn (`RandomNumberGenerator` / CSPRNG).
- **Lưu trữ an toàn:** Database **chỉ lưu `token_hash = SHA256(code)`**, không lưu mã thô.
- **Chống dò quét mã (Anti-Brute Force):** Bổ sung trường `attempts_count INT DEFAULT 0`. Mỗi lần nhập sai mã, tăng 1; nếu nhập sai quá 5 lần, mã bị vô hiệu hóa ngay lập tức.
- **Thời hạn mã (TTL):** Đề xuất 10 - 15 phút.
- **Chống sử dụng lại (Anti-Reuse & Atomic Consume):** Khi người dùng nhập đúng mã, hệ thống thực thi cập nhật nguyên tử `UPDATE user_tokens SET is_used = TRUE, used_at = NOW() WHERE ... AND is_used = FALSE AND expires_at > NOW() RETURNING user_id`.

---

## 4. TÍNH TƯƠNG THÍCH ĐỂ BÀN GIAO CHO FRONTEND (SONG)

Dựa trên sơ đồ cập nhật này, Song (Frontend Developer) thiết kế các màn hình giao diện:
1. **Màn hình Đăng ký (Register):** Form nhập `username`, `email`, `password`, `fullName`, `phoneNumber`. Frontend không gửi role (mặc định server gán CUSTOMER).
2. **Màn hình Nhập mã xác minh Email (Verify Email Code):** Form hiển thị thông báo đã gửi mã tới email, người dùng nhập mã 6 số (kèm nút "Gửi lại mã" / Resend Code sau cooldown 60 giây).
3. **Màn hình Đăng nhập (Login):** Nhận **`username`** và `password` (không nhận email). Nếu tài khoản chưa xác minh email, hiển thị thông báo lỗi và điều hướng sang màn hình nhập mã xác minh.
4. **Màn hình Hồ sơ cá nhân (Profile):** Hiển thị `username`, `email`, `fullName`, `phoneNumber`, `avatarUrl`, `roles`, `isEmailVerified`, `approvalStatus`. Cho phép sửa: `fullName`, `phoneNumber`.

---

## 5. TỔNG KẾT
Sơ đồ Accounts ERD đã cập nhật trọn vẹn theo quyết định nghiệp vụ chính thức của Owner Phụng và sẵn sàng cho đợt **Cursor Delta Audit** tại Task 02D.
