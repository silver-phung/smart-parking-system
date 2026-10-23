# SMART PARKING SYSTEM – ACCOUNTS AUTHENTICATION FLOWS
## ĐẶC TẢ CHI TIẾT CÁC LUỒNG NGHIỆP VỤ TÀI KHOẢN & XÁC THỰC

- **Dự án:** Smart Parking System (FPT Software OJT)
- **Tài liệu:** Task 02D – Đặc tả luồng dữ liệu Accounts & Auth (Bản Thiết kế Chính thức)
- **Chịu trách nhiệm kiến trúc CSDL:** Phụng (Backend / Database Lead)
- **Hệ quản trị CSDL:** PostgreSQL trên Neon Cloud
- **Trạng thái phê duyệt:** **APPROVED BY OWNER** (Đã chính thức phê duyệt toàn bộ quyết định thiết kế).

---

## 1. CẤU HÌNH BẢO VỆ RATE LIMITING BẮT BUỘC (APPROVED BY OWNER)

Để bảo vệ hệ thống trước tấn công DoS, vét cạn mật khẩu (Brute-force) và spam email, hệ thống áp dụng Rate Limiting tại tầng ASP.NET Core Middleware theo địa chỉ IP:

| Endpoint | Method | Giới hạn tối đa (Rate Limit) | Hành vi khi vượt ngưỡng |
| :--- | :---: | :--- | :--- |
| `/api/v1/auth/register` | `POST` | **5 requests / phút / IP** | Trả về `429 Too Many Requests` |
| `/api/v1/auth/login` | `POST` | **10 requests / phút / IP** | Trả về `429 Too Many Requests` |
| `/api/v1/auth/verify-email` | `POST` | **10 requests / phút / IP** | Trả về `429 Too Many Requests` |
| `/api/v1/auth/resend-verification`| `POST` | **3 requests / 10 phút / IP** | Trả về `429 Too Many Requests` (kèm Cooldown tối thiểu 60s) |

---

## 2. LUỒNG 1: ĐĂNG KÝ TÀI KHOẢN & VERIFY RECAPTCHA SERVER-SIDE

### 2.1. Mục tiêu & Nguyên tắc nghiệp vụ
- Người dùng đăng ký bằng **Username + Email + Password** (kèm `fullName`, `phoneNumber`, `recaptchaToken`).
- **reCAPTCHA Server-Side Verification:** Token `recaptchaToken` được Backend xác minh trực tiếp với Google; **không lưu token reCAPTCHA vào cơ sở dữ liệu**.
- `username` là tên đăng nhập độc lập duy nhất; `email` là email thật nhận mã xác minh OTP 6 chữ số.
- Không nhận trường role từ client; hệ thống tự động gán khóa ngoại `users.role_id` trỏ đến vai trò `CUSTOMER`.

### 2.2. Sơ đồ tuần tự (Sequence Diagram)

```mermaid
sequenceDiagram
    autonumber
    actor Client as Người dùng (Song - Frontend)
    participant API as Auth API (Backend)
    participant reCAPTCHA as Google reCAPTCHA Service
    participant DB as PostgreSQL (Neon)
    participant Mail as Real Email Service (SMTP/MailKit)

    Client->>API: POST /api/v1/auth/register (username, email, password, fullName, phoneNumber, recaptchaToken)
    
    Note over API: 1. Xác thực reCAPTCHA với Server Google:
    API->>reCAPTCHA: POST verify (secretKey, recaptchaToken)
    reCAPTCHA-->>API: Response { success: true, score: 0.9 }
    alt reCAPTCHA Thất bại (success == false)
        API-->>Client: 400 Bad Request ("Xác thực reCAPTCHA không thành công. Vui lòng thử lại.")
    else reCAPTCHA Hợp lệ
        Note over API: 2. Chuẩn hóa lowercase: username = username.Trim().ToLowerInvariant()<br/>email = email.Trim().ToLowerInvariant()
        API->>API: Hash password: passwordHash = PasswordHasher.HashPassword(password)
        API->>API: Sinh mã OTP 6 số: otpCode = CSPRNG(100000..999999) (ví dụ: "849201")
        API->>API: Tính HMAC-SHA256: tokenHash = HMAC_SHA256(secretKey, $"{userId}:EMAIL_VERIFICATION:{otpCode}")
        
        API->>DB: BEGIN TRANSACTION
        API->>DB: INSERT INTO users (username, email, password_hash, role_id, full_name, phone_number, is_active, is_email_verified, approval_status)<br/>VALUES (:username, :email, :passwordHash, (SELECT id FROM roles WHERE code = 'CUSTOMER'), :fullName, :phoneNumber, TRUE, FALSE, 'PENDING')<br/>RETURNING id;
        alt Trùng username hoặc email (PostgreSQL Unique Violation 23505)
            DB-->>API: Ném ngoại lệ Unique Violation 23505
            API->>DB: ROLLBACK
            API-->>Client: 409 Conflict ("Tên đăng nhập hoặc Email đã tồn tại trong hệ thống")
        else Thành công
            API->>DB: INSERT INTO user_tokens (user_id, token_type, token_hash, attempts_count, expires_at)<br/>VALUES (:userId, 'EMAIL_VERIFICATION', :tokenHash, 0, NOW() + INTERVAL '15 MINUTES');
            API->>DB: COMMIT TRANSACTION
            
            API->>Mail: Gửi email thật chứa mã OTP 6 số tới hộp thư người dùng
            alt Gửi Email THÀNH CÔNG
                API-->>Client: 201 Created ("Đăng ký thành công. Vui lòng nhập mã OTP 6 số đã được gửi tới email của bạn.")
            else Gửi Email THẤT BẠI (Timeout / SMTP Exception)
                Note over API: Giữ nguyên tài khoản trong DB (không rollback).<br/>Ghi log lỗi hệ thống email.
                API-->>Client: 201 Created ("Tài khoản đã tạo thành công, nhưng hệ thống gửi email đang bận. Vui lòng sử dụng tính năng 'Gửi lại mã' để nhận mã xác minh.")
            end
        end
    end
```

