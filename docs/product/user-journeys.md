# Hành trình màn hình và kịch bản Smart Parking

**Trạng thái:** Hiện hành cho hành trình actor và checkpoint tích hợp.
**Chủ sở hữu:** Product/UX và QA.
**Rà soát gần nhất:** 28/10/2026.
**Phạm vi:** Guest, Customer, Gate, Zone, Admin, Manager và System.

**Mốc đối chiếu:** `Sprint3/demo` tại `4bed0c8a929fe050d644aa4e07c0a1caa77d8d0e`, ngày 28/10/2026. Xem [README](../README.md) để biết nguồn, quy ước và trạng thái.

Hành trình được định danh theo tác nhân, có điểm vào, bàn giao, nhánh lỗi và checkpoint. Những tên màn hình đích chưa có được ghi rõ, không tự cấp số màn hình như thể đã thiết kế/triển khai. Đây là tài liệu sử dụng cho thiết kế UI và nghiệm thu, không phải kết quả các ca đã chạy.

## 1 Màn hình thực có ở React

| Đường dẫn khi API phục vụ web | Nội dung hiện có | Giới hạn |
|---|---|---|
| `/app/` | Home, giới thiệu và status API | Chưa là bản đồ Guest tra bãi hoàn chỉnh |
| `/app/login` | Đăng nhập | Không có Register/VerifyEmail route hiện tại |
| `/app/customer` | Bãi/tầng/slot | RequireCustomer; chưa có đặt chỗ UI |
| `/app/customer/profile` | Hồ sơ và xem/thêm/sửa xe | Không suy ra đã có đổi mật khẩu/xóa mọi dữ liệu |
| `/app/admin` | Tổng quan bãi/phiên/phân công | Form quản trị và tài chính chưa đủ |
| `/app/manager` | Bãi được phân công và phiên mở | Không mặc định có quyền phân công/hoàn/phạt |
| `/app/staff` | Bãi được phân công và phiên mở | Chưa tách hai màn hình thao tác theo nhiệm vụ |
| `/3d-viewer.html` | Viewer độc lập | Dữ liệu bộ nhớ/mẫu, chưa cùng nguồn core |

HTML cũ trong frontend/admin và frontend/client dùng dữ liệu localStorage. Đây không phải các màn hình API thật được cộng thêm vào danh mục đã triển khai; không mặc định đường dẫn phục vụ của chúng giống thư mục source.

## 2 Danh mục hành trình

| Mã | Hành trình | Use case |
|---|---|---|
| GUEST-1 | Xem bãi và trạng thái công khai | SP-UC-007, SP-UC-028 |
| GUEST-2 | Đến gửi vãng lai không tài khoản | SP-UC-015, SP-UC-016, SP-UC-018, SP-UC-024 |
| CUSTOMER-1 | Tạo tài khoản và xác minh | SP-UC-003, SP-UC-004, SP-UC-001 |
| CUSTOMER-2 | Hồ sơ và xe cá nhân | SP-UC-005, SP-UC-006 |
| CUSTOMER-3 | Xem slot trên web hiện tại | SP-UC-008 |
| CUSTOMER-4 | Đặt trước và đến nhận | SP-UC-030, SP-UC-032, SP-UC-018, SP-UC-024 |
| CUSTOMER-5 | Hủy hoặc theo dõi giữ hết hạn | SP-UC-031, SP-UC-039 |
| CUSTOMER-6 | Đăng ký gửi cố định và nhận thẻ | SP-UC-033, SP-UC-034 |
| CUSTOMER-7 | Sử dụng gia hạn và đổi xe cố định | SP-UC-035, SP-UC-036 |
| CUSTOMER-8 | Báo chiếm ô và theo dõi xử lý | SP-UC-037, SP-UC-038 |
| GATE-1 | Nhận xe với ảnh và OCR | SP-UC-025, SP-UC-026, SP-UC-015 |
| GATE-2 | Tra xe và thu phí | SP-UC-021, SP-UC-022, SP-UC-023 |
| GATE-3 | Xác nhận xe ra | SP-UC-024 |
| GATE-4 | Nhận khách đã đặt hoặc gói cố định | SP-UC-032, SP-UC-035 |
| ZONE-1 | Xử lý danh sách chờ và tín hiệu ô | SP-UC-016, SP-UC-027, SP-UC-017 |
| ZONE-2 | Xác nhận đúng hoặc sai ô | SP-UC-018, SP-UC-019 |
| ZONE-3 | Chuyển xe đã đỗ và tranh chấp | SP-UC-020, SP-UC-017, SP-UC-018 |
| ADMIN-1 | Thiết lập bãi sơ đồ và trạng thái | SP-UC-009, SP-UC-010, SP-UC-014 |
| ADMIN-2 | Cấp người và phạm vi nhiệm vụ | SP-UC-011, SP-UC-012, SP-UC-013 |
| ADMIN-3 | Đối soát và xử lý ngoài quyền Manager | SP-UC-038, SP-UC-039, SP-UC-040 |
| MANAGER-1 | Giám sát bãi được giao | SP-UC-014, SP-UC-021, SP-UC-040 |
| MANAGER-2 | Xác minh ngoại lệ và chứng từ | SP-UC-021, SP-UC-023, SP-UC-038, SP-UC-039 |
| SYSTEM-1 | Sự kiện lặp và đến sai thứ tự | SP-UC-027, SP-UC-016 |
| SYSTEM-2 | Mất mạng và hai thiết bị | SP-UC-029, SP-UC-028 |
| SYSTEM-3 | Sao lưu khởi động lại và phục hồi | SP-UC-041, SP-UC-040 |
| SYSTEM-4 | Mô phỏng có ranh giới | SP-UC-042 |

## GUEST-1 Xem bãi và trạng thái công khai

