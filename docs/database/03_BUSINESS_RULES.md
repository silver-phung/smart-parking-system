# SMART PARKING SYSTEM – DATABASE BUSINESS RULES
## QUY TẮC NGHIỆP VỤ & RÀNG BUỘC CƠ SỞ DỮ LIỆU

- **Dự án:** Smart Parking System (FPT Software OJT)
- **Tài liệu:** Task 02A – Quy tắc nghiệp vụ CSDL (Bản rà soát và chuẩn hóa)
- **Mục tiêu:** Định nghĩa rõ ràng toàn bộ các quy tắc nghiệp vụ, logic ràng buộc dữ liệu, các tình huống ngoại lệ và xử lý tương tranh, đảm bảo không áp đặt giả định chưa được phê duyệt.

---

## 1. NGUYÊN TẮC NGHIỆP VỤ CỐT LÕI (CORE BUSINESS INVARIANTS)

1. **Check-in không đồng nghĩa với việc xe đã đỗ:**
   - Khi xe qua cổng vào thành công, hệ thống chỉ ghi nhận sự kiện xe vào, tạo bản ghi `parking_sessions` ở trạng thái `ACTIVE` và đưa xe vào danh sách chờ điều phối (`WAITING_DISPATCH`).
   - Ô đỗ vật lý trong bãi vẫn hoàn toàn trống (`EMPTY`).
2. **Trạng thái vật lý của Slot tách biệt hoàn toàn với Trạng thái Phân công:**
   - **Trạng thái vật lý (`parking_slots.physical_status`):** Phản ánh hiện trạng cảm biến phần cứng (`EMPTY` - Trống, `OCCUPIED` - Có xe, `MAINTENANCE` - Bảo trì, `UNAVAILABLE` - Tạm thời không khả dụng).
   - **Trạng thái phân công (`parking_dispatches.status`):** Phản ánh tiến trình điều phối (`PENDING` -> `ASSIGNED` -> `OCCUPIED` / `WRONG_SLOT` / `CANCELLED`).
   - **Quy tắc không gộp trạng thái:** Tuyệt đối không thêm cờ `is_reserved` vào bảng `parking_slots` khi chưa được phê duyệt. Tính khả dụng của một slot được xác định bằng điều kiện: `physical_status = 'EMPTY'` VÀ không có bản ghi `parking_dispatches` nào đang ở trạng thái `ASSIGNED` trỏ tới slot đó.
3. **Chống phân công trùng lặp (Anti-Collision Assignment):**
   - Một vị trí đỗ không thể được phân công đồng thời cho nhiều xe đang chờ đỗ. Khi một lệnh điều phối đang ở trạng thái `ASSIGNED`, slot đó bị khóa phân công đối với các xe khác.
4. **Thanh toán thành công không đồng nghĩa với việc xe đã ra khỏi bãi:**
   - Khi giao dịch thanh toán thành công, bản ghi `payments` chuyển sang `SUCCESS`, bản ghi `parking_sessions` chuyển sang `PAID` và ghi nhận mốc thời gian thanh toán (`paid_at`).
   - Chiếc xe vẫn đang nằm tại vị trí đỗ; ô đỗ vẫn ở trạng thái vật lý `OCCUPIED`.
5. **Check-out bắt buộc xác minh xe thực sự rời cổng:**
   - Barrier mở không có nghĩa là phiên đã hoàn tất. Phiên gửi xe chỉ được chuyển sang trạng thái `COMPLETED` và ô đỗ chỉ được giải phóng về `EMPTY` khi cảm biến cổng ra xác nhận chiếc xe đã lăn bánh hoàn toàn qua barrier ra và barrier đã hạ xuống an toàn.
6. **Bảo vệ dữ liệu cá nhân (Data Privacy):**
   - Chỉ lưu trữ các trường dữ liệu định danh tối thiểu cần thiết cho vận hành (Email, Họ tên, Số điện thoại để liên hệ khi xảy ra sự cố va chạm hoặc đỗ quá giờ). Không lưu trữ các thông tin cá nhân nhạy cảm không cần thiết.
