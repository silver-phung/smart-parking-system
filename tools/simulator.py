#!/usr/bin/env python3
"""
Smart Parking System - IoT Hardware Simulator CLI (Sprint 2 - Thức)
Tuân thủ yêu cầu: US-FR-IOT-001 & FR-IOT-007
Giả lập luồng sự kiện cảm biến xe vào/ra cho bãi đỗ xe thông minh.
"""

import sys
import time
import json
import random
from datetime import datetime
import urllib.request
import urllib.error

DEFAULT_API_URL = "http://localhost:5174"

SAMPLE_PLATES = [
    "51B1-12345", "51F-67890", "30A-11111", "92C-22222", "43B-33333",
    "51G-44444", "29H-55555", "36K-66666", "51D-77777", "60A-88888",
    "72E-99999", "51B2-00001", "37A-10203", "48C-30405", "59D-50607"
]

def make_request(url, method="GET", data=None):
    headers = {"Content-Type": "application/json"}
    req_data = json.dumps(data).encode("utf-8") if data else None
    req = urllib.request.Request(url, data=req_data, headers=headers, method=method)
    try:
        with urllib.request.urlopen(req, timeout=5) as resp:
            body = resp.read().decode("utf-8")
            return resp.status, json.loads(body) if body else {}
    except urllib.error.HTTPError as e:
        body = e.read().decode("utf-8")
        try:
            return e.code, json.loads(body)
        except:
            return e.code, {"error": body}
    except Exception as e:
        return 0, {"error": str(e)}

def fetch_lots(base_url):
    status, data = make_request(f"{base_url}/parking-lots")
    if status == 200:
        return data
    return None

def fetch_parked_cars(base_url):
    status, data = make_request(f"{base_url}/api/iot/parked-cars")
    if status == 200 and isinstance(data, list):
        return data
    return []

def get_available_plate(parked_cars):
    parked_ids = {c.get("carId", "").upper() for c in parked_cars}
    available = [p for p in SAMPLE_PLATES if p.upper() not in parked_ids]
    if available:
        return random.choice(available)
    return f"51K-{random.randint(10000, 99999)}"

def send_event(base_url, lot_id, event_type, car_id=None, slot_num=None):
    if not slot_num:
        slot_num = f"SLOT-{random.randint(1, 20):02d}"

    parked_cars = fetch_parked_cars(base_url)

    if not car_id:
        if event_type in ("CarParked", "CheckIn"):
            car_id = get_available_plate(parked_cars)
        elif event_type in ("CarDeparted", "CheckOut"):
            cars_in_lot = [c for c in parked_cars if c.get("parkingLotId") == lot_id]
            if cars_in_lot:
                car_id = cars_in_lot[0].get("carId")
            else:
                print(f"[{datetime.now().strftime('%H:%M:%S')}] ⚠️ Bãi #{lot_id} hiện chưa có xe nào đang đỗ để check-out.")
                return False
        else:
            car_id = f"SENSOR-SYS-{lot_id}"

    payload = {
        "carId": car_id,
        "parkingLotId": lot_id,
        "slotNumber": slot_num,
        "eventType": event_type,
        "timestamp": datetime.utcnow().isoformat() + "Z"
    }

    status, resp = make_request(f"{base_url}/api/iot/events", method="POST", data=payload)
    now = datetime.now().strftime("%H:%M:%S")

    tag = "🚗 [XE VÀO]" if event_type in ("CarParked", "CheckIn") else "🚙 [XE RA ]" if event_type in ("CarDeparted", "CheckOut") else "⚠️ [LỖI   ]"
    if status == 200:
        print(f"[{now}] {tag} {resp.get('message', 'Thành công')} (Biển số: {car_id})")
    else:
        print(f"[{now}] ❌ HTTP {status}: {resp.get('message', resp.get('error', 'Lỗi không xác định'))}")
    return status == 200

