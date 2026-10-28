# SMART PARKING SYSTEM – ACCOUNTS DATA DICTIONARY
## TỪ ĐIỂN DỮ LIỆU CHI TIẾT PHÂN HỆ TÀI KHOẢN & XÁC THỰC

- **Dự án:** Smart Parking System (FPT Software OJT)
- **Tài liệu:** Task 02D – Từ điển dữ liệu Accounts (Bản Thiết kế Chính thức)
- **Chịu trách nhiệm kiến trúc CSDL:** Phụng (Backend / Database Lead)
- **Hệ quản trị CSDL:** PostgreSQL trên Neon Cloud
- **Quy ước:** Kiểu dữ liệu chuẩn PostgreSQL, múi giờ lưu trữ `TIMESTAMPTZ` (UTC).
- **Trạng thái phê duyệt:** **APPROVED BY OWNER** (Đã chính thức phê duyệt toàn bộ quyết định thiết kế).

---

## 1. BẢNG `users` (NGƯỜI DÙNG TOÀN HỆ THỐNG)

- **Mục đích (tiếng Việt):** Quản lý hồ sơ định danh, tên đăng nhập `username`, địa chỉ `email` thật, vai trò `role_id`, trạng thái kích hoạt, xác minh email, phê duyệt tài khoản và các thông số chống brute-force.
- **Mức độ ưu tiên:** **CORE** (Bắt buộc cho Sprint 3).
- **Phân loại nguồn gốc:** **CONFIRMED & APPROVED BY OWNER**.

### 1.1. Chi tiết các cột dữ liệu & Phân định quyền thao tác

| Tên cột (Column) | Kiểu PostgreSQL | Nullable | Mặc định | Khóa / Ràng buộc | Quyền sửa đổi (Access Scope) | Mô tả nghiệp vụ |
| :--- | :--- | :---: | :---: | :---: | :---: | :--- |
| `id` | `BIGINT` | NO | `IDENTITY` | **PK** | Hệ thống | Khóa chính sinh tự động (`GENERATED ALWAYS AS IDENTITY`). |
| `username` | `VARCHAR(50)` | NO | None | **UK** | Admin / Hệ thống | Tên đăng nhập duy nhất. Regex: `^[a-z0-9_.]{3,50}$`. Chuẩn hóa lowercase. Dùng để đăng nhập. Không cho sửa qua API Profile. |
| `email` | `VARCHAR(255)` | NO | None | **UK** | Admin / Hệ thống | Địa chỉ email thật duy nhất dùng để nhận mã xác minh OTP. Chuẩn hóa lowercase. **Không dùng làm định danh đăng nhập trong phiên bản này.** |
| `password_hash` | `VARCHAR(255)` | NO | None | - | **NỘI BỘ (Security)** | Chuỗi băm mật khẩu an toàn (`PasswordHasher<User>` PBKDF2 HMAC-SHA512 / BCrypt WorkFactor 12). **Tuyệt đối không trả ra API/DTO!** |
| `role_id` | `INT` | NO | None | **FK** | Admin / Hệ thống | Khóa ngoại trỏ đến `roles(id)` (`ON DELETE RESTRICT`). Mô hình 1-N (một user một vai trò) đã được Owner phê duyệt chính thức. |
| `full_name` | `VARCHAR(100)` | NO | None | - | **User Editable** | Họ và tên hiển thị của người dùng. Người dùng được phép tự sửa trên trang Profile. |
| `phone_number` | `VARCHAR(20)` | YES | NULL | - | **User Editable** | Số điện thoại liên lạc khi có sự cố bãi đỗ. Người dùng được phép tự sửa trên trang Profile. |
| `avatar_url` | `VARCHAR(500)` | YES | NULL | - | Admin / Hệ thống | URL ảnh đại diện. Chỉ hệ thống ghi nhận khi upload file; **không cho client gửi link tùy ý qua Update Profile DTO để chống XSS**. |
| `is_active` | `BOOLEAN` | NO | `TRUE` | - | Admin / Hệ thống | Trạng thái hoạt động (`TRUE`: bình thường, `FALSE`: tài khoản bị Admin vô hiệu hóa). |
| `is_email_verified`| `BOOLEAN` | NO | `FALSE` | - | Hệ thống | Đã xác minh email thật qua mã OTP hay chưa. **Bắt buộc `TRUE` mới được phép đăng nhập.** |
| `approval_status` | `VARCHAR(20)` | NO | `'PENDING'` | **CHECK** | Admin / Hệ thống | Trạng thái phê duyệt: `'PENDING'`, `'APPROVED'`, `'REJECTED'`. Khách hàng tự động chuyển `APPROVED` sau khi xác minh email thành công; Nhân viên cần Admin duyệt. |
| `approved_by` | `BIGINT` | YES | NULL | **FK** | Admin / Hệ thống | Tham chiếu đến `users.id` của Admin đã thực hiện phê duyệt (ON DELETE SET NULL). |
| `approved_at` | `TIMESTAMPTZ` | YES | NULL | - | Admin / Hệ thống | Mốc thời gian Admin phê duyệt tài khoản. |
| `failed_login_attempts`| `INT` | NO | `0` | **CHECK** | **NỘI BỘ (Security)** | Số lần nhập sai mật khẩu liên tiếp. Đặt lại 0 khi đăng nhập đúng. Ngưỡng 5 lần khóa 15 phút. **Không trả ra API!** |
| `lockout_end_at` | `TIMESTAMPTZ` | YES | NULL | - | **NỘI BỘ (Security)** | Thời điểm hết hạn khóa tạm thời. Nếu `NOW() < lockout_end_at`, từ chối đăng nhập (chỉ kiểm tra sau khi mật khẩu đã đúng). **Không trả ra API!** |
| `created_at` | `TIMESTAMPTZ` | NO | `NOW()` | - | Hệ thống | Thời điểm đăng ký tài khoản (tự động gán UTC hiện tại). |
| `updated_at` | `TIMESTAMPTZ` | NO | `NOW()` | - | Hệ thống | Thời điểm cập nhật hồ sơ gần nhất. |

