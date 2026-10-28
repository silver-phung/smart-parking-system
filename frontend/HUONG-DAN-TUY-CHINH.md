# Hướng dẫn tùy chỉnh: tài khoản, animation, ảnh nền

Mẹo tìm nhanh trong VS Code: bấm `Ctrl + Shift + F` rồi gõ
- `[ANIM]` để thấy mọi chỗ có hiệu ứng chuyển động,
- `[HINH-NEN]` để thấy mọi chỗ có thể gắn ảnh.

Mọi hiệu ứng tự tắt khi người dùng bật "giảm chuyển động" trong hệ điều hành (cuối `assets/css/base.css`).

---

## 1. Đăng nhập và hai tài khoản

Mở dự án sẽ vào trang đăng nhập (`index.html` ở thư mục gốc). Chọn đúng cổng rồi đăng nhập:

| Cổng | Email | Mật khẩu | Vào trang |
| --- | --- | --- | --- |
| Khách đỗ xe | `khach@smartpark.vn` | `khach123` | `client/index.html` |
| Ban quản lý | `admin@smartpark.vn` | `admin123` | `admin/index.html` |

- Chưa đăng nhập mà mở thẳng `client/` hoặc `admin/` sẽ bị đưa về trang đăng nhập.
- Khách mở trang `admin/` sẽ bị chuyển về `client/`, và ngược lại.
- Phiên đăng nhập lưu theo từng tab (sessionStorage): mở hai tab, một tab đăng nhập Khách và một tab đăng nhập Ban quản lý để xem đồng bộ cùng lúc. Đóng tab là phải đăng nhập lại.
- Nút đăng xuất: góc phải thanh trên của Client, góc dưới thanh bên của Admin (trên điện thoại ở thanh trên).
- Tài khoản demo được khai báo trong `SP.DEMO_ACCOUNTS` tại `assets/js/core.js`. Trang đăng nhập chỉ sử dụng tài khoản đã có; chức năng tự đăng ký đã được gỡ.

**Quan trọng:** đăng nhập ở bản HTML này chạy trong trình duyệt; mật khẩu demo nằm trong mã nguồn. Chỉ dùng để demo, không dùng với dữ liệu thật. Khi nối backend, chuyển `SP.auth.login` sang API và kiểm tra quyền tại máy chủ.

---

## 2. Những chỗ có thể thêm animation

Cột "Hiện trạng" cho biết chỗ đó đã có hiệu ứng hay mới chỉ là gợi ý.

### Trang đăng nhập (`assets/css/login.css`)

| Vị trí | Selector | Hiện trạng | Cách chỉnh |
| --- | --- | --- | --- |
| Ảnh nền chuyển động chậm | `.bg-img` | Đã có (phóng to nhẹ, 28 giây) | Đổi `28s`, hoặc sửa `scale(1.07)` trong `@keyframes drift`. Xóa dòng `animation` để nền đứng yên |
| Chiếc xe chạy ở đáy màn hình | `.bg-car`, `.bg-lane` | Đã có (22 giây một vòng) | Đổi `22s` để nhanh hoặc chậm. Muốn bỏ thì xóa khối `.bg-lane` trong `index.html` |
| Tiêu đề, mô tả, thẻ đăng nhập trượt lên khi mở trang | `.a1`, `.a2`, `.a3` | Đã có | Đổi độ trễ `.05s / .2s / .35s` để đổi nhịp |
| Thẻ đăng nhập rung khi nhập sai | `.login.shake` | Đã có | Sửa `@keyframes shake` |
| Chuyển qua lại giữa hai cổng | `.portal button` | Đã có (đổi màu mượt) | Có thể thêm hiệu ứng trượt cho nút đang chọn |

### Web Client (`client/client.css`, `client/client.js`)

| Vị trí | Selector | Hiện trạng | Cách chỉnh |
| --- | --- | --- | --- |
| Đổi tab (Tìm bãi, Vé, Quét QR, Tài khoản) | `.view.in` | Đã có (mờ dần, trượt lên) | Hàm `replay()` trong `client.js` thêm class `in` |
| Cửa sổ đặt chỗ mở ra | `.sheet.in` | Đã có (hiện dần ở máy tính, trượt từ dưới lên ở điện thoại) | Sửa `pop-in` và `slide-up` trong `base.css` |
| Thẻ bãi xe nhấc lên khi rê chuột | `.lotcard:hover` | Đã có | Đổi `translateY(-2px)` |
| Chọn ô chỗ đỗ | `.slot.sel`, `button.slot.free:hover` | Đã có (nảy nhẹ) | Trong `base.css` |
| Thông báo nổi ở cuối màn hình | `.toast` | Đã có | `@keyframes toast-in` trong `base.css` |
| Biểu tượng bãi trên bản đồ | `.pin span` | Gợi ý | Thêm hiệu ứng nhấp nháy cho bãi sắp hết chỗ. Lưu ý Leaflet tạo lại biểu tượng mỗi khi số chỗ đổi |
| Số chỗ trống thay đổi | `.free-n` | Gợi ý | Thêm hiệu ứng đếm số hoặc đổi màu thoáng qua khi số thay đổi |
| Bước 4: vé QR sau khi thanh toán | `.success .tick`, `.ticket` | Gợi ý | Dấu tích vẽ nét dần, vé lật hoặc trượt vào |
| Khung quét camera | `.cam .corner` | Gợi ý | Thêm đường quét chạy dọc khung |

### Web Admin (`admin/admin.css`, `admin/admin.js`)

