# Quy tắc nghiệp vụ Smart Parking

**Trạng thái:** Nguồn quy tắc nghiệp vụ hiện hành.
**Chủ sở hữu:** BA/Product.
**Rà soát gần nhất:** 28/10/2026.
**Ưu tiên:** ADR trạng thái Accepted mới hơn có quyền bổ sung hoặc thay đổi quy tắc tương ứng.

**Mốc đối chiếu:** `Sprint3/demo` tại `4bed0c8a929fe050d644aa4e07c0a1caa77d8d0e`, ngày 28/10/2026. Xem [README](../README.md) để biết nguồn, quy ước và trạng thái.

Mỗi quy tắc có mã ổn định, nguồn, điểm cần thực thi, hiện trạng và cách đối chiếu. Đây là quy tắc sản phẩm; cột hiện trạng không được hiểu là mọi quy tắc đã được code bảo vệ. Mã `BR-xx SRS` được giữ như tham chiếu nguồn; mã BR theo miền bên dưới phục vụ bộ tài liệu này.

## Danh mục quy tắc

| Mã | Quy tắc | Hiện trạng ngắn |
|---|---|---|
| BR-ACC-01 | Đăng ký Customer có xác minh | YÊU CẦU |
| BR-ACC-02 | Điều kiện đăng nhập | HIỆN CÓ |
| BR-ACC-03 | Miền vai trò và phiên | HIỆN CÓ bốn role, refresh hash/thời hạn/revoke |
| BR-ACC-04 | Cấp nội bộ có kiểm soát | HIỆN CÓ API |
| BR-RBAC-01 | Phạm vi theo bãi và sở hữu | HIỆN CÓ trong API core |
| BR-RBAC-02 | Hai nhiệm vụ Staff | YÊU CẦU |
| BR-RBAC-03 | Dữ liệu công khai tối thiểu | MỘT PHẦN ở API core |
| BR-VEH-01 | Biển số nhất quán | HIỆN CÓ uppercase/trim, helper gom khoảng trắng và kiểm tra cơ bản 3–20 ký tự |
| BR-VEH-02 | Quyền liên kết và đổi xe | MỘT PHẦN |
| BR-VEH-03 | Nhiên liệu được xác nhận | MỘT PHẦN |
| BR-PARK-01 | Bốn trạng thái vật lý | HIỆN CÓ CHECK core |
| BR-PARK-02 | Sức chứa gồm xe chờ | MỘT PHẦN |
| BR-PARK-03 | Đóng nhận không xóa xe | MỘT PHẦN |
| BR-PARK-04 | Ngưỡng báo gần đầy | CẦN CHỐT giá trị/ranh giới |
| BR-SES-01 | Vãng lai không bắt buộc tài khoản | HIỆN CÓ API core không cần Customer |
| BR-SES-02 | Qua cổng chưa phải đã đỗ | MỘT PHẦN |
| BR-SES-03 | Không trùng phiên xe | HIỆN CÓ unique active_plate và kiểm tra dịch vụ |
| BR-SES-04 | Vé gắn phiên duy nhất | YÊU CẦU |
| BR-SES-05 | Đóng phiên có điều kiện | MỘT PHẦN |
| BR-DSP-01 | Ô phù hợp và cùng bãi | MỘT PHẦN |
| BR-DSP-02 | Tranh chấp ô xử lý ở máy chủ | MỘT PHẦN |
| BR-DSP-03 | Xác nhận ô phải truy vết | MỘT PHẦN |
| BR-DSP-04 | Sai ô và di chuyển giữ lịch sử | MỘT PHẦN |
| BR-BOOK-01 | Vãng lai đặt sức chứa | MỘT PHẦN |
| BR-BOOK-02 | Khung giờ và hạn giữ rõ | MỘT PHẦN |
| BR-BOOK-03 | Nhận booking một lần | MỘT PHẦN |
| BR-BOOK-04 | Hủy giữ không xóa xe | MỘT PHẦN |
| BR-PAY-01 | Phí lấy từ máy chủ | YÊU CẦU |
| BR-PAY-02 | Ghi tiền chống trùng | YÊU CẦU |
| BR-PAY-03 | Thu tiền không trả ô | YÊU CẦU cần ghép |
| BR-PAY-04 | Chứng từ và quyền xác nhận | YÊU CẦU |
| BR-PAY-05 | Hoàn tiền có giao dịch gốc | YÊU CẦU dài hạn, chưa có API/bảng core |
| BR-FIX-01 | Khu và ô cố định | YÊU CẦU Sprint 5 |
| BR-FIX-02 | Hai bố cục khu cố định | YÊU CẦU |
| BR-FIX-03 | Phát thẻ lần đầu có bàn giao | YÊU CẦU |
| BR-FIX-04 | Report không tự phạt | YÊU CẦU |
| BR-IMG-01 | AI hỗ trợ xác nhận | MỘT PHẦN |
| BR-IMG-02 | Ảnh theo quyền phiên bãi | MỘT PHẦN |
| BR-IMG-03 | Lỗi ảnh không tạo dữ liệu giả | MỘT PHẦN |
| BR-PRV-01 | Biển số trên bảng IoT và Web App | YÊU CẦU |
| BR-EVT-01 | EventId duy nhất bền | MỘT PHẦN |
| BR-EVT-02 | Nguồn thời điểm và phiên bản | MỘT PHẦN |
| BR-EVT-03 | Một nguồn nghiệp vụ | YÊU CẦU tích hợp |
| BR-3D-01 | Render từ xác nhận thực tế | MỘT PHẦN |
| BR-3D-02 | Đồng bộ và đường xem thay thế | YÊU CẦU ghép |
| BR-DATA-01 | Lưu bền và nguồn thật | MỘT PHẦN |
| BR-DATA-02 | Lịch sử và phục hồi | MỘT PHẦN |

