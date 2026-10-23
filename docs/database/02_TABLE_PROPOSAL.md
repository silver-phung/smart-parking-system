# SMART PARKING SYSTEM – PROPOSED DATABASE TABLES
## DANH MỤC BẢNG CƠ SỞ DỮ LIỆU ĐỀ XUẤT CHO POSTGRESQL

- **Dự án:** Smart Parking System (FPT Software OJT)
- **Tài liệu:** Task 02A – Đề xuất danh mục bảng (Bản rà soát và chuẩn hóa)
- **Hệ quản trị CSDL:** PostgreSQL (Neon Serverless Postgres)
- **Quy ước đặt tên:** Danh từ số nhiều tiếng Anh dạng `snake_case` (ví dụ: `parking_sessions`, `gate_events`).

---

## 1. TỔNG QUAN PHÂN LOẠI VÀ SỐ LƯỢNG BẢNG

Toàn bộ hệ thống sau khi rà soát chặt chẽ gồm **19 bảng đề xuất** (hệ thống tinh gọn chuyên biệt cho ô tô tiêu chuẩn, không phân loại xe máy/xe điện/nhiên liệu, loại bỏ bảng dư thừa `vehicle_types`), được phân thành 3 mức độ ưu tiên rõ ràng:

| Phân loại ưu tiên | Ý nghĩa | Số lượng bảng | Danh sách bảng |
| :--- | :--- | :---: | :--- |
| **CORE** | **Bắt buộc cho Sprint 3** để vận hành luồng nghiệp vụ cốt lõi | **14 bảng** | `users`, `roles`, `user_tokens`, `refresh_tokens`, `parking_lots`, `parking_floors`, `parking_slots`, `vehicles`, `parking_sessions`, `parking_dispatches`, `pricing_policies`, `payments`, `gate_events`, `audit_logs` |
| **CONDITIONAL** | **Cần nhóm xác nhận** nhu cầu kỹ thuật trước khi triển khai | **3 bảng** | `slot_sensor_logs`, `alerts`, `system_configs` |
| **FUTURE** | **Chưa triển khai trong Sprint 3** (Dành cho giai đoạn mở rộng) | **2 bảng** | `parking_reservations`, `subscriptions` |
| **TỔNG CỘNG** | | **19 bảng** | |

### Phân định nguồn gốc yêu cầu:
- **`CONFIRMED`:** Yêu cầu đã được xác nhận trực tiếp từ đề bài OJT và quyết định nghiệp vụ chính thức của Owner Phụng (Smart Parking chỉ phục vụ ô tô tiêu chuẩn).
- **`PROPOSED`:** Đề xuất kỹ thuật của Antigravity nhằm tối ưu hóa thiết kế CSDL (đạt chuẩn 3NF, tách biệt trạng thái).
- **`NEEDS CONFIRMATION`:** Hạng mục kỹ thuật đang chờ ý kiến thống nhất từ Owner Phụng hoặc các thành viên liên quan.

---

## 2. CHI TIẾT DANH MỤC BẢNG CỐT LÕI (CORE - 13 BẢNG BẮT BUỘC)

### NHÓM A: ACCOUNTS & AUTHORIZATION (TÀI KHOẢN & PHÂN QUYỀN)

#### 1. Bảng `users`
- **Mục đích (tiếng Việt):** Quản lý tài khoản đăng nhập của người dùng trong hệ thống (Quản trị viên, Nhân viên vận hành, Khách hàng).
- **Module / Thành viên sử dụng:** Module Identity & Auth / Phụng phụ trách.
- **5 Câu hỏi phân tích:**
  - *Bảng này dùng để làm gì?* Lưu trữ danh tính người dùng (tên đăng nhập `username`, địa chỉ `email`, mật khẩu băm, họ tên, số điện thoại, vai trò `role_id`, trạng thái kích hoạt, trạng thái xác minh email, trạng thái phê duyệt). Đăng nhập bằng `username` + mật khẩu; `email` bắt buộc phải là email thật và được xác minh qua mã OTP gửi về hộp thư.
  - *Ai tạo dữ liệu?* Khách hàng tự đăng ký qua Web/App, hoặc Quản trị viên khởi tạo tài khoản cho Nhân viên.
  - *Ai đọc hoặc cập nhật?* Auth Service kiểm tra khi đăng nhập (tra cứu theo `username`); người dùng cập nhật thông tin cá nhân; Quản trị viên khóa/mở tài khoản.
  - *Nó liên kết với bảng nào?* Liên kết với `roles` (qua `role_id` 1-N), `vehicles`, `audit_logs`, `refresh_tokens`, `user_tokens`.
  - *Nếu bỏ bảng này thì có ảnh hưởng gì?* Hệ thống không thể xác thực người dùng, không phân biệt được ai là bảo vệ, quản trị viên hay khách hàng.
