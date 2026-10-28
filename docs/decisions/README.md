# Architecture Decision Records

ADR ghi lại quyết định đã chốt, lý do và hệ quả. ADR không được sửa để che lịch sử: khi thay đổi quyết định, tạo ADR mới và đánh dấu ADR cũ `Superseded`.

| ADR | Quyết định | Trạng thái |
|---|---|---|
| [ADR-001](ADR-001-parking-zones.md) | Một bãi có khu vãng lai và khu cố định | Accepted |
| [ADR-002](ADR-002-entry-verification.md) | OCR tạo phiên tạm, Staff xác minh tại slot | Accepted |
| [ADR-003](ADR-003-exit-payment-flow.md) | Quẹt vé/thẻ tại cổng ra kích hoạt tra phiên và tính phí | Accepted |
| [ADR-004](ADR-004-license-plate-visibility.md) | Bảng IoT hiện full biển số, Web phân quyền | Accepted |
| [ADR-005](ADR-005-card-and-barrier.md) | Thẻ, barrier, DB trung tâm và cache thiết bị | Proposed |

## Trạng thái ADR

- `Proposed`: đang thiết kế hoặc còn điểm cần chốt.
- `Accepted`: được dùng làm quyết định hiện hành.
- `Superseded`: đã bị ADR mới thay thế.
- `Rejected`: đã xem xét nhưng không chọn.
