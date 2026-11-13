Đức
AI   |   4 task   |   18 SP
Sở hữu context AI/ảnh, nhận dạng tạm nối core, bộ ảnh/đánh giá và độ tin cậy dịch vụ. AI hỗ trợ xác minh; không tự chốt quyền xe, vị trí đỗ hoặc cho xe ra.

## Quyết định ownership và luồng liên kết

- Core sở hữu Event, Session, Ticket, check-in/check-out, phân công Staff và trạng thái chỗ đỗ. Core là nguồn dữ liệu chuẩn và tạo/xác thực các ID nghiệp vụ.
- Đức sở hữu AI Recognition Service: tiếp nhận ảnh/context, nhận dạng, lưu recognition tạm, luồng Staff xác nhận/sửa biển số và audit, sau đó gửi yêu cầu link để Core kiểm tra. AI không tạo Session, không ghi trực tiếp Core DB và không tự quyết định nghiệp vụ vào/ra.
- Luồng mặc định: Core/barrier tạo Event và Core tạo Session theo quy tắc check-in; Staff gửi ảnh cùng context đã xác thực tới AI; AI trả kết quả tạm; Staff xác nhận/sửa; AI gửi yêu cầu link; Core xác thực quan hệ Event/Session/Ticket/Lot và phản hồi.
- Nếu recognition được tạo trước khi Core có SessionId, lưu `PENDING_LINK` và link sau khi Core tạo phiên. `PENDING_LINK` nghĩa là recognition đã lưu nhưng Core chưa xác nhận liên kết; đây không phải trạng thái Session và không cho AI tự sinh SessionId.
- `LINKED` chỉ đặt sau phản hồi thành công từ Core. Lỗi tạm thời có thể retry cùng idempotency key; lỗi quyền/quan hệ nghiệp vụ cần ghi nhận và xử lý, không retry mù. `UNREADABLE` là ảnh hợp lệ nhưng không đọc được; `FAILED` là lỗi kỹ thuật khiến xử lý không hoàn tất. Confidence thấp là kết quả cần Staff xem xét, không phải lỗi dịch vụ.
- Actor lấy từ JWT đã xác thực, không lấy actor/role từ request body. Core sở hữu dữ liệu phân công/duty và là nơi xác thực Staff có quyền ở LotId; AI không sao chép bảng phân công để tự quyết định quyền.
- Gateway chỉ route và chuyển tiếp bearer token. Mỗi service xác thực JWT ở phía mình; không coi việc request đi qua gateway là bằng chứng đã được xác thực. AI gọi API authorization nội bộ của Core để kiểm tra tài khoản/role hiện hành và assignment/duty theo lot trước khi cho phép xem/sửa/link.
- Hai bên chốt request/response, lỗi, timeout, idempotency, correlation ID và cách xác thực giữa services trước khi tích hợp. Không tạo foreign key xuyên database hoặc giả định transaction chung.