- **Mức độ ưu tiên:** **CORE** (Bắt buộc cho Sprint 3).
- **Căn cứ nguồn gốc:** **CONFIRMED & APPROVED BY OWNER**.

#### 2. Bảng `roles`
- **Mục đích (tiếng Việt):** Quản lý danh mục các vai trò trong hệ thống để thực hiện phân quyền truy cập (RBAC).
- **Module / Thành viên sử dụng:** Module Identity & Auth / Phụng phụ trách.
- **5 Câu hỏi phân tích:**
  - *Bảng này dùng để làm gì?* Định nghĩa các vai trò hợp lệ trong hệ thống (`ADMIN`, `OPERATOR`, `CUSTOMER`).
  - *Ai tạo dữ liệu?* Hệ thống tạo sẵn qua dữ liệu khởi tạo (Data Seeding).
  - *Ai đọc hoặc cập nhật?* Middleware phân quyền đọc để kiểm tra quyền hạn của từng API.
  - *Nó liên kết với bảng nào?* Liên kết với `users` thông qua khóa ngoại 1-N (`users.role_id`).
  - *Nếu bỏ bảng này thì có ảnh hưởng gì?* Phải hardcode vai trò vào mã nguồn C#, mất tính linh hoạt khi cần bổ sung vai trò mới.
- **Mức độ ưu tiên:** **CORE** (Bắt buộc cho Sprint 3).
- **Căn cứ nguồn gốc:** **CONFIRMED & APPROVED BY OWNER**.

#### 3. Bảng `user_tokens`
- **Mục đích (tiếng Việt):** Quản lý mã xác thực OTP một lần (6 chữ số) được gửi qua dịch vụ email thật để kích hoạt tài khoản khi đăng ký mới.
- **Module / Thành viên sử dụng:** Module Identity & Auth / Phụng phụ trách.
- **5 Câu hỏi phân tích:**
  - *Bảng này dùng để làm gì?* Lưu mã OTP băm an toàn HMAC-SHA256 kèm userId và purpose, số lần nhập sai `attempts_count`, thời hạn mã `expires_at` và cờ đã sử dụng `is_used`.
  - *Ai tạo dữ liệu?* Hệ thống tự động sinh khi người dùng đăng ký hoặc yêu cầu gửi lại mã xác minh.
  - *Ai đọc hoặc cập nhật?* Auth Service tra cứu, đếm số lần sai và cập nhật cờ `is_used = TRUE` nguyên tử khi xác minh.
  - *Nó liên kết với bảng nào?* Khóa ngoại trỏ đến `users.id` (ON DELETE CASCADE).
  - *Nếu bỏ bảng này thì có ảnh hưởng gì?* Không thể thực hiện quy trình xác thực email thật, không chống được brute-force và replay attack.
- **Mức độ ưu tiên:** **CORE** (Bắt buộc cho Sprint 3).
- **Căn cứ nguồn gốc:** **CONFIRMED & APPROVED BY OWNER**.

#### 4. Bảng `refresh_tokens`
- **Mục đích (tiếng Việt):** Quản lý vòng đời của Refresh Token để hỗ trợ duy trì phiên đăng nhập và thu hồi token khi đăng xuất.
- **Module / Thành viên sử dụng:** Module Identity & Auth / Phụng phụ trách.
- **5 Câu hỏi phân tích:**
  - *Bảng này dùng để làm gì?* Lưu chuỗi băm SHA-256 duy nhất của Refresh Token, thời hạn 7 ngày, cờ thu hồi `is_revoked`.
  - *Ai tạo dữ liệu?* Hệ thống sinh khi người dùng đăng nhập thành công.
  - *Ai đọc hoặc cập nhật?* Auth Service kiểm tra khi cấp mới JWT Access Token hoặc đánh dấu thu hồi khi người dùng đăng xuất.
  - *Nó liên kết với bảng nào?* Khóa ngoại trỏ đến `users.id` (ON DELETE CASCADE).
  - *Nếu bỏ bảng này thì có ảnh hưởng gì?* Không thể thu hồi phiên đăng nhập khi đăng xuất, buộc người dùng đăng nhập lại thường xuyên hoặc để JWT tồn tại vô hạn.
