# Parking zone design

**Trạng thái:** Hướng kiến trúc được chấp nhận; schema/API chưa triển khai đầy đủ.
**Quyết định:** [ADR-001](../decisions/ADR-001-parking-zones.md).

## Mô hình đích

```text
ParkingLot
├── ParkingZone TRANSIENT
│   ├── Barrier/làn vãng lai
│   └── Floors → Slots
└── ParkingZone FIXED
    ├── Barrier kiểm soát thẻ
    └── Floors → Slots → quyền ô/xe/kỳ
```

Khu `FIXED` có thể có `layoutType=INSIDE` hoặc `EXTERNAL`. Hai giá trị mô tả bố cục, không tạo hai nguồn dữ liệu hoặc hai parking lot giả nếu cùng một địa điểm quản lý.

## Dữ liệu tối thiểu

`parking_zones` cần định danh ổn định, `lot_id`, tên/mã zone, `zone_type`, `layout_type`, `access_mode`, trạng thái khai thác và version layout. Floor/slot phải ánh xạ nhất quán để Web 2D, 3D, IoT và API cùng đọc.

Chi tiết bảng/FK nằm trong [data model](data-model.md). Quyền gói, thẻ và vòng đời xe nằm trong [state machines](../product/state-machines.md).

## Hai cảnh 3D cần nghiệm thu

1. Khu cố định nằm bên trong bãi gốc, sau barrier thứ hai.
2. Khu cố định nằm ngoài khu vãng lai nhưng vẫn thuộc đúng site/lot.

Cả hai cảnh phải dùng mã zone/floor/slot từ DB; thay góc nhìn không được thay quan hệ parent hoặc trạng thái nghiệp vụ.
