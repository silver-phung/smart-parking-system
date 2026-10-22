# SMART PARKING SYSTEM – OPEN QUESTIONS & ASSUMPTIONS
## DANH MỤC CÁC ĐIỂM CẦN XÁC NHẬN VÀ QUYẾT ĐỊNH KIẾN TRÚC

- **Dự án:** Smart Parking System (FPT Software OJT)
- **Tài liệu:** Task 02A – Các câu hỏi mở & Quyết định kiến trúc (Bản rà soát và chuẩn hóa)
- **Mục tiêu:** Phân định minh bạch giữa yêu cầu đã xác nhận và các đề xuất kỹ thuật; chuyển toàn bộ các giả định chưa được phê duyệt sang danh mục câu hỏi mở để Owner Phụng và nhóm thống nhất trước Task 02B.

---

## 1. BÁO CÁO KHOẢNG TRỐNG TÀI LIỆU TRONG WORKSPACE

> [!IMPORTANT]
> **Hiện trạng tài liệu nguồn tại `E:\smartparking`:**
> - Trong workspace hiện tại **chưa có file đặc tả yêu cầu phần mềm chính thức (SRS)** hoặc tài liệu Backlog chi tiết của Sprint 3.
> - Tuân thủ nghiêm ngặt chỉ đạo của Owner, Antigravity **không sử dụng thiết kế Sprint cũ làm nguồn yêu cầu chính thức**.
> - Toàn bộ phân tích nghiệp vụ tại Task 02A được xây dựng dựa trên:
>   1. Mục tiêu và phạm vi đề bài FPT Software OJT (Bãi xe thông minh kết hợp mô phỏng 3D).
>   2. Bản mô tả nghiệp vụ và phân công vai trò do Owner Phụng trực tiếp giao trong yêu cầu Task 02A.
>   3. Các quy chuẩn vận hành bãi đỗ xe thông minh thực tế (Commercial Parking Operations).

### Phân định minh bạch nguồn gốc thông tin:
- **`CONFIRMED` (Có căn cứ tài liệu / Đề bài OJT & Quyết định chính thức):**
  - Luồng xe vào/ra qua barrier tự động kết hợp nhận diện biển số (OCR).
  - Phân cấp bãi xe: Bãi xe -> Tầng -> Vị trí đỗ ô tô (Smart Parking chỉ phục vụ ô tô tiêu chuẩn, không xe máy, không EV/trạm sạc, loại bỏ bảng `vehicle_types`).
  - Vòng đời phiên gửi xe từ Check-in, Điều phối, Đỗ xe, Thanh toán đến Check-out.
  - Sự tách biệt giữa thanh toán thành công và rời bãi (Grace Period).
  - Phân quyền người dùng (Admin, Operator, Customer).
  - Lưu vết kiểm toán các thao tác nhạy cảm (`audit_logs`).
- **`PROPOSED` (Đề xuất giải pháp thiết kế từ Antigravity):**
  - Tách bảng `parking_dispatches` độc lập để quản lý trạng thái phân công, tuyệt đối không gộp chung cờ `is_reserved` vào bảng `parking_slots`.
  - Phân tách 4 bảng vào nhóm `CONDITIONAL` để nhóm cân nhắc mức độ cần thiết trong Sprint 3.
  - Phân tách 2 bảng `parking_reservations` và `subscriptions` sang nhóm `FUTURE`.
- **`NEEDS CONFIRMATION` (Các điểm cần thống nhất trước khi chốt ERD):** Toàn bộ danh mục câu hỏi dưới đây.

---

## 2. CÁC GIẢ ĐỊNH ĐÃ ĐƯỢC CHUYỂN THÀNH CÂU HỎI MỞ

