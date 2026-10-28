# SMART PARKING SYSTEM – DATABASE ARCHITECTURE OVERVIEW
## TỔNG QUAN KIẾN TRÚC DỮ LIỆU & NGHIỆP VỤ BÃI ĐỖ XE THÔNG MINH

- **Dự án:** Smart Parking System (Mô phỏng 3D kết hợp Quản trị Vận hành Thực tế)
- **Chương trình:** FPT Software OJT
- **Chịu trách nhiệm kiến trúc CSDL:** Phụng (Backend / Database Lead)
- **Hệ quản trị CSDL mục tiêu:** PostgreSQL (Hosted trên Neon Serverless Postgres)
- **Tài liệu:** Task 02A & 02B – Phân tích nghiệp vụ dữ liệu (Bản chuẩn hóa toàn hệ thống)
- **Trạng thái:** Thiết kế kiến trúc dữ liệu (Chưa tạo Entity / DbContext / Migration / SQL)

---

## 1. MỤC TIÊU VÀ ĐỊNH HƯỚNG THIẾT KẾ CƠ SỞ DỮ LIỆU

### 1.1. Mục tiêu cốt lõi
Hệ thống Smart Parking hướng tới giải quyết bài toán quản lý bãi đỗ xe thông minh theo mô hình thực tế, đồng thời tích hợp giao diện mô phỏng không gian 3D trong khuôn khổ chương trình FPT Software OJT:
1. **Nghiệp vụ thực tế chặt chẽ:** Đảm bảo toàn vẹn dữ liệu cho các luồng nghiệp vụ: Nhận diện biển số (OCR) -> Xe vào cổng (Check-in) -> Điều phối vị trí (Dispatch) -> Đỗ xe thực tế qua cảm biến -> Tính phí linh hoạt -> Thanh toán điện tử / tiền mặt -> Kiểm tra điều kiện xuất bãi -> Xe rời cổng (Check-out).
2. **Phạm vi phương tiện xác định (CONFIRMED):** Hệ thống chỉ phục vụ **ô tô thông thường**. Không có xe máy, không có ô tô điện hoặc trạm sạc EV, không phân loại nhiên liệu xăng/dầu.
3. **Hỗ trợ mô phỏng 3D trực quan:** Cung cấp dữ liệu trạng thái ô đỗ, phân luồng xe để giao diện 3D (do Song phụ trách) kết xuất trực quan và phản ánh hiện trạng bãi xe theo thời gian thực.
4. **Đơn giản, minh bạch, dễ giải thích:** Thiết kế bảng theo nguyên tắc Đơn trách nhiệm (Single Responsibility), không lạm dụng abstraction phức tạp, không tạo bảng dư thừa, giúp Owner Phụng dễ dàng bảo vệ phương án kiến trúc trước Hội đồng Mentor FPT Software.

### 1.2. Nền tảng công nghệ lưu trữ
- **PostgreSQL trên Neon Cloud:** Tận dụng tính toàn vẹn giao dịch (ACID), tính năng Serverless co giãn linh hoạt, hỗ trợ lưu trữ JSONB khi cần lưu dữ liệu log ngoại vi linh hoạt mà không làm biến dạng schema bảng chính.
- **Chuẩn hóa dữ liệu (3NF):** Chuẩn hóa dữ liệu ở mức độ hợp lý để tránh dị thường dữ liệu (Update/Insert/Delete Anomaly), đồng thời lưu vết snapshot giá trị tại thời điểm giao dịch phát sinh (số tiền đã thu, biểu phí áp dụng) nhằm bảo toàn tính lịch sử bất biến.

---

## 2. NGUYÊN TẮC NGHIỆP VỤ BẮT BUỘC TRONG THIẾT KẾ DỮ LIỆU

Các nguyên tắc sau đây là kim chỉ nam xuyên suốt toàn bộ thiết kế cơ sở dữ liệu:

1. **Check-in không đồng nghĩa với đã đỗ xe:**
   - Khi xe qua cổng vào, hệ thống chỉ tạo phiên gửi xe ở trạng thái `ACTIVE` và đưa xe vào trạng thái chờ điều phối (`WAITING_DISPATCH`).
   - Ô đỗ vật lý trong bãi vẫn hoàn toàn trống (`EMPTY`) cho tới khi cảm biến thực tế xác nhận xe đã tiến vào đỗ.
