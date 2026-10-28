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

## 5. YARP API Gateway

Gateway chạy tại `http://localhost:5070`, proxy tới SmartParking API tại `http://localhost:5295/`. Đây là một API monolith trong repository hiện tại, chưa có User/Parking/Booking/Image microservice tách riêng.

Route và cluster được định nghĩa bằng C# trong `backend/src/ApiGateway/SmartParking.YarpApiGateway/Configurations/ReverseProxyConfig.cs`; gateway nạp chúng với YARP `LoadFromMemory`. Địa chỉ các instance downstream vẫn lấy từ cấu hình môi trường, tách khỏi code.

Route `/api/{**catch-all}` chuyển tiếp mọi HTTP method và toàn bộ endpoint hiện có dưới `/api`, giữ nguyên path, query string, body và headers (bao gồm JWT `Authorization`). Các nhóm route:

| Prefix | Chức năng |
|---|---|
| `/api/v1/auth`, `/api/v1/users`, `/api/v1/admin/users` | Xác thực, hồ sơ, quản trị người dùng |
| `/api/v1/vehicles` | Phương tiện |
| `/api/v1/parking-lots`, `/api/v1/parking-floors`, `/api/v1/parking-slots` | Bãi đỗ, tầng, chỗ đỗ |
| `/api/v1/parking-sessions`, `/api/v1/parking-dispatches`, `/api/v1/staff-assignments` | Phiên đỗ, điều phối, phân công nhân viên |
| `/api/v1/bookings`, `/api/v1/images` | Đặt chỗ, ảnh (bao gồm multipart upload) |
| `/api/system`, `/health`, `/swagger/*` | System, health, Swagger |

JWT vẫn được xác thực và phân quyền tại SmartParking API; gateway chỉ chuyển tiếp request. `/gateway/health` kiểm tra tiến trình gateway, còn `/health` proxy tới API. YARP kiểm tra destination qua `/health` mỗi 10 giây.

### Cách chạy và truy cập gateway

Mở hai terminal ở thư mục gốc repository. Chạy API trước ở terminal thứ nhất:

```powershell
dotnet run --project backend\src\SmartParking.Api --launch-profile http
```

Sau đó chạy YARP ở terminal thứ hai:

```powershell
dotnet run --project backend\src\ApiGateway\SmartParking.YarpApiGateway --launch-profile http
```

Frontend/API client gửi request tới **`http://localhost:5070`** thay vì gọi trực tiếp cổng `5295`. Ví dụ:

```powershell
Invoke-RestMethod http://localhost:5070/gateway/health
Invoke-RestMethod http://localhost:5070/health
Invoke-RestMethod http://localhost:5070/api/system/info
```

API có yêu cầu đăng nhập vẫn cần JWT access token trong header:

```powershell
Invoke-RestMethod -Uri http://localhost:5070/api/v1/users/me `
  -Headers @{ Authorization = "Bearer <access-token>" }
```

`/gateway/health` kiểm tra gateway; `/health` kiểm tra API downstream qua proxy. Request mẫu cho các endpoint nằm trong `backend/src/ApiGateway/SmartParking.YarpApiGateway/SmartParking.YarpApiGateway.http`.

Địa chỉ development được cấu hình tại `DownstreamServices:SmartParkingApi:Destinations:instance1`. Có thể ghi đè bằng biến môi trường `DownstreamServices__SmartParkingApi__Destinations__instance1`. Cluster hỗ trợ Round Robin; hiện chỉ có một instance thật được cấu hình. Để thêm replica đang chạy, thêm destination như `DownstreamServices__SmartParkingApi__Destinations__instance2=http://localhost:5296/`. Trong Docker, dùng DNS/tên container thay cho `localhost`. Repository hiện không có source frontend nên chưa có API client để chuyển base URL sang gateway.

## 6. Kết nối Neon dùng chung

Xem [hướng dẫn Windows/macOS](docs/database/KET-NOI-DB-CHUNG.md). Chạy script một lần để nhập thông tin DB, kiểm tra query thực tế và lưu cấu hình trên máy; mật khẩu không nằm trong repo. Bộ script chuẩn bị `ConnectionStrings:DefaultConnection`; việc đăng ký DbContext/repository PostgreSQL trong backend là phần triển khai riêng.