## BR-ACC-01 Đăng ký Customer có xác minh

Đăng ký công khai chỉ cấp Customer; xác minh Gmail và reCAPTCHA phía máy chủ theo Sprint 3, token có hạn/dùng một lần và giới hạn gửi lại.

- **Nguồn:** Sprint 3; FR-IAM-01/03; DEC-01.
- **Điểm thực thi:** [APP] [ROUTE] [DB].
- **Hiện trạng:** YÊU CẦU; code đăng ký/email/reCAPTCHA đã bị gỡ, user_tokens còn trong schema.
- **Đối chiếu:** Email trùng/mã sai/hết hạn/dùng lại không kích hoạt tài khoản; gửi lỗi không báo đã xác minh.

## BR-ACC-02 Điều kiện đăng nhập

Chỉ tài khoản mật khẩu đúng, active, verified, APPROVED và không bị lockout nhận token; không tiết lộ trạng thái trước khi kiểm tra mật khẩu.

- **Nguồn:** FR-IAM-02; AuthService.
- **Điểm thực thi:** [APP] [ROUTE].
- **Hiện trạng:** HIỆN CÓ; có khóa theo cấu hình và rate limit login.
- **Đối chiếu:** Thử sai mật khẩu, bị khóa, chưa verified, chưa approved; không nhận token khi bị chặn.

## BR-ACC-03 Miền vai trò và phiên

Customer ánh xạ USER; Guest không có role. Logout thu hồi refresh token được gửi; không mặc định mọi access JWT đã hết hiệu lực ngay.

- **Nguồn:** BR-01 SRS; Roles; AuthService.
- **Điểm thực thi:** [APP] [ROUTE] [DB].
- **Hiện trạng:** HIỆN CÓ bốn role, refresh hash/thời hạn/revoke; không xoay refresh ở phiên bản này.
- **Đối chiếu:** Logout rồi refresh bằng token đó bị chặn; Guest không cần tạo role trong DB.

## BR-ACC-04 Cấp nội bộ có kiểm soát

Không tự đăng ký thành Admin; tạo nội bộ và đổi role phải theo Admin, bảo vệ Admin hoạt động cuối và lưu lịch sử.

- **Nguồn:** FR-IAM-05/06; AdminUserService.
- **Điểm thực thi:** [ROUTE] [APP] [DB].
- **Hiện trạng:** HIỆN CÓ API; Create chỉ STAFF/MANAGER, form quản trị chưa đủ.
- **Đối chiếu:** Thử tạo ADMIN qua Create, tự hạ quyền hoặc khóa Admin cuối; thao tác không hợp lệ bị chặn.

## BR-RBAC-01 Phạm vi theo bãi và sở hữu

Mọi yêu cầu nội bộ kiểm tra bãi được giao; Customer chỉ dữ liệu mình; thay đổi quyền có hiệu lực tại máy chủ.

- **Nguồn:** FR-IAM-06; NFR-SEC-01.
- **Điểm thực thi:** [ROUTE] [APP].
- **Hiện trạng:** HIỆN CÓ trong API core; không áp thành chứng nhận mọi route mô phỏng đã được bảo vệ.
- **Đối chiếu:** Gọi ID bãi/xe/phiên của người khác và token có quyền vừa bị thu hồi.

## BR-RBAC-02 Hai nhiệm vụ Staff

Cổng và điều phối là quyền riêng theo bãi; không tự có cả hai vì role STAFF. Manager chỉ có quyền nâng cao khi được cấp.

- **Nguồn:** Sprint 3; DEC-14.
- **Điểm thực thi:** [DB] [APP] [ROUTE] [UI].
- **Hiện trạng:** YÊU CẦU; phân công hiện chưa có nhiệm vụ, dùng chung hàm vận hành.
- **Đối chiếu:** Staff điều phối không check-out/ghi thu; Staff cổng không tự xác nhận xe tại ô.

