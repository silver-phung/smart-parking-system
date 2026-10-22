# SMART PARKING SYSTEM – ACCOUNTS DESIGN DECISIONS
## BÁO CÁO PHÂN TÍCH QUYẾT ĐỊNH THIẾT KẾ PHÂN HỆ TÀI KHOẢN & XÁC THỰC

- **Dự án:** Smart Parking System (FPT Software OJT)
- **Tài liệu:** Task 02B & Quyết định Nghiệp vụ Cập nhật – Phân hệ Accounts & Authentication
- **Chịu trách nhiệm kiến trúc CSDL:** Phụng (Backend / Database Lead)
- **Hệ quản trị CSDL:** PostgreSQL trên Neon Cloud
- **Mục tiêu:** Phân định minh bạch giữa quyết định đã có căn cứ (CONFIRMED), phương án đề xuất (PROPOSED), các điểm chờ Owner phê duyệt và danh mục các bài toán kỹ thuật mới đánh dấu **[NEEDS DELTA AUDIT]** bàn giao cho Agent Cursor kiểm toán bổ sung.

---

## 1. QUYẾT ĐỊNH ĐÃ CÓ CĂN CỨ VỮNG CHẮC (CONFIRMED DECISIONS)

Các quyết định sau đây đã được xác nhận chính thức từ Owner Phụng và nhóm dự án, thay thế các giả định cũ về đăng nhập bằng email:

1. **Cơ chế Đăng ký (Registration):**
   - Người dùng đăng ký bằng **`username` + `email` + `password`** (+ thông tin hồ sơ `full_name`, `phone_number`).
   - `username` là tên đăng nhập độc lập, không phải địa chỉ email.
   - `email` bắt buộc phải là **email thật** và phải được xác minh bằng mã OTP gửi về hộp thư.
   - Tài khoản chưa hoàn tất xác minh email (`is_email_verified = FALSE`) **tuyệt đối không được phép đăng nhập**.
2. **Cơ chế Đăng nhập (Login):**
   - Đăng nhập duy nhất bằng **`username` + `password`**.
   - **Không sử dụng Email làm định danh đăng nhập** trong phiên bản này.
3. **Phạm vi Google Login:**
   - Xếp loại **OPTIONAL / FUTURE**.
   - Chưa triển khai trong Sprint 3.
   - Không để tính năng Google Login ảnh hưởng đến tiến độ phân hệ Accounts hiện tại.
   - **Tuyệt đối không tự ý thêm bảng hoặc cột Google OAuth** (`google_id`, `oauth_provider`...) vào cơ sở dữ liệu.
4. **Phân quyền truy cập dựa trên vai trò (RBAC):**
   - Tối thiểu 3 vai trò độc lập:
     - `ADMIN`: Toàn quyền quản trị hệ thống, phê duyệt nhân viên, xem audit log.
     - `OPERATOR`: Nhân viên vận hành bãi xe, trực cổng, xử lý đỗ sai vị trí.
     - `CUSTOMER`: Khách hàng gửi xe, quản lý xe cá nhân, thanh toán phí.
   - Frontend khi đăng ký công khai không được phép tự gán quyền `ADMIN` hoặc `OPERATOR` (luôn mặc định gán `CUSTOMER`).
5. **Duy trì trường trạng thái phê duyệt (Account Approval):**
   - Tiếp tục duy trì các cột: `approval_status`, `approved_by`, `approved_at` theo quyết định nghiệp vụ đã chốt. Tuyệt đối không tự ý xóa bỏ.
6. **Bảo mật mật khẩu bắt buộc:**
   - Tuyệt đối không lưu mật khẩu thô (Plaintext). Mật khẩu phải được băm an toàn một chiều (One-Way Hashing) kèm Salt ngẫu nhiên và lưu tại cột `password_hash` (sử dụng BCrypt WorkFactor 12 hoặc PBKDF2 HMAC-SHA512).
7. **Không để lộ trường an ninh nội bộ ra API:**
   - Tuyệt đối cấm trả `password_hash`, `token_hash`, `failed_login_attempts`, `lockout_end_at` trong bất kỳ API Response nào của hệ thống.

---

## 2. CÁC PHƯƠNG ÁN THIẾT KẾ ĐỀ XUẤT (PROPOSED SOLUTIONS)

