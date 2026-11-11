# Bàn giao vận hành bãi

Ngày 02/11/2026. Bản triển khai bổ sung khu bãi, chính sách có phiên bản, vé và lượt chờ, xác minh xe/nhiên liệu và chuyển ô. Mã nguồn và kiểm thử tự động có trong nhánh hiện tại. Chưa áp migration lên DB dùng chung, chưa duyệt thay nhóm các tham số kinh doanh và chưa xác nhận nghiệm thu môi trường thật.

## Phạm vi nghiệp vụ và dữ liệu

Tên task là **Vận hành bãi — Khu bãi, chính sách, vé và xác minh xe**. Tên task mô tả nghiệp vụ. Tên database mô tả nhóm dữ liệu do dịch vụ quản lý.

| Nghiệp vụ | Bảng mới hoặc mở rộng trong `smartparking` | API tương ứng |
| --- | --- | --- |
| Cấu hình khu, tầng và ô | Bảng mới `parking_zones`. Mở rộng `parking_floors` để gắn khu, `parking_slots` để cấu hình nhiên liệu. | `GET/POST zones`, `PUT zones/{id}`, `GET layout`, `PUT floors/{id}/zone`, `PUT slots/{id}/fuel` |
| Chính sách có phiên bản | Bảng mới `parking_policies`. `bookings` và `parking_sessions` lưu ID phiên bản áp dụng. | `GET/POST policies`, `GET policies/effective` |
| Cấp, tra và xử lý vé | Bảng mới `parking_tickets`. `parking_sessions` lưu lượt xe chờ, mã sự kiện và liên kết ảnh đầu vào. | `POST entries`, `GET sessions?waitingOnly=true`, `GET tickets/{code}`, `POST tickets/{code}/change` |
| Xác minh xe và chuyển ô | `parking_sessions` lưu biển số chính thức, nhiên liệu, người/thời điểm xác minh và ô hiện tại. `parking_slots` lưu tình trạng ô; `parking_dispatches` và `audit_logs` lưu lịch sử. | `POST sessions/{id}/verify`, `POST sessions/{id}/move`, `GET sessions/{id}/evidence` |

Các API trong bảng có prefix `/api/v1/parking-lots/{lotId}/operations`. Parking Core quản lý dữ liệu vận hành bãi trong `smartparking`. Nhân viên cấp vé tại cổng, rồi xác minh biển số, nhiên liệu và ô dựa trên xe thực tế.

Nghiệp vụ vận hành bãi nằm trong `SmartParking.Application/UseCases/ParkingOperations`. Controller trong `SmartParking.Api` xử lý HTTP và JWT. `SmartParking.Persistence` quản lý EF Core, transaction và migration. `SmartParking.Infrastructure` tích hợp lưu ảnh.

## Vị trí giao diện

- Admin: `/app/admin/parking-settings`, truy cập qua mục **Khu & chính sách**. Có thêm/sửa/đóng khu, gắn tầng, tạo tầng/ô, tọa độ, tương thích nhiên liệu, bản nháp/phiên bản chính sách và xử lý thay/thu hồi vé.
- Staff: `/app/staff?view=entry` cấp vé, `view=verify` xác minh/chuyển xe, `view=tickets` tra/báo mất vé. Xe ra có tra theo mã vé và gửi mã vé đã đối chiếu tới API check-out.
- Màn xác minh cũ tiếp tục xử lý phiên `LEGACY`. Phiên đã cấp vé mới đi qua màn xác minh tại ô.

## API và quyền

Prefix là `/api/v1/parking-lots/{lotId}/operations`. Tất cả endpoint yêu cầu JWT. Actor được lấy từ tài khoản đăng nhập; không nhận actor do client khai báo. Quyền được đối chiếu lại với tài khoản/phân công trong database.

| Method và path | Công dụng | Quyền |
| --- | --- | --- |
| GET `zones`, `layout` | Khu và metadata tầng/ô/tọa độ/tương thích | Nội bộ đúng bãi |
| POST `zones`, PUT `zones/{id}` | Tạo/sửa/đóng khu | Admin |
| PUT `floors/{id}/zone` | Gắn tầng chưa có xe/lịch sử vào khu | Admin |
| PUT `slots/{id}/fuel` | Cấu hình ANY/PETROL/ELECTRIC/HYBRID | Admin |
| GET `policies`, `policies/effective?zoneId=` | Lịch sử và chính sách đang áp dụng | Nội bộ đúng bãi |
| POST `policies` | Tạo nháp hoặc phiên bản đã duyệt | Admin |
| POST `entries` | Cấp vé và tạo lượt chờ | GATE đúng bãi hoặc cấp quản lý có quyền |
| GET `sessions?waitingOnly=true` | Hàng chờ lưu trong core | Nội bộ đúng bãi |
| GET `tickets/{code}` | Tra vé, kể cả LOST/REPLACED/REVOKED | Nội bộ đúng bãi |
| POST `tickets/{code}/change` | LOST, REPLACE, REVOKE | GATE báo mất; Admin/Manager thay/thu hồi |
| POST `sessions/{id}/verify` | Xác minh chính thức tại ô | SLOT_VERIFIER đúng bãi |
| POST `sessions/{id}/move` | Di chuyển xe đã xác minh | SLOT_VERIFIER đúng bãi |
| GET `sessions/{id}/evidence` | Ảnh đã gắn với lượt gửi | Nội bộ đúng bãi |

