# Đặc tả use case Smart Parking

**Trạng thái:** Hiện hành về hành vi đích; từng use case ghi rõ mức triển khai.
**Chủ sở hữu:** BA/Product và QA.
**Rà soát gần nhất:** 28/10/2026.
**Phạm vi:** SP-UC-001 đến SP-UC-042.

**Mốc đối chiếu:** `Sprint3/demo` tại `4bed0c8a929fe050d644aa4e07c0a1caa77d8d0e`, ngày 28/10/2026. Xem [README](../README.md) để biết nguồn, quy ước và trạng thái.

Bộ này có **42 use case SP-UC-001…SP-UC-042**; đây là mã bổ sung của docs, không đổi mã UC-01… trong SRS. Một use case có thể dùng nhiều endpoint, và alias endpoint không được đếm thành use case mới. Quyền trong mô tả là quyền nghiệp vụ đích; dòng hiện trạng cho biết API hiện có khác ở đâu.

## Danh mục use case

| Mã | Tên | Tác nhân | Phân kỳ | Hiện trạng |
|---|---|---|---|---|
| SP-UC-001 | Đăng nhập và duy trì phiên | Người có tài khoản | Sprint 3 | HIỆN CÓ API và UI login; dịch vụ thật cần cấu hình |
| SP-UC-002 | Đăng xuất | Người đang có phiên | Sprint 3 | HIỆN CÓ API và UI |
| SP-UC-003 | Đăng ký Customer | Guest | Sprint 3 | YÊU CẦU; đã gỡ code/route hiện tại |
| SP-UC-004 | Xác minh và gửi lại email | Customer chờ xác minh | Sprint 3 | YÊU CẦU; chưa có endpoint hiện tại |
| SP-UC-005 | Xem và sửa hồ sơ | Người đăng nhập; UI Customer | Sprint 3 | HIỆN CÓ API và UI Customer |
| SP-UC-006 | Quản lý ô tô cá nhân | Customer; Admin theo quyền | Sprint 3 | HIỆN CÓ API; UI Customer có xem/thêm/sửa, chưa có mọi thao tác quản trị |
| SP-UC-007 | Guest tìm và xem bãi công khai | Guest hoặc mọi vai trò | Sprint 3 | MỘT PHẦN: API đọc có, UI Guest và bản đồ tìm kiếm chưa hoàn chỉnh |
| SP-UC-008 | Customer xem tầng và slot | Customer | Sprint 3 | HIỆN CÓ UI React và API đọc |
| SP-UC-009 | Khai báo bãi | Admin | Sprint 3 | HIỆN CÓ API, UI form chưa đủ |
| SP-UC-010 | Khai báo tầng slot và sơ đồ | Admin | Sprint 3 | HIỆN CÓ API; bố cục/fuel/area cần bổ sung và UI form chưa đủ |
| SP-UC-011 | Quản lý tài khoản nội bộ | Admin | Sprint 3 | HIỆN CÓ API; UI quản lý chưa đủ |
| SP-UC-012 | Cấp và thu hồi phân công bãi | Admin; người nội bộ xem bãi mình | Sprint 3 | HIỆN CÓ API, UI phần ghi chưa đủ |
| SP-UC-013 | Phân công hai nhiệm vụ Staff | Admin; Manager nếu được cấp | Sprint 3 | YÊU CẦU; chưa có field/guard nhiệm vụ |
| SP-UC-014 | Đóng mở nhận xe và bảo trì ô | Admin; Manager đúng bãi | Sprint 3 | HIỆN CÓ API; UI thao tác chưa đủ, ảnh hưởng quyền gói chưa có |
| SP-UC-015 | Nhận xe vãng lai tại cổng | Hệ thống camera/AI và cổng vào; Staff cổng xử lý ngoại lệ | Sprint 3 | MỘT PHẦN: API check-in có; nhận dạng tạm/vé/duty/UI chưa đủ |
| SP-UC-016 | Theo dõi xe chờ và hàng việc xác minh | Staff điều phối | Sprint 3 | MỘT PHẦN: đọc sessions/dispatch có, event queue riêng, UI và nguồn chung chưa đủ |
| SP-UC-017 | Phân hoặc đổi ô mục tiêu | Staff điều phối | Sprint 3 | MỘT PHẦN: API có, thiếu duty/fuel/UI |
| SP-UC-018 | Xác nhận xe đỗ đúng ô | Staff điều phối | Sprint 3 | MỘT PHẦN: confirm API có, thiếu duty/ảnh/fuel/UI tích hợp |
| SP-UC-019 | Ghi nhận xe đỗ sai ô mục tiêu | Staff điều phối | Sprint 3 | MỘT PHẦN: API có từ ASSIGNED, chưa có toàn quy trình xử lý sau đó |
| SP-UC-020 | Di chuyển xe đã xác nhận đỗ | Staff điều phối; Manager xử lý ngoại lệ trong quyền | Sprint 3 | YÊU CẦU bổ sung; reassign hiện không nhận OCCUPIED/WRONG_SLOT |
| SP-UC-021 | Quẹt vé/thẻ tại cổng ra và tra lượt gửi | Staff cổng; hệ thống cổng; Customer chỉ lịch sử mình | Sprint 3 | HIỆN CÓ API tra bằng ID/biển số; tra bằng vé/thẻ và UI vận hành chưa đủ |
| SP-UC-022 | Tính và tra phí theo phiên | Staff cổng đủ quyền | Sprint 3 | YÊU CẦU; chưa có API phí đầy đủ |
| SP-UC-023 | Ghi nhận thu tiền và biên lai | Staff cổng đủ quyền; tiền gói theo quy trình riêng | Sprint 3 | YÊU CẦU; chưa có API/bảng khoản thu core |
| SP-UC-024 | Xác nhận xe ra và đóng phiên | Staff cổng | Sprint 3 | MỘT PHẦN: check-out có, thiếu tiền/vé/duty và UI đầy đủ |
| SP-UC-025 | Lưu và xem ảnh xe | Staff theo bãi/phiên; hiện uploader/Admin | Sprint 3 | MỘT PHẦN: upload/metadata/delete có, liên kết và quyền bãi chưa đủ |
| SP-UC-026 | AI nhận diện ở cổng và Staff xác minh tại slot | Hệ thống camera/AI; Staff điều phối | Sprint 3 | MỘT PHẦN: AI/Gemini độc lập có; trạng thái tạm và xác minh tại slot chưa đủ |
| SP-UC-027 | Tiếp nhận sự kiện cổng và slot | Thiết bị/người thao tác được xác thực | Sprint 3 | MỘT PHẦN: pipeline bộ nhớ có, chưa lưu/chung core/guard đầy đủ |
| SP-UC-028 | Xem bãi 3D và danh sách thay thế | Guest/Customer hoặc nội bộ trong quyền | Sprint 3 | MỘT PHẦN: viewer riêng mô phỏng, chưa ghép React/DB chung |
| SP-UC-029 | Mất mạng và đồng bộ lại | Mọi vai trò đang sử dụng | Sprint 3 | YÊU CẦU ghép; snapshot/version chung chưa có |
| SP-UC-030 | Tạo đặt trước khả năng tiếp nhận | Customer | Sprint 4; API có sớm | MỘT PHẦN: API booking có, UI/giữ thời gian/tiền chưa đủ |
| SP-UC-031 | Hủy hoặc hết hạn đặt chỗ | Customer; nội bộ trong quyền; hệ thống hết hạn | Sprint 4 | MỘT PHẦN: cancel PENDING có; tự hết hạn/tiền chưa có |
| SP-UC-032 | Nhận xe đã đặt trước | Staff cổng và Customer | Sprint 4 | MỘT PHẦN: check-in nhận PENDING theo bãi/biển số, thiếu khung giờ/tiền/mã vé đầy đủ |
| SP-UC-033 | Đăng ký quyền gửi cố định | Customer | Sprint 5 | YÊU CẦU; chưa có schema/API |
| SP-UC-034 | Phát thẻ khu cố định lần đầu | Staff và Customer đã đăng ký | Sprint 5 | YÊU CẦU; chưa có dữ liệu cấp thẻ |
| SP-UC-035 | Vào ra khu cố định bằng thẻ | Customer và Staff cổng/khu | Sprint 5 | YÊU CẦU; chưa có bridge thẻ/barrier/gói |
| SP-UC-036 | Gia hạn hoặc thay đổi xe của quyền cố định | Customer và người duyệt theo quyền | Sprint 5 | YÊU CẦU; chưa có API/schema |
| SP-UC-037 | Báo ô cố định bị chiếm | Customer có quyền ô | Phần khu cố định, chi tiết SP-DEC-18 | YÊU CẦU; chưa có API/schema |
| SP-UC-038 | Xác minh và xử lý report chiếm ô | Staff/Manager trong quyền; người duyệt phạt theo chính sách | Phần khu cố định | YÊU CẦU; chưa có API/schema |
| SP-UC-039 | Yêu cầu và xác nhận hoàn tiền | Customer yêu cầu; người có quyền duyệt | SRS dài hạn, cần chốt | YÊU CẦU; chưa có API/schema |
| SP-UC-040 | Xem tổng quan và đối soát lịch sử | Admin, Manager đúng bãi; Customer lịch sử mình | Sprint 3 phần tổng quan, SRS phần báo cáo | MỘT PHẦN: dashboard/count/phiên có, báo cáo tài chính và audit API chưa đầy đủ |
| SP-UC-041 | Khởi động lại sao lưu và phục hồi | Trí/người vận hành và QA được giao | Sprint 3 | MỘT PHẦN: core lưu DB có; event/viewer bộ nhớ chưa bền và chưa có bằng chứng phục hồi thật |
| SP-UC-042 | Thử simulator tách nguồn vận hành | Người kiểm thử được cấp | Công cụ kiểm thử, không thay nguồn thật Sprint 3 | MÔ PHỎNG; API hiện còn public cần giới hạn trước triển khai |

