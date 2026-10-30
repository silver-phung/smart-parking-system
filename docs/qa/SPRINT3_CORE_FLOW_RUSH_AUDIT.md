# SPRINT 3 CORE FLOW AUDIT & RUSH REPLAN
**Dự án:** Smart Parking Management System  
**Workspace:** `E:\smartparking`  
**Chủ sở hữu nghiệp vụ (Business Owner):** Võ Phụng  
**Tài liệu tham chiếu chuẩn (Canonical Baseline):** `C:\Users\Silver 1\Downloads\sprint 3.docx`  
**Nhánh sở hữu chính (Primary Ownership Branch):** `feature/phung-database`  
**Nhánh tích hợp tổng (Integration Branch):** `Sprint3/demo`  
**Nhánh Hotfix hiện tại (Local Hotfix):** `hotfix/booking-sql-cancellation-token`  
**Chế độ thực thi:** ONE-PASS SOURCE AUDIT – READ ONLY – FAST EXECUTION  
**Ngày lập báo cáo:** 29/10/2026  

---

## MỤC LỤC
1. [Phần A: Kiểm kê mã nguồn và lịch sử Git (Source Inventory)](#phần-a-kiểm-kê-mã-nguồn-và-lịch-sử-git)
2. [Phần B: Ma trận đối chiếu Yêu cầu Sprint 3 với Hiện trạng Code](#phần-b-ma-trận-đối-chiếu-yêu-cầu-sprint-3-với-hiện-trạng-code)
3. [Phần C: Sơ đồ luồng trạng thái & Phân quyền dữ liệu (State Diagram & Ownership)](#phần-c-sơ-đồ-luồng-trạng-thái--phân-quyền-dữ-liệu)
4. [Phần D: Danh mục Bug & Lỗ hổng nghiệp vụ xác nhận (P0 / P1 / P2)](#phần-d-danh-mục-bug--lỗ-hổng-nghiệp-vụ-xác-nhận)
5. [Phần E: Phân định phạm vi: Sprint 3 vs Sprint 4 vs Sprint 5](#phần-e-phân-định-phạm-vi-sprint-3-vs-sprint-4-vs-sprint-5)
6. [Phần F: Phân tích chuyên sâu Sức chứa (Capacity Deep Audit)](#phần-f-phân-tích-chuyên-sâu-sức-chứa-capacity-deep-audit)
7. [Phần G: Kiểm toán Tích hợp Thu phí & Cổng ra (Payment & Exit Audit)](#phần-g-kiểm-toán-tích-hợp-thu-phí--cổng-ra)
8. [Phần H: Ma trận trách nhiệm thành viên (RACI Matrix)](#phần-h-ma-trận-trách-nhiệm-thành-viên-raci-matrix)
9. [Phần I: Kế hoạch triển khai Rush Plan (Minimal-Diff & Dependencies)](#phần-i-kế-hoạch-triển-khai-rush-plan)
10. [Phần J: Định nghĩa Hoàn thành cho 1 lượt gửi xe vãng lai (DoD for 1 Walk-in Session)](#phần-j-định-nghĩa-hoàn-thành-cho-1-lượt-gửi-xe-vãng-lai)
11. [Phần K: Các quyết định Owner cần chốt trước khi viết code (Owner Decisions)](#phần-k-các-quyết-định-owner-cần-chốt-trước-khi-viết-code)

---

## PHẦN A: KIỂM KÊ MÃ NGUỒN VÀ LỊCH SỬ GIT

### 1. Hiện trạng Git Branches & Commit Heads
- **Current Branch:** `hotfix/booking-sql-cancellation-token` (HEAD: `afac36b` - *fix: correct booking SQL cancellation token*).
- **Primary Ownership Branch (`feature/phung-database`):** HEAD tại `7986165` (*docs(setup): provide team local backend setup guide...*).
  - Đã tích hợp PR #2 từ Đức (`aa91711` - *feat/Duc/AI-service*).
  - Đã có Task 07 (Booking & Capacity) và fix chẩn đoán RBAC (`12e0ba2`).
  - **Chưa merge** các commit mới từ `Sprint3/demo`.
- **Integration Branch (`Sprint3/demo` & `origin/Sprint3/demo`):** HEAD tại `322eed9` (*docs: establish canonical requirements and architecture*).
  - Trước đó đã merge: YARP API Gateway (`300f02c`), Cloudinary image storage (`a8cc9cb`), Sketchfab 3D viewer model (`736aefd`), Frontend relative path fix (`4bed0c8`).
  - **LƯU Ý QUAN TRỌNG:** Commit `afac36b` (Hotfix runtime CancellationToken) nằm trên nhánh local `hotfix/booking-sql-cancellation-token`, **chưa được merge vào `Sprint3/demo` hoặc `feature/phung-database`**.
- **Working Tree:** Sạch ngoại trừ 1 file untracked:
  - `backend/src/SmartParking.Infrastructure/Services/EmailService.cs`: Đang được soạn thảo dở dang, hiện có lỗi cú pháp tại dòng 41 (`}`````).

### 2. Danh sách tác giả thực tế trong Git History
Toàn bộ commit trong lịch sử Git chỉ xuất phát từ 5 cá nhân:
1. `Võ Phụng <vpdpnaten10@gmail.com>` (Phụng - Owner / Backend Core)
2. `MinhThuc1 <minhthuctim13@gmail.com>` (Thức - 3D Viewer, Event Pipeline, IoT Simulator, Canonical Docs)
3. `fkmount / tri2k5 <dphamtung123@gmail.com / trilttlk12@gmail.com>` (Trí / Tùng - YARP, Cloudinary, Auth refactor, Migrations)
4. `DoxDuck <doduc311020@gmail.com>` (Đức - AI OCR Service Python)
5. `Nguyễn Thanh Song <songntse171665@fpt.edu.vn>` (Song - Frontend Web)
- **THIẾU SÓT ĐẶC BIỆT:** Trong toàn bộ lịch sử Git **HOÀN TOÀN KHÔNG CÓ BẤT KỲ COMMIT NÀO** từ **Nam Anh** (phụ trách Phí/Khoản thu/Email) và **Danh** (phụ trách QA/Test Matrix)!

---

## PHẦN B: MA TRẬN ĐỐI CHIẾU YÊU CẦU SPRINT 3 VỚI HIỆN TRẠNG CODE

| STT | Yêu cầu nghiệp vụ Sprint 3 (theo `sprint 3.docx`) | Hiện trạng triển khai trong mã nguồn | Đánh giá | Trách nhiệm |
| :---: | :--- | :--- | :---: | :---: |
| **01** | **Customer registration / Gmail OTP / reCAPTCHA**<br>Khách đăng ký tài khoản, nhận OTP Gmail thật, reCAPTCHA xác thực máy chủ. | Commit `adac2d6` (fkmount) đã **XÓA SẠCH** đăng ký, `EmailService`, `OtpService`, `RecaptchaService`, `Register.jsx`, `VerifyEmail.jsx` khỏi `Sprint3/demo`. File `EmailService.cs` đang được Phụng phục hồi dở dang (untracked). | **FAIL / BỊ PHÁ** | Phụng (API) + Nam Anh (Email) + Song (UI) |
| **02** | **Guest public parking data**<br>Khách xem slot công khai không cần đăng nhập. Không lộ biển số, ảnh, phiên hoặc tiền. | Endpoint `GET /api/v1/parking-lots/{id}/capacity` công khai (`[AllowAnonymous]`), ẩn chẩn đoán nội bộ khi chưa đăng nhập. 3D viewer có endpoint `GET /api/v1/viewer/parking-lots/{id}/slots`. | **PASS** | Phụng + Thức + Song |
| **03** | **ADMIN / MANAGER / STAFF GATE / STAFF DISPATCH**<br>Tách biệt 2 nhiệm vụ nhân viên: Staff cổng (Gate) và Staff điều phối (Dispatch). | `Roles.cs` chỉ có 1 role `STAFF`. Thực thể `StaffLotAssignment` không có cột `Duty` (Gate/Dispatch). `ParkingAuthorizationService` dùng chung 1 hàm `CanOperateSessionOrDispatchAsync`. | **FAIL / THIẾU** | Trí (DB) + Phụng (Auth API) |
| **04** | **Phân quyền theo bãi và theo nhiệm vụ (Per-lot & Per-duty)**<br>Staff chỉ thao tác tại bãi được giao; Staff cổng không xác nhận đỗ ô; Staff điều phối không check-out/thu tiền. | Per-lot đã có qua `StaffLotAssignments`. Tuy nhiên Per-duty **HOÀN TOÀN CHƯA CÓ**. Staff cổng hiện gọi được cả dispatch; Staff điều phối hiện gọi được cả check-out. | **PARTIAL** | Phụng + Trí |
| **05** | **Thông tin xe xăng/điện & Tương thích ô đỗ**<br>Ghi nhận nhiên liệu riêng với loại xe; kiểm tra tương thích slot khi xếp xe; không tự đoán nhiên liệu từ biển số. | Entity `Vehicle` và `ParkingSlot` **KHÔNG CÓ** trường `FuelType`. Service `AlwaysCompatibleVehicleChecker.cs` chỉ là STUB luôn trả về `null` (luôn tương thích). | **MISSING** | Trí (DB) + Phụng (API) + Thức (Sự kiện) |
| **06** | **Walk-in check-in không cần Customer ID**<br>Staff cổng nhận xe vãng lai, xác nhận biển số, không đòi hỏi tài khoản khách. | `ParkingSessionService.CheckInAsync` cho phép tạo session vãng lai với `booking == null`, tự tạo hoặc liên kết `Vehicle` với `UserId = null`. | **PASS** | Phụng |
| **07** | **Hàng đợi xe chờ điều phối (Pending Dispatch Queue)**<br>Xe qua cổng nhưng chưa đỗ phải nằm trong danh sách chờ điều phối; có dấu hiệu riêng. | `CheckInAsync` chỉ tạo `ParkingSession(Status="ACTIVE", CurrentSlotId=null)`, **KHÔNG TỰ ĐỘNG TẠO** bản ghi `ParkingDispatch(Status="PENDING")`. Không có endpoint `GET /dispatches/pending-queue`. | **FAIL / ĐỨT ĐOẠN** | Phụng |
| **08** | **Phân ô, xác nhận đỗ, báo sai ô, đổi ô**<br>Staff điều phối phân ô mục tiêu, xác nhận xe đã đỗ đúng ô (chuyển OCCUPIED), ghi nhận sai ô, đổi ô kèm lý do. | `ParkingDispatchesController` và `ParkingDispatchService` đã có đầy đủ logic: `CreateDispatch`, `AssignSlot`, `ConfirmOccupied`, `ReportWrongSlot`, `CancelDispatch`. | **PASS** | Phụng |
| **09** | **Tích hợp bảng giá, tính phí & biên lai**<br>Tính phí trên máy chủ theo giờ vào thực tế; Staff cổng thu tiền, lưu chứng từ/biên lai; chống thu trùng. | **HOÀN TOÀN CHƯA CÓ CODE**. Không có Entity `Pricing`, `Receipt`, `Payment`. Nam Anh chưa bàn giao bất kỳ commit nào. | **MISSING (0%)** | Nam Anh (phụ trách) + Phụng (nối Session) |
| **10** | **Xác nhận thanh toán & Kiểm tra xe ra vật lý**<br>Thu tiền chưa làm trống ô; phải xác nhận xe ra cổng mới đóng phiên (`COMPLETED`), thu hồi vé và trả ô. | `CheckOutAsync` cho phép đóng phiên `COMPLETED` trực tiếp từ `ACTIVE` mà **KHÔNG CẦN QUA BƯỚC PAID**. Không có kiểm tra thanh toán. | **FAIL** | Phụng + Nam Anh |
| **11** | **Đồng bộ sự kiện DB lưu bền & Web/3D**<br>Trạng thái lưu Neon DB; Thức nối sự kiện; 3D cập nhật từ DB, tải lại không mất; hiển thị 4 trạng thái vật lý. | Đã có Three.js 3D Viewer (`Parking3DViewer.jsx`) và API `ParkingViewerSlotsController`. Tuy nhiên WebSocket/SignalR thời gian thực chưa hoàn thiện, chủ yếu dùng REST polling. | **PARTIAL** | Thức + Song + Phụng |
| **12** | **Chống đụng độ (Concurrency), Idempotency & Audit**<br>Lock giao dịch PostgreSQL per-lot, unique constraints, không đếm trùng, audit log đầy đủ. | Đã có Semaphore + `pg_advisory_xact_lock` + AuditLog. Đã sửa lỗi CancellationToken trong `afac36b`. Tuy nhiên công thức capacity `max(...)` có lỗ hổng undercounting khi có xe chờ phân ô và xe đỗ lụi. | **PARTIAL** | Phụng + Trí |

---

## PHẦN C: SƠ ĐỒ LUỒNG TRẠNG THÁI & PHÂN QUYỀN DỮ LIỆU

### 1. Sơ đồ trạng thái Phiên gửi xe & Điều phối (State Machine)
```
[XE VÃNG LAI ĐẾN CỔNG]
        │
        ▼ (Staff Cổng gọi POST /check-in: xác nhận biển số, nhiên liệu xăng/điện)
┌─────────────────────────────────────────────────────────────────────────┐
│ ParkingSession: ACTIVE (CurrentSlotId = null, TotalFee = 0)             │
│ ParkingDispatch: PENDING (Đưa vào Hàng đợi xe chờ xếp ô)                 │
└───────────────────────────────────┬─────────────────────────────────────┘
                                    │
                                    ▼ (Staff Điều phối gọi POST /assign: chọn ô trống tương thích)
┌─────────────────────────────────────────────────────────────────────────┐
│ ParkingDispatch: ASSIGNED (AssignedSlotId = S1)                         │
│ Slot S1: PhysicalStatus = EMPTY, nhưng IsReservedForDispatch = TRUE     │
│ (Sức chứa nhận thêm tại cổng PHẢI trừ đi ô S1 này)                      │
└───────────────────────────────────┬─────────────────────────────────────┘
                                    │
                  ┌─────────────────┴─────────────────┐
                  │ (Xe đỗ đúng ô S1)                 │ (Xe đỗ nhầm ô S2 / Báo đổi ô)
                  ▼                                   ▼
┌───────────────────────────────────┐ ┌───────────────────────────────────┐
│ POST /confirm-occupied            │ │ POST /report-wrong-slot           │
│ Dispatch: OCCUPIED (SlotId = S1)  │ │ Dispatch: WRONG_SLOT              │
│ Slot S1: PhysicalStatus = OCCUPIED│ │ Slot S1: giải phóng về EMPTY      │
│ Session.CurrentSlotId = S1        │ │ Slot S2: PhysicalStatus = OCCUPIED│
│ (3D Viewer hiển thị xe tại ô S1)  │ │ Session.CurrentSlotId = S2        │
└─────────────────┬─────────────────┘ └─────────────────┬─────────────────┘
                  │                                     │
                  └─────────────────┬───────────────────┘
                                    │
                                    ▼ (Xe chuẩn bị ra cổng: Staff Cổng tra cứu phiên)
┌─────────────────────────────────────────────────────────────────────────┐
│ GET /parking-sessions/{id}/fee (Nam Anh: Tính phí dựa trên giờ vào)     │
└───────────────────────────────────┬─────────────────────────────────────┘
                                    │
                                    ▼ (Staff Cổng thu tiền: POST /payments/collect)
┌─────────────────────────────────────────────────────────────────────────┐
│ ParkingSession: PAID (PaidAt = UtcNow, TotalFee = X, ReceiptNumber = Y) │
│ (QUY TẮC CỐT LÕI: Ô S1 VẪN GIỮ TRẠNG THÁI OCCUPIED, CHƯA ĐƯỢC GIẢI PHÓNG)│
└───────────────────────────────────┬─────────────────────────────────────┘
                                    │
                                    ▼ (Xe đến barie cổng ra: Staff Cổng gọi POST /check-out)
┌─────────────────────────────────────────────────────────────────────────┐
│ ParkingSession: COMPLETED (CheckOutTime = UtcNow)                       │
│ Slot S1: PhysicalStatus = EMPTY (Giải phóng ô)                          │
│ Sức chứa bãi xe tăng thêm 1                                             │
└─────────────────────────────────────────────────────────────────────────┘
```

---

## PHẦN D: DANH MỤC BUG & LỖ HỔNG NGHIỆP VỤ XÁC NHẬN (P0 / P1 / P2)

### Bug P0 (Chặn toàn bộ luồng vận hành - Blocker)
1. **BUG-P0-01: Thiếu hoàn toàn Module Thu phí, Bảng giá & Biên lai (Nam Anh)**
   - **Mô tả:** Trong mã nguồn không có bất kỳ DTO, Service, Controller hay Entity nào để tính phí, ghi nhận thanh toán (`PAID`), hoặc in biên lai.
   - **Hệ quả:** Luồng Sprint 3 bị đứt đoạn nghiêm trọng tại khâu trả xe. Không thể chuyển phiên sang trạng thái `PAID`.
   - **Vị trí:** Cần bổ sung `FeeCalculationService`, `PaymentService`, `Receipt` entity trong `SmartParking.Domain` và `SmartParking.Infrastructure`.
   - **Trách nhiệm:** Nam Anh (Chủ trì nghiệp vụ phí) + Phụng (Hỗ trợ cấu trúc DB).

2. **BUG-P0-02: Check-in không tự động tạo lệnh Điều phối PENDING (Hàng đợi xe chờ bị đứt)**
   - **Mô tả:** Khi Staff cổng check-in xe không kèm `SlotId`, `ParkingSessionService.CheckInAsync` tạo Session nhưng không tạo `ParkingDispatch` trạng thái `PENDING`.
   - **Hệ quả:** Staff điều phối mở màn hình lên không thấy xe nào trong hàng đợi xe chờ xếp ô.
   - **Vị trí:** `backend/src/SmartParking.Infrastructure/Services/ParkingSessionService.cs:178-195`.
   - **Khắc phục tối thiểu:** Trong `CheckInAsync`, nếu `request.SlotId == null`, tự động tạo 1 bản ghi `ParkingDispatch` với `SessionId = session.Id`, `Status = DispatchStatuses.Pending`.

3. **BUG-P0-03: Lỗi cú pháp trong file untracked `EmailService.cs` làm hỏng build**
   - **Mô tả:** File `EmailService.cs` chứa ký tự rác `}````` tại dòng 41 gây lỗi `CS1056: Unexpected character '`'`.
   - **Vị trí:** `backend/src/SmartParking.Infrastructure/Services/EmailService.cs:41`.
   - **Khắc phục:** Xóa 4 ký tự backtick thừa.

---

### Bug P1 (Sai lệch quy tắc nghiệp vụ nghiêm trọng)
1. **BUG-P1-01: Không phân biệt hai loại Staff (Gate vs Dispatch)**
   - **Mô tả:** Hệ thống chỉ có role chung `STAFF`. Không có kiểm tra Staff cổng hay Staff điều phối. Staff cổng có thể tự confirm đỗ; Staff điều phối có thể tự check-out xe.
   - **Vị trí:** `backend/src/SmartParking.Domain/Entities/StaffLotAssignment.cs`, `ParkingAuthorizationService.cs:129`.
   - **Khắc phục tối thiểu:** Thêm cột `Duty` (`GATE`, `DISPATCH`, `BOTH`) vào bảng `staff_lot_assignments`. Trong `ParkingAuthorizationService`, tạo 2 phương thức riêng: `CanOperateGateAsync` và `CanOperateDispatchAsync`.

2. **BUG-P1-02: Check-out cho phép đóng phiên trực tiếp từ ACTIVE (Bỏ qua thanh toán)**
   - **Mô tả:** `CheckOutAsync` chỉ kiểm tra `session.Status == Completed || Cancelled` thì chặn, còn lại cho phép đóng phiên ngay cả khi `Status == ACTIVE` và chưa thanh toán xu nào.
   - **Vị trí:** `backend/src/SmartParking.Infrastructure/Services/ParkingSessionService.cs:260-264`.
   - **Khắc phục tối thiểu:** Kiểm tra điều kiện: Nếu bãi có thu phí, `session.Status` bắt buộc phải là `PAID` mới cho phép `CheckOut`.

3. **BUG-P1-03: Thiếu cấu hình nhiên liệu Xăng/Điện trên Xe và Ô đỗ**
   - **Mô tả:** `Vehicle` và `ParkingSlot` không có trường `FuelType`. `AlwaysCompatibleVehicleChecker` luôn trả về tương thích (STUB).
   - **Vị trí:** `backend/src/SmartParking.Domain/Entities/ParkingSlot.cs`, `Vehicle.cs`, `AlwaysCompatibleVehicleChecker.cs`.
   - **Khắc phục tối thiểu:** Bổ sung `FuelType` (`GASOLINE`, `ELECTRIC`, `ALL`) vào `ParkingSlot` và `Vehicle`. Cập nhật `CheckInRequestDto` cho phép chọn hoặc nhận diện `FuelType`.

---

### Bug P2 (Lỗi trải nghiệm & Thiếu sót hiển thị)
1. **BUG-P2-01: Đăng ký tài khoản Customer và OTP Gmail bị xóa khỏi Sprint3/demo**
   - **Mô tả:** Commit `adac2d6` xóa mất endpoint đăng ký của khách hàng.
   - **Khắc phục:** Phục hồi `AuthController.Register` và cấu hình `EmailService` với Gmail SMTP thật.
2. **BUG-P2-02: Hotfix `afac36b` chưa được merge vào nhánh tích hợp `Sprint3/demo`**
   - **Mô tả:** Lỗi PostgreSQL advisory lock `CancellationToken` đã sửa trên `hotfix/booking-sql-cancellation-token` nhưng chưa merge vào `Sprint3/demo`.

---

## PHẦN E: PHÂN ĐỊNH PHẠM VI: SPRINT 3 VS SPRINT 4 VS SPRINT 5

Tài liệu chuẩn `sprint 3.docx` quy định rõ ràng ranh giới nghiệp vụ:

| Nghiệp vụ | Sprint 3 (Hiện tại - Đường găng) | Sprint 4 (Tuyệt đối không đưa vào đường găng) | Sprint 5 (Tương lai) |
| :--- | :---: | :---: | :---: |
| **Gửi xe vãng lai (Walk-in)** | **BẮT BUỘC 100%** | Duy trì | Duy trì |
| **Đăng ký tài khoản & Gmail OTP** | **BẮT BUỘC** | Nâng cấp hồ sơ | Duy trì |
| **Xem chỗ đỗ công khai (Guest)** | **BẮT BUỘC** | Duy trì | Duy trì |
| **Phân công 2 loại Staff (Gate/Dispatch)** | **BẮT BUỘC** | Duy trì | Duy trì |
| **Điều phối slot & Báo sai ô** | **BẮT BUỘC** | Duy trì | Duy trì |
| **Thu phí vãng lai & Biên lai** | **BẮT BUỘC** | Duy trì | Duy trì |
| **Đồng bộ 3D Viewer & Sự kiện** | **BẮT BUỘC** | Nâng cao | Duy trì |
| **Đặt chỗ trước (Booking Advance Reservation)** | ❌ **KHÔNG LÀM (Ghi rõ Sprint 4)** | **BẮT BUỘC** (Cọc, Giữ chỗ, Hủy vé, Hết hạn) | Duy trì |
| **Vé tháng / Thẻ tháng (Monthly Parking)** | ❌ **KHÔNG LÀM (Ghi rõ Sprint 5)** | ❌ KHÔNG LÀM | **BẮT BUỘC** |

> [!IMPORTANT]
> **KẾT LUẬN VỀ TASK 07 (BOOKING):**  
> Mặc dù Task 07 (Booking & Capacity) đã được Phụng hoàn thành và kiểm thử rất kỹ, **theo hợp đồng Sprint 3, tính năng Đặt chỗ trước (Advance Booking) thuộc về Sprint 4**. Nhóm KHÔNG ĐƯỢC dùng Booking làm điều kiện chặn nghiệm thu Sprint 3. Toàn bộ trọng tâm phải dồn 100% vào luồng **XE VÃNG LAI (WALK-IN FLOW)**.

---

## PHẦN F: PHÂN TÍCH CHUYÊN SÂU SỨC CHỨA (CAPACITY DEEP AUDIT)

### 1. Phân biệt 3 khái niệm độc lập:
1. **Sức chứa nhận thêm xe tại cổng (Admission Capacity):** Số lượng xe tối đa mà nhân viên cổng được phép cho qua barie vào bãi.
2. **Tính khả dụng để phân ô (Slot Availability):** Danh sách các ô đỗ cụ thể đang trống (`EMPTY`), không bị bảo trì, và không bị giữ bởi lệnh điều phối `ASSIGNED` nào khác.
3. **Hiện trạng đỗ vật lý (Physical Occupancy):** Số lượng ô thực tế đang có vật thể đè lên cảm biến (`OCCUPIED`).

### 2. Chứng minh Lỗ hổng Undercounting của công thức `max(ActiveSessions, PhysicalOccupiedSlots)`

**Công thức hiện tại trong `ParkingLotCapacityHelper.cs`:**
$$\text{EffectiveOccupied} = \max(\text{ActiveSessionsCount}, \text{PhysicalOccupiedSlots})$$
$$\text{AvailableCapacity} = \max(0, \text{TotalCapacity} - (\text{EffectiveOccupied} + \text{PendingBookings}))$$

**Chứng minh phản ví dụ (Concrete Counterexample):**
- Giả sử bãi đỗ xe có tổng cộng 10 ô đỗ (`TotalCapacity = 10`).
- **Tình huống thực tế:**
  1. Có 2 xe vừa check-in hợp lệ qua cổng, đang di chuyển trên đường dốc/chờ điều phối xếp ô. Hai xe này chưa đỗ vào ô nào $\rightarrow \text{ActiveSessionsCount} = 2$, nhưng các ô được phân cho chúng vẫn mang trạng thái vật lý `EMPTY`.
  2. Đồng thời, có 2 xe lạ (hoặc xe không quẹt thẻ, xe lụi) đã vào đỗ thực tế tại Ô số 1 và Ô số 2 khiến cảm biến IoT báo `OCCUPIED` $\rightarrow \text{PhysicalOccupiedSlots} = 2$.
- **Thực tế vật lý:**
  - 2 ô đã bị chiếm dụng vật lý bởi xe lụi.
  - 2 xe hợp lệ đang vào bãi và chắc chắn cần 2 ô trống để đỗ.
  - Tổng số xe thực tế đang cần chỗ trong bãi là: $2 + 2 = 4$ xe.
  - Số chỗ thực tế còn có thể tiếp nhận thêm xe chỉ là: $10 - 4 = 6$ xe.
- **Tính toán theo code hiện tại:**
  $$\text{EffectiveOccupied} = \max(2, 2) = 2$$
  $$\text{AvailableCapacity} = 10 - 2 = 8\text{ xe!}$$
- **Hậu quả:** Hệ thống cho phép thêm 8 xe nữa qua cổng. Khi đó bãi sẽ có $2 \text{ (lụi)} + 2 \text{ (chờ)} + 8 \text{ (mới)} = 12 \text{ xe}$ tranh chấp trong một bãi chỉ có 10 ô $\rightarrow$ **VỠ TRẬN / OVERBOOKING 2 XE!**

**Bản chất toán học:**
Gọi:
- $M$: Tập hợp các phiên đỗ xe trùng khớp với ô vật lý đã đỗ ($\text{Session.SlotId == Slot.Id}$ và $\text{Slot.PhysicalStatus == OCCUPIED}$).
- $S_{queue}$: Các phiên xe đang chờ xếp ô / đang di chuyển chưa có ô OCCUPIED.
- $P_{unmatched}$: Các ô báo OCCUPIED nhưng không gắn với phiên đỗ xe hợp lệ nào.

Khi đó:
$$\text{ActiveSessions} = |M| + |S_{queue}|$$
$$\text{PhysicalOccupied} = |M| + |P_{unmatched}|$$
Công thức hiện tại:
$$\max(\text{ActiveSessions}, \text{PhysicalOccupied}) = |M| + \max(|S_{queue}|, |P_{unmatched}|)$$
Trong khi nhu cầu chiếm chỗ thực tế là:
$$\text{TrueOccupancyDemand} = |M| + |S_{queue}| + |P_{unmatched}|$$
Sai số thiếu (Undercount) chính bằng:
$$\mathbf{Error} = \min(|S_{queue}|, |P_{unmatched}|)$$

### 3. Đề xuất Công thức Sức chứa chuẩn mực (Proposed Invariant):
$$\text{UsedCapacity} = \text{PhysicalOccupiedSlots} + \text{SessionsAwaitingPhysicalParking} + \text{PendingBookings}$$
Trong đó:
- $\text{PhysicalOccupiedSlots}$: Tổng số ô có cảm biến báo `OCCUPIED`.
- $\text{SessionsAwaitingPhysicalParking}$: Số phiên `ACTIVE` mà xe **chưa** nằm tại ô `OCCUPIED` (bao gồm xe chờ điều phối và xe đang chạy tới ô được gán).
- $\text{PendingBookings}$: Số lượt đặt chỗ `PENDING` (áp dụng cho Sprint 4).
$$\mathbf{AvailableAdmissionCapacity} = \max(0, \text{UsableCapacity} - \text{UsedCapacity})$$

---

## PHẦN G: KIỂM TOÁN TÍCH HỢP THU PHÍ & CỔNG RA

### 1. Hiện trạng Mã nguồn
- **Trạng thái Session:** Đang định nghĩa 4 trạng thái trong `SessionStatuses`: `ACTIVE`, `PAID`, `COMPLETED`, `CANCELLED`.
- **Thực tế:**
  - Không có hàm nào chuyển Session từ `ACTIVE` sang `PAID`.
  - Không có Controller hay Service nào xử lý thu tiền.
  - Endpoint `POST /check-out` nhảy cóc trực tiếp từ `ACTIVE` sang `COMPLETED`.

### 2. Yêu cầu Hợp đồng API Tối thiểu cần triển khai:
1. **API Tính phí:** `GET /api/v1/parking-sessions/{id}/fee`
   - Đầu vào: `sessionId`.
   - Đầu ra: `durationMinutes`, `hourlyRate`, `totalFee`, `applicablePricingPlan`.
2. **API Thu tiền:** `POST /api/v1/parking-sessions/{id}/pay`
   - Đầu vào: `paymentMethod` (CASH / QR_TRANSFER), `amountPaid`, `notes`.
   - Logic: Kiểm tra số tiền $\ge$ tổng phí; cập nhật `session.Status = "PAID"`, `session.PaidAt = UtcNow`, `session.TotalFee = amountPaid`. Tạo mã biên lai/chứng từ.
   - **Bảo toàn slot:** KHÔNG giải phóng ô đỗ ở bước này.
3. **API Check-out cổng ra:** `POST /api/v1/parking-sessions/{id}/check-out`
   - Điều kiện tiên quyết: `session.Status == "PAID"` (Fail-closed nếu chưa thanh toán).
   - Logic: Đổi `session.Status = "COMPLETED"`, `session.CheckOutTime = UtcNow`. Giải phóng `slot.PhysicalStatus = "EMPTY"`. Tăng sức chứa bãi xe.

---

## PHẦN H: MA TRẬN TRÁCH NHIỆM THÀNH VIÊN (RACI MATRIX)

| Thành viên | Trách nhiệm chính theo Sprint 3 | Hiện trạng đóng góp thực tế | Đánh giá & Rủi ro |
| :--- | :--- | :--- | :--- |
| **Võ Phụng** (Owner / Lead) | Kiến trúc Backend, Quản lý tài khoản, API nghiệp vụ cốt lõi, DB Schema, Task 07 Booking. | Đã hoàn thành Task 03, 05, 06, 07, RBAC, Lock advisory. | **ĐẠT 85% BACKEND**. Cần bổ sung Gate/Dispatch duty check và sửa Check-in pending queue. |
| **Minh Thức** | Event Pipeline, IoT Simulator, 3D Parking Viewer, Canonical Documentation. | Hoàn thành Event Ingestion, Three.js 3D Viewer, viết bộ tài liệu chuẩn 5.000 dòng. | **XUẤT SẮC**. Đã sẵn sàng tích hợp với luồng xe thật. |
| **Trí (fkmount / tri2k5)** | Hạ tầng PostgreSQL Neon, YARP Gateway, Cloudinary Image Storage, Auth Framework. | Đã dựng DB Neon, tích hợp Cloudinary, YARP Gateway. Tuy nhiên commit `adac2d6` đã lỡ tay xóa mất đăng ký/OTP. | **CẦN PHỐI HỢP**. Cần hỗ trợ khôi phục đăng ký và bổ sung cột `Duty` vào `StaffLotAssignment`. |
| **Đức** | Python AI Service, OCR nhận diện biển số xe xăng/điện từ ảnh thực tế. | Đã tạo PR #2 tích hợp vào `feature/phung-database`. Cung cấp model OCR. | **SẴN SÀNG**. Cần nối API OCR vào màn hình Staff cổng của Song. |
| **Song** | Frontend Web (PC & Mobile), Màn hình Guest, Staff Cổng, Staff Điều phối, ghép 3D. | Đã có khung React Vite, trang Home, Login, nhúng 3D viewer. Chưa có màn hình Staff Gate / Dispatch hoàn chỉnh. | **CHẬM TIẾN ĐỘ UI**. Thiếu 2 màn hình vận hành chính của Staff. |
| **Nam Anh** | Bảng giá có hiệu lực, Tính phí máy chủ, Thu tiền & Biên lai, Dịch vụ Gmail xác minh. | **0 COMMIT TRONG TOÀN BỘ REPO**. Chưa bàn giao bất kỳ API tính phí hoặc thanh toán nào. | **RỦI RO CAO NHẤT (BLOCKER)**. Cần Phụng và Trí hỗ trợ dựng gấp Module Phí. |
| **Danh** | QA Lead, Ma trận kiểm thử toàn luồng, Test trên thiết bị PC/mobile, Báo cáo lỗi. | Đã phát hiện lỗi runtime `CancellationToken` trên live backend. Chưa commit test matrix chính thức lên repo. | **TÍCH CỰC**. Cần tiếp tục kiểm thử luồng thực tế ngay sau khi hotfix. |

---

## PHẦN I: KẾ HOẠCH TRIỂN KHAI RUSH PLAN (MINIMAL-DIFF)

Để kịp tiến độ demo Sprint 3 mà không làm xáo trộn hệ thống, thứ tự triển khai tối thiểu như sau:

```
[BƯỚC 1: SỬA LỖI NỀN & MERGE HOTFIX] (Ưu tiên số 1 - Phụng & Trí)
  ├── 1.1. Sửa lỗi cú pháp dòng 41 trong EmailService.cs (Xóa ````).
  ├── 1.2. Merge commit hotfix afac36b vào Sprint3/demo (Fix advisory lock).
  └── 1.3. Khôi phục AuthController (Đăng ký Customer + Gmail OTP).

[BƯỚC 2: BỔ SUNG PHÂN QUYỀN 2 LOẠI STAFF] (Phụng & Trí)
  ├── 2.1. Thêm cột Duty ('GATE', 'DISPATCH', 'BOTH') vào bảng staff_lot_assignments.
  └── 2.2. Tách CanOperateGateAsync và CanOperateDispatchAsync trong ParkingAuthorizationService.

[BƯỚC 3: KẾT NỐI HÀNG ĐỢI XE CHỜ ĐIỀU PHỐI] (Phụng)
  ├── 3.1. CheckInAsync: Tự động tạo ParkingDispatch (Status = PENDING) khi xe qua cổng.
  └── 3.2. Bổ sung endpoint GET /api/v1/parking-dispatches/pending-queue cho Staff điều phối.

[BƯỚC 4: TRIỂN KHAI GẤP MODULE TÍNH PHÍ & THU TIỀN] (Phụng hỗ trợ Nam Anh)
  ├── 4.1. Tạo bảng và cấu hình Giá gửi xe đơn giản (theo block giờ).
  ├── 4.2. Viết API GET /parking-sessions/{id}/fee và POST /parking-sessions/{id}/pay.
  └── 4.3. Siết chặt CheckOutAsync: Bắt buộc session.Status == PAID mới cho check-out.

[BƯỚC 5: FRONTEND GHÉP MÀN HÌNH STAFF VẬN HÀNH] (Song)
  ├── 5.1. Màn hình Staff Cổng (PC): Check-in xe vãng lai, hiển thị ảnh OCR của Đức, Thu tiền, Check-out.
  └── 5.2. Màn hình Staff Điều phối (Mobile): Danh sách xe chờ, chọn ô trống, xác nhận xe đã đỗ đúng ô.

[BƯỚC 6: KIỂM THỬ TOÀN LUỒNG & NGHIỆM THU] (Danh & Phụng)
  └── Chạy thông suốt 1 lượt gửi xe vãng lai thực tế từ A -> Z.
```

---

## PHẦN J: ĐỊNH NGHĨA HOÀN THÀNH CHO 1 LƯỢT GỬI XE VÃNG LAI (DoD)

Một lượt gửi xe vãng lai (Walk-in Session) chỉ được coi là hoàn thành khi thỏa mãn đầy đủ 8 bước sau trên hệ thống thực tế:

1. **Bước 1 (Tiếp nhận tại cổng):** Xe vãng lai đến cổng, Staff cổng gọi `POST /api/v1/parking-sessions/check-in` với biển số thực tế. Hệ thống kiểm tra sức chứa khả dụng $> 0$, tạo `ParkingSession` trạng thái `ACTIVE` và tự động tạo `ParkingDispatch` trạng thái `PENDING`. Ô đỗ trong bãi chưa bị chuyển sang `OCCUPIED`.
2. **Bước 2 (Hàng đợi điều phối):** Staff điều phối mở màn hình mobile, nhìn thấy xe vừa vào trong danh sách chờ (`pending-queue`).
3. **Bước 3 (Gán ô mục tiêu):** Staff điều phối chọn 1 ô đỗ trống tương thích (ví dụ `S-01-05`), gọi `POST /api/v1/parking-dispatches/{id}/assign`. Ô `S-01-05` được đánh dấu giữ chỗ điều phối (`IsReservedForDispatch = true`), sức chứa bãi xe tự động trừ 1 chỗ.
4. **Bước 4 (Xác nhận đỗ thực tế):** Khi xe vào đỗ thực tế tại ô, Staff điều phối gọi `POST /api/v1/parking-dispatches/{id}/confirm-occupied`. Trạng thái ô `S-01-05` chuyển sang `OCCUPIED`. Mô hình 3D Viewer lập tức hiển thị xe màu đỏ tại đúng ô `S-01-05`.
5. **Bước 5 (Tính phí):** Xe chuẩn bị rời bãi, Staff cổng tra cứu phiên đỗ xe, hệ thống tính phí chính xác dựa trên thời gian thực tế đã gửi.
6. **Bước 6 (Thu tiền):** Staff cổng thu tiền thực tế và gọi API xác nhận thanh toán. `ParkingSession` chuyển sang trạng thái `PAID`. Ô đỗ `S-01-05` **vẫn giữ nguyên trạng thái OCCUPIED**.
7. **Bước 7 (Cổng ra):** Xe tiến đến barie cổng ra, Staff cổng đối chiếu biển số và gọi `POST /api/v1/parking-sessions/{id}/check-out`. Hệ thống xác minh phiên đã `PAID`, chuyển trạng thái phiên sang `COMPLETED`, giải phóng ô `S-01-05` về `EMPTY`, và tăng sức chứa khả dụng của bãi xe lên 1.
8. **Bước 8 (Đồng bộ):** Mô hình 3D Viewer và giao diện web cập nhật ô `S-01-05` chuyển về màu xanh (Trống). Dữ liệu được bảo toàn bền vững trên Neon DB.

---

## PHẦN K: CÁC QUYẾT ĐỊNH OWNER CẦN CHỐT TRƯỚC KHI VIẾT CODE

Trước khi tiến hành sửa code trong Sprint 3, Product Owner (Võ Phụng) cần thống nhất dứt khoát 5 điểm sau cùng cả nhóm:

1. **Phân quyền Staff:** Chốt phương án thêm cột `Duty` (`GATE`, `DISPATCH`, `BOTH`) vào `StaffLotAssignment` để phân định quyền hạn tại API mà không cần tạo thêm Role mới trong bảng `roles`.
2. **Cơ chế Tính phí Tạm thời cho Sprint 3:** Vì Nam Anh chưa có code, thống nhất áp dụng biểu phí cố định đơn giản (ví dụ: 10.000 VNĐ / lượt hoặc 5.000 VNĐ / giờ) trực tiếp trong Backend của Phụng để thông luồng thanh toán trước ngày demo.
3. **Chính sách Tương thích Nhiên liệu:** Xác nhận trong Sprint 3 bãi xe có phân chia ô riêng cho xe điện (EV Charging Slot) hay toàn bộ ô đều cho phép đỗ chung (Gasoline/EV).
4. **Phạm vi Booking:** Tuyên bố chính thức với cả nhóm và giảng viên: **Booking Đặt chỗ trước là tính năng của Sprint 4**. Buổi demo Sprint 3 sẽ tập trung 100% vào luồng **Khách vãng lai và Điều phối thời gian thực**.
5. **Merge Hotfix `afac36b`:** Cho phép merge nhánh `hotfix/booking-sql-cancellation-token` vào `Sprint3/demo` để sửa dứt điểm lỗi crash advisory lock.