7. **Truy vết thay đổi quan trọng (Auditability):**
   - Mọi thao tác can thiệp thủ công hoặc nhạy cảm (mở barrier cưỡng bức, sửa giá vé, hủy phiên đỗ xe, điều chỉnh gán slot thủ công) bắt buộc phải được ghi nhận vào bảng `audit_logs` kèm định danh người thực hiện, thời gian, IP và dữ liệu trước/sau khi đổi.

---

## 2. QUY TẮC CHI TIẾT THEO TỪNG BƯỚC VẬN HÀNH

### 2.1. Quy tắc Xe vào cổng (Check-in Rules)

```
[Xe tới cổng vào] ---> [Camera OCR nhận diện biển số] ---> [Kiểm tra điều kiện vào]
                                                                  |
                             +------------------------------------+------------------------------------+
                             |                                                                         |
                        [ĐỦ ĐIỀU KIỆN]                                                            [TỪ CHỐI VÀO]
                             |                                                                         |
             - Ghi gate_events (IN)                                                    - Ghi gate_events (REJECTED)
             - Tạo parking_sessions (ACTIVE)                                           - Bảng LED hiển thị lý do
             - Mở barrier vào                                                          - Barrier giữ nguyên ĐÓNG
             - Tạo parking_dispatches (PENDING)
```

- **Điều kiện kiểm tra trước khi cho xe vào:**
  1. *Chống gian lận biển số (Anti-Passback Check):*
     - Kiểm tra biển số xe nhận diện được có đang tồn tại trong một phiên gửi xe nào chưa kết thúc (`status IN ('ACTIVE', 'PAID')`) hay không.
     - Nếu phát hiện trùng lặp: Barrier không tự động mở; hệ thống phát cảnh báo cho nhân viên trực cổng để kiểm tra trực tiếp (ngăn chặn hành vi dùng chung một biển số hoặc lỗi OCR đọc nhầm).
  2. *Kiểm tra sức chứa khả dụng (Capacity Check):*
     - Bãi đỗ phải còn ít nhất một ô đỗ thỏa mãn đồng thời hai điều kiện:
       - Có `physical_status = 'EMPTY'`.
       - Không có lệnh điều phối nào đang `ASSIGNED` vào ô đó.
     - Nếu hết chỗ cho ô tô: Cổng từ chối mở, bảng LED hiển thị "BÃI XE ĐÃ ĐẦY".
  3. *Ngưỡng tin cậy OCR (OCR Confidence Verification):*
     - Điểm tin cậy của OCR được so sánh với ngưỡng quy định của hệ thống:
       - *Đạt ngưỡng tin cậy:* Hệ thống tự động tạo phiên và phát tín hiệu mở barrier.
       - *Dưới ngưỡng tin cậy:* Giao diện trực cổng hiển thị hình ảnh chụp snapshot phóng to để Nhân viên điều hành (Operator) xác nhận biển số bằng mắt hoặc sửa biển số trước khi bấm mở cổng thủ công (hành động này tự động ghi vết vào `audit_logs`).

---

## 2.2. Quy tắc Điều phối & Đỗ xe (Dispatching & Slot Assignment Rules)