- **Mức độ ưu tiên:** **CORE** (Bắt buộc cho Sprint 3).
- **Căn cứ nguồn gốc:** **CONFIRMED & APPROVED BY OWNER**.

---

### NHÓM B: PARKING STRUCTURE (HẠ TẦNG BÃI ĐỖ XE)

#### 4. Bảng `parking_lots`
- **Mục đích (tiếng Việt):** Lưu trữ thông tin định danh và địa điểm của từng bãi đỗ xe.
- **Module / Thành viên sử dụng:** Module Cấu trúc bãi xe / Phụng, Song phụ trách.
- **5 Câu hỏi phân tích:**
  - *Bảng này dùng để làm gì?* Lưu tên bãi, địa chỉ thực tế, tổng sức chứa thiết kế, trạng thái hoạt động.
  - *Ai tạo dữ liệu?* Quản trị viên (Admin) thiết lập cấu hình ban đầu.
  - *Ai đọc hoặc cập nhật?* Hệ thống đọc để tính tỷ lệ lấp đầy; Web Dashboard hiển thị thông tin bãi; Song đọc để tải cấu hình bãi đỗ.
  - *Nó liên kết với bảng nào?* Liên kết 1-N với `parking_floors`, `pricing_policies`, `parking_sessions`.
  - *Nếu bỏ bảng này thì có ảnh hưởng gì?* Hệ thống chỉ quản lý được một bãi xe duy nhất vô danh, không thể mở rộng mô hình chuỗi bãi xe.
- **Mức độ ưu tiên:** **CORE** (Bắt buộc cho Sprint 3).
- **Căn cứ nguồn gốc:** **CONFIRMED** (Yêu cầu Mục 3B Bãi xe).

#### 5. Bảng `parking_floors`
- **Mục đích (tiếng Việt):** Quản lý các tầng hoặc các phân khu (Zone/Floor) bên trong bãi đỗ xe.
- **Module / Thành viên sử dụng:** Module Cấu trúc bãi xe / Phụng, Song phụ trách.
- **5 Câu hỏi phân tích:**
  - *Bảng này dùng để làm gì?* Lưu danh sách các tầng (Tên tầng, số thứ tự tầng: Hầm B1, Tầng 1, Tầng 2,...).
  - *Ai tạo dữ liệu?* Quản trị viên tạo khi thiết lập bản đồ bãi xe.
  - *Ai đọc hoặc cập nhật?* Song tải thông tin tầng để kết xuất mô hình 3D theo từng mặt bằng; Operator lọc ô đỗ theo tầng.
  - *Nó liên kết với bảng nào?* Khóa ngoại trỏ đến `parking_lots.id`; liên kết 1-N với `parking_slots`.
  - *Nếu bỏ bảng này thì có ảnh hưởng gì?* Không thể phân tầng bãi xe, mô hình 3D không thể bóc tách hiển thị từng tầng.
- **Mức độ ưu tiên:** **CORE** (Bắt buộc cho Sprint 3).
- **Căn cứ nguồn gốc:** **CONFIRMED** (Yêu cầu Mục 3B Tầng).

#### 6. Bảng `parking_slots`
- **Mục đích (tiếng Việt):** Quản lý từng vị trí đỗ ô tô cụ thể và trạng thái vật lý thực tế của ô đỗ.
- **Module / Thành viên sử dụng:** Module Cấu trúc bãi xe, IoT, 3D / Phụng, Thức, Song phụ trách.
- **5 Câu hỏi phân tích:**
  - *Bảng này dùng để làm gì?* Lưu mã ô đỗ (ví dụ: `B1-01`), trạng thái vật lý từ cảm biến (`EMPTY`, `OCCUPIED`, `MAINTENANCE`, `UNAVAILABLE`), vị trí tương đối để hiển thị 3D. Tất cả ô đỗ đều quy chuẩn cho ô tô thông thường (không tự giả định toàn bộ slot có cùng kích thước, kích thước thực tế có thể khác nhau tùy quy hoạch mặt bằng).
  - *Ai tạo dữ liệu?* Quản trị viên khởi tạo mặt bằng bãi xe.
  - *Ai đọc hoặc cập nhật?* IoT Service (Thức) cập nhật trạng thái vật lý khi có ô tô vào đỗ; Song đọc để tô màu ô đỗ trên 3D (Xanh/Đỏ/Xám); Điều phối viên đọc để tìm chỗ trống.
  - *Nó liên kết với bảng nào?* Khóa ngoại trỏ đến `parking_floors.id`.
  - *Nếu bỏ bảng này thì có ảnh hưởng gì?* Không thể quản lý chi tiết từng vị trí đỗ, không thể điều phối xe vào ô và không có dữ liệu để hiển thị mô phỏng 3D.
  - **LƯU Ý THIẾT KẾ:** Bảng này **KHÔNG chứa cờ `is_reserved`** (tránh gộp trạng thái phân công vào ô đỗ). Trạng thái phân công được quản lý độc lập tại bảng `parking_dispatches`. Bốn trạng thái vật lý thống nhất là: `EMPTY` (trống), `OCCUPIED` (có xe), `MAINTENANCE` (bảo trì), `UNAVAILABLE` (không khả dụng). Không cần cột loại phương tiện vì toàn bãi chỉ phục vụ ô tô tiêu chuẩn.