## BR-RBAC-03 Dữ liệu công khai tối thiểu

Public không trả biển số/ảnh/chủ xe/phiên riêng/chi tiết tiền của người khác; xem 3D không mở thêm quyền.

- **Nguồn:** FR-MAP-04; NFR-PRV-01; Sprint 3.
- **Điểm thực thi:** [APP] [ROUTE] [UI].
- **Hiện trạng:** MỘT PHẦN ở API core; viewer/events hiện có trường nội bộ và route public cần sửa.
- **Đối chiếu:** Dùng cửa sổ không đăng nhập và kiểm tra cả response JSON, không chỉ nhãn giao diện.

## BR-VEH-01 Biển số nhất quán

Chuẩn hóa cùng quy tắc trước so sánh, unique và tra cứu; không xem kiểm tra độ dài là đã xác minh xe thật.

- **Nguồn:** FR-VEH-01/02; DEC-02.
- **Điểm thực thi:** [APP] [DB].
- **Hiện trạng:** HIỆN CÓ uppercase/trim, helper gom khoảng trắng và kiểm tra cơ bản 3–20 ký tự; chưa tự đồng nhất dấu chấm/gạch.
- **Đối chiếu:** Biến thể hoa/thường và khoảng trắng; giữ kết quả gốc OCR để đối chiếu.

## BR-VEH-02 Quyền liên kết và đổi xe

Không nhận quyền sở hữu chỉ vì nhập đúng biển số; dữ liệu xe đang có lượt/quyền cố định cần quy trình thay đổi có xác minh.

- **Nguồn:** Nghiệp vụ khu cố định; FR-VEH-02; DEC-02.
- **Điểm thực thi:** [APP] [DB].
- **Hiện trạng:** MỘT PHẦN; code chặn tự nhận xe vãng lai đã có; quản lý quyền gói/đổi xe chưa có.
- **Đối chiếu:** Tài khoản khác không chiếm xe; đổi xe đang có quyền cố định không âm thầm làm mất liên kết.

## BR-VEH-03 Nhiên liệu được xác nhận

Loại ô tô và nhiên liệu xăng/điện là hai miền khác nhau; AI chỉ gợi ý, Staff điều phối tại slot xác nhận; kiểm tra tương thích theo cấu hình.

- **Nguồn:** Sprint 3.
- **Điểm thực thi:** [APP] [DB] [UI].
- **Hiện trạng:** MỘT PHẦN; fuel chỉ ở event/Gemini, core chưa có; checker hiện luôn tương thích.
- **Đối chiếu:** Xe không tương thích bị chặn/cần xử lý theo chính sách; unknown không tự thành electric.

## BR-PARK-01 Bốn trạng thái vật lý

Mỗi slot có đúng một trạng thái EMPTY/OCCUPIED/MAINTENANCE/UNAVAILABLE; giữ/đặt/phân ô là lớp riêng.

- **Nguồn:** BR-03 SRS; FR-3D-02; Sprint 3.
- **Điểm thực thi:** [DB] [APP] [UI].
- **Hiện trạng:** HIỆN CÓ CHECK core; viewer còn RESERVED như trạng thái thứ năm.
- **Đối chiếu:** Tổng bốn trạng thái bằng tổng ô trong cùng tập lọc; không cộng giữ như một ô mới.

## BR-PARK-02 Sức chứa gồm xe chờ

Xe đã vào nhưng chưa đỗ vẫn tiêu thụ khả năng nhận; loại ô bảo trì/không khả dụng và xét quyền giữ để không nhận vượt.

- **Nguồn:** BR-05 SRS; Sprint 3.
- **Điểm thực thi:** [APP] [DB].
- **Hiện trạng:** MỘT PHẦN; helper max(active sessions, occupied) + PENDING bookings, chưa xét thời gian/gói.
- **Đối chiếu:** Nhiều xe check-in chưa gán ô vẫn làm giảm khả dụng; đồng thời không nhận vượt.

## BR-PARK-03 Đóng nhận không xóa xe

Đóng bãi/bảo trì không xóa phiên hoặc quyền lợi; vẫn cho xử lý xe đã ở trong bãi theo đúng quyền.

- **Nguồn:** FR-PARK-03/04; DEC-03.
- **Điểm thực thi:** [APP] [DB].
- **Hiện trạng:** MỘT PHẦN; core chặn đổi slot OCCUPIED khi còn phiên và chặn bảo trì ô đang ASSIGNED; tác động đặt/gói cần hoàn thiện.
- **Đối chiếu:** Đóng nhận chặn lượt mới nhưng không làm biến mất xe/phiên; ô có xe không sửa màu để trả chỗ.