- **Quy trình điều phối chuẩn:**
  1. *Khởi tạo lệnh điều phối:* Ngay khi phiên xe được tạo, một bản ghi trong `parking_dispatches` được tạo với trạng thái `PENDING` (`WAITING_DISPATCH`).
  2. *Chỉ định ô đỗ (Thuật toán tự động hoặc Nhân viên thao tác):*
     - Chỉ được chọn ô đỗ đang trống (`physical_status = 'EMPTY'`) và chưa có lệnh điều phối `ASSIGNED` nào trỏ tới.
     - Cập nhật bản ghi điều phối: `assigned_slot_id = slot.id`, trạng thái chuyển sang `ASSIGNED`, ghi nhận thời điểm phân công.
     - Yêu cầu cốt lõi của thuật toán điều phối là tìm được ô đỗ khả dụng (`EMPTY` và chưa gán `ASSIGNED`). Việc ưu tiên vị trí gần thang máy/lối ra chỉ là giải pháp đề xuất/cấu hình thêm nếu có, không phải yêu cầu bắt buộc trừ khi Sprint 3 có quy định cụ thể. Hệ thống không tự giả định toàn bộ slot có cùng kích thước và không cần logic phân loại theo nhiên liệu hay xe điện.
  3. *Chống phân công trùng lặp (Concurrency Safety):*
     - Thao tác gán slot phải được bảo vệ bằng giao dịch CSDL (Transaction) để đảm bảo hai tiến trình điều phối đồng thời không thể cùng gán một ô đỗ cho hai xe khác nhau.
  4. *Xác nhận xe đã đỗ thực tế qua Cảm biến:*
     - Khi xe di chuyển vào vị trí đỗ, cảm biến tại ô đỗ phát hiện vật thể và gửi tín hiệu báo trạng thái có xe.
     - Backend đối chiếu giữa ô được phân công (`assigned_slot_id`) và ô cảm biến phát hiện xe:
       - **Trường hợp ĐÚNG Ô PHÂN CÔNG:**
         - Cập nhật `parking_dispatches`: `actual_slot_id = slot.id`, trạng thái chuyển sang `OCCUPIED`, ghi nhận thời điểm xác nhận.
         - Cập nhật `parking_slots`: `physical_status = 'OCCUPIED'`.
         - Cập nhật `parking_sessions`: `current_slot_id = slot.id`.
       - **Trường hợp ĐỖ SAI VỊ TRÍ (Wrong Slot Occupancy):**
         - Xe đỗ vào ô B trong khi được chỉ định ô A.
         - Cập nhật bản ghi điều phối của xe: `status = 'WRONG_SLOT'`, `actual_slot_id = slot_B.id`.
         - Cập nhật ô B: `physical_status = 'OCCUPIED'`.
         - Ô A vẫn giữ nguyên trạng thái vật lý `EMPTY` và lệnh phân công vào ô A được xử lý hủy để ô A sẵn sàng đón xe khác.
         - Kích hoạt cảnh báo vận hành để nhân viên điều hành can thiệp hỗ trợ.
  5. *Hết hạn điều phối (Dispatch Timeout):*
     - Nếu sau một khoảng thời gian quy định kể từ lúc phân công mà không có tín hiệu cảm biến báo xe vào ô:
       - Lệnh điều phối chuyển sang trạng thái `CANCELLED` hoặc `EXPIRED`.
       - Ô đỗ đó được trả về trạng thái sẵn sàng để phân công cho xe tiếp theo.

---

## 2.3. Quy tắc Tính phí & Thanh toán (Pricing & Payment Rules)

- **Tính toán thời lượng gửi xe:**
  - `Thời lượng gửi xe = Thời điểm tính phí - session.check_in_time`.
- **Áp dụng chính sách biểu phí:**
  - Tra cứu biểu phí hiện hành từ bảng `pricing_policies` theo địa điểm bãi (`lot_id`) áp dụng chung cho ô tô.
  - Hỗ trợ tính theo block thời gian: Block đầu tiên, các block tiếp theo, mức trần tối đa trong ngày, và phụ thu khung giờ đặc biệt (nếu có cấu hình).
- **Quy tắc Miễn phí Thả khách nhanh (Free Drop-off):**
  - Nếu thời lượng gửi xe nhỏ hơn hoặc bằng khoảng thời gian miễn phí quy định của chính sách:
    - Tổng tiền gửi xe = 0 VNĐ.
    - Phiên gửi xe tự động được coi là hợp lệ để xuất bãi mà không cần qua bước thanh toán tiền.
