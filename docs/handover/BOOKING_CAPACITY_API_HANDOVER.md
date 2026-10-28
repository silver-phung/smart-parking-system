# SMART PARKING – BOOKING & CAPACITY MANAGEMENT API HANDOVER

**Mục tiêu:** Tài liệu bàn giao kỹ thuật toàn diện về Booking & Quản lý Sức chứa (Task 07) dành cho **Song (Frontend Web/Mobile)** và **Danh (QA/Swagger Testing)**.  
**Workspace:** `E:\smartparking`  
**Branch:** `feature/phung-database`  
**Base URL:** `http://localhost:5295/api/v1`  
**Authentication:** `Bearer {jwt_access_token}`  

---

## 1. TỔNG QUAN HỆ THỐNG VÀ NGUYÊN TẮC NGHIỆP VỤ ĐÃ KHÓA (LOCKED RULES)

1. **Rule 1 – Đặt chỗ giữ CÔNG SUẤT, không cố định Ô ĐỖ (Capacity Hold, Not Slot Locking):**
   - Đặt chỗ chỉ giữ 1 suất trong tổng sức chứa khả dụng của bãi xe.
   - Đặt chỗ **KHÔNG** gán cứng ô đỗ (`SlotId`) và **KHÔNG** đánh dấu ô vật lý sang `OCCUPIED`.
2. **Rule 2 – Đỗ xe tự do (Free Parking Slot Selection):**
   - Xe có đặt chỗ và xe vãng lai đều tự do chọn bất kỳ ô đỗ nào còn trống khi vào bãi.
   - Slot vật lý chỉ chuyển sang `OCCUPIED` khi cảm biến IoT ghi nhận xe thực tế đỗ vào ô.
3. **Rule 3 – Camera OCR tại cổng vào:**
   - Hệ thống OCR nhận diện biển số tại barrier vào.
   - Backend chuẩn hóa biển số và tự động đối soát lượt đặt chỗ `PENDING` hợp lệ.
   - Nếu có đặt chỗ: Chuyển đổi thành phiên đỗ `ACTIVE` và liên kết `BookingId`.
   - Nếu không có đặt chỗ: Kiểm tra sức chứa vãng lai, từ chối (409 Conflict) nếu bãi đã hết chỗ.
4. **Rule 4 – Tính toán sức chứa thực tế & Bộ lọc bảo vệ an toàn IoT (Conservative IoT Capacity Guard):**
   - **Xác định sức chứa chuẩn (`totalCapacity`):**
     - Nếu bãi xe **ĐÃ CẤU HÌNH** danh sách ô (`ParkingSlot`): Sức chứa chuẩn bắt buộc tính từ số ô đỗ vật lý khả dụng trên các tầng đang hoạt động (`IsActive = true`, `Floor.IsActive = true`, trạng thái khác `MAINTENANCE` và `UNAVAILABLE`). Nếu toàn bộ ô bị bảo trì hoặc đóng cửa, sức chứa là **0** (tuyệt đối **KHÔNG** fallback về `lot.TotalCapacity`).
     - Nếu bãi xe **CHƯA CẤU HÌNH** ô đỗ: Cho phép fallback lấy giá trị thiết kế `lot.TotalCapacity`.
     - Khi sức chứa giảm đột ngột (ví dụ cúp điện/sự cố phải đóng ô): Hệ thống từ chối nhận xe mới, bảo lưu toàn vẹn các phiên đỗ và đặt chỗ hiện hữu, không tự ý hủy.
   - **Công thức bảo vệ an toàn IoT đồng nhất (Booking, Walk-in Check-in, Public Capacity):**
     $$Used = \max(\text{ActiveOrPaidSessions}, \text{PhysicalOccupiedSlots}) + \text{PendingBookingHolds}$$
     $$Available = \max(0, \text{UsableCapacity} - Used)$$
   - Cảm biến IoT chỉ ghi nhận trạng thái vật lý (`EMPTY`/`OCCUPIED`), không suy đoán danh tính xe. Nếu cảm biến báo có xe vượt quá số phiên phần mềm (hoặc ngược lại), hệ thống luôn lấy cận trên ($\max$) để không bao giờ overbook.
   - Thông tin chênh lệch cảm biến được lưu giữ và cảnh báo an toàn qua `discrepancyNotes` chỉ dành cho nội bộ (`STAFF`/`MANAGER`/`ADMIN`).
