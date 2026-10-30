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

Để chạy luồng Staff nhận diện ảnh ở local, copy `.env.example` thành `.env`, điền
`ConnectionStrings__AiRecognitionConnection` và các giá trị cần thiết, sau đó nạp
biến môi trường vào terminal hiện tại:

```powershell
Copy-Item .env.example .env
notepad .env
. .\scripts\Load-Env.ps1
```

Sau đó chạy migration database AI và backend:

```powershell
dotnet ef database update `
  --project backend\src\SmartParking.Infrastructure `
  --startup-project backend\src\SmartParking.Api `
  --context AiRecognitionDbContext

dotnet run --project backend\src\SmartParking.Api --launch-profile http
```

Endpoint `POST /api/v1/staff/detect` trả `PENDING_CONFIRMATION` khi có biển số và
`UNREADABLE` khi AI không đọc được. Staff xác nhận hoặc nhập biển số bằng
`POST /api/v1/staff/detections/{id}/confirm` với JSON `{ "plateNumber": "51F12345", "note": "..." }`;
ghi chú bắt buộc khi sửa biển số AI đã đọc. Bản ghi lưu biển số AI ban đầu, kết quả
xác nhận, người xác nhận, thời điểm và ghi chú để đối chiếu.

FastAPI chạy ở terminal riêng. Có thể chạy từ thư mục gốc bằng đường dẫn đầy đủ,
nhưng chạy từ `ai-service` là cách khuyến nghị:

```powershell
Set-Location ai-service
python run.py
```

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

Xem [hướng dẫn Windows/macOS](docs/operations/shared-database.md). Chạy script một lần để nhập thông tin DB, kiểm tra query thực tế và lưu cấu hình trên máy; mật khẩu không nằm trong repo. Bộ script chuẩn bị `ConnectionStrings:DefaultConnection`; việc đăng ký DbContext/repository PostgreSQL trong backend là phần triển khai riêng.
## 7. Kết nối Neon dùng chung

Xem [hướng dẫn Windows/macOS](docs/operations/shared-database.md). Chạy script một lần để nhập thông tin DB, kiểm tra query thực tế và lưu cấu hình trên máy; mật khẩu không nằm trong repo. Backend sử dụng `ConnectionStrings:DefaultConnection` cho EF Core/PostgreSQL.

## 8. Chạy web trong bản Sprint3.demo

Chạy tại thư mục repository bằng PowerShell. Lần đầu cấu hình Neon, nhập username/password **database** khi script yêu cầu:

```powershell
.\scripts\Connect-SharedDatabase.ps1 -Configure -RunBackend
```

Script kiểm tra kết nối và lưu cấu hình trong User Secrets trên máy. Những lần sau dùng `-RunBackend` hoặc chạy API bằng:

```powershell
dotnet run --project .\backend\src\SmartParking.Api --launch-profile http
```

Mở <http://localhost:5295/app/>. `/health` chỉ kiểm tra tiến trình API, không xác nhận đã kết nối được database. Database cần có schema/migration Accounts và các vai trò của nhóm.

Customer có thể đăng ký tại `/app/register` bằng email và mật khẩu, nhập OTP 6 chữ số để xác minh, sau đó đăng nhập bằng email hoặc tên đăng nhập. Tài khoản nội bộ và phân công bãi do Admin quản lý.

Development mặc định dùng `Email:Provider=Mock`. Để gửi OTP thật bằng Gmail, cấu hình qua User Secrets hoặc biến môi trường: `Email:Provider=Smtp`, `Email:UserName` và `Email:SenderEmail` là địa chỉ Gmail gửi, `Email:Password` là App Password của tài khoản Gmail. Không đặt App Password trong source hoặc commit. Ví dụ:

```powershell
dotnet user-secrets set "Email:Provider" "Smtp" --project backend/src/SmartParking.Api
dotnet user-secrets set "Email:UserName" "<sender@gmail.com>" --project backend/src/SmartParking.Api
dotnet user-secrets set "Email:SenderEmail" "<sender@gmail.com>" --project backend/src/SmartParking.Api
dotnet user-secrets set "Email:Password" "<gmail-app-password>" --project backend/src/SmartParking.Api
```

Giữ `Security:OtpSecretKey` ổn định qua User Secrets/biến môi trường nếu cần xác minh OTP sau khi backend khởi động lại. Kiểm thử tự động dùng email Mock, không gửi Gmail thật.

Sau khi thay đổi frontend, tạo lại bundle được backend phục vụ:

```powershell
npm --prefix .\frontend\smart-parking run build
```

Kiểm tra phần xử lý API frontend bằng `npm --prefix .\frontend\smart-parking test`.

## 9. Viewer bãi đỗ 3D

Sau khi chạy backend, mở <http://localhost:5295/3d-viewer.html>. Trang và assets được đưa vào output khi build/publish API. Three.js và OrbitControls được tải từ CDN nên trình duyệt cần kết nối Internet.

API viewer dùng `/api/ParkingSlots` (alias `/api/v1/parking-viewer/slots`). Dữ liệu sơ đồ mẫu được lưu trong bộ nhớ, có nhận trạng thái từ event pipeline. API quản lý ô đỗ thật vẫn nằm dưới `/api/v1/parking-slots`.

Gemini chạy chế độ NoOp khi chưa có `Gemini:ApiKey`. Các adapter EF và live event bridge từ nhánh tích hợp vẫn là phần tùy chọn, chưa bật trong bản demo. Xem thêm [hướng dẫn viewer](docs/operations/3d-viewer-integration.md).
