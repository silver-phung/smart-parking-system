# Event and IoT architecture

**Trạng thái:** Hướng đích; pipeline hiện tại còn nguồn in-memory/mô phỏng.  
**Liên quan:** BR-EVT-01 đến BR-EVT-03, SP-MF-07 đến SP-MF-14.

## Ranh giới nguồn

- Camera/AI tạo ảnh và recognition result tạm.
- Cảm biến slot chỉ chứng minh có vật thể tại slot, chưa biết chắc session/xe.
- Staff điều phối chốt xe, biển số và actual slot.
- API nghiệp vụ áp guard và ghi PostgreSQL.
- Web, 2D, 3D và bảng tổng hợp đọc snapshot từ nguồn đã lưu.

## Event envelope tối thiểu

Mỗi event cần `eventId`, device/source identity, lot/zone/slot khi có, occurred-at, received-at, payload version và processing result. Retry cùng tác động giữ nguyên EventId.

## Queue xác minh

Hàng việc dùng trạng thái `PENDING`, `PROCESSING`, `CONFIRMED`, `REJECTED`, `NEEDS_REVIEW`. Xử lý xong không xóa bản ghi; lưu session, slot, actor, thời điểm, giá trị OCR gốc và kết quả xác minh.

## Thiết bị và mất mạng

Thiết bị có danh tính riêng, không tin `TriggeredBy` từ body và không giả token người dùng. Cache/offline queue không được trở thành nguồn nghiệp vụ cạnh tranh. Khi nối lại, client/thiết bị tải snapshot server, gửi event chưa đồng bộ bằng cùng ID và không ghi đè trạng thái mới bằng dữ liệu cũ.
