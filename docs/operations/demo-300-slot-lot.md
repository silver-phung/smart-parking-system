# Demo lot 300 ô (Tầng A/B/C × 100)

Hai cách (đều idempotent, chạy lại không tạo trùng):
1. **API (khuyến nghị)**: tạo bãi bằng `POST /api/v1/parking-lots` (hoặc dùng bãi có sẵn), rồi Admin gọi
   `POST /api/v1/parking-lots/{id}/provision-standard-layout`. Ghi audit `PROVISION_LOT_LAYOUT`.
2. **SQL**: `docs/operations/demo-300-slot-lot.sql` (DB local/staging; Neon dùng chung chỉ khi Owner đồng ý).

Kết quả mong đợi: PhysicalSlotsCount = 300, ConfiguredCapacity = 300, TotalCapacity (operational) = 300,
AvailableSlotsCount = 300, OccupiedSlotsCount = 0. Ô MAINTENANCE/UNAVAILABLE/inactive sẵn có được giữ nguyên và trừ khỏi operational.
Không cần migration (schema không đổi).
