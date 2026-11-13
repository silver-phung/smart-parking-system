# Neon cho vận hành bãi

Parking Core tiếp tục dùng database Neon `smartparking` của project. Migration vận hành bãi thêm khu, chính sách, vé và xác minh xe vào schema hiện có, cùng dữ liệu tài khoản, bãi, ô, booking và lịch sử.

## Nâng cấp schema Core hiện tại

1. Dùng một branch kiểm thử có dữ liệu hiện tại của `smartparking`. Bản sao schema-only phục vụ thử cấu trúc và thiếu dữ liệu để thử backfill.
2. Trong **SQL Editor**, chọn `smartparking` trên branch kiểm thử và chạy nguyên file [upgrade-existing-smartparking.sql](../../scripts/neon/sql/upgrade-existing-smartparking.sql). Script yêu cầu migration nền `20261029114209_AddDutyToStaffLotAssignment`, áp ba migration mới trong một transaction và giữ các bản ghi hiện có.
3. Kiểm tra đăng nhập bằng tài khoản cũ và luồng cấp vé/xác minh. Sau khi nghiệm thu bản sao, áp cùng script trên DB `smartparking` chính theo quy trình triển khai của nhóm.

## Cấu hình và chạy

Với Neon, lấy hostname từ **Connect** khi đang chọn đúng branch và dùng `SSL Mode=VerifyFull`. Lưu các biến cần nạp riêng của Core trong `scripts/.env.parking-operations`.

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

Để bật nút **Đăng nhập bằng Google**, dùng OAuth client loại **Web application** trong [Google Cloud](https://developers.google.com/identity/gsi/web/guides/get-google-api-clientid). Trong **Authorized JavaScript origins**, thêm `http://localhost` và origin của giao diện bạn mở, ví dụ `http://localhost:5295` nếu mở từ Core hoặc `http://localhost:5173` nếu dùng Vite. Origin gồm giao thức, tên máy chủ và cổng, không có `/app/`.

Khi chạy Core ở môi trường `Development`, lưu Client ID vào User Secrets từ thư mục repo:

```powershell
dotnet user-secrets set 'GoogleAuth:ClientId' '<OAUTH_WEB_CLIENT_ID>' --project .\backend\src\SmartParking.Api
```

Có thể đặt `GoogleAuth__ClientId=<OAUTH_WEB_CLIENT_ID>` trong `scripts/.env.parking-operations` nếu chạy bằng biến môi trường. Sau khi cấu hình, khởi động lại Core và kiểm tra `GET /api/v1/auth/google/challenge` trả về `data.enabled: true`. Luồng này dùng tài khoản khách đã đăng ký và xác minh địa chỉ Gmail trùng khớp trong `smartparking`. Khi triển khai nhiều instance Core, giữ các yêu cầu trong cùng một lần đăng nhập trên cùng instance vì mã xác thực tạm thời nằm trong bộ nhớ.

Trong PowerShell, đứng ở thư mục repo rồi chạy Parking Core.

```powershell
. .\scripts\Load-Env.ps1 -EnvFile .\scripts\.env.parking-operations
dotnet run --project backend/src/SmartParking.Api
```

Nếu chính sách PowerShell chặn `Load-Env.ps1`, nạp file bằng lệnh tương tác dưới đây rồi chạy Core trong cùng cửa sổ.

```powershell
Get-Content -LiteralPath .\scripts\.env.parking-operations | ForEach-Object {
    if ($_ -match '^\s*([A-Za-z_][A-Za-z0-9_]*)=(.*)$') {
        [Environment]::SetEnvironmentVariable($Matches[1], $Matches[2], 'Process')
    }
}
```

SQL Editor có thể chạy migration từ Neon Console. Để thử đăng nhập, cấp vé và xác minh với dữ liệu thật, chạy Core từ môi trường kết nối được PostgreSQL tới Neon.
