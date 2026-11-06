# ADR 006 Versioned policies tickets and slot verification

**Status:** Implemented design pending team acceptance and PostgreSQL acceptance tests
**Date:** 02/11/2026
**Phạm vi** Vận hành bãi, chính sách, vé và xác minh xe

## Nguồn quyết định

Thiết kế triển khai [ADR 001](ADR-001-parking-zones.md), [ADR 002](ADR-002-entry-verification.md), [ADR 003](ADR-003-exit-payment-flow.md) và phân công trong `docs/sprint 4.docx` của workspace cha. ADR này ghi nhận thiết kế mã nguồn; không xác nhận nhóm đã duyệt giá, hạn giữ, cọc, hoàn hoặc phạt.

## Chính sách có phiên bản

`parking_policies` lưu từng phiên bản, phạm vi bãi/khu, thời điểm hiệu lực UTC, người tạo, người duyệt và nguồn quyết định. API chỉ tạo phiên bản mới, không sửa/xóa phiên bản đã ghi. Phiên bản nháp không tham gia truy vấn vận hành. Phiên bản khu có ưu tiên hơn phiên bản toàn bãi; trong cùng phạm vi chọn hiệu lực gần nhất không vượt thời gian máy chủ, sau đó chọn ID mới nhất.

`ParkingPolicyResolver` là nguồn đọc dùng chung. `ParkingSession.PolicyId` và `Booking.PolicyId` lưu quy tắc đã chọn. Sửa cấu hình không thay ID chính sách của giao dịch cũ. Các giao dịch trước triển khai có `PolicyId = null`; không gán ngược một quyết định mới vào lịch sử cũ.

| Tham số | Giá trị chính thức trong bản triển khai | Cách xử lý |
| --- | --- | --- |
| Hạn giữ, đến sớm/trễ, no-show | Chưa được cung cấp quyết định duyệt | Để null trong bản nháp |
| Hạn hủy, cho/không cho hủy | Chưa được cung cấp quyết định duyệt | Phiên bản mới cần Admin chọn rõ |
| Cọc, ngưỡng gần đầy | Chưa được cung cấp quyết định duyệt | Không seed số tiền hoặc ngưỡng chính thức |
| Đơn vị/làm tròn phí | Chưa được cung cấp quyết định duyệt | Hợp đồng cho module phí của Nam Anh |
| Mốc chọn giá | Hỗ trợ lựa chọn `CHECK_IN` | Chỉ áp dụng sau khi Admin duyệt rõ lựa chọn |
| Mốc trả ô | `VERIFIED_EXIT` theo ADR 003 | Thu tiền không trả ô |

Các lựa chọn hiện hỗ trợ là `CEILING/FLOOR/NEAREST`, `EXPIRE`, `ALLOW/DENY`, `CHECK_IN`. Khả năng hỗ trợ không có nghĩa là lựa chọn đã được nhóm phê duyệt. Không có chính sách được duyệt thì luồng tiếp nhận theo vé từ chối, thay vì tự dùng con số mẫu. Bãi đã có khu cũng không tạo booking mới khi thiếu chính sách toàn bãi. Dữ liệu cũ chưa có khu vẫn giữ hành vi cũ trong API legacy; đó không phải bộ quy tắc chính thức mới.

## Quan hệ khu

Quan hệ chính là `ParkingLot → ParkingZone → ParkingFloor → ParkingSlot`. Slot không có một FK khu thứ hai. `ParkingFloor.LotId` được giữ để tương thích API hiện tại; FK ghép `(ZoneId, LotId)` bắt buộc khu và tầng thuộc cùng bãi. Phiên cũng dùng FK ghép tới khu. Tầng mới của bãi đã cấu hình khu phải cung cấp `ZoneId`.

Migration tạo một khu `TRANSIENT/INSIDE` cho mỗi bãi đã có và gắn tầng/phiên cũ vào đó. Không thay ID ô, tầng, phiên hoặc booking. `FIXED/INSIDE` và `FIXED/EXTERNAL` do Admin khai báo theo sơ đồ thực. Không seed bãi giả hoặc đổi bãi thật để làm dữ liệu trình diễn. Đổi loại khu có tầng/lịch sử và chuyển tầng có lịch sử/xe bị chặn. Đóng khu ngừng nhận mới, vẫn giữ phiên và cho xác minh/chuyển xe đã ở bên trong trên tầng/ô còn dùng được.

Sức chứa vãng lai loại khu FIXED, khu đóng, tầng/ô ngừng dùng và ô bảo trì/không khả dụng. Xe chờ vẫn chiếm một suất, xe đã ghép ô không bị đếm hai lần. Helper có tham số `zoneId` cho module đặt chỗ; booking legacy chưa có zone vẫn được trừ bảo thủ theo toàn bãi.

## Vé và trạng thái xác minh

Phiên chờ xác minh có `parking_sessions.status = ACTIVE` và `VerificationStatus = WAITING_VERIFICATION`. Biển số chính thức được ghi khi nhân viên đối chiếu xe. Unique biển số mở áp dụng sau khi lượt xe qua hàng chờ.

Mỗi mã vé là mã in dùng cho một lịch sử phát hành; mã đã dùng không được tái sử dụng. Báo mất giữ nguyên SessionId. Thay vé yêu cầu Admin/Manager và lý do, kết thúc vé cũ trước khi cấp vé mới trong một giao dịch. Chỉ một vé `ISSUED` được gắn với một phiên. Đây là vé có mã riêng; việc tái sử dụng thẻ vật lý và quyền gói thuộc Sprint 5.

`EntryEventId` cùng hash nội dung bảo đảm gửi lại cùng yêu cầu trả cùng phiên, kể cả sau khi tạo DbContext/tiến trình mới. Dùng lại EventId với nội dung khác bị từ chối.

## Xác minh và chuyển xe

Nhân viên đúng bãi và duty `SLOT_VERIFIER` xác nhận biển số, nhiên liệu, ô thực tế và đã quan sát xe. Sửa/nhập tay cần lý do. Máy chủ chuẩn hóa biển số chính thức, kiểm tra phiên mở trùng, tương thích nhiên liệu, tầng/ô, phân công ô và phiên bản giao dịch; sau đó ghi phiên, ô, điều phối và audit trong một transaction.

Chuyển xe đã `VERIFIED` dùng endpoint riêng, yêu cầu lý do và xác nhận đã thấy ô cũ trống/xe tại ô mới. Không mở lỏng guard `Reassign` cho dispatch OCCUPIED. Giữ lịch sử điều phối và audit vị trí trước/sau.

Ghi theo bãi dùng semaphore trong tiến trình và PostgreSQL advisory transaction lock. `Revision` cùng concurrency token của ô chặn dữ liệu cũ. Unique index chặn trùng vé, sự kiện, biển số và ô đã xác minh. Kiểm thử PostgreSQL thật xác nhận các ràng buộc này.

## Ảnh tại cổng

Ảnh tại cổng là tùy chọn. Khi cấp vé kèm ảnh, Core kiểm tra ảnh đã được lưu và thuộc người tiếp nhận, rồi gắn `EntryImageId` vào lượt gửi.

Ảnh gắn với phiên không được xóa bằng ImageService. Endpoint evidence kiểm tra quyền bãi trước khi trả metadata ảnh. Việc chuyển Cloudinary sang ảnh có đường dẫn riêng tư và phân quyền toàn hệ thống vẫn thuộc phần tích hợp của Trí; việc này không được xem là đã hoàn tất chỉ vì endpoint core có quyền.
