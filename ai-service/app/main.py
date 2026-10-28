import os
import sys
import re
import time
import logging
from pathlib import Path
from typing import Optional, List
from contextlib import asynccontextmanager

import cv2
import numpy as np
import torch
from fastapi import FastAPI, File, UploadFile, Query, HTTPException, status
from fastapi.middleware.cors import CORSMiddleware
from pydantic import BaseModel, Field

# Setup logging
logging.basicConfig(level=logging.INFO, format="%(levelname)s:     %(message)s")
logger = logging.getLogger("ai_service")

# Add ai-service root directory to sys.path for local module resolution
CURRENT_DIR = Path(__file__).resolve().parent
SERVICE_ROOT = CURRENT_DIR.parent
if str(SERVICE_ROOT) not in sys.path:
    sys.path.insert(0, str(SERVICE_ROOT))

from ultralytics import YOLO
from ppocr_onnx import DetAndRecONNXPipeline
from utils.utils import (
    crop_expanded_plate,
    check_legit_plate,
    correct_plate,
    map_label,
    VEHICLES,
    check_image_size
)

# Default model weights paths
WEIGHTS_DIR = SERVICE_ROOT / "weights"
VEHICLE_WEIGHT = str(WEIGHTS_DIR / "vehicle_yolov8s_640.pt")
PLATE_WEIGHT = str(WEIGHTS_DIR / "plate_yolov8n_320_2024.pt")
OCR_DET_WEIGHT = str(WEIGHTS_DIR / "ppocrv4" / "ch_PP-OCRv4_det_infer.onnx")
OCR_REC_WEIGHT = str(WEIGHTS_DIR / "ppocrv4" / "ch_PP-OCRv4_rec_infer.onnx")

# Global dictionary holding active model instances
models = {}


def extract_plate_text(plate_reader: DetAndRecONNXPipeline, plate_image: np.ndarray):
    """Extract license plate characters from cropped plate image using PP-OCR ONNX."""
    results = plate_reader.detect_and_ocr(plate_image)
    if not results or len(results) == 0:
        return "", 0.0

    plate_info = ""
    conf_scores = []
    for result in results:
        plate_info += result.text + " "
        conf_scores.append(result.score)

    avg_conf = sum(conf_scores) / len(conf_scores) if conf_scores else 0.0
    cleaned_plate = re.sub(r'[^A-Za-z0-9\-.]', '', plate_info)
    return cleaned_plate, float(avg_conf)


@asynccontextmanager
async def lifespan(app: FastAPI):
    """Load neural network weights upon startup and release resources upon shutdown."""
    device = "cuda" if torch.cuda.is_available() else "cpu"

    missing_weights = []
    for path, name in [
        (VEHICLE_WEIGHT, "Vehicle YOLOv8"),
        (PLATE_WEIGHT, "Plate YOLOv8"),
        (OCR_DET_WEIGHT, "OCR Det ONNX"),
        (OCR_REC_WEIGHT, "OCR Rec ONNX"),
    ]:
        if not os.path.exists(path):
            missing_weights.append(f"{name} ({path})")

    if missing_weights:
        logger.warning("The following weight files are missing:")
        for w in missing_weights:
            logger.warning("  - %s", w)
        logger.warning("Please download weights into ai-service/weights/ directory.")
    else:
        models["device"] = device
        models["vehicle_detector"] = YOLO(VEHICLE_WEIGHT, task="detect")
        models["plate_detector"] = YOLO(PLATE_WEIGHT, task="detect").to(device=device)
        models["plate_reader"] = DetAndRecONNXPipeline(
            text_det_onnx_model=OCR_DET_WEIGHT,
            text_rec_onnx_model=OCR_REC_WEIGHT,
            box_thresh=0.4
        )
        logger.info("Inference models loaded on device: %s", device.upper())

    yield

    # Cleanup upon shutdown
    models.clear()


app = FastAPI(
    title="Smart Parking AI Service",
    description="Microservice for License Plate Recognition and Vehicle Classification",
    version="1.0.0",
    lifespan=lifespan
)

# Enable CORS for web and client integrations
app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)


# --- Pydantic Data Models ---
class PlateDetectionData(BaseModel):
    plate_number: str = Field(..., description="Normalized and corrected license plate string")
    raw_plate_number: str = Field(..., description="Raw text extracted directly from OCR")
    confidence: float = Field(..., description="Average OCR confidence score (0.0 to 1.0)")
    is_legit: bool = Field(..., description="True if format matches standard Vietnamese plate rules")
    vehicle_type: Optional[str] = Field(None, description="Detected vehicle class: car, bus, truck, motorbike")
    vehicle_confidence: Optional[float] = Field(None, description="Confidence score of vehicle detection")
    vehicle_bbox: Optional[List[int]] = Field(None, description="Vehicle bounding box coordinates [x1, y1, x2, y2]")
    plate_bbox: Optional[List[int]] = Field(None, description="License plate bounding box coordinates [x1, y1, x2, y2]")
    inference_time_ms: float = Field(..., description="Processing time in milliseconds")


class ApiResponse(BaseModel):
    success: bool
    message: str
    data: Optional[PlateDetectionData] = None