## SP-UC-001 Đăng nhập và duy trì phiên

- **Tác nhân:** Người có tài khoản.
- **Phân kỳ:** Sprint 3.
- **Hiện trạng:** HIỆN CÓ API và UI login; dịch vụ thật cần cấu hình.
- **API:** SP-API-006 `POST /api/v1/auth/login`; SP-API-007 `POST /api/v1/auth/refresh`. Tra quyền/DTO ở [04 Endpoints](../api/endpoints.md).
- **Dữ liệu:** users, roles, refresh_tokens.
- **Tiền điều kiện:** Tài khoản được cấp, có mật khẩu và đủ điều kiện.
- **Nguồn yêu cầu:** FR-IAM-02.
- **Quy tắc:** BR-ACC-02,BR-ACC-03; xem [03](business-rules.md).

**Luồng chính**

1. Nhập username/password.
2. Máy chủ xác minh mật khẩu, lockout, active, verified và approval.
3. Cấp token và hồ sơ, chuyển màn hình theo role.
4. Refresh bằng token còn hiệu lực khi cần.

**Ngoại lệ:** Sai thông tin/khóa/chưa xác minh/chưa approved bị chặn; mất API báo lỗi.

**Hậu điều kiện và điểm nghiệm thu:** Chỉ tài khoản đủ điều kiện được vào đúng màn hình; refresh không vượt điều kiện tài khoản.

## SP-UC-002 Đăng xuất

- **Tác nhân:** Người đang có phiên.
- **Phân kỳ:** Sprint 3.
- **Hiện trạng:** HIỆN CÓ API và UI.
- **API:** SP-API-008 `POST /api/v1/auth/logout`. Tra quyền/DTO ở [04 Endpoints](../api/endpoints.md).
- **Dữ liệu:** refresh_tokens; auth session ở UI.
- **Tiền điều kiện:** Có refresh token của phiên muốn kết thúc.
- **Nguồn yêu cầu:** FR-IAM-02.
- **Quy tắc:** BR-ACC-03; xem [03](business-rules.md).

**Luồng chính**

1. Gửi token của phiên tới logout.
2. Máy chủ thu hồi token nếu tồn tại.
3. UI xóa trạng thái đăng nhập, quay về trang phù hợp.

**Ngoại lệ:** Token không tồn tại vẫn kết thúc idempotent; không hứa mọi JWT trên thiết bị khác bị revoke ngay.

**Hậu điều kiện và điểm nghiệm thu:** Refresh token bị thu hồi không được cấp access mới.

## SP-UC-003 Đăng ký Customer

- **Tác nhân:** Guest.
- **Phân kỳ:** Sprint 3.
- **Hiện trạng:** YÊU CẦU; đã gỡ code/route hiện tại.
- **API:** ĐỀ XUẤT: POST /api/v1/auth/register. Tra quyền/DTO ở [04 Endpoints](../api/endpoints.md).
- **Dữ liệu:** users, user_tokens; dịch vụ email cần khôi phục.
- **Tiền điều kiện:** Chốt DEC-01; có cấu hình Gmail/reCAPTCHA và địa chỉ xác minh.
- **Nguồn yêu cầu:** FR-IAM-01/03.
- **Quy tắc:** BR-ACC-01,BR-ACC-04; xem [03](business-rules.md).

**Luồng chính**

1. Nhập hồ sơ/email/mật khẩu và hoàn tất reCAPTCHA.
2. Máy chủ kiểm tra trùng/đầu vào và chỉ cấp Customer.
3. Tạo tài khoản chưa xác minh và token có hạn.
4. Gửi hướng dẫn xác minh, báo trạng thái gửi thật.

**Ngoại lệ:** Email trùng hoặc reCAPTCHA sai bị chặn; lỗi email không kích hoạt tài khoản.

**Hậu điều kiện và điểm nghiệm thu:** Có tài khoản chờ xác minh và đường dẫn theo dõi; không tự đăng ký Admin.

## SP-UC-004 Xác minh và gửi lại email

- **Tác nhân:** Customer chờ xác minh.
- **Phân kỳ:** Sprint 3.
- **Hiện trạng:** YÊU CẦU; chưa có endpoint hiện tại.
- **API:** ĐỀ XUẤT: verify-email/resend-verification. Tra quyền/DTO ở [04 Endpoints](../api/endpoints.md).
- **Dữ liệu:** users, user_tokens; email.
- **Tiền điều kiện:** Có yêu cầu xác minh và cấu hình chính sách được chốt.
- **Nguồn yêu cầu:** FR-IAM-03.
- **Quy tắc:** BR-ACC-01,BR-ACC-02; xem [03](business-rules.md).

**Luồng chính**

1. Mở link/nhập mã từ Gmail.
2. Kiểm tra token đúng tài khoản, còn hạn, chưa dùng và giới hạn thử.
3. Đánh dấu đã dùng và xác minh trong một kết quả.
4. Cho đăng nhập khi đủ các điều kiện còn lại.

**Ngoại lệ:** Mã sai/hết hạn/dùng lại bị từ chối; gửi lại có giới hạn, không ghi mã bí mật vào log.

**Hậu điều kiện và điểm nghiệm thu:** Tài khoản chỉ được verified một lần theo token hợp lệ; link dùng được PC/mobile.

## SP-UC-005 Xem và sửa hồ sơ

- **Tác nhân:** Người đăng nhập; UI Customer.
- **Phân kỳ:** Sprint 3.
- **Hiện trạng:** HIỆN CÓ API và UI Customer.
- **API:** SP-API-075 `GET /api/v1/users/me`; SP-API-076 `PUT /api/v1/users/me`. Tra quyền/DTO ở [04 Endpoints](../api/endpoints.md).
- **Dữ liệu:** users.
- **Tiền điều kiện:** JWT hợp lệ, user tồn tại.
- **Nguồn yêu cầu:** FR-IAM-04.
- **Quy tắc:** BR-RBAC-01; xem [03](business-rules.md).

**Luồng chính**

1. Tải hồ sơ người gọi.
2. Sửa họ tên/số điện thoại trong DTO được hỗ trợ.
3. Lưu API rồi cập nhật UI từ kết quả máy chủ.

**Ngoại lệ:** Dữ liệu sai báo gần trường; không thêm đổi mật khẩu/avatar như chức năng đã có trong PUT hiện tại.

**Hậu điều kiện và điểm nghiệm thu:** Chỉ hồ sơ mình thay đổi, tải lại còn dữ liệu đã lưu.

## SP-UC-006 Quản lý ô tô cá nhân

- **Tác nhân:** Customer; Admin theo quyền.
- **Phân kỳ:** Sprint 3.
- **Hiện trạng:** HIỆN CÓ API; UI Customer có xem/thêm/sửa, chưa có mọi thao tác quản trị.
- **API:** SP-API-077 `GET /api/v1/vehicles`; SP-API-078 `GET /api/v1/vehicles/{id:long}`; SP-API-079 `GET /api/v1/vehicles/by-plate/{plateNumber}`; SP-API-080 `POST /api/v1/vehicles`; SP-API-081 `PUT /api/v1/vehicles/{id:long}`; SP-API-082 `DELETE /api/v1/vehicles/{id:long}`. Tra quyền/DTO ở [04 Endpoints](../api/endpoints.md).
- **Dữ liệu:** vehicles, users, parking_sessions, bookings, audit_logs.
- **Tiền điều kiện:** Có tài khoản; quyền sở hữu xe được xác minh theo DEC-02.
- **Nguồn yêu cầu:** FR-VEH-01/02.
- **Quy tắc:** BR-VEH-01,BR-VEH-02; xem [03](business-rules.md).

**Luồng chính**

1. Tải xe trong quyền.
2. Thêm biển số/hãng/model/màu hoặc sửa thuộc tính DTO cho phép.
3. Máy chủ chuẩn hóa, kiểm tra unique/sở hữu và lượt liên quan.
4. Lưu kết quả và lịch sử.

**Ngoại lệ:** Xe thuộc người khác/xe vãng lai chưa xác minh không được tự nhận; vô hiệu hóa không xóa lịch sử; không giả định PUT cho sửa biển số.

**Hậu điều kiện và điểm nghiệm thu:** Dữ liệu xe đúng chủ, không đổi quyền gói hoặc phiên âm thầm.

## SP-UC-007 Guest tìm và xem bãi công khai

- **Tác nhân:** Guest hoặc mọi vai trò.
- **Phân kỳ:** Sprint 3.
- **Hiện trạng:** MỘT PHẦN: API đọc có, UI Guest và bản đồ tìm kiếm chưa hoàn chỉnh.
- **API:** SP-API-040 `GET /api/v1/parking-lots`; SP-API-041 `GET /api/v1/parking-lots/{id:int}`; SP-API-042 `GET /api/v1/parking-lots/{id:int}/capacity`; SP-API-033 `GET /api/v1/parking-floors/lot/{lotId:int}`; SP-API-034 `GET /api/v1/parking-lots/{lotId:int}/floors`; SP-API-053 `GET /api/v1/parking-slots/floor/{floorId:int}`; SP-API-054 `GET /api/v1/parking-floors/{floorId:int}/slots`. Tra quyền/DTO ở [04 Endpoints](../api/endpoints.md).
- **Dữ liệu:** parking_lots, parking_floors, parking_slots; capacity đọc sessions/bookings.
- **Tiền điều kiện:** Không cần đăng nhập; có dữ liệu bãi được xác nhận.
- **Nguồn yêu cầu:** FR-MAP-01/02/03/04.
- **Quy tắc:** BR-RBAC-03,BR-PARK-02; xem [03](business-rules.md).