5. **Rule 5 – Phân quyền theo bãi (Per-Lot RBAC):**
   - `USER`: Đặt chỗ, xem danh sách và chi tiết đặt chỗ của chính mình, hủy đặt chỗ của mình.
   - `STAFF` / `MANAGER`: Tra cứu danh sách và quản lý đặt chỗ trong phạm vi các bãi được phân công.
   - `ADMIN`: Toàn quyền trên tất cả các bãi.


---

## 2. DANH SÁCH API ENDPOINTS

| STT | HTTP Method | Endpoint | Mô tả chức năng | Quyền truy cập |
| :---: | :---: | :--- | :--- | :--- |
| 1 | `POST` | `/api/v1/bookings` | Đặt chỗ trước tại bãi (giữ công suất) | `USER`, `STAFF`, `MANAGER`, `ADMIN` |
| 2 | `GET` | `/api/v1/bookings/my` | Danh sách đặt chỗ của người dùng hiện tại | `USER`, `STAFF`, `MANAGER`, `ADMIN` |
| 3 | `GET` | `/api/v1/bookings/{id}` | Xem chi tiết 1 lượt đặt chỗ | Chủ sở hữu hoặc Nhân sự bãi / ADMIN |
| 4 | `POST` | `/api/v1/bookings/{id}/cancel` | Hủy đặt chỗ (giải phóng công suất) | Chủ sở hữu hoặc Nhân sự bãi / ADMIN |
| 5 | `GET` | `/api/v1/bookings` | Danh sách đặt chỗ vận hành (lọc, phân trang) | `STAFF`, `MANAGER`, `ADMIN` (Per-Lot RBAC) |
| 6 | `GET` | `/api/v1/parking-lots/{id}/capacity` | Sức chứa & khả dụng thời gian thực | `Public` (`[AllowAnonymous]`) |
| 7 | `POST` | `/api/v1/parking-sessions/check-in` | Check-in xe vào bãi (tự động nhận diện Booking) | `STAFF`, `MANAGER`, `ADMIN` |

---

## 3. CHI TIẾT CÁC ENDPOINT

### 3.1. POST `/api/v1/bookings` – Tạo lượt đặt chỗ

Người dùng đặt trước vị trí đỗ tại bãi xe. Backend kiểm tra sức chứa khả dụng bên trong khóa giao dịch chống overbooking.

- **Yêu cầu đăng nhập:** `Bearer Token`
- **Request Body (JSON):**
```json
{
  "lotId": 1,
  "vehicleId": 10,
  "plateNumber": "51A-12345",
  "startTime": "2026-10-28T08:00:00Z",
  "endTime": "2026-10-28T12:00:00Z",
  "notes": "Đặt chỗ gửi xe buổi sáng"
}
```
*Ghi chú request:*
- `vehicleId` (optional nếu truyền `plateNumber`): ID xe đã đăng ký của người dùng.
- `plateNumber` (optional nếu đã chọn `vehicleId`): Nhập biển số trực tiếp (hệ thống tự chuẩn hóa viết hoa, bỏ khoảng trắng).
- `startTime`: Mặc định thời điểm hiện tại nếu không truyền.
- `endTime` (optional): Thời gian dự kiến rời bãi.

