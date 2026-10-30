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

API chính cần database core đã migrate, trong đó có cột `staff_lot_assignments.duty`. Nhận diện ảnh cần FastAPI ở địa chỉ `AiService:BaseUrl`, database AI đã migrate và cấu hình `AiRecognitionConnection`. Lưu ảnh dùng cấu hình Cloudinary hiện có. Nhập biển số không kèm ảnh vẫn dùng được khi không dùng AI hoặc dịch vụ ảnh.

## Các khu chức năng

- **Xe vào:** chọn/chụp ảnh JPEG/PNG/WebP tối đa 10 MB hoặc nhập tay. AI chỉ gợi ý biển số. Khi sửa kết quả AI phải có lý do; xác nhận kết quả nhận diện và lưu ảnh trước khi gửi check-in. Check-in không truyền SlotId. Thành công hiển thị mã phiên; thất bại giữ form để xử lý, không báo thành công giả.
- **Xe ra:** tra cứu biển số hoặc mã phiên trong bãi đang chọn; xem thông tin, ảnh vào và khoản phí đã ghi nhận. Nhập/nhận diện biển số thực tế tại cổng ra, đối chiếu rồi xác nhận xe thực sự qua cổng. Chỉ gửi check-out cho phiên PAID; đọc lại phiên và kiểm tra biển số trước khi gửi. Không gửi quyền miễn trừ thanh toán.
- **Xác minh ô:** hàng xe chờ phân/ghép ô, xe đã phân chờ xác minh và danh sách xe ACTIVE. Chọn xe, tầng và ô; phân/đổi ô mục tiêu, xác minh vị trí hoặc báo sai ô có lý do. Ô có xe chưa ghép phiên có danh sách riêng. UI loại ô đang liên kết với xe khác và đọc lại phiên/ô/danh sách phiên trước khi ghi.
- **Xe đang gửi:** tìm biển số, phân trang, xem chi tiết phiên và ảnh; chuyển nhanh sang cổng ra hoặc xác minh ô.
- **Điều phối:** lọc/phân trang cả điều phối chờ và đã xử lý, giữ kết quả OCCUPIED/WRONG_SLOT/CANCELLED để tra cứu. Hủy điều phối cần nhập lý do và bấm xác nhận riêng.

Nút **Làm mới** tải lại số liệu bãi và danh sách của khu chức năng đang mở. Sau thao tác thành công dữ liệu liên quan được tải lại. Đổi bãi xóa bản nháp đang thao tác; không cho đổi bãi hoặc chuyển khu bằng thanh tab trong lúc nhận diện/gửi thao tác.

## Phạm vi và giới hạn tích hợp

- `my-lots` chưa trả duty của Staff theo từng bãi. UI cung cấp các khu nghiệp vụ; server vẫn kiểm tra GATE/SLOT_VERIFIER/BOTH và trả lỗi quyền khi không được phép. Không tự suy đoán rằng một Staff có cả hai nhiệm vụ.
- Backend payment-clearance đang chờ tích hợp. Giao diện không tính phí hoặc tạo khoản thu giả; phiên ACTIVE được giữ ở trạng thái chưa đủ điều kiện xe ra. Module thanh toán phải ghi PAID qua luồng tin cậy trước khi Staff dùng nút check-out.
- Nhận diện và xác nhận AI là bản ghi riêng; chưa thể sửa biển số của một session đã check-in qua API verify-slot. Giao diện chỉ chỉnh kết quả nhận diện trước khi tạo session.
- Xác minh mới giữ nguyên tín hiệu cảm biến. UI hiển thị tín hiệu trống/có xe và cảnh báo khi đối chiếu khác nhau; không tự chuyển trạng thái cảm biến bằng route viewer demo.
- Kiểm tra ô ở frontend giảm chọn nhầm nhưng không thay constraint/lock backend. Lỗi đồng thời giữa API điều phối cũ và xác minh mới vẫn cần xử lý tại backend.
- Phiên đăng nhập được khôi phục từ sessionStorage khi tải lại cùng tab. Khi API trả 401, client làm mới access token và thử lại một lần; các request đồng thời dùng chung một lần refresh. Đăng xuất xóa phiên đã lưu. Refresh bị thu hồi/hết hạn yêu cầu đăng nhập lại; lỗi mạng/5xx tạm thời không xóa phiên.

## Kiểm tra

```powershell
npm --prefix frontend/smart-parking test
npm --prefix frontend/smart-parking run build
```

`npm test` chạy test API client hiện có cùng Vitest/Testing Library cho màn hình Staff và hợp đồng request. Dữ liệu test được mock tại lớp HTTP, không ghi vào Neon, Cloudinary hoặc AI database. Kiểm thử UI bao gồm sửa OCR, lỗi lưu ảnh, quyền bị từ chối, cổng ra chưa thanh toán/biển số sai, ghép ô, lịch sử điều phối, hủy có lý do và chặn gửi lặp. Cần chạy nghiệm thu với các dịch vụ thật sau khi cấu hình tài khoản/môi trường của nhóm.
