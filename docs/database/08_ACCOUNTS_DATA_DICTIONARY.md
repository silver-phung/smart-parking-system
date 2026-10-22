# SMART PARKING SYSTEM – ACCOUNTS DATA DICTIONARY
## TỪ ĐIỂN DỮ LIỆU CHI TIẾT PHÂN HỆ TÀI KHOẢN & XÁC THỰC

- **Dự án:** Smart Parking System (FPT Software OJT)
- **Tài liệu:** Task 02B & Task 02D – Từ điển dữ liệu Accounts (Bản cập nhật xác thực chính thức)
- **Hệ quản trị CSDL:** PostgreSQL trên Neon Cloud
- **Quy ước:** Kiểu dữ liệu chuẩn PostgreSQL, múi giờ lưu trữ `TIMESTAMPTZ` (UTC).
- **Đánh dấu kiểm toán:** Toàn bộ nội dung cập nhật mới được gắn cờ **`[NEEDS DELTA AUDIT]`** để Agent Cursor kiểm toán bổ sung.

---

## 1. BẢNG `users` (NGƯỜI DÙNG TOÀN HỆ THỐNG)

- **Mục đích (tiếng Việt):** Quản lý hồ sơ định danh, tên đăng nhập `username`, địa chỉ `email` thật, trạng thái kích hoạt, xác minh email, phê duyệt tài khoản và các thông số chống brute-force.
- **Mức độ ưu tiên:** **CORE** (Bắt buộc cho Sprint 3).
- **Phân loại nguồn gốc:** **CONFIRMED BY OWNER**.

### 1.1. Chi tiết các cột dữ liệu & Phân định quyền thao tác

| Tên cột (Column) | Kiểu PostgreSQL | Nullable | Mặc định | Khóa / Ràng buộc | Quyền sửa đổi (Access Scope) | Mô tả nghiệp vụ |
| :--- | :--- | :---: | :---: | :---: | :---: | :--- |
| `id` | `BIGINT` | NO | `IDENTITY` | **PK** | Hệ thống | Khóa chính sinh tự động (`GENERATED ALWAYS AS IDENTITY`). |
| `username` | `VARCHAR(50)` | NO | None | **UK** | Admin / Hệ thống | **[NEEDS DELTA AUDIT]** Tên đăng nhập duy nhất của người dùng. Chuẩn hóa chữ thường không dấu (`a-z`, `0-9`, `_`, `.`). Dùng để đăng nhập cùng mật khẩu. Không cho phép tự đổi sau đăng ký. |
| `email` | `VARCHAR(255)` | NO | None | **UK** | Admin / Hệ thống | **[NEEDS DELTA AUDIT]** Địa chỉ email thật duy nhất dùng để nhận mã xác minh. Chuẩn hóa lowercase. **Không dùng làm định danh đăng nhập trong phiên bản này.** |
| `password_hash` | `VARCHAR(255)` | NO | None | - | **NỘI BỘ (Security)** | Chuỗi băm mật khẩu an toàn (`PasswordHasher<User>` PBKDF2 HMAC-SHA512). **Tuyệt đối không trả ra API/DTO!** |
| `full_name` | `VARCHAR(100)` | NO | None | - | **User Editable** | Họ và tên hiển thị của người dùng. Người dùng được phép tự sửa trên trang Profile. |
| `phone_number` | `VARCHAR(20)` | YES | NULL | - | **User Editable** | Số điện thoại liên lạc khi có sự cố bãi đỗ. Người dùng được phép tự sửa trên trang Profile. |
| `avatar_url` | `VARCHAR(500)` | YES | NULL | - | Admin / Hệ thống | URL ảnh đại diện. Chỉ hệ thống ghi nhận khi upload file; không cho client gửi link tùy ý để chống XSS. |
| `is_active` | `BOOLEAN` | NO | `TRUE` | - | Admin / Hệ thống | Trạng thái hoạt động (`TRUE`: bình thường, `FALSE`: tài khoản bị Admin vô hiệu hóa). |
| `is_email_verified`| `BOOLEAN` | NO | `FALSE` | - | Hệ thống | **[NEEDS DELTA AUDIT]** Đã xác minh email qua mã thật hay chưa. **Bắt buộc `TRUE` mới được phép đăng nhập.** |
| `approval_status` | `VARCHAR(20)` | NO | `'PENDING'` | **CHECK** | Admin / Hệ thống | Trạng thái phê duyệt: `'PENDING'`, `'APPROVED'`, `'REJECTED'`. Khách hàng tự động APPROVED sau khi xác minh email; Nhân viên cần Admin duyệt. |
| `approved_by` | `BIGINT` | YES | NULL | **FK** | Admin / Hệ thống | Tham chiếu đến `users.id` của Admin đã thực hiện phê duyệt. |
| `approved_at` | `TIMESTAMPTZ` | YES | NULL | - | Admin / Hệ thống | Mốc thời gian Admin phê duyệt tài khoản. |
| `failed_login_attempts`| `INT` | NO | `0` | **CHECK** | **NỘI BỘ (Security)** | Số lần nhập sai mật khẩu liên tiếp. Đặt lại 0 khi đăng nhập đúng. Ngưỡng 5 lần khóa 15 phút. **Không trả ra API!** |
| `lockout_end_at` | `TIMESTAMPTZ` | YES | NULL | - | **NỘI BỘ (Security)** | Thời điểm hết hạn khóa tạm thời. Nếu `NOW() < lockout_end_at`, từ chối đăng nhập. **Không trả ra API!** |
| `created_at` | `TIMESTAMPTZ` | NO | `NOW()` | - | Hệ thống | Thời điểm đăng ký tài khoản (tự động gán UTC hiện tại). |
| `updated_at` | `TIMESTAMPTZ` | NO | `NOW()` | - | Hệ thống | Thời điểm cập nhật hồ sơ gần nhất. |

