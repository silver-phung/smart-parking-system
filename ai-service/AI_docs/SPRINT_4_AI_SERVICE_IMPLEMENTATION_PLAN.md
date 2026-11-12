# Sprint 4 -- AI Recognition Integration, Reliability & Handover

## 1. Mục tiêu

Hãy triển khai Sprint 4 cho `ai-service` của hệ thống Smart Parking dựa
trên code và tài liệu hiện có trong repository.

Mục tiêu chính là hoàn thiện luồng nhận diện biển số (ALPR) và liên kết
kết quả nhận diện với dữ liệu nghiệp vụ của Core Service một cách an
toàn, có thể retry, có audit và không làm sai trạng thái bãi đỗ.

**Nguyên tắc kiến trúc bắt buộc:**

-   AI Service chỉ chịu trách nhiệm tiếp nhận ảnh, nhận diện biển số,
    lưu kết quả nhận diện và cung cấp chức năng xác minh/liên kết theo
    hợp đồng đã thống nhất.
-   Core Service mới là nguồn dữ liệu có thẩm quyền đối với `Event`,
    `Session`, `Ticket`, check-in/check-out và trạng thái chỗ đỗ.
-   Kết quả OCR không phải bằng chứng xác nhận xe đã vào bãi, ra bãi
    hoặc đã chiếm một chỗ đỗ.
-   Không tự tạo Session, không tự chuyển chỗ đỗ sang `OCCUPIED`, và
    không tự quyết định check-in/check-out từ AI Service.
-   Không giả định tên class, endpoint, field, schema hoặc luồng nghiệp
    vụ. Trước khi sửa, hãy đọc repository, xác định implementation hiện
    tại và đối chiếu với tài liệu.
-   Ưu tiên thay đổi nhỏ, tương thích với code hiện tại. Không refactor
    diện rộng hoặc tự ý thay đổi kiến trúc nếu không cần thiết.

## 2. Quy trình làm việc dành cho coding agent

Trước khi code:

1.  Đọc cấu trúc repository, `README`, tài liệu thiết kế, cấu hình và
    các luồng hiện tại liên quan đến recognition.
2.  Kiểm tra các thành phần quan trọng, tối thiểu gồm:
    -   `AiRecognitionRecord.cs` và entity/configuration/repository liên
        quan.
    -   Service xử lý recognition và `ConfirmAsync`.
    -   API/controller/router liên quan đến upload, detect, confirm và
        link.
    -   `main.py` và pipeline nhận diện FastAPI.
    -   `PlateCapture.jsx` hoặc UI tương ứng.
    -   Database context, migrations và cấu hình kết nối AI DB.
3.  Xác định AI Service đang giao tiếp với Core Service bằng cách nào.
    Không tự thêm RabbitMQ, Kafka, gRPC hoặc cơ chế giao tiếp mới nếu
    repository và hợp đồng hiện tại chưa yêu cầu.
4.  Ghi lại những gì đang có, những gì còn thiếu và các giả định cần
    Core team xác nhận.
5.  Nếu thiếu thông tin nghiệp vụ có thể gây sai dữ liệu, hãy hỏi trước
    khi triển khai phần đó. Không tự bịa contract.

Trong quá trình triển khai:

-   Chia công việc thành các bước nhỏ theo thứ tự ưu tiên ở mục 8.
-   Sau mỗi nhóm thay đổi, build/test phần bị ảnh hưởng.
-   Không xóa hoặc ghi đè dữ liệu, migration, cấu hình hay code hiện có
    nếu chưa xác định tác động.
-   Không commit/push thay mặt người dùng trừ khi được yêu cầu rõ ràng.
-   Không đưa connection string, password, API key hoặc secret vào
    source control.

Khi hoàn tất, cung cấp: - Tóm tắt file đã thay đổi và lý do. - Migration
cần chạy (nếu có). - Cách chạy service và test. - Kết quả build/test
thực tế; phân biệt rõ test đã chạy và test chưa chạy. - Những phần còn
thiếu hoặc cần Core team xác nhận.

## 3. Ranh giới trách nhiệm và contract với Core Service

Phối hợp với người phụ trách Core để thống nhất các định danh và ý nghĩa
của chúng:

-   `EventId`
-   `SessionId`
-   `TicketId`
-   `ImageId`
-   `LotId`

### Quyết định nghiệp vụ đã chốt