- **Response thành công (`201 Created`):**
```json
{
  "message": "Đặt chỗ thành công. Vị trí đỗ xe tại bãi 'Bãi Đỗ Xe Quận 1' đã được giữ trước cho phương tiện '51A-12345'.",
  "data": {
    "id": 101,
    "lotId": 1,
    "lotName": "Bãi Đỗ Xe Quận 1",
    "lotAddress": "123 Lê Lợi, Quận 1, TP.HCM",
    "userId": 5,
    "username": "nguyen_van_a",
    "vehicleId": 10,
    "plateNumber": "51A-12345",
    "startTime": "2026-10-28T08:00:00Z",
    "endTime": "2026-10-28T12:00:00Z",
    "status": "PENDING",
    "checkedInAt": null,
    "cancelledAt": null,
    "cancellationReason": null,
    "notes": "Đặt chỗ gửi xe buổi sáng",
    "sessionId": null,
    "createdAt": "2026-10-27T14:00:00Z",
    "updatedAt": "2026-10-27T14:00:00Z"
  }
}
```

- **Mã lỗi thường gặp:**
  - `400 Bad Request`: Bãi đỗ xe ngừng hoạt động, biển số xe sai định dạng, xe bị vô hiệu hóa, hoặc thời gian đặt chỗ không hợp lệ.
  - `403 Forbidden`: Người dùng cố tình đặt chỗ cho phương tiện thuộc sở hữu của người khác.
  - `404 Not Found`: Không tìm thấy bãi đỗ xe hoặc phương tiện.
  - `409 Conflict`:
    - Bãi xe đã hết chỗ khả dụng (`"Bãi đỗ xe '...' đã hết chỗ khả dụng. Toàn bộ sức chứa đã được đặt trước hoặc có xe đang đỗ."`).
    - Phương tiện đang có 1 lượt đặt chỗ `PENDING` chưa sử dụng.
    - Phương tiện đang có phiên gửi xe `ACTIVE`/`PAID` chưa rời bãi (Anti-Passback).

---

### 3.2. GET `/api/v1/bookings/my` – Danh sách đặt chỗ của tôi

Dành cho người dùng xem lại lịch sử và các lượt đặt chỗ đang có hiệu lực của chính mình.

- **Query Parameters:**
  - `lotId` (int, optional): Lọc theo bãi xe.
  - `status` (string, optional): Lọc theo trạng thái (`PENDING`, `CHECKED_IN`, `CANCELLED`, `EXPIRED`).
  - `pageNumber` (int, default: 1): Số trang.
  - `pageSize` (int, default: 10, max: 100): Số lượng bản ghi trên một trang.

- **Response thành công (`200 OK`):**
```json
{
  "message": "Lấy danh sách đặt chỗ của bạn thành công.",
  "data": {
    "items": [
      {
        "id": 101,
        "lotId": 1,
        "lotName": "Bãi Đỗ Xe Quận 1",
        "lotAddress": "123 Lê Lợi, Quận 1, TP.HCM",
        "userId": 5,
        "username": "nguyen_van_a",
        "vehicleId": 10,
        "plateNumber": "51A-12345",
        "startTime": "2026-10-28T08:00:00Z",
        "endTime": "2026-10-28T12:00:00Z",
        "status": "PENDING",
        "checkedInAt": null,
        "cancelledAt": null,
        "cancellationReason": null,
        "notes": "Đặt chỗ gửi xe buổi sáng",
        "sessionId": null,
        "createdAt": "2026-10-27T14:00:00Z",
        "updatedAt": "2026-10-27T14:00:00Z"
      }
    ],
    "totalCount": 1,
    "pageNumber": 1,
    "pageSize": 10,
    "totalPages": 1,
    "hasPreviousPage": false,
    "hasNextPage": false
  }
}
```

---

### 3.3. GET `/api/v1/bookings/{id}` – Xem chi tiết đặt chỗ

