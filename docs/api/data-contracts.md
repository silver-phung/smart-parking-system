# Miền giá trị và hợp đồng dữ liệu Smart Parking

**Trạng thái:** Hiện hành cho enum/DTO đã xác minh; miền bổ sung được đánh dấu ĐỀ XUẤT.  
**Chủ sở hữu:** Backend/API và Frontend.  
**Rà soát gần nhất:** 28/10/2026.  
**Phạm vi:** Enum, payload, projection public/internal/device và DTO đầu vào.

**Mốc đối chiếu:** `Sprint3/demo` tại `4bed0c8a929fe050d644aa4e07c0a1caa77d8d0e`, ngày 28/10/2026. Xem [README](../README.md) để biết nguồn, quy ước và trạng thái.

Miền hiện có được trích từ constant, CHECK và DTO. Tên trạng thái phân biệt hoa/thường theo hợp đồng; không chuyển lowercase event thành uppercase core bằng cách đổi chữ rồi ghi thẳng DB. Miền đề xuất được tách riêng ở cuối.

## 1 Vai trò và trạng thái core hiện có

### ApprovalStatuses

users.approval_status; Admin tạo nội bộ hiện APPROVED, không có API duyệt riêng đầy đủ.

| Tên constant C# | Giá trị gửi/lưu |
|---|---|
| `Pending` | `PENDING` |
| `Approved` | `APPROVED` |
| `Rejected` | `REJECTED` |

