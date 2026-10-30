Tổng quan
Smart Parking là hệ thống OJT gồm backend ASP.NET Core/.NET 10, PostgreSQL qua EF Core, giao diện React/Vite, một giao diện HTML demo cũ, cùng các dịch vụ AI và simulator. Backend chia thành bốn layer: API, Application, Domain và Infrastructure. Hướng dẫn chạy bản demo hiện tại nằm trong README.md.

Backend và nghiệp vụ

Auth hỗ trợ đăng nhập, refresh và logout; có JWT, refresh token hash trong DB, giới hạn tốc độ đăng nhập và khóa tạm khi nhập sai nhiều lần. Tài khoản hiện dùng role ADMIN, MANAGER, STAFF, USER.
Nghiệp vụ chính gồm quản lý bãi/tầng/ô, phương tiện, phiên gửi xe, đặt chỗ, điều phối ô và phân công nhân viên vào bãi. Phân quyền được giới hạn theo bãi được giao.
Booking giữ sức chứa chứ không giữ cứng một ô; check-in kiểm tra booking/công suất, còn dispatch tách riêng việc phân công và xác nhận xe đã đỗ.
EF Core/PostgreSQL có các migration cho Accounts, parking core, staff assignments và booking/capacity.
Chưa thấy entity hoặc API thanh toán/biểu phí trong source backend hiện tại, dù tài liệu schema có thiết kế các phần này.
Event, IoT và AI

Có hai luồng event/simulator tách biệt. Parking event pipeline xử lý VehicleEntered, SensorTriggered, SlotParked, VehicleExited, có kiểm tra trùng sự kiện; nhưng repository đang dùng in-memory và DI chọn NoOpParkingEventBridge. Adapter EF và live bridge có trong code nhưng chưa được bật.
IoT simulator cũ cập nhật bãi mẫu qua file JSON; telemetry và xe đang đỗ lưu trong bộ nhớ tiến trình, nên không bền sau khi restart.
Viewer 3D dùng danh sách ô mẫu trong bộ nhớ, không phải dữ liệu slot thật trong PostgreSQL. API viewer có route riêng; API quản lý slot thật nằm dưới /api/v1/parking-slots.
AI biển số là FastAPI riêng, endpoint POST /api/v1/detect, dùng YOLOv8 và PaddleOCR ONNX. Backend cũng có tích hợp Gemini để phân tích ảnh, nhưng mặc định chạy NoOp nếu chưa có API key.
Frontend và kiểm thử
Có hai frontend: React/Vite tại frontend/smart-parking được backend phục vụ ở /app/ và gọi API; bản HTML/CSS/JS tại frontend/ là demo độc lập, dùng localStorage cho đặt chỗ, QR, giá và dữ liệu mẫu. Bộ test backend dùng xUnit và EF InMemory; frontend có test cho API wrapper.

Các điểm cần lưu ý

apidesign.md chỉ đặc tả login theo route /api/auth/login và response result; code hiện dùng /api/v1/auth/login, request username, response data.
Tài liệu Accounts mô tả đăng ký OTP, nhưng bản demo hiện tại chỉ cho đăng nhập bằng tài khoản được cấp; README nói chức năng đăng ký/xác minh đã được gỡ.
Tài liệu database có một số phần cũ hoặc chưa triển khai: đề xuất pricing/payments/gate events, trong khi các migration hiện có không bao gồm module thanh toán.
Tài liệu quy định chỉ phục vụ ô tô thông thường, nhưng viewer mẫu và Gemini vẫn có dữ liệu/logic xe điện, nhiên liệu. Đây là điểm chưa thống nhất giữa mô hình nghiệp vụ và demo.
Hướng dẫn IoT dùng trường deviceId, còn DTO backend thực tế nhận carId.