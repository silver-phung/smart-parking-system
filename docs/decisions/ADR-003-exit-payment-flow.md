# ADR 003 Exit and payment flow

**Status:** Accepted  
**Date:** 28/10/2026  
**Owners:** Product, Backend, Payment  
**Related:** BR-SES-04, BR-SES-05, BR-PAY-01, SP-MF-11, SP-MF-12, SP-MF-13

## Context

Khách không cần tạo một yêu cầu lấy xe trên App. Trong vận hành thực tế, xe tự tới cổng ra và quẹt vé/thẻ để hệ thống tìm lượt gửi.

## Decision

1. Quẹt vé/thẻ tại cổng ra tạo access event và tìm đúng một session mở.
2. Hệ thống hiển thị ảnh/thông tin lúc vào cho Staff cổng và tính phí trên máy chủ.
3. Staff ghi khoản thực nhận/chứng từ theo quyền; retry dùng idempotency key.
4. Thanh toán không tự đóng session hoặc trả slot.
5. Chỉ sau khi đủ phí hoặc có ngoại lệ được duyệt và xe thực sự qua cổng mới chuyển `COMPLETED`, xử lý vé và giải phóng ô theo chính sách.

## Consequences

- Cần ticket/card assignment và API exit lookup.
- Thẻ mất/hỏng hoặc sai xe đi vào nhánh xác minh, không tạo session mới để checkout.
- Fee assessment, payment và exit confirmation là ba kết quả tách biệt.