### 1.2. Quy tắc chuẩn hóa & Ràng buộc [NEEDS DELTA AUDIT]
1. **Chuẩn hóa `username`:**
   - Định dạng hợp lệ: `^[a-z0-9_.]{3,50}$` (độ dài 3 - 50 ký tự, chỉ gồm chữ thường, chữ số, gạch dưới, dấu chấm).
   - Tầng Application chuẩn hóa trước khi lưu / tra cứu: `username = username.Trim().ToLowerInvariant()`.
   - Ràng buộc: `uq_users_username` UNIQUE trên cột `username`.
2. **Chuẩn hóa `email`:**
   - Định dạng hợp lệ: Email hợp thức theo chuẩn RFC 5322.
   - Tầng Application chuẩn hóa trước khi lưu: `email = email.Trim().ToLowerInvariant()`.
   - Ràng buộc: `uq_users_email` UNIQUE trên cột `email`.
3. **Primary Key & Foreign Key:**
   - `pk_users` trên `(id)`.
   - `fk_users_approved_by` trỏ đến `users(id)` ON DELETE SET NULL.
4. **Indexes tối ưu:**
   - `idx_users_username` (B-Tree trên `username`) phục vụ tra cứu đăng nhập tốc độ cao O(log N).
   - `idx_users_email` (B-Tree trên `email`) phục vụ tra cứu khi kiểm tra trùng lặp email lúc đăng ký và gửi mã.

#### Bảng dữ liệu mẫu (Non-sensitive Sample Data)

| id | username | email | full_name | phone_number | is_active | is_email_verified | approval_status | approved_by | failed_login_attempts | lockout_end_at |
| :---: | :--- | :--- | :--- | :--- | :---: | :---: | :---: | :---: | :---: | :---: |
| 1 | `admin_root` | `admin@smartparking.vn` | Nguyễn Văn Quản Trị | `0901234567` | TRUE | TRUE | `APPROVED` | NULL | 0 | NULL |
| 2 | `operator_01` | `operator1@smartparking.vn` | Lê Vận Hành | `0912345678` | TRUE | TRUE | `APPROVED` | 1 | 0 | NULL |
| 3 | `khachhang_a` | `khachhang@gmail.com` | Trần Thị Khách | `0987654321` | TRUE | TRUE | `APPROVED` | NULL | 0 | NULL |
| 4 | `nhanvien_moi` | `nhanvien_moi@gmail.com` | Phạm Văn Chờ | `0933112233` | TRUE | FALSE | `PENDING` | NULL | 0 | NULL |

