# Hướng Dẫn Tích Hợp Component Parking3DViewer (Sprint 3: Ngày 6–9)

**Trạng thái:** Hướng dẫn triển khai lịch sử của module 3D tại nhánh feature; không phải nguồn quy tắc nghiệp vụ.
**Ưu tiên:** Dùng [business rules](../product/business-rules.md), [state machines](../product/state-machines.md) và [ADR](../decisions/README.md) khi tài liệu này còn mô tả `RESERVED` như physical status, dữ liệu mẫu hoặc biển số trong projection public.

Tài liệu bàn giao cho **Song (Frontend React)** và các thành viên nhóm phát triển Smart Parking System.

---

## 1. Giới thiệu tổng quan

Component `Parking3DViewer` là giải pháp trực quan hóa không gian bãi đỗ xe 3D tương tác, được xây dựng trên nền tảng **Three.js** và **OrbitControls**, đáp ứng đầy đủ yêu cầu nghiệp vụ Sprint 3 (Ngày 6–9):

- **Scene 3D hoàn chỉnh:**
  - **Mô phỏng kiến trúc hầm đỗ xe ngầm (Underground Parking Lot - Sketchfab):**
    - Tường bê tông bao quanh 4 phía với dải phân cách sơn phản quang an toàn.
    - Hệ thống kỹ thuật trần hầm xe ngầm: Dầm bê tông ngang chịu lực, hệ thống đường ống thông gió HVAC mạ kẽm, mạng lưới đường ống PCCC màu đỏ rực, máng đèn tuýp LED trần công nghiệp.
    - Cột kết cấu chịu lực phân khu (A1, A2, A3, B1, B2, B3) có dải phản quang vàng đen và nhãn 3D.
    - Thanh chặn bánh xe cao su (Tire Curbs / Rubber Wheel Stoppers) tại mỗi ô đỗ xe.
    - Gờ giảm tốc (Speed Bumps) và biển báo hầm xe ngầm (2.2M, 5 KM/H, EXIT).
- **Hỗ trợ 3 Bãi mẫu:**
  - **Bãi 1:** Mô hình hầm ngầm 2 tầng (`B1`, `B2`) - Tổng số lượng slot theo DB (B1: 32 ô, B2: 32 ô = 64 ô).
  - **Bãi 2:** 3 tầng (`B1`, `B2`, `B3`) - Số lượng slot đậu random (82 ô).
  - **Bãi 3:** 4 tầng (`B1`, `B2`, `B3`, `B4`) - Số lượng slot đậu random (112 ô).
- **Khối Cầu thang bộ & Buồng Thang máy (Elevator & Staircase Core):**
  - Buồng thang máy kính cường lực & khung thép, cửa trượt inox, đèn LED hiển thị tầng hiện tại.
  - Cầu thang bộ bê tông có tay vịn inox và cửa thoát hiểm chống cháy.
  - **Nút di chuyển lên xuống các tầng tại điểm cầu thang / thang máy:**
    - Nút 3D tương tác trực tiếp tại buồng thang máy (`▲ Lên`, `▼ Xuống`).
    - Bảng điều khiển nổi gắn neo 3D (Floating Elevator Call Widget) ngay tại thang máy.
    - Hiệu ứng camera chuyển tầng mượt mà (Elevator ride animation) khi người dùng bấm nút lên/xuống.
- **Điều khiển tương tác (OrbitControls):**
  - **PC:** Chuột trái để xoay (rotate), cuộn chuột để phóng to/thu nhỏ (zoom), chuột phải để dịch chuyển mặt sàn (pan).
  - **Mobile:** Cảm ứng 1 ngón tay xoay góc nhìn, 2 ngón tay chụm/mở để zoom (pinch-to-zoom), 2 ngón tay vuốt để dời sàn.
  - **Nút "Reset View":** Đưa camera trở lại góc nhìn isometric toàn cảnh ban đầu bằng animation chuyển cảnh mượt mà.
  - **Góc nhìn 2D/3D:** Chuyển đổi nhanh góc nhìn từ trên thẳng xuống (Top-down) và 3D.