Gắn kết quả AI với đúng ảnh và lượt gửi
5 SP. Phụ thuộc: Hợp đồng vé và lượt chờ (Dương).
1. Bổ sung context nhận dạng: ảnh, bãi, EventId, RecognitionId và SessionId khi đã có; giữ raw/normalized plate, confidence, thời gian và phiên bản model.
2. Vì AI DB và core DB có thể tách, thống nhất bước gắn recognition trước/sau khi tạo phiên, kiểm tra quan hệ qua service hợp lệ và retry không tạo liên kết nhầm.
3. Lưu kết quả nhân viên xác nhận/sửa, actor/time/reason và kết quả lỗi; chỉ cho đọc/sửa trong bãi/nhiệm vụ được cấp, không tin SessionId tùy ý từ client.
Dữ liệu và điểm ghép: Input là ảnh/context được xác thực; output gồm recognition tạm, lỗi hoặc gợi ý và tham chiếu để core dùng. Field bổ sung là thiết kế mới cần migration; không giả định transaction chung cho hai DB.
Kiểm thử và điều kiện xong: Tra một phiên ra đúng ảnh và recognition, ảnh xe trước không bị dùng lại; sai bãi/phiên bị chặn. Retry liên kết sau lỗi mạng không tạo nhiều record có thẩm quyền.
Bàn giao: Migration AI/context, API/DTO, quy trình liên kết và test; Dương nhận provisional data, Trí nhận ImageId/quyền và cách vận hành hai DB.
Giới hạn: AI không tự xác nhận biển số chính thức hoặc quyền xe. Nếu chưa có SessionId ở bước chụp ảnh, dùng trạng thái chờ liên kết rõ ràng thay vì bịa ID.
Bắt đầu đọc: backend/src/SmartParking.Infrastructure/Entities/AiRecognitionRecord.cs; backend/src/SmartParking.Infrastructure/Data/AiRecognitionDbContext.cs; backend/src/SmartParking.Infrastructure/Services/StaffDetectionService.cs; ai-service/app/main.py
Nối AI ở cổng và xác minh lại tại ô
5 SP. Phụ thuộc: Liên kết ảnh và lượt gửi (Đức); hợp đồng vé và lượt chờ (Dương).
1. Ghép đường ảnh thật → FastAPI/nhận dạng → recognition tạm → vé/lượt chờ của Dương. Giữ kết quả chưa đọc được để người có quyền xử lý, không làm thành công giả.
2. Cung cấp ảnh gốc và gợi ý cho màn xác minh tại ô; khi nhân viên sửa biển số/thông tin xe, lưu bản gốc và bản được xác nhận trong cùng luồng liên kết.
3. Hỗ trợ ảnh lúc ra để đối chiếu nếu luồng sử dụng, nhưng quyết định thu/trả xe ở core và nhân viên đúng quyền; hợp tác sửa API/adapter thay vì ghi DB phiên trực tiếp.
Dữ liệu và điểm ghép: Nhận ImageId/RecognitionId/EventId và context bãi/lượt đã thống nhất. Nhiên liệu/hãng/màu nếu AI trả chỉ là gợi ý; kết quả chính thức đến từ bước xác minh hợp lệ.
Kiểm thử và điều kiện xong: Ảnh mờ/OCR sai/không có plate đi vào cần kiểm tra hoặc nhập tay có quyền; sửa giữ cùng vé/SessionId. AI không đổi ô OCCUPIED hoặc đóng phiên; retry không tạo lượt mới.
Bàn giao: Luồng gọi thật, DTO và mã lỗi nhất quán, test đúng/sai/mờ/nhập tay; Dương nối core, Song nối form, Danh dùng bộ ảnh của phần bộ ảnh kiểm thử của Đức.
Giới hạn: Không ép nhân viên cổng duyệt OCR ở mọi lượt vào. Không tạo thêm phiên khi cần sửa plate hoặc dùng kết quả nhận dạng làm bằng chứng đã đỗ/đã ra.
Bắt đầu đọc: docs/decisions/ADR-002-entry-verification.md; backend/src/SmartParking.Infrastructure/Services/StaffDetectionService.cs; frontend/smart-parking/src/pages/StaffWorkspace.jsx; ai-service/app/main.py
Bộ ảnh kiểm thử và báo cáo kết quả AI
5 SP. Phụ thuộc: Luồng AI vào cổng và xác minh tại ô (Đức), để đánh giá tích hợp cuối.
1. Chuẩn bị manifest ảnh có nguồn/quyền sử dụng, đáp án plate và nhãn điều kiện; tách ảnh tuning khỏi ảnh đánh giá, phát hiện trùng hoặc rò cùng xe giữa tập khi cần.
2. Bao phủ sáng/tối/nghiêng/mờ/che biển, ảnh PC/mobile và không đọc được. Đề xuất ít nhất 60 ảnh test độc lập nếu có đủ nguồn; công bố số thực tế khi thiếu.
3. Đo đúng plate chuẩn hóa trên toàn bộ tập và theo nhóm, tỷ lệ không đọc được, lỗi dịch vụ, latency p50/p95; tách thời gian model với thời gian cả luồng upload→response.
Dữ liệu và điểm ghép: Manifest cần ảnh/ID, đáp án, split, nhóm điều kiện và version model/cấu hình. Báo tử số, mẫu số và cách xử lý ảnh không đọc được; không chỉ chọn các ảnh thành công.
Kiểm thử và điều kiện xong: Chạy lại cùng bộ/version cho kết quả đối chiếu được; không trùng ảnh tuning/test. Danh kiểm độc lập một mẫu đáp án; lưu các nhóm sai để phần xử lý lỗi AI của Đức xử lý.
Bàn giao: Manifest, script/cách đo, báo cáo số liệu và ảnh lỗi được phép chia sẻ; phối hợp Danh kiểm đáp án và Trí chuẩn bị môi trường.
Giới hạn: Không tự ghi đạt 98% hoặc <3 giây từ bản phân vai cũ khi chưa đo. Không đưa ảnh nhạy cảm hoặc key dịch vụ vào báo cáo public.
Bắt đầu đọc: ai-service/README.md; ai-service/app/main.py; backend/src/SmartParking.Infrastructure/Services/StaffDetectionService.cs; docs/testing/acceptance-criteria.md
Xử lý lỗi AI và hướng dẫn chạy
3 SP. Phụ thuộc: Liên kết ảnh, luồng AI và bộ ảnh kiểm thử (Đức).
1. Rà giới hạn file, signature/MIME, kích thước thực, ảnh rỗng/hỏng và timeout ở cả proxy .NET lẫn FastAPI trực tiếp; trả lỗi có thể xử lý, không lộ stack/key.
2. Giới hạn retry theo loại lỗi, duy trì mã tương quan để tra yêu cầu; confidence thấp chuyển xác minh/nhập tay có lý do, không lấy plate cũ để tránh lỗi UI.
3. Bàn giao model/version, cấu hình, healthcheck, cách chạy và xử lý AI/ảnh không sẵn sàng; tích hợp trạng thái lỗi vào form thật cùng Song/Trí.
Dữ liệu và điểm ghép: Giữ schema response/lỗi đã chốt ở phần liên kết ảnh và lượt gửi của Đức và phần luồng AI vào cổng và xác minh tại ô của Đức; phân biệt không đọc được với dịch vụ không chạy. Cấu hình nhạy cảm qua môi trường/secret được hỗ trợ, không ghi vào repo.
Kiểm thử và điều kiện xong: Thử file giả MIME, quá lớn, timeout, thiếu model/key và confidence thấp. Không tạo plate giả/lượt trùng; nhập tay đi đúng quyền và lưu lý do; Danh chạy theo hướng dẫn trên máy khác.
Bàn giao: Bản sửa, test, README và ma trận lỗi→thông báo→hành động; số đo lại các nhóm ảnh lỗi khi có thay đổi model/pipeline.
Giới hạn: Không hạ kiểm tra file hoặc trả success rỗng để demo. Không coi việc proxy có guard là đã bảo vệ mọi cách gọi trực tiếp FastAPI.
Bắt đầu đọc: ai-service/app/main.py; ai-service/README.md; backend/src/SmartParking.Infrastructure/Services/StaffDetectionService.cs; frontend/smart-parking/src/pages/StaffWorkspace.jsx