---

## 2. BẢNG `roles` (DANH MỤC VAI TRÒ HỆ THỐNG)

- **Mục đích (tiếng Việt):** Quản lý danh mục các vai trò để phân quyền kiểm soát truy cập (RBAC).
- **Mức độ ưu tiên:** **CORE** (Bắt buộc cho Sprint 3).
- **Phân loại nguồn gốc:** **CONFIRMED**.

| Tên cột | Kiểu PostgreSQL | Nullable | Mặc định | Khóa / Ràng buộc | Mô tả nghiệp vụ |
| :--- | :--- | :---: | :---: | :---: | :--- |
| `id` | `INT` | NO | `IDENTITY` | **PK** | Khóa chính sinh tự động (`GENERATED ALWAYS AS IDENTITY`). |
| `code` | `VARCHAR(50)` | NO | None | **UK** | Mã định danh chuẩn hóa viết hoa không dấu (`ADMIN`, `OPERATOR`, `CUSTOMER`). |
| `name` | `VARCHAR(100)` | NO | None | - | Tên hiển thị tiếng Việt (Quản trị viên, Nhân viên điều hành, Khách hàng). |
| `description` | `VARCHAR(255)` | YES | NULL | - | Mô tả phạm vi quyền hạn và trách nhiệm của vai trò trong hệ thống. |
| `created_at` | `TIMESTAMPTZ` | NO | `NOW()` | - | Thời điểm khởi tạo vai trò trong hệ thống. |

---

## 3. BẢNG `user_roles` (BẢNG PHÂN QUYỀN N-N) & ĐÁNH GIÁ 1-N

- **Mục đích (tiếng Việt):** Bảng nối thể hiện mối quan hệ phân quyền.
- **Trạng thái Task 02D:** Cursor khuyến nghị chuyển sang 1-N (`users.role_id`), bỏ `user_roles`. Antigravity đã trình bảng quyết định chờ Owner chốt. Nếu Owner duyệt 1-N, bảng này sẽ được chuyển thành `users.role_id FK` tại Task 02E.

| Tên cột | Kiểu PostgreSQL | Nullable | Mặc định | Khóa / Ràng buộc | Mô tả nghiệp vụ |
| :--- | :--- | :---: | :---: | :---: | :--- |
| `user_id` | `BIGINT` | NO | None | **PK, FK** | Tham chiếu đến `users.id` (CASCADE). |
| `role_id` | `INT` | NO | None | **PK, FK** | Tham chiếu đến `roles.id` (RESTRICT). |
| `assigned_at` | `TIMESTAMPTZ` | NO | `NOW()` | - | Mốc thời gian gán vai trò. |
| `assigned_by` | `BIGINT` | YES | NULL | **FK** | Tham chiếu đến `users.id` của Admin đã gán quyền. |

---

## 4. BẢNG `refresh_tokens` (PHIÊN ĐĂNG NHẬP DÀI HẠN & THU HỒI)

- **Mục đích (tiếng Việt):** Quản lý phiên Refresh Token dài hạn, phục vụ việc cấp mới Access Token mà không yêu cầu đăng nhập lại, hỗ trợ Đăng xuất và Thu hồi phiên từ xa.
- **Mức độ ưu tiên:** **CORE** (sau khi chốt cần cơ chế logout an toàn).
- **Phân loại nguồn gốc:** **CONFIRMED BY 02C & OWNER**.

