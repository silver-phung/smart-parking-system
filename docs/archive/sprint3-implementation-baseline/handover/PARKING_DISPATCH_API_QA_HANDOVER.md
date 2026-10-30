# TÀI LIỆU BÀN GIAO QA: PARKING DISPATCH APIs (TASK 04 - PHASE 3)

**Người thực hiện:** Phụng
**Người nhận bàn giao:** Danh (QA), Song (Frontend / 3D Visualization)
**Nhánh Git:** `feature/phung-database`
**Commit:** `2dff73f`
**Automated Tests:** 206/206 PASS

---

## 1. TỔNG QUAN

Module **Parking Dispatch** phụ trách điều phối xe từ thời điểm Check-in tại cổng đến khi xe thực sự đỗ vào ô đỗ (Slot), đồng thời cung cấp đầy đủ dữ liệu không gian 3 chiều (`PositionX`, `PositionY`, `PositionZ`, `Floor`, `Lot`) phục vụ hiển thị trực quan trên mô hình 3D của bãi đỗ xe.

### Quy tắc bất biến cốt lõi (Business Invariants):
1. **Physical Status Invariant:** Check-in và việc tạo Dispatch (`ASSIGNED`) **KHÔNG** tự chuyển ô sang `OCCUPIED`. Ô vật lý giữ nguyên trạng thái `EMPTY` cho đến khi nhân viên xác nhận xe đã đỗ thực tế (`confirm-occupied` hoặc `report-wrong-slot`).
2. **Anti-Collision:** Không cho phép gán cùng một ô đỗ cho 2 dispatch đang ở trạng thái `ASSIGNED`. PostgreSQL partial unique index `uq_parking_dispatches_assigned_slot` (mã lỗi `23505`) được bắt và ánh xạ thành HTTP `409 Conflict`.
3. **Single Active Dispatch per Session:** Mỗi phiên gửi xe chỉ được có tối đa 1 dispatch đang hiệu lực (`PENDING` hoặc `ASSIGNED`). Partial unique index `uq_parking_dispatches_active_session` được ánh xạ thành HTTP `409 Conflict`.
4. **Session Checkout Synchronization:** Khi một phiên gửi xe check-out (`COMPLETED`), toàn bộ dispatch tồn đọng (`PENDING`/`ASSIGNED`) sẽ tự động chuyển sang `CANCELLED`, và ô đỗ mà xe thực sự đỗ sẽ được giải phóng về `EMPTY`.
5. **No Fake IoT:** Toàn bộ xác nhận vị trí hiện là **thủ công từ nhân viên/quản trị viên** (`MANUAL_STAFF_VERIFIED`), không sinh tín hiệu cảm biến giả khi chưa tích hợp phần cứng.

---

## 2. DANH SÁCH 8 ENDPOINTS & QUYỀN TRUY CẬP (RBAC)

| # | Phương thức | Endpoint | Quyền hạn (JWT) | Chức năng chính |
|---|---|---|---|---|
| 1 | `POST` | `/api/v1/parking-dispatches` | `ADMIN` *(Tạm khóa Manager/Staff)* | Tạo lệnh điều phối cho session `ACTIVE` (Hỗ trợ mode `AUTO` hoặc `MANUAL`). |
| 2 | `GET` | `/api/v1/parking-dispatches` | `ADMIN`, `USER` | Danh sách dispatch theo bộ lọc (`lotId`, `sessionId`, `status`, phân trang). Admin xem tất cả; User xem xe của mình. |
| 3 | `GET` | `/api/v1/parking-dispatches/{id}` | `ADMIN`, `USER` | Chi tiết một dispatch (gồm tọa độ 3D `PositionX`, `PositionY`, `PositionZ`, tầng, bãi). |
| 4 | `GET` | `/api/v1/parking-dispatches/session/{sessionId}` | `ADMIN`, `USER` | Tra cứu dispatch đang hiệu lực (`PENDING`/`ASSIGNED`) của một session. |
| 5 | `POST` | `/api/v1/parking-dispatches/{id}/assign` | `ADMIN` *(Tạm khóa Manager/Staff)* | Điều phối lại ô đỗ khi ô hiện tại gặp sự cố, vật cản. |
| 6 | `POST` | `/api/v1/parking-dispatches/{id}/confirm-occupied` | `ADMIN` *(Tạm khóa Manager/Staff)* | Xác nhận xe đã đỗ đúng ô chỉ định -> Dispatch và Slot chuyển sang `OCCUPIED`. |
| 7 | `POST` | `/api/v1/parking-dispatches/{id}/report-wrong-slot` | `ADMIN` *(Tạm khóa Manager/Staff)* | Ghi nhận xe đỗ nhầm ô -> Dispatch thành `WRONG_SLOT`, ô thực tế thành `OCCUPIED`, ô ban đầu giữ `EMPTY`. |
| 8 | `POST` | `/api/v1/parking-dispatches/{id}/cancel` | `ADMIN` *(Tạm khóa Manager/Staff)* | Hủy lệnh điều phối khi hợp lệ (đang `PENDING`/`ASSIGNED`), giải phóng slot. |