@app.post(
    "/api/v1/detect",
    response_model=ApiResponse,
    summary="Recognize license plate and vehicle from a single image",
    description="Receives an image captured from gate camera, detects vehicle, crops plate, and extracts text."
)
async def detect_license_plate(
    file: UploadFile = File(..., description="Input image file (JPEG, PNG)"),
    vconf: float = Query(0.5, ge=0.1, le=1.0, description="Vehicle detection confidence threshold"),
    pconf: float = Query(0.25, ge=0.1, le=1.0, description="Plate detection confidence threshold"),
    ocr_thresh: float = Query(0.4, ge=0.1, le=1.0, description="OCR character recognition threshold")
):
    if "plate_reader" not in models:
        raise HTTPException(
            status_code=status.HTTP_503_SERVICE_UNAVAILABLE,
            detail="AI models are not loaded. Please verify model weights."
        )

    # 1. Read and decode image buffer
    try:
        image_bytes = await file.read()
        nparr = np.frombuffer(image_bytes, np.uint8)
        frame = cv2.imdecode(nparr, cv2.IMREAD_COLOR)
        if frame is None:
            raise ValueError("Unable to decode image file.")
    except Exception as e:
        raise HTTPException(
            status_code=status.HTTP_400_BAD_REQUEST,
            detail=f"Invalid image format: {str(e)}"
        )

    start_time = time.time()
    device = models["device"]
    vehicle_detector = models["vehicle_detector"]
    plate_detector = models["plate_detector"]
    plate_reader = models["plate_reader"]

    best_result = None
    frame_h, frame_w = frame.shape[:2]

    # 2. Vehicle Detection
    vehicle_results = vehicle_detector(
        frame,
        verbose=False,
        imgsz=640,
        device=device,
        conf=vconf
    )[0]

    detected_vehicles = []
    if len(vehicle_results.boxes) > 0:
        for idx, box in enumerate(vehicle_results.boxes):
            xyxy = box.xyxy[0].cpu().numpy().astype(int)
            conf = float(box.conf[0].cpu().numpy())
            cls_id = int(box.cls[0].cpu().numpy())
            label = map_label(cls_id, VEHICLES["en"])
            detected_vehicles.append({
                "bbox": xyxy,
                "conf": conf,
                "label": label
            })

    # 3. Plate Detection & OCR inside detected vehicle regions
    for veh in detected_vehicles:
        vx1, vy1, vx2, vy2 = veh["bbox"]
        vx1, vy1 = max(0, vx1), max(0, vy1)
        vx2, vy2 = min(frame_w, vx2), min(frame_h, vy2)

        cropped_vehicle = frame[vy1:vy2, vx1:vx2]
        if not check_image_size(cropped_vehicle, 64, 64):
            continue

        plate_dets = plate_detector(
            cropped_vehicle,
            verbose=False,
            imgsz=320,
            device=device,
            conf=pconf
        )[0]

        if len(plate_dets.boxes) > 0:
            for p_box in plate_dets.boxes:
                p_xyxy = p_box.xyxy[0].cpu().numpy().astype(int)
                try:
                    cropped_plate = crop_expanded_plate(p_xyxy, cropped_vehicle, expand_ratio=0.2)
                except Exception:
                    cropped_plate = cropped_vehicle[p_xyxy[1]:p_xyxy[3], p_xyxy[0]:p_xyxy[2]]

                if not check_image_size(cropped_plate, 16, 16):
                    continue

                raw_text, ocr_conf = extract_plate_text(plate_reader, cropped_plate)
                if ocr_conf >= ocr_thresh and len(raw_text) > 0:
                    is_legit = check_legit_plate(raw_text)
                    corrected_text = correct_plate(raw_text) if is_legit else raw_text

                    global_plate_bbox = [
                        int(p_xyxy[0] + vx1),
                        int(p_xyxy[1] + vy1),
                        int(p_xyxy[2] + vx1),
                        int(p_xyxy[3] + vy1)
                    ]

                    best_result = {
                        "plate_number": corrected_text,
                        "raw_plate_number": raw_text,
                        "confidence": round(ocr_conf, 4),
                        "is_legit": is_legit,
                        "vehicle_type": veh["label"],
                        "vehicle_confidence": round(veh["conf"], 4),
                        "vehicle_bbox": [int(x) for x in veh["bbox"]],
                        "plate_bbox": global_plate_bbox
                    }
                    break
        if best_result is not None:
            break

    # 4. Fallback: Search for plate directly across the entire frame if none found in vehicle
    if best_result is None:
        direct_plate_dets = plate_detector(
            frame,
            verbose=False,
            imgsz=640,
            device=device,
            conf=pconf
        )[0]

        if len(direct_plate_dets.boxes) > 0:
            for p_box in direct_plate_dets.boxes:
                p_xyxy = p_box.xyxy[0].cpu().numpy().astype(int)
                try:
                    cropped_plate = crop_expanded_plate(p_xyxy, frame, expand_ratio=0.2)
                except Exception:
                    cropped_plate = frame[p_xyxy[1]:p_xyxy[3], p_xyxy[0]:p_xyxy[2]]

                if check_image_size(cropped_plate, 16, 16):
                    raw_text, ocr_conf = extract_plate_text(plate_reader, cropped_plate)
                    if ocr_conf >= ocr_thresh and len(raw_text) > 0:
                        is_legit = check_legit_plate(raw_text)
                        corrected_text = correct_plate(raw_text) if is_legit else raw_text
                        best_result = {
                            "plate_number": corrected_text,
                            "raw_plate_number": raw_text,
                            "confidence": round(ocr_conf, 4),
                            "is_legit": is_legit,
                            "vehicle_type": "unknown",
                            "vehicle_confidence": None,
                            "vehicle_bbox": None,
                            "plate_bbox": [int(x) for x in p_xyxy]
                        }
                        break

    inference_time = round((time.time() - start_time) * 1000, 2)

    if best_result is None:
        return ApiResponse(
            success=False,
            message="No license plate detected in image.",
            data=None
        )

    best_result["inference_time_ms"] = inference_time
    return ApiResponse(
        success=True,
        message="License plate recognized successfully.",
        data=PlateDetectionData(**best_result)
    )


if __name__ == "__main__":
    import uvicorn
    uvicorn.run("app.main:app", host="0.0.0.0", port=8000, reload=True)
