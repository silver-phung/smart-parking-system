# SMART PARKING – ACCOUNTS & AUTHENTICATION API HANDOVER
## TÀI LIỆU BÀN GIAO CHO FRONTEND (SONG) VÀ BÁO CÁO OWNER (PHỤNG)

**Dự án:** Smart Parking System – FPT Software OJT
**Phiên bản:** Task 02G Security Remediation
**Tác giả:** Antigravity (Backend Engineer)
**Người nhận:** Song (Frontend Lead) & Phụng (Product Owner)
**Workspace:** `E:\smartparking`
**Branch:** `feature/phung-database`

---

## 1. TỔNG QUAN HỆ THỐNG ACCOUNTS (ACCOUNTS IMPLEMENTATION OVERVIEW)

Phân hệ Accounts & Authentication đã được triển khai hoàn chỉnh theo mô hình Clean Architecture trong .NET 10, tuân thủ nghiêm ngặt 4 bảng cơ sở dữ liệu đã được Owner phê duyệt:

### 1.1. Bốn bảng Accounts và trách nhiệm
1. **`roles`**: Quản lý danh mục vai trò hệ thống. Khởi tạo 4 vai trò cố định: `ADMIN` (1), `MANAGER` (2), `STAFF` (3), `USER` (4).
2. **`users`**: Lưu trữ hồ sơ người dùng. Sử dụng khóa ngoại trực tiếp `role_id` tham chiếu `roles.id` (mô hình 1 User = 1 Role; bảng `user_roles` đã được loại bỏ hoàn toàn).
3. **`user_tokens`**: Lưu trữ mã OTP xác minh email dưới dạng mã băm bảo mật HMAC-SHA256 (`HMAC-SHA256(secret, userId:tokenType:otpCode)`). Không lưu OTP dạng thô, không đặt chỉ mục UNIQUE toàn cục trên cột hash.
4. **`refresh_tokens`**: Lưu trữ mã băm SHA-256 của Refresh Token (thời hạn 7 ngày) phục vụ duy trì phiên đăng nhập và thu hồi token (revocation).

### 1.2. Luồng nghiệp vụ cốt lõi & Quy tắc an toàn bảo mật
- **Đăng ký (Registration)**: Người dùng công khai đăng ký bằng `username` + `email` + `password` + `fullName` + `phoneNumber` kèm `recaptchaToken`. Tài khoản tự động nhận vai trò `USER`, trạng thái khởi tạo `is_email_verified = false` và `approval_status = 'PENDING'`. Hệ thống sinh mã OTP 6 số ngẫu nhiên CSPRNG và gửi đến email thật. Không cho phép tự đăng ký vai trò `ADMIN`, `MANAGER` hoặc `STAFF`.
- **Xác minh Email (Email Verification)**: Nhập email và mã OTP 6 số. Khi xác minh thành công, OTP được đánh dấu đã sử dụng (`is_used = true`), tài khoản chuyển thành `is_email_verified = true` và tự động được phê duyệt `approval_status = 'APPROVED'` đối với vai trò `USER`. Tài khoản `STAFF`, `MANAGER` và `ADMIN` không tự phê duyệt qua endpoint này mà phải do Quản trị viên chỉ định/phê duyệt.
- **Đăng nhập (Login)**: Đăng nhập BẮT BUỘC bằng **`username` + `password`** (không dùng email làm định danh đăng nhập). Backend kiểm tra mật khẩu trước khi phản hồi bất kỳ trạng thái nào để chống brute-force và user enumeration. Khóa tạm thời 15 phút nếu nhập sai quá 5 lần liên tiếp. Khi thành công, trả về JWT Access Token (15 phút) và Refresh Token (7 ngày).
- **Làm mới Token (Refresh Token)**: Gửi Refresh Token để cấp mới Access Token mà không cần người dùng nhập lại mật khẩu. Phiên bản này chưa xoay vòng refresh token.
- **Đăng xuất (Logout)**: Thu hồi Refresh Token trên server. Hoạt động bình thường ngay cả khi Access Token đã hết hạn. Lưu ý: JWT Access Token là stateless nên vẫn có hiệu lực kỹ thuật cho đến khi hết hạn (tối đa 15 phút).
- **Hồ sơ cá nhân (User Profile)**: Lấy và cập nhật hồ sơ của chính mình qua token xác thực (`/api/v1/users/me`). Chỉ cho phép cập nhật `fullName` và `phoneNumber`. Nghiêm cấm đổi username, email, role, avatarUrl hoặc approval_status qua API này.

