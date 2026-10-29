# ADR 001 Parking zones

**Status:** Accepted  
**Date:** 28/10/2026  
**Owners:** Product, Backend, 3D/IoT  
**Related:** BR-FIX-01, BR-FIX-02, SP-DEC-19, SP-MF-16, SP-MF-17

## Context

Một địa điểm cần phục vụ cả xe vãng lai và quyền gửi cố định. Khu cố định có thể nằm sau barrier bên trong bãi gốc hoặc là một khu riêng bên ngoài khu vãng lai.

## Decision

Mô hình đích dùng quan hệ `ParkingLot → ParkingZone → Floor → Slot`.

- `TRANSIENT`: khu vãng lai, khách không giữ một slot cụ thể.
- `FIXED`: khu có quyền ô/xe/kỳ và kiểm soát bằng thẻ/barrier.
- Zone có layout `INSIDE` hoặc `EXTERNAL` nhưng vẫn thuộc đúng parking lot/site.
- Slot thuộc đúng một zone; 2D/3D dùng cùng định danh và quan hệ parent.

## Consequences

- Cần migration cho `parking_zones` và FK từ floor/slot theo thiết kế được duyệt.
- Sức chứa và trạng thái nhận xe phải tính theo zone trước khi tổng hợp lên lot.
- Hai cảnh 3D mẫu phải thể hiện được khu cố định bên trong và bên ngoài.
- Đóng một zone không được xóa session, pass hoặc xe còn tồn tại.
