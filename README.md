# Smart Parking System

Dự án Hệ thống Quản lý Bãi đỗ xe thông minh (Smart Parking System) - FPT Software OJT.

## 1. Kiến trúc & Công nghệ định hướng
- **Backend:** C# / ASP.NET Core (.NET), Clean Architecture.
- **Database:** PostgreSQL hosted on Neon Cloud.
- **Frontend / Client:** Web & Dashboard.

## 2. Quy ước nhánh (Branching Strategy)
- `main`: Nhánh production/release chính thức, luôn ở trạng thái ổn định.
- `develop`: Nhánh tích hợp chính cho toàn bộ tính năng của nhóm.
- `feature/*`: Nhánh phát triển tính năng riêng lẻ (tách từ `develop`, tạo Pull Request để merge về `develop`).
  - `feature/phung-database`: Phụ trách thiết kế schema PostgreSQL, EF Core Migrations và kết nối Neon.

## 3. Bảo mật (Security)
- Tuyệt đối không commit file `.env`, connection strings có chứa password hoặc credentials của Neon Cloud vào repository.
- Sử dụng file cấu hình mẫu `.env.example` hoặc user secrets khi phát triển cục bộ.

## 4. Kết nối Neon dùng chung

Xem [hướng dẫn Windows/macOS](docs/database/KET-NOI-DB-CHUNG.md). Chạy script một lần để nhập thông tin DB, kiểm tra query thực tế và lưu cấu hình trên máy; mật khẩu không nằm trong repo. Backend sử dụng `ConnectionStrings:DefaultConnection` cho EF Core/PostgreSQL.

## 5. Chạy web và đăng ký tài khoản trong bản Sprint3.demo

Chạy tại thư mục repository bằng PowerShell. Lần đầu cấu hình Neon, nhập username/password **database** khi script yêu cầu:

```powershell
.\scripts\Connect-SharedDatabase.ps1 -Configure -RunBackend
```

Script kiểm tra kết nối và lưu cấu hình trong User Secrets trên máy. Những lần sau dùng `-RunBackend` hoặc chạy API bằng:

```powershell
dotnet run --project .\backend\src\SmartParking.Api --launch-profile http
```

Mở <http://localhost:5295/app/>. `/health` chỉ kiểm tra tiến trình API, không xác nhận đã kết nối được database. Database cần có schema/migration Accounts và các vai trò của nhóm.

Đăng ký bằng **tên đăng nhập** (3–50 ký tự chữ, số, dấu chấm hoặc gạch dưới), họ tên, email và mật khẩu ít nhất 8 ký tự. Sau khi đăng ký, nhập OTP tại `/app/verify-email`, rồi đăng nhập bằng tên đăng nhập và mật khẩu. Mỗi tài khoản mới có vai trò `USER`; các tài khoản nội bộ do quản trị viên cấp.

Trong môi trường `Development`, `Email:Provider` mặc định là `Mock`: OTP được in tại terminal backend ở dòng `[DEV EMAIL SIMULATION]`, không gửi email thật. Dùng mã đó để kiểm tra luồng đăng ký trên máy. Muốn gửi email thật, cấu hình `Email:Provider=Smtp` và thông tin SMTP trong User Secrets. Cấu hình Production vẫn yêu cầu dịch vụ gửi email thật.

Sau khi thay đổi frontend, tạo lại bundle được backend phục vụ:

```powershell
npm --prefix .\frontend\smart-parking run build
```

Kiểm tra phần xử lý API frontend bằng `npm --prefix .\frontend\smart-parking test`.
