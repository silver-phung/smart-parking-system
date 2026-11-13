# Parking Operations Service

## Mục tiêu và trạng thái

Service này là scaffold cho bounded context **Parking Operations**, bao gồm cả:

- `STAFF`: vận hành cổng (`GATE`) và xác minh ô (`SLOT_VERIFIER`/`BOTH`).
- `MANAGER`: điều hành bãi, quản lý trạng thái bãi/ô và giám sát vận hành.

Đây **chưa phải** bản nhân bản của Staff từ monolith. Chưa triển khai nghiệp vụ
độc lập, chưa có database riêng và chưa được dùng trong demo chính.

Trong giai đoạn hiện tại, backend monolith vẫn là source of truth cho identity,
role, assignment, parking master data, session, payment, audit và các API vận hành.
Không để service này truy cập trực tiếp database của monolith.

Demo vẫn chạy bằng Core monolith:

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