### 2.1. Lựa chọn Chiến lược Khóa chính (Primary Key Strategy) cho Phân hệ Accounts
- **Phân tích so sánh:**
  | Tiêu chí | Phương án A: `BIGINT IDENTITY` (Khuyến nghị) | Phương án B: `UUID v7 / UUID v4` |
  | :--- | :--- | :--- |
  | **Hiệu năng Index** | Rất cao, B-Tree index nhỏ gọn (8 bytes). | Thấp hơn, dung lượng index lớn hơn (16 bytes). |
  | **Khả năng giải thích** | Rất trong sáng, dễ giải thích với Mentor FPT. | Phức tạp hơn, giải thích sinh ngẫu nhiên phân tán. |
  | **Tiện ích Debug / Test** | Khi test qua Swagger/Postman, gõ ID `1, 2, 3` rất nhanh và trực quan. | Chuỗi 36 ký tự khó nhớ, dễ nhầm lẫn khi test thủ công. |
  | **Bảo mật đối ngoại** | Có thể bị đoán thứ tự bản ghi (đối với API công khai). | Ẩn hoàn toàn số lượng người dùng. |
- **Đề xuất của Antigravity:**
  - Áp dụng **Phương án A (`BIGINT IDENTITY`)** cho `users`, `refresh_tokens`, `user_tokens`; áp dụng `INT IDENTITY` cho `roles`.
  - Giữ kiến trúc chuẩn, đơn giản, hiệu năng cao trên PostgreSQL Neon.

### 2.2. Cơ chế Lưu trữ Token An ninh cao (Hash Token Storage)
- **Thực trạng rủi ro:** Nếu lưu chuỗi Refresh Token hoặc Email Verification Code thô vào CSDL, khi Database bị lộ (SQL Injection, rò rỉ backup), kẻ tấn công có thể chiếm đoạt tài khoản hoặc phiên đăng nhập.
- **Giải pháp đề xuất:**
  - Bảng `refresh_tokens` và `user_tokens` **chỉ lưu giá trị băm `token_hash = SHA256(raw_token)` hoặc `SHA256(otp_code)`**.
  - Client giữ mã xác minh OTP hoặc refresh token thô. Khi Client gửi lên, Backend băm SHA-256 và so sánh với CSDL.
  - Ngay cả khi hacker có được toàn bộ Database, giá trị băm SHA-256 là một chiều không thể đảo ngược để lấy mã OTP hoặc token gốc.

### 2.3. Bảng `user_tokens` đa năng & Cơ chế Xác minh Email an toàn
- **Đề xuất cấu trúc bảng `user_tokens`:**
  - `token_type`: Phân loại token (`EMAIL_VERIFICATION`, `PASSWORD_RESET`).
  - `token_hash`: Lưu chuỗi băm `SHA256(otp_code)`.
  - `attempts_count`: Đếm số lần nhập sai mã OTP (giới hạn tối đa 5 lần).
  - `expires_at`: Hạn hiệu lực của mã (15 phút).
  - `is_used`: Đánh dấu mã đã sử dụng, chống tấn công Replay Attack.
- **Quy trình Xác minh Email bằng mã gửi qua email thật:**
  1. Backend sinh mã ngẫu nhiên an toàn bảo mật (CSPRNG) gồm 6 chữ số: `100000` - `999999`.
  2. Băm mã OTP bằng `SHA256(otp_code)` và lưu vào `user_tokens`.
  3. Gửi mã qua dịch vụ gửi email thật (SMTP / SendGrid / AWS SES).
  4. Người dùng nhập mã xác minh tại giao diện Web/App.
  5. Backend kiểm tra: còn hạn (`expires_at > NOW()`), chưa dùng (`is_used = FALSE`), và `attempts_count < 5`.
  6. Nếu nhập sai: Tăng `attempts_count = attempts_count + 1`. Nếu đạt 5 lần, vô hiệu hóa mã (`is_used = TRUE`).
  7. Nếu hợp lệ: Cập nhật `is_used = TRUE` nguyên tử, đồng thời set `users.is_email_verified = TRUE`.
  8. Gửi lại mã (Resend Code): Vô hiệu hóa toàn bộ mã cũ đang active của user, áp dụng Cooldown 60 giây giữa các lần yêu cầu.
  9. Không hardcode thông tin kết nối email vào mã nguồn; nạp qua Environment Variables. Không dùng email giả hay chỉ log console.