- **4 trạng thái vật lý chuẩn + Trạng thái nghiệp vụ đặc biệt:**
  - 🟢 **Trống (Available / EMPTY):** Màu xanh ngọc lục bảo (`#10B981`), viền sáng nhẹ.
  - 🔴 **Có xe đã đỗ (Occupied / OCCUPIED):** Màu đỏ (`#EF4444`) – **Render mesh xe ô tô 3D** với đầy đủ khung thân, cabin kính trong suốt, 4 bánh xe, đèn LED trước/sau và biển số xe.
  - 🟡 **Bảo trì (Maintenance / MAINTENANCE):** Màu vàng hổ phách (`#F59E0B`) – **Render mesh cọc tiêu giao thông 3D** (cam sọc trắng).
  - ⚪ **Không khả dụng (Unavailable / UNAVAILABLE):** Màu xám (`#6B7280`) – Ký hiệu gạch chéo cấm đỗ.
  - ⏳ **Ô được phân nhưng xe chưa vào (RESERVED / DISPATCHED):** **Dấu hiệu riêng biệt: Nhấp nháy viền (Pulsing highlight glow animation)** liên tục trong render loop + Floating beacon hiển thị biển số xe đang đến.

---

## 2. Cấu trúc tệp

```
frontend/
├── assets/
│   ├── css/
│   │   └── parking-3d-viewer.css    # Stylesheet cho HUD, tooltip, stats và responsive mobile
│   └── js/
│       └── parking-3d-viewer.js     # Core engine Three.js (UMD / Vanilla JS)
├── components/
│   └── Parking3DViewer.jsx          # React Component wrapper dành cho Song
└── 3d-viewer.html                   # Trang demo tương tác toàn màn hình
```

---

## 3. Cách tích hợp vào React (Dành cho Song - `Song-frontend`)

### Cách 1: Sử dụng React Component wrapper

1. Copy `frontend/components/Parking3DViewer.jsx` vào thư mục dự án React của bạn (`src/components/Parking3DViewer.jsx`).
2. Đảm bảo file css `parking-3d-viewer.css` và core `parking-3d-viewer.js` được import đúng đường dẫn.
3. Nhúng component vào trang JSX bất kỳ:

```jsx
import React, { useState } from 'react';
import Parking3DViewer from './components/Parking3DViewer';

export default function ParkingDashboard() {
  const [selectedSlot, setSelectedSlot] = useState(null);
  const [currentFloor, setCurrentFloor] = useState('B1');

  return (
    <div className="dashboard-container">
      <h2>Sơ đồ không gian bãi đỗ xe 3D</h2>

      {/* Bộ điều khiển chọn tầng */}
      <div className="floor-tabs">
        <button onClick={() => setCurrentFloor('B1')}>Hầm B1</button>
        <button onClick={() => setCurrentFloor('B2')}>Hầm B2</button>
        <button onClick={() => setCurrentFloor('G')}>Tầng Trệt</button>
      </div>

      {/* 3D Viewer */}
      <Parking3DViewer
        lotId={1}
        lotName="Bãi xe FPT Software Q9"
        floorId={currentFloor}
        apiEndpoint="/api/ParkingSlots"
        height="600px"
        onSlotClick={(slotData) => {
          console.log('Người dùng đã chọn ô:', slotData);
          setSelectedSlot(slotData);
        }}
      />

      {/* Hiển thị chi tiết ô được chọn */}
      {selectedSlot && (
        <div className="slot-info-box">
          <h3>Ô: {selectedSlot.slotCode}</h3>
          <p>Trạng thái: {selectedSlot.physicalStatus}</p>
          <p>Biển số: {selectedSlot.currentCarPlate || 'Chưa có'}</p>
        </div>
      )}
    </div>
  );
}
```

### Props của Component React

| Prop | Kiểu | Mặc định | Mô tả |
| :--- | :--- | :--- | :--- |
| `lotId` | `number` | `1` | ID của bãi xe |
| `lotName` | `string` | `'Bãi xe SmartPark'` | Tên bãi hiển thị trên thanh tiêu đề HUD |
| `floorId` | `string` | `'B1'` | Mã tầng ('B1', 'B2', 'G', 'T1') |
| `apiEndpoint` | `string` | `'/api/ParkingSlots'` | URL API lấy danh sách ô đỗ (hoặc `'/api/v1/parking-slots'`) |
| `height` | `string` | `'560px'` | Chiều cao vùng hiển thị 3D |
| `onSlotClick` | `function` | `null` | Callback khi người dùng nhấp vào một ô: `(slotData) => void` |

