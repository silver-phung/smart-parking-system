# ADR 004 License plate visibility

**Status:** Accepted
**Date:** 28/10/2026
**Owners:** Product, Security, Frontend, IoT
**Related:** BR-RBAC-03, BR-FIX-01, BR-PRV-01

## Context

Mỗi slot cố định có bảng IoT để khách nhận biết ô đã đăng ký. Đồng thời Web App và API public phải tránh lộ dữ liệu xe của người khác.

## Decision

- Bảng IoT vật lý tại slot cố định hiển thị đầy đủ biển số đăng ký còn hiệu lực của đúng slot.
- Guest Web/API không nhận biển số, ảnh, session riêng hoặc chủ xe.
- Customer chỉ xem xe, session và quyền của mình.
- Staff/Manager/Admin xem theo actor, bãi, nhiệm vụ và quyền máy chủ.
- Thiết bị dùng device projection và danh tính thiết bị riêng; không dùng public projection hoặc giả token người dùng.

## Consequences

- Cần `slot_display_assignments` hoặc projection tương đương có version/thời gian hiệu lực.
- Đổi xe chỉ cập nhật bảng sau khi yêu cầu được xác minh và có hiệu lực.
- Kiểm thử phải đọc response JSON, không chỉ kiểm tra UI ẩn nhãn.
