# SMART PARKING SYSTEM – HIGH-LEVEL ERD OVERVIEW
## SƠ ĐỒ QUAN HỆ THỰC THỂ TỔNG THỂ TOÀN HỆ THỐNG

- **Dự án:** Smart Parking System (Mô phỏng 3D kết hợp Quản trị Vận hành Thực tế)
- **Chương trình:** FPT Software OJT
- **Chịu trách nhiệm kiến trúc CSDL:** Phụng (Backend / Database Lead)
- **Tài liệu:** Task 02B – Sơ đồ ERD Tổng thể (High-Level ERD)
- **Hệ quản trị CSDL mục tiêu:** PostgreSQL trên Neon Cloud
- **Trạng thái:** Thiết kế kiến trúc dữ liệu (Chưa tạo Entity/DbContext/Migration/SQL)

---

## 1. TỔNG QUAN KIẾN TRÚC THỰC THỂ HỆ THỐNG

Hệ thống cơ sở dữ liệu Smart Parking được tổ chức thành **7 nhóm nghiệp vụ chính** với sự phân cấp rõ ràng giữa các bảng **CORE** (Bắt buộc cho Sprint 3), **CONDITIONAL** (Cần nhóm xác nhận kỹ thuật), và **FUTURE** (Dự phòng cho Sprint 4):

```
+----------------------------------------------------------------------------------------------------+
|                                    SMART PARKING SYSTEM DOMAINS                                    |
+----------------------------------------------------------------------------------------------------+
  [A. Identity & Access]            [B. Parking Structure]           [C. Vehicles & Sessions]
   - users (CORE)                    - parking_lots (CORE)            - vehicles (CORE)
   - roles (CORE)                    - parking_floors (CORE)          - parking_sessions (CORE)
   - user_tokens (CORE)              - parking_slots (CORE)           - parking_reservations (FUTURE)
   - refresh_tokens (CORE)                                            - subscriptions (FUTURE)
              |                                 |                                |
              +---------------------------------+--------------------------------+
                                                |
                            [D. Parking Dispatch & Allocation]
                             - parking_dispatches (CORE)
                                                |
              +---------------------------------+--------------------------------+
              |                                 |                                |
  [E. Pricing & Payments]           [F. Hardware Events & IoT]       [G. Audit & Operations]
   - pricing_policies (CORE)         - gate_events (CORE)             - audit_logs (CORE)
   - payments (CORE)                 - slot_sensor_logs (CONDITIONAL) - alerts (CONDITIONAL)
                                                                      - system_configs (CONDITIONAL)
+----------------------------------------------------------------------------------------------------+
```

---

## 2. SƠ ĐỒ ERD TỔNG THỂ HỆ THỐNG (SYSTEM-WIDE ERD)

Dưới đây là sơ đồ Mermaid thể hiện mối quan hệ liên kết giữa các bảng trong toàn bộ 7 phân hệ (đã loại bỏ `vehicle_types` do hệ thống chỉ phục vụ ô tô thông thường):