Response thành công là `{ "data": ... }`. Lỗi nghiệp vụ có HTTP 400/403/404/409 và `{ "code": "...", "message": "..." }`. Các code chính gồm `POLICY_UNAPPROVED`, `EVENT_MISMATCH`, `TICKET_IN_USE`, `TICKET_USED`, `CAPACITY_EXHAUSTED`, `INVALID_IMAGE`, `STALE_DATA`, `PLATE_CONFLICT`, `SLOT_UNAVAILABLE`, `SLOT_CONFLICT`, `INCOMPATIBLE_FUEL`, `DUPLICATE`.

## Ví dụ hợp đồng

Các mã/ID dưới đây là ví dụ hợp đồng, không phải dữ liệu thật hay chính sách đã duyệt.

Bản nháp chính sách chưa có quyết định về tham số:

```json
{
  "zoneId": null,
  "effectiveFrom": "2026-11-03T02:00:00Z",
  "approve": false,
  "decisionReference": "Đang chờ nhóm xác nhận các tham số",
  "holdMinutes": null,
  "earlyArrivalMinutes": null,
  "lateArrivalMinutes": null,
  "cancellationNoticeMinutes": null,
  "depositAmount": null,
  "nearFullThreshold": null,
  "feeUnitMinutes": null,
  "feeRounding": null,
  "noShowAction": null,
  "cancellationAction": null,
  "rateSelection": null
}
```

Ngày hiệu lực trên chỉ là ví dụ. Để duyệt, cần điền đủ tham số, nguồn quyết định và thời điểm chưa qua; request gửi `approve = true`. API trả ID phiên bản, `createdBy`, `approvedBy`, `approvedAt` và các tham số đã lưu. Thay đổi tiếp theo dùng POST tạo ID mới. Hủy booking đọc chính sách đã gắn với booking, không đọc lại phiên bản mới nhất.

Ví dụ cấp vé tại cổng

```json
{
  "eventId": "0146c276-d37d-4e06-a219-6cd65c9775be",
  "zoneId": 1,
  "ticketCode": "VE-20261102-001",
  "imageId": null,
  "manualReason": "Tiếp nhận theo vé, chuyển kiểm tra xe thực tế tại ô"
}
```

Khi có ảnh, tải lên qua `POST /api/v1/images` và truyền `imageId` vào `entries`. Nếu tải ảnh lỗi, thử tải lại trước khi cấp vé. Giữ nguyên EventId và payload khi gửi lại cùng một lượt.

Phần dữ liệu phiên trả về gồm `id`, `lotId`, `zoneId`, `policyId`, `eventId`, `imageId`, `temporaryPlateNumber`, `plateNumber`, `verificationStatus`, `status`, `fuelType`, `currentSlotId`, `slotCode`, `revision`, `checkInTime`, `verifiedAt`, `ticketCode`, `ticketStatus`. Lượt chờ mới có `plateNumber = ""`, `currentSlotId = null`, `verificationStatus = "WAITING_VERIFICATION"`, `status = "ACTIVE"`.

Xác minh xe tại ô:

```json
{
  "actualSlotId": 12,
  "plateNumber": "51F12345",
  "fuelType": "PETROL",
  "expectedRevision": 1,
  "vehicleObserved": true,
  "reason": "Đối chiếu biển số trên xe và vé tại ô"
}
```

Chuyển xe đã đỗ:

```json
{
  "actualSlotId": 13,
  "expectedRevision": 2,
  "oldSlotObservedEmpty": true,
  "vehicleObservedAtDestination": true,
  "reason": "Chuyển xe theo yêu cầu vận hành, đã đối chiếu cả hai ô"
}
```

Thay vé sau đối chiếu:

```json
{ "action": "REPLACE", "replacementCode": "VE-20261102-NEW", "reason": "Quản lý đã đối chiếu vé mất với xe và phiên gốc" }
```

Check-out vẫn dùng `/api/v1/parking-sessions/{id}/check-out`; bổ sung `ticketCode` bên cạnh `confirmedPlateNumber`. Phiên mới phải VERIFIED, vé ISSUED khớp phiên và đủ điều kiện tài chính hiện có. Mất/thu hồi vé không tự cho ra. Khi xác nhận xe thực sự ra, vé được thu hồi trong cùng transaction và trả đúng ô hiện tại. Việc tính phí, khoản thu và miễn trừ thuộc module Nam Anh; UI không được tự đặt PAID để thử luồng.

