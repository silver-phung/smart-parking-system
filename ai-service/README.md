# Smart Parking - AI Service (License Plate & Vehicle Recognition)

Dịch vụ AI nhận diện biển số xe và phân loại phương tiện dành cho Hệ thống Quản lý Bãi đỗ xe thông minh (**Smart Parking System**).

Dịch vụ này được đóng gói độc lập theo kiến trúc Microservice, cung cấp REST API chuẩn để Backend **ASP.NET Core** (hoặc Web/Mobile/Edge Client) gọi tới khi xe vào/ra cổng bãi đỗ xe.

---

## 1. Công nghệ sử dụng
- **Phát hiện phương tiện (Vehicle Detection):** YOLOv8 (`weights/vehicle_yolov8s_640.pt`)
- **Phát hiện biển số (Plate Detection):** YOLOv8 (`weights/plate_yolov8n_320_2024.pt`)
- **Nhận diện ký tự (OCR):** Baidu PaddleOCR v4 ONNX Pipeline (`weights/ppocrv4/*.onnx`)
- **Theo dõi đối tượng (Tracking - tùy chọn):** DeepSORT / SORT
- **API Framework:** FastAPI + Uvicorn + Pydantic v2

---

## 2. Cấu trúc thư mục

```text
ai-service/
├── app/
│   ├── __init__.py
│   └── main.py              # FastAPI Web Application & API Endpoints
├── detectors/               # Module YOLOv8 detector
├── ppocr_onnx/              # Module ONNX Text Detection & Text Recognition
├── tracking/                # Module DeepSORT & SORT tracker
├── utils/                   # Xử lý ảnh, chuẩn hóa & sửa lỗi biển số
├── weights/                 # Nơi chứa các file model weights (bị .gitignore)
│   ├── ppocrv4/
│   │   ├── ch_PP-OCRv4_det_infer.onnx
│   │   └── ch_PP-OCRv4_rec_infer.onnx
│   ├── plate_yolov8n_320_2024.pt
│   ├── vehicle_yolov8s_640.pt
│   └── reid_model.onnx
├── .gitignore               # Loại bỏ cache, environments và các file weights nặng
├── requirements.txt         # Danh sách thư viện Python
└── README.md                # Tài liệu hướng dẫn sử dụng
```

---

## 3. Cài đặt môi trường

### Yêu cầu tiên quyết:
- **Python:** 3.8 <= Python <= 3.10
- Khuyến nghị sử dụng GPU NVIDIA (CUDA 11.8) nếu có để tăng tốc độ inference.

### Các bước cài đặt:

1. **Di chuyển vào thư mục `ai-service`:**
   ```bash
   cd ai-service
   ```

2. **Tạo và kích hoạt môi trường ảo (Virtual Environment):**
   - Trên Windows (PowerShell):
     ```powershell
     python -m venv .venv
     .\.venv\Scripts\Activate.ps1
     ```
   - Trên Linux/macOS:
     ```bash
     python3 -m venv .venv
     source .venv/bin/activate
     ```

3. **Cài đặt thư viện:**
   ```bash
   pip install --upgrade pip
   pip install -r requirements.txt
   ```

---

## 4. Model Weights (Lưu ý quan trọng)

Các file model weights (*.pt, *.onnx) có kích thước lớn (> 140MB tổng cộng) nên **đã được cấu hình trong `.gitignore`** để tránh làm nặng repository Git chung.

Nếu bạn clone repo mới về máy và thư mục `weights/` còn trống, hãy tải weights từ link lưu trữ nội bộ của team (Drive / Release) và đặt vào đúng vị trí:
- `weights/vehicle_yolov8s_640.pt`
- `weights/plate_yolov8n_320_2024.pt`
- `weights/ppocrv4/ch_PP-OCRv4_det_infer.onnx`
- `weights/ppocrv4/ch_PP-OCRv4_rec_infer.onnx`

---

## 5. Khởi động API Server

Bạn có thể khởi động server nhanh bằng 1 trong các cách sau tại thư mục `ai-service`:

- **Cách 1 (Khuyên dùng - Rút gọn):**
  ```bash
  python run.py
  ```
- **Cách 2 (Click đúp chuột trên Windows):**
  Chạy file `run.bat` (hoặc gõ `.\run.bat` trong terminal).

- **Cách 3 (Lệnh uvicorn truyền thống):**
  ```bash
  python -m uvicorn app.main:app --host 0.0.0.0 --port 8000 --reload
  ```

- **Swagger UI (Interactive API Docs):** [http://localhost:8000/docs](http://localhost:8000/docs)
- **ReDoc:** [http://localhost:8000/redoc](http://localhost:8000/redoc)

---

## 6. Tài liệu API Endpoint

### `POST /api/v1/detect`
Nhận file ảnh chụp từ camera cổng bãi đỗ xe và trả về thông tin biển số, loại xe.

- **Content-Type:** `multipart/form-data`
- **Request Parameters:**
  - `file` (bắt buộc): File ảnh (`image/jpeg`, `image/png`).
  - `vconf` (tùy chọn, mặc định 0.5): Ngưỡng tin cậy phát hiện xe.
  - `pconf` (tùy chọn, mặc định 0.25): Ngưỡng tin cậy phát hiện biển số.
  - `ocr_thresh` (tùy chọn, mặc định 0.4): Ngưỡng tin cậy đọc chữ OCR.

- **Response Mẫu (Thành công - 200 OK):**
```json
{
  "success": true,
  "message": "Nhận diện biển số thành công.",
  "data": {
    "plate_number": "51F12345",
    "raw_plate_number": "51F-123.45",
    "confidence": 0.9412,
    "is_legit": true,
    "vehicle_type": "car",
    "vehicle_confidence": 0.912,
    "vehicle_bbox": [150, 200, 680, 710],
    "plate_bbox": [320, 510, 480, 565],
    "inference_time_ms": 118.5
  }
}
```

- **Response Mẫu (Không tìm thấy biển):**
```json
{
  "success": false,
  "message": "Không nhận diện được biển số trong ảnh. Vui lòng thử lại với góc chụp rõ hơn.",
  "data": null
}
```
