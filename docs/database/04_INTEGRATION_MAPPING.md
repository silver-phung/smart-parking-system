# SMART PARKING SYSTEM – INTEGRATION MAPPING
## MA TRẬN TÍCH HỢP DỮ LIỆU & GIAO DIỆN API GIỮA CÁC MODULE

- **Dự án:** Smart Parking System (FPT Software OJT)
- **Tài liệu:** Task 02A – Phân công tích hợp dữ liệu (Bản rà soát và chuẩn hóa)
- **Mục tiêu:** Xác định rõ ràng quyền thao tác dữ liệu (CRUD Matrix) của từng module/thành viên và chuẩn hóa các điểm tiếp xúc API giữa Backend và các dịch vụ ngoại vi.

---

## 1. MA TRẬN TRUY CẬP VÀ THAO TÁC DỮ LIỆU (CRUD MATRIX)

Ký hiệu:
- **C (Create):** Quyền tạo mới bản ghi.
- **R (Read):** Quyền đọc/truy vấn dữ liệu.
- **U (Update):** Quyền chỉnh sửa/cập nhật dữ liệu.
- **D (Delete / Deactivate):** Quyền vô hiệu hóa hoặc xóa bản ghi.
- **- :** Không có quyền truy cập trực tiếp.

| Tên bảng dữ liệu | Phân loại | Phụng (Core & Auth) | Nam Anh (Payment) | Thức (IoT & Gate) | Đức (AI & OCR) | Song (Frontend & 3D) | Danh (QA & Test) |
| :--- | :---: | :---: | :---: | :---: | :---: | :---: | :---: |
| `users` | **CORE** | **C, R, U, D** | R | - | - | R, U (Profile) | C, R (Mock) |
| `roles` | **CORE** | **C, R, U** | R | - | - | R (Perms) | R |
| `user_roles` | **CORE** | **C, R, U, D** | R | - | - | R (Perms) | R |
| `parking_lots` | **CORE** | **C, R, U, D** | R | R | - | R (Render 3D) | R |
| `parking_floors` | **CORE** | **C, R, U, D** | R | R | - | R (Render 3D) | R |
| `parking_slots` | **CORE** | **C, R, U, D** | R | **U (Sensors)** | - | **R (3D Color)** | R |
| `vehicles` | **CORE** | **C, R, U, D** | R | - | R | C, R, U | C, R |
| `parking_sessions` | **CORE** | **C, R, U** *(Core)*| **R, U (Paid)** | R | R | R (Live View) | C, R, U |
| `parking_dispatches`| **CORE** | **C, R, U** | - | **U (Confirm)** | - | **R, U (Assign)** | C, R, U |
| `pricing_policies` | **CORE** | R, D | **C, R, U** | - | - | R (Show Rates) | R |
| `payments` | **CORE** | R | **C, R, U** | - | - | R (Receipt) | C, R |
| `gate_events` | **CORE** | R | - | **C, R, U** | **C, R (OCR Log)**| R (Gate Monitor)| C, R |
| `audit_logs` | **CORE** | **C, R** | C (Pay Audit) | C (Gate Audit)| - | R (Admin View) | R |
| `refresh_tokens` | *CONDITIONAL*| **C, R, U, D** | - | - | - | C, R (Auth) | R |
| `slot_sensor_logs`| *CONDITIONAL*| R | - | **C, R** | - | R (Telemetry) | C, R |
| `alerts` | *CONDITIONAL*| **C, R, U** | C (Billing) | **C, R (Sensor)** | - | **R, U (Resolve)** | C, R |
| `system_configs` | *CONDITIONAL*| **C, R, U** | R | R | R | R | R |
| `parking_reservations`| *FUTURE* | C, R, U | - | - | - | C, R, U (App) | C, R |
| `subscriptions` | *FUTURE* | C, R, U | C, R, U | R | - | R (Sub Status) | C, R |

---

## 2. CÁC TÌNH HUỐNG TÍCH HỢP VÀ GIAO DIỆN API CHỦ YẾU

### 2.1. Tích hợp AI / OCR (Đức -> Backend)
Dịch vụ nhận diện biển số xe của Đức xử lý hình ảnh chụp từ camera cổng và gọi API Backend:

1. **`POST /api/v1/gates/entry/ocr`**
   - **Mục đích:** Gửi kết quả nhận diện biển số xe ô tô tại cổng vào kèm URL hình ảnh snapshot và điểm tin cậy.
   - **Dữ liệu tiếp nhận:** Mã cổng, URL ảnh, biển số nhận diện được, điểm tin cậy.
   - **Tác động Database:**
     - Ghi nhận vào bảng `gate_events`.
     - Nếu đạt điều kiện tin cậy và bãi còn chỗ: Tạo bản ghi `parking_sessions` ở trạng thái `ACTIVE` và đưa vào hàng đợi điều phối `parking_dispatches` (`PENDING`).
   - **Phản hồi trả về:** Lệnh điều khiển mở barrier hoặc yêu cầu nhân viên xác nhận thủ công.

