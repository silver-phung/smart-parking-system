# Staff workspace — Sprint 3

Giao diện React dành cho nhân viên cổng và nhân viên xác minh ô, bổ sung trên baseline `85fe7ac`. Source chính: `frontend/smart-parking/src/pages/StaffWorkspace.jsx`. Đây là phần frontend nối các API hiện có; không thay schema hoặc quy tắc backend.

## Mở giao diện

Tại thư mục gốc repository:

```powershell
npm --prefix frontend/smart-parking ci
npm --prefix frontend/smart-parking run build
dotnet run --project backend/src/SmartParking.Api --launch-profile http
```

Mở `http://localhost:5295/app/login`, đăng nhập bằng tài khoản STAFF đã được Admin phân công bãi, sau đó vào `/app/staff`. Khi phát triển riêng, `npm --prefix frontend/smart-parking run dev` chạy web ở cổng 5173 và proxy `/api` về backend 5295.

API chính cần database core Neon đã migrate, trong đó có cột `staff_lot_assignments.duty` và migration `StoreImageContentInDatabase`. Ảnh mới lưu trực tiếp trong PostgreSQL, không cần Cloudinary; dung lượng database sẽ tăng theo số ảnh. Ảnh Cloudinary cũ vẫn giữ URL cũ, việc xóa ảnh cũ vẫn cần cấu hình Cloudinary. Nhận diện ảnh cần FastAPI ở địa chỉ `AiService:BaseUrl`, database AI đã migrate và cấu hình `AiRecognitionConnection`. Nhập biển số không kèm ảnh vẫn dùng được ở luồng cho phép không có ảnh.

Từ thư mục gốc repository, áp dụng schema core bằng `dotnet ef database update --project backend\src\SmartParking.Infrastructure --startup-project backend\src\SmartParking.Api --context SmartParkingDbContext`, sau khi kiểm tra kết nối database đích. Khởi động lại backend sau khi triển khai mã mới.

Khách xem booking đã tạo trong mục **Đặt chỗ của tôi** trên dashboard; Staff xem booking PENDING của bãi trong trang **Xe đặt qua web**. Dashboard khách lấy sức chứa từ API capacity (đã trừ booking đang giữ) và tự cập nhật sức chứa/trạng thái slot mỗi 15 giây. Booking giữ sức chứa nhưng không chỉ định trước ô vật lý; ô chỉ thành OCCUPIED sau khi Staff xác minh xe đã đỗ.

## Các khu chức năng

- **Xe vào vãng lai:** gồm các bước **Nhận xe tại cổng**, **Xác minh ô** và **Theo dõi điều phối**. Tại cổng, chọn/chụp ảnh JPEG/PNG/WebP tối đa 10 MB hoặc nhập tay. AI chỉ gợi ý biển số. Khi sửa kết quả AI phải có lý do; xác nhận kết quả nhận diện và lưu ảnh trước khi gửi check-in. Check-in không truyền SlotId. Thành công hiển thị mã phiên; thất bại giữ form để xử lý, không báo thành công giả.
- **Xe đặt chỗ qua web:** màn hình tải toàn bộ booking `PENDING` của bãi đang chọn, gồm biển số, tên/tài khoản, email, số điện thoại và thời điểm dự kiến vào. Danh sách tự kiểm tra mỗi 15 giây; booking mới được làm nổi bật và thông báo ngay trên màn hình. Thông tin liên hệ chỉ trả về trong API staff đã phân quyền theo bãi. Khi xe đã vào bãi và đỗ, Staff chọn **Xác nhận xe đã đỗ**, chọn ô thực tế, chụp ảnh và nhập thủ công biển số thực tế để đối chiếu với booking; bước này không gọi AI. Backend tạo phiên ACTIVE gắn với booking/ô, đặt `CheckInTime` đúng lúc Staff xác nhận, lưu ảnh, chuyển booking sang `CHECKED_IN` và ô sang `OCCUPIED` trong cùng giao dịch. Ô đã có phiên không thể được xác nhận cho xe khác, và sức chứa còn trống không tính ô đã sử dụng. Thời lượng phiên được tính từ `CheckInTime`; mức phí/thanh toán tự động chưa được tích hợp. Xe booking không cần check-in ở cổng; luồng xe vãng lai ở tab **Xe vào** vẫn giữ nguyên.
- **Xe ra:** gồm các bước **Xác nhận xe ra** và **Tra cứu xe đang gửi**. Tra cứu biển số hoặc mã phiên trong bãi đang chọn; xem thông tin, ảnh vào và khoản phí đã ghi nhận. Nhập/nhận diện biển số thực tế tại cổng ra, đối chiếu rồi xác nhận xe thực sự qua cổng. Chỉ gửi check-out cho phiên PAID; đọc lại phiên và kiểm tra biển số trước khi gửi. Không gửi quyền miễn trừ thanh toán.
- Sau khi xác nhận booking đã đỗ thành công, giao diện chuyển tới **Xe ra** và mở phiên vừa tạo. Đây chỉ là màn hình theo dõi; xe vẫn ở trong bãi và chưa được thanh toán hoặc check-out. Khi xe thực sự ra, Staff cần hoàn tất thanh toán qua hệ thống được tích hợp trước khi xác nhận xe ra.
- **Xác minh ô — xe vãng lai:** chọn phiên xe đã nhận ở cổng, xem ảnh cổng nếu có, nhập biển số thực tế để đối chiếu, chọn ô xe đã đỗ và xác nhận. Nếu phiên cũ không có ảnh cổng, giao diện cảnh báo để Staff kiểm tra trực tiếp và liên hệ nhân viên cổng. Có thể phân ô mục tiêu hoặc báo sai ô theo quy trình hiện có. Ô có xe chưa ghép phiên có danh sách riêng. UI loại ô không khả dụng và backend đọc/khóa lại dữ liệu trước khi ghi.
- **Xe đang gửi:** tìm biển số, phân trang, xem chi tiết phiên và ảnh; chuyển nhanh sang cổng ra hoặc xác minh ô.
- **Điều phối:** lọc/phân trang cả điều phối chờ và đã xử lý, giữ kết quả OCCUPIED/WRONG_SLOT/CANCELLED để tra cứu. Hủy điều phối cần nhập lý do và bấm xác nhận riêng.