## Điểm nhận của từng người

| Người | Dữ liệu đã có để ghép | Việc cần hoàn tất trong phần của người nhận |
| --- | --- | --- |
| Trí | SessionId/EventId/vé/khu, helper sức chứa có `zoneId`, chính sách hiệu lực | Booking theo khung giờ/khu, hết hạn/no-show, liên kết đặt → vé/phiên, event pipeline bền, môi trường HTTPS và ảnh riêng tư |
| Nam Anh | `session.PolicyId`, `booking.PolicyId`, tra vé, đơn vị/làm tròn/mốc chọn giá | Bảng giá/fee assessment có phiên bản, khoản thu/biên lai, điều kiện đủ phí và ngoại lệ |
| Thức | `layout` có khu/tầng/ô/tọa độ/nhiên liệu/trạng thái/thời điểm; slot chỉ OCCUPIED sau verify | Public/internal projection cho viewer, snapshot/version, ghép 3D và đồng bộ |
| Song | API khu/chính sách, vé/xác minh, API adapter dùng apiRequest | Hành trình booking, ghép 3D, bản đồ, kiểm tra mobile toàn hệ thống |
| Phụng và Danh | Test nghiệp vụ và bộ PostgreSQL tách biệt | Rà lại quyền/sức chứa và nghiệm thu trên DB/thiết bị thật |

`layout` là API **nội bộ**; không đưa token hoặc response của nó vào trang Guest. Public viewer cần projection riêng của Thức. Booking hiện lưu PolicyId và đọc quy tắc hủy của phiên bản gắn với booking. Bộ xử lý hết hạn/no-show và phí chưa được thay bằng mô phỏng trong phần này.

## Migration và triển khai kiểm thử

Core có migration khu/vé, liên kết ảnh đầu vào và lưu lý do xác minh/chuyển ô đầy đủ. Nâng cấp database `smartparking` hiện có bằng [hướng dẫn Neon](../operations/neon-microservices.md).

Sau migration, Admin đối chiếu khu backfill với sơ đồ thực, thêm hai cấu trúc FIXED INSIDE/EXTERNAL **trên DB kiểm thử**, cấu hình và duyệt tham số có nguồn quyết định. Chỉ chính sách đã duyệt và có hiệu lực mới mở luồng nhận xe.

EF migration history hỗ trợ chạy triển khai lại mà không lặp backfill. Trước khi phát sinh vé mới có thể dùng Down trên DB kiểm thử theo quy trình đã review. Sau khi có phiên cấp vé mới, Down của migration khu/vé cố ý từ chối để không mất lịch sử và tạo lại unique index cũ trên nhiều biển số đang chờ rỗng; ưu tiên bản sửa tiến hoặc phục hồi backup vào DB riêng để đối chiếu.

## Kiểm thử và phần chưa nghiệm thu

```powershell
dotnet test backend/tests/SmartParking.Tests/SmartParking.Tests.csproj
npm --prefix frontend/smart-parking test
```

`ParkingOperationsTests` kiểm tra replay EventId, sức chứa FIXED, biển số chính thức trùng, nhiên liệu, tầng/ô lỗi, duty, revision, di chuyển, mất/thay vé, quyền sử dụng ảnh, phiên bản chính sách và chặn đường xác minh cũ. Kiểm thử constraint và tranh chấp PostgreSQL nằm trong bộ riêng.

`ParkingOperationsPostgresTests` chỉ chạy khi biến môi trường `PARKING_TEST_POSTGRES` trỏ tới **database kiểm thử riêng**. Mỗi ca tạo/xóa schema ngẫu nhiên `s4_test_*` của riêng ca, chạy migration từ dữ liệu cũ có phiên/booking, kiểm tra backfill/chạy lại, EventId qua nhiều context, và hai kết nối cùng tranh một ô. Không tự lấy connection từ User Secrets/database chung. Thiếu biến môi trường thì ba ca này được ghi Skipped.

Các bước nghiệm thu còn lại gồm migration/backfill và tranh chấp trên PostgreSQL thật, lưu ảnh Cloudinary, xác nhận tham số chính sách, ghép booking/phí/event/3D, kiểm tra cấu trúc INSIDE/EXTERNAL trên DB kiểm thử và thử trên PC, điện thoại.

Kết quả kiểm tra local ngày 02/11/2026: build toàn bộ backend không có warning/error; backend có 396 test đạt, 3 ca PostgreSQL Skipped; frontend có 19 test API/session và 55 test UI/service đạt. Cả hai DbContext không có thay đổi model chưa được migration bao phủ. Các ca PostgreSQL cần biến môi trường riêng như trên và không được cộng vào số ca đã đạt khi Skipped.