- **Use case:** SP-UC-007, SP-UC-028.
- **Điểm vào:** Guest mở trang công khai; màn hình tra cứu Guest đích chưa hoàn chỉnh trong React.
- **Đường đi:** Trang công khai → chọn bãi/tầng → danh sách/3D public → chọn dịch vụ cá nhân thì đăng nhập.
- **Nhánh quyết định:** GPS từ chối vẫn có cách tìm/chọn khác khi triển khai map; 3D lỗi có danh sách; không có dữ liệu báo rỗng.
- **Checkpoint QA:** Không có token vẫn đọc được public; response không có biển số/ảnh/tiền của người khác; xem không giảm chỗ.
- **API/dữ liệu:** ParkingLotsController.GetLots,ParkingLotsController.GetLotById,ParkingLotsController.GetLotCapacity,ParkingFloorsController.GetFloorsByLotId,ParkingSlotsController.GetSlotsByFloorId; ParkingViewerSlotsController.GetSlots,ParkingViewerSlotsController.GetSlotsByFloor. Tra mã/DTO/quyền ở [04](../api/endpoints.md).

## GUEST-2 Đến gửi vãng lai không tài khoản

- **Use case:** SP-UC-015, SP-UC-016, SP-UC-018, SP-UC-024.
- **Điểm vào:** Khách và xe tới cổng; không yêu cầu đăng ký.
- **Đường đi:** Khách tới cổng → camera/AI tạo nhận dạng tạm, cấp vé/thẻ và session → chờ điều phối → Staff tại ô xác minh xe/biển số/actual slot → xe tự tới cổng ra và quẹt vé/thẻ → hệ thống tính phí/thu tiền → xác nhận ra.
- **Nhánh quyết định:** Bãi đầy/xe trùng chuyển từ chối; thiếu vé/khó xác minh chuyển Manager.
- **Checkpoint QA:** Một lượt có mã phiên/vé và nguồn ảnh; xe chờ được tính vào sức chứa.
- **API/dữ liệu:** ParkingSessionsController.CheckIn; ParkingSessionsController.GetActiveSessions,ParkingDispatchesController.GetDispatches,ParkingEventController.GetActiveSessions; ParkingDispatchesController.ConfirmOccupied; ParkingSessionsController.CheckOut. Tra mã/DTO/quyền ở [04](../api/endpoints.md).

## CUSTOMER-1 Tạo tài khoản và xác minh

- **Use case:** SP-UC-003, SP-UC-004, SP-UC-001.
- **Điểm vào:** Guest chọn đăng ký; Register/VerifyEmail hiện chưa có route trong App.
- **Đường đi:** Form đăng ký đích → trạng thái gửi thư → mở Gmail → xác minh → đăng nhập.
- **Nhánh quyết định:** Trùng email/reCAPTCHA sai/mã hết hạn/dùng lại/giới hạn gửi lại; email lỗi không kích hoạt.
- **Checkpoint QA:** Có bằng chứng thư thật và link PC/mobile; không tự cấp quyền nội bộ.
- **API/dữ liệu:** ĐỀ XUẤT: POST /api/v1/auth/register; ĐỀ XUẤT: verify-email/resend-verification; AuthController.Login,AuthController.RefreshToken. Tra mã/DTO/quyền ở [04](../api/endpoints.md).

## CUSTOMER-2 Hồ sơ và xe cá nhân

- **Use case:** SP-UC-005, SP-UC-006.
- **Điểm vào:** USER mở /app/customer/profile.
- **Đường đi:** Tải hồ sơ/xe → sửa họ tên/điện thoại hoặc thêm/sửa xe → lưu API → reload đối chiếu.
- **Nhánh quyết định:** Xe người khác/xe vãng lai chưa xác minh bị chặn; lỗi form không mất nội dung đang sửa.
- **Checkpoint QA:** Chỉ dữ liệu mình đổi; không giả định UI đã có xóa xe/đổi biển số khi code chưa hỗ trợ.
- **API/dữ liệu:** UsersController.GetMyProfile,UsersController.UpdateMyProfile; VehiclesController.GetVehicles,VehiclesController.GetVehicleById,VehiclesController.GetVehicleByPlate,VehiclesController.CreateVehicle,VehiclesController.UpdateVehicle,VehiclesController.DeleteVehicle. Tra mã/DTO/quyền ở [04](../api/endpoints.md).

## CUSTOMER-3 Xem slot trên web hiện tại

- **Use case:** SP-UC-008.
- **Điểm vào:** USER mở /app/customer.
- **Đường đi:** Chọn bãi → chọn tầng → xem slot → đọc trạng thái và kết quả tải.
- **Nhánh quyết định:** Empty/loading/API lỗi riêng; thông báo đặt chỗ chưa ghép phải đúng thực tế backend.
- **Checkpoint QA:** Bốn trạng thái từ DB; không coi slot xem là được giữ riêng.
- **API/dữ liệu:** ParkingLotsController.GetLots,ParkingFloorsController.GetFloorsByLotId,ParkingSlotsController.GetSlotsByFloorId,ParkingSlotsController.GetSlotById. Tra mã/DTO/quyền ở [04](../api/endpoints.md).

## CUSTOMER-4 Đặt trước và đến nhận

- **Use case:** SP-UC-030, SP-UC-032, SP-UC-018, SP-UC-024.
- **Điểm vào:** Customer có xe; giao diện booking đích chưa ghép.
- **Đường đi:** Chọn bãi/khung giờ → tạo giữ/hoàn tất điều kiện → nhận mã → Staff kiểm tra → tạo phiên → điều phối → trả xe.
- **Nhánh quyết định:** Sai giờ/bãi/xe hoặc tiền chưa rõ không tự xác nhận; retry không sinh hai booking/phiên.
- **Checkpoint QA:** Core API có nhưng chưa đủ hạn giữ/tiền/khung giờ; tách bằng chứng đã triển khai và kịch bản đích.
- **API/dữ liệu:** BookingsController.CreateBooking,BookingsController.GetMyBookings,BookingsController.GetBookingById; ParkingSessionsController.CheckIn,BookingsController.GetBookingById; ParkingDispatchesController.ConfirmOccupied; ParkingSessionsController.CheckOut. Tra mã/DTO/quyền ở [04](../api/endpoints.md).

