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

## 4. Lưu trữ ảnh Cloudinary

API ảnh dùng thông tin tài khoản Cloudinary qua User Secrets hoặc biến môi trường; API secret không được đặt trong `appsettings.json` hay commit vào Git. Sau khi rotate API secret trên Cloudinary, cấu hình local bằng User Secrets:

```powershell
dotnet user-secrets set "Cloudinary:ApiKey" "<api-key>" --project backend\src\SmartParking.Api
dotnet user-secrets set "Cloudinary:ApiSecret" "<rotated-api-secret>" --project backend\src\SmartParking.Api
```

Cloud name hiện được cấu hình là `dpwcvdfg`. Có thể cấu hình `CLOUDINARY_CLOUD_NAME`, `CLOUDINARY_API_KEY`, `CLOUDINARY_API_SECRET` làm biến môi trường; `CLOUDINARY_URL` vẫn được hỗ trợ nếu cần. API giới hạn ảnh mặc định 10 MiB và chỉ nhận JPEG, PNG, WebP; có thể điều chỉnh `Cloudinary:MaxUploadSizeBytes` và `Cloudinary:Folder` trong cấu hình ứng dụng.

Các endpoint yêu cầu JWT:

- `POST /api/v1/images` — gửi multipart form-data với field `file`.
- `GET /api/v1/images/{id}` — lấy metadata ảnh của mình (ADMIN xem được mọi ảnh).
- `DELETE /api/v1/images/{id}` — xóa ảnh Cloudinary của mình (ADMIN có thể xóa mọi ảnh).

Ảnh được tải lên Cloudinary trước rồi metadata mới được lưu vào PostgreSQL. Chạy migration bằng:

```powershell
dotnet ef database update --project backend\src\SmartParking.Infrastructure --startup-project backend\src\SmartParking.Api
```

Request mẫu có trong `backend/src/SmartParking.Api/SmartParking.Api.http`.
