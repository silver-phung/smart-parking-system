# Danh mục API và hợp đồng tích hợp Smart Parking

**Trạng thái:** Hiện trạng API tại mốc đối chiếu; mục 7 là ĐỀ XUẤT và chưa được gọi như API đã có.
**Chủ sở hữu:** Backend/API.
**Rà soát gần nhất:** 28/10/2026.
**Cảnh báo:** Phải đối chiếu lại khi nhánh triển khai không kế thừa commit tham chiếu.

**Mốc đối chiếu:** `Sprint3/demo` tại `4bed0c8a929fe050d644aa4e07c0a1caa77d8d0e`, ngày 28/10/2026. Xem [README](../README.md) để biết nguồn, quy ước và trạng thái.

## 1 Cách đọc

Danh sách lấy từ attribute của controller trong assembly hiện có: **82 cặp HTTP method/path**, bao gồm alias; không phải 82 nghiệp vụ độc lập. Nhóm quyền `PUBLIC` nghĩa là route hiện không yêu cầu JWT, không phải xác nhận nội dung phù hợp để công khai. `AUTHENTICATED` cần JWT; refresh/logout public ở route nhưng yêu cầu refresh token hợp lệ theo xử lý tương ứng. Quyền role còn phải qua guard sở hữu/bãi trong service.

Cột HTTP là `ProducesResponseType` khai báo tại action, không phải danh sách đầy đủ mọi mã có thể trả. Middleware có thể trả 401/403/429; lỗi model tự trả 400; lỗi DB có thể 500. Một số controller ánh xạ lỗi bằng chuỗi thông báo, chưa có error code đồng nhất. Khi tích hợp phải kiểm tra body và status, không tin duy nhất dòng mô tả Swagger.

API core thường bọc `{ message, data }`; không áp wrapper này cho tất cả: events, viewer, Gemini, FastAPI và health có hình dạng khác. Ví dụ request .NET dùng lower camelCase theo JSON mặc định ASP.NET, còn tên DTO là tên C#. [06 Enum domains](data-contracts.md) có từ điển 39 DTO đầu vào hiện có, ghi từng trường/kiểu/nullable/DataAnnotation.

## 2 Địa chỉ và ranh giới

- API development: `http://localhost:5295`; web ở `/app/`; Swagger development ở `/swagger`.
- Gateway development: `http://localhost:5070`, proxy `/api/**`, `/health`, `/swagger/**`. Chưa có route proxy FastAPI nhận diện hoặc cả web.
- `localhost` trên điện thoại là điện thoại; môi trường PC/mobile phải dùng hostname/HTTPS dùng chung được cấu hình, không lấy URL development làm URL triển khai.
- Controller route `/api/System` và `/api/IoT` giữ casing từ tên controller; ASP.NET routing không phân biệt hoa thường ở các đoạn literal.

## 3 Controller routes hiện có

### A01 AdminUsersController

**Dữ liệu liên quan:** users, roles, staff_lot_assignments, audit_logs. Phạm vi đọc/ghi cụ thể theo action và service; nguồn bộ nhớ được ghi rõ.

API quản trị đã có; React Admin chưa có đủ form. Create chỉ STAFF/MANAGER.

| Mã | Method và path | Quyền route hiện tại | Đầu vào | HTTP khai báo | Hành vi và giới hạn |
|---|---|---|---|---|---|
| SP-API-001 | `GET /api/v1/admin/users` | ADMIN | query: filter (AdminUserFilterDto) | 200, 401, 403 | Tra danh sách tài khoản |
| SP-API-002 | `GET /api/v1/admin/users/{id:long}` | ADMIN | route/inferred: id (Int64) | 200, 401, 403, 404 | Tra tài khoản và phân công |
| SP-API-003 | `POST /api/v1/admin/users` | ADMIN | body: request (AdminCreateUserRequestDto) | 201, 400, 401, 403, 409 | Tạo nội bộ STAFF/MANAGER, không tự đăng ký Customer |
| SP-API-004 | `PATCH /api/v1/admin/users/{id:long}/status` | ADMIN | route/inferred: id (Int64); body: request (AdminUpdateStatusRequestDto) | 200, 400, 401, 403, 404 | Khóa/mở, bảo vệ Admin cuối |
| SP-API-005 | `PATCH /api/v1/admin/users/{id:long}/role` | ADMIN | route/inferred: id (Int64); body: request (AdminUpdateRoleRequestDto) | 200, 400, 401, 403, 404 | Đổi role, bảo vệ Admin cuối và cập nhật phân công |