- **Response thành công (`200 OK`):**
```json
{
  "message": "Lấy thông tin chi tiết đặt chỗ thành công.",
  "data": {
    "id": 101,
    "lotId": 1,
    "lotName": "Bãi Đỗ Xe Quận 1",
    "lotAddress": "123 Lê Lợi, Quận 1, TP.HCM",
    "userId": 5,
    "username": "nguyen_van_a",
    "userFullName": "Nguyễn Văn A",
    "userEmail": "nguyenvana@gmail.com",
    "userPhone": "0901234567",
    "vehicleId": 10,
    "plateNumber": "51A-12345",
    "vehicleType": "CAR",
    "vehicleModel": "Toyota Corolla Cross",
    "vehicleColor": "Trắng",
    "startTime": "2026-10-28T08:00:00Z",
    "endTime": "2026-10-28T12:00:00Z",
    "status": "PENDING",
    "checkedInAt": null,
    "cancelledAt": null,
    "cancellationReason": null,
    "notes": "Đặt chỗ gửi xe buổi sáng",
    "sessionId": null,
    "createdAt": "2026-10-27T14:00:00Z",
    "updatedAt": "2026-10-27T14:00:00Z"
  }
}
```

- **Mã lỗi:**
  - `403 Forbidden`: Người dùng cố ý xem booking của tài khoản khác.
  - `404 Not Found`: Không tìm thấy ID đặt chỗ.

---

### 3.4. POST `/api/v1/bookings/{id}/cancel` – Hủy lượt đặt chỗ

Người dùng hoặc nhân viên vận hành hủy lượt đặt chỗ. Hệ thống tự động giải phóng 1 suất sức chứa giữ trước cho bãi xe.

- **Request Body (JSON):**
```json
{
  "reason": "Lịch trình cá nhân thay đổi"
}
```

- **Response thành công (`200 OK`):**
```json
{
  "message": "Hủy lượt đặt chỗ thành công. Sức chứa giữ chỗ đã được giải phóng.",
  "data": {
    "id": 101,
    "lotId": 1,
    "lotName": "Bãi Đỗ Xe Quận 1",
    "status": "CANCELLED",
    "cancelledAt": "2026-10-27T14:30:00Z",
    "cancellationReason": "Lịch trình cá nhân thay đổi",
    "updatedAt": "2026-10-27T14:30:00Z"
  }
}
```

- **Mã lỗi:**
  - `400 Bad Request`: Booking đã ở trạng thái `CHECKED_IN` (xe đã vào bãi), `CANCELLED` hoặc `EXPIRED`.
  - `403 Forbidden`: Người dùng cố ý hủy booking của người khác (hoặc nhân viên ngoài phạm vi bãi phân công).
  - `404 Not Found`: Không tìm thấy ID đặt chỗ.

---

### 3.5. GET `/api/v1/bookings` – Danh sách đặt chỗ vận hành (STAFF / MANAGER / ADMIN)

API quản lý nội bộ dành cho nhân viên bãi và ban quản lý. Áp dụng Per-Lot RBAC (STAFF/MANAGER chỉ thấy các bãi mình được phân công).

- **Query Parameters:**
  - `lotId` (int, optional): Lọc theo ID bãi xe.
  - `status` (string, optional): Lọc theo `PENDING`, `CHECKED_IN`, `CANCELLED`, `EXPIRED`.
  - `plateNumber` (string, optional): Tìm kiếm theo biển số xe.
  - `fromDate` (DateTime, optional): Lọc theo thời gian bắt đầu từ ngày.
  - `toDate` (DateTime, optional): Lọc theo thời gian bắt đầu đến ngày.
  - `pageNumber` (int, default: 1).
  - `pageSize` (int, default: 20).

- **Response thành công (`200 OK`):** Trả về `PagedResult<BookingDto>`.

---

### 3.6. GET `/api/v1/parking-lots/{id}/capacity` – Thông tin sức chứa thời gian thực

Endpoint cho phép Frontend tra cứu tình trạng sức chứa theo thời gian thực.
Hệ thống tự động phân tách thông tin hiển thị theo phân quyền (RBAC):
- **Khách ẩn danh / Người dùng thông thường (Public):** Chỉ hiển thị các trường dữ liệu cần thiết phục vụ đặt chỗ và đỗ xe. Các trường giám sát chẩn đoán nội bộ bị ẩn hoàn toàn (omitted).
- **Nhân viên / Quản lý / Quản trị viên (`STAFF`, `MANAGER`, `ADMIN`):** Nhận đầy đủ dữ liệu vận hành, số liệu cảm biến IoT và ghi chú chẩn đoán chênh lệch (`discrepancyNotes`).