-   **Core Service sở hữu** `Event`, `Session`, `Ticket`, check-in/check-out,
    phân công Staff và trạng thái chỗ đỗ. Core là nguồn dữ liệu có thẩm quyền
    cho các định danh và trạng thái nghiệp vụ này.
-   **AI Recognition Service sở hữu** tiếp nhận ảnh/context, chạy nhận dạng,
    lưu recognition tạm, cung cấp luồng nhân viên xác nhận/sửa biển số và
    audit, sau đó gửi yêu cầu liên kết đến Core. Service này không tạo Session,
    không ghi trực tiếp Core DB và không quyết định check-in/check-out.
-   Luồng vào đề xuất: camera/barrier tạo sự kiện; Core xử lý và tạo Event/
    Session theo quy tắc check-in; Staff gửi ảnh cùng context đã được Core xác
    thực cho AI. AI trả recognition tạm để Staff xem/sửa. Sau xác nhận, AI gửi
    yêu cầu link; Core kiểm tra Event/Session/Ticket/Lot và trả kết quả.
-   Nếu ảnh/recognition đến trước khi Core có `SessionId`, AI vẫn lưu recognition
    với `PENDING_LINK`; khi Core tạo Session, Staff hoặc Core gửi yêu cầu link
    lại. Không tự sinh hoặc đoán `SessionId`.
-   `PENDING_LINK` là trạng thái của **recognition**: kết quả đã được lưu nhưng
    chưa có liên kết nghiệp vụ được Core xác nhận. Trạng thái này không có nghĩa
    Session đang chờ và không cho phép AI tự tạo Session. `LINKED` chỉ được đặt
    sau phản hồi xác nhận thành công từ Core. Lỗi tạm thời giữ trạng thái chờ và
    có thể retry; lỗi dữ liệu/quyền được ghi nhận để xử lý, không retry mù.
-   `UNREADABLE` dành cho ảnh hợp lệ nhưng không đọc được biển số; `FAILED` dành
    cho lỗi kỹ thuật/xử lý khiến recognition không hoàn tất. Confidence thấp là
    thuộc tính/kết quả cần Staff xem xét, không đồng nhất với service failure.
-   Core tạo và xác thực `EventId`, `SessionId`, `TicketId`, `LotId`. AI lưu các
    ID tham chiếu, không tin giá trị tự khai từ frontend. Actor lấy từ JWT đã
    xác thực. Quyền theo role và assignment/lot phải được kiểm tra dựa trên
    nguồn phân công có thẩm quyền của Core.
-   Idempotency key do caller tạo ổn định cho một thao tác nghiệp vụ (detect
    hoặc link); AI/Core lưu và kiểm tra để cùng key không tạo recognition/link
    trùng. Retry khác payload với cùng key phải bị từ chối.

### Contract cần sản xuất

Xác định request/response, lỗi, timeout, idempotency key và xác thực quyền giữa
AI Recognition Service với Core. Không còn để thời điểm tạo Session hoặc ý nghĩa
của `PENDING_LINK` ở trạng thái câu hỏi mở; các chi tiết field/wire format cần
được ghi trong DTO/API contract trước khi tích hợp.

**Deliverables:**

-   DTO/API contract giữa AI Service và Core Service.
-   Sơ đồ trạng thái recognition và các transition hợp lệ.
-   Quy tắc retry, timeout và idempotency.
-   Thiết kế migration cho AI database.

Nếu contract chưa được chốt, ghi rõ điểm đang chờ xác nhận và không tự
triển khai logic phụ thuộc vào giả định đó.

## 4. Ngày 2 -- Mở rộng schema và authorization

### 4.1. Recognition record

Kiểm tra entity hiện tại, sau đó bổ sung các trường cần thiết nếu chưa
có:

-   `ImageId`
-   `LotId`
-   `EventId`
-   `SessionId`
-   `RecognitionStatus`
-   `ModelVersion`
-   `CorrelationId`
-   `IdempotencyKey`
-   Thời điểm liên kết thành công.
-   Thời điểm thất bại.
-   Thông tin lỗi phù hợp để phục vụ điều tra.
-   Thông tin actor xác nhận/chỉnh sửa và thời điểm thao tác.