### 1.2. Quy tắc chuẩn hóa & Ràng buộc
1. **Chuẩn hóa `username`:**
   - Định dạng: `^[a-z0-9_.]{3,50}$` (độ dài 3 - 50 ký tự).
   - Tầng Application chuẩn hóa trước khi lưu / tra cứu: `username = username.Trim().ToLowerInvariant()`.
   - Ràng buộc: `CONSTRAINT uq_users_username UNIQUE (username)`.
2. **Chuẩn hóa `email`:**
   - Định dạng: Chuẩn RFC 5322.
   - Tầng Application chuẩn hóa trước khi lưu: `email = email.Trim().ToLowerInvariant()`.
   - Ràng buộc: `CONSTRAINT uq_users_email UNIQUE (email)`.
3. **Primary Key & Foreign Keys:**
   - `pk_users PRIMARY KEY (id)`.
   - `fk_users_role FOREIGN KEY (role_id) REFERENCES roles(id) ON DELETE RESTRICT`.
   - `fk_users_approved_by FOREIGN KEY (approved_by) REFERENCES users(id) ON DELETE SET NULL`.
4. **Xử lý Race-Condition:** Bắt mã lỗi PostgreSQL `23505` (Unique Violation) tại tầng Application khi có xung đột đồng thời trên `username` hoặc `email`, chuyển đổi thành `409 Conflict`.

---

## 2. BẢNG `roles` (DANH MỤC VAI TRÒ HỆ THỐNG)

- **Mục đích (tiếng Việt):** Quản lý danh mục các vai trò để phân quyền kiểm soát truy cập (RBAC).
- **Mức độ ưu tiên:** **CORE** (Bắt buộc cho Sprint 3).
- **Phân loại nguồn gốc:** **CONFIRMED & APPROVED BY OWNER**.

