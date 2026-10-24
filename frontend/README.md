# SmartPark - Giao diện Web cho dự án Smart Parking

Hai giao diện web thuần HTML/CSS/JS (không cần cài đặt hay build):

- **Web Client** (`client/`): tìm bãi trên bản đồ, xem sơ đồ vị trí, đặt chỗ, thanh toán, vé QR, quét/hiển thị QR check-in/out.
- **Web Admin** (`admin/`): dashboard real-time, heatmap, báo cáo doanh thu, cài đặt giá vé, xử lý cảnh báo.

Dữ liệu hiện là **dữ liệu mẫu** lưu trong `localStorage`, dùng chung giữa Client và Admin. Đặt chỗ ở Client sẽ hiện ngay ở Admin, và Admin chạy bộ giả lập cảm biến để trạng thái chỗ đỗ thay đổi liên tục.

## Đăng nhập (trang mở đầu)

Mở dự án sẽ vào trang đăng nhập. Nhập tài khoản để hệ thống tự chuyển đến đúng giao diện. Khách mới có thể chọn **Đăng ký**, nhập họ tên, email và mật khẩu (ít nhất 8 ký tự); tài khoản mới chỉ có quyền dùng Web Client. Tài khoản đăng ký được lưu trên trình duyệt hiện tại.

| Cổng | Email | Mật khẩu |
| --- | --- | --- |
| Khách đỗ xe (vào Web Client) | `khach@smartpark.vn` | `khach123` |
| Ban quản lý (vào Web Admin) | `admin@smartpark.vn` | `admin123` |

Đăng nhập và đăng ký hiện chỉ lưu trong trình duyệt để demo (mật khẩu lưu dạng văn bản, không an toàn, không dùng cho môi trường thật). Không có chức năng tự đăng ký tài khoản Admin. Xem `HUONG-DAN-TUY-CHINH.md`.

**Muốn thêm animation hoặc ảnh nền?** Đọc `HUONG-DAN-TUY-CHINH.md`. Trong code, bấm `Ctrl + Shift + F` và tìm `[ANIM]` hoặc `[HINH-NEN]` để thấy mọi chỗ đã đánh dấu. Ảnh đặt trong `assets/img/`.

## Chạy thử

Nên chạy bằng máy chủ tĩnh (để hai trang dùng chung dữ liệu và bản đồ tải ổn định):

```bash
cd smart-parking
python3 -m http.server 8080
# hoặc: npx serve
```

Mở `http://localhost:8080` (trang đăng nhập). Phiên đăng nhập lưu theo từng tab, nên bạn đăng nhập tài khoản Khách ở một tab và tài khoản Ban quản lý ở tab khác để xem đồng bộ. Đóng tab là phải đăng nhập lại.
Cần có mạng để tải bản đồ (Leaflet + OpenStreetMap), thư viện tạo QR và phông chữ.

## Cấu trúc thư mục

```
index.html              Trang đăng nhập (trang mở đầu)
HUONG-DAN-TUY-CHINH.md  Ghi chú chỗ thêm animation, ảnh nền, tài khoản
assets/img/             Thả ảnh nền và ảnh bãi xe vào đây (xem README.txt trong thư mục)
assets/css/base.css     Màu sắc, nút, form, ô chỗ đỗ, hiệu ứng dùng chung (sáng/tối)
assets/css/login.css    Giao diện trang đăng nhập
assets/js/core.js       Dữ liệu mẫu, tài khoản, trạng thái, giá vé, đặt chỗ, QR (dùng chung)
assets/js/login.js      Logic trang đăng nhập
client/                 index.html, client.css, client.js
admin/                  index.html, admin.css, admin.js
```

## Chỉnh nhanh

| Muốn đổi | Sửa ở đâu |
| --- | --- |
| Tên thương hiệu | `SP.CFG.brand` trong `assets/js/core.js` |
| Màu chủ đạo | `--accent`, `--accent-soft` trong `assets/css/base.css` |
| Danh sách bãi, tọa độ, số tầng, số chỗ | `SP.LOTS` trong `assets/js/core.js` |
| Giá vé mặc định, phụ thu cao điểm | `DEF_PRICING` trong `assets/js/core.js` (hoặc chỉnh ngay ở Admin, mục Giá vé) |
| Logo | Thay phần tử `.logo` (chữ "P") trong `client/index.html` và `admin/index.html` bằng thẻ `<img>` |
| Đặt lại dữ liệu demo | Client, tab Tài khoản, nút "Đặt lại dữ liệu demo" |

## Luồng chính để demo

1. Client: chọn bãi trên bản đồ, bấm "Xem sơ đồ và đặt chỗ", chọn chỗ, nhập biển số, thanh toán, nhận vé QR.
2. Client, tab Quét QR: giả lập "Quét cổng VÀO" (check-in), sau đó "Quét cổng RA" (check-out, tính phí phát sinh).
3. Admin: xem chỗ đó đổi trạng thái theo thời gian thực, thấy hoạt động trong "Hoạt động gần đây", doanh thu hôm nay tăng.
4. Admin, mục Giá vé: đổi giá giờ đầu của ô tô, quay lại Client để thấy bảng giá mới.

## Khi nối backend

Toàn bộ truy cập dữ liệu nằm trong `assets/js/core.js`. Các hàm cần thay bằng API:

| Hàm hiện tại | Gợi ý API |
| --- | --- |
| `SP.LOTS`, `SP.counts()` | `GET /lots`, `GET /lots/{id}/slots` |
| trạng thái chỗ đỗ real-time | WebSocket / SSE từ dịch vụ cảm biến (IoT) |
| `SP.createBooking()`, `SP.cancelBooking()` | `POST /bookings`, `DELETE /bookings/{id}` |
| `SP.checkIn()`, `SP.completeCheckout()` | `POST /gate/in`, `POST /gate/out` (QR cổng) |
| `SP.price()`, `SP.state.pricing` | `GET/PUT /pricing` |
| `SP.state.alerts` | `GET /alerts`, `PATCH /alerts/{id}` |
| `SP.hist` (heatmap, doanh thu) | `GET /reports/heatmap`, `GET /reports/revenue` |

Thanh toán hiện là giả lập. Khi tích hợp thật, thay bước "Thanh toán" trong `client/client.js` (hàm `wizPay`) bằng luồng của MoMo, ZaloPay hoặc cổng thẻ.

## Lưu ý

- Mã QR vé có dạng `SPK|mã vé|token`, mã QR cổng có dạng `SPGATE|mã bãi|IN` hoặc `OUT`.
- Quét camera dùng `BarcodeDetector`, chỉ có trên một số trình duyệt (Chrome/Edge trên Android và desktop). Trình duyệt khác dùng nút giả lập hoặc ô nhập mã.
- Camera cần HTTPS hoặc `localhost`.
- Bản đồ dùng tile OpenStreetMap miễn phí, chỉ phù hợp demo. Khi chạy thật hãy dùng nhà cung cấp tile có hợp đồng.
- Tên bãi, tọa độ, giá và doanh thu là số liệu mẫu.