---

## 2. API CONTRACT CHI TIẾT DÀNH CHO FRONTEND

**Base URL Local Development:**
- HTTP: `http://localhost:5295`
- HTTPS: `https://localhost:7189`

Header chung: `Content-Type: application/json`

---

### 2.1. Đăng ký tài khoản (Register)
- **Method / URL:** `POST /api/v1/auth/register`
- **Auth:** Không yêu cầu (Public).
- **Rate Limit:** Tối đa 5 requests / 5 phút trên mỗi IP (`register-rate-limit`).
- **Request Body:**
```json
{
  "username": "nguyenvana",
  "email": "nguyenvana@gmail.com",
  "password": "Password123!",
  "fullName": "Nguyễn Văn A",
  "phoneNumber": "0901234567",
  "recaptchaToken": "03AFcWeA7..."
}
```
- **Validation Rules:**
  - `username`: 3-50 ký tự chữ cái, số, dấu chấm hoặc gạch dưới `^[a-zA-Z0-9_.]{3,50}$`. Backend tự động chuẩn hóa thành chữ thường không dấu.
  - `email`: Định dạng email hợp lệ, tối đa 255 ký tự.
  - `password`: Tối thiểu 8 ký tự, tối đa 100 ký tự.
  - `fullName`: Bắt buộc, tối đa 100 ký tự.
  - `phoneNumber`: Tùy chọn, tối đa 20 ký tự, định dạng số điện thoại.
  - `recaptchaToken`: Chuỗi token do Google reCAPTCHA client trả về (bắt buộc khi reCAPTCHA bật).
- **Response Thành công (`201 Created`):**
```json
{
  "message": "Đăng ký thành công. Vui lòng kiểm tra email để nhận mã xác minh OTP.",
  "data": {
    "id": 1,
    "username": "nguyenvana",
    "email": "nguyenvana@gmail.com",
    "fullName": "Nguyễn Văn A",
    "phoneNumber": "0901234567",
    "avatarUrl": null,
    "role": "USER",
    "isEmailVerified": false,
    "approvalStatus": "PENDING",
    "createdAt": "2026-10-23T04:30:00Z"
  }
}
```
- **Response Lỗi:**
  - `400 Bad Request`: Thiếu dữ liệu, reCAPTCHA không hợp lệ (`{"message": "Xác thực reCAPTCHA không thành công. Vui lòng thử lại."}`).
  - `409 Conflict`: `{"message": "Tên đăng nhập hoặc Email đã tồn tại trong hệ thống."}` (thông báo đồng nhất, không phân biệt username hay email bị trùng).
  - `429 Too Many Requests`: `{"message": "Bạn đã gửi quá nhiều yêu cầu. Vui lòng thử lại sau ít phút."}`.

---

### 2.2. Xác minh Email bằng OTP (Verify Email)
- **Method / URL:** `POST /api/v1/auth/verify-email`
- **Auth:** Không yêu cầu.
- **Rate Limit:** Tối đa 10 requests / 1 phút trên mỗi IP (`verify-rate-limit`).
- **Request Body:**
```json
{
  "email": "nguyenvana@gmail.com",
  "code": "123456"
}
```
- **Validation Rules:**
  - `email`: Bắt buộc.
  - `code`: Chuỗi đúng 6 chữ số `^\d{6}$`.
- **Response Thành công (`200 OK`):**
```json
{
  "message": "Xác minh email thành công. Tài khoản của bạn đã được kích hoạt."
}
```
- **Response Lỗi:**
  - `400 Bad Request`: `{"message": "Mã xác minh không chính xác hoặc đã hết hạn."}` (hệ thống không phản hồi số lần thử còn lại để tránh brute-force feedback).
  - `429 Too Many Requests`: Vượt quá tần suất gọi API.

---

### 2.3. Gửi lại mã OTP (Resend Verification)
- **Method / URL:** `POST /api/v1/auth/resend-verification`
- **Auth:** Không yêu cầu.
- **Rate Limit:** Tối đa 3 requests / 5 phút trên mỗi IP (`resend-rate-limit`), kèm **cooldown 60 giây** cho mỗi tài khoản email.
- **Request Body:**
```json
{
  "email": "nguyenvana@gmail.com"
}
```
- **Response Thành công (`200 OK`):**
```json
{
  "message": "Mã xác minh mới đã được gửi tới email của bạn."
}
```
- **Response Lỗi Cooldown (`429 Too Many Requests`):**
```json
{
  "message": "Vui lòng chờ 45 giây trước khi yêu cầu gửi lại mã xác minh."
}
```