### Câu hỏi 1: Lựa chọn Chiến lược Khóa chính (Primary Key Strategy)
- **Bối cảnh:** Cần quyết định loại khóa chính thống nhất cho các thực thể trước khi thiết kế ERD ở Task 02B.
- **Hai phương án cân nhắc:**
  - **Phương án A (BIGINT GENERATED ALWAYS AS IDENTITY):**
    - *Ưu điểm:* Hiệu năng cao nhất trên PostgreSQL, dung lượng index B-Tree nhỏ gọn, giải thích trước Hội đồng Mentor FPT rất trong sáng và dễ hiểu; khi test thử nghiệm thủ công qua Swagger/Postman gõ ID số nguyên (`1, 2, 3`) rất tiện lợi.
    - *Bảo mật đối ngoại:* Đối với các bảng cần che giấu số lượng bản ghi (như `parking_sessions` hay `payments`), có thể bổ sung thêm một mã tra cứu công khai `public_code` dạng chuỗi ngẫu nhiên.
  - **Phương án B (UUID v7 hoặc UUID v4):**
    - *Ưu điểm:* Mã định danh ngẫu nhiên toàn cục, sinh ID phân tán ở tầng ứng dụng không phụ thuộc CSDL.
    - *Nhược điểm:* Dung lượng khóa lớn hơn (16 bytes vs 8 bytes), khó nhớ khi test thủ công và giải trình logic.
- 👉 **Cần Owner Phụng quyết định:** Thống nhất chọn **Phương án A (BIGINT IDENTITY)** hay **Phương án B (UUID)** cho các bảng dữ liệu?

---

### Câu hỏi 2: Quyết định số phận 4 bảng CONDITIONAL cho Sprint 3
Nhóm cần xem xét có triển khai 4 bảng này trong Sprint 3 hay không:
1. **`refresh_tokens`:**
   - *Câu hỏi:* Cơ chế xác thực tài khoản của nhóm trong Sprint 3 có bắt buộc dùng JWT Access Token + Refresh Token (kèm thu hồi token qua CSDL) hay chỉ cần JWT Access Token đơn giản kèm thời gian sống (TTL)?
2. **`slot_sensor_logs`:**
   - *Câu hỏi (với Thức - IoT):* Hệ thống có cần lưu nhật ký thô của mọi lần cảm biến đổi trạng thái vào một bảng riêng trong CSDL không, hay chỉ cần IoT Service gọi API cập nhật trạng thái trực tiếp vào `parking_slots` và `parking_dispatches`? (Lưu toàn bộ log có thể làm nặng CSDL).
3. **`alerts`:**
   - *Câu hỏi (với Song - 3D/Frontend):* Cảnh báo vận hành (đỗ sai chỗ, quá giờ) có cần lưu bảng CSDL để quản lý trạng thái xử lý (`is_resolved`) hay chỉ cần bắn tín hiệu trực tiếp qua WebSocket / SignalR sang giao diện 3D?
4. **`system_configs`:**
   - *Câu hỏi:* Các tham số vận hành (thời gian ân hạn, ngưỡng OCR) có cần lưu động trong CSDL để Admin chỉnh sửa trên Web không, hay cấu hình tĩnh trong file `appsettings.json` của ASP.NET Core để đơn giản hóa Sprint 3?

---

### Câu hỏi 3: Thống nhất các Tham số Nghiệp vụ Thời gian (Business Parameters)
Cần sự thống nhất giữa Phụng (Backend), Nam Anh (Payment) và Song (3D/Frontend):
1. **Thời gian ân hạn sau thanh toán (Grace Period):**
   - Đề xuất: Cho phép một khoảng thời gian hợp lý (ví dụ: 15 phút) để khách lấy xe và ra cổng. Nhóm chốt con số bao nhiêu phút là phù hợp với kịch bản mô phỏng?
2. **Miễn phí thả khách nhanh (Free Drop-off):**
   - Đề xuất: Xe vào rồi quay đầu ra ngay dưới một khoảng thời gian ngắn (ví dụ: 10 phút) có được miễn phí hoàn toàn không?
3. **Thời gian hết hạn điều phối (Dispatch Timeout):**
   - Sau bao nhiêu phút kể từ lúc xe được chỉ định ô đỗ mà không thấy cảm biến báo có xe vào thì hệ thống sẽ hủy phân công để giải phóng ô đỗ?

---