> **Lưu ý bảo mật (Security Policy):** Do chưa triển khai bảng `staff_lot_assignments`, các role `MANAGER` và `STAFF` tạm thời nhận **403 Forbidden** trên các endpoint ghi dữ liệu và tra cứu nội bộ, ngăn chặn rủi ro can thiệp bãi khác ngoài thẩm quyền.

---

## 3. CHI TIẾT REQUEST / RESPONSE VÀ EXPECTED STATUS CODES

### 3.1. Tạo lệnh điều phối: `POST /api/v1/parking-dispatches`
- **Headers:** `Authorization: Bearer <Admin_Token>`, `Content-Type: application/json`
- **Request Body:**
  ```json
  {
    "sessionId": 101,
    "assignedSlotId": 5, // Để null nếu muốn hệ thống AUTO tìm ô trống đầu tiên trong bãi
    "dispatchMode": "MANUAL", // "AUTO" hoặc "MANUAL"
    "notes": "Điều phối xe vào tầng 1"
  }
  ```
- **Response (201 Created):**
  ```json
  {
    "message": "Tạo lệnh điều phối và phân công ô đỗ thành công.",
    "data": {
      "id": 1,
      "sessionId": 101,
      "plateNumber": "51A-99999",
      "lotId": 1,
      "lotName": "Bãi A Trung Tâm",
      "assignedSlotId": 5,
      "assignedSlotCode": "A1-05",
      "assignedFloorNumber": 1,
      "assignedFloorName": "Tầng 1",
      "assignedPositionX": 10.5,
      "assignedPositionY": 0.0,
      "assignedPositionZ": 5.2,
      "actualSlotId": null,
      "actualSlotCode": null,
      "status": "ASSIGNED",
      "dispatchMode": "MANUAL",
      "dispatchedAt": "2026-10-23T11:45:00Z",
      "confirmedAt": null,
      "notes": "Điều phối xe vào tầng 1"
    }
  }
  ```
- **Status Codes:**
  - `201 Created`: Tạo và phân công thành công.
  - `400 Bad Request`: Session không ở trạng thái `ACTIVE`, hoặc ô bảo trì / thuộc bãi khác.
  - `401 Unauthorized`: Chưa truyền JWT Token.
  - `403 Forbidden`: Role không phải `ADMIN`.
  - `404 Not Found`: Không tìm thấy session hoặc slot.
  - `409 Conflict`: Session đã có dispatch đang hiệu lực, hoặc slot vừa bị gán cho xe khác (vi phạm partial unique index).

---

### 3.2. Điều phối lại ô đỗ: `POST /api/v1/parking-dispatches/{id}/assign`
- **Request Body:**
  ```json
  {
    "slotId": 8,
    "notes": "Đổi sang ô A1-08 do ô cũ bị vật cản"
  }
  ```
- **Status Codes:**
  - `200 OK`: Đổi ô thành công. Cả ô cũ và ô mới đều bảo toàn trạng thái vật lý `EMPTY`.
  - `400 Bad Request`: Dispatch không ở trạng thái `PENDING`/`ASSIGNED`, hoặc ô mới thuộc bãi khác / bảo trì.
  - `409 Conflict`: Ô mới vừa được gán cho một phương tiện khác.

---

### 3.3. Xác nhận xe đỗ đúng ô: `POST /api/v1/parking-dispatches/{id}/confirm-occupied`
- **Request Body:**
  ```json
  {
    "notes": "Nhân viên bảo vệ xác nhận xe đã vào đúng vị trí"
  }
  ```
- **Hành vi hệ thống:**
  - `parking_dispatches.status` -> `OCCUPIED`.
  - `parking_dispatches.actual_slot_id` -> `assigned_slot_id`.
  - `parking_slots.physical_status` của ô đó -> `OCCUPIED`.
- **Status Codes:**
  - `200 OK`: Xác nhận thành công.
  - `400 Bad Request`: Dispatch không ở trạng thái `ASSIGNED`.
  - `409 Conflict`: Ô đỗ đã bị xe khác chiếm (`OCCUPIED`) trước đó.