| Tên cột | Kiểu PostgreSQL | Nullable | Mặc định | Khóa / Ràng buộc | Mô tả nghiệp vụ |
| :--- | :--- | :---: | :---: | :---: | :--- |
| `id` | `INT` | NO | `IDENTITY` | **PK** | Khóa chính sinh tự động (`GENERATED ALWAYS AS IDENTITY`). |
| `code` | `VARCHAR(50)` | NO | None | **UK** | Mã định danh chuẩn hóa viết hoa không dấu (`ADMIN`, `MANAGER`, `STAFF`, `USER`). |
| `name` | `VARCHAR(100)` | NO | None | - | Tên hiển thị tiếng Việt (Quản trị viên, Quản lý bãi đỗ, Nhân viên vận hành, Người dùng). |
| `description` | `VARCHAR(255)` | YES | NULL | - | Mô tả phạm vi quyền hạn và trách nhiệm của vai trò trong hệ thống. |
| `created_at` | `TIMESTAMPTZ` | NO | `NOW()` | - | Thời điểm khởi tạo vai trò trong hệ thống. |

---

## 3. MÔ HÌNH PHÂN QUYỀN 1-N (ĐÃ LOẠI BỎ BẢNG `user_roles`)

- **Quyết định chính thức:** Owner Phụng đã chính thức phê duyệt mô hình **một user một vai trò (1-N)**:
  - Cột `users.role_id INT NOT NULL FK REFERENCES roles(id)` đại diện cho vai trò duy nhất của mỗi tài khoản.
  - **Bảng trung gian `user_roles` bị loại bỏ hoàn toàn khỏi thiết kế CSDL**.
  - **Lợi ích kiến trúc:** Đơn giản hóa cấu trúc phân quyền, không có rủi ro leo quyền khi auto-approve tài khoản `USER`, giảm số lượng JOIN và tối ưu hóa việc nạp Role Claim vào JWT Access Token.

---

## 4. BẢNG `refresh_tokens` (PHIÊN ĐĂNG NHẬP DÀI HẠN & THU HỒI)

- **Mục đích (tiếng Việt):** Quản lý phiên Refresh Token dài hạn, phục vụ cấp mới Access Token mà không yêu cầu đăng nhập lại, hỗ trợ Đăng xuất an toàn.
- **Mức độ ưu tiên:** **CORE** (Chốt theo quyết định chính thức của Owner).
- **Phân loại nguồn gốc:** **CONFIRMED & APPROVED BY OWNER**.

| Tên cột | Kiểu PostgreSQL | Nullable | Mặc định | Khóa / Ràng buộc | Mô tả nghiệp vụ |
| :--- | :--- | :---: | :---: | :---: | :--- |
| `id` | `BIGINT` | NO | `IDENTITY` | **PK** | Khóa chính sinh tự động (`GENERATED ALWAYS AS IDENTITY`). |
| `user_id` | `BIGINT` | NO | None | **FK** | Tham chiếu đến `users.id` của người sở hữu phiên (ON DELETE CASCADE). |
| `token_hash` | `VARCHAR(64)` | NO | None | **UK** | **Chuỗi băm SHA-256 (64 ký tự hex) của Refresh Token 256-bit thô. UNIQUE toàn cục.** Tuyệt đối không lưu token thô. |
| `user_agent` | `VARCHAR(255)` | YES | NULL | - | Thông tin thiết bị / trình duyệt đăng nhập. |
| `ip_address` | `VARCHAR(45)` | YES | NULL | - | Địa chỉ IPv4 hoặc IPv6 của client tại thời điểm đăng nhập. |
| `expires_at` | `TIMESTAMPTZ` | NO | None | - | Thời điểm hết hạn của Refresh Token (7 ngày, không xoay vòng). |
| `is_revoked` | `BOOLEAN` | NO | `FALSE` | - | Trạng thái thu hồi: `TRUE` nếu người dùng đã bấm Đăng xuất hoặc bị buộc thoát. |
| `revoked_at` | `TIMESTAMPTZ` | YES | NULL | - | Thời điểm thu hồi token. |
| `created_at` | `TIMESTAMPTZ` | NO | `NOW()` | - | Thời điểm cấp phát Refresh Token. |