## BR-PARK-04 Ngưỡng báo gần đầy

Khoảng 5 ô trong nghiệp vụ là yêu cầu ngưỡng cần cấu hình và xác nhận cho bãi; dùng khả dụng nghiệp vụ, không chỉ số màu xanh.

- **Nguồn:** Nghiệp vụ vãng lai; DEC-06.
- **Điểm thực thi:** [APP] [DB] [UI].
- **Hiện trạng:** CẦN CHỐT giá trị/ranh giới; core chưa có cấu hình ngưỡng này.
- **Đối chiếu:** Thử đúng trước/tại/sau ngưỡng với cả xe chờ và giữ; bảng vào bãi và Staff cùng kết quả.

## BR-SES-01 Vãng lai không bắt buộc tài khoản

Nhận xe thực tế không yêu cầu Customer ID. Cổng chụp ảnh, AI/OCR tạo kết quả nhận dạng tạm và hệ thống tạo một phiên truy vết được; Staff điều phối tại ô xác minh/sửa thông tin xe trước khi xác nhận OCCUPIED.

- **Nguồn:** FR-SES-01; Sprint 3.
- **Điểm thực thi:** [APP] [DB] [UI].
- **Hiện trạng:** HIỆN CÓ API core không cần Customer; UI Staff/vé chưa đủ.
- **Đối chiếu:** Nhận một xe chưa có tài khoản, tra lại đúng phiên bằng vé/thẻ/sự kiện vào; OCR sai được sửa tại ô mà không tạo phiên thứ hai.

## BR-SES-02 Qua cổng chưa phải đã đỗ

Check-in tự động chỉ ghi xe ở trong bãi/chờ xác minh; biển số AI/OCR là dữ liệu tạm. Sensor/ảnh/gán mục tiêu chưa chứng minh xe đúng ô hoặc biển số đã chính xác.

- **Nguồn:** Sprint 3; nghiệp vụ vãng lai.
- **Điểm thực thi:** [APP] [DB] [UI].
- **Hiện trạng:** MỘT PHẦN; check-in không đổi physical, nhưng CurrentSlotId có thể được gán trước và viewer riêng.
- **Đối chiếu:** Check-in/phân ô xong slot chưa OCCUPIED; Staff tại ô xác minh/sửa xe và biển số, xe chỉ hiện tại ô sau xác nhận thực tế.

## BR-SES-03 Không trùng phiên xe

Một xe không có hai phiên ACTIVE/PAID; mọi retry và request đồng thời phải giữ bất biến.

- **Nguồn:** BR-06 SRS; SessionConfiguration.
- **Điểm thực thi:** [DB] [APP].
- **Hiện trạng:** HIỆN CÓ unique active_plate và kiểm tra dịch vụ; concurrency cần thử PostgreSQL.
- **Đối chiếu:** Gửi đồng thời cùng biển số ở cùng/khác bãi; không có hai phiên mở.

## BR-SES-04 Vé gắn phiên duy nhất

Một vé/thẻ vãng lai đang dùng chỉ gắn một phiên; cấp/thu hồi/mất/thay thế có lịch sử và đối chiếu xe.

- **Nguồn:** FR-TKT-01/02/03/04; DEC-12.
- **Điểm thực thi:** [DB] [APP] [ROUTE].
- **Hiện trạng:** YÊU CẦU; core chưa có bảng/API vé/thẻ.
- **Đối chiếu:** Thử cấp cùng mã cho hai xe, mất vé và nhập nhầm mã; không tự trả xe.

## BR-SES-05 Đóng phiên có điều kiện

Khi xe tự tới cổng ra và quẹt vé/thẻ, hệ thống phải tải đúng phiên, hiển thị thông tin lúc vào và tính phí máy chủ. Chỉ sau khi đối chiếu xe/vé, thu đủ hoặc duyệt ngoại lệ và xác nhận xe thực sự ra mới đóng/trả ô theo chính sách.

- **Nguồn:** BR-13 SRS; FR-SES-05; Sprint 3.
- **Điểm thực thi:** [APP] [DB] [ROUTE].
- **Hiện trạng:** MỘT PHẦN; check-out hiện đóng/giải phóng nhưng thiếu guard tiền/vé và nhiệm vụ.
- **Đối chiếu:** Chưa thu đủ hoặc sai xe/vé không hoàn tất; gửi lặp không tạo lần ra mới.

## BR-DSP-01 Ô phù hợp và cùng bãi

Ô mục tiêu/thực tế thuộc đúng bãi, khai thác được, không xung đột và tương thích xe; không nhận ô bảo trì/không khả dụng.

- **Nguồn:** Sprint 3; ParkingDispatchService.
- **Điểm thực thi:** [APP] [DB].
- **Hiện trạng:** MỘT PHẦN; có guard bãi/status/assignment, chưa có fuel core; cần rà cả floor active.
- **Đối chiếu:** Thử ô khác bãi, tầng ngừng dùng, ô đã có xe và ô không tương thích.