| Tên cột | Kiểu PostgreSQL | Nullable | Mặc định | Khóa / Ràng buộc | Mô tả nghiệp vụ |
| :--- | :--- | :---: | :---: | :---: | :--- |
| `id` | `BIGINT` | NO | `IDENTITY` | **PK** | Khóa chính sinh tự động (`GENERATED ALWAYS AS IDENTITY`). |
| `user_id` | `BIGINT` | NO | None | **FK** | Tham chiếu đến `users.id` của người sở hữu phiên đăng nhập. |
| `token_hash` | `VARCHAR(64)` | NO | None | **UK** | Chuỗi băm SHA-256 (64 ký tự hex) của Refresh Token thô. Tuyệt đối không lưu token thô. |
| `user_agent` | `VARCHAR(255)` | YES | NULL | - | Thông tin thiết bị / trình duyệt đăng nhập. |
| `ip_address` | `VARCHAR(45)` | YES | NULL | - | Địa chỉ IPv4 hoặc IPv6 của client tại thời điểm đăng nhập. |
| `expires_at` | `TIMESTAMPTZ` | NO | None | - | Thời điểm hết hạn của Refresh Token (đề xuất: 7 ngày). |
| `is_revoked` | `BOOLEAN` | NO | `FALSE` | - | Trạng thái thu hồi: `TRUE` nếu người dùng đã bấm Đăng xuất hoặc bị buộc thoát. |
| `revoked_at` | `TIMESTAMPTZ` | YES | NULL | - | Thời điểm thu hồi token. |
| `created_at` | `TIMESTAMPTZ` | NO | `NOW()` | - | Thời điểm cấp phát Refresh Token. |

*(Lưu ý: Đã loại bỏ cột `replaced_by_token_hash` theo khuyến nghị của Cursor để tránh rủi ro race conditions của cơ chế xoay vòng token trong Sprint 3).*

---

## 5. BẢNG `user_tokens` (MÃ XÁC MINH EMAIL THẬT & ĐẶT LẠI MẬT KHẨU) [NEEDS DELTA AUDIT]

- **Mục đích (tiếng Việt):** Quản lý các mã xác thực một lần (One-Time Verification Code) được gửi qua dịch vụ email thật để kích hoạt tài khoản khi đăng ký mới.
- **Mức độ ưu tiên:** **CORE** (Bắt buộc cho quy trình xác thực email).

### 5.1. Chi tiết các cột dữ liệu & Cơ chế bảo mật mã OTP

| Tên cột | Kiểu PostgreSQL | Nullable | Mặc định | Khóa / Ràng buộc | Mô tả nghiệp vụ |
| :--- | :--- | :---: | :---: | :---: | :--- |
| `id` | `BIGINT` | NO | `IDENTITY` | **PK** | Khóa chính sinh tự động (`GENERATED ALWAYS AS IDENTITY`). |
| `user_id` | `BIGINT` | NO | None | **FK** | Tham chiếu đến `users.id` của người nhận mã xác minh (CASCADE). |
| `token_type` | `VARCHAR(50)` | NO | None | **CHECK** | Loại mã: `'EMAIL_VERIFICATION'` hoặc `'PASSWORD_RESET'`. |
| `token_hash` | `VARCHAR(64)` | NO | None | **UK** | **[NEEDS DELTA AUDIT]** Chuỗi băm SHA-256 của mã xác minh 6 chữ số gửi qua email thật. Tuyệt đối không lưu mã thô trong DB. |
| `attempts_count` | `INT` | NO | `0` | **CHECK** | **[NEEDS DELTA AUDIT]** Số lần người dùng đã nhập sai mã này. Nếu `attempts_count >= 5`, mã tự động bị vô hiệu hóa để chống tấn công brute-force vét cạn 6 chữ số. |
| `expires_at` | `TIMESTAMPTZ` | NO | None | - | **[NEEDS DELTA AUDIT]** Thời điểm hết hạn của mã (Đề xuất: 10 - 15 phút đối với mã OTP email). |
| `is_used` | `BOOLEAN` | NO | `FALSE` | - | Trạng thái đã sử dụng: `TRUE` nếu mã đã được xác nhận thành công. Ngăn chặn việc tái sử dụng mã. |
| `used_at` | `TIMESTAMPTZ` | YES | NULL | - | Thời điểm mã được sử dụng thành công. |
| `created_at` | `TIMESTAMPTZ` | NO | `NOW()` | - | Thời điểm hệ thống phát hành mã và gửi email. |