- **Mức độ ưu tiên:** **CORE** (Bắt buộc cho Sprint 3).
- **Căn cứ nguồn gốc:** **CONFIRMED** (Yêu cầu Mục 3B Vị trí đỗ, trạng thái bảo trì/không khả dụng).

---

### NHÓM C: VEHICLES & PARKING SESSIONS (PHƯƠNG TIỆN & PHIÊN GỬI XE)

#### 7. Bảng `vehicles`
- **Mục đích (tiếng Việt):** Quản lý hồ sơ phương tiện ô tô trong hệ thống.
- **Module / Thành viên sử dụng:** Module Phương tiện / Phụng phụ trách.
- **5 Câu hỏi phân tích:**
  - *Bảng này dùng để làm gì?* Lưu biển số xe ô tô chuẩn hóa, nhãn hiệu/màu sắc và người sở hữu (nếu là xe đã đăng ký tài khoản).
  - *Ai tạo dữ liệu?* Khách hàng đăng ký xe vào tài khoản cá nhân, hoặc hệ thống tự động ghi nhận hồ sơ khi ô tô vãng lai vào cổng lần đầu.
  - *Ai đọc hoặc cập nhật?* Cổng vào/ra tra cứu khi nhận diện biển số; khách hàng quản lý xe của mình.
  - *Nó liên kết với bảng nào?* Khóa ngoại trỏ đến `users.id` (cho phép NULL đối với khách vãng lai).
  - *Nếu bỏ bảng này thì có ảnh hưởng gì?* Không gắn kết được lịch sử gửi xe với tài khoản người dùng; khách hàng không thể theo dõi danh sách xe của mình trên ứng dụng.
- **Mức độ ưu tiên:** **CORE** (Bắt buộc cho Sprint 3).
- **Căn cứ nguồn gốc:** **CONFIRMED** (Yêu cầu Mục 3C Thông tin phương tiện).

#### 8. Bảng `parking_sessions` (BẢNG TRUNG TÂM HỆ THỐNG)
- **Mục đích (tiếng Việt):** Quản lý vòng đời trọn vẹn của từng lượt gửi xe ô tô từ khi vào cổng, đỗ xe, thanh toán cho đến khi rời bãi.
- **Module / Thành viên sử dụng:** Toàn bộ hệ thống / Phụng chủ trì, phối hợp Nam Anh, Thức, Đức, Song.
- **5 Câu hỏi phân tích:**
  - *Bảng này dùng để làm gì?* Ghi nhận: Biển số xe ô tô, thời điểm vào (`check_in_time`), thời điểm ra (`check_out_time`), ảnh chụp vào/ra, ô đỗ thực tế (`current_slot_id`), tổng tiền phí, trạng thái phiên (`ACTIVE`, `PAID`, `COMPLETED`, `CANCELLED`).
  - *Ai tạo dữ liệu?* Hệ thống Check-in tự động tạo khi ô tô qua barrier cổng vào.
  - *Ai đọc hoặc cập nhật?*
    - Module Dispatch cập nhật ô đỗ thực tế khi xe đỗ an toàn.
    - Module Payment (Nam Anh) cập nhật trạng thái `PAID` và thời điểm thanh toán `paid_at`.
    - Module Check-out cập nhật thời điểm ra và chuyển trạng thái `COMPLETED`.
    - Dashboard (Song) hiển thị danh sách xe đang trong bãi.
  - *Nó liên kết với bảng nào?* Khóa ngoại trỏ đến `parking_lots.id`, `vehicles.id`, `parking_slots.id`; liên kết 1-N với `parking_dispatches`, `payments`, `gate_events`.
  - *Nếu bỏ bảng này thì có ảnh hưởng gì?* Hệ thống sụp đổ hoàn toàn; không thể theo dõi xe nào đang gửi, không tính được tiền và không kiểm soát được xe ra vào.