## BR-DSP-02 Tranh chấp ô xử lý ở máy chủ

Hai người không phân/xác nhận hai xe cùng một ô; dùng transaction và constraint/lock phù hợp cho cả assigned và actual.

- **Nguồn:** BR-06 SRS; NFR-REL-01.
- **Điểm thực thi:** [APP] [DB].
- **Hiện trạng:** MỘT PHẦN; unique ASSIGNED tồn tại, không có unique active current_slot; chưa chứng minh mọi actual-slot race.
- **Đối chiếu:** Dùng hai kết nối PostgreSQL cùng xác nhận actual slot; chỉ một xe chiếm thành công.

## BR-DSP-03 Xác nhận ô phải truy vết

Staff điều phối xác minh xe thực tế; lưu actor, thời điểm, ảnh/tham chiếu và kết quả, physical mới thành Có xe.

- **Nguồn:** Sprint 3.
- **Điểm thực thi:** [APP] [DB] [UI].
- **Hiện trạng:** MỘT PHẦN; confirm và confirmed_at/audit có, duty và liên kết ảnh chưa hoàn chỉnh.
- **Đối chiếu:** So sánh xe tại hiện trường với phiên và hình 3D; kiểm tra ai xác nhận.

## BR-DSP-04 Sai ô và di chuyển giữ lịch sử

Sai ô/đổi ô cần lý do; chỉ trả ô cũ khi có bằng chứng xe rời, không ghi đè lịch sử để che sai lệch.

- **Nguồn:** Sprint 3; FR-SES-06.
- **Điểm thực thi:** [APP] [DB] [UI].
- **Hiện trạng:** MỘT PHẦN; wrong-slot chỉ từ ASSIGNED; reassign chỉ PENDING/ASSIGNED, chưa hỗ trợ di chuyển xe đã đỗ đầy đủ.
- **Đối chiếu:** Xe OCCUPIED cần đổi ô không được dùng endpoint reassign như thể có sẵn; kiểm tra quy trình bổ sung.

## BR-BOOK-01 Vãng lai đặt sức chứa

Customer đặt trước khả năng tiếp nhận ở bãi/khu vãng lai, không tự chiếm một ô vật lý cụ thể; xem/chọn trên 3D không tạo giữ.

- **Nguồn:** Nghiệp vụ vãng lai; DEC-06.
- **Điểm thực thi:** [APP] [DB] [UI].
- **Hiện trạng:** MỘT PHẦN; core booking không có SlotId, web chưa nối.
- **Đối chiếu:** Hai thao tác xem không giảm chỗ; đặt chưa làm xuất hiện xe hoặc gắn ô cố định.

## BR-BOOK-02 Khung giờ và hạn giữ rõ

Kiểm tra thời gian đầu/cuối, thời hạn giữ, giao khung giờ và điều kiện xác nhận theo cấu hình được duyệt.

- **Nguồn:** BR-04 SRS; FR-BOOK-01/02/03; DEC-07/08.
- **Điểm thực thi:** [APP] [DB].
- **Hiện trạng:** MỘT PHẦN; có start/end validation cơ bản, chưa có hold expiry/payment hoặc availability theo khung giờ.
- **Đối chiếu:** Kiểm tra ranh giới hết hạn, đặt tương lai và kết quả thanh toán muộn; không tự hardcode 15 phút.

## BR-BOOK-03 Nhận booking một lần

Nhận đúng bãi/xe/khung giờ và điều kiện; liên kết một phiên duy nhất, không trừ công suất hai lần.

- **Nguồn:** BR-07 SRS; FR-BOOK-07.
- **Điểm thực thi:** [APP] [DB].
- **Hiện trạng:** MỘT PHẦN; check-in chuyển PENDING theo bãi/biển số thành CHECKED_IN; điều kiện giờ/tiền chưa đủ.
- **Đối chiếu:** Dùng lại mã/biển số đã nhận không sinh phiên thứ hai; giờ đến không hợp lệ phải xử lý rõ.

## BR-BOOK-04 Hủy giữ không xóa xe

Hủy/hết hạn chỉ bỏ phần giữ/quyền đặt của yêu cầu đó; khoản tiền đối soát riêng; xe đang đỗ giữ nguyên phiên.

- **Nguồn:** BR-08 SRS; FR-BOOK-06.
- **Điểm thực thi:** [APP] [DB].
- **Hiện trạng:** MỘT PHẦN; cancel PENDING có, EXPIRED mới có miền chưa thấy tác vụ tự chạy.
- **Đối chiếu:** Hủy sau khi đã nhận bị chặn; hết hạn không làm ô Có xe thành Trống.

