import { useEffect, useState } from "react";
import ParkingViewer3D from "../components/ParkingViewer3D.jsx";
import { apiRequest } from "../services/api.js";
import { getAuthSession } from "../services/authSession.js";

export default function Viewer3D({ embedded = false }) {
  const role = getAuthSession()?.userProfile.role;
  const canChoose = role === "USER";
  const [lots, setLots] = useState([]);
  const [lotId, setLotId] = useState(null);
  const [error, setError] = useState("");
  const [selected, setSelected] = useState(null);
  const [chosen, setChosen] = useState(null);
  const [showForm, setShowForm] = useState(false);
  const [form, setForm] = useState({ carPlate: "", vehicleModel: "", vehicleColor: "" });
  const [formError, setFormError] = useState("");
  const [saving, setSaving] = useState(false);
  const [refreshKey, setRefreshKey] = useState(0);

  const openForm = () => {
    setFormError("");
    setShowForm(true);
  };

  const submitReserve = async (event) => {
    event.preventDefault();
    setSaving(true);
    setFormError("");
    try {
      const data = await apiRequest(
        `/v1/parking-viewer/slots/${encodeURIComponent(selected.slotCode)}/reserve`,
        { method: "POST", body: JSON.stringify(form) }
      );
      setChosen(data.slot);
      setSelected(null);
      setShowForm(false);
      setForm({ carPlate: "", vehicleModel: "", vehicleColor: "" });
      setRefreshKey((k) => k + 1);
    } catch (err) {
      setFormError(err.message);
    } finally {
      setSaving(false);
    }
  };

  useEffect(() => {
    apiRequest("/v1/parking-viewer/lots")
      .then((data) => {
        setLots(data.lots || []);
        setLotId(data.lots?.[0]?.lotId ?? null);
      })
      .catch((err) => setError(err.message));
  }, []);

  const lot = lots.find((l) => l.lotId === lotId);

  return (
    <section className={embedded ? "viewer3d" : "viewer3d container"}>
      <div className="viewer3d__bar">
        <h1>Mô hình 3D bãi đỗ</h1>
        <label>
          Bãi đỗ{" "}
          <select
            value={lotId ?? ""}
            onChange={(e) => {
              setLotId(Number(e.target.value));
              setSelected(null);
              setChosen(null);
            }}
          >
            {lots.map((l) => (
              <option key={l.lotId} value={l.lotId}>{l.lotName}</option>
            ))}
          </select>
        </label>
      </div>

      {error && <p className="viewer3d__error" role="alert">{error}</p>}

      <div className="viewer3d__stage">
        {lot && (
          <ParkingViewer3D
            key={`${lot.lotId}-${refreshKey}`}
            lot={lot}
            onSlotClick={setSelected}
            onError={(err) => setError(err.message)}
          />
        )}
      </div>

      <div className="viewer3d__panel">
        {selected ? (
          <>
            <strong>Ô {selected.slotCode}</strong> · {selected.floorName || selected.floorId} · {selected.physicalStatus}
            {canChoose && selected.physicalStatus === "EMPTY" && (
              <button type="button" className="btn btn--primary" onClick={openForm}>
                Chọn ô này
              </button>
            )}
          </>
        ) : (
          <span>{canChoose ? "Nhấn vào một ô trống để chọn chỗ đỗ." : "Nhấn vào một ô để xem chi tiết."}</span>
        )}
        {canChoose && chosen && (
          <p className="viewer3d__chosen">
            Đã giữ chỗ ô <strong>{chosen.slotCode}</strong> ({chosen.lotName || lot?.lotName}, {chosen.floorName || chosen.floorId}) cho xe {chosen.currentCarPlate}.
          </p>
        )}
      </div>

      {showForm && selected && (
        <div className="viewer3d-modal" role="dialog" aria-modal="true" aria-labelledby="reserve-title">
          <form className="viewer3d-modal__box" onSubmit={submitReserve}>
            <h2 id="reserve-title">Giữ chỗ ô {selected.slotCode}</h2>
            <p className="viewer3d-modal__sub">{selected.lotName || lot?.lotName} · {selected.floorName || selected.floorId}</p>
            <label>
              Biển số xe
              <input value={form.carPlate} onChange={(e) => setForm({ ...form, carPlate: e.target.value })} placeholder="VD: 51F-123.45" maxLength={15} required autoFocus />
            </label>
            <label>
              Tên xe
              <input value={form.vehicleModel} onChange={(e) => setForm({ ...form, vehicleModel: e.target.value })} placeholder="VD: Toyota Vios" maxLength={50} required />
            </label>
            <label>
              Màu xe
              <input value={form.vehicleColor} onChange={(e) => setForm({ ...form, vehicleColor: e.target.value })} placeholder="VD: Trắng" maxLength={30} required />
            </label>
            {formError && <p className="viewer3d__error" role="alert">{formError}</p>}
            <div className="viewer3d-modal__actions">
              <button type="button" className="btn btn--outline" onClick={() => setShowForm(false)} disabled={saving}>Hủy</button>
              <button type="submit" className="btn btn--primary" disabled={saving}>{saving ? "Đang lưu..." : "Xác nhận giữ chỗ"}</button>
            </div>
          </form>
        </div>
      )}
    </section>
  );
}
