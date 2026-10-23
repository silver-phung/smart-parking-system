# Kết nối database Neon dùng chung

Mỗi thành viên cài **.NET 10 SDK**, có Internet và nhận **username/password DB** từ người quản lý Neon. Không cần cài PostgreSQL, Docker hoặc deploy web.

Thông tin đích của nhóm nằm trong `scripts/shared-database.json`: branch Neon `production`, database `smartparking`. Đây là branch của Neon, khác với nhánh code GitHub `duong`/`develop`. Username của các thành viên có thể khác nhau, nhưng phải có quyền truy cập cùng database này.

## Windows — chạy lần đầu

Mở PowerShell tại thư mục repo, chạy:

```powershell
Set-ExecutionPolicy -Scope Process -ExecutionPolicy RemoteSigned
.\scripts\Connect-SharedDatabase.ps1 -Configure -RunBackend
```

Nếu PowerShell hỏi xác nhận chính sách, nhập `Y`. Thiết lập này chỉ áp dụng cho cửa sổ PowerShell hiện tại; không cần mở bằng quyền quản trị. Nếu chính sách máy công ty chặn, cần nhờ quản trị viên hỗ trợ.

Nhập **username DB** và **password DB** nhận từ người quản lý. Đây là tài khoản Postgres trong hộp **Connect** của Neon, không phải email/mật khẩu đăng nhập website Neon. Mật khẩu không hiện khi gõ.

## macOS — chạy lần đầu

Mở Terminal tại thư mục repo, chạy:

```bash
bash scripts/connect-shared-database.sh --configure --run
```

Nhập username/password DB như trên. Không cần `chmod` vì lệnh gọi trực tiếp qua `bash`.

## Những lần sau

Windows:

```powershell
.\scripts\Connect-SharedDatabase.ps1 -RunBackend
```

Nếu mở cửa sổ PowerShell mới và bị chặn script, chạy lại lệnh `Set-ExecutionPolicy -Scope Process -ExecutionPolicy RemoteSigned` ở trên.

macOS:

```bash
bash scripts/connect-shared-database.sh --run
```

Không có `-RunBackend`/`--run`: chỉ kiểm tra DB rồi thoát. Khi đổi mật khẩu DB, chạy lại lệnh cấu hình lần đầu. Dùng `-Help` trên Windows hoặc `--help` trên macOS để xem tham số.

## Khi nào biết đã kết nối thành công?

Script phải in dòng **`OK: query thanh cong. Database: smartparking | User: ...`**. Công cụ mở kết nối TLS và thực sự chạy:

```sql
SELECT current_database(), current_user;
```

Chỉ khi query thành công, thông tin mới được lưu vào **User Secrets của máy đó**, với tên `ConnectionStrings:DefaultConnection`. Mật khẩu không được đưa vào tham số dòng lệnh hoặc ghi ra repo. User Secrets dành cho phát triển cục bộ, không phải kho mã hóa mật khẩu.

Khi dùng tùy chọn chạy backend, script truyền cấu hình đã kiểm tra qua biến `ConnectionStrings__DefaultConnection` cho tiến trình backend, tránh bị cấu hình môi trường cũ ghi đè. Nếu chạy API trực tiếp ngoài script, cần tự kiểm tra các biến môi trường cũ trên máy.

## Phạm vi của bộ script

- Bộ script kiểm tra khả năng đăng nhập/query DB và cấp cấu hình cho backend. Bản backend nền tảng hiện tại **chưa đăng ký DbContext/repository PostgreSQL**; phần đó cần dùng `ConnectionStrings:DefaultConnection` khi triển khai chức năng dữ liệu. API báo `Healthy` không có nghĩa đã truy cập DB.
- Script không tạo/xóa bảng, không chạy migration, không seed dữ liệu và không kiểm tra quyền trên từng bảng nghiệp vụ. Nếu DB đã bị xóa bảng, người phụ trách DB cần triển khai schema phù hợp một lần.
- Không commit mật khẩu, connection string có mật khẩu hoặc `secrets.json`. Hostname/database trong file cấu hình chung không chứa mật khẩu.
- Đóng terminal chỉ dừng backend local; dữ liệu Neon đã lưu vẫn nằm trên cloud.

Nếu thấy lỗi `28P01`, lấy lại đúng username/password trong **Neon → Connect** rồi cấu hình lại. Nếu lỗi mạng/TLS, kiểm tra Internet và khả năng truy cập cổng `5432`. Lần chạy đầu cần Internet để .NET tải thư viện Npgsql.

## Kiểm tra công cụ dành cho người bảo trì

```bash
dotnet run --project scripts/SharedDatabase.Tests
```

Bộ kiểm tra này chạy offline, không truy cập Neon và không ghi User Secrets. Nó kiểm tra mật khẩu có ký tự đặc biệt, đích kết nối/TLS và việc chỉ lưu cấu hình sau khi query thành công.