Tên field, kiểu dữ liệu, nullability, index và foreign key phải phù hợp
với convention hiện tại. Không tạo foreign key cross-database tới Core
DB nếu hai service dùng database riêng; khi đó cần lưu external ID và
xác thực qua contract/service tương ứng.

### 4.2. Tách riêng nghiệp vụ link

-   Tạo service/use case riêng để liên kết recognition với
    Event/Session/Ticket.
-   Không để frontend tự quyết định hoặc tự gắn `SessionId` tùy ý.
-   Chỉ cho phép link khi dữ liệu và trạng thái hiện tại hợp lệ.
-   Link phải idempotent: gửi lại cùng một yêu cầu không được tạo liên
    kết hoặc dữ liệu nghiệp vụ trùng.
-   Không cho phép một recognition bị liên kết với một phiên không liên
    quan.
-   Xử lý rõ trường hợp link thành công, link thất bại, retry và yêu cầu
    link lặp lại.

### 4.3. Xác minh Staff và sửa `ConfirmAsync`

Kiểm tra và bảo đảm các điều kiện sau:

-   Nhân viên hiện tại có quyền thao tác tại `LotId` tương ứng. Core là nguồn
    chuẩn của assignment/duty; AI Service xác minh qua contract/API Core hoặc
    cơ chế claim đã được Core cấp và có thời hạn phù hợp. Không tự sao chép
    bảng phân công và tự quyết định quyền ở AI DB.
-   Recognition thuộc đúng nhiệm vụ/phiên/bãi cần xác minh.
-   Recognition chưa được xác nhận hoặc xử lý theo một trạng thái không
    cho phép xác nhận lại.
-   Ghi nhận actor thực hiện thao tác.
-   Ghi nhận thời điểm xác nhận/chỉnh sửa.
-   Nếu nhân viên sửa biển số, phải lưu giá trị trước/sau và lý do chỉnh
    sửa.
-   Không tin actor ID hoặc quyền hạn do client tự khai báo; lấy danh tính từ
    JWT đã xác thực và kiểm tra authorization ở backend. Gateway chỉ định tuyến
    request; mỗi service nhận request trực tiếp từ client cũng phải tự xác thực
    token. Không dựa vào việc request đi qua gateway như bằng chứng quyền.

Cần có audit trail đủ để truy vết ai đã xác nhận hoặc chỉnh sửa
recognition, vào thời điểm nào và vì lý do gì.

## 5. Ngày 3 -- Hoàn thiện workflow AI thực tế

Điều chỉnh `PlateCapture.jsx`, AI Recognition Service và adapter/API Core theo
workflow sau. Core tạo nghiệp vụ phiên; AI chỉ đề xuất recognition và yêu cầu
link:

``` text
Core/barrier xác thực event và tạo Session (hoặc trả context Event trước Session)
        |
        v
Staff gửi ảnh/context qua Gateway tới AI Recognition Service
        |
        v
AI lưu recognition tạm và chạy nhận diện
        |
        v
Lưu kết quả và confidence
        |
        v
Nhân viên kiểm tra kết quả
        |
        +---- Biển số đúng ------> Xác nhận
        |
        +---- Biển số sai -------> Sửa biển số + nhập lý do
        |
        +---- Không đọc được ----> Chuyển sang nhập tay
        |
        v
AI gửi yêu cầu liên kết recognition với Event / Session / Ticket tới Core
        |
        v
Core xác thực quan hệ/quyền, xác nhận link; Core giữ quyền quyết định nghiệp vụ
```

Các quy tắc bắt buộc:

-   OCR sai không được tạo Session mới.
-   AI Service không được tự tạo Session.
-   AI Service không được tự chuyển trạng thái chỗ đỗ thành `OCCUPIED`.
-   Không coi kết quả AI là bằng chứng xe đã vào hoặc ra khỏi bãi.
-   Ảnh mờ, không có biển số, confidence thấp hoặc AI unavailable phải
    có đường xử lý thủ công.
-   Nhân viên phải xác minh/chỉnh sửa trước khi link nếu nghiệp vụ yêu
    cầu xác nhận của con người.
-   Retry thao tác link không được tạo recognition, link hoặc Session
    trùng.
-   Check-out phải đối chiếu với Core Service và chỉ thực hiện khi actor
    có quyền.
-   AI Service không được tự quyết định trạng thái check-in/check-out.

UI cần hiển thị trạng thái rõ ràng, thông báo dễ hiểu và hành động tiếp
theo phù hợp. Không chỉ hiển thị exception kỹ thuật cho nhân viên.