---

## 3. LUỒNG 2: XÁC MINH EMAIL BẰNG MÃ OTP (CUSTOMER AUTO-APPROVED)

### 3.1. Mục tiêu & Nguyên tắc nghiệp vụ
- Người dùng gửi `email` và `code` (6 chữ số).
- **Atomic OTP Consume:** Kiểm tra điều kiện và consume mã trong cùng transaction. Kiểm tra affected rows: nếu bằng 0 (hết hạn, mã sai quá 5 lần, hoặc đã bị consume) thì Rollback.
- **CUSTOMER Auto-Approve:** Khi mã OTP hợp lệ, tài khoản khách hàng tự động được cập nhật `is_email_verified = TRUE` và `approval_status = 'APPROVED'`.

### 3.2. Sơ đồ xử lý chi tiết

```mermaid
sequenceDiagram
    autonumber
    actor Client as Frontend (Song)
    participant API as Auth API
    participant DB as PostgreSQL

    Client->>API: POST /api/v1/auth/verify-email (email, code)
    Note over API: email = email.Trim().ToLowerInvariant()
    API->>DB: SELECT id FROM users WHERE email = :email;
    alt Không tìm thấy user
        API-->>Client: 400 Bad Request ("Yêu cầu xác minh không hợp lệ")
    else Tìm thấy user
        API->>DB: SELECT id, token_hash, attempts_count, expires_at<br/>FROM user_tokens WHERE user_id = :userId AND token_type = 'EMAIL_VERIFICATION' AND is_used = FALSE<br/>ORDER BY created_at DESC LIMIT 1;
        
        alt Không có mã active hoặc NOW() > expires_at
            API-->>Client: 400 Bad Request ("Mã xác minh không tồn tại hoặc đã hết hạn. Vui lòng yêu cầu mã mới.")
        else attempts_count >= 5
            API-->>Client: 400 Bad Request ("Mã xác minh đã bị hủy do nhập sai quá 5 lần. Vui lòng bấm 'Gửi lại mã'.")
        else Mã còn hạn và attempts_count < 5
            Note over API: Tính expectedHash = HMAC_SHA256(secretKey, $"{userId}:EMAIL_VERIFICATION:{code}")
            alt Mã SAI (expectedHash != token_hash)
                API->>DB: UPDATE user_tokens SET attempts_count = attempts_count + 1,<br/>is_used = CASE WHEN attempts_count + 1 >= 5 THEN TRUE ELSE is_used END<br/>WHERE id = :tokenId AND is_used = FALSE;
                API-->>Client: 400 Bad Request ("Mã xác minh không chính xác. Bạn còn (5 - attempts - 1) lần thử.")
            else Mã ĐÚNG (expectedHash == token_hash)
                API->>DB: BEGIN TRANSACTION
                API->>DB: UPDATE user_tokens SET is_used = TRUE, used_at = NOW()<br/>WHERE id = :tokenId AND user_id = :userId AND token_type = 'EMAIL_VERIFICATION'<br/>  AND is_used = FALSE AND expires_at > NOW() AND attempts_count < 5;
                alt affected rows == 0 (Đã bị consume song song hoặc vừa hết hạn)
                    API->>DB: ROLLBACK
                    API-->>Client: 400 Bad Request ("Mã xác minh không hợp lệ hoặc đã được sử dụng.")
                else affected rows == 1 (Thành công tuyệt đối)
                    Note over DB: [Approved by Owner] Tự động kích hoạt & duyệt CUSTOMER:
                    API->>DB: UPDATE users SET is_email_verified = TRUE, approval_status = 'APPROVED', updated_at = NOW() WHERE id = :userId;
                    API->>DB: COMMIT TRANSACTION
                    API-->>Client: 200 OK ("Xác minh email thành công. Tài khoản của bạn đã được kích hoạt và có thể đăng nhập ngay.")
                end
            end
        end
    end
```