- **Quy tắc Giao dịch Thanh toán:**
  - Hỗ trợ thanh toán bằng Tiền mặt tại quầy hoặc Cổng thanh toán điện tử.
  - Mỗi giao dịch thanh toán được ghi nhận độc lập trong bảng `payments`.
  - Khóa chống trùng lặp (Idempotency): Mỗi giao dịch phải có mã giao dịch duy nhất để tránh trừ tiền hai lần khi mạng chập chờn hoặc người dùng bấm xác nhận nhiều lần.
  - Khi thanh toán thành công:
    - Bản ghi `payments` chuyển trạng thái `SUCCESS`.
    - Bản ghi `parking_sessions` chuyển trạng thái sang `PAID`, lưu số tiền đã thu và mốc thời gian thanh toán `paid_at = NOW()`.
    - **Ô đỗ xe vẫn giữ nguyên trạng thái `OCCUPIED` vì chiếc xe vẫn đang nằm trong bãi.**

---

### 2.4. Quy tắc Xe ra bãi & Thời gian Ân hạn (Check-out & Grace Period Rules)

```
[Xe tới cổng ra] ---> [Camera OCR nhận diện biển số] ---> [Tra cứu phiên gửi xe]
                                                                |
                     +------------------------------------------+------------------------------------------+
                     |                                                                                     |
                [TRẠNG THÁI: PAID]                                                                 [CHƯA THANH TOÁN]
                     |                                                                                     |
         [Kiểm tra Thời gian Ân hạn]                                                              Yêu cầu thanh toán
                     |                                                                            tại Kiosk / Quầy
        +------------+------------+
        |                         |
   [TRONG HẠN]                [QUÁ HẠN]
        |                         |
- Mở barrier ra              - Yêu cầu thanh toán phụ thu quá giờ
- Cảm biến báo xe qua        - Sau khi thanh toán đủ -> Cấp hạn mới
- Session = COMPLETED        - Mở barrier ra
- Slot = EMPTY
```

- **Quy tắc Thời gian ân hạn (Grace Period Rule):**
  - Sau khi thanh toán thành công, khách hàng được hưởng một khoảng thời gian ân hạn hợp lý để di chuyển từ quầy/thang máy ra xe và lái xe tới cổng kiểm soát xuất bãi.
  - **Trường hợp Xuất bãi trong hạn:**
    - `Thời gian xuất bãi - session.paid_at <= Thời gian ân hạn cho phép`.
    - Hệ thống xác nhận đủ điều kiện rời bãi và gửi lệnh mở barrier ra.
  - **Trường hợp Xuất bãi quá hạn (Overstay after Payment):**
    - `Thời gian xuất bãi - session.paid_at > Thời gian ân hạn cho phép`.
    - Hệ thống từ chối mở barrier ra; tự động tính khoản phụ thu bổ sung cho khoảng thời gian vượt quá.
    - Sau khi khách hàng hoàn tất thanh toán phụ thu, thời gian ân hạn được tính lại từ mốc mới để cho phép xe ra.
- **Xác minh xe thực sự rời bãi (Check-out Confirmation):**
  - Khi barrier ra mở, xe bắt đầu di chuyển qua cổng.
  - Cảm biến cổng ra (Loop detector / Infrared sensor) xác nhận xe đã lăn bánh hoàn toàn qua vạch barrier và barrier đã hạ xuống an toàn.
  - Hệ thống ghi nhận sự kiện xuất bãi vào `gate_events`.
  - Cập nhật `parking_sessions.status = 'COMPLETED'`, ghi nhận thời điểm ra thực tế `check_out_time`.
  - Cập nhật ô đỗ xe tương ứng: `parking_slots.physical_status = 'EMPTY'`.

---