**Luồng chính**

1. Mở trang công khai, chọn điểm/vị trí nếu chức năng bản đồ đã được cấu hình.
2. Tải bãi/tầng/ô và khả dụng từ API.
3. Hiển thị trạng thái và thời điểm cập nhật.
4. Khi chọn dịch vụ cá nhân thì yêu cầu đăng nhập.

**Ngoại lệ:** Không GPS vẫn chọn được cách tìm khác khi triển khai bản đồ; dữ liệu rỗng/lỗi hiển thị đúng; không lộ ảnh/biển số.

**Hậu điều kiện và điểm nghiệm thu:** Chỉ xem không tạo giữ/xe; phân biệt public API đã có với GPS/map UI chưa có.

## SP-UC-008 Customer xem tầng và slot

- **Tác nhân:** Customer.
- **Phân kỳ:** Sprint 3.
- **Hiện trạng:** HIỆN CÓ UI React và API đọc.
- **API:** SP-API-040 `GET /api/v1/parking-lots`; SP-API-033 `GET /api/v1/parking-floors/lot/{lotId:int}`; SP-API-034 `GET /api/v1/parking-lots/{lotId:int}/floors`; SP-API-053 `GET /api/v1/parking-slots/floor/{floorId:int}`; SP-API-054 `GET /api/v1/parking-floors/{floorId:int}/slots`; SP-API-055 `GET /api/v1/parking-slots/{id:long}`. Tra quyền/DTO ở [04 Endpoints](../api/endpoints.md).
- **Dữ liệu:** parking_lots, parking_floors, parking_slots.
- **Tiền điều kiện:** Đăng nhập USER cho route Customer hiện tại.
- **Nguồn yêu cầu:** FR-MAP-03/04; FR-3D-02.
- **Quy tắc:** BR-PARK-01,BR-BOOK-01; xem [03](business-rules.md).

**Luồng chính**

1. Chọn bãi.
2. Tải và chọn tầng.
3. Xem danh sách bốn trạng thái và chi tiết được phép.

**Ngoại lệ:** Lỗi/empty không thay dữ liệu mẫu; trạng thái trống chưa được giữ riêng cho khách.

**Hậu điều kiện và điểm nghiệm thu:** Xem đúng dữ liệu API; không suy ra đã đặt chỗ hoặc đã có xe ở ô.

## SP-UC-009 Khai báo bãi

- **Tác nhân:** Admin.
- **Phân kỳ:** Sprint 3.
- **Hiện trạng:** HIỆN CÓ API, UI form chưa đủ.
- **API:** SP-API-043 `POST /api/v1/parking-lots`; SP-API-044 `PUT /api/v1/parking-lots/{id:int}`; SP-API-046 `DELETE /api/v1/parking-lots/{id:int}`. Tra quyền/DTO ở [04 Endpoints](../api/endpoints.md).
- **Dữ liệu:** parking_lots, audit_logs và dữ liệu tham chiếu.
- **Tiền điều kiện:** Admin hợp lệ, nguồn tên/địa chỉ/sức chứa được xác nhận.
- **Nguồn yêu cầu:** FR-PARK-01/04.
- **Quy tắc:** BR-PARK-03,BR-DATA-02; xem [03](business-rules.md).

**Luồng chính**

1. Nhập bãi theo DTO hiện có.
2. Kiểm tra tên unique và dữ liệu hợp lệ.
3. Lưu và đọc lại.
4. Xóa chỉ khi guard tham chiếu cho phép.

**Ngoại lệ:** Không thêm tọa độ địa lý/giờ hoạt động vào request rồi coi đã lưu; entity hiện chưa có các field đó.

**Hậu điều kiện và điểm nghiệm thu:** Bãi được lưu DB và không xóa lịch sử xe/booking để vượt ràng buộc.

## SP-UC-010 Khai báo tầng slot và sơ đồ

- **Tác nhân:** Admin.
- **Phân kỳ:** Sprint 3.
- **Hiện trạng:** HIỆN CÓ API; bố cục/fuel/area cần bổ sung và UI form chưa đủ.
- **API:** SP-API-036 `POST /api/v1/parking-floors/lot/{lotId:int}`; SP-API-037 `POST /api/v1/parking-lots/{lotId:int}/floors`; SP-API-038 `PUT /api/v1/parking-floors/{id:int}`; SP-API-039 `DELETE /api/v1/parking-floors/{id:int}`; SP-API-056 `POST /api/v1/parking-slots/floor/{floorId:int}`; SP-API-057 `POST /api/v1/parking-floors/{floorId:int}/slots`; SP-API-058 `PUT /api/v1/parking-slots/{id:long}`; SP-API-060 `DELETE /api/v1/parking-slots/{id:long}`. Tra quyền/DTO ở [04 Endpoints](../api/endpoints.md).
- **Dữ liệu:** parking_floors, parking_slots, audit_logs.
- **Tiền điều kiện:** Bãi tồn tại; có sơ đồ/mã/vị trí được xác nhận.
- **Nguồn yêu cầu:** FR-PARK-02/03; FR-3D-01.
- **Quy tắc:** BR-PARK-01,BR-DATA-01; xem [03](business-rules.md).

**Luồng chính**

1. Khai báo tầng.
2. Khai báo slot theo tầng và tọa độ.
3. Kiểm tra unique floor/slot code, trạng thái và quan hệ.
4. Đọc lại danh sách để đối chiếu sơ đồ.

**Ngoại lệ:** ID tầng viewer chuỗi không dùng thẳng thay core int; không sinh ô/xe khi thiếu dữ liệu.

**Hậu điều kiện và điểm nghiệm thu:** Mã/số/vị trí ô có thể ánh xạ nhất quán, dữ liệu có lịch sử.

## SP-UC-011 Quản lý tài khoản nội bộ

- **Tác nhân:** Admin.
- **Phân kỳ:** Sprint 3.
- **Hiện trạng:** HIỆN CÓ API; UI quản lý chưa đủ.
- **API:** SP-API-001 `GET /api/v1/admin/users`; SP-API-002 `GET /api/v1/admin/users/{id:long}`; SP-API-003 `POST /api/v1/admin/users`; SP-API-004 `PATCH /api/v1/admin/users/{id:long}/status`; SP-API-005 `PATCH /api/v1/admin/users/{id:long}/role`. Tra quyền/DTO ở [04 Endpoints](../api/endpoints.md).
- **Dữ liệu:** users, roles, staff_lot_assignments, audit_logs.
- **Tiền điều kiện:** Admin đang hoạt động.
- **Nguồn yêu cầu:** FR-IAM-05/06.
- **Quy tắc:** BR-ACC-04,BR-RBAC-01; xem [03](business-rules.md).

**Luồng chính**

1. Tra tài khoản.
2. Tạo STAFF/MANAGER hoặc đổi status/role theo quyền.
3. Máy chủ bảo vệ Admin cuối và cập nhật phân công liên quan.
4. Trả dữ liệu đã lưu.

**Ngoại lệ:** Không tạo ADMIN/USER qua Create nội bộ hiện tại; khóa/đổi Admin cuối bị chặn.

**Hậu điều kiện và điểm nghiệm thu:** Phân quyền không tự tăng qua dữ liệu client; có actor/lịch sử.

## SP-UC-012 Cấp và thu hồi phân công bãi

- **Tác nhân:** Admin; người nội bộ xem bãi mình.
- **Phân kỳ:** Sprint 3.
- **Hiện trạng:** HIỆN CÓ API, UI phần ghi chưa đủ.
- **API:** SP-API-068 `POST /api/v1/staff-assignments`; SP-API-069 `GET /api/v1/staff-assignments`; SP-API-070 `GET /api/v1/staff-assignments/my-lots`; SP-API-071 `GET /api/v1/staff-assignments/{id:long}`; SP-API-072 `POST /api/v1/staff-assignments/{id:long}/revoke`. Tra quyền/DTO ở [04 Endpoints](../api/endpoints.md).
- **Dữ liệu:** staff_lot_assignments, users, roles, parking_lots, audit_logs.
- **Tiền điều kiện:** Tài khoản nội bộ và bãi hợp lệ.
- **Nguồn yêu cầu:** FR-IAM-06; FR-PARK-05.
- **Quy tắc:** BR-RBAC-01,BR-RBAC-02; xem [03](business-rules.md).

**Luồng chính**

1. Admin chọn người và bãi.
2. Cấp phân công active, kiểm tra không trùng.
3. Nội bộ tải my-lots.
4. Thu hồi có lý do/người/thời điểm.

**Ngoại lệ:** Người không phải Admin không tự cấp; phân công bị thu hồi không còn cho thao tác.

**Hậu điều kiện và điểm nghiệm thu:** Chỉ bãi được giao được sử dụng; cấp bãi chưa đồng nghĩa có cả hai nhiệm vụ Staff.

## SP-UC-013 Phân công hai nhiệm vụ Staff

- **Tác nhân:** Admin; Manager nếu được cấp.
- **Phân kỳ:** Sprint 3.
- **Hiện trạng:** YÊU CẦU; chưa có field/guard nhiệm vụ.
- **API:** ĐỀ XUẤT: staff assignment duties. Tra quyền/DTO ở [04 Endpoints](../api/endpoints.md).
- **Dữ liệu:** Bổ sung nhiệm vụ vào phân công và audit.
- **Tiền điều kiện:** Chốt DEC-14 và cơ chế cấp/thu hồi.
- **Nguồn yêu cầu:** Sprint 3; FR-IAM-06.
- **Quy tắc:** BR-RBAC-02; xem [03](business-rules.md).