---

## 4. LUỒNG 3: GỬI LẠI MÃ XÁC MINH (RESEND VERIFICATION)

1. Client gửi: `POST /api/v1/auth/resend-verification` kèm `{ "email": "phung.vo@example.com" }`.
2. Backend chuẩn hóa email lowercase, tra cứu `users`:
   - Nếu user không tồn tại: Trả về `200 OK` trung tính để tránh user enumeration.
   - Nếu `is_email_verified == TRUE`: Trả về `200 OK` ("Tài khoản đã được xác minh trước đó. Bạn có thể đăng nhập ngay.").
3. **Cơ chế Cooldown 60 giây chống spam:**
   - Tra cứu bản ghi gần nhất trong `user_tokens`: Nếu `NOW() - created_at < 60 SECONDS`, trả về `429 Too Many Requests` ("Vui lòng chờ 60 giây trước khi yêu cầu gửi lại mã.").
4. **Vô hiệu hóa toàn bộ mã cũ chưa dùng:**
   ```sql
   UPDATE user_tokens
   SET is_used = TRUE
   WHERE user_id = :userId AND token_type = 'EMAIL_VERIFICATION' AND is_used = FALSE;
   ```
5. Sinh mã OTP 6 số mới, tính `HMAC-SHA256`, ghi vào `user_tokens` với TTL 15 phút.
6. Gửi email thật chứa mã mới tới email của user. Trả về `200 OK`.

---

## 5. LUỒNG 4: ĐĂNG NHẬP BẰNG USERNAME + PASSWORD

### 5.1. Quy tắc an ninh đăng nhập chuẩn OWASP
1. Đăng nhập **BẮT BUỘC bằng Username + Password**. Không nhận Email. Không bắt OTP hay reCAPTCHA khi đăng nhập thông thường.
2. **Thứ tự kiểm tra an ninh:**
   - **Xác thực Mật khẩu PHẢI ĐƯỢC THỰC HIỆN TRƯỚC TIÊN**.
   - Nếu `username` không tồn tại: Chạy Dummy Password Verify để ngăn ngừa Timing Attack dò tìm tên đăng nhập.
   - Nếu mật khẩu SAI: Tăng `failed_login_attempts` (ngưỡng 5 lần khóa 15 phút) $\rightarrow$ Trả `401 Unauthorized`.
   - **Chỉ sau khi mật khẩu ĐÚNG** mới kiểm tra các trạng thái: `lockout_end_at`, `is_email_verified`, `is_active`, `approval_status`.

### 5.2. Sơ đồ tuần tự