---

## 5. BẢNG `user_tokens` (MÃ OTP XÁC MINH EMAIL QUA DỊCH VỤ THẬT)

- **Mục đích (tiếng Việt):** Quản lý các mã xác thực OTP một lần (6 chữ số) được gửi qua dịch vụ email thật để kích hoạt tài khoản khi đăng ký mới.
- **Mức độ ưu tiên:** **CORE** (Bắt buộc cho quy trình xác thực email).
- **Phân loại nguồn gốc:** **CONFIRMED & APPROVED BY OWNER**.

### 5.1. Chi tiết các cột dữ liệu & Cơ chế bảo mật HMAC-SHA256

| Tên cột | Kiểu PostgreSQL | Nullable | Mặc định | Khóa / Ràng buộc | Mô tả nghiệp vụ |
| :--- | :--- | :---: | :---: | :---: | :--- |
| `id` | `BIGINT` | NO | `IDENTITY` | **PK** | Khóa chính sinh tự động (`GENERATED ALWAYS AS IDENTITY`). |
| `user_id` | `BIGINT` | NO | None | **FK** | Tham chiếu đến `users.id` của người nhận mã xác minh (ON DELETE CASCADE). |
| `token_type` | `VARCHAR(50)` | NO | None | **CHECK** | Loại mã: `'EMAIL_VERIFICATION'` hoặc `'PASSWORD_RESET'`. |
| `token_hash` | `VARCHAR(64)` | NO | None | - | **HMAC-SHA256(secret_key, $"{userId}:{token_type}:{otpCode}")**. **KHÔNG CÓ RÀNG BUỘC UNIQUE TOÀN CỤC**. Không lưu mã OTP thô. Khóa bí mật đọc từ biến môi trường. |
| `attempts_count` | `INT` | NO | `0` | **CHECK** | Số lần người dùng đã nhập sai mã. Nếu `attempts_count >= 5`, mã tự động vô hiệu hóa để chống tấn công brute-force vét cạn 6 chữ số. |
| `expires_at` | `TIMESTAMPTZ` | NO | None | - | Thời điểm hết hạn của mã (15 phút). |
| `is_used` | `BOOLEAN` | NO | `FALSE` | - | Trạng thái đã sử dụng: `TRUE` nếu mã đã được xác nhận thành công. Ngăn chặn tái sử dụng mã. |
| `used_at` | `TIMESTAMPTZ` | YES | NULL | - | Thời điểm mã được sử dụng thành công. |
| `created_at` | `TIMESTAMPTZ` | NO | `NOW()` | - | Thời điểm hệ thống phát hành mã và gửi email. |

### 5.2. Ràng buộc & Chỉ mục
- **Primary Key:** `pk_user_tokens PRIMARY KEY (id)`.
- **Foreign Key:** `fk_user_tokens_user FOREIGN KEY (user_id) REFERENCES users(id) ON DELETE CASCADE`.
- **Check Constraints:**
  - `chk_user_tokens_type CHECK (token_type IN ('EMAIL_VERIFICATION', 'PASSWORD_RESET'))`.
  - `chk_user_tokens_attempts CHECK (attempts_count >= 0)`.
- **Index:** `idx_user_tokens_user_type ON (user_id, token_type)` phục vụ tra cứu mã active và vô hiệu hóa mã cũ khi người dùng bấm "Gửi lại mã xác minh".

---

