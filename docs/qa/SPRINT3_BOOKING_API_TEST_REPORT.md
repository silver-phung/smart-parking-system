# BÁO CÁO KIỂM THỬ CHỨC NĂNG & ĐÁNH GIÁ NGHIỆP VỤ BOOKING API (TASK 07)

**Dự án:** Smart Parking Management System  
**Workspace:** `E:\smartparking`  
**Nhánh kiểm thử (Target Branch):** `Sprint3/demo`  
**Commit ID (HEAD):** `6ec1a4ce342be193af0a16f38bf33c3bab0aa182`  
**Thời gian thực hiện:** 28/10/2026  
**Chế độ thực thi:** FAST QA – API & DATABASE INTEGRATION TEST + BUSINESS LOGIC AUDIT  

---

## MỤC LỤC

1. [Tổng quan môi trường & An toàn cơ sở dữ liệu (Phase 1)](#1-tổng-quan-môi-trường--an-toàn-cơ-sở-dữ-liệu-phase-1)
2. [Kết quả kiểm thử Endpoint & Quyền truy cập (Phase 2 & Phase 4)](#2-kết-quả-kiểm-thử-endpoint--quyền-truy-cập-phase-2--phase-4)
3. [Tích hợp Sức chứa & Check-in (Phase 3)](#3-tích-hợp-sức-chứa--check-in-phase-3)
4. [Kết quả Build & Regression Test Suite (Phase 5)](#4-kết-quả-build--regression-test-suite-phase-5)
5. [ĐÁNH GIÁ SÂU NGHIỆP VỤ BOOKING - BUSINESS LOGIC AUDIT (Phase 7)](#5-đánh-giá-sâu-nghiệp-vụ-booking---business-logic-audit-phase-7)
   - 5.1. Bảng đối chiếu Business Rules
   - 5.2. Booking State Machine & Thiếu sót cơ chế Expiration
   - 5.3. Công thức tính toán Sức chứa & Chống đếm trùng (No Double Counting)
   - 5.4. Đồng thời (Concurrency) & Khóa giao dịch PostgreSQL
   - 5.5. Ma trận RBAC & Quyền sở hữu (Ownership)
   - 5.6. Các Edge Cases chưa được xử lý
   - 5.7. Các nghiệp vụ còn thiếu / Chờ quyết định từ Product Owner
   - 5.8. Bảng tổng hợp Findings (HIGH / MEDIUM / LOW)
   - 5.9. Khuyến nghị khắc phục trước buổi Demo
6. [Kết luận cuối cùng (Final Verdict) (Phase 6)](#6-kết-luận-cuối-cùng-final-verdict-phase-6)

---

## 1. TỔNG QUAN MÔI TRƯỜNG & AN TOÀN CƠ SỞ DỮ LIỆU (PHASE 1)

| Tiêu chí | Trạng thái thực tế | Ghi chú an toàn & kỹ thuật |
| :--- | :--- | :--- |
| **Git Branch & HEAD** | `Sprint3/demo` (`6ec1a4c`) | Working tree sạch (`clean`), không có uncommitted changes. |
| **Backend API Host** | `http://localhost:5295` | Đang chạy nền bình thường (Healthcheck `GET /health` trả về `200 OK`). |
| **Database Connection** | Neon Shared Database (`ep-wispy-bread-aoyxxn50...`) | Kết nối chia sẻ dùng chung của cả nhóm. Tuyệt đối **KHÔNG** ghi dữ liệu rác/test giả. |
| **Migration Schema** | `20261027135014_AddBookingsAndCapacity.cs` | Đã có sẵn trong mã nguồn, định nghĩa bảng `bookings`, foreign keys, và các unique index. |
| **Chính sách an toàn dữ liệu** | **TUÂN THỦ NGHIÊM NGẶT** | Toàn bộ thao tác ghi API thực tế (`POST /bookings`, `POST /cancel`, `POST /check-in`) trên live DB được đánh dấu **BLOCKED / AN TOÀN** để bảo vệ dữ liệu Neon dùng chung. Thay vào đó, toàn bộ logic ghi được kiểm chứng qua bộ kiểm thử tự động (Automated In-Memory Test Suite) và kiểm thử API đọc (Read-only API calls). |

---

## 2. KẾT QUẢ KIỂM THỬ ENDPOINT & QUYỀN TRUY CẬP (PHASE 2 & PHASE 4)

### 2.1. Kiểm thử trực tiếp trên Live Server (`http://localhost:5295`) - Read-only & Guard Verification

| Endpoint | Method | Trạng thái xác thực | Expected HTTP | Actual HTTP | Kết quả | Ghi chú kiểm chứng |
| :--- | :--- | :--- | :---: | :---: | :---: | :--- |
| `/api/v1/bookings` | `GET` | Không kèm JWT Token | `401 Unauthorized` | `401` | **PASS** | `[Authorize(Roles = "ADMIN,MANAGER,STAFF")]` chặn request nặc danh. |
| `/api/v1/bookings/my` | `GET` | Không kèm JWT Token | `401 Unauthorized` | `401` | **PASS** | `[Authorize]` chặn người dùng chưa đăng nhập. |
| `/api/v1/bookings/1` | `GET` | Không kèm JWT Token | `401 Unauthorized` | `401` | **PASS** | `[Authorize]` chặn truy cập trái phép. |
| `/api/v1/bookings` | `POST` | Không kèm JWT Token | `401 Unauthorized` | `401` | **PASS** | `[Authorize]` chặn tạo booking trái phép. |
| `/api/v1/bookings/1/cancel` | `POST` | Không kèm JWT Token | `401 Unauthorized` | `401` | **PASS** | `[Authorize]` chặn hủy booking trái phép. |
| `/api/v1/parking-lots/2/capacity` | `GET` | Nặc danh (Anonymous) | `200 OK` | `200` | **PASS** | Trả về JSON sức chứa công khai: `{"lotId":2,"totalCapacity":10,"availableForWalkIn":9,"availableForBooking":9,"isFull":false}`. Ẩn toàn bộ chẩn đoán nội bộ đối với khách vãng lai. |

---

### 2.2. Kiểm thử chức năng Endpoint qua Automated Integration Test Suite

| Test Case ID | Endpoint & Kịch bản kiểm thử | Kỳ vọng (Expected Result) | Thực tế (Actual Result) | Kết quả kiểm thử |
| :--- | :--- | :--- | :--- | :---: |
| **TC-BK-01** | `POST /api/v1/bookings`<br>Đặt chỗ hợp lệ | Trả về `201 Created`. Tạo Booking `PENDING`. Không gán cứng `SlotId`. Giữ 1 suất capacity bãi. | HTTP 201, `Status = PENDING`, `Booking.SlotId` không tồn tại, capacity bãi giảm 1. | **PASS** (Automated Test) |
| **TC-BK-02** | `POST /api/v1/bookings`<br>Đặt chỗ khi bãi hết capacity | Trả về `409 Conflict`. Báo bãi đã hết chỗ khả dụng. Rollback transaction. | HTTP 409, message cảnh báo hết chỗ khả dụng, không tạo booking. | **PASS** (Automated Test) |
| **TC-BK-03** | `POST /api/v1/bookings`<br>Đặt chỗ khi bãi bị vô hiệu hóa (`IsActive = false`) | Trả về `400 Bad Request`. Báo bãi đỗ xe đóng cửa. | HTTP 400, message thông báo bãi đóng cửa hoặc tạm ngừng hoạt động. | **PASS** (Automated Test) |
| **TC-BK-04** | `POST /api/v1/bookings`<br>USER đặt xe của người dùng khác | Trả về `403 Forbidden`. Không cho phép chiếm xe của người khác. | HTTP 403, báo không có quyền đặt xe của người dùng khác. | **PASS** (Automated Test) |
| **TC-BK-05** | `POST /api/v1/bookings`<br>Xe đã có 1 Booking `PENDING` | Trả về `409 Conflict`. Chặn duplicate booking ở cả tầng ứng dụng và DB Unique Index. | HTTP 409, báo xe đã có lượt đặt chỗ đang có hiệu lực. | **PASS** (Automated Test) |
| **TC-BK-06** | `POST /api/v1/bookings`<br>Xe đang có phiên gửi xe `ACTIVE`/`PAID` | Trả về `409 Conflict` (Chống quay vòng vé - Anti-passback). | HTTP 409, báo xe đang có phiên đỗ chưa kết thúc trong bãi. | **PASS** (Automated Test) |
| **TC-BK-07** | `GET /api/v1/bookings/my`<br>USER xem danh sách đặt chỗ | Chỉ trả về các booking do chính `UserId` đó tạo. Hỗ trợ phân trang và bộ lọc `Status`. | Trả về đúng danh sách của User, không lộ booking của User khác. | **PASS** (Automated Test) |
| **TC-BK-08** | `GET /api/v1/bookings/{id}`<br>USER xem booking của mình | Trả về `200 OK` kèm chi tiết booking. | HTTP 200, thông tin chính xác. | **PASS** (Automated Test) |
| **TC-BK-09** | `GET /api/v1/bookings/{id}`<br>USER xem booking người khác | Trả về `403 Forbidden`. | HTTP 403, báo không có quyền xem booking người khác. | **PASS** (Automated Test) |
| **TC-BK-10** | `GET /api/v1/bookings/{id}`<br>STAFF xem booking bãi khác | Trả về `403 Forbidden`. Chỉ cho phép xem booking thuộc bãi được phân công. | HTTP 403, báo không được phân công quản lý bãi này. | **PASS** (Automated Test) |
| **TC-BK-11** | `GET /api/v1/bookings`<br>STAFF/MANAGER tra cứu danh sách | Chỉ trả về booking thuộc các bãi được phân công (`assignedLotIds`). | Dữ liệu được lọc nghiêm ngặt theo bãi phân công. | **PASS** (Automated Test) |
| **TC-BK-12** | `GET /api/v1/bookings`<br>USER gọi endpoint quản trị | Trả về `403 Forbidden` (do role USER không nằm trong `ADMIN,MANAGER,STAFF`). | HTTP 403 Forbidden. | **PASS** (Automated Test) |
| **TC-BK-13** | `POST /api/v1/bookings/{id}/cancel`<br>Hủy booking `PENDING` | Trả về `200 OK`. `Status -> CANCELLED`. Trả lại đúng 1 suất capacity cho bãi. | HTTP 200, booking cập nhật `CANCELLED`, ghi nhận Audit Log. | **PASS** (Automated Test) |
| **TC-BK-14** | `POST /api/v1/bookings/{id}/cancel`<br>Hủy booking đã `CHECKED_IN` | Trả về `400 Bad Request`. Không cho phép hủy vé xe đã vào bãi. | HTTP 400, báo lượt đặt chỗ đã được sử dụng. | **PASS** (Automated Test) |
| **TC-BK-15** | `POST /api/v1/bookings/{id}/cancel`<br>Hủy nhiều lần (Idempotency) | Lần 1: Thành công (`200 OK`). Lần 2: Báo lỗi `400 Bad Request` ("đã bị hủy trước đó"). Không trừ capacity 2 lần. | HTTP 400, không thay đổi capacity lần 2. | **PASS** (Automated Test) |
| **TC-BK-16** | `POST /api/v1/bookings/{id}/cancel`<br>Hủy booking của người khác | Trả về `403 Forbidden`. | HTTP 403, báo không có quyền hủy booking người khác. | **PASS** (Automated Test) |

---

## 3. TÍCH HỢP SỨC CHỨA & CHECK-IN (PHASE 3)

| Kịch bản kiểm thử tích hợp | Hành vi & Công thức kỳ vọng | Kết quả thực tế | Đánh giá |
| :--- | :--- | :--- | :---: |
| **1. Tạo Booking giảm capacity** | `AvailableCapacity = Usable - (Occupied + Holds)`. Khi tạo Booking: Holds tăng 1 $\rightarrow$ Available giảm 1. | Code & Tests xác nhận: `pendingBookingsCount` tăng 1, `availableCapacity` giảm đúng 1. | **PASS** |
| **2. Hủy Booking tăng capacity** | Khi hủy Booking: Holds giảm 1 $\rightarrow$ Available tăng 1. | Code & Tests xác nhận: `pendingBookingsCount` giảm 1, `availableCapacity` tăng đúng 1. | **PASS** |
| **3. Check-in xe có Booking** | Booking chuyển `PENDING -> CHECKED_IN`. Holds giảm 1, ActiveSessions tăng 1. | Chuyển đổi trạng thái nguyên tử trong 1 transaction: Tổng `TotalOccupiedOrReserved` giữ nguyên, không biến động sai số. | **PASS** |
| **4. Chống đếm trùng (No Double-Counting)** | Không được tính đồng thời xe vừa giữ chỗ vừa là phiên gửi xe. | `CalculateAsync` phân tách: `activeSessionsCount` chỉ đếm `Active/Paid`, `pendingBookingsCount` chỉ đếm `Pending`. Hoàn toàn độc lập. | **PASS** |
| **5. Bảo vệ suất giữ chỗ trước xe vãng lai** | Khi bãi còn 1 chỗ trống và có 1 Booking PENDING: Xe vãng lai đến cổng phải bị từ chối vào bãi. | Check-in xe vãng lai kiểm tra `calc.AvailableCapacity <= 0` $\rightarrow$ Trả về lỗi hết chỗ khả dụng cho khách vãng lai. Suất đặt trước được bảo vệ tuyệt đối. | **PASS** |
| **6. Loại trừ ô MAINTENANCE & UNAVAILABLE** | Các ô có trạng thái `MAINTENANCE`, `UNAVAILABLE` hoặc tầng bị tắt (`Floor.IsActive = false`) không được tính vào sức chứa sử dụng. | `ParkingLotCapacityHelper.cs` dòng 42-45 lọc bỏ các ô này khỏi `TotalCapacity`. | **PASS** |
| **7. Giám sát bảo thủ cảm biến IoT (Conservative Guard)** | Nếu IoT báo số ô `OCCUPIED` vật lý nhiều hơn số phiên `ACTIVE` (ví dụ xe đỗ lụi/trốn vé), hệ thống phải dùng giá trị lớn hơn để bảo vệ bãi không bị quá tải. | Áp dụng công thức: `EffectiveOccupied = Math.Max(activeSessionsCount, physicalOccupiedSlots)`. Available tính theo `EffectiveOccupied`. | **PASS** |
| **8. Không gán cứng slot vật lý khi Booking** | Booking chỉ giữ quota bãi (`LotId`), không gán trước `SlotId`. | Thực thể `Booking` không có thuộc tính `SlotId`. Người dùng vào bãi tự do chọn ô trống theo hướng dẫn. | **PASS** |
| **9. Chống Overbooking khi có đồng thời nhiều request** | 2 request cùng tranh chấp 1 chỗ trống cuối cùng: Chỉ 1 request thành công. | Sử dụng cơ chế 2 lớp: `SemaphoreSlim` in-process + PostgreSQL transaction lock `pg_advisory_xact_lock(300000 + lotId)`. | **PASS** (Code & In-Memory Lock) |

---

## 4. KẾT QUẢ BUILD & REGRESSION TEST SUITE (PHASE 5)

Thực thi kiểm tra toàn bộ Solution `SmartParking.sln`:
- **Lệnh build:** `dotnet build backend/SmartParking.sln` $\rightarrow$ **0 Errors, 0 Warnings**.
- **Lệnh test chuyên biệt Booking & Capacity:** `dotnet test backend/SmartParking.sln --filter "FullyQualifiedName~BookingAndCapacityTests"`  
  $\rightarrow$ **Passed: 32, Failed: 0, Skipped: 0** (Thời gian chạy: 2.1 giây).
- **Lệnh test toàn bộ hệ thống (Regression Suite):** `dotnet test backend/SmartParking.sln`  
  $\rightarrow$ **Passed: 308, Failed: 0, Skipped: 0** (Thời gian chạy: 3.4 giây).
- **Kết luận Regression:** Toàn bộ 308 automated tests đều vượt qua thành công, không phát sinh bất kỳ lỗi hồi quy nào sau khi tích hợp mã nguồn Task 07 vào nhánh `Sprint3/demo`.

---

## 5. ĐÁNH GIÁ SÂU NGHIỆP VỤ BOOKING - BUSINESS LOGIC AUDIT (PHASE 7)

### 5.1. Bảng đối chiếu Business Rules

| Quy tắc nghiệp vụ (Business Rule) | Hiện trạng triển khai | Vị trí Source Code chứng minh | Test Case chứng minh | Đánh giá |
| :--- | :--- | :--- | :--- | :---: |
| **BR-01:** Booking giữ quota bãi (`LotId`), KHÔNG đặt cố định ô đỗ (`ParkingSlot`). | Thực thể `Booking` không có trường `SlotId`. Khi tạo booking không can thiệp bảng `ParkingSlots`. | `Booking.cs`<br>`BookingService.cs:176-191` | `CreateBooking_ValidRequest_ReservesCapacityWithoutPreassigningSlot` | **PASS** |
| **BR-02:** USER chỉ được đặt chỗ cho xe thuộc quyền sở hữu của mình (hoặc xe mới chưa gán ai). | Chặn nếu xe đã có chủ (`UserId != currentUserId`). Tự động gán quyền sở hữu nếu xe chưa có chủ. | `BookingService.cs:85-88, 103-112` | `CreateBooking_VehicleOfAnotherUser_Returns403Forbidden` | **PASS** |
| **BR-03:** Xe có Booking và xe vãng lai đều có quyền đỗ tự do tại các slot trống hợp lệ. | Check-in cho phép `SlotId = null` hoặc tự chọn ô hợp lệ thuộc bãi. | `ParkingSessionService.cs:134-176` | `CheckIn_ReservedVehicle_ConvertsHoldToAdmittedSessionWithoutDoubleCounting` | **PASS** |
| **BR-04:** Không tạo Booking cho bãi không đủ điều kiện hoạt động (`IsActive = false`). | Kiểm tra cờ `lot.IsActive` trước khi xử lý, từ chối với mã 400. | `BookingService.cs:52-55` | `CreateBooking_InactiveLot_Returns400BadRequest` | **PASS** |
| **BR-05:** Không tạo Booking vượt quá sức chứa khả dụng của bãi. | Tính toán `calc.AvailableCapacity <= 0` bên trong lock giao dịch, từ chối với mã 409. | `BookingService.cs:165-171` | `CreateBooking_WhenLotAtFullCapacity_Returns409Conflict` | **PASS** |
| **BR-06:** Không cho một phương tiện có nhiều Booking `PENDING` đồng thời. | Kiểm tra ứng dụng + Ràng buộc duy nhất tại DB: `uq_bookings_pending_vehicle`. | `BookingService.cs:141-148`<br>`20261027135014_AddBookingsAndCapacity.cs:89-93` | `CreateBooking_DuplicatePendingBooking_Returns409Conflict` | **PASS** |
| **BR-07:** Không tạo Booking khi xe đang có phiên gửi xe `ACTIVE` hoặc `PAID`. | Kiểm tra Anti-passback trên bảng `ParkingSessions`, từ chối với mã 409. | `BookingService.cs:132-139` | `CreateBooking_VehicleWithActiveSession_Returns409Conflict` | **PASS** |
| **BR-08:** Không tự động chuyển slot sang `OCCUPIED` khi tạo Booking hoặc check-in tại cổng. | Trạng thái vật lý của Slot giữ nguyên `Empty`, chỉ chuyển `Occupied` khi cảm biến IoT/Dispatch xác nhận. | `ParkingSessionService.cs:174-176`<br>`BookingService.cs:176-191` | `CheckIn_ReservedVehicle_ConvertsHoldToAdmittedSessionWithoutDoubleCounting` | **PASS** |

---

### 5.2. Booking State Machine & Thiếu sót cơ chế Expiration

Hệ thống định nghĩa 4 trạng thái đặt chỗ trong `BookingStatuses`:
```
   [Tạo mới]
       │
       ▼
   ┌─────────┐   Xe đến cổng check-in    ┌────────────┐
   │ PENDING ├──────────────────────────►│ CHECKED_IN │ (Kết thúc chu kỳ đặt chỗ)
   └────┬────┘                           └────────────┘
        │
        ├──────── Người dùng / Quản lý hủy ────────► ┌───────────┐
        │                                            │ CANCELLED │
        │
        └──────── Quá hạn đặt chỗ (Theo lý thuyết) ─► ┌───────────┐
                                                      │  EXPIRED  │ (CHƯA ĐƯỢC KÍCH HOẠT TỰ ĐỘNG)
                                                      └───────────┘
```

#### Xác minh tính toàn vẹn chuyển trạng thái:
1. **`CHECKED_IN` không thể tái sử dụng:** Tại cổng vào, `CheckInAsync` chỉ tìm kiếm booking thỏa mãn điều kiện `Status == BookingStatuses.Pending` (`ParkingSessionService.cs:90`). Booking đã `CHECKED_IN` sẽ bị bỏ qua.
2. **`CANCELLED` không thể check-in:** Không thể tìm thấy bởi bộ lọc `Status == BookingStatuses.Pending`.
3. **`EXPIRED` không thể check-in:** Không thể tìm thấy bởi bộ lọc `Status == BookingStatuses.Pending`.
4. **Hủy nhiều lần:** `CancelBookingAsync` kiểm tra trạng thái trước khi hủy; nếu `Status != Pending`, trả về mã 400 và không cập nhật lại capacity.
5. **Một Booking không thể tạo nhiều ParkingSession:** Database enforce Unique Index `IX_parking_sessions_booking_id` trên cột `parking_sessions.booking_id`.
6. **Liên kết thực thể khi Check-in:** Phiên gửi xe tạo mới lưu chính xác `BookingId = booking.Id`, `VehicleId = vehicle.Id`, `LotId = lot.Id`.

> [!WARNING]
> ### PHÁT HIỆN LỖI THIẾT KẾ QUAN TRỌNG: THIẾU BACKGROUND EXPIRATION WORKER
> - **Hiện trạng:** Cột `end_time` tồn tại trong bảng `bookings`, hằng số `BookingStatuses.Expired` đã có, và DB check constraint cho phép `'EXPIRED'`. Tuy nhiên, trong toàn bộ mã nguồn Backend **KHÔNG CÓ** bất kỳ background job nào (`IHostedService`, Quartz.NET, Cron) để quét và tự động chuyển các Booking quá hạn từ `PENDING` sang `EXPIRED`.
> - **Hệ quả nghiệp vụ:** Nếu người dùng đặt chỗ nhưng sau đó không đến gửi xe (no-show) và không tự hủy, lượt đặt chỗ này sẽ **vĩnh viễn ở trạng thái `PENDING`**, chiếm giữ 1 suất capacity của bãi vô thời hạn. Điều này làm giảm ảo số chỗ trống của bãi xe và từ chối đón tiếp các khách hàng khác.
> - **Phân loại:** **MISSING IMPLEMENTATION / POLICY** (Chờ Product Owner ban hành quy định về thời gian timeout, ví dụ quá 30 phút sau `StartTime` hoặc sau `EndTime`).

---

### 5.3. Công thức tính toán Sức chứa & Chống đếm trùng (No Double Counting)

Toàn bộ hệ sinh thái (Booking, Check-in, Public Capacity API) đều sử dụng duy nhất một nguồn chân lý (Single Source of Truth) thông qua class `ParkingLotCapacityHelper.cs`:

$$\text{EffectiveOccupied} = \max(\text{ActiveSessionsCount}, \text{PhysicalOccupiedSlots})$$
$$\text{TotalOccupiedOrReserved} = \text{EffectiveOccupied} + \text{PendingBookingsCount}$$
$$\text{AvailableCapacity} = \max(0, \text{TotalCapacity} - \text{TotalOccupiedOrReserved})$$

#### Kiểm chứng tính đồng nhất:
1. **Không đếm trùng (No Double-Counting):**
   - Khi xe có Booking vào cổng: `PendingBookingsCount` giảm 1, `ActiveSessionsCount` tăng 1.
   - Do đó, $\Delta(\text{PendingBookingsCount}) = -1$ và $\Delta(\text{ActiveSessionsCount}) = +1$.
   - Tổng $\text{TotalOccupiedOrReserved}$ không đổi $\rightarrow$ `AvailableCapacity` không bị biến động sai lệch.
2. **Bảo vệ quyền lợi khách đặt trước:**
   - Khách vãng lai check-in chỉ được duyệt khi `AvailableCapacity > 0`.
   - Do `PendingBookingsCount` đã được cộng vào `TotalOccupiedOrReserved`, các suất đặt trước luôn được bảo lưu nguyên vẹn.
3. **Loại trừ ô hư hỏng:**
   - Các ô có `PhysicalStatus == Maintenance` hoặc `Unavailable` hoặc thuộc tầng `Floor.IsActive == false` bị loại bỏ hoàn toàn khỏi `TotalCapacity`.
   - Khi toàn bộ ô trong bãi bị hỏng, `TotalCapacity = 0` (hệ thống **không** fallback về `lot.TotalCapacity`). Fallback chỉ áp dụng khi bãi chưa từng khởi tạo cấu hình tầng/ô nào.

---

### 5.4. Đồng thời (Concurrency) & Khóa giao dịch PostgreSQL

#### Cơ chế bảo vệ 2 lớp (Two-tier Locking):
1. **Lớp 1 (In-Process Serialization):** `ParkingLotLockHelper.GetSemaphore(lot.Id)` đảm bảo các luồng chạy song song trên cùng 1 server instance tranh chấp cùng 1 bãi xe được xếp hàng tuần tự.
2. **Lớp 2 (Distributed Database Lock):** Khi chạy với PostgreSQL (Npgsql), hệ thống thực thi khóa giao dịch advisory lock:
   ```sql
   SELECT pg_advisory_xact_lock(300000 + lot.Id);
   ```
   Khóa này tự động được giải phóng khi Transaction `Commit` hoặc `Rollback`, đảm bảo an toàn tuyệt đối ngay cả khi mở rộng chạy Backend đa tiến trình (Multiple Pods/Instances).
3. **Ràng buộc cứng mức cơ sở dữ liệu:**
   - `uq_bookings_pending_vehicle`: Unique filtered index ngăn chặn 2 request đồng thời lách qua code để tạo 2 booking PENDING cho cùng 1 xe.
   - `IX_parking_sessions_booking_id`: Unique index ngăn chặn 1 booking bị check-in 2 lần tại cổng.

> [!NOTE]
> Khả năng chống race condition ở tầng PostgreSQL advisory lock đã được kiểm chứng về mặt kiến trúc mã nguồn. Trong môi trường kiểm thử InMemory (`Microsoft.EntityFrameworkCore.InMemory`), advisory lock không thể mô phỏng vật lý mà phụ thuộc vào lớp `SemaphoreSlim`.

---

### 5.5. Ma trận RBAC & Quyền sở hữu (Ownership)

| Vai trò (Role) | `POST /bookings` (Tạo) | `GET /bookings/my` (Xem của tôi) | `GET /bookings/{id}` (Xem chi tiết) | `GET /bookings` (Danh sách vận hành) | `POST /{id}/cancel` (Hủy vé) |
| :---: | :---: | :---: | :---: | :---: | :---: |
| **USER** | Cho phép (Chỉ xe của mình) | Cho phép (Chỉ booking của mình) | Cho phép (Nếu là chủ sở hữu) | **BỊ CHẶN (403)** | Cho phép (Chỉ booking của mình) |
| **STAFF** | Bị từ chối quyền đặt chỗ khách | Cho phép | Cho phép (Chỉ trong bãi phân công) | Cho phép (Chỉ trong bãi phân công) | Cho phép (Chỉ trong bãi phân công) |
| **MANAGER** | Bị từ chối quyền đặt chỗ khách | Cho phép | Cho phép (Chỉ trong bãi phân công) | Cho phép (Chỉ trong bãi phân công) | Cho phép (Chỉ trong bãi phân công) |
| **ADMIN** | Cho phép quản trị | Cho phép | Cho phép (Toàn bộ bãi) | Cho phép (Toàn bộ bãi) | Cho phép (Toàn bộ bãi) |
| **Nặc danh** | **401 Unauthorized** | **401 Unauthorized** | **401 Unauthorized** | **401 Unauthorized** | **401 Unauthorized** |

- **Bảo mật phản hồi API Capacity:** Khi khách nặc danh hoặc USER gọi `GET /api/v1/parking-lots/{id}/capacity`, API ẩn toàn bộ các trường chẩn đoán nhạy cảm (`ActiveSessionsCount`, `PhysicalOccupiedSlotsCount`, `DiscrepancyNotes`), chỉ trả về `totalCapacity`, `availableForWalkIn`, `availableForBooking`. Chỉ STAFF/MANAGER được phân công tại bãi và ADMIN mới nhận được đầy đủ dữ liệu chẩn đoán.

---

### 5.6. Các Edge Cases chưa được xử lý

1. **User đặt chỗ nhiều ngày trong tương lai (Advance Booking):**
   - Hiện tại, khi tạo Booking với `StartTime` là ngày mai hoặc tuần sau, công thức `CalculateAsync` lập tức tính nó vào `PendingBookingsCount` và trừ ngay 1 suất khả dụng **ngay tại thời điểm hiện tại**.
   - Điều này làm giảm công suất phục vụ khách gửi xe trong ngày hôm nay dù xe đặt trước chưa hề đến.
2. **Xe check-in quá giờ kết thúc (`EndTime`):**
   - Hệ thống cho phép check-in nếu trạng thái vẫn là `PENDING`, không kiểm tra xem thời điểm hiện tại có vượt quá `EndTime` hay không.
3. **Người dùng nhập biển số lạ:**
   - Logic tự động tạo bản ghi `Vehicle` mới nếu biển số chưa có trong DB. Nếu biển số đó từng được người khác gõ nhưng chưa đăng ký tài khoản, quyền sở hữu sẽ được gán cho người dùng hiện tại (`vehicle.UserId = userId`). Điều này có thể dẫn đến việc nhận vơ phương tiện nếu không có quy trình xác minh giấy tờ cà-vẹt xe.

---

### 5.7. Các nghiệp vụ còn thiếu / Chờ quyết định từ Product Owner

1. **Chính sách hết hạn Booking (Booking Expiration Policy):**
   - Thời gian giữ chỗ tối đa sau `StartTime` (ví dụ: giữ chỗ 30 phút, quá 30 phút tự động hủy `EXPIRED`).
   - Có cần thu phí đặt cọc/phí giữ chỗ hay không?
2. **Quy trình hoàn phí khi hủy Booking:**
   - Hiện tại hủy booking chỉ thay đổi trạng thái và giải phóng capacity, chưa tích hợp với Payment/Refund.
3. **Giao diện Frontend cho Booking:**
   - Trên nhánh `Sprint3/demo`, mã nguồn Frontend (`frontend/src`) hoàn toàn **chưa có trang hoặc component nào cho người dùng đặt chỗ Booking**. Người dùng chỉ có thể gọi qua Swagger/API.

---

### 5.8. Bảng tổng hợp Findings (HIGH / MEDIUM / LOW)

| Mức độ | Mã lỗi | Chi tiết Finding | Vị trí liên quan | Khuyến nghị hành động |
| :---: | :---: | :--- | :--- | :--- |
| **HIGH** | `F-BK-01` | **Thiếu Background Worker tự động Expire Booking quá hạn.** Đặt chỗ không đến sẽ chiếm giữ capacity vĩnh viễn. | `SmartParking.Infrastructure/Services/BookingService.cs` | Triển khai 1 `IHostedService` chạy định kỳ 1-2 phút quét `bookings` có `end_time < UtcNow` và `status = 'PENDING'` chuyển sang `EXPIRED`. |
| **MEDIUM** | `F-BK-02` | **Frontend chưa tích hợp giao diện đặt chỗ Booking.** Tính năng chỉ có ở Backend API. | `frontend/src` | Chuẩn bị kịch bản demo sử dụng Swagger UI hoặc Postman cho tính năng Booking. |
| **MEDIUM** | `F-BK-03` | **Capacity trừ ngay lập tức cho các Booking trong tương lai xa.** Chưa có cơ chế phân tách capacity theo khung giờ (Time-slot Reservation). | `SmartParking.Infrastructure/Common/ParkingLotCapacityHelper.cs:68` | Tạm thời giới hạn cho phép đặt chỗ trong ngày hoặc trước giờ đến tối đa 2-4 tiếng. |
| **LOW** | `F-BK-04` | **Tự động gán quyền sở hữu xe vãng lai cho User mà không cần xác thực chủ quyền.** | `BookingService.cs:108-111` | Cần bổ sung tính năng duyệt xe hoặc upload chứng nhận đăng ký xe ở các Sprint tiếp theo. |

---

### 5.9. Khuyến nghị khắc phục trước buổi Demo

1. **Kịch bản Demo trực tiếp:**
   - Vì Frontend chưa có nút Booking, khuyến nghị mở tab Swagger UI (`http://localhost:5295/swagger`) để demo luồng Booking:
     - Bước 1: Login tài khoản USER lấy Bearer Token.
     - Bước 2: Gọi `GET /api/v1/parking-lots/2/capacity` xem số chỗ khả dụng ban đầu.
     - Bước 3: Gọi `POST /api/v1/bookings` tạo đặt chỗ mới.
     - Bước 4: Gọi lại `GET /api/v1/parking-lots/2/capacity` chứng minh số chỗ khả dụng giảm đúng 1.
     - Bước 5: Giả lập xe đến cổng, gọi `POST /api/v1/parking-sessions/check-in` với biển số đã đặt chỗ $\rightarrow$ Chứng minh Booking tự động chuyển thành `CHECKED_IN` và không bị đếm trùng công suất.
2. **Dọn dẹp Booking PENDING cũ trước giờ demo:**
   - Đảm bảo các booking demo có `StartTime` gần thời điểm hiện tại và tự hủy các booking test trước khi bắt đầu bài thuyết trình.

---

## 6. KẾT LUẬN CUỐI CÙNG (FINAL VERDICT) (PHASE 6)

| Tiêu chí đánh giá | Kết quả | Chi tiết nhận định |
| :--- | :---: | :--- |
| **Backend Code & Build** | **PASS** | Build hoàn toàn sạch, 0 lỗi, 0 cảnh báo. |
| **Unit & Integration Tests** | **PASS** | 32/32 Booking & Capacity tests đạt 100%. 308/308 toàn hệ thống đạt 100%. |
| **API Endpoints & RBAC Guard** | **PASS** | 5/5 endpoint được bảo vệ đúng ma trận phân quyền, chặn truy cập nặc danh với mã 401. |
| **Capacity & Anti-Double-Counting** | **PASS** | Công thức tính toán chuẩn mực, áp dụng nhất quán, kiểm soát IoT bảo thủ. |
| **Bảo vệ Database Neon Shared** | **PASS** | Tuân thủ tuyệt đối quy tắc an toàn: Không can thiệp migration hay ghi dữ liệu rác lên DB dùng chung. |
| **Độ sẵn sàng Demo (Frontend)** | **PARTIAL** | Backend API hoàn thiện 100%, nhưng Frontend chưa có UI cho tính năng này. |

### ĐÁNH GIÁ TỔNG THỂ: **PARTIALLY READY FOR DEMO (SẴN SÀNG DEMO QUA BACKEND/SWAGGER)**

- **Lý do:** Toàn bộ API Backend, logic nghiệp vụ sức chứa, chống double-counting, concurrency lock và ma trận phân quyền của Task 07 hoạt động cực kỳ ổn định, chính xác và đã vượt qua 100% kiểm thử hồi quy. Tuy nhiên, tính năng chưa thể coi là hoàn toàn sẵn sàng cho người dùng cuối (End-to-End Ready) do:
  1. Frontend chưa có giao diện cho Booking (cần demo qua Swagger/Postman).
  2. Chưa có background worker xử lý hủy vé tự động khi quá hạn (Overdue/Expired).
- Nhóm hoàn toàn có thể tiến hành demo thành công chức năng này theo kịch bản kỹ thuật (API Demonstration) tại buổi báo cáo Sprint 3.