## 6. Ngày 4 -- Hardening API và kiểm thử tích hợp

### 6.1. Hardening tại FastAPI (`main.py`)

Kiểm tra và triển khai các yêu cầu sau:

-   Giới hạn kích thước file dựa trên số byte thực tế đọc được, trước
    khi decode ảnh.
-   Không chỉ tin `Content-Type` do client gửi.
-   Kiểm tra MIME type và magic bytes/nội dung file trực tiếp tại
    FastAPI.
-   Từ chối file rỗng, file hỏng hoặc định dạng không hỗ trợ.
-   Không trả nguyên văn exception, stack trace, đường dẫn nội bộ hoặc
    thông tin nhạy cảm về client.
-   Bổ sung `/health` để kiểm tra tiến trình/service còn sống.
-   Bổ sung `/ready` để kiểm tra model và dependency cần thiết đã sẵn
    sàng.
-   Hỗ trợ `X-Correlation-ID`; nếu request chưa có ID hợp lệ thì tạo ID
    mới và đưa ID đó vào log/response.
-   Không log nội dung nhạy cảm hoặc secret.

Phân biệt rõ các nhóm kết quả/lỗi:

1.  Ảnh không đọc được hoặc file không hợp lệ.
2.  Không tìm thấy biển số trong ảnh.
3.  Có detection nhưng confidence thấp.
4.  Model hoặc service chưa sẵn sàng.
5.  Request timeout.
6.  Lỗi mạng hoặc lỗi hệ thống nội bộ.

Không dùng chung một mã lỗi/thông báo cho tất cả trường hợp nếu UI cần
hành động khác nhau.

### 6.2. Quy tắc retry

Chỉ retry các lỗi có khả năng khắc phục bằng cách gửi lại request, ví
dụ:

-   Lỗi mạng tạm thời.
-   HTTP 5xx.
-   Timeout, trong giới hạn retry đã thống nhất.

Không retry tự động đối với:

-   File sai định dạng.
-   File quá lớn.
-   File rỗng/hỏng.
-   Validation error.
-   Ảnh không có biển số.
-   Confidence thấp.

Retry phải có giới hạn, backoff phù hợp và idempotency để không tạo dữ
liệu trùng.

### 6.3. Test tối thiểu

Bổ sung hoặc cập nhật test cho các trường hợp:

-   MIME type khai báo sai nhưng nội dung thực tế là ảnh.
-   File vượt giới hạn dung lượng.
-   File rỗng.
-   File ảnh bị hỏng.
-   Model bị thiếu.
-   Model/service chưa ready.
-   Request timeout.
-   OCR không tìm thấy biển số.
-   Confidence thấp.
-   Nhân viên thuộc bãi khác cố đọc recognition.
-   Nhân viên thuộc bãi khác cố sửa/xác nhận recognition.
-   Gửi lại yêu cầu link nhiều lần.
-   Confirm cùng một recognition nhiều lần.
-   Sửa biển số nhưng không cung cấp lý do.
-   Recognition không làm thay đổi trạng thái Core ngoài ý muốn.
-   Correlation ID được duy trì xuyên suốt request và log.

Test phải kiểm tra cả HTTP response, dữ liệu lưu trong DB và side
effect. Không chỉ kiểm tra response status.

## 7. Ngày 5 -- Benchmark, tài liệu và bàn giao

### 7.1. Dataset và manifest

Tạo manifest cho bộ ảnh benchmark. Mỗi mẫu nên có:

-   `imageId`
-   Đáp án biển số chuẩn (ground truth).
-   Điều kiện chụp/nhóm điều kiện.
-   Dataset split (ví dụ tuning hoặc test).
-   Model version dùng để đánh giá, nếu phù hợp.

Mục tiêu tối thiểu đề xuất là 60 ảnh nếu dữ liệu hiện có cho phép. Không
được tự tạo kết quả benchmark hoặc ghi nhận số liệu chưa chạy.

Cần hạn chế data leakage:

-   Tách dữ liệu tuning và test.
-   Kiểm tra ảnh trùng lặp.
-   Nếu có thể, tránh để ảnh của cùng một xe/xuất hiện gần như giống
    nhau ở cả tuning và test.
-   Ghi rõ giới hạn của dataset nhỏ hoặc mất cân bằng.