**Luồng chính**

1. Chọn nhân viên/bãi.
2. Cấp cổng và/hoặc điều phối độc lập.
3. API lưu nhiệm vụ và hiệu lực.
4. UI/API kiểm tra lại nhiệm vụ ở mỗi thao tác.

**Ngoại lệ:** Không suy role STAFF thành cả hai; người cấp không có quyền bị chặn.

**Hậu điều kiện và điểm nghiệm thu:** Hai Staff bị giới hạn nhiệm vụ đúng cả khi gọi API trực tiếp.

## SP-UC-014 Đóng mở nhận xe và bảo trì ô

- **Tác nhân:** Admin; Manager đúng bãi.
- **Phân kỳ:** Sprint 3.
- **Hiện trạng:** HIỆN CÓ API; UI thao tác chưa đủ, ảnh hưởng quyền gói chưa có.
- **API:** SP-API-045 `PATCH /api/v1/parking-lots/{id:int}/status`; SP-API-059 `PATCH /api/v1/parking-slots/{id:long}/status`. Tra quyền/DTO ở [04 Endpoints](../api/endpoints.md).
- **Dữ liệu:** parking_lots, parking_slots, parking_sessions, parking_dispatches, audit_logs.
- **Tiền điều kiện:** Có quyền bãi; chốt cách xử lý cam kết đặt/gói.
- **Nguồn yêu cầu:** FR-PARK-03/04.
- **Quy tắc:** BR-PARK-03; xem [03](business-rules.md).

**Luồng chính**

1. Tra trạng thái và lượt liên quan.
2. Chọn đóng nhận hoặc bảo trì/không khả dụng, nhập lý do.
3. Kiểm tra ô có phiên/assignment.
4. Lưu và thông báo theo kết quả API.

**Ngoại lệ:** Core chặn đổi ô OCCUPIED còn phiên và bảo trì ô ASSIGNED; không xóa xe để đóng nhận.

**Hậu điều kiện và điểm nghiệm thu:** Xe đang ở bãi vẫn tra/ra được; khả dụng cập nhật đúng phạm vi.

## SP-UC-015 Nhận xe vãng lai tại cổng

- **Tác nhân:** Hệ thống camera/AI và cổng vào; Staff cổng chỉ giám sát/xử lý ngoại lệ; hiện API Admin/Manager/Staff có quyền bãi.
- **Phân kỳ:** Sprint 3.
- **Hiện trạng:** MỘT PHẦN: API check-in có; duty/vé/fuel/UI chưa đủ.
- **API:** SP-API-047 `POST /api/v1/parking-sessions/check-in`. Tra quyền/DTO ở [04 Endpoints](../api/endpoints.md).
- **Dữ liệu:** vehicles, parking_sessions, bookings, audit_logs; vé/ảnh xác nhận cần bổ sung.
- **Tiền điều kiện:** Bãi nhận xe, khả dụng đủ; camera/AI và cơ chế cấp vé/thẻ vào hoạt động hoặc có nhánh xử lý ngoại lệ.
- **Nguồn yêu cầu:** FR-SES-01/02; FR-TKT-02; Sprint 3.
- **Quy tắc:** BR-SES-01,BR-SES-02,BR-SES-03,BR-PARK-02; xem [03](business-rules.md).

**Luồng chính**

1. Camera chụp ảnh các mặt cần thiết và AI/OCR tạo kết quả nhận dạng tạm.
2. Kiểm tra sức chứa; chống trùng bằng event/vé/thẻ và biển số khi đủ tin cậy, không yêu cầu Staff xác nhận ở cổng trong luồng chuẩn.
3. Cấp vé/thẻ vào và tạo một phiên ACTIVE ở trạng thái vận hành `WAITING_VERIFICATION`, không cần Customer ID.
4. Đưa phiên, ảnh và recognition result vào danh sách chờ để Staff điều phối xác minh tại slot.

**Ngoại lệ:** Bãi đầy/đóng bị chặn; OCR không đọc được hoặc nghi trùng chuyển `NEEDS_REVIEW`; timeout tra lại bằng EventId/vé trước khi thử mới. Staff cổng chỉ can thiệp ở các ngoại lệ này.

**Hậu điều kiện và điểm nghiệm thu:** Xe đã vào được tính trong sức chứa; có session/vé/ảnh truy vết dù biển số còn tạm, nhưng ô chưa OCCUPIED chỉ vì check-in.

## SP-UC-016 Theo dõi xe chờ và hàng việc xác minh

- **Tác nhân:** Staff điều phối.
- **Phân kỳ:** Sprint 3.
- **Hiện trạng:** MỘT PHẦN: đọc sessions/dispatch có, event queue riêng, UI và nguồn chung chưa đủ.
- **API:** SP-API-049 `GET /api/v1/parking-sessions/active`; SP-API-023 `GET /api/v1/parking-dispatches`; SP-API-031 `GET /api/events/sessions/active`. Tra quyền/DTO ở [04 Endpoints](../api/endpoints.md).
- **Dữ liệu:** parking_sessions/dispatches; event session bộ nhớ riêng hiện tại.
- **Tiền điều kiện:** Nhân viên đúng bãi/nhiệm vụ; có mã phiên thống nhất khi tích hợp.
- **Nguồn yêu cầu:** Sprint 3; FR-SES-03.
- **Quy tắc:** BR-SES-02,BR-EVT-03; xem [03](business-rules.md).

**Luồng chính**

1. Tải xe đã vào chưa có xác nhận ô.
2. Hiển thị ảnh/biển số trong quyền và thời điểm chờ.
3. Nhận tín hiệu slot cần xác minh theo thứ tự.
4. Đổi hàng việc sang `CONFIRMED`, `REJECTED` hoặc `NEEDS_REVIEW` sau kết quả máy chủ; không xóa lịch sử.

**Ngoại lệ:** Không ghép event SessionId chuỗi với core ID bằng cách ép kiểu; nguồn ngừng phải báo cũ.

**Hậu điều kiện và điểm nghiệm thu:** Không bỏ sót xe chờ, không render xe chờ thành xe đã đỗ.

## SP-UC-017 Phân hoặc đổi ô mục tiêu

- **Tác nhân:** Staff điều phối.
- **Phân kỳ:** Sprint 3.
- **Hiện trạng:** MỘT PHẦN: API có, thiếu duty/fuel/UI.
- **API:** SP-API-022 `POST /api/v1/parking-dispatches`; SP-API-026 `POST /api/v1/parking-dispatches/{id:long}/assign`. Tra quyền/DTO ở [04 Endpoints](../api/endpoints.md).
- **Dữ liệu:** parking_dispatches, parking_sessions, parking_slots, audit_logs.
- **Tiền điều kiện:** Phiên mở, ô cùng bãi và nhận được xe; reassign chỉ PENDING/ASSIGNED.
- **Nguồn yêu cầu:** Sprint 3.
- **Quy tắc:** BR-DSP-01,BR-DSP-02,BR-SES-02; xem [03](business-rules.md).

**Luồng chính**

1. Chọn phiên và ô mục tiêu hoặc AUTO.
2. Kiểm tra bãi/trạng thái/xung đột/tương thích.
3. Ghi assignment và cập nhật tham chiếu phiên.
4. Hiển thị lớp được phân riêng.

**Ngoại lệ:** Hai xe tranh ô: một yêu cầu phải xung đột; ô bảo trì/khác bãi không hợp lệ.

**Hậu điều kiện và điểm nghiệm thu:** Ô vẫn EMPTY về vật lý khi mới phân; không coi assignment là quyền đặt ô của khách vãng lai.

## SP-UC-018 Xác nhận xe đỗ đúng ô

- **Tác nhân:** Staff điều phối.
- **Phân kỳ:** Sprint 3.
- **Hiện trạng:** MỘT PHẦN: confirm API có, thiếu duty/ảnh/fuel/UI tích hợp.
- **API:** SP-API-027 `POST /api/v1/parking-dispatches/{id:long}/confirm-occupied`. Tra quyền/DTO ở [04 Endpoints](../api/endpoints.md).
- **Dữ liệu:** parking_dispatches, parking_slots, parking_sessions, audit_logs.
- **Tiền điều kiện:** Lệnh ASSIGNED; Staff kiểm tra xe thực tế và ô.
- **Nguồn yêu cầu:** Sprint 3; FR-3D-03.
- **Quy tắc:** BR-DSP-02,BR-DSP-03,BR-3D-01; xem [03](business-rules.md).

**Luồng chính**

1. Đối chiếu ảnh/biển số OCR tạm với xe thực tế tại ô và phiên/vé tương ứng.
2. Xác nhận hoặc sửa biển số, thông tin xe/nhiên liệu; gửi actual slot và lý do sửa nếu có.
3. Máy chủ kiểm tra trạng thái/xung đột/unique, chốt nhận dạng và ghi actual slot/confirmed_at/OCCUPIED trong một giao dịch.
4. Web/3D đọc kết quả cùng nguồn khi đã ghép; hàng việc được đánh dấu đã xử lý nhưng giữ lịch sử.

**Ngoại lệ:** Lệnh đã kết thúc hoặc ô vừa có xe khác bị chặn; không báo thành công khi mất mạng.

**Hậu điều kiện và điểm nghiệm thu:** Chỉ sau xác nhận tại ô mới có biển số chính thức và xe trong slot; truy vết giá trị OCR gốc, giá trị sửa, người/thời điểm xác nhận.

## SP-UC-019 Ghi nhận xe đỗ sai ô mục tiêu