```mermaid
erDiagram
    %% ==========================================
    roles ||--o{ users : "vai trò người dùng (1-N)"
    users ||--o{ refresh_tokens : "sở hữu phiên"
    users ||--o{ user_tokens : "nhận mã OTP xác minh"
    users ||--o{ vehicles : "đăng ký xe"
    users ||--o{ audit_logs : "thực hiện thao tác"

    %% ==========================================
    %% NHÓM B: PARKING STRUCTURE
    %% ==========================================
    parking_lots ||--o{ parking_floors : "bao gồm các tầng"
    parking_floors ||--o{ parking_slots : "chứa các ô đỗ"
    parking_lots ||--o{ pricing_policies : "áp dụng biểu phí ô tô"
    parking_lots ||--o{ parking_sessions : "phục vụ lượt xe"

    %% ==========================================
    %% NHÓM C: VEHICLES & PARKING SESSIONS
    %% ==========================================
    vehicles ||--o{ parking_sessions : "tham gia phiên đỗ"

    %% ==========================================
    %% NHÓM D: PARKING DISPATCH
    %% ==========================================
    parking_sessions ||--o{ parking_dispatches : "phát lệnh điều phối"
    parking_slots ||--o{ parking_dispatches : "được chỉ định ô"
    parking_slots ||--o{ parking_sessions : "vị trí đỗ thực tế"

    %% ==========================================
    %% NHÓM E: PRICING & PAYMENTS
    %% ==========================================
    parking_sessions ||--o{ payments : "thanh toán phí"

    %% ==========================================
    %% NHÓM F: HARDWARE EVENTS & IOT
    %% ==========================================
    parking_sessions ||--o{ gate_events : "ghi nhận sự kiện cổng"
    parking_slots ||--o{ slot_sensor_logs : "nhận tín hiệu cảm biến"

    %% ==========================================
    %% NHÓM G: AUDIT & OPERATIONS
    %% ==========================================
    parking_slots ||--o{ alerts : "cảnh báo tại ô"
    parking_sessions ||--o{ alerts : "cảnh báo phiên xe"

    %% ==========================================
    %% ĐỊNH NGHĨA CÁC THỰC THỂ
    %% ==========================================
    users {
        BIGINT id PK
        VARCHAR username UK
        VARCHAR email UK
        VARCHAR password_hash
        INT role_id FK
        VARCHAR full_name
        VARCHAR phone_number
        BOOLEAN is_active
        BOOLEAN is_email_verified
        VARCHAR approval_status
    }

    roles {
        INT id PK
        VARCHAR code UK
        VARCHAR name
    }

    refresh_tokens {
        BIGINT id PK
        BIGINT user_id FK
        VARCHAR token_hash UK
        BOOLEAN is_revoked
    }

    user_tokens {
        BIGINT id PK
        BIGINT user_id FK
        VARCHAR token_type
        VARCHAR token_hash
    }

    parking_lots {
        INT id PK
        VARCHAR name UK
        INT total_capacity
        BOOLEAN is_active
    }

    parking_floors {
        INT id PK
        INT lot_id FK
        INT floor_number
        VARCHAR name
    }

    parking_slots {
        BIGINT id PK
        INT floor_id FK
        VARCHAR slot_code
        VARCHAR physical_status
    }

    vehicles {
        BIGINT id PK
        BIGINT user_id FK
        VARCHAR plate_number UK
    }

    parking_sessions {
        BIGINT id PK
        INT lot_id FK
        BIGINT vehicle_id FK
        VARCHAR plate_number
        TIMESTAMP check_in_time
        TIMESTAMP check_out_time
        BIGINT current_slot_id FK
        VARCHAR status
        DECIMAL total_fee
        TIMESTAMP paid_at
    }

    parking_dispatches {
        BIGINT id PK
        BIGINT session_id FK
        BIGINT assigned_slot_id FK
        BIGINT actual_slot_id FK
        VARCHAR status
    }

    pricing_policies {
        INT id PK
        INT lot_id FK
        DECIMAL first_hour_fee
        DECIMAL incremental_hour_fee
        DECIMAL daily_max_cap
    }

    payments {
        BIGINT id PK
        BIGINT session_id FK
        VARCHAR payment_method
        DECIMAL amount
        VARCHAR transaction_code UK
        VARCHAR status
    }

    gate_events {
        BIGINT id PK
        VARCHAR gate_code
        VARCHAR event_type
        BIGINT session_id FK
        VARCHAR raw_plate_number
        DECIMAL confidence_score
    }

    slot_sensor_logs {
        BIGINT id PK
        BIGINT slot_id FK
        VARCHAR state_change
    }

    audit_logs {
        BIGINT id PK
        BIGINT actor_id FK
        VARCHAR action
        VARCHAR target_entity
    }

    alerts {
        BIGINT id PK
        VARCHAR alert_type
        BIGINT reference_slot_id FK
        BIGINT reference_session_id FK
        BOOLEAN is_resolved
    }
```

---

## 3. PHÂN ĐỊNH RANH GIỚI VÀ MỐI QUAN HỆ GIỮA CÁC PHÂN HỆ

