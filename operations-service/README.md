# Parking Operations Service

## Mục tiêu và trạng thái

Service này sở hữu bounded context **Parking Operations**. Các vertical slice hiện có là quản lý ca và bàn giao ca tiền mặt:

- Manager lập ca 05–13, 13–21, 21–05 theo ngày nghiệp vụ, phân công Staff, bắt đầu và chốt ca.
- Nhân viên chỉ được xếp ca nếu đang có assignment STAFF hiệu lực tại bãi trong Core.
- Manager tạo handover cho hai ca liền kề và hai nhân viên thuộc roster tương ứng.
- Nhân viên giao và nhận tự xác nhận số tiền kiểm đếm bằng tài khoản riêng.
- Hai số tiền khớp thì handover hoàn tất; nếu lệch, record chuyển `DISPUTED` để Manager xử lý.

Operations lưu handover trong database riêng qua `ConnectionStrings:OperationsConnection`.
Không dùng database `smartparking` hoặc `BILLING_SERVICE_DB` cho dữ liệu handover.

Core vẫn là source of truth cho identity, role, assignment và parking master data;
Payment là source of truth cho transaction. Operations gọi Core API bằng JWT đang
đăng nhập để kiểm tra quyền bãi/nhân viên và chỉ lưu mã tham chiếu, không truy cập
trực tiếp database Core/Payment.

Các luồng parking khác vẫn chạy bằng Core monolith:

```powershell
dotnet run --project ..\backend\src\SmartParking.Api\SmartParking.Api.csproj --launch-profile http
```

## Ownership dự kiến

| Nhóm dữ liệu/nghiệp vụ | Giai đoạn đầu |
|---|---|
| User, role, JWT, refresh token | Core monolith |
| Staff/Manager assignment vào bãi | Core monolith |
| Parking lot, floor, slot master data | Core monolith |
| Parking session và payment clearance | Core-authoritative |
| Check-in/check-out orchestration | Operations, gọi Core API |
| Dispatch và slot verification | Operations, chuyển dần theo vertical slice |
| OCR workflow | Operations orchestration, AI service vẫn riêng |
| Audit trung tâm | Core hoặc audit contract dùng chung |

Operations Service không được sao chép database Core. Mọi thay đổi trạng thái
Core phải đi qua API/event contract đã thống nhất.

## Quy trình tách theo giai đoạn

### Phase 0 — Chốt boundary và contract

Trước khi viết business code:

1. Liệt kê endpoint monolith hiện tại và xác định endpoint nào thuộc Operations.
2. Chốt ownership cho từng entity, đặc biệt `ParkingSession`, `ParkingDispatch`,
   `ParkingSlot` và `Payment`.
3. Định nghĩa request/response, error code, idempotency và correlation ID.
4. Chốt policy quyền:
   - Manager có quyền vận hành đầy đủ trong bãi được phân công.
   - Staff phải bị giới hạn bởi duty `GATE`, `SLOT_VERIFIER` hoặc `BOTH`.
5. Định nghĩa timeout, retry, fallback và hành vi khi Core/AI không khả dụng.

### Phase 1 — Chuẩn hóa scaffold

1. Đổi tên namespace/project nội bộ từ tên Staff cũ sang `ParkingOperations`.
2. Giữ các layer `WebAPI`, `Application`, `Domain`, `Infrastructure`,
   `Persistence` nhưng chỉ thêm abstraction thuộc Operations.
3. Không copy nguyên các service monolith sang đây.
4. Tạo Core API client typed, DTO contract và adapter; không dùng EF context của Core.
5. Thống nhất cách validate JWT, revoke assignment và ghi audit.

### Phase 2 — Tách vertical slice đầu tiên

Ưu tiên `Manager read workspace`:

1. Đọc bãi được phân công.
2. Đọc capacity, active sessions và trạng thái ô.
3. Chưa cho phép ghi nếu chưa có authorization contract và integration test.
4. So sánh response với monolith để bảo đảm parity.

### Phase 3 — Tách nghiệp vụ Staff/Manager

Triển khai từng slice, không chuyển toàn bộ cùng lúc:

1. Staff gate: check-in và check-out.
2. OCR detect/confirm và upload ảnh.
3. Dispatch: tạo, phân lại, huỷ.
4. Slot verification: xác nhận đúng ô và báo sai ô.
5. Manager: đóng/mở bãi và cập nhật trạng thái ô.
6. Payment clearance vẫn do Core/payment flow quyết định; Operations không tự
   tạo trạng thái `PAID` hoặc bypass thanh toán.

Mỗi slice phải có contract test, authorization test, concurrency test và audit test.

### Phase 4 — Chạy song song và nghiệm thu

1. Chạy monolith và Operations trong staging.
2. Đối chiếu kết quả đọc, trạng thái và audit giữa hai bên.
3. Test revoke assignment/user inactive phải có hiệu lực.
4. Test lỗi timeout, retry, duplicate request và Core unavailable.
5. Chuẩn bị feature flag hoặc route switch để rollback về monolith.

### Phase 5 — Cut-over

Chỉ chuyển route sau khi slice đạt đủ điều kiện:

- Contract tests và integration tests đạt.
- Authorization theo role, lot và duty đạt.
- Không có quyền ghi trực tiếp database Core.
- Có logging, correlation ID, health check và metrics cơ bản.
- Có rollback rõ ràng.

