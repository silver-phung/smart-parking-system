# ADR 002 Entry verification

**Status:** Accepted
**Date:** 28/10/2026
**Owners:** Product, Backend, AI, IoT
**Related:** BR-SES-01, BR-SES-02, BR-IMG-01, SP-MF-07, SP-MF-08, SP-MF-09

## Context

Luồng vãng lai cần tránh bắt Staff cổng xác nhận biển số trước khi cho xe vào. Camera/AI có thể đọc sai nên kết quả OCR cũng không được coi ngay là biển số chính thức.

## Decision

1. Camera chụp ảnh và AI/OCR tạo recognition result tạm.
2. Hệ thống kiểm tra sức chứa, cấp vé/thẻ và tạo session `ACTIVE` với trạng thái vận hành `WAITING_VERIFICATION`.
3. Chống lặp ở đầu vào bằng EventId và vé/thẻ; không dùng OCR tạm làm căn cứ duy nhất.
4. Khi IoT báo có xe tại slot, Staff điều phối đối chiếu ảnh/xe, xác nhận hoặc sửa biển số và thông tin xe, rồi gắn actual slot.
5. Chỉ kết quả này mới chuyển slot sang `OCCUPIED` và áp kiểm tra unique biển số chính thức.

## Consequences

- Cần `recognition_results` hoặc trường provisional riêng; không ghi mù OCR vào trường biển số chính thức.
- Sửa OCR không được tạo session thứ hai.
- Queue xác minh phải lưu bền và đổi trạng thái thay vì xóa lịch sử.
- Staff cổng chỉ xử lý ngoại lệ thiết bị/OCR ở chiều vào; Staff điều phối chịu trách nhiệm xác minh tại slot.