2. **Tách biệt Trạng thái Vật lý của Ô đỗ và Trạng thái Phân công:**
   - **Trạng thái vật lý (`parking_slots.physical_status`):** Thống nhất 4 trạng thái phản ánh hiện trạng từ cảm biến phần cứng:
     - `EMPTY`: Ô đỗ đang trống, không có xe.
     - `OCCUPIED`: Đang có xe đỗ trong ô.
     - `MAINTENANCE`: Ô đỗ đang bảo trì thiết bị/cảm biến.
     - `UNAVAILABLE`: Ô đỗ tạm thời không khả dụng.
   - **Trạng thái phân công:** Được quản lý độc lập tại bảng điều phối (`parking_dispatches`), gồm: `PENDING` -> `ASSIGNED` -> `OCCUPIED` / `WRONG_SLOT` / `CANCELLED`.
   - **Không tự ý nhét cờ `is_reserved` vào bảng slot nếu chưa thống nhất:** Việc kiểm tra một slot có đang được phân công hay không sẽ được xác định qua việc truy vấn các lệnh điều phối đang hoạt động (`ASSIGNED`) trong bảng `parking_dispatches`.
3. **Chống phân công trùng lặp (Anti-Collision Assignment):**
   - Một vị trí đỗ không thể được phân công đồng thời cho hai phiên gửi xe khác nhau trong cùng một khoảng thời gian.
4. **Thanh toán thành công không đồng nghĩa với xe đã rời bãi:**
   - Khách thanh toán thành công chỉ chuyển trạng thái phiên sang `PAID` và ghi nhận mốc thời gian thanh toán (`paid_at`).
   - Chiếc xe vẫn đang nằm tại ô đỗ trong bãi xe; ô đỗ vẫn ở trạng thái vật lý `OCCUPIED`.
5. **Check-out bắt buộc xác minh xe thực sự rời cổng:**
   - Phiên gửi xe chỉ chuyển sang `COMPLETED` và ô đỗ chỉ được giải phóng về `EMPTY` khi cảm biến tại cổng ra xác nhận xe đã lăn bánh qua barrier ra và barrier đã đóng lại an toàn.
6. **Bảo vệ dữ liệu cá nhân & Truy vết thay đổi:**
   - Không lưu thông tin cá nhân dư thừa (chỉ lưu định danh tối thiểu: email, họ tên, số điện thoại để liên hệ sự cố).
   - Mọi thao tác quản trị nhạy cảm (mở barrier thủ công, sửa biểu giá, cưỡng bức hoàn tất phiên) bắt buộc phải được ghi nhận vào nhật ký kiểm toán (`audit_logs`).

---

## 3. BẢY NHÓM NGHIỆP VỤ CHÍNH (BUSINESS DOMAINS)

Toàn bộ nghiệp vụ Smart Parking được phân thành 7 nhóm độc lập, liên kết chặt chẽ qua khóa ngoại:

```
+-----------------------------------------------------------------------------------+
|                           SMART PARKING DATA ARCHITECTURE                         |
+-----------------------------------------------------------------------------------+
  [A. Identity & Access]        [B. Parking Structure & 3D]       [C. Vehicles & Sessions]
   - users, roles                - lots, floors, slots             - vehicles
   - user_tokens, refresh_tokens                                   - parking_sessions (CORE)
            |                                 |                             |
            +---------------------------------+-----------------------------+
                                              |
                          [D. Parking Dispatch & Allocation]
                           - parking_dispatches (Logical State)
                                              |
            +---------------------------------+-----------------------------+
            |                                 |                             |
  [E. Pricing & Payments]        [F. Hardware Events & IoT]        [G. Audit & Operations]
   - pricing_policies, payments   - gate_events, slot_sensor_logs   - audit_logs, alerts
+-----------------------------------------------------------------------------------+
```