### 3.1. Phân hệ Trung tâm: Phiên gửi xe (`parking_sessions`)
- Bảng `parking_sessions` là trái tim kết nối toàn bộ hệ thống.
- **Vào cổng:** Nhận dữ liệu từ `gate_events` (Nhận diện biển số, ảnh chụp vào).
- **Hồ sơ phương tiện:** Tra cứu `vehicles` để xác định xe ô tô đã đăng ký hoặc ghi nhận xe ô tô vãng lai.
- **Điều phối:** Kích hoạt tạo bản ghi trong `parking_dispatches` để chỉ định ô đỗ.
- **Đỗ xe thực tế:** Khi cảm biến ô đỗ xác nhận, trường `current_slot_id` liên kết trực tiếp tới `parking_slots`.
- **Thanh toán:** Tạo liên kết 1-N tới `payments` để ghi nhận các giao dịch thanh toán.
- **Xuất bãi:** Xác nhận điều kiện xuất bãi với `gate_events` cổng ra, ghi nhận `check_out_time` và chuyển trạng thái `COMPLETED`.

### 3.2. Mối quan hệ giữa Accounts và Vận hành bãi xe
- Người dùng (`users`) có thể liên kết 1-N với phương tiện cá nhân (`vehicles`).
- Khách vãng lai chưa có tài khoản vẫn có thể vào bãi: Khi đó, trường `vehicles.user_id` có giá trị `NULL`, hệ thống vẫn lưu biển số xe vào `parking_sessions.plate_number` bình thường.
- Nhân viên vận hành (`users` có role `STAFF`), Quản lý (`MANAGER`) và Quản trị viên (`ADMIN`) tương tác với hệ thống qua các hành động ghi nhận vào `audit_logs` (thao tác điều phối thủ công, mở barrier cưỡng bức, sửa giá).

### 3.3. Tách biệt Trạng thái Ô đỗ và Trạng thái Điều phối
- Ô đỗ (`parking_slots`) **chỉ quản lý trạng thái vật lý thực tế** (`physical_status`: `EMPTY`, `OCCUPIED`, `MAINTENANCE`, `UNAVAILABLE`). Không tự giả định toàn bộ slot có cùng kích thước.
- Lệnh điều phối (`parking_dispatches`) **quản lý trạng thái phân công logic** (`PENDING`, `ASSIGNED`, `OCCUPIED`, `WRONG_SLOT`, `CANCELLED`).
- Tính khả dụng của một slot được tính toán bằng biểu thức:
  $$\text{Slot Available} \iff \text{physical\_status} = \text{'EMPTY'} \land \neg \exists \text{ active dispatch with status 'ASSIGNED' trỏ tới slot đó}$$

---

## 4. DANH MỤC PHÂN BỔ TRẠNG THÁI TRIỂN KHAI (19 BẢNG TOÀN HỆ THỐNG)

Toàn hệ thống thống nhất chuẩn hóa **19 bảng** (14 CORE, 3 CONDITIONAL, 2 FUTURE):

| Nhóm nghiệp vụ | Bảng CORE (14 bảng - Sprint 3) | Bảng CONDITIONAL (3 bảng - Cần xác nhận) | Bảng FUTURE (2 bảng - Sprint 4) |
| :--- | :--- | :--- | :--- |
| **A. Accounts** | `users`, `roles`, `user_tokens`, `refresh_tokens` | - | - |
| **B. Structure** | `parking_lots`, `parking_floors`, `parking_slots` | - | - |
| **C. Sessions** | `vehicles`, `parking_sessions` | - | `parking_reservations`, `subscriptions` |
| **D. Dispatch** | `parking_dispatches` | - | - |
| **E. Pricing** | `pricing_policies`, `payments` | - | - |
| **F. Hardware** | `gate_events` | `slot_sensor_logs` | - |
| **G. Audit/Ops** | `audit_logs` | `alerts`, `system_configs` | - |

*(Ghi chú: Mô hình phân hệ Accounts đã được Owner phê duyệt chính thức sử dụng mô hình 1-N `users.role_id`, loại bỏ bảng `user_roles`; bổ sung đầy đủ `user_tokens` và `refresh_tokens` vào CORE).*

---

## 5. TỔNG KẾT
Sơ đồ ERD tổng thể cung cấp cái nhìn toàn cảnh về kiến trúc liên kết giữa các bảng. Tài liệu tiếp theo [07_ACCOUNTS_ERD.md](file:///e:/smartparking/docs/database/07_ACCOUNTS_ERD.md) sẽ đi sâu vào thiết kế chi tiết cấp thuộc tính cho Phân hệ Accounts & Authentication theo yêu cầu bàn giao cho Song.
