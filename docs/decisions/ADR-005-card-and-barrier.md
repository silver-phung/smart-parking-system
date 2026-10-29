# ADR 005 Card and barrier architecture

**Status:** Proposed
**Date:** 28/10/2026
**Owners:** Product, Backend, Infrastructure, IoT
**Related:** DEC-12, SP-MF-16, SP-MF-17

## Context

Khu cố định cần thẻ và barrier; vãng lai cần vé/thẻ để tìm session tại cổng ra. Thiết bị vẫn phải xử lý có kiểm soát khi kết nối chập chờn.

## Proposed decision

- PostgreSQL trung tâm là nguồn chính cho card, assignment, pass, session và access event.
- Barrier giữ cache tối thiểu gồm token thẻ, zone được phép, hiệu lực và danh sách thu hồi.
- Thiết bị giữ offline queue cho access event chưa đồng bộ; không giữ một DB nghiệp vụ cạnh tranh.
- Server lưu card public identifier đã hash/token hóa, trạng thái `READY/IN_USE/LOST/DAMAGED/RETIRED`, assignment và lịch sử cấp/thu.
- Barrier chỉ mở sau kiểm tra thẻ, xe, quyền, zone và trạng thái theo chính sách được chốt.

## Open points

- Loại thẻ dùng cho vãng lai và cố định có tái sử dụng chung hay không.
- Thời gian cache/offline được phép và cách thu hồi khẩn cấp.
- Cách đối chiếu xe tại barrier khi mất kết nối.
- Ai có quyền cấp lại thẻ mất/hỏng.