---

### 2.4. Đăng nhập (Login)
- **Method / URL:** `POST /api/v1/auth/login`
- **Auth:** Không yêu cầu.
- **Rate Limit:** Tối đa 10 requests / 1 phút trên mỗi IP (`login-rate-limit`).
- **Request Body:**
```json
{
  "username": "nguyenvana",
  "password": "Password123!"
}
```
- **Response Thành công (`200 OK`):**
```json
{
  "message": "Đăng nhập thành công.",
  "data": {
    "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
    "refreshToken": "4a7f9b8c...",
    "tokenType": "Bearer",
    "expiresIn": 900,
    "userProfile": {
      "id": 1,
      "username": "nguyenvana",
      "email": "nguyenvana@gmail.com",
      "fullName": "Nguyễn Văn A",
      "phoneNumber": "0901234567",
      "avatarUrl": null,
      "role": "USER",
      "isEmailVerified": true,
      "approvalStatus": "APPROVED",
      "createdAt": "2026-10-23T04:30:00Z"
    }
  }
}
```
- **Response Lỗi:**
  - `401 Unauthorized`: Sai tên đăng nhập hoặc mật khẩu (`{"message": "Tên đăng nhập hoặc mật khẩu không chính xác."}`).
  - `403 Forbidden`: Email chưa xác minh (`{"message": "Tài khoản chưa hoàn tất xác minh email. Vui lòng xác minh mã OTP được gửi qua email trước khi đăng nhập."}`), hoặc tài khoản bị vô hiệu hóa / đang chờ phê duyệt.
  - `423 Locked`: Nhập sai mật khẩu liên tiếp 5 lần (`{"message": "Tài khoản tạm thời bị khóa do nhập sai mật khẩu nhiều lần. Vui lòng thử lại sau 15 phút."}`).
  - `429 Too Many Requests`: Vượt tần suất gọi đăng nhập trên IP.

---

### 2.5. Làm mới Access Token (Refresh Token)
- **Method / URL:** `POST /api/v1/auth/refresh`
- **Auth:** Không bắt buộc Access Token còn hạn.
- **Request Body:**
```json
{
  "refreshToken": "4a7f9b8c..."
}
```
- **Response Thành công (`200 OK`):**
```json
{
  "message": "Làm mới Access Token thành công.",
  "data": {
    "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
    "refreshToken": "4a7f9b8c...",
    "tokenType": "Bearer",
    "expiresIn": 900,
    "userProfile": { ... }
  }
}
```
- **Response Lỗi:**
  - `401 Unauthorized`: Refresh token hết hạn, bị thu hồi, hoặc không tồn tại.

---

### 2.6. Đăng xuất (Logout)
- **Method / URL:** `POST /api/v1/auth/logout`
- **Auth:** Không bắt buộc JWT còn hạn.
- **Request Body:**
```json
{
  "refreshToken": "4a7f9b8c..."
}
```
- **Response Thành công (`200 OK`):**
```json
{
  "message": "Đăng xuất thành công. Phiên làm việc đã kết thúc."
}
```

---

### 2.7. Xem hồ sơ người dùng hiện tại (Get Current User Profile)
- **Method / URL:** `GET /api/v1/users/me`
- **Auth:** Bắt buộc JWT Bearer: `Authorization: Bearer {accessToken}`
- **Response Thành công (`200 OK`):**
```json
{
  "message": "Lấy thông tin người dùng thành công.",
  "data": {
    "id": 1,
    "username": "nguyenvana",
    "email": "nguyenvana@gmail.com",
    "fullName": "Nguyễn Văn A",
    "phoneNumber": "0901234567",
    "avatarUrl": null,
    "role": "USER",
    "isEmailVerified": true,
    "approvalStatus": "APPROVED",
    "createdAt": "2026-10-23T04:30:00Z"
  }
}
```
- **Response Lỗi:** `401 Unauthorized`.

---

