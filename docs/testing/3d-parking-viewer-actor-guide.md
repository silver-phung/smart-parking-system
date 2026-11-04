# Hướng dẫn kiểm thử Sơ đồ bãi đỗ xe 3D theo Actor

## 1. Chuẩn bị bản chạy cục bộ

Viewer nằm trong React app tại route `/app/parking-3d`.

```bash
cd frontend/smart-parking
npm install
npm run build
```

Cách nhanh nhất để kiểm tra riêng giao diện tĩnh:

```bash
cd backend/src/SmartParking.Api/wwwroot
python3 -m http.server 4174
```

Mở `http://127.0.0.1:4174/app/`, sau đó bấm **Sơ đồ 3D** trên thanh điều hướng. Với static server đơn giản, không tải lại trực tiếp URL con vì server này không có SPA fallback. Khi chạy qua ASP.NET API, có thể mở trực tiếp `/app/parking-3d`.

## 2. Actor cần kiểm tra

| Actor | Cách vào viewer | Được xem layout/trạng thái | Biển số và mẫu xe ở gần |
|---|---|---:|---:|
| `GUEST` | Không đăng nhập → **Sơ đồ 3D** | Có | Không |
| `USER` | Đăng nhập Customer → **Sơ đồ 3D** | Có | Không |
| `STAFF` | Đăng nhập Staff → **Sơ đồ 3D** | Có | Có, trong phạm vi 11 m |
| `MANAGER` | Đăng nhập Manager → **Sơ đồ 3D** | Có | Có, trong phạm vi 11 m |
| `ADMIN` | Đăng nhập Admin → **Sơ đồ 3D** | Có | Có, trong phạm vi 11 m |

Không dùng tài khoản thật hoặc dữ liệu biển số thật để kiểm thử. Dữ liệu xe trong scene là dữ liệu mô phỏng xác định trước.

## 3. Ma trận bãi và sức chứa

Mỗi tầng luôn có 100 vị trí hình học (`4 hàng × 25 vị trí`) để thể hiện layout. Số **ô theo cấu hình** mới là sức chứa nghiệp vụ:

| Bãi | Tầng | Sức chứa cần thấy |
|---|---|---:|
| Hầm ngầm FPT Q9 | B1, B2 | 32 + 32 = 64 |
| Vincom Landmark 81 | B1, B2, B3 | 28 + 27 + 27 = 82 |
| Aeon Mall Mega Hub | B1–B4 | 28 mỗi tầng = 112 |
| Vinhomes Central Park | T1, T2 | 100 mỗi tầng |
| Saigon Centre Parking | T1–T3 | 100 mỗi tầng |

Các vị trí vật lý chưa được cấu hình phải có màu xám và dấu chéo; không được tính vào số ô trống.

## 4. Kiểm thử layout và luồng xe

### Tầng B1

1. Chọn một trong ba bãi hầm ngầm và tầng `B1`.
2. Xác nhận có 4 hàng `A–D`, ba lối đi xen giữa và mã theo dạng `B1-A01` … `B1-D25`.
3. Các ô được cấu hình tại B1 phải thuộc khu **Gửi tháng**.
4. Sau dốc từ mặt đất xuống phải thấy bảng phân hướng, booth check-in thẻ tháng và barrier vào khu tháng.
5. Làn khách vãng lai phải tách khỏi khu tháng bằng tường, đi tiếp đến dốc xuống B2 mà không quẹt lại.
6. Cuối luồng tháng phải có booth check-out và dốc hai làn dùng chung để lên mặt đất.
7. Tầng cao nhất có barrier `LỐI VÀO`, `LỐI RA` và bảng `LỐI RA / EXIT`.

### Tầng B2

1. Các ô được cấu hình phải thuộc khu **Vãng lai**.
2. Phải có booth check-out khách vãng lai trước dốc lên B1.
3. Nếu bãi còn B3, dốc xuống B3 đặt ở cuối tuyến tìm chỗ.
4. Sau khi lên B1, luồng vãng lai phải tiếp tục dùng dốc chung lên mặt đất.

### Tầng B3/B4

1. Có dốc lên tầng trước ở cuối đường.
2. Nếu chưa phải tầng cuối, có dốc xuống tầng tiếp theo đặt song song ở cuối tuyến.
3. Ở tầng cuối, bảng hướng dẫn phải chỉ xe quay lại dốc lên khi hết chỗ.

### Bãi cao tầng

1. Kiểm tra cả tầng cuối của Vinhomes và Saigon Centre.
2. Mỗi tầng phải có đủ 4 hàng, 3 lối, cột, lan can hở, phòng thang máy và dốc liên tầng.
3. Trần/tầng phía trên không được che góc thao tác toàn cảnh.

## 5. Kiểm thử tương tác

1. Ở chế độ **Đi bộ**, bấm các vòng tròn xanh phía trước. Camera phải trượt tới điểm mới, không nhảy tức thời.
2. Bấm **Toàn cảnh 3D**: trần phải ẩn và nhìn được toàn bộ luồng xe.
3. Bấm **Mặt bằng**: nhìn được đủ 4 hàng và 3 lối.
4. Khi đứng xa phòng thang máy, bảng gọi tầng phải ẩn.
5. Bấm **Tìm thang máy** hoặc đi tới vòng tròn trước phòng kính; bảng `▲ Lên / ▼ Xuống` mới xuất hiện.
6. Tại tầng đầu/cuối, nút không còn tầng tương ứng phải bị vô hiệu hóa.
7. Chuyển tầng bằng bảng gọi thang máy; tên tầng, số liệu và scene phải cùng cập nhật.

## 6. Kiểm thử quyền riêng tư xe

Thực hiện cho từng Actor:

1. Ở **Toàn cảnh 3D**, không được thấy biển số gắn trên xe.
2. Chuyển sang **Đi bộ**, tới gần một xe có người đỗ.
3. Card `XE Ở GẦN` chỉ được xuất hiện trong khoảng 11 m.
4. `GUEST` và `USER` chỉ thấy mã ô, khu vực, trạng thái; không thấy biển số hoặc mẫu xe.
5. `STAFF`, `MANAGER`, `ADMIN` thấy thêm biển số và mẫu xe mô phỏng khi ở gần.
6. Đi xa xe: card và biển số gắn trên xe phải biến mất.

## 7. Kiểm tra tự động

```bash
cd frontend/smart-parking
node --test src/services/api.test.js \
  src/services/authSession.test.js \
  src/components/parking3d/garageScene.test.js
npm run build
```

Kết quả mong đợi: toàn bộ test Node pass và Vite build thành công. Đường dẫn dự án hiện chứa ký tự `#`; Vitest/jsdom có thể báo lỗi phân giải `/@vite/env` dù dependency đã cài. Đây là giới hạn đường dẫn của Vite trong workspace hiện tại, không phải lỗi scene 3D.