### 2.4. Quy tắc Chuẩn hóa Username và Email (Normalization & Validation Rules)
- **Quy tắc cho `username`:**
  - Định dạng: Chỉ cho phép chữ cái thường không dấu, số, dấu gạch dưới và dấu chấm: `^[a-z0-9_.]{3,50}$`.
  - Độ dài: Từ 3 đến 50 ký tự.
  - Chuẩn hóa: `username = username.Trim().ToLowerInvariant()` trước khi kiểm tra trùng lặp và lưu trữ.
  - Ràng buộc: `users_username_key UNIQUE (username)`.
- **Quy tắc cho `email`:**
  - Định dạng: Chuẩn RFC 5322 regex.
  - Độ dài: Tối đa 255 ký tự.
  - Chuẩn hóa: `email = email.Trim().ToLowerInvariant()` trước khi kiểm tra trùng lặp và lưu trữ.
  - Ràng buộc: `users_email_key UNIQUE (email)`.

### 2.5. Cơ chế Phòng thủ Chống Tấn công Dò Mật khẩu & Timing Attack (Login Security)
- **Chống Brute-force Lockout:** Tích hợp `failed_login_attempts` (`INT`) và `lockout_end_at` (`TIMESTAMPTZ`) vào bảng `users`. Khóa tài khoản tạm thời 15 phút sau 5 lần nhập sai mật khẩu liên tiếp.
- **Chống Timing Attack (User Enumeration):**
  - Khi người dùng gửi `username` không tồn tại trong hệ thống, Backend vẫn tiến hành tính toán một phép verify mật khẩu giả lập (Dummy BCrypt Verify) với cùng WorkFactor để thời gian phản hồi đồng nhất, ngăn chặn kẻ tấn công dò tìm danh sách `username` hợp lệ qua độ trễ mạng.
- **Thứ tự kiểm tra an ninh khi đăng nhập:**
  1. Tra cứu user theo `LOWER(username)`.
  2. Kiểm tra khóa tài khoản (`lockout_end_at > NOW()`).
  3. Xác thực mật khẩu (`VerifyPassword`).
  4. Kiểm tra trạng thái xác minh email: Nếu `is_email_verified == FALSE` -> Trả về `403 Forbidden` kèm thông báo *"Tài khoản chưa hoàn tất xác minh email"*.
  5. Kiểm tra trạng thái kích hoạt `is_active` và phê duyệt `approval_status`.

### 2.6. Phân định Quyền Chỉnh sửa Hồ sơ Người dùng (User Profile Data Boundary)
- **Người dùng được phép tự sửa:** `full_name`, `phone_number`, `avatar_url`.
- **Bất biến đối với người dùng (Immutable):** `username` (định danh đăng nhập duy nhất không được đổi) và `email` (chỉ đổi qua quy trình Re-verification riêng nếu phát triển sau này).
- **Chỉ Admin / Hệ thống được sửa:** `roles`, `is_active`, `is_email_verified`, `approval_status`, `approved_by`, `approved_at`.
- **Trường an ninh nội bộ cấm trả ra API:** `password_hash`, `token_hash`, `failed_login_attempts`, `lockout_end_at`.

---

## 3. CÁC ĐIỂM CHỜ OWNER PHỤNG PHÊ DUYỆT (NEEDS OWNER APPROVAL)

Trước khi tiến hành các bước tiếp theo, các hạng mục sau cần Owner Phụng chính thức phê duyệt:

1. **Phê duyệt bảng `refresh_tokens`:**
   - Đồng ý triển khai bảng `refresh_tokens` để quản lý phiên đăng nhập và hỗ trợ cơ chế cấp mới Access Token / Đăng xuất (Logout) an toàn.
2. **Phê duyệt bảng `user_tokens`:**
   - Đồng ý triển khai bảng `user_tokens` lưu mã băm OTP 6 số xác minh email và reset password.
3. **Phê duyệt các tham số an ninh đề xuất (PROPOSED Parameters):**
   - Độ dài mã xác minh email: **6 chữ số** (OTP).
   - Thời hạn hiệu lực của mã OTP xác minh email: **15 phút**.
   - Số lần nhập sai mã OTP tối đa trước khi hủy mã: **5 lần**.
   - Thời gian giãn cách tối thiểu giữa 2 lần gửi lại mã (Resend Cooldown): **60 giây**.
   - Số lần nhập sai mật khẩu tối đa trước khi khóa tài khoản: **5 lần**.
   - Thời gian khóa tạm thời tài khoản: **15 phút**.
   - Thời hạn hiệu lực của Refresh Token: **7 ngày**.
   - Thời hạn hiệu lực của Access Token JWT: **15 phút**.
