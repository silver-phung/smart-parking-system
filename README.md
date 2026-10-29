# Smart Parking System

Dự án Hệ thống Quản lý Bãi đỗ xe thông minh (Smart Parking System) - FPT Software OJT.

## 1. Kiến trúc & Công nghệ định hướng
- **Backend:** C# / ASP.NET Core (.NET), Clean Architecture.
- **Database:** PostgreSQL hosted on Neon Cloud.
- **Frontend / Client:** Web & Dashboard.

Tài liệu nghiệp vụ, kiến trúc, API và hướng dẫn vận hành bắt đầu tại [Documentation](docs/README.md).

## 2. Quy ước nhánh (Branching Strategy)
- `main`: Nhánh production/release chính thức, luôn ở trạng thái ổn định.
- `develop`: Nhánh tích hợp chính cho toàn bộ tính năng của nhóm.
- `feature/*`: Nhánh phát triển tính năng riêng lẻ (tách từ `develop`, tạo Pull Request để merge về `develop`).
  - `feature/phung-database`: Phụ trách thiết kế schema PostgreSQL, EF Core Migrations và kết nối Neon.

## 3. Bảo mật (Security)
- Tuyệt đối không commit file `.env`, connection strings có chứa password hoặc credentials của Neon Cloud vào repository.
- Sử dụng file cấu hình mẫu `.env.example` hoặc user secrets khi phát triển cục bộ.

## 4. Kết nối Neon dùng chung

Xem [hướng dẫn Windows/macOS](docs/operations/shared-database.md). Chạy script một lần để nhập thông tin DB, kiểm tra query thực tế và lưu cấu hình trên máy; mật khẩu không nằm trong repo. Bộ script chuẩn bị `ConnectionStrings:DefaultConnection`; việc đăng ký DbContext/repository PostgreSQL trong backend là phần triển khai riêng.
