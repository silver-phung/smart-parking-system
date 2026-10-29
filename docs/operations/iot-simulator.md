# 🚗 Tài Liệu Module IoT Hardware Simulator (Sprint 2)

**Trạng thái:** Hướng dẫn công cụ mô phỏng; không thay nguồn thiết bị/DB vận hành.  
**Ưu tiên:** Luồng và trạng thái hiện hành nằm tại [event and IoT architecture](../architecture/event-and-iot.md) và [main flows](../product/main-flows.md).

**Người phụ trách:** Thức (IoT Ingestion & Streaming Lead)  
**Mã yêu cầu (SRS):** `US-FR-IOT-001`, `FR-IOT-001`, `FR-IOT-007`  
**Nhánh:** `feat/Thuc/iot-simulator`

---

## 1. Mục Tiêu & Bối Cảnh
Trong dự án Bãi đỗ xe thông minh, hệ thống cần nhận diện và đồng bộ trạng thái xe vào/ra theo thời gian thực. Do nhóm không có thiết bị cảm biến vật lý thực tế, module **IoT Hardware Simulator** được xây dựng nhằm:
1. Giả lập tín hiệu cảm biến từ các bãi xe gửi về Backend qua giao thức HTTP/REST (chuẩn bị sẵn sàng nâng cấp lên MQTT Broker ở các Sprint tiếp theo).
2. Tự động cập nhật số chỗ trống (`availableSlots`) của bãi xe tương ứng khi có sự kiện xe đỗ hoặc rời bãi.
3. Cung cấp cả **Giao diện Web trực quan (Web Dashboard)** và **Công cụ dòng lệnh (CLI)** để phục vụ đội ngũ QA (Danh), Frontend (Song) và buổi Demo bảo vệ Sprint với giảng viên.

---

## 2. Kiến Trúc & API Endpoints

### 2.1. Ingestion Endpoint (Nhận sự kiện cảm biến)
* **URL:** `POST /api/iot/events`
* **Request Body:**
```json
{
  "deviceId": "SENSOR-LOT1-SLOT05",
  "parkingLotId": 1,
  "slotNumber": "SLOT-05",
  "eventType": "CarParked",
  "timestamp": "2026-10-16T10:00:00Z"
}
```
* **Các loại sự kiện (`eventType`):**
  * `CarParked` (hoặc `CheckIn`): Xe vào bãi ➔ Giảm `availableSlots` đi 1 (nếu bãi còn chỗ).
  * `CarDeparted` (hoặc `CheckOut`): Xe rời bãi ➔ Tăng `availableSlots` lên 1.
  * `Fault`: Cảm biến báo lỗi ➔ Ghi nhận telemetry cảnh báo.

* **Response Success (200 OK):**
```json
{
  "success": true,
  "message": "Xe vào bãi 'Bãi xe FPT Software Quận 9'. Số chỗ còn lại: 23.",
  "deviceId": "SENSOR-LOT1-SLOT05",
  "parkingLotId": 1,
  "parkingLotName": "Bãi xe FPT Software Quận 9",
  "availableSlots": 23,
  "eventType": "CarParked",
  "processedAt": "2026-10-16T10:00:00.123Z"
}
```

### 2.2. Telemetry History Endpoint
* **URL:** `GET /api/iot/telemetry`
* Trả về lịch sử 100 sự kiện cảm biến gần nhất phục vụ giám sát và audit log.

### 2.3. Reset Endpoint
* **URL:** `POST /api/iot/reset`
* Khôi phục số chỗ trống của 3 bãi xe về trạng thái mẫu ban đầu (24, 0, 8) và xoá log tạm.

---

## 3. Hướng Dẫn Sử Dụng Công Cụ Giả Lập

### Cách 1: Sử dụng Web Simulator Dashboard (Khuyên dùng khi Demo)
1. Mở file `frontend/simulator/index.html` trực tiếp bằng trình duyệt hoặc qua Live Server (VS Code).
2. Nhập URL của Backend API (mặc định: `http://localhost:5242` hoặc `http://localhost:5000`).
3. Bấm **"Kiểm tra kết nối"** ➔ Huy hiệu chuyển sang màu xanh **Online (Connected)**.
4. **Thực hiện thao tác:**
   * Bấm `🚗 Xe Vào (Check-in)` tại bãi bất kỳ ➔ Chỗ trống giảm, terminal hiển thị payload gửi nhận.
   * Bấm `🚙 Xe Ra (Check-out)` ➔ Chỗ trống tăng.
   * Bấm `⚡ Bật Auto Traffic` ➔ Hệ thống tự động sinh luồng xe vào/ra mỗi 3 giây ngẫu nhiên!
   * Bấm `↺ Reset về mặc định` ➔ Khôi phục lại 24, 0, 8 bất cứ lúc nào.

---

### Cách 2: Sử dụng Console CLI Tool (Dành cho Developer / QA)
Chạy script Python trong thư mục `tools/`:
```bash
python3 tools/simulator.py [URL_API_TÙY_CHỌN]
# Ví dụ:
python3 tools/simulator.py http://localhost:5242
```
* Menu tương tác bằng phím số [1-8] giúp test nhanh từng bãi hoặc chạy stress-test tự động.

---

## 4. Kịch Bản Kiểm Thử (Dành cho Danh - QA)

| STT | Tên ca kiểm thử | Thao tác | Kết quả mong đợi |
|---|---|---|---|
| TC-IOT-01 | Xe vào bãi còn chỗ | Bấm Xe vào tại Bãi FPT (24 chỗ) | Số chỗ giảm còn 23, HTTP 200 |
| TC-IOT-02 | Xe rời bãi | Bấm Xe ra tại Bãi FPT (23 chỗ) | Số chỗ tăng lại 24, HTTP 200 |
| TC-IOT-03 | Xe vào bãi ĐÃ HẾT CHỖ | Bấm Xe vào tại Bãi Vincom (0 chỗ) | Hệ thống từ chối, báo bãi đã đầy, HTTP 400 |
| TC-IOT-04 | Đồng bộ sang Web Client | Gửi sự kiện từ Simulator rồi F5 Web Song | Số chỗ hiển thị trên Web Song khớp 100% với Simulator |
| TC-IOT-05 | Chế độ Auto Traffic | Bật Auto Traffic 30 giây | Luồng sự kiện liên tục, không bị treo |
| TC-IOT-06 | Reset dữ liệu | Bấm Reset | Trạng thái trở về đúng 24, 0, 8 |