- **Tác nhân:** Staff điều phối.
- **Phân kỳ:** Sprint 3.
- **Hiện trạng:** MỘT PHẦN: API có từ ASSIGNED, chưa có toàn quy trình xử lý sau đó.
- **API:** SP-API-028 `POST /api/v1/parking-dispatches/{id:long}/report-wrong-slot`. Tra quyền/DTO ở [04 Endpoints](../api/endpoints.md).
- **Dữ liệu:** parking_dispatches, parking_slots, parking_sessions, audit_logs.
- **Tiền điều kiện:** Lệnh đang ASSIGNED; biết ô thực tế và có bằng chứng.
- **Nguồn yêu cầu:** Sprint 3; FR-SES-06.
- **Quy tắc:** BR-DSP-01,BR-DSP-04; xem [03](business-rules.md).

**Luồng chính**

1. Đối chiếu xe và ô thực tế.
2. Gửi actualSlotId cùng lý do.
3. Kiểm tra cùng bãi/khả dụng/xung đột.
4. Lưu WRONG_SLOT, actual slot OCCUPIED và cập nhật phiên.

**Ngoại lệ:** Không gọi sau khi đã confirm OCCUPIED; ô thực tế đang được dùng không được ghi đè.

**Hậu điều kiện và điểm nghiệm thu:** Trạng thái mô tả đúng xe thực tế, không che sai lệch bằng đổi màu.

## SP-UC-020 Di chuyển xe đã xác nhận đỗ

- **Tác nhân:** Staff điều phối; Manager xử lý ngoại lệ trong quyền.
- **Phân kỳ:** Sprint 3.
- **Hiện trạng:** YÊU CẦU bổ sung; reassign hiện không nhận OCCUPIED/WRONG_SLOT.
- **API:** ĐỀ XUẤT: nghiệp vụ relocation cho phiên đã đỗ. Tra quyền/DTO ở [04 Endpoints](../api/endpoints.md).
- **Dữ liệu:** Phiên/dispatch/slot, lịch sử di chuyển và ảnh nếu cần.
- **Tiền điều kiện:** Chốt thời điểm trả ô cũ và xác nhận ô mới theo DEC-16.
- **Nguồn yêu cầu:** Sprint 3; FR-SES-06.
- **Quy tắc:** BR-DSP-02,BR-DSP-04; xem [03](business-rules.md).

**Luồng chính**

1. Ghi yêu cầu/lý do chuyển.
2. Giữ/kiểm tra ô mới không xung đột.
3. Xác nhận xe rời ô cũ và tới ô mới.
4. Cập nhật nguyên tử theo quy tắc, giữ lịch sử.

**Ngoại lệ:** Xe chưa rời không trả ô; không gọi reassign hiện tại để bỏ qua guard.

**Hậu điều kiện và điểm nghiệm thu:** Mỗi thời điểm nguồn xác nhận không ghi hai xe cùng ô hoặc làm mất xe giữa hai bước.

## SP-UC-021 Quẹt vé/thẻ tại cổng ra và tra lượt gửi

- **Tác nhân:** Hệ thống cổng ra và Staff cổng; Customer chỉ lịch sử mình.
- **Phân kỳ:** Sprint 3.
- **Hiện trạng:** HIỆN CÓ API tra bằng ID/biển số; vé và UI vận hành chưa đủ.
- **API:** SP-API-049 `GET /api/v1/parking-sessions/active`; SP-API-051 `GET /api/v1/parking-sessions/{id:long}`; SP-API-052 `GET /api/v1/parking-sessions/by-plate/{plateNumber}/active`; SP-API-050 `GET /api/v1/parking-sessions/my-sessions`. Tra quyền/DTO ở [04 Endpoints](../api/endpoints.md).
- **Dữ liệu:** parking_sessions, vehicles, slots, bãi; vé/thẻ, ảnh vào và access event cần bổ sung.
- **Tiền điều kiện:** Xe đã tới cổng ra; vé/thẻ được quẹt hoặc có nhánh tra cứu ngoại lệ; đúng quyền bãi/sở hữu.
- **Nguồn yêu cầu:** FR-SES-03/06.
- **Quy tắc:** BR-RBAC-01,BR-SES-04,BR-SES-05; xem [03](business-rules.md).

**Luồng chính**

1. Xe tự tới làn checkout và quẹt vé/thẻ.
2. Máy chủ tìm phiên mở duy nhất được gắn với vé/thẻ và hiển thị ảnh/thông tin lúc vào.
3. Staff/camera đối chiếu xe; nếu thẻ lỗi mới tra biển số hoặc chuyển xác minh ngoại lệ.
4. Ngay sau khi chọn đúng phiên, máy chủ tính fee assessment và chuyển sang bước thu tiền.

**Ngoại lệ:** Không tìm thấy, thẻ mất/hỏng, sai vé hoặc sai xe chuyển xác minh; không tạo phiên mới để trả và không tính phí cho phiên chưa xác định.

**Hậu điều kiện và điểm nghiệm thu:** Một lần quẹt tải đúng phiên và thông tin lúc vào rồi tạo/tra đúng fee assessment; dữ liệu ngoài bãi không lộ và retry không tạo phí/khoản thu trùng.

## SP-UC-022 Tính và tra phí theo phiên

- **Tác nhân:** Staff cổng đủ quyền.
- **Phân kỳ:** Sprint 3.
- **Hiện trạng:** YÊU CẦU; chưa có API phí đầy đủ.
- **API:** ĐỀ XUẤT: GET /api/v1/parking-sessions/{id}/fee. Tra quyền/DTO ở [04 Endpoints](../api/endpoints.md).
- **Dữ liệu:** Phiên, bảng giá và fee assessment đề xuất.
- **Tiền điều kiện:** Xe đã tới cổng ra, vé/thẻ đã chọn đúng phiên; giá/mốc giờ/làm tròn được chốt ở DEC-04.
- **Nguồn yêu cầu:** FR-PAY-01; FR-SES-04.
- **Quy tắc:** BR-PAY-01,BR-PAY-03; xem [03](business-rules.md).

**Luồng chính**

1. Nhận phiên đã xác minh từ sự kiện quẹt vé/thẻ tại cổng ra.
2. Máy chủ lấy giờ và phiên bản giá.
3. Tính chi tiết, đối chiếu khoản đã trả/quyền hợp lệ.
4. Trả phí/đã thu/còn thiếu và mốc hiệu lực kết quả.

**Ngoại lệ:** Chưa có giá báo thiếu cấu hình; amount do web sửa không làm thay phí.

**Hậu điều kiện và điểm nghiệm thu:** Có căn cứ phí lưu/đối chiếu được, tách khỏi tiền thực nhận và trạng thái xe ra.

## SP-UC-023 Ghi nhận thu tiền và biên lai

- **Tác nhân:** Staff cổng đủ quyền; tiền gói theo quy trình riêng.
- **Phân kỳ:** Sprint 3.
- **Hiện trạng:** YÊU CẦU; chưa có API/bảng khoản thu core.
- **API:** ĐỀ XUẤT: session payments và receipt. Tra quyền/DTO ở [04 Endpoints](../api/endpoints.md).
- **Dữ liệu:** payments, chứng từ/fee assessment đề xuất, actor/phiên.
- **Tiền điều kiện:** Phí hợp lệ, phương thức được phép, có bằng chứng nhận tiền.
- **Nguồn yêu cầu:** FR-PAY-04/05/06/08; Sprint 3.
- **Quy tắc:** BR-PAY-02,BR-PAY-03,BR-PAY-04; xem [03](business-rules.md).

**Luồng chính**

1. Xác nhận khoản thực nhận và phương thức.
2. Gửi mã yêu cầu chống trùng và chứng từ.
3. Máy chủ kiểm tra quyền/số tiền/kết quả.
4. Lưu khoản thu và biên lai, trả trạng thái đủ/chưa đủ.

**Ngoại lệ:** Timeout tra trạng thái theo mã, không thu lại ngay; điều phối không ghi thu.

**Hậu điều kiện và điểm nghiệm thu:** Một khoản thực nhận được lưu một lần; phiên/ô chưa bị trả do tiền.

## SP-UC-024 Xác nhận xe ra và đóng phiên

- **Tác nhân:** Staff cổng.
- **Phân kỳ:** Sprint 3.
- **Hiện trạng:** MỘT PHẦN: check-out có, thiếu tiền/vé/duty và UI đầy đủ.
- **API:** SP-API-048 `POST /api/v1/parking-sessions/{id:long}/check-out`. Tra quyền/DTO ở [04 Endpoints](../api/endpoints.md).
- **Dữ liệu:** parking_sessions, dispatches, slots, audit_logs; ticket/payment cần bổ sung.
- **Tiền điều kiện:** Xe đã tới cổng ra và quẹt vé/thẻ; phiên/ảnh lúc vào đã đối chiếu; đủ phí hoặc ngoại lệ được duyệt; xác nhận xe ra thực tế.
- **Nguồn yêu cầu:** FR-SES-05; FR-TKT-03.
- **Quy tắc:** BR-SES-05,BR-PAY-03,BR-DATA-02; xem [03](business-rules.md).

**Luồng chính**

1. Đọc lại kết quả quẹt thẻ, phiên, ảnh lúc vào và điều kiện phí.
2. Sau khi thanh toán/ngoại lệ hợp lệ, Staff hoặc thiết bị được ủy quyền xác nhận xe thực sự qua cổng ra và lưu ảnh nếu có.
3. Ghi giờ ra, đóng phiên, thu/giải phóng vé và trả đúng ô theo quy tắc.
4. Tải lại danh sách/3D từ kết quả lưu.

**Ngoại lệ:** Chưa đủ phí/sai xe/vé không hoàn tất; gửi lại không sinh lần ra mới; hiện các guard tiền/vé chưa có.