- **Quyền:** Mở công khai (`[AllowAnonymous]`)
- **Public Response (Khách ẩn danh / Chưa đăng nhập):**
```json
{
  "message": "Lấy thông tin sức chứa bãi đỗ xe thành công.",
  "data": {
    "lotId": 1,
    "lotName": "Bãi Đỗ Xe Quận 1",
    "lotAddress": "123 Lê Lợi, Quận 1, TP.HCM",
    "totalCapacity": 100,
    "availableForWalkIn": 10,
    "availableForBooking": 10,
    "isFull": false
  }
}
```

- **Staff / Admin Response (Đã đăng nhập với vai trò nội bộ):**
```json
{
  "message": "Lấy thông tin sức chứa bãi đỗ xe thành công.",
  "data": {
    "lotId": 1,
    "lotName": "Bãi Đỗ Xe Quận 1",
    "lotAddress": "123 Lê Lợi, Quận 1, TP.HCM",
    "totalCapacity": 100,
    "activeSessionsCount": 80,
    "activeReservationsCount": 10,
    "totalOccupiedOrReserved": 90,
    "availableForWalkIn": 10,
    "availableForBooking": 10,
    "isFull": false,
    "physicalOccupiedSlotsCount": 78,
    "physicalEmptySlotsCount": 22,
    "discrepancyNotes": "Ghi nhận chênh lệch IoT: 78 ô vật lý báo có xe (OCCUPIED), hệ thống đang có 80 phiên đỗ xe (ACTIVE/PAID)."
  }
}
```

*Ý nghĩa các trường công suất:*
- `totalCapacity`: Tổng số ô đỗ hợp lệ đang hoạt động (không tính ô `MAINTENANCE` hoặc `UNAVAILABLE`). Nếu toàn bộ ô hỏng/bảo trì, giá trị là `0`.
- `availableForWalkIn` / `availableForBooking`: `max(0, totalCapacity - (max(activeSessions, physicalOccupied) + pendingBookings))`.
- `isFull`: `true` nếu không còn vị trí khả dụng (`<= 0`).
- `activeSessionsCount` *(Staff/Admin only)*: Số lượng xe đang có phiên đỗ thực tế trong bãi (`ACTIVE` hoặc `PAID`).
- `activeReservationsCount` *(Staff/Admin only)*: Số lượng lượt đặt chỗ `PENDING` đang giữ công suất chờ xe đến.
- `totalOccupiedOrReserved` *(Staff/Admin only)*: Tổng số chỗ đang sử dụng theo công thức bảo thủ: `max(activeSessions, physicalOccupied) + activeReservationsCount`.
- `physicalOccupiedSlotsCount` *(Staff/Admin only)*: Số ô cảm biến IoT thực tế báo có xe.
- `discrepancyNotes` *(Staff/Admin only)*: Cảnh báo đối soát an toàn nếu cảm biến IoT và số phiên đỗ đang mở có độ lệch (ví dụ xe vừa qua barrier đang tìm chỗ đỗ).

---

## 4. QUY TRÌNH CHECK-IN TÍCH HỢP ĐẶT CHỖ & NGUYÊN TẮC KHÔNG CỘNG DỒN KÉP (NO DOUBLE-COUNTING)

Khi xe đến cổng vào:
1. Camera OCR nhận diện biển số (ví dụ: `51A-12345`).
2. Nhân viên hoặc Barrier gọi API `POST /api/v1/parking-sessions/check-in`:
```json
{
  "lotId": 1,
  "plateNumber": "51A-12345",
  "checkInImageUrl": "https://cdn.smartparking.vn/gate/in_12345.jpg"
}
```