- **Nhóm A: Accounts, Identity & Authorization:** Quản lý tài khoản, xác thực, phân quyền 4 vai trò (Admin, Manager, Staff, User).
- **Nhóm B: Parking Structure & Space Representation:** Mô hình hóa địa điểm bãi xe, các tầng/khu vực và các ô đỗ ô tô cụ thể (không giả định kích thước toàn bộ slot đồng nhất).
- **Nhóm C: Vehicles & Parking Sessions:** Thông tin xe ô tô và quản lý vòng đời phiên gửi xe từ vào đến ra (hệ thống chuyên biệt cho ô tô tiêu chuẩn, không phân loại xe máy/xe điện/nhiên liệu).
- **Nhóm D: Parking Dispatch & Allocation:** Hàng đợi điều phối, gán vị trí đỗ, xác nhận đỗ thực tế và xử lý xe đỗ sai vị trí.
- **Nhóm E: Pricing & Payments:** Thiết lập chính sách biểu phí cho ô tô, tính tiền gửi xe theo thời gian thực tế, quản lý giao dịch thanh toán.
- **Nhóm F: Hardware Events, OCR & IoT:** Nhật ký sự kiện đóng/mở barrier, kết quả nhận diện biển số của camera, tín hiệu cảm biến ô đỗ.
- **Nhóm G: Audit & Operations:** Nhật ký kiểm toán các thao tác can thiệp của con người và danh mục cảnh báo vận hành.

---

## 4. LUỒNG DỮ LIỆU TỔNG THỂ (END-TO-END DATA FLOW)

### Luồng 1: Xe vào cổng (Check-in Flow)
1. Xe tiến vào cổng vào -> Cảm biến phát hiện phương tiện -> Kích hoạt Camera chụp ảnh.
2. AI OCR nhận diện biển số -> Gửi kết quả (biển số, ảnh chụp, điểm tin cậy) về Backend.
3. Backend kiểm tra:
   - Biển số này có đang trong phiên gửi xe `ACTIVE` nào trong bãi không (chống trùng lặp/gian lận).
   - Bãi đỗ còn chỗ trống khả dụng cho ô tô hay không.
4. Nếu hợp lệ:
   - Ghi bản ghi vào `gate_events` (Sự kiện vào cổng).
   - Tạo bản ghi mới trong `parking_sessions` với trạng thái `ACTIVE`, ghi nhận thời điểm vào (`check_in_time`).
   - Gửi lệnh mở barrier vào.
   - Tạo bản ghi điều phối trong `parking_dispatches` với trạng thái `PENDING` (Chờ phân công ô đỗ).
   - **Lưu ý:** Xe chưa vào đỗ, các ô đỗ vẫn ở trạng thái vật lý `EMPTY`.

### Luồng 2: Điều phối & Đỗ xe thực tế (Dispatch & Parking Flow)
1. Hệ thống (thuật toán tự động) hoặc Nhân viên điều hành (thao tác trên giao diện Dashboard/3D) chọn một ô đỗ phù hợp đang ở trạng thái `EMPTY` và chưa bị gán cho xe khác.
2. Cập nhật `parking_dispatches`: `assigned_slot_id = slot.id`, trạng thái chuyển sang `ASSIGNED`.
3. Giao diện 3D nhận sự kiện, hiển thị chỉ dẫn xe di chuyển tới ô đỗ được phân công.
4. Khi xe vào ô đỗ thực tế:
   - Cảm biến phần cứng tại ô đỗ (hoặc trigger mô phỏng 3D) gửi tín hiệu báo có vật thể chiếm chỗ.
   - Backend đối chiếu giữa ô được gán (`assigned_slot_id`) và ô cảm biến báo có xe:
     - **Trường hợp ĐÚNG Ô:** Cập nhật `parking_dispatches` sang `OCCUPIED`, cập nhật `parking_slots.physical_status = 'OCCUPIED'`, cập nhật `parking_sessions.current_slot_id = slot.id`.
     - **Trường hợp ĐỖ SAI Ô (Wrong Slot):** Cập nhật `parking_dispatches` sang `WRONG_SLOT`, cập nhật ô thực tế bị đỗ sang `OCCUPIED`, kích hoạt cảnh báo vận hành để nhân viên xử lý.

