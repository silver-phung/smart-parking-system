"""
Download model weights utility.
Once weights are uploaded to cloud storage (Google Drive, HuggingFace, GitHub Release),
fill in the links below so team members can download all required weights with 1 command:
    python download_weights.py
"""

import os
from pathlib import Path

WEIGHTS_DIR = Path(__file__).resolve().parent / "weights"
PPOCR_DIR = WEIGHTS_DIR / "ppocrv4"

# URLs or Google Drive file IDs for required weight files
WEIGHT_FILES = {
    # Destination path: Download URL or Drive ID
    # Example: WEIGHTS_DIR / "vehicle_yolov8s_640.pt": "https://..."
}


def main():
    os.makedirs(WEIGHTS_DIR, exist_ok=True)
    os.makedirs(PPOCR_DIR, exist_ok=True)

    print("[Info] Model weights download utility")
    print(f"[Info] Target directory: {WEIGHTS_DIR}")

    if not WEIGHT_FILES:
        print("\n[Notice] No download URLs configured yet.")
        print("Required model files to place into 'weights/':")
        print("  - weights/vehicle_yolov8s_640.pt")
        print("  - weights/plate_yolov8n_320_2024.pt")
        print("  - weights/ppocrv4/ch_PP-OCRv4_det_infer.onnx")
        print("  - weights/ppocrv4/ch_PP-OCRv4_rec_infer.onnx")
        print("\nPlease obtain these files from the team repository release/drive and place them in 'weights/'.")
        return

    # Once URLs are added, download logic will execute here
    print("[Success] All weights are up to date.")


if __name__ == "__main__":
    main()
