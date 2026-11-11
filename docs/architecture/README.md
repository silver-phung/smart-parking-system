# Architecture Documentation

- [Data model](data-model.md): schema hiện có và thiết kế bổ sung.
- [Parking zone design](parking-zone-design.md): cách mô hình hóa khu vãng lai/cố định và barrier.
- [Event and IoT](event-and-iot.md): ranh giới thiết bị, queue và nguồn trạng thái.
- [Security and privacy](security-and-privacy.md): projection dữ liệu và quyền xem.

Kiến trúc không tự ban hành nghiệp vụ mới. Quyết định thay đổi phải được ghi trong [ADR](../decisions/README.md).

## Phạm vi nghiệp vụ, dữ liệu và API

| Nghiệp vụ | Dữ liệu chính | API hiện có | Tiến trình sở hữu |
| --- | --- | --- | --- |
| Tài khoản và phân quyền | `users`, `roles`, token đăng nhập, phân công bãi trong `smartparking` | `/api/v1/auth`, `/api/v1/users`, `/api/v1/staff-assignments` | Parking Core |
| Đặt chỗ | `bookings`, tham chiếu xe và chính sách trong `smartparking` | `/api/v1/bookings` | Parking Core |
| Vận hành bãi | Khu, chính sách, vé, lượt gửi, ô và lịch sử xác minh trong `smartparking` | `/api/v1/parking-lots/{lotId}/operations`, `/api/v1/parking-sessions` | Parking Core |
| Nhận dạng biển số | Bản ghi nhận dạng, độ tin cậy và hash ảnh trong DB được cấu hình riêng | `/internal/recognition` qua HTTP nội bộ | Recognition Service |
| Sự kiện thiết bị | Sự kiện và projection phiên của pipeline InMemory hiện tại | `/api/events`, `/api/IoT` | Parking Core |

Gateway định tuyến yêu cầu. Mỗi API đăng ký persistence theo phạm vi dữ liệu của mình. Trong backend .NET, `Domain` giữ mô hình nghiệp vụ, `Application` giữ use case và hợp đồng, `Persistence` giữ DbContext, mapping, transaction và migration. `Infrastructure` tích hợp dịch vụ ngoài như HTTP, email và lưu ảnh. Các project API lắp ghép những lớp này để chạy.

### Vận hành bãi

Các API trong bảng dùng tiền tố `/api/v1/parking-lots/{lotId}/operations`. Parking Core xử lý các thao tác cần cập nhật nhiều bảng trong cùng transaction, chẳng hạn xác minh xe và đổi trạng thái ô. Nhân viên ghi nhận biển số sau khi đối chiếu xe tại ô.

| Chức năng | Dữ liệu đọc hoặc ghi trong `smartparking` | API |
| --- | --- | --- |
| Quản lý khu, tầng và cấu hình ô | `parking_zones`, `parking_floors`, `parking_slots` | `GET/POST /zones`, `PUT /zones/{id}`, `GET /layout`, `PUT /floors/{id}/zone`, `PUT /slots/{id}/fuel` |
| Quản lý chính sách theo phiên bản | `parking_policies`. Booking và lượt gửi giữ ID phiên bản đã áp dụng. | `GET/POST /policies`, `GET /policies/effective` |
| Tiếp nhận xe và cấp vé | `parking_sessions`, `parking_tickets`, tham chiếu ảnh trong `stored_images` | `POST /entries`, `GET /sessions` |
| Xác minh xe và chuyển ô | `parking_sessions`, `parking_slots`, `parking_dispatches`, `audit_logs` | `POST /sessions/{id}/verify`, `POST /sessions/{id}/move`, `GET /sessions/{id}/evidence` |
| Tra vé, báo mất, thay hoặc thu hồi vé | `parking_tickets`, `parking_sessions`, `audit_logs` | `GET /tickets/{code}`, `POST /tickets/{code}/change` |

Tên project .NET tương ứng với mẫu `Project.CleanArchitecture`. `SmartParking.Application/UseCases/ParkingOperations` chứa nghiệp vụ, `SmartParking.Persistence/EntityTypeConfigurations` chứa cấu hình bảng, và `SmartParking.Persistence/Repositories` điều phối thao tác lưu. `SmartParking.Api` và `SmartParking.Recognition.Api` là hai điểm khởi chạy hiện có.

Xem [nghiệp vụ vận hành bãi và quyền API](../handover/PARKING_OPERATIONS_HANDOVER.md) và [cấu hình Neon cho vận hành bãi](../operations/neon-microservices.md).