## Phân tích hiện trạng trong repository

Đánh giá dưới đây dựa trên code/tài liệu đang có trong repo; “đã triển khai” nghĩa là có implementation nhìn thấy được, chưa khẳng định đã chạy thành công trên môi trường thật.

### Đã có nền tảng, cần improve/hoàn thiện

| Task | Đã có | Còn cần làm trong Sprint 4 |
|---|---|---|
| Gắn recognition với ảnh/lượt gửi | Có `AiRecognitionRecord`, migration riêng cho AI DB; lưu plate raw/normalized, confidence, bbox, inference time; API nhận dạng và API nhân viên xác nhận/sửa, lưu actor/time/note. | Record chưa có ImageId, EventId, RecognitionId ngoài ID nội bộ, SessionId, lot/context hoặc model version. Ảnh được upload riêng sau bước nhận dạng nên chưa có liên kết bền vững từ record đến đúng ảnh/phiên. Bổ sung schema/migration, quy trình liên kết/chờ phiên, kiểm tra quyền theo bãi/nhiệm vụ và retry/idempotency. Hiện Confirm chỉ giới hạn theo role Staff/Admin, chưa kiểm tra nhân viên được phân công đúng bãi hay ownership record. |
| Luồng AI cổng và xác minh | Có FastAPI `/api/v1/detect`, proxy .NET `/api/v1/staff/detect`, lưu recognition tạm; form `PlateCapture` cho nhân viên xem confidence, sửa plate và nhập lý do. Ảnh gốc được tải lên khi lưu phiên. ADR mô tả rõ OCR tạm và xác minh tại ô. | Luồng hiện tại là công cụ nhận dạng ở UI Staff, chưa nối recognition với vé/EventId/phiên chờ và ảnh trong một workflow xuyên suốt. Cần dùng adapter/API liên kết với core và giữ cùng session khi sửa; bổ sung trạng thái chờ/xử lý rõ ràng cho no-plate/service failure. Xác minh rõ luồng ra nếu áp dụng. Không để AI tự chốt trạng thái core. |
| Bộ ảnh và báo cáo AI | Có model detect/OCR, confidence, ngưỡng OCR và `inference_time_ms`; có helper đánh giá tracking nhưng không thấy bộ benchmark OCR/plate gắn với yêu cầu này. | Chưa thấy manifest dataset có quyền sử dụng, đáp án ground-truth, split tuning/test, nhãn điều kiện; chưa thấy script/báo cáo plate accuracy, unreadable rate, lỗi dịch vụ, p50/p95 và end-to-end latency. Tạo bộ độc lập (mục tiêu ≥60 nếu đủ nguồn), kiểm tra trùng/rò cùng xe, đo theo nhóm và ghi rõ số mẫu thực tế. Đây là phần mới cần triển khai chính. |
| Xử lý lỗi và hướng dẫn chạy | Proxy kiểm tra MIME allowlist, kích thước khai báo và thực tế, signature JPEG/PNG/WebP; FastAPI trả 503 khi thiếu model và 400 khi decode ảnh lỗi; .NET có timeout 60 giây và trả lỗi dịch vụ có kiểm soát; README có hướng dẫn cài/chạy và schema response. | FastAPI trực tiếp hiện đọc toàn bộ file không có giới hạn byte/MIME/signature rõ ràng; thông điệp lỗi decode có thể chứa chi tiết exception. Chưa thấy correlation ID, retry policy theo loại lỗi, readiness/healthcheck model cụ thể, hay ma trận lỗi→UI action được hoàn thiện. Thêm guard ở FastAPI, thống nhất schema lỗi và test service unavailable/timeout/confidence thấp; phân biệt unreadable với service down. |