- **Mức độ ưu tiên:** **CORE** (Bắt buộc cho Sprint 3).
- **Căn cứ nguồn gốc:** **CONFIRMED** (Yêu cầu Mục 3C Tạo phiên gửi xe, xe rời bãi).

---

### NHÓM D: PARKING DISPATCH (ĐIỀU PHỐI VỊ TRÍ ĐỖ)

#### 9. Bảng `parking_dispatches`
- **Mục đích (tiếng Việt):** Quản lý việc phân công vị trí đỗ và theo dõi trạng thái điều phối; phân tách rạch ròi giữa "Giao chỗ" và "Đỗ thực tế", hỗ trợ phát hiện xe đỗ sai vị trí.
- **Module / Thành viên sử dụng:** Module Dispatch & 3D / Phụng, Song phụ trách.
- **5 Câu hỏi phân tích:**
  - *Bảng này dùng để làm gì?* Lưu vết từng lệnh điều phối: Phiên xe nào (`session_id`), được chỉ định ô đỗ nào (`assigned_slot_id`), thực tế đỗ vào ô nào (`actual_slot_id`), trạng thái điều phối (`PENDING`, `ASSIGNED`, `OCCUPIED`, `WRONG_SLOT`, `CANCELLED`), chế độ điều phối (Tự động / Nhân viên chỉ định). Thuật toán chỉ cần tìm slot trống, không cần logic phân loại nhiên liệu/xe điện.
  - *Ai tạo dữ liệu?* Hệ thống tạo khi xe check-in xong và đưa vào hàng đợi điều phối.
  - *Ai đọc hoặc cập nhật?*
    - Thuật toán Auto-dispatch hoặc Operator cập nhật ô được gán (`ASSIGNED`).
    - Cảm biến IoT / Mô phỏng 3D cập nhật khi xe vào ô thực tế (`OCCUPIED` hoặc `WRONG_SLOT`).
    - 3D View của Song đọc để vẽ đường dẫn di chuyển cho xe.
  - *Nó liên kết với bảng nào?* Khóa ngoại trỏ đến `parking_sessions.id` và `parking_slots.id`.
  - *Nếu bỏ bảng này thì có ảnh hưởng gì?* Vi phạm quy tắc phân tách trạng thái; không thể quản lý hàng đợi điều phối, không thể xử lý tình huống xe đỗ sai vị trí so với chỉ định.
- **Mức độ ưu tiên:** **CORE** (Bắt buộc cho Sprint 3).
- **Căn cứ nguồn gốc:** **CONFIRMED** (Yêu cầu Mục 3D Danh sách chờ điều phối, phân công vị trí, xác nhận đỗ, xử lý đỗ sai).

---

### NHÓM E: PRICING & PAYMENTS (BIỂU PHÍ & THANH TOÁN)

#### 10. Bảng `pricing_policies`
- **Mục đích (tiếng Việt):** Cấu hình các chính sách và biểu giá gửi xe ô tô áp dụng cho bãi xe.
- **Module / Thành viên sử dụng:** Module Pricing / Nam Anh, Phụng phụ trách.
- **5 Câu hỏi phân tích:**
  - *Bảng này dùng để làm gì?* Lưu biểu giá gửi xe ô tô của bãi xe (giá block đầu tiên, giá block tiếp theo, giá trần ngày, phụ thu ban đêm, khoảng thời gian ân hạn cho phép). Không cần phân chia theo loại xe hay nhiên liệu vì bãi chỉ phục vụ ô tô tiêu chuẩn.
  - *Ai tạo dữ liệu?* Quản trị viên (Admin) thiết lập và điều chỉnh chính sách.
  - *Ai đọc hoặc cập nhật?* Module tính phí (Nam Anh) tra cứu để tính tiền gửi xe khi khách yêu cầu thanh toán; Web hiển thị bảng giá cho khách xem.
  - *Nó liên kết với bảng nào?* Khóa ngoại trỏ đến `parking_lots.id`.
  - *Nếu bỏ bảng này thì có ảnh hưởng gì?* Không có căn cứ tính tiền gửi xe; phải hardcode mức giá cố định vào mã nguồn, mất khả năng thay đổi giá theo thực tế.
- **Mức độ ưu tiên:** **CORE** (Bắt buộc cho Sprint 3).
- **Căn cứ nguồn gốc:** **CONFIRMED** (Yêu cầu Mục 3E Chính sách giá, tính phí gửi xe).