Nút **Làm mới** tải lại số liệu bãi và danh sách của khu chức năng đang mở. Sau thao tác thành công dữ liệu liên quan được tải lại. Đổi bãi xóa bản nháp đang thao tác; không cho đổi bãi hoặc chuyển bước trong trang trong lúc nhận diện/gửi thao tác.

## Phạm vi và giới hạn tích hợp

- `my-lots` chưa trả duty của Staff theo từng bãi. UI cung cấp các khu nghiệp vụ; server vẫn kiểm tra GATE/SLOT_VERIFIER/BOTH và trả lỗi quyền khi không được phép. Không tự suy đoán rằng một Staff có cả hai nhiệm vụ.
- Backend payment-clearance đang chờ tích hợp. Giao diện không tính phí hoặc tạo khoản thu giả; phiên ACTIVE được giữ ở trạng thái chưa đủ điều kiện xe ra. Module thanh toán phải ghi PAID qua luồng tin cậy trước khi Staff dùng nút check-out.
- Nhận diện và xác nhận AI là bản ghi riêng; chưa thể sửa biển số của một session đã check-in qua API verify-slot. Giao diện chỉ chỉnh kết quả nhận diện trước khi tạo session.
- Xác minh booking tạo phiên tại ô thực tế và cập nhật `physicalStatus` sang `OCCUPIED`; audit lưu trạng thái trước/sau xác nhận. Booking chỉ giữ sức chứa, không đặt cứng ô cụ thể. Check-in xe vãng lai hoặc tạo booking tự nó không làm ô thành `OCCUPIED`.
- Kiểm tra ô ở frontend giảm chọn nhầm nhưng không thay constraint/lock backend. Lỗi đồng thời giữa API điều phối cũ và xác minh mới vẫn cần xử lý tại backend.
- Phiên đăng nhập được khôi phục từ sessionStorage khi tải lại cùng tab. Khi API trả 401, client làm mới access token và thử lại một lần; các request đồng thời dùng chung một lần refresh. Đăng xuất xóa phiên đã lưu. Refresh bị thu hồi/hết hạn yêu cầu đăng nhập lại; lỗi mạng/5xx tạm thời không xóa phiên.

## Kiểm tra

```powershell
npm --prefix frontend/smart-parking test
npm --prefix frontend/smart-parking run build
```

`npm test` chạy test API client hiện có cùng Vitest/Testing Library cho màn hình Staff và hợp đồng request. Dữ liệu test được mock tại lớp HTTP, không ghi vào Neon, Cloudinary hoặc AI database. Kiểm thử UI bao gồm sửa OCR, lỗi lưu ảnh, quyền bị từ chối, cổng ra chưa thanh toán/biển số sai, ghép ô, lịch sử điều phối, hủy có lý do và chặn gửi lặp. Cần chạy nghiệm thu với các dịch vụ thật sau khi cấu hình môi trường của nhóm.