[Nguồn controller](https://github.com/silver-phung/smart-parking-system/blob/4bed0c8a929fe050d644aa4e07c0a1caa77d8d0e/backend/src/SmartParking.Api/Controllers/AdminUsersController.cs).

### A02 AuthController

**Dữ liệu liên quan:** users, roles, refresh_tokens. Phạm vi đọc/ghi cụ thể theo action và service; nguồn bộ nhớ được ghi rõ.

Login/refresh/logout còn có; register/verify/resend không có trong controller hiện tại.

| Mã | Method và path | Quyền route hiện tại | Đầu vào | HTTP khai báo | Hành vi và giới hạn |
|---|---|---|---|---|---|
| SP-API-006 | `POST /api/v1/auth/login` | PUBLIC | body: request (LoginRequestDto) | 200, 401, 403, 423, 429 | Đăng nhập username/password |
| SP-API-007 | `POST /api/v1/auth/refresh` | PUBLIC | body: request (RefreshTokenRequestDto) | 200, 401 | Kiểm tra refresh token và cấp access mới |
| SP-API-008 | `POST /api/v1/auth/logout` | PUBLIC | body: request (LogoutRequestDto) | 200, 400 | Thu hồi refresh token được gửi |

[Nguồn controller](https://github.com/silver-phung/smart-parking-system/blob/4bed0c8a929fe050d644aa4e07c0a1caa77d8d0e/backend/src/SmartParking.Api/Controllers/AuthController.cs).

### A03 BookingsController

**Dữ liệu liên quan:** bookings, vehicles, parking_lots, parking_sessions, audit_logs. Phạm vi đọc/ghi cụ thể theo action và service; nguồn bộ nhớ được ghi rõ.

API có trước phân kỳ Sprint 4; web chưa nối. Không có hold expiry/payment hoàn chỉnh.

| Mã | Method và path | Quyền route hiện tại | Đầu vào | HTTP khai báo | Hành vi và giới hạn |
|---|---|---|---|---|---|
| SP-API-009 | `POST /api/v1/bookings` | AUTHENTICATED | body: request (CreateBookingRequestDto) | 201, 400, 401, 403, 404, 409 | Tạo booking theo bãi/xe và sức chứa |
| SP-API-010 | `GET /api/v1/bookings/my` | AUTHENTICATED | query: filter (BookingFilterDto) | 200, 401 | Đặt chỗ của người gọi |
| SP-API-011 | `GET /api/v1/bookings/{id:long}` | AUTHENTICATED | route/inferred: id (Int64) | 200, 401, 403, 404 | Chi tiết trong quyền sở hữu/bãi |
| SP-API-012 | `POST /api/v1/bookings/{id:long}/cancel` | AUTHENTICATED | route/inferred: id (Int64); body: request (CancelBookingRequestDto) | 200, 400, 401, 403, 404, 409 | Hủy PENDING và lưu lý do |
| SP-API-013 | `GET /api/v1/bookings` | ADMIN,MANAGER,STAFF | query: filter (BookingFilterDto) | 200, 401, 403 | Danh sách booking theo quyền bãi |

[Nguồn controller](https://github.com/silver-phung/smart-parking-system/blob/4bed0c8a929fe050d644aa4e07c0a1caa77d8d0e/backend/src/SmartParking.Api/Controllers/BookingsController.cs).

### A04 GeminiController

**Dữ liệu liên quan:** Không ghi phiên core. Phạm vi đọc/ghi cụ thể theo action và service; nguồn bộ nhớ được ghi rõ.

Route hiện public; kết quả AI không tự xác nhận nghiệp vụ.

| Mã | Method và path | Quyền route hiện tại | Đầu vào | HTTP khai báo | Hành vi và giới hạn |
|---|---|---|---|---|---|
| SP-API-014 | `POST /api/gemini/analyze` | PUBLIC | query: imageUrl (String) | 200, 400, 422 | Phân tích URL ảnh bằng Gemini hoặc NoOp khi thiếu cấu hình |

[Nguồn controller](https://github.com/silver-phung/smart-parking-system/blob/4bed0c8a929fe050d644aa4e07c0a1caa77d8d0e/backend/src/SmartParking.Api/Controllers/GeminiController.cs).

### A05 ImagesController

**Dữ liệu liên quan:** stored_images và Cloudinary. Phạm vi đọc/ghi cụ thể theo action và service; nguồn bộ nhớ được ghi rõ.

Hiện quyền người tải/Admin; chưa có liên kết phiên/bãi hoàn chỉnh.

| Mã | Method và path | Quyền route hiện tại | Đầu vào | HTTP khai báo | Hành vi và giới hạn |
|---|---|---|---|---|---|
| SP-API-015 | `POST /api/v1/images` | AUTHENTICATED | route/inferred: file (IFormFile) | 201, 400, 401, 502, 503 | Upload file lên Cloudinary và lưu metadata |
| SP-API-016 | `GET /api/v1/images/{id:long}` | AUTHENTICATED | route/inferred: id (Int64) | 200, 401, 404 | Metadata ảnh của người tải/Admin |
| SP-API-017 | `DELETE /api/v1/images/{id:long}` | AUTHENTICATED | route/inferred: id (Int64) | 204, 401, 403, 404, 500, 502, 503 | Xóa ảnh theo quyền người tải/Admin |

[Nguồn controller](https://github.com/silver-phung/smart-parking-system/blob/4bed0c8a929fe050d644aa4e07c0a1caa77d8d0e/backend/src/SmartParking.Api/Controllers/ImagesController.cs).

### A06 IoTController

**Dữ liệu liên quan:** JSON và bộ nhớ. Phạm vi đọc/ghi cụ thể theo action và service; nguồn bộ nhớ được ghi rõ.

MÔ PHỎNG; hiện không thấy Authorize, gồm cả reset.

| Mã | Method và path | Quyền route hiện tại | Đầu vào | HTTP khai báo | Hành vi và giới hạn |
|---|---|---|---|---|---|
| SP-API-018 | `POST /api/IoT/events` | PUBLIC | body: eventDto (IoTEventDto) | Không khai báo tại action | Nhận sự kiện vào pipeline của controller này |
| SP-API-019 | `GET /api/IoT/telemetry` | PUBLIC | Không có đầu vào nghiệp vụ | Không khai báo tại action | Telemetry mô phỏng |
| SP-API-020 | `GET /api/IoT/parked-cars` | PUBLIC | Không có đầu vào nghiệp vụ | Không khai báo tại action | Xe đang lưu trong bộ nhớ IoT |
| SP-API-021 | `POST /api/IoT/reset` | PUBLIC | Không có đầu vào nghiệp vụ | Không khai báo tại action | Reset mô phỏng, không phải quy trình vận hành |

[Nguồn controller](https://github.com/silver-phung/smart-parking-system/blob/4bed0c8a929fe050d644aa4e07c0a1caa77d8d0e/backend/src/SmartParking.Api/Controllers/IoTController.cs).

### A07 ParkingDispatchesController

**Dữ liệu liên quan:** parking_dispatches, parking_sessions, parking_slots, audit_logs. Phạm vi đọc/ghi cụ thể theo action và service; nguồn bộ nhớ được ghi rõ.

Có kiểm tra quyền bãi/sở hữu ở service; chưa có nhiệm vụ cổng/điều phối.

| Mã | Method và path | Quyền route hiện tại | Đầu vào | HTTP khai báo | Hành vi và giới hạn |
|---|---|---|---|---|---|
| SP-API-022 | `POST /api/v1/parking-dispatches` | ADMIN,MANAGER,STAFF | body: request (CreateDispatchRequestDto) | 201, 400, 401, 403, 404, 409 | Tạo lệnh ASSIGNED và gắn ô mục tiêu |
| SP-API-023 | `GET /api/v1/parking-dispatches` | ADMIN,MANAGER,STAFF,USER | query: filter (ParkingDispatchFilterDto) | 200, 401, 403 | Tra danh sách điều phối trong phạm vi |
| SP-API-024 | `GET /api/v1/parking-dispatches/{id:long}` | ADMIN,MANAGER,STAFF,USER | route/inferred: id (Int64) | 200, 401, 403, 404 | Tra lệnh theo ID |
| SP-API-025 | `GET /api/v1/parking-dispatches/session/{sessionId:long}` | ADMIN,MANAGER,STAFF,USER | route/inferred: sessionId (Int64) | 200, 401, 403, 404 | Tra lệnh hiệu lực theo phiên |
| SP-API-026 | `POST /api/v1/parking-dispatches/{id:long}/assign` | ADMIN,MANAGER,STAFF | route/inferred: id (Int64); body: request (AssignSlotRequestDto) | 200, 400, 401, 403, 404, 409 | Đổi ô chỉ khi PENDING/ASSIGNED |
| SP-API-027 | `POST /api/v1/parking-dispatches/{id:long}/confirm-occupied` | ADMIN,MANAGER,STAFF | route/inferred: id (Int64); body: request (ConfirmOccupiedRequestDto) | 200, 400, 401, 403, 404, 409 | Xác nhận ô thực tế theo lệnh ASSIGNED |
| SP-API-028 | `POST /api/v1/parking-dispatches/{id:long}/report-wrong-slot` | ADMIN,MANAGER,STAFF | route/inferred: id (Int64); body: request (ReportWrongSlotRequestDto) | 200, 400, 401, 403, 404, 409 | Xác nhận sai ô từ ASSIGNED, actual slot thành OCCUPIED |
| SP-API-029 | `POST /api/v1/parking-dispatches/{id:long}/cancel` | ADMIN,MANAGER,STAFF | route/inferred: id (Int64); body: request (CancelDispatchRequestDto) | 200, 400, 401, 403, 404 | Hủy PENDING/ASSIGNED |

[Nguồn controller](https://github.com/silver-phung/smart-parking-system/blob/4bed0c8a929fe050d644aa4e07c0a1caa77d8d0e/backend/src/SmartParking.Api/Controllers/ParkingDispatchesController.cs).

### A08 ParkingEventController

**Dữ liệu liên quan:** EventParkingSession/ParkingEventLog trong bộ nhớ. Phạm vi đọc/ghi cụ thể theo action và service; nguồn bộ nhớ được ghi rõ.

Không ghi cùng nguồn session core vì bridge NoOp; hiện không thấy Authorize.

| Mã | Method và path | Quyền route hiện tại | Đầu vào | HTTP khai báo | Hành vi và giới hạn |
|---|---|---|---|---|---|
| SP-API-030 | `POST /api/events` | PUBLIC | body: request (IngestEventRequest) | 200, 400 | Nhận sự kiện vào pipeline của controller này |
| SP-API-031 | `GET /api/events/sessions/active` | PUBLIC | query: parkingLotId (Int32) | 200 | Tra phiên hoạt động của nguồn tương ứng |
| SP-API-032 | `GET /api/events/logs` | PUBLIC | query: parkingLotId (Int32); query: limit (Int32) | 200 | Tra log sự kiện bộ nhớ |

[Nguồn controller](https://github.com/silver-phung/smart-parking-system/blob/4bed0c8a929fe050d644aa4e07c0a1caa77d8d0e/backend/src/SmartParking.Api/Controllers/ParkingEventController.cs).

### A09 ParkingFloorsController

**Dữ liệu liên quan:** parking_floors, parking_lots, parking_slots, audit_logs. Phạm vi đọc/ghi cụ thể theo action và service; nguồn bộ nhớ được ghi rõ.

Các route tương đương là alias cùng action.

| Mã | Method và path | Quyền route hiện tại | Đầu vào | HTTP khai báo | Hành vi và giới hạn |
|---|---|---|---|---|---|
| SP-API-033 | `GET /api/v1/parking-floors/lot/{lotId:int}` | PUBLIC | route/inferred: lotId (Int32); query: isActiveOnly (Nullable`1) | 200 | Danh sách tầng theo bãi |
| SP-API-034 | `GET /api/v1/parking-lots/{lotId:int}/floors` | PUBLIC | route/inferred: lotId (Int32); query: isActiveOnly (Nullable`1) | 200 | Danh sách tầng theo bãi |
| SP-API-035 | `GET /api/v1/parking-floors/{id:int}` | PUBLIC | route/inferred: id (Int32) | 200, 404 | Chi tiết tầng |
| SP-API-036 | `POST /api/v1/parking-floors/lot/{lotId:int}` | ADMIN | route/inferred: lotId (Int32); body: request (CreateParkingFloorRequestDto) | 201, 400, 401, 403, 404, 409 | Tạo tầng |
| SP-API-037 | `POST /api/v1/parking-lots/{lotId:int}/floors` | ADMIN | route/inferred: lotId (Int32); body: request (CreateParkingFloorRequestDto) | 201, 400, 401, 403, 404, 409 | Tạo tầng |
| SP-API-038 | `PUT /api/v1/parking-floors/{id:int}` | ADMIN | route/inferred: id (Int32); body: request (UpdateParkingFloorRequestDto) | 200, 400, 401, 403, 404, 409 | Cập nhật tầng |
| SP-API-039 | `DELETE /api/v1/parking-floors/{id:int}` | ADMIN | route/inferred: id (Int32) | 200, 400, 401, 403, 404 | Xóa theo guard tham chiếu |

[Nguồn controller](https://github.com/silver-phung/smart-parking-system/blob/4bed0c8a929fe050d644aa4e07c0a1caa77d8d0e/backend/src/SmartParking.Api/Controllers/ParkingFloorsController.cs).

### A10 ParkingLotsController

**Dữ liệu liên quan:** parking_lots, parking_floors, parking_slots, bookings, parking_sessions, audit_logs. Phạm vi đọc/ghi cụ thể theo action và service; nguồn bộ nhớ được ghi rõ.

GET public; một số chẩn đoán chỉ trả trong quyền. IsActive hiện được dùng cho trạng thái bãi.

| Mã | Method và path | Quyền route hiện tại | Đầu vào | HTTP khai báo | Hành vi và giới hạn |
|---|---|---|---|---|---|
| SP-API-040 | `GET /api/v1/parking-lots` | PUBLIC | query: filter (ParkingLotFilterDto) | 200 | Danh sách bãi và trạng thái |
| SP-API-041 | `GET /api/v1/parking-lots/{id:int}` | PUBLIC | route/inferred: id (Int32) | 200, 404 | Chi tiết bãi |
| SP-API-042 | `GET /api/v1/parking-lots/{id:int}/capacity` | PUBLIC | route/inferred: id (Int32) | 200, 404 | Sức chứa; chi tiết chẩn đoán chỉ trong quyền |
| SP-API-043 | `POST /api/v1/parking-lots` | ADMIN | body: request (CreateParkingLotRequestDto) | 201, 400, 401, 403, 409 | Tạo bãi |
| SP-API-044 | `PUT /api/v1/parking-lots/{id:int}` | ADMIN | route/inferred: id (Int32); body: request (UpdateParkingLotRequestDto) | 200, 400, 401, 403, 404, 409 | Cập nhật bãi |
| SP-API-045 | `PATCH /api/v1/parking-lots/{id:int}/status` | ADMIN,MANAGER | route/inferred: id (Int32); body: request (UpdateLotStatusRequestDto) | 200, 400, 401, 403, 404 | Mở/đóng tiếp nhận theo quyền bãi |
| SP-API-046 | `DELETE /api/v1/parking-lots/{id:int}` | ADMIN | route/inferred: id (Int32) | 200, 400, 401, 403, 404 | Xóa bãi theo guard tham chiếu |

[Nguồn controller](https://github.com/silver-phung/smart-parking-system/blob/4bed0c8a929fe050d644aa4e07c0a1caa77d8d0e/backend/src/SmartParking.Api/Controllers/ParkingLotsController.cs).

### A11 ParkingSessionsController

**Dữ liệu liên quan:** parking_sessions, vehicles, bookings, parking_dispatches, parking_slots, audit_logs. Phạm vi đọc/ghi cụ thể theo action và service; nguồn bộ nhớ được ghi rõ.

Check-out chưa kiểm tra đủ phí/vé; không dùng API rời để chứng nhận luồng thu/trả hoàn chỉnh.

| Mã | Method và path | Quyền route hiện tại | Đầu vào | HTTP khai báo | Hành vi và giới hạn |
|---|---|---|---|---|---|
| SP-API-047 | `POST /api/v1/parking-sessions/check-in` | ADMIN,MANAGER,STAFF | body: request (CheckInRequestDto) | 201, 400, 401, 403, 404, 409 | Nhận xe, tạo ACTIVE; không tự chuyển ô Có xe |
| SP-API-048 | `POST /api/v1/parking-sessions/{id:long}/check-out` | ADMIN,MANAGER,STAFF | route/inferred: id (Int64); body: request (CheckOutRequestDto) | 200, 400, 401, 403, 404 | Đóng phiên và giải phóng ô; chưa có guard đủ phí |
| SP-API-049 | `GET /api/v1/parking-sessions/active` | ADMIN,MANAGER,STAFF | query: filter (ParkingSessionFilterDto) | 200, 401, 403 | Tra phiên hoạt động của nguồn tương ứng |
| SP-API-050 | `GET /api/v1/parking-sessions/my-sessions` | ADMIN,USER | query: filter (ParkingSessionFilterDto) | 200, 401 | Phiên gắn xe của người gọi |
| SP-API-051 | `GET /api/v1/parking-sessions/{id:long}` | ADMIN,MANAGER,STAFF,USER | route/inferred: id (Int64) | 200, 401, 403, 404 | Chi tiết phiên theo quyền |
| SP-API-052 | `GET /api/v1/parking-sessions/by-plate/{plateNumber}/active` | ADMIN,MANAGER,STAFF,USER | route/inferred: plateNumber (String) | 200, 401, 403, 404 | Tra phiên mở theo biển số |

[Nguồn controller](https://github.com/silver-phung/smart-parking-system/blob/4bed0c8a929fe050d644aa4e07c0a1caa77d8d0e/backend/src/SmartParking.Api/Controllers/ParkingSessionsController.cs).

### A12 ParkingSlotsController

**Dữ liệu liên quan:** parking_slots, parking_floors, parking_sessions, parking_dispatches, audit_logs. Phạm vi đọc/ghi cụ thể theo action và service; nguồn bộ nhớ được ghi rõ.

Chi tiết nội bộ của slot hiện chỉ Admin; alias floor có cùng xử lý.

| Mã | Method và path | Quyền route hiện tại | Đầu vào | HTTP khai báo | Hành vi và giới hạn |
|---|---|---|---|---|---|
| SP-API-053 | `GET /api/v1/parking-slots/floor/{floorId:int}` | PUBLIC | route/inferred: floorId (Int32); query: filter (ParkingSlotFilterDto) | 200 | Danh sách slot công khai |
| SP-API-054 | `GET /api/v1/parking-floors/{floorId:int}/slots` | PUBLIC | route/inferred: floorId (Int32); query: filter (ParkingSlotFilterDto) | 200 | Danh sách slot công khai |
| SP-API-055 | `GET /api/v1/parking-slots/{id:long}` | PUBLIC | route/inferred: id (Int64) | 200, 404 | Chi tiết public; dữ liệu nội bộ hiện chỉ Admin |
| SP-API-056 | `POST /api/v1/parking-slots/floor/{floorId:int}` | ADMIN | route/inferred: floorId (Int32); body: request (CreateParkingSlotRequestDto) | 201, 400, 401, 403, 404, 409 | Tạo ô trong tầng |
| SP-API-057 | `POST /api/v1/parking-floors/{floorId:int}/slots` | ADMIN | route/inferred: floorId (Int32); body: request (CreateParkingSlotRequestDto) | 201, 400, 401, 403, 404, 409 | Tạo ô trong tầng |
| SP-API-058 | `PUT /api/v1/parking-slots/{id:long}` | ADMIN | route/inferred: id (Int64); body: request (UpdateParkingSlotRequestDto) | 200, 400, 401, 403, 404, 409 | Cập nhật mã/tọa độ/cờ hoạt động |
| SP-API-059 | `PATCH /api/v1/parking-slots/{id:long}/status` | ADMIN,MANAGER | route/inferred: id (Int64); body: request (UpdateSlotPhysicalStatusRequestDto) | 200, 400, 401, 403, 404 | Cập nhật trạng thái vật lý qua dịch vụ |
| SP-API-060 | `DELETE /api/v1/parking-slots/{id:long}` | ADMIN | route/inferred: id (Int64) | 200, 400, 401, 403, 404 | Xóa theo guard tham chiếu |

[Nguồn controller](https://github.com/silver-phung/smart-parking-system/blob/4bed0c8a929fe050d644aa4e07c0a1caa77d8d0e/backend/src/SmartParking.Api/Controllers/ParkingSlotsController.cs).

### A13 ParkingViewerSlotsController

**Dữ liệu liên quan:** Dictionary static và event session bộ nhớ. Phạm vi đọc/ghi cụ thể theo action và service; nguồn bộ nhớ được ghi rõ.

MÔ PHỎNG; GET có biển số/session; PATCH hiện không thấy guard. Không dùng thay API core.

| Mã | Method và path | Quyền route hiện tại | Đầu vào | HTTP khai báo | Hành vi và giới hạn |
|---|---|---|---|---|---|
| SP-API-061 | `GET /api/ParkingSlots` | PUBLIC | query: lotId (Int32); query: floorId (String) | 200 | Viewer trả slot từ dictionary và event session |
| SP-API-062 | `GET /api/v1/parking-viewer/slots` | PUBLIC | query: lotId (Int32); query: floorId (String) | 200 | Viewer trả slot từ dictionary và event session |
| SP-API-063 | `GET /api/ParkingSlots/floor/{floorId}` | PUBLIC | route/inferred: floorId (String); query: lotId (Int32) | Không khai báo tại action | Viewer theo tầng dạng chuỗi |
| SP-API-064 | `GET /api/v1/parking-viewer/floors/{floorId}/slots` | PUBLIC | route/inferred: floorId (String); query: lotId (Int32) | Không khai báo tại action | Viewer theo tầng dạng chuỗi |
| SP-API-065 | `PATCH /api/ParkingSlots/{slotCode}/status` | PUBLIC | route/inferred: slotCode (String); body: request (UpdateSlotStatusRequest) | Không khai báo tại action | PATCH dictionary viewer, hiện chưa có guard quyền |
| SP-API-066 | `PATCH /api/v1/parking-viewer/slots/{slotCode}/status` | PUBLIC | route/inferred: slotCode (String); body: request (UpdateSlotStatusRequest) | Không khai báo tại action | PATCH dictionary viewer, hiện chưa có guard quyền |

[Nguồn controller](https://github.com/silver-phung/smart-parking-system/blob/4bed0c8a929fe050d644aa4e07c0a1caa77d8d0e/backend/src/SmartParking.Api/Controllers/ParkingViewerSlotsController.cs).

### A14 SimulatorParkingLotsController

**Dữ liệu liên quan:** Data/parking-lots.json. Phạm vi đọc/ghi cụ thể theo action và service; nguồn bộ nhớ được ghi rõ.

Đường /parking-lots khác API quản lý /api/v1/parking-lots.

| Mã | Method và path | Quyền route hiện tại | Đầu vào | HTTP khai báo | Hành vi và giới hạn |
|---|---|---|---|---|---|
| SP-API-067 | `GET /parking-lots` | PUBLIC | Không có đầu vào nghiệp vụ | Không khai báo tại action | Danh mục bãi mô phỏng từ JSON |

[Nguồn controller](https://github.com/silver-phung/smart-parking-system/blob/4bed0c8a929fe050d644aa4e07c0a1caa77d8d0e/backend/src/SmartParking.Api/Controllers/SimulatorParkingLotsController.cs).

### A15 StaffAssignmentsController

**Dữ liệu liên quan:** staff_lot_assignments, users, roles, parking_lots, audit_logs. Phạm vi đọc/ghi cụ thể theo action và service; nguồn bộ nhớ được ghi rõ.

Admin cấp/thu hồi; chưa có field nhiệm vụ Staff.

| Mã | Method và path | Quyền route hiện tại | Đầu vào | HTTP khai báo | Hành vi và giới hạn |
|---|---|---|---|---|---|
| SP-API-068 | `POST /api/v1/staff-assignments` | ADMIN | body: request (CreateStaffAssignmentRequestDto) | 201, 400, 401, 403, 404, 409 | Cấp phân công bãi |
| SP-API-069 | `GET /api/v1/staff-assignments` | ADMIN | query: filter (StaffAssignmentFilterDto) | 200, 401, 403 | Danh sách phân công |
| SP-API-070 | `GET /api/v1/staff-assignments/my-lots` | ADMIN,MANAGER,STAFF | Không có đầu vào nghiệp vụ | 200, 401, 403 | Các bãi của người gọi |
| SP-API-071 | `GET /api/v1/staff-assignments/{id:long}` | ADMIN | route/inferred: id (Int64) | 200, 401, 403, 404 | Chi tiết phân công |
| SP-API-072 | `POST /api/v1/staff-assignments/{id:long}/revoke` | ADMIN | route/inferred: id (Int64); body: request (RevokeStaffAssignmentRequestDto) | 200, 400, 401, 403, 404 | Thu hồi phân công và lưu lý do |

[Nguồn controller](https://github.com/silver-phung/smart-parking-system/blob/4bed0c8a929fe050d644aa4e07c0a1caa77d8d0e/backend/src/SmartParking.Api/Controllers/StaffAssignmentsController.cs).

### A16 SystemController

**Dữ liệu liên quan:** Không query DB. Phạm vi đọc/ghi cụ thể theo action và service; nguồn bộ nhớ được ghi rõ.

Trạng thái Healthy không chứng minh kết nối Neon thành công.

| Mã | Method và path | Quyền route hiện tại | Đầu vào | HTTP khai báo | Hành vi và giới hạn |
|---|---|---|---|---|---|
| SP-API-073 | `GET /api/System/info` | PUBLIC | Không có đầu vào nghiệp vụ | 200 | Thông tin tiến trình/môi trường |
| SP-API-074 | `GET /api/System/health` | PUBLIC | Không có đầu vào nghiệp vụ | 200 | Kiểm tra tiến trình, không query DB |

[Nguồn controller](https://github.com/silver-phung/smart-parking-system/blob/4bed0c8a929fe050d644aa4e07c0a1caa77d8d0e/backend/src/SmartParking.Api/Controllers/SystemController.cs).

### A17 UsersController

**Dữ liệu liên quan:** users. Phạm vi đọc/ghi cụ thể theo action và service; nguồn bộ nhớ được ghi rõ.

Profile chỉ họ tên/số điện thoại trong PUT hiện tại; không có đổi password.

| Mã | Method và path | Quyền route hiện tại | Đầu vào | HTTP khai báo | Hành vi và giới hạn |
|---|---|---|---|---|---|
| SP-API-075 | `GET /api/v1/users/me` | AUTHENTICATED | Không có đầu vào nghiệp vụ | 200, 401, 404 | Hồ sơ người gọi |
| SP-API-076 | `PUT /api/v1/users/me` | AUTHENTICATED | body: request (UpdateProfileRequestDto) | 200, 400, 401, 404 | Cập nhật họ tên/số điện thoại |

[Nguồn controller](https://github.com/silver-phung/smart-parking-system/blob/4bed0c8a929fe050d644aa4e07c0a1caa77d8d0e/backend/src/SmartParking.Api/Controllers/UsersController.cs).

### A18 VehiclesController

**Dữ liệu liên quan:** vehicles, users, parking_sessions, bookings, audit_logs. Phạm vi đọc/ghi cụ thể theo action và service; nguồn bộ nhớ được ghi rõ.

USER giới hạn dữ liệu mình, ADMIN theo quyền; không tự nhận sở hữu xe vãng lai.

| Mã | Method và path | Quyền route hiện tại | Đầu vào | HTTP khai báo | Hành vi và giới hạn |
|---|---|---|---|---|---|
| SP-API-077 | `GET /api/v1/vehicles` | ADMIN,USER | query: filter (VehicleFilterDto) | 200, 401, 403 | Xe trong quyền |
| SP-API-078 | `GET /api/v1/vehicles/{id:long}` | ADMIN,USER | route/inferred: id (Int64) | 200, 401, 403, 404 | Chi tiết xe |
| SP-API-079 | `GET /api/v1/vehicles/by-plate/{plateNumber}` | ADMIN,USER | route/inferred: plateNumber (String) | 200, 401, 403, 404 | Tra xe theo biển số trong quyền |
| SP-API-080 | `POST /api/v1/vehicles` | ADMIN,USER | body: request (CreateVehicleRequestDto) | 201, 400, 401, 403, 409 | Tạo xe gắn tài khoản sau kiểm tra sở hữu |
| SP-API-081 | `PUT /api/v1/vehicles/{id:long}` | ADMIN,USER | route/inferred: id (Int64); body: request (UpdateVehicleRequestDto) | 200, 400, 401, 403, 404 | Sửa thuộc tính xe theo DTO |
| SP-API-082 | `DELETE /api/v1/vehicles/{id:long}` | ADMIN,USER | route/inferred: id (Int64) | 200, 400, 401, 403, 404 | Vô hiệu hóa xe theo guard, không xóa lịch sử |

[Nguồn controller](https://github.com/silver-phung/smart-parking-system/blob/4bed0c8a929fe050d644aa4e07c0a1caa77d8d0e/backend/src/SmartParking.Api/Controllers/VehiclesController.cs).

## 4 Endpoint ngoài danh sách controller

| Dịch vụ | Method/path | Hành vi |
|---|---|---|
| API | `GET /health` | Health check tiến trình, không gắn kiểm tra DB hiện tại |
| API | `GET /` | Redirect `/app/` |
| API | `/app/{*path:nonfile}` | SPA fallback phục vụ app/index.html; không phải API nghiệp vụ |
| API development | `/swagger/*` | Tài liệu OpenAPI, chỉ bật theo môi trường |
| Gateway | `GET /gateway/health` | Kiểm tra tiến trình gateway |
| Gateway | `/api/**`, `/health`, `/swagger/**` | Chuyển tiếp, không nhân thành API nghiệp vụ mới |
| FastAPI | `POST /api/v1/detect` | Multipart `file`; query vconf, pconf, ocr_thresh; nhận diện ảnh độc lập |

FastAPI có các route tài liệu mặc định tùy cấu hình framework; không tính chúng là chức năng nghiệp vụ được bổ sung. Các default query nhận diện hiện là 0.5/0.25/0.4, thuộc cấu hình kỹ thuật, không phải cam kết độ chính xác.

## 5 Ví dụ hợp đồng hiện có

Giá trị ID/biển số/địa chỉ dưới đây minh họa hợp đồng, không cấp dữ liệu vận hành hoặc credential.

### 5.1 Nhận vãng lai

`POST /api/v1/parking-sessions/check-in` với Bearer của người có quyền bãi:

```json
{"lotId": 12, "plateNumber": "51F-123.45", "checkInImageUrl": "https://example.com/entry.jpg"}
```

`slotId` tùy chọn nhưng chưa chứng minh đã đỗ. Không có `customerId`, `ticketId` hoặc `fuelType` trong CheckInRequestDto hiện tại. Thành công tạo phiên ACTIVE; không dùng response này để hiện xe tại ô chưa xác nhận.

Hợp đồng hiện có coi `plateNumber` là đầu vào chính thức và chưa đáp ứng quyết định mới. Hướng đích cho luồng chuẩn là camera/AI tạo recognition result và session ACTIVE ở trạng thái vận hành `WAITING_VERIFICATION`, liên kết vé/thẻ vào và ảnh; Staff không phải xác nhận ở cổng. Biển số chính thức chỉ được chốt/sửa khi Staff điều phối xác minh xe tại actual slot. Cần idempotency theo event/vé để OCR sai hoặc retry không tạo hai phiên.

### 5.2 Phân ô và xác nhận

```text
POST /api/v1/parking-dispatches
{"sessionId": 456, "assignedSlotId": 91, "dispatchMode": "MANUAL", "notes": "Đối chiếu ô mục tiêu"}

POST /api/v1/parking-dispatches/789/confirm-occupied
{"notes": "Đã kiểm tra xe tại ô"}

POST /api/v1/parking-dispatches/789/report-wrong-slot
{"actualSlotId": 92, "notes": "Xe thực tế ở ô bên cạnh"}
```

Confirm và report-wrong chỉ áp dụng khi lệnh còn ASSIGNED; không gọi nối cả hai trên cùng lệnh đã kết thúc. `assign` nhận `{ slotId, notes }`, chỉ đổi được PENDING/ASSIGNED.

### 5.3 Check-out hiện tại

```text
POST /api/v1/parking-sessions/456/check-out
{"checkOutImageUrl": "https://example.com/exit.jpg"}
```

DTO chưa có vé, mã khoản thu hoặc xác nhận đối chiếu. Không thêm các field vào request rồi nghĩ backend đã kiểm tra; cần sửa hợp đồng và service để đạt nghiệp vụ.

Hướng đích tại cổng ra: xe tự tới làn checkout và quẹt vé/thẻ; hệ thống tra đúng phiên mở, trả ảnh/thông tin lúc vào và tính phí máy chủ. Không cần sự kiện “khách yêu cầu lấy xe” riêng. Sau khi thu đủ hoặc duyệt ngoại lệ và có xác nhận xe qua cổng, lệnh check-out mới đóng phiên.

### 5.4 Sự kiện riêng

```json
{"eventId":"f5061ecf-16b8-4ea2-b785-dc92ba05a803","parkingLotId":12,"carPlate":"51F-123.45","eventType":"VehicleEntered","source":"GateStaff","triggeredBy":"staff-reference","eventTimestampUtc":"2026-10-28T07:00:00Z"}
```

Gửi tới `POST /api/events`. Khi retry cùng thao tác dùng lại EventId. SessionId là chuỗi của pipeline riêng, không phải số `parking_sessions.id`. Source/TriggeredBy hiện là dữ liệu client; chưa phải danh tính được xác thực. Xem [06 Enum domains](data-contracts.md) cho regex slot và sai khác mã.

### 5.5 Upload và OCR

`POST /api/v1/images` nhận multipart `file`, Bearer bắt buộc. Backend mặc định giới hạn ảnh 10 MiB và JPEG/PNG/WebP theo cấu hình Cloudinary. Quyền metadata hiện theo người tải/Admin. FastAPI `/api/v1/detect` cũng nhận `file` nhưng không tự dùng chung giới hạn/guard của upload .NET.

FastAPI trả `{ success, message, data }`, data có `plate_number`, `raw_plate_number`, `confidence`, `is_legit`, bbox và `inference_time_ms`. Không đọc được biển số không được lấy kết quả lần trước điền thành công. Gemini nhận `imageUrl` qua query và trả `isSuccess`; NoOp không có nghĩa đã phân tích thật.

## 6 Phân trang và lỗi

- Bãi/slot/booking dùng `pageNumber`, `pageSize`; phiên/vehicle/assignment/admin users dùng `page`, `pageSize` trong DTO hiện tại. Tra danh mục trường ở [06](data-contracts.md), không dùng một query chung cho mọi màn hình.
- Loại ID bãi/tầng là int; slot/phiên/xe/booking là long. FloorId trong viewer là chuỗi như B1, không tự ghép với core floor ID.
- Login: sai thông tin 401, trạng thái không đủ điều kiện 403, lockout 423, rate limit 429 theo xử lý hiện tại. Không làm lộ mật khẩu/OTP/token trong thông báo.
- Lỗi 400/403/404/409 của nghiệp vụ cần giữ thông báo người dùng hiểu được. Khi timeout ở thao tác ghi, tra lại kết quả trước khi tạo yêu cầu mới.
- Upload có lỗi cấu hình/provider/persistence khác nhau; API/DB lỗi phải báo lỗi và giữ dữ liệu đã xác nhận, không thay bằng dữ liệu mẫu.

## 7 API còn thiếu và hợp đồng đề xuất

Các URI dưới đây là **ĐỀ XUẤT, chưa tồn tại tại commit đối chiếu**. Không gọi chúng như API đã có. Chốt tên/DTO/quyền và migration trước khi bàn giao.

| Nhóm | URI đề xuất | Quyền và kết quả cần đạt |
|---|---|---|
| Tài khoản | `POST /api/v1/auth/register`, `/verify-email`, `/resend-verification` | Customer, Gmail/reCAPTCHA, giới hạn và token dùng một lần; tên endpoint khôi phục cần thống nhất |
| Nhiệm vụ | `PATCH /api/v1/staff-assignments/{id}/duties` | Admin, nhiệm vụ cổng/điều phối độc lập theo bãi |
| Nhận xe tự động | `POST /api/v1/entry-events`; `POST /api/v1/parking-sessions/{id}/verify-at-slot` | Camera/OCR tạo phiên tạm chống trùng; Staff điều phối xác nhận/sửa xe, biển số và actual slot nguyên tử |
| Giá/phí | `GET /api/v1/parking-sessions/{id}/fee`; CRUD pricing-policies | Giá/time server và snapshot chính sách; không tin tiền từ UI |
| Khoản thu | `POST /api/v1/parking-sessions/{id}/payments` | Staff cổng đủ quyền, chứng từ, mã chống trùng, không tự đóng phiên |
| Vé/thẻ | `POST /api/v1/ticket-assignments`; thu hồi/xử lý mất | Đúng bãi, một vé cho một phiên mở, lịch sử cấp/thu |
| Cổng ra | `POST /api/v1/exit-lookups`; `POST /api/v1/parking-sessions/{id}/confirm-exit` | Quẹt vé/thẻ để tải phiên và tính phí; chỉ confirm-exit sau thanh toán/ngoại lệ và xe thực sự qua cổng |
| Ảnh theo phiên | `POST /api/v1/parking-sessions/{id}/images` | Liên kết ảnh/nhận diện/xác nhận, quyền theo bãi và phiên |
| Snapshot | `GET /api/v1/parking-lots/{id}/snapshot` | Public/internal projection khác nhau, version/time, cùng nguồn DB |
| Bảng IoT slot cố định | `GET /api/v1/devices/{id}/slot-display-config` | Thiết bị được xác thực nhận full biển số đúng slot/quyền/hiệu lực; không public và không dùng token người dùng |
| Gói cố định | `/api/v1/parking-passes`, `/renewals` | Customer sở hữu, ô/khu/xe/quyền hạn và điều kiện tiền/duyệt |
| Report | `/api/v1/slot-reports` | Chủ quyền ô gửi bằng chứng; nội bộ xác minh; không tự tạo khoản phạt |
| Hoàn tiền/báo cáo | `/api/v1/refunds`, `/reports` | Đúng quyền, giao dịch gốc/retention và phạm vi bãi |

Xem [05 Use cases](../product/use-cases.md), [02 State machines](../product/state-machines.md) và [03 Business rules](../product/business-rules.md) trước khi mở API mới. Việc docs ghi đề xuất không phải phê duyệt triển khai hoặc thay chính sách.