#### 11. Bảng `payments`
- **Mục đích (tiếng Việt):** Quản lý chi tiết từng giao dịch thanh toán tiền gửi xe, đảm bảo đối soát chính xác giữa số tiền phải thu và dòng tiền thực nhận.
- **Module / Thành viên sử dụng:** Module Payment / Nam Anh phụ trách.
- **5 Câu hỏi phân tích:**
  - *Bảng này dùng để làm gì?* Ghi nhận: Phiên gửi xe liên quan, số tiền thanh toán, phương thức thanh toán (Tiền mặt / Cổng thanh toán điện tử), mã giao dịch, thời điểm thanh toán, trạng thái giao dịch (`PENDING`, `SUCCESS`, `FAILED`).
  - *Ai tạo dữ liệu?* Module Payment tạo khi khách hàng bắt đầu thực hiện thanh toán.
  - *Ai đọc hoặc cập nhật?* Webhook từ cổng thanh toán cập nhật trạng thái `SUCCESS`; Cổng kiểm soát ra đọc để xác minh xe đã trả tiền hay chưa; Kế toán/Admin tra cứu báo cáo doanh thu.
  - *Nó liên kết với bảng nào?* Khóa ngoại trỏ đến `parking_sessions.id`.
  - *Nếu bỏ bảng này thì có ảnh hưởng gì?* Không theo dõi được lịch sử thanh toán, không biết khách trả bằng tiền mặt hay ví điện tử, không đối soát được khi cổng thanh toán gặp sự cố mạng hoặc tranh chấp hoàn tiền.
- **Mức độ ưu tiên:** **CORE** (Bắt buộc cho Sprint 3).
- **Căn cứ nguồn gốc:** **CONFIRMED** (Yêu cầu Mục 3E Giao dịch thanh toán, xác minh thanh toán trước khi cho xe ra).

---

### NHÓM F: HARDWARE EVENTS & OCR (SỰ KIỆN PHẦN CỨNG & NHẬN DIỆN)

#### 12. Bảng `gate_events`
- **Mục đích (tiếng Việt):** Lưu trữ nhật ký sự kiện phần cứng phát sinh tại cổng vào và cổng ra (Camera OCR chụp ảnh, Barrier đóng/mở, Mở cưỡng bức thủ công).
- **Module / Thành viên sử dụng:** Module OCR & IoT / Đức, Thức, Phụng phụ trách.
- **5 Câu hỏi phân tích:**
  - *Bảng này dùng để làm gì?* Lưu mã cổng (`GATE_IN`, `GATE_OUT`), thời điểm sự kiện, biển số thô nhận diện được từ AI, điểm tin cậy (Confidence Score), URL ảnh chụp snapshot, trạng thái barrier.
  - *Ai tạo dữ liệu?* Dịch vụ AI OCR (Đức) và IoT Controller (Thức) đẩy dữ liệu lên khi có xe qua cổng; Bảo vệ ghi nhận khi mở cổng thủ công.
  - *Ai đọc hoặc cập nhật?* Bảo vệ đối chiếu ảnh xe vào và xe ra; Admin tra cứu đối soát khi có nghi vấn tráo biển số hoặc khiếu nại mất vé.
  - *Nó liên kết với bảng nào?* Khóa ngoại trỏ đến `parking_sessions.id` (có thể NULL nếu xe bị từ chối vào cổng).
  - *Nếu bỏ bảng này thì có ảnh hưởng gì?* Mất hoàn toàn dữ liệu thô từ phần cứng; không thể đối soát ảnh chụp khi OCR đọc sai biển số; không phát hiện được hành vi gian lận hoặc lỗi kỹ thuật tại cổng.
- **Mức độ ưu tiên:** **CORE** (Bắt buộc cho Sprint 3).
- **Căn cứ nguồn gốc:** **CONFIRMED** (Yêu cầu Mục 3F Sự kiện cổng vào/ra, thông tin OCR).

---

### NHÓM G: AUDIT & ADMINISTRATION (KIỂM TOÁN VẬN HÀNH)