### Luồng 3: Tính phí & Thanh toán (Pricing & Payment Flow)
1. Khách hàng yêu cầu thanh toán (quét mã QR tại trạm, ứng dụng hoặc tại quầy thu phí).
2. Backend tính toán thời lượng đỗ: `Duration = Thời điểm tính phí - check_in_time`.
3. Tra cứu biểu phí hiện hành trong `pricing_policies` của bãi xe để tính ra tổng số tiền.
4. Tạo bản ghi giao dịch thanh toán trong `payments` với trạng thái `PENDING`.
5. Khi nhận được xác nhận thanh toán thành công (từ cổng thanh toán điện tử hoặc xác nhận thu tiền mặt):
   - Cập nhật `payments.status = 'SUCCESS'`.
   - Cập nhật `parking_sessions.status = 'PAID'`, ghi nhận `total_fee` và thời điểm thanh toán `paid_at`.
   - **Lưu ý quan trọng:** Xe vẫn đang đỗ trong bãi, ô đỗ vẫn `OCCUPIED`, phiên gửi xe chưa kết thúc.

### Luồng 4: Xe rời cổng (Check-out Flow)
1. Xe di chuyển đến cổng ra -> Camera cổng ra chụp ảnh và AI OCR nhận diện biển số.
2. Backend tra cứu phiên gửi xe tương ứng đang có trạng thái `PAID`:
   - **Kiểm tra Điều kiện Xuất bãi & Thời gian Ân hạn (Grace Period):**
     - Khoảng thời gian từ lúc thanh toán (`paid_at`) đến hiện tại có nằm trong giới hạn ân hạn cho phép hay không?
     - *Nếu trong hạn:* Hợp lệ để xuất bãi.
     - *Nếu vượt quá thời gian ân hạn:* Yêu cầu thanh toán bổ sung cho khoảng thời gian vượt hạn trước khi cho phép ra.
3. Khi điều kiện xuất bãi được xác nhận:
   - Gửi tín hiệu mở barrier ra.
   - Xe lăn bánh qua cổng ra -> Cảm biến cổng ra báo xe đã qua -> Barrier đóng lại.
   - Ghi nhận sự kiện xuất bãi vào `gate_events`.
   - Cập nhật `parking_sessions.status = 'COMPLETED'`, ghi nhận thời điểm rời bãi `check_out_time`.
   - Cập nhật ô đỗ liên quan trong `parking_slots.physical_status = 'EMPTY'`. Phiên gửi xe hoàn tất trọn vẹn.

---

## 5. MA TRẬN PHÂN CHIA TRÁCH NHIỆM THÀNH VIÊN TRONG DỰ ÁN

| Thành viên | Trách nhiệm chuyên môn | Phạm vi tương tác với Database | Module chính |
| :--- | :--- | :--- | :--- |
| **Phụng** | Backend & DB Lead | Kiến trúc CSDL tổng thể, Tài khoản & Phân quyền, Hạ tầng bãi xe, Vòng đời Phiên gửi xe, Quản trị hệ thống | Nhóm A, B, C, G |
| **Nam Anh** | Payment & Pricing | Thiết kế biểu phí, tính toán tiền gửi xe, xử lý giao dịch thanh toán và đối soát thanh toán | Nhóm E |
| **Thức** | IoT & Hardware | Tín hiệu cảm biến ô đỗ (Telemetry), trạng thái barrier cổng, điều khiển phần cứng | Nhóm F |
| **Đức** | AI & OCR Vision | Kết quả nhận diện biển số xe (OCR), độ tin cậy nhận diện, ảnh chụp snapshot cổng vào/ra | Nhóm F, C |
| **Song** | Frontend & 3D Web | Hiển thị hiện trạng bãi xe trên Web Dashboard, render vị trí và trạng thái ô đỗ trên mô hình 3D | Nhóm B, D |
| **Trí** | DevOps & Cloud | Cấu hình Neon PostgreSQL, biến môi trường, backup, quản lý connection pool và CI/CD | Hạ tầng Cloud |
| **Danh** | QA & Testing | Kiểm thử tính toàn vẹn dữ liệu, kiểm thử tải tương tranh (concurrency), kịch bản lỗi | Toàn hệ thống |