## CUSTOMER-5 Hủy hoặc theo dõi giữ hết hạn

- **Use case:** SP-UC-031, SP-UC-039.
- **Điểm vào:** Customer xem booking của mình; UI quản lý booking đích chưa ghép.
- **Đường đi:** Chi tiết đặt → hủy có lý do hoặc theo dõi hết hạn → xem quyền giữ đã bỏ → xem khoản cần đối soát.
- **Nhánh quyết định:** Đã CHECKED_IN không hủy như giữ chưa dùng; refund cần luồng riêng.
- **Checkpoint QA:** Xe đã đỗ không biến mất; scheduler hết hạn chưa có phải ghi rõ.
- **API/dữ liệu:** BookingsController.CancelBooking,BookingsController.GetMyBookings; ĐỀ XUẤT: refunds. Tra mã/DTO/quyền ở [04](../api/endpoints.md).

## CUSTOMER-6 Đăng ký gửi cố định và nhận thẻ

- **Use case:** SP-UC-033, SP-UC-034.
- **Điểm vào:** Customer có xe được xác minh, lựa chọn khu/ô và kỳ.
- **Đường đi:** Xem gói/ô → đăng ký → online hoặc chứng từ trực tiếp → đủ điều kiện → nhân viên nhận thông tin → phát thẻ lần đầu.
- **Nhánh quyết định:** Tiền/chứng từ chưa xác minh thì chưa active; phát thẻ trùng bị chặn; kỳ/giá chưa chốt báo thiếu.
- **Checkpoint QA:** Quyền ô/xe/kỳ khác xe đang đỗ; biết ai đã cấp và ai đã nhận thẻ.
- **API/dữ liệu:** ĐỀ XUẤT: parking-passes, plan và quyền ô; ĐỀ XUẤT: cấp thẻ và thông báo tiếp nhận. Tra mã/DTO/quyền ở [04](../api/endpoints.md).

## CUSTOMER-7 Sử dụng gia hạn và đổi xe cố định

- **Use case:** SP-UC-035, SP-UC-036.
- **Điểm vào:** Đã có quyền gửi/thẻ; toàn module hiện chưa triển khai.
- **Đường đi:** Quét/đối chiếu thẻ xe → vào đúng khu/ô → tạo phiên thực tế → ra/đối chiếu ngoài quyền → yêu cầu gia hạn/đổi xe nếu cần.
- **Nhánh quyết định:** Thẻ sai/xe khác/hết hạn/đang có phiên không mở lượt mới; thay đổi xe cần xác minh.
- **Checkpoint QA:** Mua/gia hạn không tự sinh xe; retry không cộng kỳ hai lần; bảng IoT vật lý của slot hiển thị đầy đủ biển số đăng ký và chỉ đổi theo quyền/xe đã xác minh có hiệu lực; Web App vẫn lọc thông tin xe theo actor.
- **API/dữ liệu:** ĐỀ XUẤT: kiểm tra thẻ/quyền và tạo phiên; ĐỀ XUẤT: pass renewals/change requests. Tra mã/DTO/quyền ở [04](../api/endpoints.md).

## CUSTOMER-8 Báo chiếm ô và theo dõi xử lý

- **Use case:** SP-UC-037, SP-UC-038.
- **Điểm vào:** Khách đang có quyền ô phát hiện xe khác chiếm.
- **Đường đi:** Chọn ô mình → gửi ảnh/mô tả → theo dõi đã nhận/đang xác minh → xem kết quả và thông báo.
- **Nhánh quyết định:** Không có quyền ô/ảnh lỗi/chưa đủ căn cứ phải có kết quả rõ; không tự tạo khoản phạt.
- **Checkpoint QA:** Report và bằng chứng đúng bãi/quyền, quyết định có người/lý do.
- **API/dữ liệu:** ĐỀ XUẤT: POST /api/v1/slot-reports; ĐỀ XUẤT: report review/resolve. Tra mã/DTO/quyền ở [04](../api/endpoints.md).

## GATE-1 Nhận xe với ảnh và OCR

- **Use case:** SP-UC-025, SP-UC-026, SP-UC-015.
- **Điểm vào:** Xe tới cổng; camera/AI và cơ chế cấp vé/thẻ hoạt động; Staff cổng giám sát ngoại lệ.
- **Đường đi:** Chụp/tải ảnh → OCR tạo nhận dạng tạm → kiểm tra sức chứa/chống trùng → cấp vé/thẻ và tạo session chờ xác minh → đưa vào queue.
- **Nhánh quyết định:** OCR sai/không đọc được chuyển `NEEDS_REVIEW` để Staff tại ô xác minh; check-in timeout tra lại bằng EventId/vé; Staff cổng chỉ can thiệp khi thiết bị hoặc luồng tự động lỗi.
- **Checkpoint QA:** Lưu ảnh/kết quả OCR gốc; không cần Staff cổng xác nhận ở luồng chuẩn; xe chưa xuất hiện tại slot khi mới nhận.
- **API/dữ liệu:** ImagesController.Upload,ImagesController.GetById,ImagesController.Delete; GeminiController.AnalyzeVehicle; FastAPI POST /api/v1/detect; ParkingSessionsController.CheckIn. Tra mã/DTO/quyền ở [04](../api/endpoints.md).

## GATE-2 Tra xe và thu phí

- **Use case:** SP-UC-021, SP-UC-022, SP-UC-023.
- **Điểm vào:** Xe tự tới cổng ra và vé/thẻ được quẹt; phí/khoản thu hiện chưa có module hoàn chỉnh.
- **Đường đi:** Quẹt vé/thẻ → tải phiên và ảnh/thông tin lúc vào → đối chiếu xe → máy chủ tính phí → ghi thực nhận/chứng từ → biên lai.
- **Nhánh quyết định:** Sai xe/vé chuyển xác minh; thiếu giá báo lỗi; thu timeout giữ chưa rõ và tra lại.
- **Checkpoint QA:** Không tin tiền client, không thu trùng; đủ tiền chưa làm trống ô.
- **API/dữ liệu:** ParkingSessionsController.GetActiveSessions,ParkingSessionsController.GetSessionById,ParkingSessionsController.GetActiveSessionByPlate,ParkingSessionsController.GetMySessions; ĐỀ XUẤT: GET /api/v1/parking-sessions/{id}/fee; ĐỀ XUẤT: session payments và receipt. Tra mã/DTO/quyền ở [04](../api/endpoints.md).

