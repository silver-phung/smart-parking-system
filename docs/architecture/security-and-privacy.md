# Security and privacy projections

**Trạng thái:** Yêu cầu hiện hành.  
**Quyết định:** [ADR-004](../decisions/ADR-004-license-plate-visibility.md).

## Projection dữ liệu

| Đối tượng gọi | Dữ liệu xe được xem |
|---|---|
| Guest/public | Trạng thái slot và sức chứa tổng hợp; không biển số, ảnh, session riêng, chủ xe hoặc tiền |
| Customer | Xe, session, booking và quyền thuộc mình |
| Staff cổng | Dữ liệu cần nhận/tra/thu/trả tại bãi và duty được cấp |
| Staff điều phối | Ảnh/OCR/xe/slot cần xác minh tại bãi và duty được cấp |
| Manager | Dữ liệu vận hành của bãi được giao theo chức năng được cấp |
| Admin | Dữ liệu quản trị theo chức năng; không mặc định bỏ mọi kiểm soát audit |
| Bảng IoT slot cố định | Full biển số đăng ký hiệu lực của đúng slot qua device projection |

## Guard bắt buộc

Quyền phải được kiểm tra tại server theo actor, sở hữu, bãi và duty. Ẩn trường hoặc nút ở frontend không phải kiểm soát truy cập. Ảnh, recognition result, audit JSON và payment evidence có retention và quyền riêng; không log password, OTP, raw token hoặc secret thiết bị.