def run_auto_simulation(base_url, interval=3):
    print(f"\n⚡ Đang chạy chế độ AUTO TRAFFIC SIMULATION (Mỗi {interval}s 1 sự kiện)...")
    print("Nhấn Ctrl+C để dừng lại.\n")
    try:
        while True:
            lots = fetch_lots(base_url)
            parked = fetch_parked_cars(base_url)
            if not lots:
                print(f"[{datetime.now().strftime('%H:%M:%S')}] ⚠️ Không thể kết nối Backend tại {base_url}. Đang thử lại...")
                time.sleep(interval)
                continue

            # Quyết định hành động thông minh:
            if parked and random.random() < 0.45:
                random_car = random.choice(parked)
                send_event(base_url, random_car["parkingLotId"], "CarDeparted", car_id=random_car["carId"])
            else:
                available_lots = [l for l in lots if l.get("availableSlots", 0) > 0]
                if available_lots:
                    lot = random.choice(available_lots)
                    send_event(base_url, lot["id"], "CarParked")

            time.sleep(interval)
    except KeyboardInterrupt:
        print("\n🛑 Đã dừng mô phỏng tự động.\n")

def reset_lots(base_url):
    status, resp = make_request(f"{base_url}/api/iot/reset", method="POST")
    if status == 200:
        print("✅ Đã reset dữ liệu 3 bãi xe và danh sách xe về mặc định thành công!")
    else:
        print(f"❌ Lỗi reset: {resp}")

def print_lots_status(base_url):
    lots = fetch_lots(base_url)
    parked = fetch_parked_cars(base_url)
    if not lots:
        print(f"⚠️ Không thể kết nối tới {base_url}/parking-lots")
        return

    print("\n" + "="*65)
    print("  TRẠNG THÁI HIỆN TẠI CÁC BÃI XE (LIVE TỪ API)")
    print("="*65)
    for lot in lots:
        slots = lot.get("availableSlots", 0)
        status_text = f"{slots:2d} chỗ trống" if slots > 0 else "HẾT CHỖ (FULL)"
        print(f"  [ID {lot['id']}] {lot['name']:<30} : {status_text} | {lot.get('hourlyRate', 0):,} đ/h")
    print("-" * 65)
    if parked:
        cars_str = ", ".join(f"{c['carId']} (Bãi #{c['parkingLotId']})" for c in parked[:5])
        print(f"  Xe đang đỗ ({len(parked)} xe): {cars_str}{'...' if len(parked) > 5 else ''}")
    else:
        print("  Xe đang đỗ: Chưa có xe nào trong hệ thống.")
    print("="*65 + "\n")

def main():
    base_url = sys.argv[1] if len(sys.argv) > 1 else DEFAULT_API_URL
    print("\n" + "#"*65)
    print("  SMART PARKING - HARDWARE IOT SENSOR SIMULATOR (CLI)")
    print("  Sprint 2 - Phụ trách: Thức • Tuân thủ US-FR-IOT-001")
    print("#"*65)
    print(f"  API Endpoint: {base_url}\n")

    while True:
        print_lots_status(base_url)
        print("CHỌN HÀNH ĐỘNG:")
        print("  1. 🚗 Xe vào (CarParked) bãi FPT Software Quận 9 (ID 1)")
        print("  2. 🚙 Xe ra  (CarDeparted) bãi FPT Software Quận 9 (ID 1)")
        print("  3. 🚗 Xe vào (CarParked) bãi Vincom Landmark 81 (ID 2)")
        print("  4. 🚙 Xe ra  (CarDeparted) bãi Vincom Landmark 81 (ID 2)")
        print("  5. 🚗 Xe vào (CarParked) bãi Aeon Mall Tân Phú (ID 3)")
        print("  6. 🚙 Xe ra  (CarDeparted) bãi Aeon Mall Tân Phú (ID 3)")
        print("  7. ⚡ Bật chế độ tự động mô phỏng (Auto Traffic Simulation)")
        print("  8. 🔄 Reset về dữ liệu gốc (24, 0, 8)")
        print("  0. Thoát")

        choice = input("\nNhập lựa chọn [0-8]: ").strip()
        if choice == "0":
            print("Tạm biệt!")
            break
        elif choice == "1": send_event(base_url, 1, "CarParked")
        elif choice == "2": send_event(base_url, 1, "CarDeparted")
        elif choice == "3": send_event(base_url, 2, "CarParked")
        elif choice == "4": send_event(base_url, 2, "CarDeparted")
        elif choice == "5": send_event(base_url, 3, "CarParked")
        elif choice == "6": send_event(base_url, 3, "CarDeparted")
        elif choice == "7":
            run_auto_simulation(base_url, interval=3)
        elif choice == "8":
            reset_lots(base_url)
        else:
            print("Lựa chọn không hợp lệ, vui lòng chọn lại.")

        time.sleep(0.5)

if __name__ == "__main__":
    main()