## GATE-3 Xác nhận xe ra

- **Use case:** SP-UC-024.
- **Điểm vào:** Xe đã tới cổng ra, quẹt vé/thẻ, tải đúng phiên và đủ điều kiện phí; code hiện chưa có đủ guard này.
- **Đường đi:** Đọc lại điều kiện → xác nhận xe ra thực tế → đóng phiên/thu vé/trả ô theo chính sách → đối chiếu hai màn hình.
- **Nhánh quyết định:** Thiếu phí hoặc chưa xác minh không hoàn tất; gửi lại không tạo thêm lần ra.
- **Checkpoint QA:** COMPLETED/ô/vé khớp cùng giao dịch và lịch sử; xe còn trong ô không được trả vì tiền.
- **API/dữ liệu:** ParkingSessionsController.CheckOut. Tra mã/DTO/quyền ở [04](../api/endpoints.md).

## GATE-4 Nhận khách đã đặt hoặc gói cố định

- **Use case:** SP-UC-032, SP-UC-035.
- **Điểm vào:** Có mã booking hoặc thẻ và thông tin xe.
- **Đường đi:** Tra quyền dịch vụ → kiểm tra xe/bãi/kỳ/tiền → nhận một phiên → điều phối thực tế.
- **Nhánh quyết định:** Booking sai giờ/dùng rồi hoặc pass hết hạn chuyển xử lý theo chính sách.
- **Checkpoint QA:** Không cộng hai xe cho một lượt; đăng ký dịch vụ khác với đã nhận xe thực tế.
- **API/dữ liệu:** ParkingSessionsController.CheckIn,BookingsController.GetBookingById; ĐỀ XUẤT: kiểm tra thẻ/quyền và tạo phiên. Tra mã/DTO/quyền ở [04](../api/endpoints.md).

## ZONE-1 Xử lý danh sách chờ và tín hiệu ô

- **Use case:** SP-UC-016, SP-UC-027, SP-UC-017.
- **Điểm vào:** Staff điều phối mở mobile đúng bãi.
- **Đường đi:** Tải session kèm ảnh/OCR tạm → tín hiệu slot xếp hàng thời gian → tới xác minh → đối chiếu/sửa biển số và thông tin xe → chọn actual slot → xác nhận OCCUPIED.
- **Nhánh quyết định:** Cảm biến chỉ thấy vật thể chưa biết xe; source cũ và mapping sai phải báo xác minh.
- **Checkpoint QA:** Hàng việc đã xử lý đổi trạng thái nhưng không bị xóa; xe chờ không mất giữa nguồn core/event và sửa OCR không tạo session mới.
- **API/dữ liệu:** ParkingSessionsController.GetActiveSessions,ParkingDispatchesController.GetDispatches,ParkingEventController.GetActiveSessions; ParkingEventController.IngestEvent,ParkingEventController.GetEventLogs; ParkingDispatchesController.CreateDispatch,ParkingDispatchesController.ReassignSlot. Tra mã/DTO/quyền ở [04](../api/endpoints.md).

## ZONE-2 Xác nhận đúng hoặc sai ô

- **Use case:** SP-UC-018, SP-UC-019.
- **Điểm vào:** Có phiên và lệnh ASSIGNED cùng bãi.
- **Đường đi:** Quan sát xe/ảnh/OCR tạm → xác nhận hoặc sửa biển số/thông tin xe → đúng ô thì confirm → sai mục tiêu thì report-wrong theo ô thực tế và lý do.
- **Nhánh quyết định:** Ô vừa có xe khác/lệnh đã kết thúc bị chặn; không gọi cả confirm rồi wrong trên cùng lệnh.
- **Checkpoint QA:** biển số chính thức/actual_slot/physical/phiên cùng kết quả; có giá trị OCR gốc, actor/time và thay đổi 3D đúng.
- **API/dữ liệu:** ParkingDispatchesController.ConfirmOccupied; ParkingDispatchesController.ReportWrongSlot. Tra mã/DTO/quyền ở [04](../api/endpoints.md).

## ZONE-3 Chuyển xe đã đỗ và tranh chấp

- **Use case:** SP-UC-020, SP-UC-017, SP-UC-018.
- **Điểm vào:** Xe đã OCCUPIED cần chuyển hoặc hai người cùng nhận một ô.
- **Đường đi:** Yêu cầu chuyển có lý do → kiểm tra/khóa ô mới → xác nhận rời/đến → cập nhật lịch sử.
- **Nhánh quyết định:** Reassign hiện tại không hỗ trợ OCCUPIED/WRONG_SLOT; hai request cùng ô chỉ một được nhận.
- **Checkpoint QA:** Không dùng thao tác đổi màu để che xung đột; kiểm thử trên PostgreSQL hai kết nối.
- **API/dữ liệu:** ĐỀ XUẤT: nghiệp vụ relocation cho phiên đã đỗ; ParkingDispatchesController.CreateDispatch,ParkingDispatchesController.ReassignSlot; ParkingDispatchesController.ConfirmOccupied. Tra mã/DTO/quyền ở [04](../api/endpoints.md).

## ADMIN-1 Thiết lập bãi sơ đồ và trạng thái

