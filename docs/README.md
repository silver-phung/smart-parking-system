# Smart Parking Documentation

Thư mục này là cổng tài liệu chính thức được quản lý cùng source code. Tài liệu nghiệp vụ đích, hiện trạng triển khai và đề xuất tương lai phải được phân biệt rõ; không suy chức năng đã hoàn thành chỉ vì có mô tả hoặc endpoint mẫu.

**Rà soát cấu trúc:** 29/10/2026.
**Mốc code được bộ đặc tả đối chiếu:** `Sprint3/demo` tại `4bed0c8a929fe050d644aa4e07c0a1caa77d8d0e`.
**Lưu ý:** Nhánh làm việc hiện tại có thể không kế thừa mốc trên. Khi thay đổi code, phải cập nhật cột hiện trạng và mốc đối chiếu trước khi gọi tài liệu API/schema là hiện hành.

## Bắt đầu từ đâu

| Nhu cầu | Tài liệu |
|---|---|
| Hiểu phạm vi, kiến trúc và mức hoàn thiện | [Tổng quan](product/overview.md) |
| Hiểu trình tự xe vào, đỗ, thanh toán và ra | [Main flows](product/main-flows.md) |
| Tra quy tắc bắt buộc | [Business rules](product/business-rules.md) |
| Tra từng hành vi và actor | [Use cases](product/use-cases.md) |
| Tra vòng đời trạng thái | [State machines](product/state-machines.md) |
| Tra hành trình màn hình và bàn giao actor | [User journeys](product/user-journeys.md) |
| Tra bảng, quan hệ và thiết kế bổ sung | [Data model](architecture/data-model.md) |
| Tra endpoint hiện có và đề xuất | [API endpoints](api/endpoints.md) |
| Tra enum, DTO và projection | [Data contracts](api/data-contracts.md) |
| Hiểu quyết định đã chốt | [Decision records](decisions/README.md) |
| Chạy/tích hợp hệ thống | [Operations](operations/README.md) |
| Lập kế hoạch nghiệm thu | [Acceptance criteria](testing/acceptance-criteria.md) |

## Phân loại trạng thái

- `HIỆN CÓ`: đã xác minh trong code tại mốc đối chiếu; không tự chứng minh đã ghép UI hoặc chạy trên môi trường thật.
- `MỘT PHẦN`: có một phần code/API/UI nhưng chưa hoàn thành toàn flow hoặc guard.
- `YÊU CẦU`: nghiệp vụ đích đã được ghi nhận nhưng chưa có đủ triển khai.
- `ĐỀ XUẤT`: thiết kế hoặc hợp đồng cần phê duyệt/triển khai; không gọi như API/schema đã tồn tại.
- `MÔ PHỎNG`: công cụ thử nghiệm, không thay nguồn vận hành.
- `LỊCH SỬ`: chỉ dùng truy vết kế hoạch hoặc nguồn cũ.

## Thứ tự ưu tiên khi có xung đột

1. ADR có trạng thái `Accepted` mới nhất.
2. Business rules và state machines hiện hành.
3. Main flows và use cases.
4. Data model, API và data contracts đã đối chiếu cùng commit.
5. Operations guides.
6. Sprint plan, SRS/PDF/DOCX cũ và tài liệu trong `archive`.

Nếu code khác tài liệu, không tự chọn code làm nghiệp vụ đúng. Ghi rõ chênh lệch, xác nhận với owner, sau đó cập nhật ADR/quy tắc và code hoặc hạ trạng thái tài liệu.

## Quy tắc một thông tin một nguồn

| Thông tin | Nguồn chính |
|---|---|
| Quy tắc và bất biến | `product/business-rules.md` |
| Trình tự xuyên hệ thống | `product/main-flows.md` |
| Chi tiết tác nhân, tiền/hậu điều kiện | `product/use-cases.md` |
| Vòng đời trạng thái | `product/state-machines.md` |
| Hành trình UI và bàn giao | `product/user-journeys.md` |
| Bảng, FK, index và thiết kế dữ liệu | `architecture/data-model.md` |
| Route và HTTP contract | `api/endpoints.md` |
| Enum, DTO, payload và projection | `api/data-contracts.md` |
| Lý do quyết định | `decisions/ADR-*.md` |
| Cách chạy/vận hành | `operations/` |

Tài liệu khác chỉ tham chiếu mã hoặc liên kết, không sao chép một phiên bản quy tắc mới.

## Quy trình cập nhật

Một thay đổi liên quan trạng thái, quyền, tiền, dữ liệu xe hoặc IoT tối thiểu phải:

1. Thêm hoặc cập nhật ADR nếu thay quyết định đã chốt.
2. Cập nhật business rule và state machine liên quan.
3. Cập nhật main flow/use case.
4. Cập nhật data model, endpoint và DTO nếu hợp đồng thay đổi.
5. Thêm tiêu chí nghiệm thu và ca retry/concurrency/restart.
6. Ghi mốc commit đã đối chiếu.

## Tài liệu nguồn và lịch sử

Các PDF/DOCX trong thư mục `/Users/tim/FPTU/C#/SmartParkingSystem_OJT/Docs` được giữ làm nguồn yêu cầu và lịch sử Sprint. Chúng không tự ghi đè bộ tài liệu hiện hành trong repository. `docs/archive/` chỉ chứa chỉ mục hoặc tài liệu lịch sử cần đi cùng source; không nhân bản binary lớn nếu không cần.