### Ưu tiên Sprint 4

1. **Mới cần triển khai (ưu tiên cao):** liên kết recognition ↔ ảnh ↔ Event/phiên/bãi; kiểm tra quyền theo assignment/lot; quy trình chờ liên kết nếu chưa có SessionId; retry không tạo record/link trùng; migration và API/DTO tương thích hai DB.
2. **Mới cần triển khai:** manifest và pipeline benchmark có thể tái lập, báo cáo accuracy theo nhóm, unreadable/error rate và p50/p95 cả model lẫn end-to-end.
3. **Cần improve nền tảng có sẵn:** workflow form/API để recognition đi cùng đúng phiên và ảnh, giữ gốc + bản nhân viên xác nhận, xử lý thủ công khi OCR không đọc được, không nhân đôi phiên/lượt.
4. **Cần hardening nền tảng có sẵn:** giới hạn/kiểm tra file ở FastAPI trực tiếp, correlation ID, phân loại lỗi và UI thông báo/hành động, healthcheck model và tài liệu môi trường sạch.
5. **Đã có tương đối:** record nhận dạng riêng, lưu raw/normalized/confidence và thời gian suy luận; xác nhận/sửa có actor/time/reason; migration ban đầu; guard upload ở .NET; form chụp/nhận dạng; ADR và acceptance baseline. Không cần làm lại, nhưng cần bổ sung liên kết/context và xác minh permission/behavior thực tế.

### Rủi ro/phụ thuộc cần chốt khi làm

- Hợp đồng core của Dương phải xác định EventId/vé/phiên chờ, cách idempotency và thời điểm tạo SessionId; không tự đặt SessionId từ client.
- Ảnh có hai bước (gửi AI và lưu ảnh core); cần cơ chế dùng chung ImageId hoặc liên kết sau upload có retry/idempotency, tránh record AI không truy được ảnh.
- AI DB và core DB có thể tách connection; thiết kế eventual linking/trạng thái pending, không dựa vào transaction phân tán.
- Role check hiện có chưa đồng nghĩa quyền trên đúng lot/assignment hoặc đúng recognition record; cần authorization ở service/controller.
- Chỉ công bố độ chính xác/latency sau khi chạy benchmark trên bộ ảnh có ground-truth và version model/config cố định.