- **Use case:** SP-UC-009, SP-UC-010, SP-UC-014.
- **Điểm vào:** Admin mở /app/admin; hiện là dashboard, form thiết lập cần bổ sung.
- **Đường đi:** Tạo bãi → tầng/ô/tọa độ → đối chiếu sơ đồ → cấu hình hoạt động/bảo trì có lý do.
- **Nhánh quyết định:** Mã trùng/tham chiếu đang dùng bị chặn; không thêm dữ liệu vận hành tự sinh.
- **Checkpoint QA:** Số ô/vị trí/mã đúng DB và 3D, đóng nhận không xóa xe còn trong bãi.
- **API/dữ liệu:** ParkingLotsController.CreateLot,ParkingLotsController.UpdateLot,ParkingLotsController.DeleteLot; ParkingFloorsController.CreateFloor,ParkingFloorsController.UpdateFloor,ParkingFloorsController.DeleteFloor,ParkingSlotsController.CreateSlot,ParkingSlotsController.UpdateSlot,ParkingSlotsController.DeleteSlot; ParkingLotsController.UpdateLotStatus,ParkingSlotsController.UpdateSlotPhysicalStatus. Tra mã/DTO/quyền ở [04](../api/endpoints.md).

## ADMIN-2 Cấp người và phạm vi nhiệm vụ

- **Use case:** SP-UC-011, SP-UC-012, SP-UC-013.
- **Điểm vào:** Admin có quyền quản trị; form ghi chưa đủ.
- **Đường đi:** Tạo nội bộ → cấp bãi → cấp cổng/điều phối riêng khi bổ sung → kiểm tra bằng tài khoản nhận → thu hồi.
- **Nhánh quyết định:** Bảo vệ Admin cuối; Staff không tự cấp; quyền vừa thu hồi cần bị chặn tại API.
- **Checkpoint QA:** Gọi sai bãi/nhiệm vụ không thành công dù sửa UI/body.
- **API/dữ liệu:** AdminUsersController.GetUsers,AdminUsersController.GetUserById,AdminUsersController.CreateUser,AdminUsersController.UpdateUserStatus,AdminUsersController.UpdateUserRole; StaffAssignmentsController.CreateAssignment,StaffAssignmentsController.GetAssignments,StaffAssignmentsController.GetMyAssignedLots,StaffAssignmentsController.GetAssignmentById,StaffAssignmentsController.RevokeAssignment; ĐỀ XUẤT: staff assignment duties. Tra mã/DTO/quyền ở [04](../api/endpoints.md).

## ADMIN-3 Đối soát và xử lý ngoài quyền Manager

- **Use case:** SP-UC-038, SP-UC-039, SP-UC-040.
- **Điểm vào:** Admin hoặc người được cấp chức năng đặc biệt.
- **Đường đi:** Tra bãi/phiên/khoản tiền/report → xem căn cứ → quyết định trong quyền → lưu lý do/kết quả.
- **Nhánh quyết định:** Báo cáo tài chính/refund/report hiện chưa có đầy đủ; không thay bằng doanh thu demo.
- **Checkpoint QA:** Mọi tổng có nguồn chi tiết; không xóa giao dịch gốc hay tự tạo phạt.
- **API/dữ liệu:** ĐỀ XUẤT: report review/resolve; ĐỀ XUẤT: refunds; ParkingLotsController.GetLots,ParkingSessionsController.GetActiveSessions,StaffAssignmentsController.GetAssignments. Tra mã/DTO/quyền ở [04](../api/endpoints.md).

## MANAGER-1 Giám sát bãi được giao

- **Use case:** SP-UC-014, SP-UC-021, SP-UC-040.
- **Điểm vào:** MANAGER mở /app/manager.
- **Đường đi:** Xem my-lots và phiên mở → đối chiếu chờ/đã đỗ → đóng/mở hoặc bảo trì theo quyền khi có UI.
- **Nhánh quyết định:** Không thấy bãi ngoài phân công; đóng nhận vẫn xử lý xe cũ.
- **Checkpoint QA:** Không suy quyền xem thành quyền phân công/hoàn/phạt hoặc toàn hệ thống.
- **API/dữ liệu:** ParkingLotsController.UpdateLotStatus,ParkingSlotsController.UpdateSlotPhysicalStatus; ParkingSessionsController.GetActiveSessions,ParkingSessionsController.GetSessionById,ParkingSessionsController.GetActiveSessionByPlate,ParkingSessionsController.GetMySessions; ParkingLotsController.GetLots,ParkingSessionsController.GetActiveSessions,StaffAssignmentsController.GetAssignments. Tra mã/DTO/quyền ở [04](../api/endpoints.md).

## MANAGER-2 Xác minh ngoại lệ và chứng từ

- **Use case:** SP-UC-021, SP-UC-023, SP-UC-038, SP-UC-039.
- **Điểm vào:** Staff chuyển sai xe/vé, khoản tiền chưa rõ hoặc report.
- **Đường đi:** Nhận bằng chứng → kiểm tra quyền → xác minh hoặc chuyển Admin → trả kết quả có lịch sử.
- **Nhánh quyết định:** Quyền duyệt tiền gói/hoàn/phạt chưa chốt thì đóng mặc định; Staff điều phối không ghi thu.
- **Checkpoint QA:** Biết người xác nhận, nguyên nhân và liên kết giao dịch/phiên/report.
- **API/dữ liệu:** ParkingSessionsController.GetActiveSessions,ParkingSessionsController.GetSessionById,ParkingSessionsController.GetActiveSessionByPlate,ParkingSessionsController.GetMySessions; ĐỀ XUẤT: session payments và receipt; ĐỀ XUẤT: report review/resolve; ĐỀ XUẤT: refunds. Tra mã/DTO/quyền ở [04](../api/endpoints.md).

## SYSTEM-1 Sự kiện lặp và đến sai thứ tự

