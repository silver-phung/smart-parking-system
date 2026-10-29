# SMART PARKING SPRINT 3 – BÀN GIAO API TÍCH HỢP CHO SONG (FRONTEND)

Tài liệu này tổng hợp toàn bộ API Backend do Phụng sở hữu đã được kiểm thử 100% (347/347 tests PASS) sẵn sàng tích hợp với giao diện Frontend cho buổi Demo Sprint 3 ngày 30/10/2026.

---

## 1. QUY CHUẨN KẾT NỐI CHUNG
- **Base URL Backend:** `http://localhost:5000` (hoặc qua Gateway: `http://localhost:5001`)
- **Prefix chuẩn:** `/api/v1/...`
- **Header xác thực:** `Authorization: Bearer <accessToken>` (dành cho các endpoint yêu cầu quyền `USER`, `STAFF`, hoặc `ADMIN`).
- **Content-Type:** `application/json`

---

## 2. NHÓM 1: AUTHENTICATION & ONBOARDING

### 2.1 Đăng ký tài khoản khách hàng (`POST /api/v1/auth/register`)
- **Vai trò:** Anonymous
- **Request Body:**
  ```json
  {
    "username": "customer_demo",
    "password": "Password@123",
    "email": "customer@smartparking.vn",
    "fullName": "Nguyễn Văn Demo",
    "phoneNumber": "0901234567",
    "recaptchaToken": "optional-token"
  }
  ```
- **Response (201 Created):**
  ```json
  {
    "message": "Đăng ký tài khoản thành công. Vui lòng kiểm tra email để lấy mã xác minh.",
    "data": {
      "id": 105,
      "username": "customer_demo",
      "email": "customer@smartparking.vn",
      "fullName": "Nguyễn Văn Demo",
      "role": "USER",
      "isEmailVerified": false
    }
  }
  ```

### 2.2 Xác minh Email OTP (`POST /api/v1/auth/verify-email`)
> [!IMPORTANT]
> Lưu ý: Tên trường trong request là `code` (6 chữ số), không phải `otp`.
- **Request Body:**
  ```json
  {
    "email": "customer@smartparking.vn",
    "code": "123456"
  }
  ```
- **Response (200 OK):**
  ```json
  {
    "message": "Xác minh email thành công. Tài khoản của bạn đã được kích hoạt."
  }
  ```

### 2.3 Gửi lại mã OTP (`POST /api/v1/auth/resend-verification`)
> [!NOTE]
> Hệ thống áp dụng cooldown 60 giây giữa 2 lần gửi lại OTP.
- **Request Body:**
  ```json
  {
    "email": "customer@smartparking.vn"
  }
  ```
- **Response (200 OK):**
  ```json
  {
    "message": "Mã xác minh mới đã được gửi tới email của bạn."
  }
  ```

### 2.4 Đăng nhập (`POST /api/v1/auth/login`)
- **Request Body:**
  ```json
  {
    "username": "customer_demo",
    "password": "Password@123"
  }
  ```
- **Response (200 OK):**
  ```json
  {
    "message": "Đăng nhập thành công.",
    "data": {
      "accessToken": "eyJhbGciOi...",
      "refreshToken": "7c8e9b...",
      "tokenType": "Bearer",
      "expiresIn": 900,
      "userProfile": {
        "id": 105,
        "username": "customer_demo",
        "email": "customer@smartparking.vn",
        "fullName": "Nguyễn Văn Demo",
        "role": "USER"
      }
    }
  }
  ```

---

## 3. NHÓM 2: USER PROFILE

### 3.1 Xem hồ sơ cá nhân (`GET /api/v1/users/me`)
- **Auth:** `Bearer <token>` (USER, ADMIN)
- **Response (200 OK):**
  ```json
  {
    "message": "Lấy thông tin người dùng thành công.",
    "data": {
      "id": 105,
      "username": "customer_demo",
      "email": "customer@smartparking.vn",
      "fullName": "Nguyễn Văn Demo",
      "phoneNumber": "0901234567",
      "role": "USER",
      "isEmailVerified": true
    }
  }
  ```

### 3.2 Cập nhật hồ sơ (`PUT /api/v1/users/me`)
- **Auth:** `Bearer <token>` (USER, ADMIN)
- **Request Body:**
  ```json
  {
    "fullName": "Nguyễn Văn Demo (VIP)",
    "phoneNumber": "0987654321"
  }
  ```

---

## 4. NHÓM 3: QUẢN LÝ XE (VEHICLE CRUD)

### 4.1 Danh sách xe của tôi (`GET /api/v1/vehicles`)
- **Auth:** `Bearer <token>`
- **Response (200 OK):** Danh sách xe do người dùng sở hữu có phân trang (`items`, `totalCount`, `page`, `pageSize`).

### 4.2 Thêm xe mới (`POST /api/v1/vehicles`)
- **Auth:** `Bearer <token>`
- **Request Body:**
  ```json
  {
    "plateNumber": "51H-999.88",
    "brand": "Toyota",
    "model": "Camry",
    "color": "Đen"
  }
  ```