## BR-PAY-01 Phí lấy từ máy chủ

Lưu giá/phiên bản/giờ và quy tắc làm tròn được xác nhận; không tin số tiền UI; phân biệt dự kiến, cuối cùng, đã thu và còn thiếu.

- **Nguồn:** BR-10/11 SRS; FR-PAY-01; DEC-04.
- **Điểm thực thi:** [APP] [DB].
- **Hiện trạng:** YÊU CẦU; TotalFee/PaidAt hiện chỉ là trường, chưa có dịch vụ phí/khoản thu đầy đủ.
- **Đối chiếu:** Sửa amount trên request không làm thay phí; kiểm tra giá đổi hiệu lực và ranh giới giờ.

## BR-PAY-02 Ghi tiền chống trùng

Một giao dịch gốc/mã yêu cầu có một kết quả hợp lệ; retry cùng thao tác không ghi thêm khoản thu hoặc cộng kỳ.

- **Nguồn:** BR-12 SRS; NFR-REL-01.
- **Điểm thực thi:** [DB] [APP].
- **Hiện trạng:** YÊU CẦU; chưa có payments/idempotency key trong core.
- **Đối chiếu:** Timeout rồi gửi lại và callback lặp chỉ có một khoản thu.

## BR-PAY-03 Thu tiền không trả ô

Thành công thanh toán không mở phiên, không tạo xe hoặc tự chuyển ô Trống; xe ra được xác nhận riêng.

- **Nguồn:** BR-09/13 SRS; Sprint 3.
- **Điểm thực thi:** [APP] [DB] [UI].
- **Hiện trạng:** YÊU CẦU cần ghép; không có luồng phí hoàn chỉnh hiện tại.
- **Đối chiếu:** Thu đủ nhưng giữ xe trong ô: web/3D vẫn có xe, phiên chưa kết thúc.

## BR-PAY-04 Chứng từ và quyền xác nhận

Khoản trực tiếp lưu người thu, thời điểm, phương thức và bằng chứng; vãng lai thuộc Staff cổng đủ quyền, tiền gói theo quy trình Staff báo/Manager xác nhận được chốt.

- **Nguồn:** Nghiệp vụ khu cố định; Sprint 3; DEC-13/14.
- **Điểm thực thi:** [ROUTE] [APP] [DB].
- **Hiện trạng:** YÊU CẦU; không tự suy ra cùng quyền cho mọi khoản tiền.
- **Đối chiếu:** Điều phối không ghi thu; Staff ghi chứng từ tiền gói không tự thành người duyệt khi chưa được cấp.

## BR-PAY-05 Hoàn tiền có giao dịch gốc

Hoàn không vượt phần có thể hoàn của khoản hợp lệ; lưu lý do/người duyệt/kết quả riêng, không xóa khoản gốc.

- **Nguồn:** BR-15 SRS; FR-PAY-07.
- **Điểm thực thi:** [DB] [APP] [ROUTE].
- **Hiện trạng:** YÊU CẦU dài hạn, chưa có API/bảng core.
- **Đối chiếu:** Hoàn lặp hoặc vượt tiền bị chặn; báo cáo vẫn truy vết thu và hoàn.

## BR-FIX-01 Khu và ô cố định

Đăng ký gửi cố định gắn tài khoản/xe/quyền ô riêng theo kỳ; barrier và bảng IoT dùng quyền đã xác nhận. Bảng IoT vật lý tại mỗi slot cố định hiển thị đầy đủ biển số đã đăng ký còn hiệu lực của slot đó.

- **Nguồn:** Nghiệp vụ khu cố định; DEC-09.
- **Điểm thực thi:** [DB] [APP] [UI].
- **Hiện trạng:** YÊU CẦU Sprint 5; chưa có schema/API; giá và kỳ chưa chốt chi tiết.
- **Đối chiếu:** Mua quyền không tự tạo xe; xe/thẻ sai không mở barrier hoặc thay biển số bảng ô; đổi xe chỉ đổi biển số full sau khi yêu cầu được xác minh và có hiệu lực.

## BR-FIX-02 Hai bố cục khu cố định

Thể hiện khu cố định trong bãi gốc và khu riêng bên ngoài; quản lý đúng parent/khu và quyền qua barrier.

- **Nguồn:** Nghiệp vụ khu cố định; SP-DEC-19.
- **Điểm thực thi:** [DB] [APP] [UI].
- **Hiện trạng:** YÊU CẦU; mô hình hiện mới bãi/tầng/ô, viewer mẫu chưa chứng minh hai quan hệ này.
- **Đối chiếu:** Đổi góc nhìn không đổi parent/bãi; mã ô và vùng quyền khớp dữ liệu.

## BR-FIX-03 Phát thẻ lần đầu có bàn giao