---

## 4. Cách tích hợp vào HTML thuần (Vanilla JS)

Thêm các script và stylesheet vào tệp HTML:

```html
<!-- Stylesheet -->
<link rel="stylesheet" href="assets/css/parking-3d-viewer.css">

<!-- Three.js & OrbitControls -->
<script src="https://cdnjs.cloudflare.com/ajax/libs/three.js/r128/three.min.js"></script>
<script src="https://cdn.jsdelivr.net/npm/three@0.128.0/examples/js/controls/OrbitControls.js"></script>

<!-- Component Script -->
<script src="assets/js/parking-3d-viewer.js"></script>

<!-- Vùng hiển thị -->
<div id="myParking3D" style="width: 100%; height: 550px;"></div>

<script>
  var viewer = new Parking3DViewer({
    container: '#myParking3D',
    lotId: 1,
    floorId: 'B1',
    apiEndpoint: '/api/ParkingSlots',
    onSlotClick: function(slot) {
      alert('Đã chọn ô ' + slot.slotCode + ' (Trạng thái: ' + slot.physicalStatus + ')');
    }
  });

  // Cập nhật trạng thái thời gian thực từ WebSocket hoặc Event:
  // viewer.updateSlotStatus('A1-012', 'OCCUPIED', { carPlate: '51F-123.45' });
  // viewer.resetView();
</script>
```

---

## 5. Các phương thức công khai (Public Methods) của `Parking3DViewer`

- `viewer.loadSlots(floorId)`: Tải lại dữ liệu các ô đỗ từ API.
- `viewer.setFloor(floorId)`: Chuyển đổi giữa các tầng bãi đỗ.
- `viewer.resetView()`: Đưa camera về góc nhìn toàn cảnh ban đầu.
- `viewer.toggleTopDownView()`: Chuyển đổi giữa góc nhìn 2D từ trên xuống và 3D.
- `viewer.filterStatus(status)`: Lọc ô hiển thị (`'ALL'`, `'EMPTY'`, `'OCCUPIED'`, `'RESERVED'`, `'MAINTENANCE'`).
- `viewer.updateSlotStatus(slotCode, newStatus, details)`: Cập nhật real-time trạng thái của một ô và render lại 3D ngay lập tức.
- `viewer.destroy()`: Hủy giải phóng WebGL contexts và sự kiện, chống rò rỉ bộ nhớ.

---

## 6. Hợp đồng API Backend (`/api/ParkingSlots`)

Endpoint hỗ trợ:
- `GET /api/ParkingSlots?lotId=1&floorId=B1`
- `GET /api/ParkingSlots/floor/{floorId}`
- `PATCH /api/ParkingSlots/{slotCode}/status`

Cấu trúc JSON trả về mẫu:
```json
{
  "success": true,
  "lotId": 1,
  "floorId": "B1",
  "total": 20,
  "availableCount": 14,
  "occupiedCount": 4,
  "reservedCount": 1,
  "maintenanceCount": 1,
  "slots": [
    {
      "id": 1,
      "slotCode": "B1-A01",
      "physicalStatus": "EMPTY",
      "vehicleType": "car",
      "positionX": -16.4,
      "positionY": 0,
      "positionZ": -8.0,
      "rotationY": 0
    },
    {
      "id": 1012,
      "slotCode": "A1-012",
      "physicalStatus": "OCCUPIED",
      "vehicleType": "car",
      "currentCarPlate": "51F-123.45",
      "vehicleModel": "Toyota Vios",
      "vehicleColor": "white",
      "positionX": 23.2,
      "positionY": 0,
      "positionZ": -8.0
    }
  ]
}
```
*(Nếu backend offline, component tự động chuyển sang bộ dữ liệu mẫu mà không làm gián đoạn giao diện).*
