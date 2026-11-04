# Khởi động Sprint 4 trên Neon

Parking Core tiếp tục dùng database Neon `smartparking` của project. Recognition Service dùng kết nối riêng cho dữ liệu nhận dạng.

| Tiến trình | Nơi lưu dữ liệu |
| --- | --- |
| Parking Core (`SmartParking.Api`) | `smartparking`. Giữ tài khoản, bãi, ô, booking và lịch sử. Sprint 4 thêm khu, chính sách, vé và xác minh xe vào cùng DB. |
| Recognition Service (`SmartParking.Recognition.Api`) | `smartparking_recognition`. Bản ghi nhận dạng, xác nhận OCR và hash ảnh. |
| FastAPI OCR (`ai-service`) | Xử lý ảnh và trả kết quả cho Recognition Service. |
| YARP Gateway | Chuyển tiếp API công khai về Parking Core như hiện tại. Core gọi Recognition Service qua HTTP nội bộ. |

## Nâng cấp schema Core hiện tại

1. Dùng một branch kiểm thử có dữ liệu hiện tại của `smartparking`. Bản sao schema-only phục vụ thử cấu trúc và thiếu dữ liệu để thử backfill.
2. Trong **SQL Editor**, chọn `smartparking` trên branch kiểm thử và chạy nguyên file [upgrade-existing-smartparking.sql](../../scripts/neon/sql/upgrade-existing-smartparking.sql). Script yêu cầu migration nền `20261029114209_AddDutyToStaffLotAssignment`, áp ba migration mới trong một transaction và giữ các bản ghi hiện có.
3. Kiểm tra đăng nhập bằng tài khoản cũ và luồng cấp vé/xác minh. Sau khi nghiệm thu bản sao, áp cùng script trên DB `smartparking` chính theo quy trình triển khai của nhóm.
4. Với Recognition Service, dùng DB `smartparking_recognition` đã cài schema. Đối chiếu dữ liệu trong `AI_SERVICE_DB` trước khi chuyển luồng OCR đang chạy của nhóm sang endpoint Recognition mới.

## Cấu hình và chạy

Mỗi tiến trình dùng biến môi trường riêng. Với Neon, lấy hostname từ **Connect** khi đang chọn đúng branch, điền tên DB tương ứng và dùng `SSL Mode=VerifyFull`. Khi triển khai, cấp credential giới hạn theo DB cho từng tiến trình. Tạo một khóa ngẫu nhiên ít nhất 32 byte và đặt cùng giá trị `Sprint4__RecognitionKey` cho Core và Recognition Service. Dùng HTTPS cho kết nối service ngoài loopback và giữ route `/internal/recognition` trên mạng nội bộ.

Trong thư mục `smart-parking-system`, lưu các giá trị của Core trong `scripts/.env.parking-operations` (đã có mẫu ignore `.env.*` trong Git).

```dotenv
Sprint4__Enabled=true
Sprint4__RecognitionUrl=http://127.0.0.1:5296/
Sprint4__RecognitionKey=<KHOA_NOI_BO>
```

Core dùng `ConnectionStrings:DefaultConnection` hiện có trong User Secrets khi chạy Development. Xem [cấu hình database dùng chung](shared-database.md). Nếu cần trỏ tới bản sao kiểm thử, đặt `ConnectionStrings__DefaultConnection` cho tiến trình Core với `Database=smartparking` và host của branch kiểm thử. Đăng nhập bằng tài khoản đã có trong DB.

Để gửi OTP thật qua Gmail, bật xác minh hai bước và tạo [mật khẩu ứng dụng Google](https://myaccount.google.com/apppasswords) cho tài khoản gửi. Thêm vào `scripts/.env.parking-operations`:

```dotenv
Email__Provider=Smtp
Email__Host=smtp.gmail.com
Email__Port=587
Email__EnableSsl=true
Email__UserName=<EMAIL_GUI>
Email__Password=<MAT_KHAU_UNG_DUNG>
Email__SenderEmail=<EMAIL_GUI>
```

Để bật nút **Đăng nhập bằng Google**, tạo OAuth client loại **Web application** trong [Google Cloud](https://developers.google.com/identity/gsi/web/guides/get-google-api-clientid). Thêm origin mở giao diện vào **Authorized JavaScript origins**, ví dụ `http://localhost:5295` cho giao diện được Core phục vụ. Điền `GoogleAuth__ClientId=<OAUTH_WEB_CLIENT_ID>` vào `scripts/.env.parking-operations`, nạp lại file rồi khởi động Core. Luồng này dùng tài khoản khách đã đăng ký và xác minh địa chỉ Gmail trùng khớp trong `smartparking`. Khi triển khai nhiều instance Core, giữ các yêu cầu trong cùng một lần đăng nhập trên cùng instance vì mã xác thực tạm thời nằm trong bộ nhớ.

Lưu cấu hình của Recognition Service trong `scripts/.env.recognition`.

```dotenv
Sprint4__RecognitionKey=<CUNG_KHOA_NOI_BO>
ConnectionStrings__Sprint4Recognition=Host=<HOST_TEST>;Database=smartparking_recognition;Username=<ROLE_RECOGNITION>;Password=<MAT_KHAU>;SSL Mode=VerifyFull
AiService__BaseUrl=http://127.0.0.1:8000/
```

Chạy FastAPI OCR như [hướng dẫn AI](../../ai-service/README.md). Trong hai cửa sổ PowerShell riêng, đứng ở thư mục repo. Nếu máy cho phép chạy script nội bộ, nạp đúng file rồi chạy tiến trình tương ứng.

```powershell
# Cửa sổ 1
. .\scripts\Load-Env.ps1 -EnvFile .\scripts\.env.recognition
dotnet run --project backend/src/SmartParking.Recognition.Api -- --urls http://127.0.0.1:5296
```

```powershell
# Cửa sổ 2
. .\scripts\Load-Env.ps1 -EnvFile .\scripts\.env.parking-operations
dotnet run --project backend/src/SmartParking.Api
```

Nếu chính sách PowerShell chặn `Load-Env.ps1`, có thể nạp file cấu hình bằng lệnh tương tác dưới đây. Đổi tên file cho từng cửa sổ trước khi chạy `dotnet run`.

```powershell
Get-Content -LiteralPath .\scripts\.env.parking-operations | ForEach-Object {
    if ($_ -match '^\s*([A-Za-z_][A-Za-z0-9_]*)=(.*)$') {
        [Environment]::SetEnvironmentVariable($Matches[1], $Matches[2], 'Process')
    }
}
```

Khi dùng mạng công ty đang chặn PostgreSQL TCP 5432, tạo và kiểm tra schema vẫn làm được trên SQL Editor. Chạy hai tiến trình trên môi trường được phép kết nối PostgreSQL tới Neon để thử luồng có dữ liệu. `GET /health` trên Recognition Service kiểm tra tiến trình HTTP. Thử nhận dạng ảnh qua Core mới kiểm tra cả OCR và ghi DB.