- **Use case:** SP-UC-027, SP-UC-016.
- **Điểm vào:** Nguồn cổng/slot gửi sự kiện có EventId.
- **Đường đi:** Nhận → kiểm tra trùng/danh tính/phiên → nghiệp vụ core → lưu log → phát/tải snapshot.
- **Nhánh quyết định:** Retry giữ ID; sự kiện cũ hoặc slot không khớp không ghi đè mù.
- **Checkpoint QA:** Chống lặp phải bền qua restart; hiện repository bộ nhớ chưa đạt tiêu chí này.
- **API/dữ liệu:** ParkingEventController.IngestEvent,ParkingEventController.GetEventLogs; ParkingSessionsController.GetActiveSessions,ParkingDispatchesController.GetDispatches,ParkingEventController.GetActiveSessions. Tra mã/DTO/quyền ở [04](../api/endpoints.md).

## SYSTEM-2 Mất mạng và hai thiết bị

- **Use case:** SP-UC-029, SP-UC-028.
- **Điểm vào:** PC/mobile đang xem cùng bãi và một thao tác ghi bị ngắt.
- **Đường đi:** Giữ dữ liệu cuối/timestamp → báo chưa đồng bộ → tra kết quả thao tác → tải snapshot mới khi nối lại.
- **Nhánh quyết định:** Không tạo success giả; source/DB/AI lỗi có phản hồi; WebGL lỗi dùng danh sách.
- **Checkpoint QA:** Hai thiết bị cùng version/trạng thái sau phục hồi; không nhân đôi xe/tiền.
- **API/dữ liệu:** ĐỀ XUẤT: đọc snapshot core và tra kết quả thao tác; ParkingViewerSlotsController.GetSlots,ParkingViewerSlotsController.GetSlotsByFloor. Tra mã/DTO/quyền ở [04](../api/endpoints.md).

## SYSTEM-3 Sao lưu khởi động lại và phục hồi

- **Use case:** SP-UC-041, SP-UC-040.
- **Điểm vào:** DB thử riêng có lượt thực tế và bảng đối chiếu trước thử.
- **Đường đi:** Ghi mã/giờ → backup/restart/restore → đọc API/UI → đối chiếu phiên/ô/ảnh/tiền/quyền.
- **Nhánh quyết định:** Không ghi đè vận hành; ảnh ngoài DB cần kiểm tra tồn tại/quyền sau restore.
- **Checkpoint QA:** Lịch sử bền, không tự thêm kỳ/thu hoặc mất xe chờ.
- **API/dữ liệu:** Không có endpoint nghiệp vụ backup; dùng quy trình môi trường được cấp; ParkingLotsController.GetLots,ParkingSessionsController.GetActiveSessions,StaffAssignmentsController.GetAssignments. Tra mã/DTO/quyền ở [04](../api/endpoints.md).

## SYSTEM-4 Mô phỏng có ranh giới

- **Use case:** SP-UC-042.
- **Điểm vào:** Người kiểm thử dùng nguồn mô phỏng tách biệt.
- **Đường đi:** Chạy simulator → kiểm tra telemetry/bộ nhớ → ghi kết quả trong phạm vi test → reset chỉ nguồn được phép.
- **Nhánh quyết định:** Không dùng dữ liệu tự sinh hoặc reset để chứng minh hoàn tất nghiệp vụ Sprint 3.
- **Checkpoint QA:** Báo cáo QA phân biệt bằng chứng mô phỏng và dữ liệu nguồn thật.
- **API/dữ liệu:** IoTController.IngestEvent,IoTController.GetTelemetryHistory,IoTController.GetParkedCars,IoTController.Reset,SimulatorParkingLotsController.GetAll,ParkingViewerSlotsController.UpdateSlotStatus. Tra mã/DTO/quyền ở [04](../api/endpoints.md).

## 3 Bàn giao giữa tác nhân

| Bên gửi | Bên nhận | Dữ liệu tối thiểu | Điều kiện hoàn tất bàn giao |
|---|---|---|---|
| Cổng | Điều phối | Mã phiên core, bãi, xe/ảnh trong quyền, thời điểm vào, trạng thái chờ | Phiên được lưu; xe chờ không thành xe tại ô |
| Cảm biến | Điều phối | Mã sự kiện, slot/bãi, nguồn và time | Có việc cần xác minh; tín hiệu không tự gán xe |
| Điều phối | Cổng và 3D | Phiên/actual slot, người/time/lý do, trạng thái đã lưu | Core chấp nhận; danh sách/3D cùng nguồn |
| Phí | Cổng và check-out | Giá/time áp dụng, phí, đã thu/còn thiếu, trạng thái đối soát | Có căn cứ server; không tin tiền UI |
| Staff thu trực tiếp gói | Manager/người được cấp | Khách/xe/quyền kỳ, khoản tiền, ảnh chứng từ, người ghi | Xác nhận theo phân cấp đã chốt, không tự gộp ghi và duyệt |
| Customer quyền cố định | Nhân viên phát thẻ | Quyền ô/xe/kỳ, điều kiện đủ, trạng thái chưa phát | Nhân viên thực tế bàn giao, lưu actor/time |
| Customer report | Staff/Manager đúng bãi | Quyền ô, ảnh/mô tả/time, mã report | Đã nhận/xử lý có trạng thái, không tự phạt |
| Khôi phục | QA | Snapshot trước/sau, mã phiên/ô/ảnh/tiền, log sai lệch | Dữ liệu bền được đối chiếu, không chỉ tiến trình báo Healthy |

## 4 Ma trận API tới use case và hành trình

82 dòng dưới đây bao gồm alias hiện có. Mapping là mục đích kiểm thử/tích hợp của bộ docs, không chứng nhận API đã được ghép vào màn hình. API nhận diện FastAPI, minimal health/gateway nằm ngoài danh sách 82 ở [04](../api/endpoints.md).