### Trường hợp A: Xe ĐÃ CÓ ĐẶT CHỖ (`PENDING` Booking)
- Backend phát hiện Booking `PENDING` khớp biển số tại bãi xe.
- Chuyển trạng thái Booking: `PENDING` $\rightarrow$ `CHECKED_IN` atomically.
- Tạo phiên đỗ xe mới với trạng thái `ACTIVE` và gán `session.BookingId = booking.Id`.
- **Bảo toàn công suất (No Double-Counting):**
  - Trước Check-in: `activeReservationsCount = 1`, `activeSessionsCount = 0` $\rightarrow$ Tổng chiếm dụng = 1.
  - Sau Check-in: `activeReservationsCount = 0`, `activeSessionsCount = 1` $\rightarrow$ Tổng chiếm dụng = 1.
  - Sức chứa khả dụng của bãi **KHÔNG BỊ GIẢM THÊM 1 SUẤT OAN UỔNG**.

### Trường hợp B: Xe VÃNG LAI (Không có Booking)
- Backend kiểm tra `availableForWalkIn = totalCapacity - (activeSessionsCount + activeReservationsCount)`.
- Nếu `availableForWalkIn > 0`: Cho phép Check-in tạo phiên `ACTIVE`.
- Nếu `availableForWalkIn <= 0`: **Từ chối Check-in ngay lập tức với mã `409 Conflict`**:
  ```json
  {
    "message": "Bãi đỗ xe 'Bãi Đỗ Xe Quận 1' đã hết chỗ khả dụng cho khách vãng lai. Toàn bộ vị trí còn lại đã được đặt trước hoặc đã có xe đỗ."
  }
  ```
  *(Đảm bảo khách vãng lai không bao giờ cướp mất chỗ của khách đã đặt trước và đang trên đường đến bãi).*

---

## 5. CƠ CHẾ BẢO VỆ CHỐNG OVERBOOKING ĐỒNG THỜI (CONCURRENCY STRATEGY)

Hệ thống bảo vệ giao dịch đa tầng nghiêm ngặt trên toàn bộ các luồng thay đổi trạng thái và sức chứa (Tạo Booking, Hủy Booking, Check-in, Check-out):
1. **In-Memory Lock per Lot (`ParkingLotLockHelper`):** `SemaphoreSlim` theo từng `LotId` đảm bảo các request trong cùng tiến trình ứng dụng xếp hàng tuần tự khi kiểm tra, trừ hoặc giải phóng công suất.
   - **Thứ tự khóa nhất quán:** `SemaphoreSlim.WaitAsync` $\rightarrow$ `BeginTransactionAsync` $\rightarrow$ `pg_advisory_xact_lock(700000000 + lotId)` $\rightarrow$ Xử lý nghiệp vụ $\rightarrow$ `CommitAsync` $\rightarrow$ `SemaphoreSlim.Release`.
   - **Chống Deadlock & Non-reentrant:** Không có thao tác lồng nhau (nested acquisitions) giữa các luồng. Việc giải phóng sức chứa từ Hủy Booking (`CancelBookingAsync`) hoặc Rời bãi (`CheckOutAsync`) cam kết vào cơ sở dữ liệu trước khi các request kiểm tra sức chứa xếp hàng phía sau được phép đọc.
2. **PostgreSQL Authoritative Advisory Lock:** Gọi hàm `SELECT pg_advisory_xact_lock(700000000 + lotId);` trực tiếp trong Transaction. Khóa này tự động giải phóng khi transaction `COMMIT` hoặc `ROLLBACK`. Đảm bảo an toàn tuyệt đối ngay cả khi chạy trên nhiều container / cluster.
3. **Unique Filtered Index:** `uq_bookings_pending_vehicle` trên bảng `bookings (vehicle_id)` với điều kiện `WHERE status = 'PENDING'` ngăn chặn hoàn toàn việc 1 phương tiện cố ý tạo 2 lượt đặt chỗ đồng thời.

---

## 6. HƯỚNG DẪN KIỂM THỬ SWAGGER DÀNH CHO DANH (QA)