Khách nhận thẻ ở lần đến đầu; hệ thống đưa thông tin cần thiết cho nhân viên và lưu cấp/nhận, không tự coi đã giao.

- **Nguồn:** Nghiệp vụ khu cố định; DEC-12.
- **Điểm thực thi:** [APP] [DB] [UI].
- **Hiện trạng:** YÊU CẦU; chưa có dữ liệu cấp thẻ/thông báo.
- **Đối chiếu:** Đăng ký trước chưa có bản ghi đã phát thẻ; nhân viên phát một lần có xác nhận.

## BR-FIX-04 Report không tự phạt

Chủ quyền ô gửi report có hình ảnh; nhân viên/Manager nhận và xác minh; xử phạt cần quyền, chính sách và kênh thu đã chốt.

- **Nguồn:** Nghiệp vụ chiếm ô; SP-DEC-18.
- **Điểm thực thi:** [ROUTE] [APP] [DB].
- **Hiện trạng:** YÊU CẦU; quyền báo cáo/bằng chứng có định hướng, mức phạt và API chưa có.
- **Đối chiếu:** Người không có quyền ô không báo thay; gửi report không tự sinh khoản phạt chắc chắn.

## BR-IMG-01 AI hỗ trợ xác nhận

Lưu ảnh/kết quả gốc, độ tin cậy nếu có và giá trị Staff xác nhận/sửa. AI/OCR được phép cung cấp biển số tạm để tạo session `ACTIVE` chờ xác minh, nhưng không tự chứng minh xe đã đỗ, không tự chuyển slot `OCCUPIED` và không tự cho xe ra. Staff điều phối xác nhận/sửa tại slot.

- **Nguồn:** Sprint 3; FR-SES-02.
- **Điểm thực thi:** [APP] [DB] [UI].
- **Hiện trạng:** MỘT PHẦN; OCR/Gemini có module riêng, liên kết/xác nhận chưa hoàn chỉnh.
- **Đối chiếu:** Ảnh mờ giữ trạng thái cần xác minh hoặc chuyển luồng xử lý lỗi; sửa tại ô có actor/time/lý do, không lấy biển số của phiên trước báo thành công và không tạo thêm session.

## BR-PRV-01 Biển số trên bảng IoT và Web App

Bảng IoT vật lý tại slot cố định hiển thị đầy đủ biển số đã đăng ký theo quyền slot. Web/API không dùng cùng projection: Guest không xem biển số; Customer chỉ xem xe/phiên/quyền của mình; Staff/Manager/Admin xem theo bãi, nhiệm vụ và quyền được cấp.

- **Nguồn:** Nghiệp vụ khu cố định; quyết định cập nhật ngày 28/10/2026.
- **Điểm thực thi:** [ROUTE] [APP] [DB] [UI] [DEVICE].
- **Hiện trạng:** YÊU CẦU; chưa có bảng IoT/quyền slot core và các route viewer hiện còn public quá mức.
- **Đối chiếu:** Bảng đúng slot hiển thị full biển số hiệu lực; API public không trả biển số; gọi Web API sai actor/bãi bị chặn ở máy chủ.

## BR-IMG-02 Ảnh theo quyền phiên bãi

Quyền ảnh cần cho cộng tác Staff đúng phiên/bãi, public không có ảnh nhạy cảm; thời hạn lưu/xóa theo chính sách.

- **Nguồn:** NFR-PRV-01; Sprint 3; DEC-17.
- **Điểm thực thi:** [ROUTE] [APP] [DB].
- **Hiện trạng:** MỘT PHẦN; stored_images hiện theo uploader/Admin, không FK phiên/bãi.
- **Đối chiếu:** Staff khác bãi không xem; Staff đúng nhiệm vụ cần xem ảnh hợp lệ của cùng phiên.

## BR-IMG-03 Lỗi ảnh không tạo dữ liệu giả

Kiểm tra file/kích thước/định dạng; khi dịch vụ lỗi báo thật, cho Staff xác minh/nhập tay trong quyền.

- **Nguồn:** Sprint 3; NFR-REL-03.
- **Điểm thực thi:** [APP] [UI].
- **Hiện trạng:** MỘT PHẦN; upload .NET có giới hạn, FastAPI không tự thừa hưởng guard này.
- **Đối chiếu:** File sai hoặc timeout không tạo ảnh/biển số giả, không báo check-in đã thành công.

## BR-EVT-01 EventId duy nhất bền

Retry cùng sự kiện giữ EventId, thao tác mới có ID mới; chống lặp phải giữ sau restart và đồng thời.

- **Nguồn:** Sprint 3; NFR-REL-01/02.
- **Điểm thực thi:** [DB] [APP].
- **Hiện trạng:** MỘT PHẦN; pipeline hiện chống lặp trong bộ nhớ, chưa bền.
- **Đối chiếu:** Gửi cùng ID nhiều lần trước/sau restart: một tác động nghiệp vụ, log truy vết rõ.

