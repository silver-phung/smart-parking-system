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

## 5. Chạy web trong bản Sprint3.demo

Chạy tại thư mục repository bằng PowerShell. Lần đầu cấu hình Neon, nhập username/password **database** khi script yêu cầu:

```powershell
.\scripts\Connect-SharedDatabase.ps1 -Configure -RunBackend
```

Script kiểm tra kết nối và lưu cấu hình trong User Secrets trên máy. Những lần sau dùng `-RunBackend` hoặc chạy API bằng:

```powershell
dotnet run --project .\backend\src\SmartParking.Api --launch-profile http
```

Mở <http://localhost:5295/app/>. `/health` chỉ kiểm tra tiến trình API, không xác nhận đã kết nối được database. Database cần có schema/migration Accounts và các vai trò của nhóm.

Bản Sprint3.demo chỉ đăng nhập bằng tài khoản đã được cấp. Chức năng tự đăng ký và xác minh email/OTP đã được gỡ. Tài khoản nội bộ và phân công bãi do Admin quản lý.

Sau khi thay đổi frontend, tạo lại bundle được backend phục vụ:

```powershell
npm --prefix .\frontend\smart-parking run build
```

Kiểm tra phần xử lý API frontend bằng `npm --prefix .\frontend\smart-parking test`.

## 6. Viewer bãi đỗ 3D

Sau khi chạy backend, mở <http://localhost:5295/3d-viewer.html>. Trang và assets được đưa vào output khi build/publish API. Three.js và OrbitControls được tải từ CDN nên trình duyệt cần kết nối Internet.

API viewer dùng `/api/ParkingSlots` (alias `/api/v1/parking-viewer/slots`). Dữ liệu sơ đồ mẫu được lưu trong bộ nhớ, có nhận trạng thái từ event pipeline. API quản lý ô đỗ thật vẫn nằm dưới `/api/v1/parking-slots`.

Gemini chạy chế độ NoOp khi chưa có `Gemini:ApiKey`. Các adapter EF và live event bridge từ nhánh tích hợp vẫn là phần tùy chọn, chưa bật trong bản demo. Xem thêm [hướng dẫn viewer](docs/3D-VIEWER-INTEGRATION.md).