## 6. CHUẨN HÓA DATA CONTRACT CHO AUTHENTICATION API (BÀN GIAO CHO SONG)

### 6.1. Contract Đăng ký: `POST /api/v1/auth/register`
**Request DTO (`RegisterRequestDto`):**
```json
{
  "username": "phung_vo",
  "email": "phung.vo@example.com",
  "password": "SecurePassword@123",
  "fullName": "Võ Phụng",
  "phoneNumber": "0901234567",
  "recaptchaToken": "03AFcWeA7..."
}
```
*(Server xác minh `recaptchaToken` server-side với Google, không lưu token vào DB; chuẩn hóa lowercase cho `username` và `email`; gán mặc định `role_id` là `USER`; phát sinh mã OTP 6 số và gửi qua email thật).*

### 6.2. Contract Xác minh Email: `POST /api/v1/auth/verify-email`
**Request DTO (`VerifyEmailRequestDto`):**
```json
{
  "email": "phung.vo@example.com",
  "code": "849201"
}
```
*(Thống nhất nhận `email` và `code` 6 chữ số. Khi thành công, `is_email_verified = TRUE` và `approval_status = 'APPROVED'`).*

### 6.3. Contract Gửi lại Mã xác minh: `POST /api/v1/auth/resend-verification`
**Request DTO (`ResendVerificationRequestDto`):**
```json
{
  "email": "phung.vo@example.com"
}
```
*(Vô hiệu hóa toàn bộ mã cũ chưa dùng, sinh mã mới, gửi email thật, áp dụng cooldown tối thiểu 60s).*

### 6.4. Contract Đăng nhập: `POST /api/v1/auth/login`
**Request DTO (`LoginRequestDto`):**
```json
{
  "username": "phung_vo",
  "password": "SecurePassword@123"
}
```
*(Đăng nhập duy nhất bằng `username` và `password`. Không nhận email. Không bắt OTP hay reCAPTCHA khi đăng nhập).*

### 6.5. Contract Cấp mới Token: `POST /api/v1/auth/refresh`
**Request DTO (`RefreshTokenRequestDto`):**
```json
{
  "refreshToken": "c9a7b8e1f2..."
}
```
*(Giữ nguyên refresh token hiện tại, cấp mới JWT Access Token 15 phút).*

### 6.6. Contract Đăng xuất: `POST /api/v1/auth/logout`
**Request DTO (`LogoutRequestDto`):**
```json
{
  "refreshToken": "c9a7b8e1f2..."
}
```
*(Chỉ yêu cầu Refresh Token hợp lệ để thu hồi phiên trong CSDL, **kể cả khi Access Token JWT đã hết hạn** người dùng vẫn đăng xuất thành công).*

### 6.7. Contract Xem Hồ sơ: `GET /api/v1/users/me`
**Headers:** `Authorization: Bearer <accessToken>`
**Response DTO (`UserProfileDto`):**
```json
{
  "id": 1,
  "username": "phung_vo",
  "email": "phung.vo@example.com",
  "fullName": "Võ Phụng",
  "phoneNumber": "0901234567",
  "avatarUrl": null,
  "role": "USER",
  "isEmailVerified": true,
  "approvalStatus": "APPROVED",
  "createdAt": "2026-10-23T08:00:00Z"
}
```
*(Tuyệt đối không trả `password_hash`, `token_hash`, `failed_login_attempts`, `lockout_end_at`).*

### 6.8. Contract Cập nhật Hồ sơ: `PUT /api/v1/users/me`
**Headers:** `Authorization: Bearer <accessToken>`
**Request DTO (`UpdateProfileRequestDto`):**
```json
{
  "fullName": "Võ Phụng",
  "phoneNumber": "0909998888"
}
```
*(Chỉ cho phép cập nhật `fullName` và `phoneNumber`. Đã loại bỏ `avatarUrl`. `username`, `email`, `role`, `approvalStatus` bất biến).*