#### 13. Bảng `audit_logs`
- **Mục đích (tiếng Việt):** Ghi vết toàn bộ các thao tác nhạy cảm của người dùng và nhân viên quản trị phục vụ công tác thanh tra, bảo mật và phòng chống tiêu cực.
- **Module / Thành viên sử dụng:** Module Quản trị hệ thống / Toàn bộ hệ thống, Phụng chủ trì.
- **5 Câu hỏi phân tích:**
  - *Bảng này dùng để làm gì?* Ghi nhận: Ai thực hiện (`actor_id`), hành động gì (Mở barrier cưỡng bức, sửa biểu giá, hủy phiên xe, gán lại ô đỗ thủ công), tác động lên đối tượng nào, dữ liệu cũ và dữ liệu mới, địa chỉ IP, thời điểm thực hiện.
  - *Ai tạo dữ liệu?* Hệ thống tự động ghi nhận ngầm mỗi khi có API nghiệp vụ nhạy cảm được gọi thành công.
  - *Ai đọc hoặc cập nhật?* Quản trị viên cấp cao (Admin) tra cứu kiểm tra định kỳ; bảng này chỉ cho phép ghi (Append-Only), tuyệt đối không cho sửa hoặc xóa.
  - *Nó liên kết với bảng nào?* Khóa ngoại trỏ đến `users.id`.
  - *Nếu bỏ bảng này thì có ảnh hưởng gì?* Hệ thống không có khả năng truy vết trách nhiệm khi xảy ra thất thoát doanh thu, sự cố mở barrier trái phép hoặc sai lệch dữ liệu vận hành.
- **Mức độ ưu tiên:** **CORE** (Bắt buộc cho Sprint 3).
- **Căn cứ nguồn gốc:** **CONFIRMED** (Yêu cầu Mục 3G Lịch sử thay đổi quan trọng, truy vết thao tác).

---

## 3. CHI TIẾT DANH MỤC BẢNG CẦN XÁC NHẬN (CONDITIONAL - 3 BẢNG)

#### 15. Bảng `slot_sensor_logs`
- **Mục đích (tiếng Việt):** Lưu nhật ký dòng dữ liệu (Telemetry) từ cảm biến siêu âm / từ trường tại từng ô đỗ xe.
- **Module / Thành viên sử dụng:** Module IoT & Hardware / Thức phụ trách.
- **Lý do cần lưu trong Database:** Theo dõi lịch sử phản hồi của cảm biến phần cứng, chẩn đoán cảm biến chập chờn hoặc pin yếu.
- **Điểm cần xác nhận:** Thức và nhóm có nhu cầu lưu lịch sử mọi lần cảm biến đổi trạng thái vào CSDL không, hay IoT Service chỉ cần gọi API cập nhật trạng thái trực tiếp vào bảng `parking_slots` và `parking_dispatches`? (Lưu toàn bộ log có thể làm nặng CSDL).
- **Mức độ ưu tiên:** **CONDITIONAL** (Cần xác nhận trước khi triển khai).
- **Căn cứ nguồn gốc:** **PROPOSED** (NEEDS CONFIRMATION).

#### 16. Bảng `alerts`
- **Mục đích (tiếng Việt):** Quản lý các sự cố cảnh báo vận hành phát sinh trong thời gian thực (Xe đỗ sai vị trí, Xe đỗ quá thời hạn cho phép, Cảm biến mất kết nối).
- **Module / Thành viên sử dụng:** Module Vận hành & 3D / Phụng, Song phụ trách.
- **Lý do cần lưu trong Database:** Giúp nhân viên điều hành theo dõi danh sách các sự cố chưa được xử lý (`is_resolved = false`), ghi nhận người xử lý và thời điểm giải quyết.
- **Điểm cần xác nhận:** Song có cần đọc danh sách cảnh báo từ CSDL để hiển thị lên Dashboard hay chỉ cần nhận tín hiệu tức thời qua WebSocket / In-memory?
- **Mức độ ưu tiên:** **CONDITIONAL** (Cần xác nhận trước khi triển khai).
- **Căn cứ nguồn gốc:** **PROPOSED** (NEEDS CONFIRMATION).

#### 17. Bảng `system_configs`
- **Mục đích (tiếng Việt):** Lưu trữ các tham số cấu hình vận hành có thể thay đổi động qua giao diện quản trị mà không cần build lại mã nguồn.
- **Module / Thành viên sử dụng:** Quản trị hệ thống / Phụng phụ trách.
- **Lý do cần lưu trong Database:** Cho phép Admin tùy chỉnh thời gian ân hạn, thời gian chờ điều phối, ngưỡng tin cậy OCR trực tiếp trên giao diện.
- **Điểm cần xác nhận:** Trong Sprint 3, các tham số này có cần lưu động trong CSDL không, hay có thể cấu hình tĩnh trong file `appsettings.json` của ASP.NET Core để đơn giản hóa hệ thống?
- **Mức độ ưu tiên:** **CONDITIONAL** (Cần xác nhận trước khi triển khai).
- **Căn cứ nguồn gốc:** **PROPOSED** (NEEDS CONFIRMATION).

---