2. **`POST /api/v1/gates/exit/ocr`**
   - **Mục đích:** Gửi kết quả nhận diện biển số tại cổng ra để kiểm tra điều kiện xuất bãi.
   - **Tác động Database:** Tra cứu phiên gửi xe tương ứng, kiểm tra trạng thái thanh toán và điều kiện thời gian ân hạn.

---

### 2.2. Tích hợp IoT & Thiết bị Phần cứng (Thức <-> Backend)
Kết nối bộ điều khiển barrier cổng và cảm biến ô đỗ:

1. **`POST /api/v1/iot/slots/telemetry`**
   - **Mục đích:** Báo cáo sự thay đổi trạng thái có xe / không có xe tại một ô đỗ cụ thể.
   - **Dữ liệu tiếp nhận:** Mã ô đỗ (`slot_code`), trạng thái cảm biến (`OCCUPIED` hoặc `EMPTY`), thời điểm.
   - **Tác động Database:**
     - Cập nhật trạng thái vật lý trong `parking_slots.physical_status`.
     - Đối soát với lệnh điều phối trong `parking_dispatches`: Xác nhận xe đã vào đúng ô hoặc phát hiện xe đỗ sai vị trí so với chỉ định.
     - *(Nếu bật bảng CONDITIONAL `slot_sensor_logs`)*: Ghi nhận thêm bản ghi telemetry thô.

2. **`POST /api/v1/iot/gates/{gateCode}/command`**
   - **Mục đích:** Backend gửi lệnh đóng/mở barrier hoặc phát tín hiệu còi/đèn cảnh báo tới bộ điều khiển phần cứng của Thức.

---

### 2.3. Tích hợp Biểu phí & Thanh toán (Nam Anh <-> Backend)
Module tính giá và xử lý giao dịch tài chính:

1. **`GET /api/v1/pricing/calculate`**
   - **Mục đích:** Tra cứu số tiền gửi xe tính toán được theo thời lượng thực tế và biểu phí hiện hành.
   - **Tác động Database:** Chỉ đọc (`pricing_policies`, `parking_sessions`).

2. **`POST /api/v1/payments/create-intent`**
   - **Mục đích:** Khởi tạo giao dịch thanh toán (sinh thông tin thanh toán điện tử hoặc phiếu thu tiền mặt).
   - **Tác động Database:** Tạo bản ghi giao dịch trong `payments` với trạng thái `PENDING`.

3. **`POST /api/v1/payments/webhook` (hoặc Xác nhận Thu tiền mặt)**
   - **Mục đích:** Nhận tín hiệu xác nhận thanh toán thành công.
   - **Tác động Database:**
     - Kiểm tra tính chống trùng lặp (Idempotency).
     - Cập nhật `payments.status = 'SUCCESS'`.
     - Cập nhật `parking_sessions.status = 'PAID'` và gán `paid_at = NOW()`.
     - Ghi nhận `audit_logs` đối soát tài chính.

---

### 2.4. Tích hợp Web Dashboard & Mô phỏng 3D (Song <-> Backend)
Giao diện điều hành thời gian thực và mô hình Digital Twin 3D:

1. **`GET /api/v1/simulation/layout`**
   - **Mục đích:** Tải toàn bộ cấu trúc bãi đỗ: danh sách tầng (`parking_floors`), danh sách ô đỗ (`parking_slots`) kèm thông số vị trí để render trên 3D.
   - **Tác động Database:** Chỉ đọc dữ liệu cấu trúc bãi xe.

2. **`GET /api/v1/dispatch/queue`**
   - **Mục đích:** Lấy danh sách các xe vừa qua cổng vào đang chờ được phân công vị trí (`parking_dispatches.status = 'PENDING'`).

3. **`POST /api/v1/dispatch/assign`**
   - **Mục đích:** Thực hiện phân công một ô đỗ cụ thể cho xe (qua thuật toán hoặc nhân viên bấm chọn trên giao diện 3D).
   - **Tác động Database:** Cập nhật `parking_dispatches.assigned_slot_id` và chuyển trạng thái sang `ASSIGNED`.

4. **Kênh sự kiện thời gian thực (WebSocket / SSE):**
   - **Mục đích:** Đẩy các sự kiện tức thì (Xe vào cổng, Cảm biến ô đỗ chuyển màu, Cảnh báo đỗ sai) sang giao diện Web và mô hình 3D của Song.