4. **Phê duyệt cơ chế Phê duyệt tài khoản (Approval Policy):**
   - Quy tắc tự động phê duyệt (`approval_status = 'APPROVED'`) cho vai trò `CUSTOMER` ngay sau khi xác minh email thành công: **Chờ Owner phê duyệt**.
   - Quy tắc tài khoản `OPERATOR` / `ADMIN`: Tiếp tục giữ trạng thái `PENDING` chờ Quản trị viên duyệt thủ công.

---

## 4. TUÂN THỦ NGHIÊM NGẶT QUY TRÌNH DỰ ÁN

Quy trình kỹ thuật được xác lập là bất biến:

$$\text{Task 02A} \longrightarrow \text{Task 02B} \longrightarrow \text{Task 02C (Cursor Audit)} \longrightarrow \text{Task 02D (Pre-Approval)} \longrightarrow \text{Tasks tiếp theo}$$

- **Trạng thái Task 02C:** Cursor đã kiểm toán và kết luận **PASS WITH CHANGES** trên thiết kế Task 02B ban đầu.
- **Nhiệm vụ Task 02D:** Cập nhật yêu cầu nghiệp vụ chính thức từ Owner Phụng (Username Login, Real Email OTP Verification, loại bỏ Google OAuth).
- **Yêu cầu kiểm soát:** Mọi nội dung thay đổi mới phải được gắn nhãn **[NEEDS DELTA AUDIT]** để Agent Cursor kiểm toán bổ sung trước khi triển khai mã nguồn.
- **Kỷ luật tuyệt đối:**
  - Không tự ý tạo C# Entity, DbContext, Migration hay API Controller.
  - Không kết nối Neon Cloud.
  - Không tự tiện commit/push hay đổi nhánh git.

---

## 5. BÀI TOÁN KIẾN TRÚC BÀN GIAO CHO AGENT CURSOR KIỂM TOÁN

### 5.1. Báo cáo Tóm tắt Kết quả Task 02C (Cursor Audit Đã Hoàn Thành)
Agent Cursor đã thực hiện kiểm toán Task 02C với các ghi nhận:
- **C1 - Refresh Token:** Chốt sử dụng Refresh Token không xoay vòng (No-rotation) cho Sprint 3 để tránh race-conditions phức tạp khi chưa có Redis; thu hồi token khi đăng xuất.
- **C2 - Phân quyền RBAC:** Thống nhất giữ bảng `user_roles` (N-N) để đảm bảo chuẩn mực mở rộng và lưu vết kiểm toán `assigned_by`, `assigned_at`.
- **C3 - Đăng xuất:** Chấp nhận TTL Access Token ngắn (15 phút) hết hạn tự nhiên mà không cần blacklist phức tạp.
- **C4 - Hash Mật khẩu:** Khuyến nghị BCrypt (WorkFactor = 12) hoặc PBKDF2 HMAC-SHA512.

---

### 5.2. DANH MỤC KIỂM TOÁN BỔ SUNG: [NEEDS DELTA AUDIT] (DÀNH CHO AGENT CURSOR)

Dưới đây là các hạng mục thay đổi thiết kế theo quyết định chính thức của Owner, yêu cầu Cursor thực hiện kiểm toán bổ sung:

1. **[NEEDS DELTA AUDIT] Schema & Constraints của `username`:**
   - *Hạng mục:* Thêm cột `username VARCHAR(50) NOT NULL` vào bảng `users` kèm ràng buộc `CONSTRAINT users_username_key UNIQUE (username)`.
   - *Cursor Audit Scope:* Đánh giá độ dài (50 ký tự), quy tắc regex `^[a-z0-9_.]{3,50}$`, và chi phí index B-Tree trên Neon PostgreSQL.
2. **[NEEDS DELTA AUDIT] Cơ chế Xác minh Email bằng mã OTP 6 số qua Dịch vụ Thật:**
   - *Hạng mục:* Chuyển từ token đường link sang mã 6 số OTP; lưu trữ `token_hash = SHA256(otp_code)` trong `user_tokens`; thêm cột `attempts_count INT NOT NULL DEFAULT 0`.
   - *Cursor Audit Scope:*
     - Kiểm toán cơ chế chống brute-force mã OTP 6 chữ số: `attempts_count >= 5` thì hủy mã.
     - Kiểm toán tính toàn vẹn và concurrency (race condition): Sử dụng câu lệnh UPDATE nguyên tử `UPDATE user_tokens SET is_used = TRUE WHERE id = :id AND is_used = FALSE` để ngăn chặn việc nhập mã đồng thời 2 nơi.
     - Kiểm toán cơ chế Resend: Vô hiệu hóa toàn bộ mã cũ và áp dụng Cooldown 60s.
