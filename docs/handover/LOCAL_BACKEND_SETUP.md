# HƯỚNG DẪN CẤU HÌNH & CHẠY LOCAL BACKEND CHO THÀNH VIÊN NHÓM
## SMART PARKING SYSTEM – LOCAL DEVELOPMENT & TEAM SETUP GUIDE

**Dự án:** Smart Parking System – FPT Software OJT  
**Workspace:** `E:\smartparking`  
**Nhánh làm việc:** `feature/phung-database`  
**Đối tượng sử dụng:** Tất cả thành viên trong nhóm (Frontend, Backend, AI Service, QA)

---

## 1. TỔNG QUAN & NGUYÊN TẮC BẢO MẬT

Tài liệu này hướng dẫn các thành viên sau khi kéo code (Pull) từ repository về máy cá nhân có thể nhanh chóng:
1. Thiết lập cơ sở dữ liệu PostgreSQL test độc lập (không chạm hoặc ghi vào Neon Production).
2. Chạy Backend .NET 10 API và truy cập Swagger UI.
3. Sử dụng dịch vụ Email:
   - **Chế độ Mock (mặc định):** In mã OTP trực tiếp ra Terminal / Console để kiểm thử ngay lập tức mà không cần tài khoản Gmail.
   - **Chế độ SMTP thật:** Sử dụng Gmail App Password của cá nhân khi cần kiểm tra luồng gửi email thực tế đến hòm thư.