### Kịch bản 1: Đặt chỗ hợp lệ & Kiểm tra sức chứa
1. Đăng nhập tài khoản `user1` $\rightarrow$ Copy access token vào nút **Authorize** trên Swagger.
2. Gọi `GET /api/v1/parking-lots/1/capacity`: Ghi nhận `availableForBooking`.
3. Gọi `POST /api/v1/bookings` với `lotId: 1`, `plateNumber: "51A-99999"`.
   - Kết quả mong đợi: `201 Created`, trả về thông tin booking với `status: "PENDING"`.
4. Gọi lại `GET /api/v1/parking-lots/1/capacity`:
   - Kết quả mong đợi: `availableForBooking` giảm 1 (và `activeReservationsCount` tăng 1 nếu gọi với tài khoản Staff/Admin).

### Kịch bản 2: Ngăn chặn Đặt chỗ trùng (Duplicate Active Booking)
1. Dùng tiếp tài khoản `user1`, gọi lại `POST /api/v1/bookings` với cùng biển số `51A-99999`.
2. Kết quả mong đợi: `409 Conflict`, thông báo phương tiện đã có 1 lượt đặt chỗ đang có hiệu lực.

### Kịch bản 3: Hủy đặt chỗ & Giải phóng sức chứa
1. Gọi `POST /api/v1/bookings/{id}/cancel` với ID vừa tạo ở Kịch bản 1.
2. Kết quả mong đợi: `200 OK`, `status: "CANCELLED"`.
3. Kiểm tra lại `GET /api/v1/parking-lots/1/capacity`:
   - Kết quả mong đợi: `availableForBooking` tăng 1 (sức chứa đã được giải phóng ngay lập tức).

### Kịch bản 4: Check-in xe có đặt chỗ (Chuyển đổi công suất)
1. Đặt chỗ mới cho biển số `51B-12345`.
2. Đăng nhập tài khoản `STAFF` hoặc `ADMIN`.
3. Gọi `POST /api/v1/parking-sessions/check-in` với `lotId: 1`, `plateNumber: "51b-12345"`.
4. Kết quả mong đợi: `201 Created`, phiên đỗ xe có trường `bookingId` khớp với ID booking.
5. Gọi `GET /api/v1/bookings/{id}`: Trạng thái booking chuyển sang `CHECKED_IN`, có trường `checkedInAt`.
6. Gọi `GET /api/v1/parking-lots/1/capacity`:
   - `availableForBooking` không đổi (không bị trừ thêm suất oan uổng).

### Kịch bản 5: Chặn xe vãng lai khi bãi hết chỗ (Capacity Conflict)
1. Chọn bãi xe chỉ còn đúng số chỗ bằng số lượt đặt chỗ `PENDING`.
2. Dùng tài khoản `STAFF` check-in cho một biển số vãng lai không có đặt chỗ (`51C-WALKIN`).
3. Kết quả mong đợi: `409 Conflict`, từ chối nhận xe vãng lai để bảo lưu chỗ cho khách đã đặt trước.

---

## 7. LƯU Ý DÀNH CHO SONG (FRONTEND CONTRACT UPDATE)

1. **Hiển thị tình trạng bãi xe công khai (Public UI):**
   - Dùng endpoint `GET /api/v1/parking-lots/{id}/capacity`.
   - **Lưu ý quan trọng về Payload DTO:**
     - Khi gọi ẩn danh (chưa đăng nhập), Backend **CHỈ** trả về: `lotId`, `lotName`, `lotAddress`, `totalCapacity`, `availableForWalkIn`, `availableForBooking`, `isFull`.
     - Các trường nội bộ như `activeSessionsCount`, `activeReservationsCount`, `physicalOccupiedSlotsCount`, `discrepancyNotes` sẽ là `undefined`/`null` và không xuất hiện trong JSON.
     - **Công thức hiển thị thanh tiến trình công khai:**
       $$\% \text{ Đã sử dụng} = \frac{\text{totalCapacity} - \text{availableForBooking}}{\text{totalCapacity}} \times 100\%$$
   - Nếu `isFull === true` hoặc `availableForBooking === 0`: Disable nút "Đặt chỗ ngay", hiển thị badge "Hết chỗ".