```mermaid
sequenceDiagram
    autonumber
    actor Client as Frontend (Song)
    participant API as Auth API
    participant DB as PostgreSQL

    Client->>API: POST /api/v1/auth/login (username, password)
    Note over API: username = username.Trim().ToLowerInvariant()
    API->>DB: SELECT id, password_hash, role_id, is_active, is_email_verified, approval_status,<br/>failed_login_attempts, lockout_end_at<br/>FROM users WHERE username = :username;
    
    alt User KHÔNG tồn tại
        API->>API: Chạy Dummy Password Verify (giả lập BCrypt/PBKDF2 cùng thời gian)
        API-->>Client: 401 Unauthorized ("Tên đăng nhập hoặc mật khẩu không chính xác")
    else User TỒN TẠI
        API->>API: PasswordHasher.VerifyHashedPassword(password, password_hash)
        
        alt Mật khẩu SAI
            API->>DB: UPDATE users SET failed_login_attempts = failed_login_attempts + 1,<br/>lockout_end_at = CASE WHEN failed_login_attempts + 1 >= 5 THEN NOW() + INTERVAL '15 MINUTES' ELSE NULL END<br/>WHERE id = :userId;
            API-->>Client: 401 Unauthorized ("Tên đăng nhập hoặc mật khẩu không chính xác")
        else Mật khẩu ĐÚNG
            alt Tài khoản đang bị khóa tạm thời (lockout_end_at IS NOT NULL AND NOW() < lockout_end_at)
                API-->>Client: 423 Locked ("Tài khoản đang tạm thời bị khóa do trước đó nhập sai nhiều lần. Vui lòng thử lại sau.")
            else Chưa xác minh email (is_email_verified == FALSE)
                API-->>Client: 403 Forbidden ("Tài khoản chưa hoàn tất xác minh email. Vui lòng xác minh mã OTP gửi qua email trước khi đăng nhập.")
            else Tài khoản bị vô hiệu hóa (is_active == FALSE)
                API-->>Client: 403 Forbidden ("Tài khoản đã bị vô hiệu hóa bởi Quản trị viên.")
            else Đang chờ duyệt (approval_status != 'APPROVED')
                API-->>Client: 403 Forbidden ("Tài khoản đang chờ Quản trị viên phê duyệt.")
            else ĐỦ ĐIỀU KIỆN ĐĂNG NHẬP
                API->>DB: UPDATE users SET failed_login_attempts = 0, lockout_end_at = NULL WHERE id = :userId;
                API->>API: Sinh JWT Access Token (TTL 15 phút, claims: sub=userId, role)
                API->>API: Sinh Refresh Token 256-bit thô CSPRNG (rawRefreshToken)
                API->>DB: INSERT INTO refresh_tokens (user_id, token_hash, user_agent, ip_address, expires_at)<br/>VALUES (:userId, SHA256(:rawRefreshToken), :userAgent, :ip, NOW() + INTERVAL '7 DAYS');
                API-->>Client: 200 OK { accessToken, refreshToken: rawRefreshToken, userProfile }
            end
        end
    end
```

---

## 6. LUỒNG 5: CẤP MỚI ACCESS TOKEN (REFRESH TOKEN FLOW)

Thực hiện cơ chế không xoay vòng (No-rotation) cho Sprint 3 theo quyết định chính thức:
1. Client gửi: `POST /api/v1/auth/refresh` kèm `{ "refreshToken": rawRefreshToken }`.
2. Backend tính: `tokenHash = SHA256(rawRefreshToken)`.
3. Backend tra cứu:
   ```sql
   SELECT r.id, r.user_id, u.is_active, u.is_email_verified, u.approval_status
   FROM refresh_tokens r
   JOIN users u ON r.user_id = u.id
   WHERE r.token_hash = :tokenHash AND r.is_revoked = FALSE AND r.expires_at > NOW();
   ```
4. Nếu không hợp lệ hoặc user bị khóa: Trả về `401 Unauthorized`.
5. Nếu hợp lệ: Cấp mới một **JWT Access Token mới (TTL 15 phút)**. Giữ nguyên Refresh Token hiện tại. Trả về `200 OK { "accessToken": newJwt }`.

---

## 7. LUỒNG 6: ĐĂNG XUẤT (LOGOUT & LOGOUT-ALL)

### 7.1. Đăng xuất phiên hiện tại (`POST /api/v1/auth/logout`)
- Client gửi: `{ "refreshToken": rawRefreshToken }`.
- **Quy tắc quan trọng đã được Owner duyệt:** API Logout **chỉ yêu cầu Refresh Token hợp lệ** để thu hồi phiên. **Kể cả khi JWT Access Token đã hết hạn**, người dùng vẫn đăng xuất thành công.
- Xử lý CSDL:
  ```sql
  UPDATE refresh_tokens 
  SET is_revoked = TRUE, revoked_at = NOW() 
  WHERE token_hash = SHA256(:refreshToken) AND is_revoked = FALSE;
  ```
- Trả về `200 OK` ("Đăng xuất thành công.").

### 7.2. Đăng xuất tất cả thiết bị (Logout-All)
- Áp dụng khi đổi mật khẩu hoặc phát hiện tài khoản bị xâm nhập:
  ```sql
  UPDATE refresh_tokens 
  SET is_revoked = TRUE, revoked_at = NOW() 
  WHERE user_id = :userId AND is_revoked = FALSE;
  ```

---

## 8. LUỒNG 7: XEM VÀ CẬP NHẬT HỒ SƠ NGƯỜI DÙNG

1. **Xem hồ sơ (`GET /api/v1/users/me`):** Trả về `UserProfileDto` (`id`, `username`, `email`, `fullName`, `phoneNumber`, `avatarUrl`, `role`, `isEmailVerified`, `approvalStatus`).
2. **Cập nhật hồ sơ (`PUT /api/v1/users/me`):**
   - Request DTO: `UpdateProfileRequestDto(fullName, phoneNumber)`.
   - **Đã loại bỏ `avatarUrl`**. Client không được tự gửi avatar url để chống XSS.
   - **Tuyệt đối cấm sửa `username`, `email`, `role`, `approvalStatus`** qua API này.