| Mã API | Method/path | Use case | Hành trình liên quan |
|---|---|---|---|
| SP-API-001 | `GET /api/v1/admin/users` | SP-UC-011 | ADMIN-2 |
| SP-API-002 | `GET /api/v1/admin/users/{id:long}` | SP-UC-011 | ADMIN-2 |
| SP-API-003 | `POST /api/v1/admin/users` | SP-UC-011 | ADMIN-2 |
| SP-API-004 | `PATCH /api/v1/admin/users/{id:long}/status` | SP-UC-011 | ADMIN-2 |
| SP-API-005 | `PATCH /api/v1/admin/users/{id:long}/role` | SP-UC-011 | ADMIN-2 |
| SP-API-006 | `POST /api/v1/auth/login` | SP-UC-001 | CUSTOMER-1 |
| SP-API-007 | `POST /api/v1/auth/refresh` | SP-UC-001 | CUSTOMER-1 |
| SP-API-008 | `POST /api/v1/auth/logout` | SP-UC-002 |  |
| SP-API-009 | `POST /api/v1/bookings` | SP-UC-030 | CUSTOMER-4 |
| SP-API-010 | `GET /api/v1/bookings/my` | SP-UC-030, SP-UC-031 | CUSTOMER-4, CUSTOMER-5 |
| SP-API-011 | `GET /api/v1/bookings/{id:long}` | SP-UC-030, SP-UC-032 | CUSTOMER-4, GATE-4 |
| SP-API-012 | `POST /api/v1/bookings/{id:long}/cancel` | SP-UC-031 | CUSTOMER-5 |
| SP-API-013 | `GET /api/v1/bookings` | SP-UC-030 | CUSTOMER-4 |
| SP-API-014 | `POST /api/gemini/analyze` | SP-UC-026 | GATE-1 |
| SP-API-015 | `POST /api/v1/images` | SP-UC-025 | GATE-1 |
| SP-API-016 | `GET /api/v1/images/{id:long}` | SP-UC-025 | GATE-1 |
| SP-API-017 | `DELETE /api/v1/images/{id:long}` | SP-UC-025 | GATE-1 |
| SP-API-018 | `POST /api/IoT/events` | SP-UC-042 | SYSTEM-4 |
| SP-API-019 | `GET /api/IoT/telemetry` | SP-UC-042 | SYSTEM-4 |
| SP-API-020 | `GET /api/IoT/parked-cars` | SP-UC-042 | SYSTEM-4 |
| SP-API-021 | `POST /api/IoT/reset` | SP-UC-042 | SYSTEM-4 |
| SP-API-022 | `POST /api/v1/parking-dispatches` | SP-UC-017 | ZONE-1, ZONE-3 |
| SP-API-023 | `GET /api/v1/parking-dispatches` | SP-UC-016 | GUEST-2, ZONE-1, SYSTEM-1 |
| SP-API-024 | `GET /api/v1/parking-dispatches/{id:long}` | SP-UC-017 | ZONE-1, ZONE-3 |
| SP-API-025 | `GET /api/v1/parking-dispatches/session/{sessionId:long}` | SP-UC-017 | ZONE-1, ZONE-3 |
| SP-API-026 | `POST /api/v1/parking-dispatches/{id:long}/assign` | SP-UC-017 | ZONE-1, ZONE-3 |
| SP-API-027 | `POST /api/v1/parking-dispatches/{id:long}/confirm-occupied` | SP-UC-018 | GUEST-2, CUSTOMER-4, ZONE-2, ZONE-3 |
| SP-API-028 | `POST /api/v1/parking-dispatches/{id:long}/report-wrong-slot` | SP-UC-019 | ZONE-2 |
| SP-API-029 | `POST /api/v1/parking-dispatches/{id:long}/cancel` | SP-UC-017 | ZONE-1, ZONE-3 |
| SP-API-030 | `POST /api/events` | SP-UC-027 | ZONE-1, SYSTEM-1 |
| SP-API-031 | `GET /api/events/sessions/active` | SP-UC-016 | GUEST-2, ZONE-1, SYSTEM-1 |
| SP-API-032 | `GET /api/events/logs` | SP-UC-027 | ZONE-1, SYSTEM-1 |
| SP-API-033 | `GET /api/v1/parking-floors/lot/{lotId:int}` | SP-UC-007, SP-UC-008 | GUEST-1, CUSTOMER-3 |
| SP-API-034 | `GET /api/v1/parking-lots/{lotId:int}/floors` | SP-UC-007, SP-UC-008 | GUEST-1, CUSTOMER-3 |
| SP-API-035 | `GET /api/v1/parking-floors/{id:int}` | SP-UC-010 | ADMIN-1 |
| SP-API-036 | `POST /api/v1/parking-floors/lot/{lotId:int}` | SP-UC-010 | ADMIN-1 |
| SP-API-037 | `POST /api/v1/parking-lots/{lotId:int}/floors` | SP-UC-010 | ADMIN-1 |
| SP-API-038 | `PUT /api/v1/parking-floors/{id:int}` | SP-UC-010 | ADMIN-1 |
| SP-API-039 | `DELETE /api/v1/parking-floors/{id:int}` | SP-UC-010 | ADMIN-1 |
| SP-API-040 | `GET /api/v1/parking-lots` | SP-UC-007, SP-UC-008, SP-UC-040 | GUEST-1, CUSTOMER-3, ADMIN-3, MANAGER-1, SYSTEM-3 |
| SP-API-041 | `GET /api/v1/parking-lots/{id:int}` | SP-UC-007 | GUEST-1 |
| SP-API-042 | `GET /api/v1/parking-lots/{id:int}/capacity` | SP-UC-007 | GUEST-1 |
| SP-API-043 | `POST /api/v1/parking-lots` | SP-UC-009 | ADMIN-1 |
| SP-API-044 | `PUT /api/v1/parking-lots/{id:int}` | SP-UC-009 | ADMIN-1 |
| SP-API-045 | `PATCH /api/v1/parking-lots/{id:int}/status` | SP-UC-014 | ADMIN-1, MANAGER-1 |
| SP-API-046 | `DELETE /api/v1/parking-lots/{id:int}` | SP-UC-009 | ADMIN-1 |
| SP-API-047 | `POST /api/v1/parking-sessions/check-in` | SP-UC-015, SP-UC-032 | GUEST-2, CUSTOMER-4, GATE-1, GATE-4 |
| SP-API-048 | `POST /api/v1/parking-sessions/{id:long}/check-out` | SP-UC-024 | GUEST-2, CUSTOMER-4, GATE-3 |
| SP-API-049 | `GET /api/v1/parking-sessions/active` | SP-UC-016, SP-UC-021, SP-UC-040 | GUEST-2, GATE-2, ZONE-1, ADMIN-3, MANAGER-1, MANAGER-2, SYSTEM-1, SYSTEM-3 |
| SP-API-050 | `GET /api/v1/parking-sessions/my-sessions` | SP-UC-021 | GATE-2, MANAGER-1, MANAGER-2 |
| SP-API-051 | `GET /api/v1/parking-sessions/{id:long}` | SP-UC-021 | GATE-2, MANAGER-1, MANAGER-2 |
| SP-API-052 | `GET /api/v1/parking-sessions/by-plate/{plateNumber}/active` | SP-UC-021 | GATE-2, MANAGER-1, MANAGER-2 |
| SP-API-053 | `GET /api/v1/parking-slots/floor/{floorId:int}` | SP-UC-007, SP-UC-008 | GUEST-1, CUSTOMER-3 |
| SP-API-054 | `GET /api/v1/parking-floors/{floorId:int}/slots` | SP-UC-007, SP-UC-008 | GUEST-1, CUSTOMER-3 |
| SP-API-055 | `GET /api/v1/parking-slots/{id:long}` | SP-UC-008 | CUSTOMER-3 |
| SP-API-056 | `POST /api/v1/parking-slots/floor/{floorId:int}` | SP-UC-010 | ADMIN-1 |
| SP-API-057 | `POST /api/v1/parking-floors/{floorId:int}/slots` | SP-UC-010 | ADMIN-1 |
| SP-API-058 | `PUT /api/v1/parking-slots/{id:long}` | SP-UC-010 | ADMIN-1 |
| SP-API-059 | `PATCH /api/v1/parking-slots/{id:long}/status` | SP-UC-014 | ADMIN-1, MANAGER-1 |
| SP-API-060 | `DELETE /api/v1/parking-slots/{id:long}` | SP-UC-010 | ADMIN-1 |
| SP-API-061 | `GET /api/ParkingSlots` | SP-UC-028 | GUEST-1, SYSTEM-2 |
| SP-API-062 | `GET /api/v1/parking-viewer/slots` | SP-UC-028 | GUEST-1, SYSTEM-2 |
| SP-API-063 | `GET /api/ParkingSlots/floor/{floorId}` | SP-UC-028 | GUEST-1, SYSTEM-2 |
| SP-API-064 | `GET /api/v1/parking-viewer/floors/{floorId}/slots` | SP-UC-028 | GUEST-1, SYSTEM-2 |
| SP-API-065 | `PATCH /api/ParkingSlots/{slotCode}/status` | SP-UC-042 | SYSTEM-4 |
| SP-API-066 | `PATCH /api/v1/parking-viewer/slots/{slotCode}/status` | SP-UC-042 | SYSTEM-4 |
| SP-API-067 | `GET /parking-lots` | SP-UC-042 | SYSTEM-4 |
| SP-API-068 | `POST /api/v1/staff-assignments` | SP-UC-012 | ADMIN-2 |
| SP-API-069 | `GET /api/v1/staff-assignments` | SP-UC-012, SP-UC-040 | ADMIN-2, ADMIN-3, MANAGER-1, SYSTEM-3 |
| SP-API-070 | `GET /api/v1/staff-assignments/my-lots` | SP-UC-012 | ADMIN-2 |
| SP-API-071 | `GET /api/v1/staff-assignments/{id:long}` | SP-UC-012 | ADMIN-2 |
| SP-API-072 | `POST /api/v1/staff-assignments/{id:long}/revoke` | SP-UC-012 | ADMIN-2 |
| SP-API-073 | `GET /api/System/info` | SP-UC-040 | ADMIN-3, MANAGER-1, SYSTEM-3 |
| SP-API-074 | `GET /api/System/health` | SP-UC-040 | ADMIN-3, MANAGER-1, SYSTEM-3 |
| SP-API-075 | `GET /api/v1/users/me` | SP-UC-005 | CUSTOMER-2 |
| SP-API-076 | `PUT /api/v1/users/me` | SP-UC-005 | CUSTOMER-2 |
| SP-API-077 | `GET /api/v1/vehicles` | SP-UC-006 | CUSTOMER-2 |
| SP-API-078 | `GET /api/v1/vehicles/{id:long}` | SP-UC-006 | CUSTOMER-2 |
| SP-API-079 | `GET /api/v1/vehicles/by-plate/{plateNumber}` | SP-UC-006 | CUSTOMER-2 |
| SP-API-080 | `POST /api/v1/vehicles` | SP-UC-006 | CUSTOMER-2 |
| SP-API-081 | `PUT /api/v1/vehicles/{id:long}` | SP-UC-006 | CUSTOMER-2 |
| SP-API-082 | `DELETE /api/v1/vehicles/{id:long}` | SP-UC-006 | CUSTOMER-2 |

## 5 Ghi nhận kết quả nghiệm thu

Mỗi lần chạy ghi ngày/giờ Việt Nam, môi trường/commit, tài khoản và nhiệm vụ, thiết bị/trình duyệt, nguồn ảnh/sự kiện, mã bãi/ô/phiên/vé/khoản thu và kết quả mong đợi/thực tế. Không lưu credential vào bằng chứng. Ca chưa có module hoặc chưa có nguồn thật ghi CHƯA ĐẠT/CHƯA CHẠY kèm lý do; không đổi thành đạt nhờ mock.

Ưu tiên chuỗi GUEST-2 → GATE-1 → ZONE-1/2 → GATE-2/3, đồng thời SYSTEM-2/3 để chứng minh lưu bền/đồng bộ. Tài khoản/Guest/quyền và các yêu cầu Sprint 3 khác vẫn cần bằng chứng riêng. Booking và khu cố định dùng để thiết kế, không trình bày là chức năng hoàn tất hiện tại.