## 4. CHI TIẾT DANH MỤC BẢNG DỰ PHÒNG TƯƠNG LAI (FUTURE - 2 BẢNG)

#### 18. Bảng `parking_reservations`
- **Mục đích (tiếng Việt):** Dành cho khách hàng đặt trước chỗ đỗ xe qua ứng dụng di động trước khi tới bãi.
- **Lý do xếp vào FUTURE:** Sprint 3 tập trung giải quyết bài toán vận hành cốt lõi tại bãi (xe vãng lai + xe đăng ký đến cổng, điều phối, đỗ xe và thanh toán). Việc giữ chỗ từ xa (Reservation Lifecycle, hủy cọc, phạt vắng mặt No-Show) làm phình to phạm vi nghiệp vụ và tăng rủi ro khóa slot ảo.
- **Mức độ ưu tiên:** **FUTURE** (Chưa triển khai trong Sprint 3).
- **Căn cứ nguồn gốc:** **PROPOSED** (Khuyến nghị xem xét tại Sprint 4).

#### 19. Bảng `subscriptions` (Vé tháng / Thuê bao định kỳ)
- **Mục đích (tiếng Việt):** Quản lý thuê bao gửi xe định kỳ hàng tháng cho cư dân hoặc nhân viên tòa nhà.
- **Lý do xếp vào FUTURE:** Nghiệp vụ vé tháng có chu kỳ thanh toán riêng (gia hạn định kỳ, miễn trừ tính phí từng lượt), không cấp thiết cho mục tiêu kiểm thử luồng tính phí thực tế theo lượt của Sprint 3.
- **Mức độ ưu tiên:** **FUTURE** (Chưa triển khai trong Sprint 3).
- **Căn cứ nguồn gốc:** **PROPOSED** (Khuyến nghị xem xét tại Sprint 4).

---

## 5. BẢNG TỔNG HỢP VÀ PHÂN ĐỊNH TRÁCH NHIỆM

| STT | Tên bảng đề xuất | Nhóm nghiệp vụ | Phân loại ưu tiên | Căn cứ nguồn gốc | Phụ trách chính |
| :---: | :--- | :--- | :---: | :---: | :--- |
| 1 | `users` | A. Accounts & RBAC | **CORE** | CONFIRMED & APPROVED | Phụng |
| 2 | `roles` | A. Accounts & RBAC | **CORE** | CONFIRMED & APPROVED | Phụng |
| 3 | `user_tokens` | A. Accounts & RBAC | **CORE** | CONFIRMED & APPROVED | Phụng |
| 4 | `refresh_tokens` | A. Accounts & RBAC | **CORE** | CONFIRMED & APPROVED | Phụng |
| 5 | `parking_lots` | B. Parking Structure | **CORE** | CONFIRMED | Phụng |
| 6 | `parking_floors` | B. Parking Structure | **CORE** | CONFIRMED | Phụng, Song |
| 7 | `parking_slots` | B. Parking Structure | **CORE** | CONFIRMED | Phụng, Thức, Song |
| 8 | `vehicles` | C. Vehicles & Sessions | **CORE** | CONFIRMED | Phụng |
| 9 | `parking_sessions` | C. Vehicles & Sessions | **CORE** *(Bảng trung tâm)* | CONFIRMED | Phụng (Toàn nhóm) |
| 10 | `parking_dispatches` | D. Parking Dispatch | **CORE** | CONFIRMED | Phụng, Song |
| 11 | `pricing_policies` | E. Pricing & Payments | **CORE** | CONFIRMED | Nam Anh, Phụng |
| 12 | `payments` | E. Pricing & Payments | **CORE** | CONFIRMED | Nam Anh |
| 13 | `gate_events` | F. Hardware & OCR | **CORE** | CONFIRMED | Đức, Thức, Phụng |
| 14 | `audit_logs` | G. Audit & Admin | **CORE** | CONFIRMED | Phụng |
| 15 | `slot_sensor_logs` | F. Hardware & IoT | **CONDITIONAL** | NEEDS CONFIRMATION | Thức, Phụng |
| 16 | `alerts` | G. Audit & Operations | **CONDITIONAL** | NEEDS CONFIRMATION | Phụng, Song |
| 17 | `system_configs` | G. Audit & Operations | **CONDITIONAL** | NEEDS CONFIRMATION | Phụng |
| 18 | `parking_reservations`| Mở rộng (Booking) | **FUTURE** | PROPOSED | Phụng, Song |
| 19 | `subscriptions` | Mở rộng (Vé tháng) | **FUTURE** | PROPOSED | Phụng, Nam Anh |