2. **Màn hình Dashboard Giám sát Dành cho Nhân viên / Ban Quản Lý (Internal Staff Dashboard):**
   - Khi gọi `GET /api/v1/parking-lots/{id}/capacity` có kèm Bearer Token của `STAFF`, `MANAGER` hoặc `ADMIN`, Backend sẽ trả về đầy đủ các trường chẩn đoán:
     - `activeSessionsCount`: Số xe đang có phiên đỗ thực tế trong bãi.
     - `activeReservationsCount`: Số lượt giữ chỗ PENDING.
     - `physicalOccupiedSlotsCount`: Số ô cảm biến IoT đang báo có vật cản.
     - `discrepancyNotes`: Cảnh báo đối soát lệch giữa cảm biến và phiên xe.
3. **Form đặt chỗ:**
   - Người dùng có thể chọn nhanh từ danh sách xe đã lưu trong profile (`GET /api/v1/vehicles/my`) hoặc nhập trực tiếp biển số xe mới.
   - Không cần bắt buộc chọn giờ kết thúc (`endTime` là tùy chọn).
   - Không cho người dùng chọn trước ô đỗ vật lý (đúng tinh thần Locked Rule 1 & 2).
4. **Tab "Đặt chỗ của tôi":**
   - Gọi `GET /api/v1/bookings/my`.
   - Đối với các booking có `status === "PENDING"`: Hiển thị nút "Hủy đặt chỗ" mở popup nhập lý do.
   - Đối với các booking có `status === "CHECKED_IN"`: Ẩn nút hủy, hiển thị badge "Đã vào bãi" kèm link xem phiên đỗ hiện tại.

---

## 8. CƠ SỞ DỮ LIỆU & MIGRATION

- **Migration artifact đã tạo (chưa apply lên Neon):**
  - File: `backend/src/SmartParking.Infrastructure/Migrations/20261027135014_AddBookingsAndCapacity.cs`
  - ModelSnapshot: `SmartParkingDbContextModelSnapshot.cs`
- **Các bảng và ràng buộc chính:**
  - Bảng mới: `bookings` (khóa ngoại `lot_id`, `user_id`, `vehicle_id` với `ON DELETE RESTRICT`).
  - Bảng chỉnh sửa: `parking_sessions` thêm cột `booking_id` (nullable, 1-1 với `bookings`, `ON DELETE RESTRICT`).
  - Check constraint: `chk_bookings_status IN ('PENDING', 'CHECKED_IN', 'CANCELLED', 'EXPIRED')`.
  - Filtered unique index: `uq_bookings_pending_vehicle` trên `(vehicle_id)` lọc `WHERE status = 'PENDING'`.
- **Cảnh báo triển khai:**
  - **TUYỆT ĐỐI KHÔNG CHẠY MIGRATION TỰ ĐỘNG LÊN NEON PRODUCTION.**
  - Migration sẽ được Owner và Dương review và deploy theo quy trình CI/CD có kiểm soát.

---

## 9. CÁC QUYẾT ĐỊNH NGHIỆP VỤ CHỜ OWNER PHÊ DUYỆT (CHƯA ÁP ĐẶT CỨNG)

Hệ thống đã triển khai mô hình giữ công suất an toàn và tinh gọn nhất. Các chính sách dưới đây thuộc thẩm quyền Owner và sẽ được tích hợp cấu hình khi có quyết định chính thức:
1. Khoảng thời gian cho phép đặt trước tối đa (Advance Booking Window - ví dụ tối đa trước 24h hay 7 ngày).
2. Thời hạn hết hạn đặt chỗ nếu không đến (No-show / Expiration Timeout - ví dụ sau 30 phút kể từ giờ hẹn).
3. Hạn chót hủy đặt chỗ (Cancellation Cutoff - ví dụ phải hủy trước 15 phút).
4. Giới hạn số lượng lượt đặt chỗ tối đa đang mở trên mỗi tài khoản người dùng.
5. Chính sách phí đặt chỗ hoặc tiền cọc (hiện tại đặt chỗ giữ công suất không thu phí).