### 2.8. Cập nhật hồ sơ người dùng hiện tại (Update Current User Profile)
- **Method / URL:** `PUT /api/v1/users/me`
- **Auth:** Bắt buộc JWT Bearer: `Authorization: Bearer {accessToken}`
- **Request Body:**
```json
{
  "fullName": "Nguyễn Văn A (Cập nhật)",
  "phoneNumber": "0988776655"
}
```
- **Lưu ý:** Chỉ gửi `fullName` và `phoneNumber`. Không gửi `username`, `email`, `role`, hay `approvalStatus`.
- **Response Thành công (`200 OK`):**
```json
{
  "message": "Cập nhật thông tin hồ sơ thành công.",
  "data": {
    "id": 1,
    "username": "nguyenvana",
    "email": "nguyenvana@gmail.com",
    "fullName": "Nguyễn Văn A (Cập nhật)",
    "phoneNumber": "0988776655",
    "avatarUrl": null,
    "role": "USER",
    "isEmailVerified": true,
    "approvalStatus": "APPROVED",
    "createdAt": "2026-10-23T04:30:00Z"
  }
}
```

---

## 3. HƯỚNG DẪN TÍCH HỢP CHO SONG (FRONTEND INTEGRATION GUIDE)

1. **Kết nối hệ thống:**
   - Frontend **tuyệt đối không kết nối trực tiếp vào PostgreSQL Neon**. Toàn bộ tương tác dữ liệu phải thông qua các REST API ở trên.
2. **Form Đăng ký (Register):**
   - Tích hợp widget Google reCAPTCHA trên trang Đăng ký. Lấy `recaptchaToken` gửi kèm payload đăng ký.
   - Nhắc nhở người dùng: Username là tên đăng nhập duy nhất (hệ thống tự chuyển thành chữ thường); Email là nơi nhận mã xác thực OTP.
3. **Màn hình Nhập OTP (Verify Email):**
   - Sau khi đăng ký thành công, chuyển hướng người dùng sang màn hình nhập mã OTP 6 số.
   - Hiển thị đồng hồ đếm ngược 60 giây cho nút "Gửi lại mã" (Resend OTP). Khi đồng hồ về 0, người dùng mới có thể bấm gửi lại.
4. **Form Đăng nhập (Login):**
   - Trường định danh đăng nhập là **Tên đăng nhập (Username)**, không phải Email.
   - Không yêu cầu reCAPTCHA hay OTP ở màn hình Đăng nhập bình thường.
5. **Lưu trữ Token:**
   - Lưu `accessToken` trong bộ nhớ ứng dụng (Memory) hoặc secure storage.
   - Lưu `refreshToken` an toàn.
   - Khi API trả về `401 Unauthorized` trên các endpoint nghiệp vụ, gọi `POST /api/v1/auth/refresh` bằng `refreshToken`. Nếu thành công, cập nhật `accessToken` và retry request trước đó. Nếu refresh thất bại, xóa token và điều hướng về trang Login.
6. **Xử lý Đăng xuất (Logout):**
   - Khi bấm Logout, gọi `POST /api/v1/auth/logout` với `refreshToken`. Sau đó xóa toàn bộ token ở client.

---

## 4. HƯỚNG DẪN CẤU HÌNH BIẾN MÔI TRƯỜNG DÀNH CHO OWNER (PHỤNG)

Để ứng dụng vận hành đầy đủ trên môi trường Development / Production, Owner cấu hình các biến môi trường hoặc User Secrets sau (Backend KHÔNG lưu secret trong Git):

| Mục | Tên cấu hình trong `appsettings.json` | Biến môi trường tương đương | Ghi chú |
| :--- | :--- | :--- | :--- |
| **Database** | `ConnectionStrings:DefaultConnection` | `ConnectionStrings__DefaultConnection` | Chuỗi kết nối PostgreSQL Neon DEV |
| **JWT Key** | `Jwt:SecretKey` | `Jwt__SecretKey` | Chuỗi ngẫu nhiên bí mật tối thiểu 32 bytes (256 bits) |
| **OTP Key** | `Security:OtpSecretKey` | `Security__OtpSecretKey` | Khóa bí mật HMAC cho mã OTP (tối thiểu 32 bytes) |
| **reCAPTCHA** | `Recaptcha:SecretKey` | `Recaptcha__SecretKey` | Secret key từ Google reCAPTCHA Admin Console |
| **Email SMTP**| `Email:UserName` / `Email:Password` | `Email__UserName` / `Email__Password` | Tài khoản và App Password Gmail/SendGrid |
| **Admin Seed**| `AdminBootstrap:Username` / `Password` | `AdminBootstrap__Username` / `Password` | Thông tin tài khoản Admin đầu tiên khi khởi tạo |

---
**Trạng thái bàn giao:** Hoàn tất đầy đủ Code, Tests và Tài liệu sau khi khắc phục triệt để các phát hiện kiểm toán bảo mật.