## 3. CÁC TRƯỜNG HỢP NGOẠI LỆ ĐẶC BIỆT (EDGE CASES)

| STT | Tình huống ngoại lệ | Rủi ro dữ liệu | Giải pháp xử lý |
| :---: | :--- | :--- | :--- |
| 1 | **Biển số lúc ra không khớp lúc vào (do biển số bẩn, thời tiết mờ)** | Không tra cứu được phiên xe tương ứng, xe bị kẹt cổng ra | Nhân viên trực cổng tra cứu phiên theo thời gian vào, ảnh chụp vào hoặc màu xe; thực hiện đối chiếu thủ công và xác nhận mở cổng có ghi vết `audit_logs`. |
| 2 | **Cúp điện hoặc mở barrier cơ học thủ công** | Xe đã rời bãi nhưng CSDL không nhận được tín hiệu cảm biến, slot bị treo trạng thái `OCCUPIED` | Bảng `gate_events` hỗ trợ ghi sự kiện `MANUAL_OVERRIDE`. Cung cấp quyền cho Quản trị viên đóng phiên cưỡng bức (`FORCE_COMPLETE`) kèm lý do giải trình bắt buộc. |
| 3 | **Nhiều tiến trình cùng chọn một ô đỗ trống** | Xung đột dữ liệu (Race condition), ô đỗ bị gán đồng thời cho 2 xe | Sử dụng Transaction với khóa bi quan (`SELECT ... FOR UPDATE`) hoặc cơ chế khóa lạc quan trên PostgreSQL khi thực hiện lệnh gán ô đỗ. |
| 4 | **Xe lưu bãi dài ngày vượt quá quy định** | Ô đỗ bị chiếm dụng lâu ngày, phát sinh nợ phí lớn | Hệ thống chạy tiến trình kiểm tra định kỳ (Background Job) quét các phiên `ACTIVE` có thời lượng vượt giới hạn cảnh báo để thông báo cho Quản trị viên xử lý. |

---

## 4. CÁC RÀNG BUỘC DỮ LIỆU BẮT BUỘC (DATABASE CONSTRAINTS)

### 4.1. Ràng buộc duy nhất (Unique Constraints)
- `users(username)`: Mỗi tài khoản có 1 tên đăng nhập `username` duy nhất (chuẩn hóa lowercase).
- `users(email)`: Mỗi tài khoản chỉ có 1 địa chỉ email thật duy nhất (chuẩn hóa lowercase).
- `roles(code)`: Mã vai trò là duy nhất (`ADMIN`, `OPERATOR`, `CUSTOMER`).
- `parking_lots(name)`: Tên bãi đỗ không trùng lặp.
- `parking_slots(floor_id, slot_code)`: Trong cùng một tầng, mã ô đỗ không được phép trùng nhau.
- `payments(transaction_code)`: Mỗi giao dịch thanh toán có một mã duy nhất.

### 4.2. Ràng buộc toàn vẹn và kiểm tra logic (Check Constraints)
- `parking_sessions`: `check_out_time >= check_in_time` (Thời điểm ra phải sau hoặc bằng thời điểm vào).
- `parking_sessions`: `total_fee >= 0` (Phí gửi xe không được phép mang giá trị âm).
- `payments`: `amount >= 0` (Số tiền thanh toán không được âm).
- `pricing_policies`: Các mức phí cấu hình không được âm (`>= 0`).

### 4.3. Ràng buộc xóa dữ liệu (Deletion Rules)
- Tuyệt đối **không sử dụng `CASCADE DELETE`** đối với các bảng lưu vết vận hành (`parking_sessions`, `payments`, `gate_events`, `audit_logs`).
- Áp dụng nguyên tắc vô hiệu hóa trạng thái (Soft Deactivation: `is_active = FALSE`) đối với người dùng, ô đỗ và bảng giá để bảo toàn toàn vẹn lịch sử giao dịch trong quá khứ.