## BR-EVT-02 Nguồn thời điểm và phiên bản

Ghi nguồn cổng/slot, danh tính đáng tin, thời điểm xảy ra/nhận và version; sự kiện cũ không ghi đè trạng thái mới mù quáng.

- **Nguồn:** Sprint 3; DEC-16.
- **Điểm thực thi:** [APP] [DB] [ROUTE].
- **Hiện trạng:** MỘT PHẦN; có source/time/TriggeredBy payload, thiếu xác thực/đồng bộ bền hoàn chỉnh.
- **Đối chiếu:** Giả TriggeredBy hoặc sự kiện đảo thứ tự không vượt quyền; có kết quả xác minh/reject.

## BR-EVT-03 Một nguồn nghiệp vụ

Sự kiện đi qua service cùng quyền/guard, không cập nhật riêng bộ đếm/slot hoặc giữ hai phiên quyết định song song.

- **Nguồn:** Sprint 3.
- **Điểm thực thi:** [APP] [DB].
- **Hiện trạng:** YÊU CẦU tích hợp; core PostgreSQL và event InMemory hiện tách, bridge NoOp.
- **Đối chiếu:** Cùng mã phiên/ô khớp ở API nghiệp vụ, event log, danh sách và 3D.

## BR-3D-01 Render từ xác nhận thực tế

Bốn màu có nhãn; xe chờ tách riêng; chỉ render xe tại ô sau xác nhận, có lớp phân/giữ riêng và nhãn nhiên liệu phù hợp.

- **Nguồn:** FR-3D-01/02/03/04; Sprint 3.
- **Điểm thực thi:** [UI] [APP].
- **Hiện trạng:** MỘT PHẦN; viewer độc lập còn dữ liệu tự sinh và RESERVED physical.
- **Đối chiếu:** Bấm ô/zoom hoặc thanh toán không sinh xe; mã/số/vị trí ô khớp DB.

## BR-3D-02 Đồng bộ và đường xem thay thế

PC/mobile/3D đọc cùng kết quả đã lưu, hiển thị cập nhật cuối; mất mạng giữ trạng thái đã xác nhận, nối lại tải snapshot; 3D lỗi có danh sách.

- **Nguồn:** FR-3D-05/06; Sprint 3.
- **Điểm thực thi:** [UI] [APP].
- **Hiện trạng:** YÊU CẦU ghép; React chưa dùng viewer wrapper, snapshot chung chưa có.
- **Đối chiếu:** Hai thiết bị cùng phiên/ô sau reload; lỗi WebGL vẫn xem và thao tác đúng quyền qua danh sách.

## BR-DATA-01 Lưu bền và nguồn thật

Nghiệp vụ Sprint 3 dùng bãi/giá/ảnh/sự kiện có nguồn xác nhận, lưu DB; API/DB lỗi báo lỗi, rỗng trả rỗng; không fallback sang seed/simulator.

- **Nguồn:** Sprint 3; BR-17 SRS.
- **Điểm thực thi:** [APP] [DB] [UI].
- **Hiện trạng:** MỘT PHẦN; core PostgreSQL có, event/viewer/simulator chưa phù hợp làm nguồn vận hành.
- **Đối chiếu:** Ngắt DB và restart không tự có dữ liệu mẫu hoặc mất phiên đã ghi.

## BR-DATA-02 Lịch sử và phục hồi

Giữ liên kết phiên/xe/vé/giá/tiền/ảnh và người thao tác; sửa/hoàn không xóa dấu vết; thử restore DB riêng.

- **Nguồn:** BR-14 SRS; FR-AUD-01; DEC-17.
- **Điểm thực thi:** [APP] [DB].
- **Hiện trạng:** MỘT PHẦN; audit có, nhiều quan hệ tiền/vé còn thiếu; cascade cần rà retention.
- **Đối chiếu:** Phục hồi đối chiếu đúng mã/giờ/số tiền; không reset dữ liệu vận hành để diễn lại.

## Quy tắc ưu tiên khi có xung đột

Nghiệp vụ đã chốt và yêu cầu Sprint 3 được áp dụng theo [00 Master reference](overview.md). Không dùng hành vi thiếu của code để đổi quy tắc. Giá trị chưa chốt đi vào sổ DEC; không sao chép phí, tỷ lệ, thời gian giữ hoặc chuẩn tiền từ AITasker/prototype.

Một thay đổi liên quan trạng thái/tiền/quyền phải cập nhật [02 State machines](state-machines.md), [04 Endpoints](../api/endpoints.md), [05 Use cases](use-cases.md) và bằng chứng QA. Chức năng hiện có API nhưng chưa có UI/dịch vụ thật chỉ được ghi MỘT PHẦN.
