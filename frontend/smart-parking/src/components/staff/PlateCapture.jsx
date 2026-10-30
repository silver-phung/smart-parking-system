import { useEffect, useId, useState } from "react";
import {
  normalizePlate,
  staffApi,
  validatePhoto,
} from "../../services/staffApi.js";
import { Notice, SafePhoto, StaffError, StaffIcon } from "./StaffUI.jsx";

export const emptyCapture = () => ({
  file: null,
  plate: "",
  recognition: null,
  note: "",
  imageUrl: "",
});
export const needsCorrectionNote = (capture) =>
  Boolean(
    capture.recognition?.plateNumber &&
      normalizePlate(capture.recognition.plateNumber) !==
        normalizePlate(capture.plate),
  );

export async function prepareCapture(capture, update) {
  if (needsCorrectionNote(capture) && !capture.note.trim())
    throw new Error("Nhập lý do sửa biển số AI trước khi xác nhận.");
  if (
    capture.recognition?.id &&
    normalizePlate(capture.recognition.confirmedPlateNumber || "") !==
      normalizePlate(capture.plate)
  ) {
    const result = await staffApi.confirmDetection(
      capture.recognition.id,
      capture.plate,
      capture.note.trim() || null,
    );
    if (
      normalizePlate(result?.data?.confirmedPlateNumber || "") !==
      normalizePlate(capture.plate)
    ) {
      throw new Error(
        "Chưa xác nhận được biển số nhận diện. Vui lòng thử lại trước khi ghi nhận phiên.",
      );
    }
    update((current) => ({ ...current, recognition: result.data }));
  }
  if (!capture.file || capture.imageUrl) return capture.imageUrl;
  const imageUrl = await staffApi.upload(capture.file);
  update((current) => ({ ...current, imageUrl }));
  return imageUrl;
}

export default function PlateCapture({
  value,
  onChange,
  disabled,
  onWorking,
  title = "Ảnh xe tại cổng",
  plateLabel = "Biển số xe",
}) {
  const id = useId();
  const [preview, setPreview] = useState("");
  const [detecting, setDetecting] = useState(false);
  const [error, setError] = useState(null);
  useEffect(() => {
    if (!value.file) {
      setPreview("");
      return;
    }
    const url = URL.createObjectURL(value.file);
    setPreview(url);
    return () => URL.revokeObjectURL(url);
  }, [value.file]);
  const update = (patch) => onChange((current) => ({ ...current, ...patch }));
  const choose = (event) => {
    const file = event.target.files?.[0];
    event.target.value = "";
    if (!file) return;
    try {
      validatePhoto(file);
      setError(null);
      update({ file, plate: "", recognition: null, note: "", imageUrl: "" });
    } catch (err) {
      setError(err);
    }
  };
  const detect = async () => {
    if (detecting || !value.file) return;
    setDetecting(true);
    setError(null);
    onWorking?.(true);
    try {
      const recognition = await staffApi.detect(value.file);
      if (!recognition?.id)
        throw new Error(
          "Chưa nhận được kết quả nhận diện hợp lệ. Bạn có thể nhập biển số bằng tay.",
        );
      update({ recognition, plate: recognition.plateNumber || "", note: "" });
    } catch (err) {
      setError(err);
    } finally {
      setDetecting(false);
      onWorking?.(false);
    }
  };
  return (
    <fieldset className="staff-capture" disabled={disabled || detecting}>
      <legend>
        {title} <span className="staff-optional">· Không bắt buộc</span>
      </legend>
      <div className="staff-capture-row">
        <SafePhoto url={preview} alt="Ảnh xe vừa chọn" />
        <div className="staff-capture-controls">
          <p>Chọn ảnh rõ biển số, hoặc nhập biển số đã kiểm tra bên dưới.</p>
          <label className="staff-file-button" htmlFor={`${id}-file`}>
            <StaffIcon name="camera" />
            {value.file ? "Đổi ảnh" : "Chọn / chụp ảnh"}
          </label>
          <input
            id={`${id}-file`}
            className="staff-file-input"
            type="file"
            accept="image/jpeg,image/png,image/webp"
            capture="environment"
            onChange={choose}
          />
          <span className="staff-hint">JPEG, PNG, WebP · Tối đa 10 MB</span>
          {value.file && (
            <>
              <span className="staff-file-name">{value.file.name}</span>
              <div className="staff-inline-actions">
                <button
                  type="button"
                  className="staff-button staff-button--secondary"
                  onClick={detect}
                >
                  {detecting ? "Đang nhận diện…" : "Nhận diện biển số"}
                </button>
                <button
                  type="button"
                  className="staff-text-button"
                  onClick={() => {
                    onChange(emptyCapture());
                    setError(null);
                  }}
                >
                  Bỏ ảnh
                </button>
              </div>
            </>
          )}
        </div>
      </div>
      <StaffError error={error} />
      {value.recognition && (
        <Notice kind={value.recognition.plateNumber ? "info" : "warning"}>
          {value.recognition.plateNumber ? (
            <>
              AI đọc được <strong>{value.recognition.plateNumber}</strong>
              {Number.isFinite(value.recognition.confidence) && (
                <>
                  {" "}
                  · Độ tin cậy {Math.round(value.recognition.confidence * 100)}%
                </>
              )}
              . Kiểm tra và chỉnh lại nếu cần.
            </>
          ) : (
            "Chưa đọc được biển số. Hãy nhập biển số thực tế hoặc chọn ảnh rõ hơn."
          )}
        </Notice>
      )}
      <label className="staff-field" htmlFor={`${id}-plate`}>
        {plateLabel}
        <input
          id={`${id}-plate`}
          className="staff-plate-input"
          autoComplete="off"
          placeholder="Ví dụ: 51F-123.45"
          maxLength={20}
          value={value.plate}
          onChange={(event) =>
            update({ plate: event.target.value.toUpperCase() })
          }
          required
        />
      </label>
      {needsCorrectionNote(value) && (
        <label className="staff-field" htmlFor={`${id}-note`}>
          Lý do sửa biển số AI
          <textarea
            id={`${id}-note`}
            value={value.note}
            onChange={(event) => update({ note: event.target.value })}
            maxLength={256}
            required
            placeholder="Ví dụ: AI nhận nhầm số 8 thành số 3"
          />
        </label>
      )}
      {value.file && (
        <p className="staff-hint">
          Ảnh sẽ được lưu cùng phiên khi xác nhận. Nếu lưu ảnh thất bại, bạn có
          thể thử lại hoặc bỏ ảnh để nhập tay.
        </p>
      )}
    </fieldset>
  );
}