### 5.2. Ràng buộc & Chỉ mục
- **Primary Key:** `pk_user_tokens` trên `(id)`.
- **Unique Constraint:** `uq_user_tokens_hash` trên `(token_hash)`.
- **Foreign Key:** `fk_user_tokens_user` trỏ đến `users(id)` ON DELETE CASCADE.
- **Check Constraints:**
  - `chk_user_tokens_type`: `token_type IN ('EMAIL_VERIFICATION', 'PASSWORD_RESET')`.
  - `chk_user_tokens_attempts`: `attempts_count >= 0`.
- **Index:** `idx_user_tokens_user_type` trên `(user_id, token_type)` phục vụ truy vấn và vô hiệu hóa các mã cũ khi người dùng bấm "Gửi lại mã xác minh".

---

## 6. CHUẨN HÓA DATA CONTRACT CHO AUTHENTICATION API (BÀN GIAO CHO SONG)

### 6.1. Contract Đăng ký (Register Request DTO)
```json
{
  "username": "phung_vo",
  "email": "phung.vo@example.com",
  "password": "SecurePassword@123",
  "fullName": "Võ Phụng",
  "phoneNumber": "0901234567"
}
```
*(Server tự động chuẩn hóa `username` và `email` về chữ thường; gán mặc định vai trò `CUSTOMER`; phát sinh mã OTP 6 số và gửi tới email thật).*

### 6.2. Contract Xác minh Email bằng Mã (Verify Email Request DTO)
```json
{
  "identifier": "phung_vo",
  "code": "849201"
}
```
*(Identifier có thể là `username` hoặc `email`; `code` là chuỗi 6 chữ số nhận từ email).*

### 6.3. Contract Gửi lại Mã xác minh (Resend Verification Code DTO)
```json
{
  "identifier": "phung_vo"
}
```
*(Vô hiệu hóa toàn bộ mã chưa dùng cũ của user, sinh mã OTP mới, gửi lại qua email, áp dụng cooldown 60 giây chống spam).*

### 6.4. Contract Đăng nhập (Login Request DTO)
```json
{
  "username": "phung_vo",
  "password": "SecurePassword@123"
}
```
*(Đăng nhập BẮT BUỘC bằng `username` và `password`. Không nhận email).*

### 6.5. Contract Phản hồi Đăng nhập Thành công (Auth Response DTO)
```json
{
  "accessToken": "eyJhbGciOi...",
  "refreshToken": "c9a7b...",
  "tokenType": "Bearer",
  "expiresIn": 900,
  "userProfile": {
    "id": 1,
    "username": "phung_vo",
    "email": "phung.vo@example.com",
    "fullName": "Võ Phụng",
    "phoneNumber": "0901234567",
    "avatarUrl": null,
    "role": "CUSTOMER",
    "isEmailVerified": true,
    "approvalStatus": "APPROVED",
    "createdAt": "2026-10-23T08:00:00Z"
  }
}
```

### 6.6. Contract Cập nhật Hồ sơ (Update Profile Request DTO)
```json
{
  "fullName": "Võ Phụng",
  "phoneNumber": "0909998888"
}
```
*(Chỉ cho phép sửa `fullName` và `phoneNumber`. Tuyệt đối cấm sửa `username`, `email`, `role`, `approvalStatus` qua API này).*

---

## 7. TỔNG KẾT
Từ điển dữ liệu Accounts đã cập nhật trọn vẹn theo quyết định chính thức: Tách biệt rõ `username` để đăng nhập và `email` để nhận mã xác minh OTP qua dịch vụ email thật.
