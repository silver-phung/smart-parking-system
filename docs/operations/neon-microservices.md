# Khởi động Sprint 4 trên Neon

Dùng branch `test mcsv` để chạy Sprint 4. Dữ liệu Sprint 3 nằm ở database `smartparking`. Khởi tạo dữ liệu mới cho Sprint 4 theo quy trình dưới đây.

| Tiến trình | Nơi lưu dữ liệu |
| --- | --- |
| Parking Core (`SmartParking.Api`) | `smartparking_core_sprint4`. Bãi, khu, ô, vé, phiên, booking và tài khoản phục vụ phân quyền của Core. |
| Recognition Service (`SmartParking.Recognition.Api`) | `smartparking_recognition`. Bản ghi nhận dạng, xác nhận OCR và hash ảnh. |
| FastAPI OCR (`ai-service`) | Xử lý ảnh và trả kết quả cho Recognition Service. |
| YARP Gateway | Chuyển tiếp API công khai về Parking Core như hiện tại. Core gọi Recognition Service qua HTTP nội bộ. |

`smartparking_identity` đã chuẩn bị schema cho đợt tách Identity. Để dùng nó cần tách cả đăng nhập, tra quyền, các quan hệ với `users` và luồng tham chiếu ID người dùng. Đợt này Parking Core có bộ tài khoản Sprint 4 riêng trong DB của Core.

## Tạo schema

1. Trong branch `test mcsv`, vào **Databases** và tạo `smartparking_core_sprint4`.
2. Trong **SQL Editor** chọn database mới, chạy `SELECT current_database();` và đối chiếu đúng `smartparking_core_sprint4`. Dán toàn bộ [01-core-full.sql](../../scripts/neon/sql/01-core-full.sql) và bấm **Run**. Đây là bộ schema đầy đủ cho DB mới. Nó cài các migration Sprint 4 và seed các role, còn bãi/ô/vé/tài khoản cần tạo cho môi trường Sprint 4.
3. Chạy [05-verify-core.sql](../../scripts/neon/sql/05-verify-core.sql) ở DB mới. Lịch sử phải có `20261102083650_AddZonesPoliciesTicketsAndVerification` và `20261102084552_LinkEntryEvidence`. Các kiểm tra vi phạm phải bằng 0.
4. Dùng lại `smartparking_recognition` trên `test mcsv`. Nếu dùng môi trường mới, tạo DB cùng tên rồi chạy [02-recognition-full.sql](../../scripts/neon/sql/02-recognition-full.sql) tại đó. Có thể xác nhận schema bằng [06-verify-recognition.sql](../../scripts/neon/sql/06-verify-recognition.sql).

## Cấu hình và chạy

Mỗi tiến trình dùng biến môi trường riêng. Với Neon, lấy hostname từ **Connect** khi đang chọn đúng branch, điền tên DB tương ứng và dùng `SSL Mode=VerifyFull`. Khi triển khai, cấp credential giới hạn theo DB cho từng tiến trình. Tạo một khóa ngẫu nhiên ít nhất 32 byte và đặt cùng giá trị `Sprint4__RecognitionKey` cho Core và Recognition Service. Dùng HTTPS cho kết nối service ngoài loopback và giữ route `/internal/recognition` trên mạng nội bộ.

Trong thư mục `smart-parking-system`, lưu các giá trị của Core trong `.env.sprint4-core` (đã có mẫu ignore `.env.*` trong Git).

```dotenv
Sprint4__Enabled=true
Sprint4__RecognitionUrl=http://127.0.0.1:5296/
Sprint4__RecognitionKey=<KHOA_NOI_BO>
ConnectionStrings__Sprint4Core=Host=<HOST_TEST>;Database=smartparking_core_sprint4;Username=<ROLE_CORE>;Password=<MAT_KHAU>;SSL Mode=VerifyFull
```

Để tạo tài khoản quản trị đầu tiên trên Core mới, đặt thêm `AdminBootstrap__Enabled=true`, `AdminBootstrap__Username`, `AdminBootstrap__Email` và `AdminBootstrap__Password` với giá trị dành cho Sprint 4. Sau khi đăng nhập được, đặt `AdminBootstrap__Enabled=false` và bỏ mật khẩu bootstrap khỏi file. Dùng tài khoản này để tạo bãi, khu, ô và chính sách trước khi thử cấp vé.

Lưu cấu hình của Recognition Service trong `.env.sprint4-recognition`.

```dotenv
Sprint4__RecognitionKey=<CUNG_KHOA_NOI_BO>
ConnectionStrings__Sprint4Recognition=Host=<HOST_TEST>;Database=smartparking_recognition;Username=<ROLE_RECOGNITION>;Password=<MAT_KHAU>;SSL Mode=VerifyFull
AiService__BaseUrl=http://127.0.0.1:8000/
```

Chạy FastAPI OCR như [hướng dẫn AI](../../ai-service/README.md). Trong hai cửa sổ PowerShell riêng, đứng ở thư mục repo. Nếu máy cho phép chạy script nội bộ, nạp đúng file rồi chạy tiến trình tương ứng.

```powershell
# Cửa sổ 1
. .\scripts\Load-Env.ps1 -EnvFile .env.sprint4-recognition
dotnet run --project backend/src/SmartParking.Recognition.Api -- --urls http://127.0.0.1:5296
```

```powershell
# Cửa sổ 2
. .\scripts\Load-Env.ps1 -EnvFile .env.sprint4-core
dotnet run --project backend/src/SmartParking.Api
```

Nếu chính sách PowerShell chặn `Load-Env.ps1`, có thể nạp file cấu hình bằng lệnh tương tác dưới đây. Đổi tên file cho từng cửa sổ trước khi chạy `dotnet run`.

```powershell
Get-Content -LiteralPath .env.sprint4-core | ForEach-Object {
    if ($_ -match '^\s*([A-Za-z_][A-Za-z0-9_]*)=(.*)$') {
        [Environment]::SetEnvironmentVariable($Matches[1], $Matches[2], 'Process')
    }
}
```

Khi dùng mạng công ty đang chặn PostgreSQL TCP 5432, tạo và kiểm tra schema vẫn làm được trên SQL Editor. Chạy hai tiến trình trên môi trường được phép kết nối PostgreSQL tới Neon để thử luồng có dữ liệu. `GET /health` trên Recognition Service kiểm tra tiến trình HTTP. Thử nhận dạng ảnh qua Core mới kiểm tra cả OCR và ghi DB.