**Hậu điều kiện và điểm nghiệm thu:** Phiên COMPLETED phản ánh xe đã ra, ô/vé đúng trạng thái và lịch sử còn.

## SP-UC-025 Lưu và xem ảnh xe

- **Tác nhân:** Staff theo bãi/phiên; hiện uploader/Admin.
- **Phân kỳ:** Sprint 3.
- **Hiện trạng:** MỘT PHẦN: upload/metadata/delete có, liên kết và quyền bãi chưa đủ.
- **API:** SP-API-015 `POST /api/v1/images`; SP-API-016 `GET /api/v1/images/{id:long}`; SP-API-017 `DELETE /api/v1/images/{id:long}`. Tra quyền/DTO ở [04 Endpoints](../api/endpoints.md).
- **Dữ liệu:** stored_images, Cloudinary; session-image links đề xuất.
- **Tiền điều kiện:** Dịch vụ ảnh cấu hình, file hợp lệ và quyền người gửi.
- **Nguồn yêu cầu:** Sprint 3; NFR-PRV-01.
- **Quy tắc:** BR-IMG-02,BR-IMG-03; xem [03](business-rules.md).

**Luồng chính**

1. Chụp hoặc chọn file.
2. Upload Cloudinary, lưu metadata DB.
3. Gắn ảnh vào đúng phiên/bãi khi bổ sung hợp đồng.
4. Xem lại trong quyền cần vận hành.

**Ngoại lệ:** File sai/provider/DB lỗi báo thật; không xóa ảnh đang làm bằng chứng trái retention.

**Hậu điều kiện và điểm nghiệm thu:** Ảnh truy vết được và không lộ public/sai bãi.

## SP-UC-026 AI nhận diện ở cổng và Staff xác minh tại slot

- **Tác nhân:** Hệ thống camera/AI tại cổng; Staff điều phối tại slot.
- **Phân kỳ:** Sprint 3.
- **Hiện trạng:** MỘT PHẦN: AI/Gemini độc lập có, UI và bản ghi xác nhận chưa đủ.
- **API:** SP-API-014 `POST /api/gemini/analyze`; FastAPI POST /api/v1/detect. Tra quyền/DTO ở [04 Endpoints](../api/endpoints.md).
- **Dữ liệu:** Ảnh, recognition result và xác nhận theo phiên cần bổ sung.
- **Tiền điều kiện:** Ảnh có nguồn được phép dùng; AI chạy được; session/vé có thể liên kết; Staff điều phối có quyền xác nhận tại bãi.
- **Nguồn yêu cầu:** Sprint 3; FR-SES-02.
- **Quy tắc:** BR-IMG-01,BR-VEH-03; xem [03](business-rules.md).

**Luồng chính**

1. Gửi ảnh hoặc URL tới đúng dịch vụ.
2. Nhận biển số gốc/chuẩn hóa/confidence hoặc lỗi.
3. Lưu kết quả AI/OCR là dữ liệu tạm, liên kết session ACTIVE/vé/ảnh và đưa vào queue; không dừng luồng chuẩn để Staff cổng xác nhận.
4. Khi xe đỗ, Staff điều phối quan sát xe tại slot, xác nhận/sửa biển số và nhiên liệu; lưu giá trị gốc/xác nhận/actor/time cùng actual slot.

**Ngoại lệ:** Không đọc được chuyển `NEEDS_REVIEW` để Staff tại ô nhập có lý do; không lấy plate cũ; Gemini NoOp không tính phân tích thật; sửa biển số không tạo session thứ hai.

**Hậu điều kiện và điểm nghiệm thu:** AI được phép khởi tạo nhận dạng tạm/session chờ xác minh nhưng không tự cho rằng xe đã đỗ hoặc đã ra; biển số chính thức chỉ được chốt tại slot.

## SP-UC-027 Tiếp nhận sự kiện cổng và slot

- **Tác nhân:** Thiết bị/người thao tác được xác thực.
- **Phân kỳ:** Sprint 3.
- **Hiện trạng:** MỘT PHẦN: pipeline bộ nhớ có, chưa lưu/chung core/guard đầy đủ.
- **API:** SP-API-030 `POST /api/events`; SP-API-032 `GET /api/events/logs`. Tra quyền/DTO ở [04 Endpoints](../api/endpoints.md).
- **Dữ liệu:** ParkingEventLog/EventParkingSession bộ nhớ; đích log tham chiếu core.
- **Tiền điều kiện:** Có EventId, nguồn/time, mã liên kết và quyền nguồn xác nhận.
- **Nguồn yêu cầu:** Sprint 3; NFR-REL-01/02.
- **Quy tắc:** BR-EVT-01,BR-EVT-02,BR-EVT-03; xem [03](business-rules.md).

**Luồng chính**

1. Kiểm tra danh tính, định dạng và trùng ID.
2. Chuẩn hóa loại nguồn cổng/slot.
3. Gọi dịch vụ nghiệp vụ đúng bước khi đã tích hợp.
4. Lưu log kết quả và cung cấp snapshot đã lưu.

**Ngoại lệ:** Lặp trả kết quả không tác động lần hai; sự kiện cũ/sai phiên chuyển reject/xác minh; không tin TriggeredBy từ body.

**Hậu điều kiện và điểm nghiệm thu:** Một sự kiện có kết quả truy vết bền và không tạo nguồn xe cạnh tranh.

## SP-UC-028 Xem bãi 3D và danh sách thay thế

- **Tác nhân:** Guest/Customer hoặc nội bộ trong quyền.
- **Phân kỳ:** Sprint 3.
- **Hiện trạng:** MỘT PHẦN: viewer riêng mô phỏng, chưa ghép React/DB chung.
- **API:** SP-API-061 `GET /api/ParkingSlots`; SP-API-062 `GET /api/v1/parking-viewer/slots`; SP-API-063 `GET /api/ParkingSlots/floor/{floorId}`; SP-API-064 `GET /api/v1/parking-viewer/floors/{floorId}/slots`. Tra quyền/DTO ở [04 Endpoints](../api/endpoints.md).
- **Dữ liệu:** Viewer dictionary/event memory hiện tại; đích snapshot core.
- **Tiền điều kiện:** Có sơ đồ/ánh xạ slot và projection công khai/nội bộ.
- **Nguồn yêu cầu:** FR-3D-01/02/03/04/06.
- **Quy tắc:** BR-3D-01,BR-RBAC-03; xem [03](business-rules.md).

**Luồng chính**

1. Chọn bãi/tầng.
2. Tải cùng snapshot với danh sách.
3. Render bốn trạng thái, lớp phân riêng và xe chỉ tại ô xác nhận.
4. Cho xoay/zoom/chọn ô, lỗi 3D chuyển danh sách.

**Ngoại lệ:** Thiếu dữ liệu báo rỗng; không tự sinh xe; public không có biển số/ảnh/phiên riêng.

**Hậu điều kiện và điểm nghiệm thu:** PC/mobile đối chiếu đúng mã/vị trí/số ô, tương tác quan sát không đổi nghiệp vụ.

## SP-UC-029 Mất mạng và đồng bộ lại

- **Tác nhân:** Mọi vai trò đang sử dụng.
- **Phân kỳ:** Sprint 3.
- **Hiện trạng:** YÊU CẦU ghép; snapshot/version chung chưa có.
- **API:** ĐỀ XUẤT: đọc snapshot core và tra kết quả thao tác. Tra quyền/DTO ở [04 Endpoints](../api/endpoints.md).
- **Dữ liệu:** Dữ liệu đã lưu, mã yêu cầu, version/time.
- **Tiền điều kiện:** Có trạng thái xác nhận cuối và cơ chế nhận biết mất kết nối.
- **Nguồn yêu cầu:** FR-SES-07; FR-3D-05/06.
- **Quy tắc:** BR-3D-02,BR-DATA-01,BR-EVT-02; xem [03](business-rules.md).

**Luồng chính**

1. Giữ kết quả cuối, hiển thị chưa đồng bộ/thời điểm.
2. Không báo thao tác ghi thành công khi chưa xác nhận.
3. Nối lại tra kết quả request chưa rõ.
4. Tải snapshot đầy đủ và bỏ cập nhật cũ.

**Ngoại lệ:** Không gửi mới để nhân đôi thu/xe; 3D lỗi có danh sách; DB lỗi không fallback seed.

**Hậu điều kiện và điểm nghiệm thu:** Hai thiết bị về cùng dữ liệu server, không mất lịch sử hoặc tăng xe/tiền do retry.

## SP-UC-030 Tạo đặt trước khả năng tiếp nhận

- **Tác nhân:** Customer.
- **Phân kỳ:** Sprint 4; API có sớm.
- **Hiện trạng:** MỘT PHẦN: API booking có, UI/giữ thời gian/tiền chưa đủ.
- **API:** SP-API-009 `POST /api/v1/bookings`; SP-API-010 `GET /api/v1/bookings/my`; SP-API-011 `GET /api/v1/bookings/{id:long}`. Tra quyền/DTO ở [04 Endpoints](../api/endpoints.md).
- **Dữ liệu:** bookings, vehicles, capacity và audit_logs.
- **Tiền điều kiện:** Customer có xe trong quyền; giá/giữ/khung giờ đã cấu hình cho luồng đích.
- **Nguồn yêu cầu:** FR-BOOK-01/02/03/04/05.
- **Quy tắc:** BR-BOOK-01,BR-BOOK-02,BR-PAY-02; xem [03](business-rules.md).

**Luồng chính**

1. Chọn bãi/xe/khoảng giờ.
2. Kiểm tra khả dụng và tạo quyền giữ theo chính sách.
3. Hoàn tất điều kiện xác nhận/tiền nếu có.
4. Trả mã/trạng thái và danh sách cá nhân.