> [!IMPORTANT]
> **QUY TẮC AN TOÀN TUYỆT ĐỐI:**
> - Tuyệt đối không commit file cấu hình cục bộ chứa mật khẩu, connection strings hoặc API keys lên GitHub.
> - Sử dụng cơ chế **.NET User Secrets** hoặc **Biến môi trường (Environment Variables)** để lưu trữ bí mật trên máy cá nhân.
> - File [appsettings.example.json](file:///e:/smartparking/backend/src/SmartParking.Api/appsettings.example.json) cung cấp mẫu cấu hình chuẩn an toàn (chỉ chứa placeholder).

---

## 2. YÊU CẦU MÔI TRƯỜNG (PREREQUISITES)

- **.NET 10 SDK**: Kiểm tra bằng lệnh:
  ```bash
  dotnet --version
  ```
  *(Yêu cầu phiên bản 10.0.100 trở lên)*.
- **Công cụ EF Core CLI** (nếu chưa có):
  ```bash
  dotnet tool install --global dotnet-ef
  ```
- **Hệ quản trị CSDL PostgreSQL 15+** (chọn 1 trong 3 phương án ở Mục 3).

---

## 3. CẤU HÌNH CƠ SỞ DỮ LIỆU TEST (POSTGRESQL)

Mỗi thành viên cần một database riêng để kiểm thử. Chọn **1 trong 3 phương án** sau:

### Phương án A: Dùng Docker (Khuyên dùng – Nhanh và sạch)
Chạy lệnh PowerShell hoặc CMD sau để khởi chạy PostgreSQL container:
```bash
docker run --name smartparking-postgres -e POSTGRES_USER=postgres -e POSTGRES_PASSWORD=postgres -e POSTGRES_DB=smartparking_dev -p 5432:5432 -d postgres:16
```
- Host: `localhost`
- Port: `5432`
- Database: `smartparking_dev`
- Username: `postgres`
- Password: `postgres`

### Phương án B: Dùng PostgreSQL cài trực tiếp trên máy
1. Mở **pgAdmin** hoặc `psql`.
2. Tạo cơ sở dữ liệu mới có tên: `smartparking_dev`.
3. Ghi nhớ Port (thường là `5432`), Username (`postgres`) và Mật khẩu bạn đã đặt khi cài đặt PostgreSQL.

### Phương án C: Dùng Neon PostgreSQL (Branch Test riêng)
Nếu sử dụng Cloud Neon, bắt buộc phải tạo một **Branch riêng biệt** (ví dụ: `dev-yourname`), **tuyệt đối không dùng branch `production`**.

---

### Cấu hình chuỗi kết nối vào User Secrets

Tại thư mục gốc dự án (`E:\smartparking`), chạy lệnh:

```bash
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;Database=smartparking_dev;Username=postgres;Password=YOUR_LOCAL_PASSWORD;SSL Mode=Prefer" --project backend/src/SmartParking.Api
```

*(Thay thế `YOUR_LOCAL_PASSWORD` bằng mật khẩu PostgreSQL thực tế trên máy bạn).*

### Khởi tạo cấu trúc bảng (Chạy Migrations)

Sau khi cấu hình chuỗi kết nối, chạy lệnh sau để EF Core tự động tạo toàn bộ bảng và seed data ban đầu:

```bash
dotnet ef database update --project backend/src/SmartParking.Infrastructure --startup-project backend/src/SmartParking.Api
```

Sau khi migration hoàn tất, database đã có sẵn đầy đủ:
- Bảng danh mục vai trò: `ADMIN`, `MANAGER`, `STAFF`, `USER`.
- Các bảng bãi đỗ xe: `parking_lots`, `parking_floors`, `parking_slots`.
- Các bảng điều phối & phiên: `parking_dispatches`, `parking_sessions`, `vehicles`.
- Bảng đặt chỗ & công suất: `bookings`, `staff_lot_assignments`.

---

## 4. CẤU HÌNH DỊCH VỤ EMAIL (MOCK VS REAL GMAIL SMTP)

### 4.1. Chế độ Mock OTP (Mặc định trong Development)

Trong môi trường Development, file [appsettings.Development.json](file:///e:/smartparking/backend/src/SmartParking.Api/appsettings.Development.json) đã được cấu hình sẵn:
```json
"Email": {
  "Provider": "Mock"
}
```

**Cách hoạt động:**
- Khi người dùng đăng ký (`/api/v1/auth/register`) hoặc yêu cầu gửi lại OTP (`/api/v1/auth/resend-verification-otp`), hệ thống không kết nối SMTP ra ngoài.
- Mã OTP 6 số sẽ được in trực tiếp ra màn hình Terminal / Console của Backend:
  ```text
  info: SmartParking.Infrastructure.Services.EmailService[0]
        [DEV EMAIL SIMULATION] Verification OTP for user@example.com: 849201 (Expires in 15 minutes).
  ```
- Bạn chỉ cần copy mã này và gọi endpoint `/api/v1/auth/verify-email` để kích hoạt tài khoản.

---

### 4.2. Chế độ Real Gmail SMTP (Gửi mail thật)

Khi cần kiểm thử giao diện email HTML thực tế gửi về hộp thư cá nhân, hãy sử dụng **Gmail App Password**:

#### Bước 1: Tạo Gmail App Password (Mật khẩu ứng dụng)
1. Đăng nhập vào tài khoản Google cá nhân.
2. Truy cập [Google Account Security](https://myaccount.google.com/security).
3. Bật **Xác minh 2 bước (2-Step Verification)** nếu chưa bật.
4. Tìm kiếm từ khóa **"Mật khẩu ứng dụng" (App passwords)**.
5. Đặt tên ứng dụng (ví dụ: `SmartParking Backend`) và nhấn **Tạo (Create)**.
6. Google sẽ cấp cho bạn một chuỗi **16 ký tự** (dạng: `xxxx xxxx xxxx xxxx`). Lưu lại chuỗi này.

#### Bước 2: Lưu cấu hình vào User Secrets
Chạy các lệnh sau trong terminal:

```bash
# 1. Chuyển Provider sang Smtp
dotnet user-secrets set "Email:Provider" "Smtp" --project backend/src/SmartParking.Api

# 2. Cấu hình email Gmail của bạn
dotnet user-secrets set "Email:UserName" "your.email@gmail.com" --project backend/src/SmartParking.Api

# 3. Cấu hình mật khẩu ứng dụng 16 ký tự vừa tạo
dotnet user-secrets set "Email:Password" "xxxx xxxx xxxx xxxx" --project backend/src/SmartParking.Api

# 4. Cấu hình Sender Email hiển thị
dotnet user-secrets set "Email:SenderEmail" "your.email@gmail.com" --project backend/src/SmartParking.Api
```

#### Bước 3: Chuyển lại về chế độ Mock khi không muốn gửi mail thật
Khi muốn quay lại chế độ Mock để dev nhanh, chỉ cần chạy:
```bash
dotnet user-secrets set "Email:Provider" "Mock" --project backend/src/SmartParking.Api
```

> [!NOTE]
> **Quy tắc An toàn Nghiệp vụ:**  
> Hệ thống áp dụng cơ chế **Fail-Closed**. Nếu `Email:Provider` được đặt là `Smtp` nhưng mật khẩu sai hoặc mất mạng, Backend sẽ báo lỗi rõ ràng và từ chối gửi OTP, **tuyệt đối không âm thầm fallback sang Mock** để tránh việc người dùng tưởng rằng email đã được gửi thành công.

---

## 5. CÁCH CHẠY BACKEND VÀ TRUY CẬP SWAGGER

### 5.1. Khởi chạy ứng dụng

Từ thư mục gốc dự án:
```bash
dotnet run --project backend/src/SmartParking.Api
```

Ứng dụng sẽ khởi động và lắng nghe tại 2 cổng cục bộ:
- HTTP: `http://localhost:5295`
- HTTPS: `https://localhost:7189`

### 5.2. Mở Swagger UI

Mở trình duyệt và truy cập:
- `http://localhost:5295/swagger` (hoặc `https://localhost:7189/swagger`)

Tại đây, bạn có thể kiểm tra toàn bộ danh mục API:
- `Auth`: Đăng ký, đăng nhập, làm mới token, xác minh OTP.
- `Users`: Quản lý thông tin tài khoản cá nhân.
- `AdminUsers`: Quản trị viên phân quyền, tạo/duyệt tài khoản staff/manager.
- `ParkingLots`: Quản lý bãi đỗ xe, tầng, vị trí đỗ.
- `Vehicles`: Quản lý phương tiện cá nhân.
- `ParkingSessions`: Phiên gửi xe vào/ra bãi, tính phí, chống quay vòng vé (Anti-passback).
- `Bookings`: Đặt chỗ đỗ trước & quản lý công suất bãi (Task 07).

---

## 6. KỊCH BẢN KIỂM THỬ MẪU (TEST SCENARIOS)

### Kịch bản 1: Đăng ký & Kích hoạt tài khoản với Mock Email
1. Mở Swagger, tìm endpoint `POST /api/v1/auth/register`.
2. Gửi request body:
   ```json
   {
     "username": "tester01",
     "email": "tester01@example.com",
     "password": "Password123@",
     "fullName": "Nguyen Van Tester",
     "phoneNumber": "0912345678",
     "recaptchaToken": "test"
   }
   ```
3. Quan sát màn hình Terminal chạy Backend:
   ```text
   [DEV EMAIL SIMULATION] Verification OTP for tester01@example.com: 123456 (Expires in 15 minutes).
   ```
4. Mở endpoint `POST /api/v1/auth/verify-email`, gửi body:
   ```json
   {
     "email": "tester01@example.com",
     "otpCode": "123456"
   }
   ```
5. Nhận kết quả: `isEmailVerified: true`, tài khoản đã sẵn sàng đăng nhập!

### Kịch bản 2: Đăng nhập & Xác thực trên Swagger
1. Gọi `POST /api/v1/auth/login` với username `tester01` và password `Password123@`.
2. Copy chuỗi `accessToken` trong JSON phản hồi.
3. Nhấn nút **Authorize** (hình ổ khóa màu xanh ở góc phải trên Swagger).
4. Nhập vào ô Value: `Bearer <paste_token_tai_day>` và nhấn **Authorize**.
5. Giờ bạn có thể gọi các API bảo mật như `GET /api/v1/users/me` hoặc `GET /api/v1/vehicles`.

---

## 7. BẢNG TỔNG HỢP USER SECRETS & BIẾN MÔI TRƯỜNG

| Cấu hình | Mục đích | Giá trị mẫu / Khuyên dùng |
| :--- | :--- | :--- |
| `ConnectionStrings:DefaultConnection` | Chuỗi kết nối PostgreSQL | `Host=localhost;Port=5432;Database=smartparking_dev;Username=postgres;Password=YOUR_PASS;SSL Mode=Prefer` |
| `Email:Provider` | Chế độ Email | `Mock` (mặc định) hoặc `Smtp` |
| `Email:Host` | Địa chỉ máy chủ SMTP | `smtp.gmail.com` |
| `Email:Port` | Cổng SMTP | `587` |
| `Email:EnableSsl` | Bật SSL/TLS | `true` |
| `Email:UserName` | Tài khoản Gmail | `your.email@gmail.com` |
| `Email:Password` | Mật khẩu ứng dụng 16 ký tự | `abcd efgh ijkl mnop` |
| `Email:SenderEmail` | Email người gửi hiển thị | `your.email@gmail.com` |
| `Recaptcha:Enabled` | Bật/Tắt reCAPTCHA | `false` (trong `appsettings.Development.json`) |

---

## 8. XỬ LÝ SỰ CỐ THƯỜNG GẶP (TROUBLESHOOTING)

1. **Lỗi `Npgsql.NpgsqlException: Connection refused`**:
   - Nguyên nhân: Docker container PostgreSQL chưa chạy hoặc dịch vụ PostgreSQL trên máy chưa được khởi động.
   - Khắc phục: Chạy `docker start smartparking-postgres` hoặc kiểm tra dịch vụ PostgreSQL trong `services.msc`.

2. **Lỗi `535-5.7.8 Username and Password not accepted` khi gửi Gmail**:
   - Nguyên nhân: Bạn đang dùng mật khẩu đăng nhập tài khoản Gmail chính thay vì **Mật khẩu ứng dụng (App Password)**, hoặc chưa bật xác thực 2 bước.
   - Khắc phục: Tạo lại App Password 16 ký tự và cập nhật lại `Email:Password`.

3. **Lỗi Port `5295` hoặc `7189` đã bị chiếm dụng**:
   - Nguyên nhân: Có một instance Backend trước đó vẫn đang chạy ngầm.
   - Khắc phục: Tắt tiến trình cũ trong Task Manager hoặc đổi port trong `backend/src/SmartParking.Api/Properties/launchSettings.json`.