[Nguồn](https://github.com/silver-phung/smart-parking-system/blob/4bed0c8a929fe050d644aa4e07c0a1caa77d8d0e/backend/src/SmartParking.Domain/Constants/ApprovalStatuses.cs).

### BookingStatuses

bookings.status; EXPIRED có miền nhưng chưa thấy scheduler tự hết hạn.

| Tên constant C# | Giá trị gửi/lưu |
|---|---|
| `Pending` | `PENDING` |
| `CheckedIn` | `CHECKED_IN` |
| `Cancelled` | `CANCELLED` |
| `Expired` | `EXPIRED` |

[Nguồn](https://github.com/silver-phung/smart-parking-system/blob/4bed0c8a929fe050d644aa4e07c0a1caa77d8d0e/backend/src/SmartParking.Domain/Constants/BookingStatuses.cs).

### DispatchModes

parking_dispatches.dispatch_mode; AUTO tìm ô, MANUAL chỉ định ô; không là kiểu nhiên liệu.

| Tên constant C# | Giá trị gửi/lưu |
|---|---|
| `Auto` | `AUTO` |
| `Manual` | `MANUAL` |

[Nguồn](https://github.com/silver-phung/smart-parking-system/blob/4bed0c8a929fe050d644aa4e07c0a1caa77d8d0e/backend/src/SmartParking.Domain/Constants/DispatchModes.cs).

### DispatchStatuses

parking_dispatches.status; tạo trực tiếp ASSIGNED, đổi ô chỉ PENDING/ASSIGNED.

| Tên constant C# | Giá trị gửi/lưu |
|---|---|
| `Pending` | `PENDING` |
| `Assigned` | `ASSIGNED` |
| `Occupied` | `OCCUPIED` |
| `WrongSlot` | `WRONG_SLOT` |
| `Cancelled` | `CANCELLED` |

[Nguồn](https://github.com/silver-phung/smart-parking-system/blob/4bed0c8a929fe050d644aa4e07c0a1caa77d8d0e/backend/src/SmartParking.Domain/Constants/DispatchStatuses.cs).

### EventProcessingStatus

Kết quả xử lý event; pending_verification có constant, không mặc định hàng đợi bền.

| Tên constant C# | Giá trị gửi/lưu |
|---|---|
| `Accepted` | `accepted` |
| `Rejected` | `rejected` |
| `Duplicate` | `duplicate` |
| `PendingVerification` | `pending_verification` |

[Nguồn](https://github.com/silver-phung/smart-parking-system/blob/4bed0c8a929fe050d644aa4e07c0a1caa77d8d0e/backend/src/SmartParking.Domain/Entities/ParkingEventLog.cs).

### ParkingEventType

IngestEventRequest.EventType; source cổng/slot khác nhau, cần đúng actor.

| Tên constant C# | Giá trị gửi/lưu |
|---|---|
| `VehicleEntered` | `VehicleEntered` |
| `SensorTriggered` | `SensorTriggered` |
| `SlotParked` | `SlotParked` |
| `VehicleExited` | `VehicleExited` |

[Nguồn](https://github.com/silver-phung/smart-parking-system/blob/4bed0c8a929fe050d644aa4e07c0a1caa77d8d0e/backend/src/SmartParking.Domain/Entities/ParkingEventLog.cs).

### ParkingSessionStatus

EventParkingSession.Status; repository bộ nhớ, không phải enum core.

| Tên constant C# | Giá trị gửi/lưu |
|---|---|
| `PendingSlot` | `pending_slot` |
| `Parked` | `parked` |
| `Exited` | `exited` |

[Nguồn](https://github.com/silver-phung/smart-parking-system/blob/4bed0c8a929fe050d644aa4e07c0a1caa77d8d0e/backend/src/SmartParking.Domain/Entities/EventParkingSession.cs).

### ParkingSessionWarning

Cảnh báo event session; checker hiện AlwaysCompatible nên không coi tương thích đã có.

| Tên constant C# | Giá trị gửi/lưu |
|---|---|
| `WrongVehicleType` | `wrong_vehicle_type` |

[Nguồn](https://github.com/silver-phung/smart-parking-system/blob/4bed0c8a929fe050d644aa4e07c0a1caa77d8d0e/backend/src/SmartParking.Domain/Entities/EventParkingSession.cs).

### Roles

roles.code; users.role_id tham chiếu roles. Customer = USER, Guest không có mã.

| Tên constant C# | Giá trị gửi/lưu |
|---|---|
| `Admin` | `ADMIN` |
| `Manager` | `MANAGER` |
| `Staff` | `STAFF` |
| `User` | `USER` |

[Nguồn](https://github.com/silver-phung/smart-parking-system/blob/4bed0c8a929fe050d644aa4e07c0a1caa77d8d0e/backend/src/SmartParking.Domain/Constants/Roles.cs).

### SessionStatuses

parking_sessions.status; PAID/CANCELLED có miền nhưng chưa có lệnh tương ứng đầy đủ.

| Tên constant C# | Giá trị gửi/lưu |
|---|---|
| `Active` | `ACTIVE` |
| `Paid` | `PAID` |
| `Completed` | `COMPLETED` |
| `Cancelled` | `CANCELLED` |

[Nguồn](https://github.com/silver-phung/smart-parking-system/blob/4bed0c8a929fe050d644aa4e07c0a1caa77d8d0e/backend/src/SmartParking.Domain/Constants/SessionStatuses.cs).

### SlotPhysicalStatuses

parking_slots.physical_status; đúng bốn trạng thái, RESERVED không thuộc miền này.

| Tên constant C# | Giá trị gửi/lưu |
|---|---|
| `Empty` | `EMPTY` |
| `Occupied` | `OCCUPIED` |
| `Maintenance` | `MAINTENANCE` |
| `Unavailable` | `UNAVAILABLE` |

[Nguồn](https://github.com/silver-phung/smart-parking-system/blob/4bed0c8a929fe050d644aa4e07c0a1caa77d8d0e/backend/src/SmartParking.Domain/Constants/SlotPhysicalStatuses.cs).

### TokenTypes

user_tokens.token_type; cấu trúc còn dù endpoint đăng ký/xác minh/reset bị gỡ hoặc chưa có.

| Tên constant C# | Giá trị gửi/lưu |
|---|---|
| `EmailVerification` | `EMAIL_VERIFICATION` |
| `PasswordReset` | `PASSWORD_RESET` |

[Nguồn](https://github.com/silver-phung/smart-parking-system/blob/4bed0c8a929fe050d644aa4e07c0a1caa77d8d0e/backend/src/SmartParking.Domain/Constants/TokenTypes.cs).

## 2 Miền kỹ thuật khác hiện có

| Miền | Giá trị/định dạng | Giới hạn |
|---|---|---|
| Biển số core | uppercase/trim; helper gom khoảng trắng, kiểm tra độ dài 3–20 | Không có quy tắc chung loại bỏ dấu chấm/gạch; không tự chứng minh biển số hợp lệ thực tế |
| SlotCode core | StringLength 1–20, unique trong floor | Không cùng regex với event/viewer |
| SlotCode event | `^[A-Z]\d{1,2}-\d{3}$`, ví dụ A1-012 | B1-A01 từ viewer mẫu không khớp regex; cần mapping, không gửi mù |
| ID core | Lot/Floor: int; User/Vehicle/Slot/Session/Booking: long | Không đổi thành UUID chỉ vì mẫu AITasker dùng UUID |
| ID phiên event | Chuỗi session, luồng thường sinh UUID | Không phải parking_sessions.id; bridge cần mapping có lưu |
| FloorId viewer | Chuỗi như B1/B2/G | Không phải parking_floors.id số |
| FuelType event/Gemini | gasoline, electric, unknown theo mô tả DTO/AI | Chưa có CHECK/core field; string payload không phải ràng buộc DB |
| VehicleType viewer | car, ev, moto được mô tả | Đang trộn loại xe/nhiên liệu; sản phẩm hiện phạm vi ô tô |
| IoT simulator EventType | CarParked/CheckIn, CarDeparted/CheckOut, Fault | Khác ParkingEventType; không đồng nhất hai endpoint |
| Ảnh Cloudinary | JPEG/PNG/WebP; mặc định 10 MiB theo cấu hình | FastAPI không tự dùng giới hạn này; cần kiểm tra từng dịch vụ |
| Thời gian | ISO 8601 UTC/offset; lưu timestamptz, hiển thị Asia/Saigon UTC+7 | Không trộn giờ trình duyệt làm căn cứ phí |
| Tiền core | decimal/numeric(12,2) TotalFee; VND theo nghiệp vụ | Chưa có pricing/payment; không tự áp số lẻ hay làm tròn không được duyệt |

## 3 Payload slot public và nội bộ

DTO core có id, floorId, floorName, lotId, lotName, slotCode, physicalStatus, isAvailable, positionX/Y/Z, isActive, createdAt, updatedAt. DTO chi tiết thêm CurrentVehiclePlate/CurrentSessionId; controller hiện chỉ cho Admin nhận nội bộ.

Viewer trả wrapper `{ success, lotId, floorId, total, availableCount, occupiedCount, reservedCount, maintenanceCount, unavailableCount, slots }`; slot còn có CurrentCarPlate, SessionId, VehicleColor/Model/ParkedSinceUtc. Wrapper này không phải public projection đạt yêu cầu vì có dữ liệu nội bộ và trạng thái thứ năm.

**Snapshot đích ĐỀ XUẤT:** dùng ID core ổn định, bốn trạng thái physical, lớp được phân/giữ riêng, updatedAt/version. Public không có ảnh/biển số/session riêng; Web App nội bộ trả trường xe theo actor/sở hữu/bãi/duty. Bảng IoT vật lý của slot cố định dùng device projection riêng và được nhận đầy đủ biển số đã đăng ký của đúng slot. Không công bố các JSON này như API đã có; xem mục API đề xuất ở [04](endpoints.md).

## 4 Hợp đồng nhận diện và xác nhận

FastAPI trả snake_case `plate_number`, `raw_plate_number`, `confidence`, `is_legit`, `vehicle_type`, `vehicle_confidence`, `vehicle_bbox`, `plate_bbox`, `inference_time_ms`; envelope success/message/data. Gemini trả camelCase imageUrl/carPlate/fuelType/carColor/carModel/confidence/isSuccess/errorMessage. Không đổi hai response thành cùng một cấu trúc mà bỏ thông tin lỗi/gốc.

Kết quả AI/OCR ở cổng cần được lưu là nhận dạng tạm gồm raw/normalized/confidence/source/time và tham chiếu ảnh/vé/sự kiện. Kết quả này được phép tạo session ACTIVE chờ xác minh nhưng chưa phải biển số chính thức. Staff điều phối tại ô xác nhận/sửa cần lưu người/thời điểm/lý do, actual slot và liên kết ảnh-phiên-bãi trong cùng kết quả nghiệp vụ. Đây là hợp đồng cần bổ sung, chưa có đầy đủ trong DTO check-in/confirm hiện tại. `unknown` phải được giữ cho tới khi xác minh, không mặc định thành xe điện hoặc xe xăng.

## 5 JSON audit và phân trang

`audit_logs.old_values/new_values` là text JSON sinh theo từng hành động, không có một schema payload chung đã được ràng buộc DB. Khi mở rộng cần version và loại action; không ghi password, raw token, OTP hay secret. Mã action lấy từ service, không sao chép mã ledger của AITasker.

API core thường dùng `{message,data}`; data phân trang dùng items và thông tin trang/tổng theo PagedResult. Tên query `page` và `pageNumber` khác nhau giữa nhóm DTO; theo [04 Endpoints](endpoints.md), không chuẩn hóa chỉ ở frontend khi server chưa hỗ trợ.

## 6 Các miền đề xuất chưa triển khai

| Miền đề xuất | Giá trị dự kiến | Điều kiện trước khi sử dụng |
|---|---|---|
| Nhiệm vụ Staff | GATE, DISPATCH | Chốt DEC-14, bổ sung assignment/guard; không ghi vào roles.code như miền đã có |
| Trạng thái nhận dạng xe vào | PENDING_VERIFICATION, VERIFIED, NEEDS_REVIEW, REJECTED | Tách kết quả OCR tạm khỏi biển số chính thức; Staff tại ô xác minh mà không tạo session thứ hai |
| Khu đỗ | TRANSIENT, FIXED | Chốt khu trong/ngoài và barrier; bổ sung entity/quan hệ |
| Nhiên liệu core | GASOLINE, ELECTRIC, UNKNOWN | Chốt mapping AI lowercase và xác nhận người; tách loại ô tô |
| Khoản thu | PENDING, SUCCEEDED, FAILED, UNKNOWN | Schema tiền, phương thức, chứng từ, chống trùng và đối soát |
| Vé vãng lai | READY, IN_USE, LOST, DAMAGED, RETIRED | Loại mã/thẻ, phát/thu/thay và unique active assignment |
| Hàng việc xác minh slot | PENDING, PROCESSING, CONFIRMED, REJECTED, NEEDS_REVIEW | Lưu bền, actor/time/result; xử lý xong đổi trạng thái chứ không xóa lịch sử |
| Quyền gửi cố định | PENDING, SCHEDULED, ACTIVE, EXPIRED, SUSPENDED, CANCELLED | Kỳ, ô, xe, bãi, thanh toán/duyệt, đổi/gia hạn |
| Report chiếm ô | SUBMITTED, REVIEWING, RESOLVED, REJECTED | Quyền người gửi, bằng chứng, người xử lý và chính sách phạt |

Các miền này dùng để thảo luận thiết kế ở [02 State machines](../product/state-machines.md), không phải enum PostgreSQL hiện tại. Chỉ chuyển sang HIỆN CÓ sau khi code/migration/API đồng bộ.

## 7 Từ điển DTO đầu vào hiện có

Danh mục lấy từ type thực tế của tham số controller, gồm body và query DTO. Cột nullable là metadata C#; không tự thay thế Required hoặc điều kiện nghiệp vụ. Dấu “—” nghĩa không có DataAnnotation trên property, không nghĩa server bỏ mọi kiểm tra. Hợp đồng đề xuất chưa nằm trong danh mục này. Tên JSON theo lower camelCase; query dùng tên trường tương ứng.

### AdminCreateUserRequestDto

| Trường JSON/query | Kiểu .NET | Nullable | DataAnnotation |
|---|---|---|---|
| `username` | `String` | Không | Required; Regex ^[a-zA-Z0-9_.]{3,50}$ |
| `email` | `String` | Không | Required; EmailAddress; MaxLength 255 |
| `password` | `String` | Không | Required; MinLength 8; MaxLength 100 |
| `fullName` | `String` | Không | Required; MaxLength 100 |
| `phoneNumber` | `String` | Có | MaxLength 20; Phone |
| `role` | `String` | Không | Required |
| `isActive` | `Boolean` | Không | — |

[Nguồn DTO](https://github.com/silver-phung/smart-parking-system/blob/4bed0c8a929fe050d644aa4e07c0a1caa77d8d0e/backend/src/SmartParking.Application/DTOs/AdminUsers/AdminUserDtos.cs).

### AdminUpdateRoleRequestDto

| Trường JSON/query | Kiểu .NET | Nullable | DataAnnotation |
|---|---|---|---|
| `role` | `String` | Không | Required |
| `reason` | `String` | Có | MaxLength 500 |

[Nguồn DTO](https://github.com/silver-phung/smart-parking-system/blob/4bed0c8a929fe050d644aa4e07c0a1caa77d8d0e/backend/src/SmartParking.Application/DTOs/AdminUsers/AdminUserDtos.cs).

### AdminUpdateStatusRequestDto

| Trường JSON/query | Kiểu .NET | Nullable | DataAnnotation |
|---|---|---|---|
| `isActive` | `Boolean` | Không | Required |
| `reason` | `String` | Có | MaxLength 500 |

[Nguồn DTO](https://github.com/silver-phung/smart-parking-system/blob/4bed0c8a929fe050d644aa4e07c0a1caa77d8d0e/backend/src/SmartParking.Application/DTOs/AdminUsers/AdminUserDtos.cs).

### AdminUserFilterDto

| Trường JSON/query | Kiểu .NET | Nullable | DataAnnotation |
|---|---|---|---|
| `page` | `Int32` | Không | — |
| `pageSize` | `Int32` | Không | — |
| `search` | `String` | Có | — |
| `role` | `String` | Có | — |
| `isActive` | `Boolean?` | Có | — |
| `approvalStatus` | `String` | Có | — |

[Nguồn DTO](https://github.com/silver-phung/smart-parking-system/blob/4bed0c8a929fe050d644aa4e07c0a1caa77d8d0e/backend/src/SmartParking.Application/DTOs/AdminUsers/AdminUserDtos.cs).

### AssignSlotRequestDto

| Trường JSON/query | Kiểu .NET | Nullable | DataAnnotation |
|---|---|---|---|
| `slotId` | `Int64` | Không | Required; Range 1..9.223372036854776E+18 |
| `notes` | `String` | Có | MaxLength 255 |

[Nguồn DTO](https://github.com/silver-phung/smart-parking-system/blob/4bed0c8a929fe050d644aa4e07c0a1caa77d8d0e/backend/src/SmartParking.Application/DTOs/Dispatches/ParkingDispatchDtos.cs).

### BookingFilterDto

| Trường JSON/query | Kiểu .NET | Nullable | DataAnnotation |
|---|---|---|---|
| `lotId` | `Int32?` | Có | — |
| `status` | `String` | Có | — |
| `search` | `String` | Có | — |
| `fromDate` | `DateTime?` | Có | — |
| `toDate` | `DateTime?` | Có | — |
| `pageNumber` | `Int32` | Không | — |
| `pageSize` | `Int32` | Không | — |

[Nguồn DTO](https://github.com/silver-phung/smart-parking-system/blob/4bed0c8a929fe050d644aa4e07c0a1caa77d8d0e/backend/src/SmartParking.Application/DTOs/Bookings/BookingDtos.cs).

### CancelBookingRequestDto

| Trường JSON/query | Kiểu .NET | Nullable | DataAnnotation |
|---|---|---|---|
| `reason` | `String` | Có | MaxLength 500 |

[Nguồn DTO](https://github.com/silver-phung/smart-parking-system/blob/4bed0c8a929fe050d644aa4e07c0a1caa77d8d0e/backend/src/SmartParking.Application/DTOs/Bookings/BookingDtos.cs).

### CancelDispatchRequestDto

| Trường JSON/query | Kiểu .NET | Nullable | DataAnnotation |
|---|---|---|---|
| `reason` | `String` | Có | MaxLength 255 |

[Nguồn DTO](https://github.com/silver-phung/smart-parking-system/blob/4bed0c8a929fe050d644aa4e07c0a1caa77d8d0e/backend/src/SmartParking.Application/DTOs/Dispatches/ParkingDispatchDtos.cs).

### CheckInRequestDto

| Trường JSON/query | Kiểu .NET | Nullable | DataAnnotation |
|---|---|---|---|
| `lotId` | `Int32` | Không | Required; Range 1..2147483647 |
| `plateNumber` | `String` | Không | Required; StringLength 3..20 |
| `slotId` | `Int64?` | Có | — |
| `checkInImageUrl` | `String` | Có | MaxLength 500 |

[Nguồn DTO](https://github.com/silver-phung/smart-parking-system/blob/4bed0c8a929fe050d644aa4e07c0a1caa77d8d0e/backend/src/SmartParking.Application/DTOs/Sessions/ParkingSessionDtos.cs).

### CheckOutRequestDto

| Trường JSON/query | Kiểu .NET | Nullable | DataAnnotation |
|---|---|---|---|
| `checkOutImageUrl` | `String` | Có | MaxLength 500 |

[Nguồn DTO](https://github.com/silver-phung/smart-parking-system/blob/4bed0c8a929fe050d644aa4e07c0a1caa77d8d0e/backend/src/SmartParking.Application/DTOs/Sessions/ParkingSessionDtos.cs).

### ConfirmOccupiedRequestDto

| Trường JSON/query | Kiểu .NET | Nullable | DataAnnotation |
|---|---|---|---|
| `notes` | `String` | Có | MaxLength 255 |

[Nguồn DTO](https://github.com/silver-phung/smart-parking-system/blob/4bed0c8a929fe050d644aa4e07c0a1caa77d8d0e/backend/src/SmartParking.Application/DTOs/Dispatches/ParkingDispatchDtos.cs).

### CreateBookingRequestDto

| Trường JSON/query | Kiểu .NET | Nullable | DataAnnotation |
|---|---|---|---|
| `lotId` | `Int32` | Không | Required |
| `vehicleId` | `Int64?` | Có | — |
| `plateNumber` | `String` | Có | MaxLength 20 |
| `startTime` | `DateTime` | Không | Required |
| `endTime` | `DateTime?` | Có | — |
| `notes` | `String` | Có | MaxLength 500 |

[Nguồn DTO](https://github.com/silver-phung/smart-parking-system/blob/4bed0c8a929fe050d644aa4e07c0a1caa77d8d0e/backend/src/SmartParking.Application/DTOs/Bookings/BookingDtos.cs).

### CreateDispatchRequestDto

| Trường JSON/query | Kiểu .NET | Nullable | DataAnnotation |
|---|---|---|---|
| `sessionId` | `Int64` | Không | Required; Range 1..9.223372036854776E+18 |
| `assignedSlotId` | `Int64?` | Có | — |
| `dispatchMode` | `String` | Có | — |
| `notes` | `String` | Có | MaxLength 255 |

[Nguồn DTO](https://github.com/silver-phung/smart-parking-system/blob/4bed0c8a929fe050d644aa4e07c0a1caa77d8d0e/backend/src/SmartParking.Application/DTOs/Dispatches/ParkingDispatchDtos.cs).

### CreateParkingFloorRequestDto

| Trường JSON/query | Kiểu .NET | Nullable | DataAnnotation |
|---|---|---|---|
| `floorNumber` | `Int32` | Không | Required; Range -10..100 |
| `name` | `String` | Không | Required; StringLength 1..50 |
| `totalSlots` | `Int32` | Không | Range 0..10000 |
| `isActive` | `Boolean` | Không | — |

[Nguồn DTO](https://github.com/silver-phung/smart-parking-system/blob/4bed0c8a929fe050d644aa4e07c0a1caa77d8d0e/backend/src/SmartParking.Application/DTOs/Parking/ParkingFloorDtos.cs).

### CreateParkingLotRequestDto

| Trường JSON/query | Kiểu .NET | Nullable | DataAnnotation |
|---|---|---|---|
| `name` | `String` | Không | Required; StringLength 2..100 |
| `address` | `String` | Có | StringLength 0..255 |
| `totalCapacity` | `Int32` | Không | Range 0..100000 |
| `isActive` | `Boolean` | Không | — |

[Nguồn DTO](https://github.com/silver-phung/smart-parking-system/blob/4bed0c8a929fe050d644aa4e07c0a1caa77d8d0e/backend/src/SmartParking.Application/DTOs/Parking/ParkingLotDtos.cs).

### CreateParkingSlotRequestDto

| Trường JSON/query | Kiểu .NET | Nullable | DataAnnotation |
|---|---|---|---|
| `slotCode` | `String` | Không | Required; StringLength 1..20 |
| `physicalStatus` | `String` | Không | — |
| `positionX` | `Decimal?` | Có | — |
| `positionY` | `Decimal?` | Có | — |
| `positionZ` | `Decimal?` | Có | — |
| `isActive` | `Boolean` | Không | — |

[Nguồn DTO](https://github.com/silver-phung/smart-parking-system/blob/4bed0c8a929fe050d644aa4e07c0a1caa77d8d0e/backend/src/SmartParking.Application/DTOs/Parking/ParkingSlotDtos.cs).

### CreateStaffAssignmentRequestDto

| Trường JSON/query | Kiểu .NET | Nullable | DataAnnotation |
|---|---|---|---|
| `userId` | `Int64` | Không | Required; Range 1..9.223372036854776E+18 |
| `lotId` | `Int32` | Không | Required; Range 1..2147483647 |
| `notes` | `String` | Có | MaxLength 500 |

[Nguồn DTO](https://github.com/silver-phung/smart-parking-system/blob/4bed0c8a929fe050d644aa4e07c0a1caa77d8d0e/backend/src/SmartParking.Application/DTOs/StaffAssignments/StaffLotAssignmentDtos.cs).

### CreateVehicleRequestDto

| Trường JSON/query | Kiểu .NET | Nullable | DataAnnotation |
|---|---|---|---|
| `plateNumber` | `String` | Không | Required; StringLength 3..20 |
| `brand` | `String` | Có | MaxLength 50 |
| `model` | `String` | Có | MaxLength 50 |
| `color` | `String` | Có | MaxLength 30 |
| `userId` | `Int64?` | Có | — |

[Nguồn DTO](https://github.com/silver-phung/smart-parking-system/blob/4bed0c8a929fe050d644aa4e07c0a1caa77d8d0e/backend/src/SmartParking.Application/DTOs/Vehicles/VehicleDtos.cs).

### IngestEventRequest

| Trường JSON/query | Kiểu .NET | Nullable | DataAnnotation |
|---|---|---|---|
| `eventId` | `String` | Không | Required |
| `sessionId` | `String` | Có | — |
| `parkingLotId` | `Int32` | Không | Required; Range 1..2147483647 |
| `slotCode` | `String` | Có | Regex ^[A-Z]\d{1,2}-\d{3}$ |
| `carPlate` | `String` | Có | — |
| `eventType` | `String` | Không | Required |
| `source` | `String` | Không | Required |
| `triggeredBy` | `String` | Có | — |
| `eventTimestampUtc` | `DateTimeOffset?` | Có | — |
| `entryImageUrl` | `String` | Có | — |
| `fuelType` | `String` | Có | — |
| `carColor` | `String` | Có | — |
| `carModel` | `String` | Có | — |
| `exitImageUrl` | `String` | Có | — |

[Nguồn DTO](https://github.com/silver-phung/smart-parking-system/blob/4bed0c8a929fe050d644aa4e07c0a1caa77d8d0e/backend/src/SmartParking.Application/Events/Dtos/EventDtos.cs).

### IoTEventDto

| Trường JSON/query | Kiểu .NET | Nullable | DataAnnotation |
|---|---|---|---|
| `carId` | `String` | Không | — |
| `parkingLotId` | `Int32` | Không | — |
| `slotNumber` | `String` | Có | — |
| `eventType` | `String` | Không | — |
| `timestamp` | `DateTimeOffset?` | Có | — |

[Nguồn DTO](https://github.com/silver-phung/smart-parking-system/blob/4bed0c8a929fe050d644aa4e07c0a1caa77d8d0e/backend/src/SmartParking.Application/IoT/IoTEventDto.cs).

### LoginRequestDto

| Trường JSON/query | Kiểu .NET | Nullable | DataAnnotation |
|---|---|---|---|
| `username` | `String` | Không | Required |
| `password` | `String` | Không | Required |

[Nguồn DTO](https://github.com/silver-phung/smart-parking-system/blob/4bed0c8a929fe050d644aa4e07c0a1caa77d8d0e/backend/src/SmartParking.Application/DTOs/Auth/AuthDtos.cs).

### LogoutRequestDto

| Trường JSON/query | Kiểu .NET | Nullable | DataAnnotation |
|---|---|---|---|
| `refreshToken` | `String` | Không | Required |

[Nguồn DTO](https://github.com/silver-phung/smart-parking-system/blob/4bed0c8a929fe050d644aa4e07c0a1caa77d8d0e/backend/src/SmartParking.Application/DTOs/Auth/AuthDtos.cs).

### ParkingDispatchFilterDto

| Trường JSON/query | Kiểu .NET | Nullable | DataAnnotation |
|---|---|---|---|
| `lotId` | `Int32?` | Có | — |
| `sessionId` | `Int64?` | Có | — |
| `status` | `String` | Có | — |
| `page` | `Int32` | Không | — |
| `pageSize` | `Int32` | Không | — |

[Nguồn DTO](https://github.com/silver-phung/smart-parking-system/blob/4bed0c8a929fe050d644aa4e07c0a1caa77d8d0e/backend/src/SmartParking.Application/DTOs/Dispatches/ParkingDispatchDtos.cs).

### ParkingLotFilterDto

| Trường JSON/query | Kiểu .NET | Nullable | DataAnnotation |
|---|---|---|---|
| `pageNumber` | `Int32` | Không | Range 1..2147483647 |
| `pageSize` | `Int32` | Không | Range 1..100 |
| `isActive` | `Boolean?` | Có | — |
| `search` | `String` | Có | — |

[Nguồn DTO](https://github.com/silver-phung/smart-parking-system/blob/4bed0c8a929fe050d644aa4e07c0a1caa77d8d0e/backend/src/SmartParking.Application/DTOs/Parking/ParkingLotDtos.cs).

### ParkingSessionFilterDto

| Trường JSON/query | Kiểu .NET | Nullable | DataAnnotation |
|---|---|---|---|
| `lotId` | `Int32?` | Có | — |
| `plateNumber` | `String` | Có | — |
| `status` | `String` | Có | — |
| `fromDate` | `DateTime?` | Có | — |
| `toDate` | `DateTime?` | Có | — |
| `page` | `Int32` | Không | — |
| `pageSize` | `Int32` | Không | — |

[Nguồn DTO](https://github.com/silver-phung/smart-parking-system/blob/4bed0c8a929fe050d644aa4e07c0a1caa77d8d0e/backend/src/SmartParking.Application/DTOs/Sessions/ParkingSessionDtos.cs).

### ParkingSlotFilterDto

| Trường JSON/query | Kiểu .NET | Nullable | DataAnnotation |
|---|---|---|---|
| `pageNumber` | `Int32` | Không | Range 1..2147483647 |
| `pageSize` | `Int32` | Không | Range 1..100 |
| `physicalStatus` | `String` | Có | — |
| `isActive` | `Boolean?` | Có | — |
| `search` | `String` | Có | — |

[Nguồn DTO](https://github.com/silver-phung/smart-parking-system/blob/4bed0c8a929fe050d644aa4e07c0a1caa77d8d0e/backend/src/SmartParking.Application/DTOs/Parking/ParkingSlotDtos.cs).

### RefreshTokenRequestDto

| Trường JSON/query | Kiểu .NET | Nullable | DataAnnotation |
|---|---|---|---|
| `refreshToken` | `String` | Không | Required |

[Nguồn DTO](https://github.com/silver-phung/smart-parking-system/blob/4bed0c8a929fe050d644aa4e07c0a1caa77d8d0e/backend/src/SmartParking.Application/DTOs/Auth/AuthDtos.cs).

### ReportWrongSlotRequestDto

| Trường JSON/query | Kiểu .NET | Nullable | DataAnnotation |
|---|---|---|---|
| `actualSlotId` | `Int64` | Không | Required; Range 1..9.223372036854776E+18 |
| `notes` | `String` | Có | MaxLength 255 |

[Nguồn DTO](https://github.com/silver-phung/smart-parking-system/blob/4bed0c8a929fe050d644aa4e07c0a1caa77d8d0e/backend/src/SmartParking.Application/DTOs/Dispatches/ParkingDispatchDtos.cs).

### RevokeStaffAssignmentRequestDto

| Trường JSON/query | Kiểu .NET | Nullable | DataAnnotation |
|---|---|---|---|
| `reason` | `String` | Có | MaxLength 255 |

[Nguồn DTO](https://github.com/silver-phung/smart-parking-system/blob/4bed0c8a929fe050d644aa4e07c0a1caa77d8d0e/backend/src/SmartParking.Application/DTOs/StaffAssignments/StaffLotAssignmentDtos.cs).

### StaffAssignmentFilterDto

| Trường JSON/query | Kiểu .NET | Nullable | DataAnnotation |
|---|---|---|---|
| `lotId` | `Int32?` | Có | — |
| `userId` | `Int64?` | Có | — |
| `isActive` | `Boolean?` | Có | — |
| `page` | `Int32` | Không | Range 1..2147483647 |
| `pageSize` | `Int32` | Không | Range 1..100 |

[Nguồn DTO](https://github.com/silver-phung/smart-parking-system/blob/4bed0c8a929fe050d644aa4e07c0a1caa77d8d0e/backend/src/SmartParking.Application/DTOs/StaffAssignments/StaffLotAssignmentDtos.cs).

### UpdateLotStatusRequestDto

| Trường JSON/query | Kiểu .NET | Nullable | DataAnnotation |
|---|---|---|---|
| `isActive` | `Boolean` | Không | Required |
| `reason` | `String` | Có | StringLength 0..255 |

[Nguồn DTO](https://github.com/silver-phung/smart-parking-system/blob/4bed0c8a929fe050d644aa4e07c0a1caa77d8d0e/backend/src/SmartParking.Application/DTOs/Parking/ParkingLotDtos.cs).

### UpdateParkingFloorRequestDto

| Trường JSON/query | Kiểu .NET | Nullable | DataAnnotation |
|---|---|---|---|
| `floorNumber` | `Int32` | Không | Required; Range -10..100 |
| `name` | `String` | Không | Required; StringLength 1..50 |
| `totalSlots` | `Int32` | Không | Range 0..10000 |
| `isActive` | `Boolean` | Không | — |

[Nguồn DTO](https://github.com/silver-phung/smart-parking-system/blob/4bed0c8a929fe050d644aa4e07c0a1caa77d8d0e/backend/src/SmartParking.Application/DTOs/Parking/ParkingFloorDtos.cs).

### UpdateParkingLotRequestDto

| Trường JSON/query | Kiểu .NET | Nullable | DataAnnotation |
|---|---|---|---|
| `name` | `String` | Không | Required; StringLength 2..100 |
| `address` | `String` | Có | StringLength 0..255 |
| `totalCapacity` | `Int32` | Không | Range 0..100000 |
| `isActive` | `Boolean` | Không | — |

[Nguồn DTO](https://github.com/silver-phung/smart-parking-system/blob/4bed0c8a929fe050d644aa4e07c0a1caa77d8d0e/backend/src/SmartParking.Application/DTOs/Parking/ParkingLotDtos.cs).

### UpdateParkingSlotRequestDto

| Trường JSON/query | Kiểu .NET | Nullable | DataAnnotation |
|---|---|---|---|
| `slotCode` | `String` | Không | Required; StringLength 1..20 |
| `positionX` | `Decimal?` | Có | — |
| `positionY` | `Decimal?` | Có | — |
| `positionZ` | `Decimal?` | Có | — |
| `isActive` | `Boolean` | Không | — |

[Nguồn DTO](https://github.com/silver-phung/smart-parking-system/blob/4bed0c8a929fe050d644aa4e07c0a1caa77d8d0e/backend/src/SmartParking.Application/DTOs/Parking/ParkingSlotDtos.cs).

### UpdateProfileRequestDto

| Trường JSON/query | Kiểu .NET | Nullable | DataAnnotation |
|---|---|---|---|
| `fullName` | `String` | Không | Required; MaxLength 100 |
| `phoneNumber` | `String` | Có | MaxLength 20; Phone |

[Nguồn DTO](https://github.com/silver-phung/smart-parking-system/blob/4bed0c8a929fe050d644aa4e07c0a1caa77d8d0e/backend/src/SmartParking.Application/DTOs/Auth/AuthDtos.cs).

### UpdateSlotPhysicalStatusRequestDto

| Trường JSON/query | Kiểu .NET | Nullable | DataAnnotation |
|---|---|---|---|
| `physicalStatus` | `String` | Không | Required |
| `reason` | `String` | Có | StringLength 0..255 |

[Nguồn DTO](https://github.com/silver-phung/smart-parking-system/blob/4bed0c8a929fe050d644aa4e07c0a1caa77d8d0e/backend/src/SmartParking.Application/DTOs/Parking/ParkingSlotDtos.cs).

### UpdateSlotStatusRequest

| Trường JSON/query | Kiểu .NET | Nullable | DataAnnotation |
|---|---|---|---|
| `physicalStatus` | `String` | Không | — |
| `carPlate` | `String` | Có | — |
| `reason` | `String` | Có | — |

[Nguồn DTO](https://github.com/silver-phung/smart-parking-system/blob/4bed0c8a929fe050d644aa4e07c0a1caa77d8d0e/backend/src/SmartParking.Application/Events/Dtos/ParkingSlotDtos.cs).

### UpdateVehicleRequestDto

| Trường JSON/query | Kiểu .NET | Nullable | DataAnnotation |
|---|---|---|---|
| `brand` | `String` | Có | MaxLength 50 |
| `model` | `String` | Có | MaxLength 50 |
| `color` | `String` | Có | MaxLength 30 |
| `isActive` | `Boolean?` | Có | — |

[Nguồn DTO](https://github.com/silver-phung/smart-parking-system/blob/4bed0c8a929fe050d644aa4e07c0a1caa77d8d0e/backend/src/SmartParking.Application/DTOs/Vehicles/VehicleDtos.cs).

### VehicleFilterDto

| Trường JSON/query | Kiểu .NET | Nullable | DataAnnotation |
|---|---|---|---|
| `searchPlate` | `String` | Có | — |
| `userId` | `Int64?` | Có | — |
| `isActive` | `Boolean?` | Có | — |
| `page` | `Int32` | Không | — |
| `pageSize` | `Int32` | Không | — |

[Nguồn DTO](https://github.com/silver-phung/smart-parking-system/blob/4bed0c8a929fe050d644aa4e07c0a1caa77d8d0e/backend/src/SmartParking.Application/DTOs/Vehicles/VehicleDtos.cs).
