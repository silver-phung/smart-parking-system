# Hướng dẫn Tích hợp & Vận hành Sprint 3 (Days 6–9 - Minh Thức)

## 1. Tổng quan kiến trúc & Luồng vận hành thực tế tại Việt Nam
Hệ thống kết hợp mô hình linh hoạt (semi-automated free parking model), phù hợp với chi phí đầu tư tại các bãi xe Việt Nam:
1. **Xe vào cổng**:
   - Camera chụp ảnh xe → Gửi sang API Gemini 2.5 Pro (`POST /api/gemini/analyze?imageUrl=...`) để AI phân tích nhận diện: Biển số, Hãng/Dòng xe, Màu xe, Loại nhiên liệu (xăng/điện).
   - Staff tại cổng đối chiếu/xác nhận thông tin → Gửi sự kiện `VehicleEntered` qua `POST /api/events` → Tạo phiên `pending_slot`.
2. **Xe vào bãi tự do tìm chỗ**:
   - Cảm biến siêu âm tại từng ô đỗ phát hiện vật thể → Gửi sự kiện `SensorTriggered` (`SlotCode` theo format chuẩn như `A1-012`, `B2-005`). Hệ thống ghi nhận log, chưa gán cứng xe để tránh nhầm lẫn.
3. **Staff khu vực xác nhận gán chỗ**:
   - Staff khu vực di chuyển kiểm tra và dùng mobile app gán xe vào ô đỗ (`SlotParked`).
   - Phiên chuyển trạng thái sang `parked`.
   - `IVehicleCompatibilityChecker` tự động kiểm tra tương thích nhiên liệu (ví dụ xe xăng đỗ nhầm ô sạc điện) và bắn cảnh báo `wrong_vehicle_type`.
4. **Xe ra cổng**:
   - Staff cổng đối chiếu ảnh lúc vào (`EntryImageUrl`) và lúc ra (`ExitImageUrl`).
   - Gửi sự kiện `VehicleExited` đóng phiên đỗ.
   - Đồng bộ sang hệ thống tính cước & giải phóng ô đỗ của phân hệ Quản lý phiên đỗ.

---

## 2. Các thành phần đã triển khai trong Days 6–9
- **Day 6: Gemini 2.5 Pro AI Service & Controller**
  - `IGeminiVehicleAnalyzer`: Interface trích xuất thông tin ảnh xe.
  - `GeminiVehicleAnalyzerService`: Triển khai gọi Google Generative Language API (`gemini-2.5-pro:generateContent`) với cơ chế retry tự động.
  - `NoOpGeminiVehicleAnalyzer`: Fallback an toàn khi chưa cấu hình API key trong môi trường local/test.
  - `GeminiController`: Endpoint `POST /api/gemini/analyze?imageUrl=...`.
- **Day 7: EF Core Repository Adapters (Sẵn sàng cho Neon PostgreSQL)**
  - `EfParkingEventLogRepository` & `EfParkingSessionRepository`: Thiết kế chuẩn EF Core để lưu trữ vào database dùng chung sau khi nhánh `feature/phung-database` được merge.
  - Sử dụng compile flags an toàn tránh xung đột entity trước khi hợp nhất.
- **Day 8: Parking Event Bridge (Kết nối hệ thống Ingestion & Dispatch/Session)**
  - `IParkingEventBridge`: Cầu nối trung gian giữa luồng Ingestion và hệ thống quản trị phiên đỗ.
  - `NoOpParkingEventBridge`: Mock bridge ghi log truy vết trong quá trình phát triển độc lập.
  - `LiveParkingEventBridge`: Triển khai gọi trực tiếp `IParkingSessionService` của hệ thống chính.
- **Day 9: Test tự động & Hoàn thiện tài liệu**
  - Bổ sung `GeminiControllerTests`: Đạt 23/23 tests pass 100%.
  - Cập nhật kịch bản kiểm thử `Events.http`.

---

## 3. Cấu hình Gemini API Key
Để chạy phân tích AI với Gemini 2.5 Pro thật, thêm key vào User Secrets:
```bash
dotnet user-secrets set "Gemini:ApiKey" "AIzaSy..." --project backend/src/SmartParking.Api
```
Nếu chưa set key, hệ thống tự động chạy ở chế độ `NoOpGeminiVehicleAnalyzer` mà không gây crash ứng dụng.