**Ngoại lệ:** Core hiện tạo PENDING và trừ sức chứa, chưa có hold expiry/payment; khách không chọn chiếm slot.

**Hậu điều kiện và điểm nghiệm thu:** Booking chưa tạo xe hoặc phiên, không nhận vượt và không nhầm PENDING là đã trả tiền.

## SP-UC-031 Hủy hoặc hết hạn đặt chỗ

- **Tác nhân:** Customer; nội bộ trong quyền; hệ thống hết hạn.
- **Phân kỳ:** Sprint 4.
- **Hiện trạng:** MỘT PHẦN: cancel PENDING có; tự hết hạn/tiền chưa có.
- **API:** SP-API-012 `POST /api/v1/bookings/{id:long}/cancel`; SP-API-010 `GET /api/v1/bookings/my`. Tra quyền/DTO ở [04 Endpoints](../api/endpoints.md).
- **Dữ liệu:** bookings, audit_logs; khoản tiền đối soát riêng.
- **Tiền điều kiện:** Booking thuộc người gọi/bãi, còn ở bước cho phép hủy.
- **Nguồn yêu cầu:** FR-BOOK-06; FR-PAY-05/07.
- **Quy tắc:** BR-BOOK-04,BR-PAY-05; xem [03](business-rules.md).

**Luồng chính**

1. Tra booking và quyền.
2. Hủy có lý do hoặc xử lý hết hạn theo chính sách.
3. Giải phóng đúng phần giữ.
4. Hiển thị tiền cần đối soát nếu có.

**Ngoại lệ:** CHECKED_IN không hủy như giữ chưa dùng; EXPIRED mới có miền, không coi scheduler đã có.

**Hậu điều kiện và điểm nghiệm thu:** Không mất phiên đang đỗ; hủy không tự hoàn tiền thành công.

## SP-UC-032 Nhận xe đã đặt trước

- **Tác nhân:** Staff cổng và Customer.
- **Phân kỳ:** Sprint 4.
- **Hiện trạng:** MỘT PHẦN: check-in nhận PENDING theo bãi/biển số, thiếu khung giờ/tiền/mã vé đầy đủ.
- **API:** SP-API-047 `POST /api/v1/parking-sessions/check-in`; SP-API-011 `GET /api/v1/bookings/{id:long}`. Tra quyền/DTO ở [04 Endpoints](../api/endpoints.md).
- **Dữ liệu:** bookings, vehicles, parking_sessions, audit_logs.
- **Tiền điều kiện:** Đúng xe/bãi/khung giờ và điều kiện tiền theo chính sách.
- **Nguồn yêu cầu:** FR-BOOK-07; FR-SES-02.
- **Quy tắc:** BR-BOOK-03,BR-SES-02; xem [03](business-rules.md).

**Luồng chính**

1. Đối chiếu mã đặt/xe và hiệu lực.
2. Chuyển đúng booking sang đã nhận một lần.
3. Tạo phiên liên kết, không tính đôi sức chứa.
4. Chuyển xe sang điều phối thực tế.

**Ngoại lệ:** Đến sớm/trễ/không hợp lệ xử lý theo DEC-08; không dùng lại booking tạo phiên thứ hai.

**Hậu điều kiện và điểm nghiệm thu:** Xe chỉ thành Có xe sau xác nhận ô; booking và phiên có liên kết duy nhất.

## SP-UC-033 Đăng ký quyền gửi cố định

- **Tác nhân:** Customer.
- **Phân kỳ:** Sprint 5.
- **Hiện trạng:** YÊU CẦU; chưa có schema/API.
- **API:** ĐỀ XUẤT: parking-passes; plan và quyền ô. Tra quyền/DTO ở [04 Endpoints](../api/endpoints.md).
- **Dữ liệu:** Khu/ô, xe, gói/quyền kỳ, tiền/duyệt đề xuất.
- **Tiền điều kiện:** Tài khoản/xe xác minh, có khu/ô và kỳ/chính sách được chốt.
- **Nguồn yêu cầu:** FR-PASS-01/02/03; nghiệp vụ khu cố định.
- **Quy tắc:** BR-FIX-01,BR-VEH-02,BR-PAY-04; xem [03](business-rules.md).

**Luồng chính**

1. Chọn bãi/khu/ô cố định và kỳ được bán.
2. Kiểm tra xung đột quyền, xe và giá.
3. Thanh toán online hoặc gửi chứng từ trực tiếp theo quy trình.
4. Kích hoạt/chờ hiệu lực sau đủ điều kiện.

**Ngoại lệ:** Staff ghi chứng từ tiền gói không tự thành người duyệt khi chưa được cấp; không tự coi mọi gói năm/tuần đã duyệt.

**Hậu điều kiện và điểm nghiệm thu:** Có quyền ô/xe/kỳ truy vết; chưa có xe vật lý chỉ vì đăng ký thành công.

## SP-UC-034 Phát thẻ khu cố định lần đầu

- **Tác nhân:** Staff và Customer đã đăng ký.
- **Phân kỳ:** Sprint 5.
- **Hiện trạng:** YÊU CẦU; chưa có dữ liệu cấp thẻ.
- **API:** ĐỀ XUẤT: cấp thẻ và thông báo tiếp nhận. Tra quyền/DTO ở [04 Endpoints](../api/endpoints.md).
- **Dữ liệu:** Thẻ, quyền gói/xe/ô và bản ghi bàn giao đề xuất.
- **Tiền điều kiện:** Khách có quyền hợp lệ và tới lần đầu, nhân viên nhận được thông tin hỗ trợ.
- **Nguồn yêu cầu:** FR-TKT-01/04; nghiệp vụ khu cố định.
- **Quy tắc:** BR-FIX-03,BR-SES-04; xem [03](business-rules.md).

**Luồng chính**

1. Nhân viên đối chiếu khách/xe/quyền.
2. Chọn mã thẻ chưa cấp xung đột.
3. Bàn giao và xác nhận đã phát.
4. Lưu người/thời điểm/ảnh hoặc chứng cứ theo chính sách.

**Ngoại lệ:** Chưa đủ điều kiện không phát; gửi lại không tạo hai thẻ cùng quyền; mất/hỏng theo DEC-12.

**Hậu điều kiện và điểm nghiệm thu:** Phân biệt đã đăng ký và đã nhận thẻ; truy vết đúng khách/xe.

## SP-UC-035 Vào ra khu cố định bằng thẻ

- **Tác nhân:** Customer và Staff cổng/khu.
- **Phân kỳ:** Sprint 5.
- **Hiện trạng:** YÊU CẦU; chưa có bridge thẻ/barrier/gói.
- **API:** ĐỀ XUẤT: kiểm tra thẻ/quyền và tạo phiên. Tra quyền/DTO ở [04 Endpoints](../api/endpoints.md).
- **Dữ liệu:** Thẻ, pass, xe, khu/slot, phiên và log đề xuất.
- **Tiền điều kiện:** Thẻ đúng xe/khu và quyền còn hiệu lực; có điều kiện nhận xe.
- **Nguồn yêu cầu:** FR-PASS-04; FR-SES-02/05.
- **Quy tắc:** BR-FIX-01,BR-PAY-03,BR-SES-05; xem [03](business-rules.md).

**Luồng chính**

1. Quét/nhập thẻ và đối chiếu xe.
2. Kiểm tra hiệu lực/khu/barrier/ô đã đăng ký.
3. Nhận xe tạo phiên, xác nhận đỗ thực tế và hiển thị đầy đủ biển số đã đăng ký trên bảng IoT vật lý của đúng slot.
4. Khi ra đối chiếu phí ngoài quyền, xác nhận và đóng phiên.

**Ngoại lệ:** Sai xe/thẻ/hết hạn xử lý trong quyền; hết gói khi đang đỗ không xóa phiên.

**Hậu điều kiện và điểm nghiệm thu:** Mỗi lượt có phiên riêng, quyền gói không thay bằng chứng xe vào/ra.

## SP-UC-036 Gia hạn hoặc thay đổi xe của quyền cố định

- **Tác nhân:** Customer và người duyệt theo quyền.
- **Phân kỳ:** Sprint 5.
- **Hiện trạng:** YÊU CẦU; chưa có API/schema.
- **API:** ĐỀ XUẤT: pass renewals/change requests. Tra quyền/DTO ở [04 Endpoints](../api/endpoints.md).
- **Dữ liệu:** Quyền gói, lịch sử kỳ/xe, khoản tiền đề xuất.
- **Tiền điều kiện:** Chốt DEC-02/09/10/11, có quyền sở hữu và căn cứ thay đổi.
- **Nguồn yêu cầu:** FR-PASS-05/06/07.
- **Quy tắc:** BR-VEH-02,BR-PAY-02,BR-FIX-01; xem [03](business-rules.md).

**Luồng chính**

1. Xem kỳ/xe/ô và điều kiện hiện tại.
2. Gửi gia hạn hoặc yêu cầu đổi xe.
3. Xác minh/thu/duyệt đúng quy trình.
4. Lưu kỳ/xe mới và lịch sử, cập nhật bảng ô sau hiệu lực.

**Ngoại lệ:** Không đổi xe đang dùng để tránh đối chiếu; retry không cộng kỳ/thu hai lần.

**Hậu điều kiện và điểm nghiệm thu:** Lịch sử quyền cũ còn, kỳ mới có ngày bắt đầu/kết thúc rõ, không tự đổi số xe vật lý.

## SP-UC-037 Báo ô cố định bị chiếm