3. **[NEEDS DELTA AUDIT] Luồng Đăng nhập duy nhất bằng Username + Password:**
   - *Hạng mục:* API Login chỉ nhận `username` và `password`. Kiểm tra `is_email_verified == TRUE` trước khi cấp phát JWT Token.
   - *Cursor Audit Scope:*
     - Kiểm toán kỹ thuật chống Timing Attack (User Enumeration) khi truy vấn theo `username`.
     - Kiểm toán thứ tự xác thực (Xác thực mật khẩu -> Kiểm tra Email Verified -> Kiểm tra Account Approval).
4. **[NEEDS DELTA AUDIT] Loại bỏ hoàn toàn Google OAuth:**
   - *Hạng mục:* Chuyển Google Login sang diện FUTURE/OPTIONAL; rà soát CSDL không phát sinh cột hoặc bảng liên quan đến bên thứ ba.
   - *Cursor Audit Scope:* Xác nhận tính tinh gọn của thiết kế CSDL Sprint 3.

---

## 6. KẾ HOẠCH BÀN GIAO CHO FRONTEND (SONG) VÀO NGÀY 24/10/2026

Dựa trên toàn bộ tài liệu đã cập nhật và chuẩn hóa, Owner Phụng có đầy đủ Data Contracts chính xác để bàn giao cho Song triển khai giao diện:

- **1. Contract Đăng ký (Register):**
  - Endpoint: `POST /api/v1/auth/register`
  - Request: `RegisterRequestDto(username, email, password, fullName, phoneNumber)`
  - Response: `201 Created` kèm thông báo yêu cầu kiểm tra email để lấy mã xác minh.
- **2. Contract Xác minh Email (Verify Email):**
  - Endpoint: `POST /api/v1/auth/verify-email`
  - Request: `VerifyEmailRequestDto(email, code)` (mã 6 chữ số).
  - Response: `200 OK` xác nhận hoàn tất xác minh email.
- **3. Contract Gửi lại mã Xác minh (Resend Verification Code):**
  - Endpoint: `POST /api/v1/auth/resend-verification`
  - Request: `ResendVerificationCodeRequestDto(email)`
  - Response: `200 OK` (áp dụng cooldown 60s ở giao diện).
- **4. Contract Đăng nhập (Login):**
  - Endpoint: `POST /api/v1/auth/login`
  - Request: `LoginRequestDto(username, password)`
  - Response: `200 OK` trả về `AuthResponseDto(accessToken, refreshToken, userProfile)`.
- **5. Contract Cấp lại Token (Refresh Token):**
  - Endpoint: `POST /api/v1/auth/refresh-token`
  - Request: `RefreshTokenRequestDto(refreshToken)`
  - Response: `200 OK` trả về `AuthResponseDto(accessToken, refreshToken, userProfile)`.
- **6. Contract Đăng xuất (Logout):**
  - Endpoint: `POST /api/v1/auth/logout`
  - Header: `Authorization: Bearer <accessToken>` + Body: `LogoutRequestDto(refreshToken)`
  - Response: `200 OK` thu hồi Refresh Token.
- **7. Contract Hồ sơ Cá nhân (Get Profile):**
  - Endpoint: `GET /api/v1/users/me`
  - Response: `UserProfileDto(id, username, email, fullName, phoneNumber, avatarUrl, roles, isEmailVerified, approvalStatus, createdAt)`.
- **8. Contract Cập nhật Hồ sơ (Update Profile):**
  - Endpoint: `PUT /api/v1/users/me`
  - Request: `UpdateProfileRequestDto(fullName, phoneNumber, avatarUrl)`.
  - Response: `UserProfileDto(...)`.
- **9. Contract Phê duyệt Tài khoản (Account Approval - Dành cho Quản trị viên):**
  - Endpoint: `POST /api/v1/users/{id}/approve`
  - Request: `AccountApprovalDto(action: "APPROVE" | "REJECT", reason)`.