---

### 3.4. Ghi nhận xe đỗ sai ô: `POST /api/v1/parking-dispatches/{id}/report-wrong-slot`
- **Request Body:**
  ```json
  {
    "actualSlotId": 9,
    "notes": "Xe đỗ nhầm vào ô A1-09"
  }
  ```
- **Hành vi hệ thống:**
  - `parking_dispatches.status` -> `WRONG_SLOT`.
  - `parking_dispatches.actual_slot_id` -> `9`.
  - Ô chỉ định ban đầu (`assigned_slot_id`): Giữ `EMPTY` và giải phóng gán để tiếp nhận xe khác.
  - Ô thực tế (`actual_slot_id = 9`): Chuyển sang `OCCUPIED`.
  - `parking_sessions.current_slot_id`: Cập nhật sang ô thực tế `9`.
- **Status Codes:**
  - `200 OK`: Ghi nhận thành công.
  - `400 Bad Request`: Dispatch không ở trạng thái `ASSIGNED`, hoặc ô thực tế bảo trì / thuộc bãi khác.
  - `409 Conflict`: Ô thực tế đã bị xe khác chiếm hoặc đang được gán cho xe khác đang di chuyển tới.

---

### 3.5. Hủy lệnh điều phối: `POST /api/v1/parking-dispatches/{id}/cancel`
- **Request Body:**
  ```json
  {
    "reason": "Khách quay đầu không muốn gửi xe nữa"
  }
  ```
- **Status Codes:**
  - `200 OK`: Hủy thành công. Ô đỗ được giải phóng khỏi gán. `session.current_slot_id` được reset về `null`.
  - `400 Bad Request`: Dispatch đã `OCCUPIED`, `WRONG_SLOT` hoặc `CANCELLED` trước đó.

---

## 4. DỮ LIỆU TỌA ĐỘ 3D PHỤC VỤ FRONTEND

Các trường tọa độ không gian 3 chiều trong `ParkingDispatchDto`:
- `assignedPositionX`, `assignedPositionY`, `assignedPositionZ`: Tọa độ ô được chỉ định.
- `actualPositionX`, `actualPositionY`, `actualPositionZ`: Tọa độ ô xe thực tế đỗ (trong trường hợp `OCCUPIED` hoặc `WRONG_SLOT`).
- `assignedFloorNumber`, `assignedFloorName`: Tầng bãi xe được chỉ định.
- `actualFloorNumber`: Tầng của ô thực tế xe đỗ.

---

## 5. CÁC KỊCH BẢN DANH (QA) CẦN TẬP TRUNG KIỂM THỬ

1. **Kiểm tra State Machine:**
   - Đi đủ luồng: Check-In -> Create Dispatch (`ASSIGNED`) -> Confirm Occupied (`OCCUPIED`) -> Check-Out (`COMPLETED`). Xác minh slot chuyển `EMPTY` -> `EMPTY` -> `OCCUPIED` -> `EMPTY`.
   - Luồng Wrong Slot: Check-In -> Create Dispatch (ô 1) -> Report Wrong Slot (ô 2) -> Check-Out. Xác minh ô 1 giữ `EMPTY`, ô 2 thành `OCCUPIED`, sau check-out ô 2 trở về `EMPTY`.
2. **Kiểm tra Phân quyền (RBAC):**
   - Dùng token `USER` gọi `POST /api/v1/parking-dispatches` -> Phải nhận **403 Forbidden**.
   - Dùng token `MANAGER` hoặc `STAFF` gọi các endpoint điều phối -> Phải nhận **403 Forbidden** (chờ task `staff_lot_assignments`).
   - Dùng token `USER` gọi `GET /api/v1/parking-dispatches` -> Chỉ thấy các dispatch có biển số xe thuộc sở hữu của User đó.
3. **Kiểm tra Edge Cases & Concurrency trên PostgreSQL:**
   - *Lưu ý môi trường:* Các automated tests hiện tại đã kiểm tra nghiệp vụ và giả lập conflict qua in-memory database.
   - *Danh cần kiểm thử thực tế trên PostgreSQL (Neon test-parking):*
     - Gửi 2 request đồng thời gán cùng một ô `SlotId` cho 2 session khác nhau -> Phải có đúng 1 request thành công (`201`), request thứ hai phải nhận `409 Conflict` do vi phạm partial unique index `uq_parking_dispatches_assigned_slot`.
     - Gửi 2 request dispatch đồng thời cho cùng 1 session -> Đúng 1 request nhận `201`, request còn lại nhận `409 Conflict` do vi phạm `uq_parking_dispatches_active_session`.