### Câu hỏi 4: Ngưỡng tin cậy OCR và Quy trình Xử lý Ngoại lệ
Cần xác nhận với **Đức (AI / Computer Vision)**:
- Mô hình AI nhận diện biển số trả về thang điểm tin cậy (Confidence Score) như thế nào?
- Ngưỡng tin cậy bao nhiêu (ví dụ: 0.80 hay 0.85) thì hệ thống được phép tự động mở barrier?
- Khi biển số mờ hoặc đọc sai, giao diện của Song sẽ hiển thị ảnh snapshot để Operator sửa biển số thủ công như thế nào?

---

### Câu hỏi 5: Danh mục Cổng Thanh toán Điện tử
Cần xác nhận với **Nam Anh (Payment & Billing)**:
- Trong phạm vi Sprint 3, hệ thống sẽ tích hợp cổng thanh toán nào? (MoMo, VNPay, ZaloPay, hay cổng thanh toán giả lập / Sandbox để demo OJT)?
- Quy trình thanh toán tiền mặt tại quầy thu phí có cần in hóa đơn hoặc mã giao dịch riêng không?

---

### Câu hỏi 6: Phương thức Phối hợp Dữ liệu với Mô phỏng 3D
Cần xác nhận với **Song (Frontend & 3D Lead)**:
- Dữ liệu vị trí ô đỗ: Tọa độ 3D (Position X, Y, Z và Rotation Y) có cần lưu trực tiếp vào từng dòng của bảng `parking_slots` trong CSDL không, hay mô hình 3D được định nghĩa sẵn trong file 3D (GLTF/JSON) và CSDL chỉ cần lưu mã định danh ô đỗ (`slot_code`) để map trạng thái màu sắc?
- Khi xe đỗ sai vị trí so với chỉ định: Mô phỏng 3D sẽ thể hiện như thế nào (đổi màu ô đỗ thành đỏ nhấp nháy, hiển thị popup cảnh báo hay tự động vẽ đường đi mới)?

---

### Câu hỏi 7: Nơi Lưu trữ Hình ảnh Snapshot của Camera Cổng
Cần xác nhận với **Đức (AI)** và **Trí (DevOps)**:
- Ảnh chụp biển số xe lúc vào và lúc ra sẽ được lưu trữ ở đâu? (Dịch vụ Object Storage như AWS S3, Cloudinary, MinIO cục bộ, hay lưu thư mục tĩnh trên Web Server)?
- Database thống nhất **chỉ lưu đường dẫn URL** (`VARCHAR(500)`), tuyệt đối không lưu dữ liệu nhị phân (Binary BLOB) trong PostgreSQL để đảm bảo dung lượng gọn nhẹ cho Neon.

---

## 3. CÁC QUYẾT ĐỊNH KIẾN TRÚC CẦN CURSOR KIỂM TOÁN (AUDIT CHECKLIST)

Khi chuyển sang các task tiếp theo, Antigravity ghi chú các bài toán kỹ thuật phức tạp sau cần sự tham gia và kiểm toán của Agent Cursor:

1. **Xử lý tranh chấp dữ liệu khi phân công ô đỗ (Concurrency Safety on Slot Allocation):**
   - Khi có nhiều xe cùng check-in đồng thời, cần cơ chế Transaction và Khóa phù hợp (`SELECT ... FOR UPDATE` hoặc Optimistic Locking) để đảm bảo một ô đỗ không bao giờ bị gán trùng lặp cho hai xe.
2. **Cơ chế Chống trùng lặp (Idempotency) cho Giao dịch và Webhook Thanh toán:**
   - Đảm bảo mỗi giao dịch thanh toán và webhook từ cổng thanh toán có mã định danh duy nhất (`idempotency_key`), ngăn chặn việc trừ tiền hoặc ghi nhận thanh toán lặp lại khi mạng bị retry.
3. **Chiến lược Đánh Chỉ mục (Indexing Strategy) trên PostgreSQL:**
   - Thiết lập chỉ mục tối ưu cho các cột thường xuyên tìm kiếm và đối soát: `plate_number`, `session_id`, `check_in_time`, `physical_status`.