### 7.2. Script benchmark

Tạo script có thể chạy lại với dataset, model version và cấu hình cố
định.

Báo cáo tối thiểu:

-   Exact plate accuracy.
-   Accuracy theo từng nhóm điều kiện.
-   Unreadable rate.
-   Service error rate.
-   Model latency p50/p95.
-   Upload-to-response latency p50/p95.

Nêu rõ cách tính từng metric, số lượng mẫu và các trường hợp bị loại
khỏi phép tính. Không tuyên bố đạt mục tiêu accuracy nếu chưa có
benchmark hợp lệ.

### 7.3. README

Cập nhật `ai-service/README.md`, bao gồm:

-   Yêu cầu môi trường và cách cài đặt sạch trên máy mới.
-   Cách cấu hình environment variables/secrets.
-   Cách tải hoặc đặt model đúng vị trí.
-   Cách khởi chạy AI Service.
-   Cách kiểm tra `/health` và `/ready`.
-   API workflow và contract liên quan.
-   Ma trận lỗi → thông báo UI → hành động nhân viên.
-   Cách chạy test.
-   Cách chạy benchmark.
-   Cách xử lý khi AI/model không sẵn sàng.
-   Migration và cách cập nhật database an toàn.

Không ghi secret thật vào README. Dùng placeholder và ví dụ an toàn.

## 8. Thứ tự ưu tiên khi thiếu thời gian

Triển khai theo thứ tự sau:

1.  Liên kết recognition đúng ảnh/phiên/bãi và bảo đảm authorization.
2.  Idempotency và chống tạo dữ liệu trùng khi retry.
3.  Workflow nhân viên xác nhận/chỉnh sửa có audit trail.
4.  Hardening upload và phân loại lỗi.
5.  Test tích hợp.
6.  Benchmark và báo cáo chất lượng.

Nếu không đủ thời gian hoàn thành toàn bộ Sprint 4, ưu tiên nhóm 1 và 2.
Không ưu tiên làm đẹp UI hoặc tối ưu model trước khi dữ liệu, quyền truy
cập và tính nhất quán đã an toàn.

## 9. Definition of Done

Chỉ đánh dấu Sprint 4 hoàn thành khi có bằng chứng kiểm tra được cho các
tiêu chí sau:

-   Từ một `SessionId` có thể truy ra đúng ảnh, recognition và lịch sử
    xác nhận/liên kết.
-   Ảnh của xe khác không thể bị gắn nhầm vào Session.
-   Nhân viên không được phân công tại bãi tương ứng không thể đọc hoặc
    sửa recognition.
-   Retry không tạo recognition, link hoặc Session trùng.
-   OCR sai, ảnh mờ hoặc không đọc được đi đúng luồng nhập tay và có lý
    do/audit phù hợp.
-   AI Service không tự quyết định check-in, check-out hoặc trạng thái
    chỗ đỗ.
-   Có test cho upload lỗi, timeout, model unavailable, confidence thấp
    và authorization.
-   Có benchmark tái lập được với dataset, model version và config cố
    định.
-   README đủ để thành viên khác cài đặt và chạy service trên máy sạch.
-   Build/test/migration đã được kiểm tra; kết quả thực tế được ghi rõ.

## 10. Yêu cầu báo cáo sau triển khai

Khi hoàn tất từng phần, hãy báo cáo theo format:

### Đã hoàn thành

-   Liệt kê thay đổi cụ thể.
-   Nêu file/class/endpoint/migration liên quan.

### Kiểm thử

-   Lệnh đã chạy.
-   Số test pass/fail.
-   Build có thành công không.
-   Test nào chưa chạy và lý do.

### Cần xác nhận

-   Các điểm contract hoặc nghiệp vụ còn phụ thuộc Core team.
-   Các giả định chưa được xác nhận.

### Rủi ro và việc còn lại

-   Vấn đề bảo mật, consistency, retry hoặc dữ liệu còn tồn tại.
-   Công việc chưa hoàn tất và mức độ ưu tiên.

**Lưu ý cuối:** Đây là kế hoạch triển khai dựa trên static review của
code và tài liệu. Không được mô tả bất kỳ tính năng nào là đã hoạt động,
test pass hoặc benchmark đạt yêu cầu nếu chưa thực sự chạy và xác minh
trong repository hiện tại.