Chuyển từng slice, không chuyển toàn bộ service trong một lần. Monolith vẫn là
fallback cho đến khi Operations chạy ổn định qua một chu kỳ nghiệm thu đầy đủ.

## Shift Management API

Base path: `/api/v1/operations/shifts`. Tất cả endpoint yêu cầu JWT Core và
Manager phải đang được phân công vào bãi tương ứng.

| Method | Route | Tác dụng |
|---|---|---|
| `POST` | `/api/v1/operations/shifts` | Manager tạo ca đã lên lịch |
| `GET` | `/api/v1/operations/shifts?lotId=…&businessDate=…` | Manager xem roster bãi; Staff chỉ xem ca mình được xếp |
| `POST` | `/api/v1/operations/shifts/{id}/assignments` | Manager xếp nhân viên Core-assigned vào ca |
| `POST` | `/api/v1/operations/shifts/{id}/start` | Bắt đầu ca đã có roster |
| `POST` | `/api/v1/operations/shifts/{id}/complete` | Chốt ca đang diễn ra |

Trạng thái ca: `PLANNED → IN_PROGRESS → COMPLETED`. Roster chỉ chỉnh được khi
`PLANNED`. Payroll và tính lương không thuộc phạm vi Operations hiện tại.

## Cash Handover API

Base path: `/api/v1/operations/handovers`. Tất cả endpoint nghiệp vụ yêu cầu JWT
từ Core; tạo/list cần `MANAGER`, xác nhận/từ chối cần `STAFF` hoặc `MANAGER` là
đúng một trong hai nhân viên của handover và đang được phân công tại bãi.

| Method | Route | Tác dụng |
|---|---|---|
| `POST` | `/api/v1/operations/handovers` | Manager tạo handover giữa hai shift ID liền kề |
| `GET` | `/api/v1/operations/handovers?lotId=…&businessDate=…` | Manager xem cả bãi; Staff chỉ xem handover mình tham gia |
| `POST` | `/api/v1/operations/handovers/{id}/confirm` | Mỗi bên ghi số tiền mặt tự kiểm đếm |
| `POST` | `/api/v1/operations/handovers/{id}/dispute` | Người giao/nhận từ chối và nêu lý do |

Shift codes: `MORNING` (05–13), `AFTERNOON` (13–21), `NIGHT` (21–05).
Ngày nghiệp vụ là ngày bắt đầu ca; thời điểm lưu UTC với giờ vận hành Việt Nam
(UTC+7). Ca `NIGHT` kết thúc 05:00 ngày tiếp theo. Handover liên kết hai shift ID;
chỉ cho phép chuyển ca liền kề và đúng ngày (handover `NIGHT → MORNING` sang sáng
ngày hôm sau). Mỗi bãi/ngày/ca chỉ có một record.

| Method | Route | Tác dụng |
|---|---|---|
### Cấu hình và migration

Operations cần một PostgreSQL database riêng. Tạo database cho service trên Neon
trước khi cấu hình, rồi đặt connection string qua User Secrets hoặc môi trường:

```powershell
dotnet user-secrets set "ConnectionStrings:OperationsConnection" "<operations-db-connection-string>" --project src\ParkingOperations.WebAPI
dotnet user-secrets set "Jwt:SecretKey" "<same-secret-used-by-core-at-least-32-bytes>" --project ..\backend\src\SmartParking.Api
```

`ParkingOperations.WebAPI` dùng cùng `UserSecretsId` với Core để kiểm tra access
token Core. Không dùng secret minh họa làm secret thật. Cấu hình `CoreApi:BaseUrl`
cho địa chỉ Core khi chạy ngoài máy local; Production yêu cầu HTTPS.

Áp EF migration lên **database Operations riêng**:

```powershell
dotnet ef database update --project src\ParkingOperations.Persistence --startup-project src\ParkingOperations.WebAPI
```

SQL idempotent được xuất tại `neon/sql/operations-handover-v1.sql` ở workspace
root. Chỉ chạy file này khi Neon đang chọn database Operations mới; không chạy vào
`smartparking` hay `BILLING_SERVICE_DB`.

Khi phục vụ UI từ origin khác, cấu hình `Cors:AllowedOrigins` trong Operations và
đặt `VITE_OPERATIONS_API_BASE_URL` lúc build frontend. Development mặc định gọi
`http://localhost:5174`.

## Điều kiện chưa được phép làm

- Không dùng Operations Service cho demo chính hiện tại.
- Không tự tạo user/role/assignment độc lập.
- Không tạo database bản sao của Core.
- Không cho Manager/Staff vượt quyền chỉ vì role nằm trong JWT.
- Không tính phí, ghi `PAID` hoặc mở cổng giả lập trong Operations.
- Không copy-and-modify các service monolith mà không có contract và test parity.

## Build scaffold

Từ thư mục này:

```powershell
dotnet build ParkingOperations.slnx
dotnet run --project src\ParkingOperations.WebAPI\ParkingOperations.WebAPI.csproj
```

Tên thư mục và project chính đã thống nhất theo tiền tố `ParkingOperations.*`.
Namespace nghiệp vụ sẽ tiếp tục được chuẩn hóa khi bắt đầu Phase 1, không ảnh hưởng
đến demo monolith hiện tại.