| Vị trí | Selector | Hiện trạng | Cách chỉnh |
| --- | --- | --- | --- |
| Đổi mục ở thanh bên | `.page.enter` | Đã có | Class `enter` được thêm trong hàm `go()` của `admin.js` |
| Ô Heatmap phóng to khi rê chuột | `.hc:hover` | Đã có | Đổi `scale(1.35)` |
| Thẻ KPI (chỗ trống, doanh thu) | `.kpi`, `.k-v` | Gợi ý | Đếm số từ 0 lên giá trị khi vào trang |
| Cảnh báo mới xuất hiện | `.alert` | Gợi ý | Viền nhấp nháy với cảnh báo mức Cao còn "Mới" |
| Chấm "Trực tiếp" | `.dot.live` | Đã có (nhịp đập) | `@keyframes pulse` trong `base.css` |
| Ô chỗ đỗ đổi trạng thái | `.slot` | Gợi ý | Hiệu ứng nháy màu khi ô đổi trạng thái |

**Lưu ý quan trọng khi thêm animation "xuất hiện" ở Admin và danh sách ở Client:** các vùng này được vẽ lại toàn bộ mỗi 3 giây (Admin) hoặc mỗi khi dữ liệu đổi (Client). Nếu gắn hiệu ứng "trượt lên khi xuất hiện" vào các phần tử đó, hiệu ứng sẽ chạy lại liên tục. Hãy gắn hiệu ứng vào phần tử bao ngoài và chỉ bật khi chuyển trang, như cách `go()` và `replay()` đang làm. Hiệu ứng khi rê chuột (`:hover`, `transition`) thì dùng thoải mái.

### Cách thêm một hiệu ứng mới

1. Viết `@keyframes` trong `assets/css/base.css`, ví dụ:
   ```css
   @keyframes blink { 50% { box-shadow: 0 0 0 4px rgba(210, 63, 63, .35); } }
   ```
2. Gắn vào selector cần dùng:
   ```css
   .alert.high { animation: blink 1.6s ease-in-out infinite; }
   ```
3. Hoặc dùng sẵn hai class dùng chung: `anim-fade-up` (trượt lên) và `anim-pop` (phóng nhẹ), thêm thẳng vào thẻ HTML.

---

## 3. Những chỗ có thể thêm ảnh nền và ảnh minh họa

Tất cả ảnh để trong `assets/img/` (xem thêm `assets/img/README.txt`). **Chưa có ảnh thì giao diện tự dùng nền màu, không bị lỗi.** Sau khi thả ảnh, tải lại trang bằng `Ctrl + F5`.

| Vị trí trên giao diện | Tên tệp ảnh | Kích thước gợi ý | Chỉnh ở đâu |
| --- | --- | --- | --- |
| Nền toàn màn hình trang đăng nhập | `assets/img/login-bg.png` | Ảnh ngang; tự co phủ màn hình | `assets/css/login.css`, mục `.bg-img` |
| Độ tối của lớp phủ trên ảnh đăng nhập | không cần ảnh | Số `.78 / .45 / .25` | `assets/css/login.css`, mục `.bg-shade` |
| Nền các trang Vé / Quét QR / Tài khoản (Client) | `assets/img/client-bg.jpg` | 1600x900 trở lên, ảnh nhạt | `client/client.css`, mục `.page` |
| Nền vùng nội dung Web Admin | `assets/img/admin-bg.jpg` | 1600x900 trở lên, ảnh nhạt | `admin/admin.css`, mục `.page` |
| Ảnh bìa lớn khi mở chi tiết một bãi | `assets/img/lots/L1.jpg` ... `L4.jpg` | 1200x400 (ngang) | `client/client.js`, hàm `detailHtml`; kiểu ở `.cover` trong `client.css` |
| Ảnh nhỏ cạnh tên bãi ở danh sách | cùng tệp `lots/L1.jpg` ... | dùng chung ảnh bìa | `client/client.js`, hàm `renderPanel`; kiểu ở `.thumb` |
| Màu nền khi chưa có ảnh bãi | không cần ảnh | hai màu gradient | `--cover-fallback` trong `assets/css/base.css` |
| Logo | thẻ `<img>` thay cho chữ "P" | 64x64, PNG hoặc SVG | phần tử `.logo` trong `index.html`, `client/index.html`, `admin/index.html` |

### Thêm ảnh cho bãi mới hoặc đổi tên tệp

Ảnh bãi lấy theo mã bãi trong `SP.LOTS` (`core.js`). Bãi mới có mã `L5` thì đặt ảnh `assets/img/lots/L5.jpg`. Muốn đổi đuôi hoặc tên, sửa đường dẫn trong hai hàm `detailHtml` và `renderPanel` ở `client/client.js`.

### Chỉnh độ rõ của ảnh nền

- Trang đăng nhập: chữ trắng nằm trên ảnh nên cần lớp tối phủ. Ảnh sáng thì tăng số đầu trong `.bg-shade`, ảnh tối thì giảm.
- Client và Admin: ảnh được phủ một lớp màu nền (90% và 93%) để bảng, biểu đồ vẫn dễ đọc. Muốn ảnh hiện rõ hơn, giảm `90%` hoặc `93%` xuống `70%`. Đừng giảm quá thấp ở Admin vì biểu đồ sẽ khó đọc.
- Nên dùng ảnh JPG hoặc WebP, nén dưới 500KB để trang mở nhanh.

### Muốn nền chuyển động bằng video thay cho ảnh

Trong `index.html`, thay `<div class="bg-img"></div>` bằng thẻ `<video class="bg-img" src="assets/img/login-bg.mp4" autoplay muted loop playsinline></video>` và thêm `object-fit: cover` cho `.bg-img` trong `login.css`. Dùng video ngắn, dưới 5MB.
