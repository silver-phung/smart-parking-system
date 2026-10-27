# SMART PARKING – ADMIN USER MANAGEMENT API HANDOVER

**Mục tiêu:** Tài liệu bàn giao 5 API Quản lý Người dùng dành cho Song (Frontend ADMIN).  
**Workspace:** `E:\smartparking`  
**Base URL:** `http://localhost:5295/api/v1/admin/users`  
**Authentication:** `Bearer {jwt_access_token}`  
**Role yêu cầu:** `ADMIN` (Người dùng không phải ADMIN sẽ nhận `403 Forbidden`).

---

## 1. TỔNG QUAN DANH SÁCH ENDPOINTS

| STT | Phương thức | Endpoint | Mô tả chức năng | Vai trò |
| :---: | :---: | :--- | :--- | :---: |
| 1 | `GET` | `/api/v1/admin/users` | Lấy danh sách người dùng (phân trang, tìm kiếm, lọc) | `ADMIN` |
| 2 | `GET` | `/api/v1/admin/users/{id}` | Lấy chi tiết tài khoản (kèm phân công bãi & số xe) | `ADMIN` |
| 3 | `POST` | `/api/v1/admin/users` | Tạo tài khoản nhân sự / quản lý / người dùng mới | `ADMIN` |
| 4 | `PATCH` | `/api/v1/admin/users/{id}/status` | Bật / Tắt kích hoạt tài khoản (`IsActive`) | `ADMIN` |
| 5 | `PATCH` | `/api/v1/admin/users/{id}/role` | Thay đổi vai trò (`ADMIN`, `MANAGER`, `STAFF`, `USER`) | `ADMIN` |

---

## 2. CHI TIẾT TỪNG API

### 2.1. GET `/api/v1/admin/users` – Danh sách người dùng

**Query Parameters:**
- `page` (int, default: 1): Số trang (bắt đầu từ 1).
- `pageSize` (int, default: 10, max: 100): Số lượng bản ghi mỗi trang.
- `search` (string, optional): Từ khóa tìm kiếm theo `username`, `email` hoặc `fullName`.
- `role` (string, optional): Lọc theo vai trò (`ADMIN`, `MANAGER`, `STAFF`, `USER`).
- `isActive` (bool, optional): Lọc theo trạng thái hoạt động (`true` hoặc `false`).
- `approvalStatus` (string, optional): Lọc theo phê duyệt (`PENDING`, `APPROVED`, `REJECTED`).

**Response thành công (`200 OK`):**
```json
{
  "message": "Lấy danh sách người dùng thành công.",
  "data": {
    "items": [
      {
        "id": 1,
        "username": "admin",
        "email": "admin@smartparking.vn",
        "fullName": "System Administrator",
        "phoneNumber": "0912345678",
        "role": "ADMIN",
        "isActive": true,
        "isEmailVerified": true,
        "approvalStatus": "APPROVED",
        "createdAt": "2026-10-27T10:00:00Z",
        "updatedAt": "2026-10-27T10:00:00Z"
      },
      {
        "id": 2,
        "username": "manager_binh",
        "email": "binh.manager@smartparking.vn",
        "fullName": "Nguyễn Văn Bình",
        "phoneNumber": "0987654321",
        "role": "MANAGER",
        "isActive": true,
        "isEmailVerified": true,
        "approvalStatus": "APPROVED",
        "createdAt": "2026-10-27T10:15:00Z",
        "updatedAt": "2026-10-27T10:15:00Z"
      }
    ],
    "totalCount": 2,
    "pageNumber": 1,
    "pageSize": 10,
    "totalPages": 1,
    "hasPreviousPage": false,
    "hasNextPage": false
  }
}
```

---

### 2.2. GET `/api/v1/admin/users/{id}` – Chi tiết người dùng

**Path Parameters:**
- `id` (long, required): ID tài khoản cần xem.

**Response thành công (`200 OK`):**
```json
{
  "message": "Lấy thông tin người dùng thành công.",
  "data": {
    "id": 2,
    "username": "manager_binh",
    "email": "binh.manager@smartparking.vn",
    "fullName": "Nguyễn Văn Bình",
    "phoneNumber": "0987654321",
    "avatarUrl": null,
    "role": "MANAGER",
    "isActive": true,
    "isEmailVerified": true,
    "approvalStatus": "APPROVED",
    "approvedBy": 1,
    "approvedByUsername": "admin",
    "approvedAt": "2026-10-27T10:15:00Z",
    "failedLoginAttempts": 0,
    "lockoutEndAt": null,
    "activeAssignmentsCount": 1,
    "vehiclesCount": 0,
    "activeAssignments": [
      {
        "lotId": 1,
        "lotName": "Bãi đỗ xe Landmark",
        "lotAddress": "Số 1 Đường Hầm, Q.1",
        "isActive": true,
        "assignedAt": "2026-10-27T10:30:00Z"
      }
    ],
    "createdAt": "2026-10-27T10:15:00Z",
    "updatedAt": "2026-10-27T10:15:00Z"
  }
}
```