- **Response (201 Created):**
  ```json
  {
    "message": "Đăng ký phương tiện thành công.",
    "data": {
      "id": 12,
      "plateNumber": "51H-999.88",
      "brand": "Toyota",
      "model": "Camry",
      "color": "Đen",
      "isActive": true
    }
  }
  ```

### 4.3 Cập nhật xe (`PUT /api/v1/vehicles/{id:long}`)
- **Auth:** `Bearer <token>`
- **Request Body:** `{ "brand": "Toyota", "model": "Camry 2026", "color": "Trắng", "isActive": true }`

### 4.4 Xóa xe (`DELETE /api/v1/vehicles/{id:long}`)
- **Auth:** `Bearer <token>`
- **Lưu ý:** Chỉ cho phép xóa xe chưa từng phát sinh phiên gửi xe trong hệ thống.

---

## 5. NHÓM 4: BÃI ĐỖ XE & TRA CỨU SỨC CHỨA CÔNG KHAI

### 5.1 Danh sách bãi đỗ xe (`GET /api/v1/parking-lots`)
- **Auth:** Anonymous (Công khai)
- **Query params:** `?name=...&isActive=true&page=1&pageSize=10`

### 5.2 Chi tiết bãi đỗ xe (`GET /api/v1/parking-lots/{id:int}`)
- **Auth:** Anonymous (Công khai)

### 5.3 Tra cứu sức chứa thời gian thực (`GET /api/v1/parking-lots/{id:int}/capacity`)
> [!IMPORTANT]
> Lưu ý: Route chính xác là `/capacity` và ID là số nguyên (`int`), không phải `/availability`.
- **Auth:** Anonymous (Công khai)
- **Response (200 OK):**
  ```json
  {
    "message": "Lấy thông tin sức chứa bãi đỗ xe thành công.",
    "data": {
      "lotId": 1,
      "lotName": "Bãi Đỗ Xe Trung Tâm",
      "totalCapacity": 100,
      "physicalOccupiedCount": 45,
      "activeSessionsCount": 42,
      "bookingHoldsCount": 5,
      "effectiveOccupiedCount": 50,
      "availableCapacity": 50,
      "isFull": false
    }
  }
  ```

---

## 6. NHÓM 5: ĐẶT CHỖ (BOOKING)

### 6.1 Tạo lượt đặt chỗ (`POST /api/v1/bookings`)
- **Auth:** `Bearer <token>` (USER, ADMIN)
- **Request Body:**
  ```json
  {
    "lotId": 1,
    "vehicleId": 12,
    "plateNumber": "51H-999.88",
    "startTime": "2026-10-30T08:00:00Z",
    "endTime": "2026-10-30T12:00:00Z",
    "notes": "Đặt chỗ buổi sáng"
  }
  ```
- **Response (201 Created):** Trả về đối tượng `BookingDto` với `status: "PENDING"` hoặc `"CONFIRMED"`.

### 6.2 Lịch sử đặt chỗ của tôi (`GET /api/v1/bookings/my`)
- **Auth:** `Bearer <token>` (USER)

### 6.3 Hủy lượt đặt chỗ (`POST /api/v1/bookings/{id:long}/cancel`)
> [!IMPORTANT]
> Lưu ý: Phương thức HTTP là `POST`, không phải `PUT`.
- **Request Body:**
  ```json
  {
    "reason": "Thay đổi lịch trình"
  }
  ```

---

## 7. NHÓM 6: PHIÊN ĐỖ XE & CỔNG RA VÀO (SESSIONS & GATE)

### 7.1 Lịch sử phiên gửi xe của tôi (`GET /api/v1/parking-sessions/my-sessions`)
- **Auth:** `Bearer <token>` (USER)
- **Response (200 OK):** Trả về danh sách phiên đỗ xe của người dùng (gồm cả phiên đang đỗ `ACTIVE` và phiên đã hoàn tất `COMPLETED`).

### 7.2 Check-in tại cổng (`POST /api/v1/parking-sessions/check-in`)
- **Auth:** `Bearer <token>` (STAFF, ADMIN)
- **Request Body:**
  ```json
  {
    "plateNumber": "51H-999.88",
    "parkingLotId": 1,
    "gateId": 1,
    "checkInImageUrl": "https://res.cloudinary.com/..."
  }
  ```

### 7.3 Xác minh ô đỗ thực tế (`POST /api/v1/parking-sessions/{id:long}/verify-slot`)
- **Auth:** `Bearer <token>` (STAFF có nhiệm vụ `SLOT_VERIFIER`, ADMIN)
- **Request Body:**
  ```json
  {
    "slotId": 5,
    "notes": "Xác minh xe đỗ đúng ô A-05"
  }
  ```

### 7.4 Check-out xuất bãi (`POST /api/v1/parking-sessions/{id:long}/check-out`)
> [!IMPORTANT]
> Lưu ý: Thuộc tính đối chiếu biển số ra là `confirmedPlateNumber`, không phải `exitPlateNumber`.
- **Auth:** `Bearer <token>` (STAFF, ADMIN)
- **Request Body:**
  ```json
  {
    "confirmedPlateNumber": "51H-999.88",
    "checkOutImageUrl": "https://res.cloudinary.com/..."
  }
  ```