- **Tác nhân:** Customer có quyền ô.
- **Phân kỳ:** Phần khu cố định, chi tiết SP-DEC-18.
- **Hiện trạng:** YÊU CẦU; chưa có API/schema.
- **API:** ĐỀ XUẤT: POST /api/v1/slot-reports. Tra quyền/DTO ở [04 Endpoints](../api/endpoints.md).
- **Dữ liệu:** Report, ảnh, quyền ô và thông báo đề xuất.
- **Tiền điều kiện:** Người gửi có quyền ô trong kỳ, có ảnh/quan sát hợp lệ.
- **Nguồn yêu cầu:** Nghiệp vụ chiếm ô; SP-DEC-18.
- **Quy tắc:** BR-FIX-04,BR-IMG-02; xem [03](business-rules.md).

**Luồng chính**

1. Chọn đúng ô quyền mình.
2. Gửi mô tả/ảnh và thời điểm.
3. Kiểm tra quyền/bằng chứng.
4. Lưu report, thông báo nhân viên/Manager trong phạm vi.

**Ngoại lệ:** Không được báo thay quyền người khác; upload lỗi không giả đã nhận bằng chứng.

**Hậu điều kiện và điểm nghiệm thu:** Report có trạng thái theo dõi và người tiếp nhận, chưa tự phát sinh phạt.

## SP-UC-038 Xác minh và xử lý report chiếm ô

- **Tác nhân:** Staff/Manager trong quyền; người duyệt phạt theo chính sách.
- **Phân kỳ:** Phần khu cố định.
- **Hiện trạng:** YÊU CẦU; chưa có API/schema.
- **API:** ĐỀ XUẤT: report review/resolve. Tra quyền/DTO ở [04 Endpoints](../api/endpoints.md).
- **Dữ liệu:** Report, bằng chứng, quyết định và tiền nếu được duyệt.
- **Tiền điều kiện:** Chốt quyền xử lý và chính sách phạt/khiếu nại.
- **Nguồn yêu cầu:** Nghiệp vụ chiếm ô; SP-DEC-18.
- **Quy tắc:** BR-FIX-04,BR-PAY-04,BR-DATA-02; xem [03](business-rules.md).

**Luồng chính**

1. Nhận report đúng bãi.
2. Đối chiếu quyền ô/xe/bằng chứng thực tế.
3. Ghi kết quả/lý do và hành động trong quyền.
4. Thông báo người gửi, khoản phạt đi luồng thu riêng nếu hợp lệ.

**Ngoại lệ:** Không đủ bằng chứng từ chối có lý do; không tự quy mọi report thành vi phạm.

**Hậu điều kiện và điểm nghiệm thu:** Có lịch sử quyết định, không sửa occupancy hoặc thu tiền ngoài quyền để kết thúc report.

## SP-UC-039 Yêu cầu và xác nhận hoàn tiền

- **Tác nhân:** Customer yêu cầu; người có quyền duyệt.
- **Phân kỳ:** SRS dài hạn, cần chốt.
- **Hiện trạng:** YÊU CẦU; chưa có API/schema.
- **API:** ĐỀ XUẤT: refunds. Tra quyền/DTO ở [04 Endpoints](../api/endpoints.md).
- **Dữ liệu:** Khoản thu gốc, yêu cầu/duyệt/kết quả hoàn đề xuất.
- **Tiền điều kiện:** Giao dịch gốc hợp lệ, chính sách và quyền hoàn đã được duyệt.
- **Nguồn yêu cầu:** FR-PAY-07/08.
- **Quy tắc:** BR-PAY-05,BR-PAY-02; xem [03](business-rules.md).

**Luồng chính**

1. Tra giao dịch và phần còn hoàn được.
2. Gửi yêu cầu có lý do.
3. Người có quyền duyệt và xác nhận kết quả kênh thu.
4. Lưu giao dịch hoàn riêng, đối soát trạng thái.

**Ngoại lệ:** Vượt tiền/hoàn lặp bị chặn; timeout giữ chưa rõ, không xóa khoản thu gốc.

**Hậu điều kiện và điểm nghiệm thu:** Tổng hoàn hợp lệ không vượt căn cứ, báo cáo truy vết thu và hoàn riêng.

## SP-UC-040 Xem tổng quan và đối soát lịch sử

- **Tác nhân:** Admin, Manager đúng bãi; Customer lịch sử mình.
- **Phân kỳ:** Sprint 3 phần tổng quan, SRS phần báo cáo.
- **Hiện trạng:** MỘT PHẦN: dashboard/count/phiên có, báo cáo tài chính và audit API chưa đầy đủ.
- **API:** SP-API-040 `GET /api/v1/parking-lots`; SP-API-049 `GET /api/v1/parking-sessions/active`; SP-API-069 `GET /api/v1/staff-assignments`. Tra quyền/DTO ở [04 Endpoints](../api/endpoints.md).
- **Dữ liệu:** Bãi/ô/phiên/phân công; audit có bảng; tiền/report cần bổ sung.
- **Tiền điều kiện:** Đúng phạm vi; chọn khoảng giờ và nguồn dữ liệu rõ.
- **Nguồn yêu cầu:** FR-RPT-01/02/03; FR-AUD-01.
- **Quy tắc:** BR-RBAC-01,BR-DATA-02,BR-PAY-01; xem [03](business-rules.md).

**Luồng chính**

1. Tải số liệu đang có.
2. Đối chiếu tổng ô/phiên chờ/đã đỗ và khả dụng.
3. Tra lịch sử trong quyền khi có API.
4. Tách khoản thu/hoàn/gói khi phần tiền được triển khai.

**Ngoại lệ:** Không tạo doanh thu từ số xe hoặc KPI ngẫu nhiên; không coi dashboard hiện tại là báo cáo tài chính hoàn chỉnh.

**Hậu điều kiện và điểm nghiệm thu:** Mọi tổng có dữ liệu chi tiết giải thích, không lộ bãi khác hoặc tiền cá nhân.

## SP-UC-041 Khởi động lại sao lưu và phục hồi

- **Tác nhân:** Trí/người vận hành và QA được giao.
- **Phân kỳ:** Sprint 3.
- **Hiện trạng:** MỘT PHẦN: core lưu DB có; event/viewer bộ nhớ chưa bền và chưa có bằng chứng phục hồi thật.
- **API:** Không có endpoint nghiệp vụ backup; dùng quy trình môi trường được cấp. Tra quyền/DTO ở [04 Endpoints](../api/endpoints.md).
- **Dữ liệu:** DB/ảnh/log/phiên/tiền và cấu hình không chứa secret trong docs.
- **Tiền điều kiện:** Có DB thử riêng, quyền thực hiện và bản sao có nguồn xác nhận.
- **Nguồn yêu cầu:** NFR-REL-02/03; NFR-REC-01.
- **Quy tắc:** BR-DATA-01,BR-DATA-02,BR-EVT-01; xem [03](business-rules.md).

**Luồng chính**

1. Ghi mã/giờ/trạng thái chuẩn trước thử.
2. Sao lưu và khởi động lại/restore trên môi trường riêng.
3. Đọc lại dữ liệu qua API/UI.
4. Đối chiếu liên kết/ảnh/quyền và kết quả chống trùng.

**Ngoại lệ:** Không reset DB vận hành để diễn lại; nguồn ảnh mất phải báo sai lệch.

**Hậu điều kiện và điểm nghiệm thu:** Trạng thái đã xác nhận không mất hoặc phát sinh thêm xe/kỳ/tiền sau phục hồi.

## SP-UC-042 Thử simulator tách nguồn vận hành

- **Tác nhân:** Người kiểm thử được cấp.
- **Phân kỳ:** Công cụ kiểm thử, không thay nguồn thật Sprint 3.
- **Hiện trạng:** MÔ PHỎNG; API hiện còn public cần giới hạn trước triển khai.
- **API:** SP-API-018 `POST /api/IoT/events`; SP-API-019 `GET /api/IoT/telemetry`; SP-API-020 `GET /api/IoT/parked-cars`; SP-API-021 `POST /api/IoT/reset`; SP-API-067 `GET /parking-lots`; SP-API-065 `PATCH /api/ParkingSlots/{slotCode}/status`; SP-API-066 `PATCH /api/v1/parking-viewer/slots/{slotCode}/status`. Tra quyền/DTO ở [04 Endpoints](../api/endpoints.md).
- **Dữ liệu:** JSON/bộ nhớ/localStorage tùy module.
- **Tiền điều kiện:** Môi trường kiểm thử riêng, dữ liệu mô phỏng có nhãn và quyền.
- **Nguồn yêu cầu:** FR-DEMO-01/02; Sprint 3.
- **Quy tắc:** BR-DATA-01,BR-EVT-03; xem [03](business-rules.md).

**Luồng chính**

1. Chọn kịch bản mô phỏng.
2. Gửi event/đọc telemetry với đúng hợp đồng.
3. So sánh kết quả xử lý trong phạm vi mô phỏng.
4. Reset chỉ dữ liệu được phép ở môi trường riêng.

**Ngoại lệ:** Không dùng PATCH viewer đổi màu để chứng minh core đã check-in/out; không reset dữ liệu thật.

**Hậu điều kiện và điểm nghiệm thu:** Phân biệt rõ bằng chứng test mô phỏng với lượt vận hành đã lưu DB.

## Liên kết triển khai và nghiệm thu

Các hành trình theo vai trò và checkpoint kiểm tra nằm ở [07 Scenario paths](user-journeys.md). Bàn giao giữa Staff, DB, tiền và 3D nằm ở [08 Main flows](main-flows.md). Use case chưa có endpoint không được tính đạt chỉ vì đã có mô tả này. Khi thay scope, cập nhật nguồn quyết định và giữ lịch sử mã.