**Mã lỗi thường gặp:**
- `404 Not Found`: Không tìm thấy tài khoản với ID đã cung cấp.

---

### 2.3. POST `/api/v1/admin/users` – Tạo tài khoản mới bởi ADMIN

**Mục đích:** Tạo nhanh tài khoản cho nhân viên vận hành (`STAFF`) hoặc người quản lý bãi (`MANAGER`). Tài khoản do ADMIN tạo được mặc định phê duyệt (`APPROVED`) và xác minh email (`true`).

**Request Body:**
```json
{
  "username": "staff_tam",
  "email": "tam.staff@smartparking.vn",
  "password": "Password123@",
  "fullName": "Lê Hoàng Tâm",
  "phoneNumber": "0909112233",
  "role": "STAFF",
  "isActive": true
}
```

**Quy tắc Validate:**
- `username`: Bắt buộc, 3–50 ký tự (`^[a-zA-Z0-9_.]{3,50}$`).
- `email`: Bắt buộc, đúng chuẩn email RFC.
- `password`: Bắt buộc, tối thiểu 8 ký tự (Mật khẩu khởi tạo ban đầu do Admin cấp trực tiếp cho nhân sự qua kênh nội bộ).
- `fullName`: Bắt buộc, tối đa 100 ký tự.
- `role`: Bắt buộc, **chỉ cho phép `'STAFF'` hoặc `'MANAGER'`** (Phạm vi tạo tài khoản nội bộ; không cho phép tự tạo ADMIN hoặc USER qua API này).
- `phoneNumber`: Không bắt buộc, định dạng số điện thoại hợp lệ.

> **Ghi chú về xác minh & mật khẩu ban đầu:**
> - Tài khoản do ADMIN tạo được kích hoạt trực tiếp với `isEmailVerified = true` và `approvalStatus = "APPROVED"` để nhân viên có thể đăng nhập ngay mà không qua luồng OTP tự đăng ký của khách hàng (USER).
> - Mật khẩu khởi tạo được quản trị viên cấp phát ban đầu cho nhân sự; nhân sự sử dụng mật khẩu này để đăng nhập vào hệ thống.

**Response thành công (`201 Created`):**
```json
{
  "message": "Tạo tài khoản người dùng thành công.",
  "data": {
    "id": 15,
    "username": "staff_tam",
    "email": "tam.staff@smartparking.vn",
    "fullName": "Lê Hoàng Tâm",
    "phoneNumber": "0909112233",
    "avatarUrl": null,
    "role": "STAFF",
    "isActive": true,
    "isEmailVerified": true,
    "approvalStatus": "APPROVED",
    "approvedBy": 1,
    "approvedByUsername": "admin",
    "approvedAt": "2026-10-27T12:00:00Z",
    "failedLoginAttempts": 0,
    "lockoutEndAt": null,
    "activeAssignmentsCount": 0,
    "vehiclesCount": 0,
    "activeAssignments": [],
    "createdAt": "2026-10-27T12:00:00Z",
    "updatedAt": "2026-10-27T12:00:00Z"
  }
}
```

**Mã lỗi thường gặp:**
- `400 Bad Request`: Thiếu trường bắt buộc, mật khẩu < 8 ký tự, hoặc vai trò không phải `STAFF`/`MANAGER`.
- `409 Conflict`: Tên đăng nhập hoặc Email đã tồn tại trong hệ thống.

---

### 2.4. PATCH `/api/v1/admin/users/{id}/status` – Bật / Tắt hoạt động tài khoản

**Mục đích:** Khóa tài khoản tạm thời khi nhân viên nghỉ việc hoặc vi phạm quy chế.

**Request Body:**
```json
{
  "isActive": false,
  "reason": "Tạm đình chỉ công tác để kiểm tra an toàn."
}
```

**Hệ quả bảo mật phía Server:**
1. Khi `isActive = false`, toàn bộ phân công bãi đỗ xe active (`StaffLotAssignments`) của người này sẽ **tự động được thu hồi** (soft-revoke) với lý do đã nhập.
2. Toàn bộ `RefreshTokens` đang hoạt động của người này bị hủy bỏ ngay lập tức.
3. Access Token hiện có của người này sẽ **bị từ chối ngay lập tức (401 Unauthorized)** trong mọi request tiếp theo nhờ cơ chế kiểm tra `OnTokenValidated` realtime từ DB.
4. **Bảo vệ Quản trị viên:**
   - Không cho phép ADMIN tự khóa chính mình (`400 Bad Request`).
   - Không cho phép khóa tài khoản ADMIN đang hoạt động cuối cùng của hệ thống (`400 Bad Request`).

**Response thành công (`200 OK`):**
```json
{
  "message": "Cập nhật trạng thái tài khoản 'staff_tam' thành vô hiệu hóa thành công.",
  "data": {
    "id": 15,
    "username": "staff_tam",
    "email": "tam.staff@smartparking.vn",
    "fullName": "Lê Hoàng Tâm",
    "phoneNumber": "0909112233",
    "role": "STAFF",
    "isActive": false,
    "isEmailVerified": true,
    "approvalStatus": "APPROVED",
    "activeAssignmentsCount": 0,
    "vehiclesCount": 0,
    "activeAssignments": [],
    "createdAt": "2026-10-27T12:00:00Z",
    "updatedAt": "2026-10-27T12:05:00Z"
  }
}
```

---

### 2.5. PATCH `/api/v1/admin/users/{id}/role` – Thay đổi vai trò người dùng

**Mục đích:** Thăng cấp hoặc điều chuyển vai trò nhân sự (ví dụ: `STAFF` lên `MANAGER`, hoặc hạ về `USER`).

**Request Body:**
```json
{
  "role": "MANAGER",
  "reason": "Thăng chức lên Quản lý bãi đỗ xe Landmark."
}
```

**Hệ quả bảo mật phía Server:**
1. Nếu hạ quyền từ `MANAGER`/`STAFF` về `USER`, toàn bộ phân công bãi đỗ xe active sẽ **tự động bị thu hồi** (do role `USER` không được phép nhận bãi xe).
2. Hủy bỏ `RefreshTokens` để buộc người dùng refresh và nhận token với claims mới.
3. Access Token hiện hành sẽ tự động cập nhật role mới ngay lập tức trong pipeline xác thực.
4. **Bảo vệ Quản trị viên:**
   - Không cho phép ADMIN tự hạ quyền tài khoản của chính mình (`400 Bad Request`).
   - Không cho phép hạ quyền tài khoản ADMIN đang hoạt động cuối cùng của hệ thống (`400 Bad Request`).

**Response thành công (`200 OK`):**
```json
{
  "message": "Cập nhật vai trò tài khoản 'staff_tam' thành 'MANAGER' thành công.",
  "data": {
    "id": 15,
    "username": "staff_tam",
    "email": "tam.staff@smartparking.vn",
    "fullName": "Lê Hoàng Tâm",
    "phoneNumber": "0909112233",
    "role": "MANAGER",
    "isActive": true,
    "isEmailVerified": true,
    "approvalStatus": "APPROVED",
    "activeAssignmentsCount": 0,
    "vehiclesCount": 0,
    "activeAssignments": [],
    "createdAt": "2026-10-27T12:00:00Z",
    "updatedAt": "2026-10-27T12:10:00Z"
  }
}
```

---

## 3. BẢNG MÃ LỖI HTTP CHUẨN

| HTTP Status Code | Ý nghĩa | Khi nào trả về |
| :---: | :--- | :--- |
| `200 OK` | Thành công | GET danh sách, GET chi tiết, PATCH status/role thành công |
| `201 Created` | Đã tạo thành công | POST tạo tài khoản mới thành công |
| `400 Bad Request` | Dữ liệu không hợp lệ | Thiếu trường, mật khẩu < 8 ký tự, role sai, tự khóa mình, khóa last admin |
| `401 Unauthorized` | Chưa xác thực | Không truyền JWT token hoặc token hết hạn/bị vô hiệu hóa |
| `403 Forbidden` | Không có quyền | Đăng nhập bằng role không phải `ADMIN` (`MANAGER`, `STAFF`, `USER`) |
| `404 Not Found` | Không tìm thấy | ID tài khoản cần xem/sửa không tồn tại |
| `409 Conflict` | Xung đột dữ liệu | Tên đăng nhập (`username`) hoặc `email` đã có người khác sử dụng |
