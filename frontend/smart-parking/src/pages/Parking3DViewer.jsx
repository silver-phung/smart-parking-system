import { useCallback, useEffect, useMemo, useState } from "react";
import { Link } from "react-router-dom";
import ParkingGarageViewport from "../components/parking3d/ParkingGarageViewport.jsx";
import {
  createDemoSlots,
  DEMO_PARKING_LOTS,
  findDemoFloor,
  findDemoLot,
  inferFloorContext,
} from "../components/parking3d/parkingProfiles.js";
import { apiRequest } from "../services/api.js";
import { collectPages } from "../services/staffApi.js";
import "../parking-3d.css";

const STATUS = {
  EMPTY: { label: "Còn trống", color: "#36d399" },
  OCCUPIED: { label: "Có xe", color: "#ef4444" },
  MAINTENANCE: { label: "Bảo trì", color: "#f59e0b" },
  UNAVAILABLE: { label: "Không khả dụng", color: "#64748b" },
};

function readItems(response) {
  const data = response?.data;
  return Array.isArray(data) ? data : data?.items || [];
}

function Icon({ children }) {
  return <span className="p3d-icon" aria-hidden="true">{children}</span>;
}

export default function Parking3DViewer() {
  const defaultLot = DEMO_PARKING_LOTS[0];
  const defaultFloor = defaultLot.floors[0];
  const [lots, setLots] = useState(DEMO_PARKING_LOTS);
  const [floors, setFloors] = useState(defaultLot.floors);
  const [lotId, setLotId] = useState(defaultLot.id);
  const [floorId, setFloorId] = useState(defaultFloor.id);
  const [slots, setSlots] = useState(() => createDemoSlots(defaultFloor));
  const [dataMode, setDataMode] = useState("loading");
  const [error, setError] = useState("");
  const [lastUpdated, setLastUpdated] = useState(new Date());
  const [selected, setSelected] = useState(null);
  const [filter, setFilter] = useState("ALL");
  const [cameraMode, setCameraMode] = useState("overview");
  const [cameraRevision, setCameraRevision] = useState(0);
  const [ready, setReady] = useState(false);
  const [layers, setLayers] = useState({
    ceiling: false,
    lights: true,
    mep: true,
    facilities: true,
    entrance: true,
    parking: true,
  });

  useEffect(() => {
    document.body.classList.add("p3d-viewer-active");
    return () => document.body.classList.remove("p3d-viewer-active");
  }, []);

  const loadLots = useCallback(async () => {
    try {
      const items = await collectPages(async (page) => (
        await apiRequest(`/v1/parking-lots?pageNumber=${page}&pageSize=100&isActive=true`, { skipAuth: true })
      ).data);
      if (items.length) {
        setLots(items);
        setLotId((current) => items.some((lot) => String(lot.id) === current) ? current : String(items[0].id));
      } else {
        setLots(DEMO_PARKING_LOTS);
        setDataMode("demo");
      }
    } catch (requestError) {
      setLots(DEMO_PARKING_LOTS);
      setDataMode("demo");
      setError("Backend chưa sẵn sàng — đang hiển thị dữ liệu mô phỏng an toàn.");
    }
  }, []);

  useEffect(() => {
    void loadLots();
  }, [loadLots]);

  useEffect(() => {
    if (!lotId) return undefined;
    const demoLot = findDemoLot(lotId);
    if (demoLot) {
      setFloors(demoLot.floors);
      setFloorId((current) => demoLot.floors.some((floor) => floor.id === current) ? current : demoLot.floors[0].id);
      setDataMode("demo");
      return undefined;
    }
    let active = true;
    apiRequest(`/v1/parking-lots/${lotId}/floors?isActiveOnly=true`, { skipAuth: true })
      .then((response) => {
        if (!active) return;
        const items = readItems(response);
        setFloors(items);
        setFloorId((current) => items.some((floor) => String(floor.id) === current) ? current : String(items[0]?.id || ""));
      })
      .catch(() => {
        if (active) {
          setFloors([]);
          setDataMode("demo");
        }
      });
    return () => { active = false; };
  }, [lotId]);

  const loadSlots = useCallback(async ({ silent = false } = {}) => {
    const demoFloor = findDemoFloor(floorId);
    if (demoFloor) {
      setSlots(createDemoSlots(demoFloor));
      setDataMode("demo");
      setLastUpdated(new Date());
      return;
    }
    const currentFloor = floors.find((floor) => String(floor.id) === floorId);
    if (!floorId) {
      setSlots(createDemoSlots(defaultFloor));
      setDataMode("demo");
      return;
    }
    if (!silent) setDataMode("loading");
    try {
      const items = await collectPages(async (page) => (
        await apiRequest(`/v1/parking-floors/${floorId}/slots?pageNumber=${page}&pageSize=100`, { skipAuth: true })
      ).data);
      setSlots(items.length ? items.slice(0, 48) : createDemoSlots(inferFloorContext(currentFloor, currentFloor?.name)));
      setDataMode(items.length ? "live" : "demo");
      setError(items.length ? "" : "Tầng chưa có dữ liệu vị trí — đang dùng layout mô phỏng.");
      setLastUpdated(new Date());
    } catch (requestError) {
      setSlots(createDemoSlots(inferFloorContext(currentFloor, currentFloor?.name)));
      setDataMode("demo");
      setError("Không tải được trạng thái vị trí — đang hiển thị dữ liệu mô phỏng.");
    }
  }, [floorId, floors]);

  useEffect(() => {
    void loadSlots();
    const timer = window.setInterval(() => void loadSlots({ silent: true }), 30_000);
    return () => window.clearInterval(timer);
  }, [loadSlots]);

  const selectedLot = lots.find((lot) => String(lot.id) === lotId);
  const selectedFloor = floors.find((floor) => String(floor.id) === floorId);
  const floorLabel = selectedFloor?.name || "Tầng B1";
  const floorContext = useMemo(() => {
    const inferred = inferFloorContext(selectedFloor, floorLabel);
    return { ...inferred, floorCount: selectedFloor?.floorCount || floors.length || inferred.floorCount };
  }, [selectedFloor, floorLabel, floors.length]);
  const normalizedSlots = useMemo(() => slots.map((slot) => ({
    ...slot,
    physicalStatus: slot.physicalStatus === "EMPTY" && slot.isAvailable === false ? "UNAVAILABLE" : slot.physicalStatus,
  })), [slots]);
  const counts = useMemo(() => normalizedSlots.reduce((result, slot) => {
    const key = STATUS[slot.physicalStatus] ? slot.physicalStatus : "UNAVAILABLE";
    result[key] += 1;
    return result;
  }, { EMPTY: 0, OCCUPIED: 0, MAINTENANCE: 0, UNAVAILABLE: 0 }), [normalizedSlots]);
  const occupancy = normalizedSlots.length ? Math.round((counts.OCCUPIED / normalizedSlots.length) * 100) : 0;

  const changeCamera = (mode) => {
    setCameraMode(mode);
    setLayers((current) => ({
      ...current,
      ceiling: ["drive", "entrance", "elevator"].includes(mode),
    }));
    setCameraRevision((value) => value + 1);
  };

  return (
    <section className="p3d-page">
      <div className="p3d-topbar">
        <div className="p3d-topbar__brand">
          <Link to="/parking" aria-label="Quay lại danh sách bãi"><Icon>←</Icon></Link>
          <div>
            <span>TRUNG TÂM GIÁM SÁT 3D</span>
            <strong>{selectedLot?.name || "SmartPark Underground"}</strong>
          </div>
        </div>
        <div className="p3d-topbar__state">
          <span className={`p3d-live p3d-live--${dataMode}`} />
          <div><strong>{dataMode === "live" ? "DỮ LIỆU TRỰC TIẾP" : dataMode === "loading" ? "ĐANG KẾT NỐI" : "CHẾ ĐỘ MÔ PHỎNG"}</strong><small>Cập nhật {lastUpdated.toLocaleTimeString("vi-VN", { hour: "2-digit", minute: "2-digit", second: "2-digit" })}</small></div>
          <button type="button" onClick={() => void loadSlots()} title="Làm mới">↻</button>
        </div>
      </div>

      <div className="p3d-stage">
        <ParkingGarageViewport
          key={`${lotId}-${floorId || floorLabel}`}
          floorLabel={floorLabel}
          floorContext={floorContext}
          slots={normalizedSlots}
          filter={filter}
          cameraMode={cameraMode}
          cameraRevision={cameraRevision}
          layers={layers}
          onSelect={setSelected}
          onReady={(value, viewerError) => {
            setReady(value);
            if (viewerError) setError("Thiết bị không khởi tạo được WebGL. Hãy bật tăng tốc phần cứng.");
          }}
        />

        <aside className="p3d-panel p3d-panel--left">
          <div className="p3d-panel__eyebrow">BẢN ĐỒ VẬN HÀNH</div>
          <label className="p3d-field">
            <span>Bãi đỗ xe</span>
            <select value={lotId} onChange={(event) => setLotId(event.target.value)} disabled={!lots.length}>
              {lots.length ? lots.map((lot) => <option key={lot.id} value={lot.id}>{lot.name}</option>) : <option>SmartPark Underground</option>}
            </select>
          </label>
          <label className="p3d-field">
            <span>Tầng quan sát</span>
            <select value={floorId} onChange={(event) => setFloorId(event.target.value)} disabled={!floors.length}>
              {floors.length ? floors.map((floor) => <option key={floor.id} value={floor.id}>{floor.name}</option>) : <><option value="">Tầng B1</option><option value="B2">Tầng B2</option></>}
            </select>
          </label>
          <div className={`p3d-flow-card p3d-flow-card--${floorContext.parkingType?.toLowerCase()}`}>
            <span>{floorContext.code} · PHÂN LUỒNG</span>
            <strong>{floorContext.parkingType === "MONTHLY" ? "KHU GỬI XE THÁNG" : "KHU GỬI XE VÃNG LAI"}</strong>
            <small>{floorContext.floorNumber === 1 ? "Xe vãng lai đi làn riêng xuống B2" : floorContext.floorNumber === 2 ? "Checkout vãng lai trước dốc lên" : "Dốc một chiều đặt cuối hành trình"}</small>
          </div>
          <div className="p3d-meter">
            <div><span>Mức sử dụng</span><strong>{occupancy}%</strong></div>
            <div className="p3d-meter__track"><span style={{ width: `${occupancy}%` }} /></div>
          </div>
          <div className="p3d-stats">
            {Object.entries(STATUS).map(([key, value]) => (
              <button key={key} type="button" className={filter === key ? "is-active" : ""} onClick={() => setFilter(filter === key ? "ALL" : key)}>
                <i style={{ background: value.color }} /><span>{value.label}</span><strong>{counts[key]}</strong>
              </button>
            ))}
          </div>
          <button type="button" className={`p3d-filter-all ${filter === "ALL" ? "is-active" : ""}`} onClick={() => setFilter("ALL")}>Hiển thị tất cả vị trí</button>
        </aside>

        <div className="p3d-camera-bar" aria-label="Góc camera">
          {[
            ["overview", "◇", "Toàn cảnh"],
            ["top", "⌗", "Mặt bằng"],
            ["drive", "▰", "Làn xe"],
            ["entrance", "⊣", "Cổng vào"],
            ["elevator", "▥", "Thang máy"],
          ].map(([mode, icon, label]) => (
            <button type="button" key={mode} className={cameraMode === mode ? "is-active" : ""} onClick={() => changeCamera(mode)}><b>{icon}</b><span>{label}</span></button>
          ))}
        </div>

        <aside className="p3d-panel p3d-panel--right">
          <div className="p3d-panel__eyebrow">LỚP HIỂN THỊ</div>
          {[
            ["ceiling", "Trần & dầm"], ["lights", "Chiếu sáng"], ["mep", "Ống gió & PCCC"],
            ["facilities", "Tiện ích & thoát hiểm"], ["entrance", "Booth vào / ra"], ["parking", "Ô đỗ & phương tiện"],
          ].map(([key, label]) => (
            <label className="p3d-switch" key={key}><span>{label}</span><input type="checkbox" checked={layers[key]} onChange={(event) => setLayers((current) => ({ ...current, [key]: event.target.checked }))} /><i /></label>
          ))}
          <div className="p3d-system">
            <div><span>Camera</span><strong>{ready ? "ONLINE" : "INIT"}</strong></div>
            <div><span>Thông gió</span><strong>NORMAL</strong></div>
            <div><span>PCCC</span><strong>ARMED</strong></div>
            <div><span>CO₂</span><strong>412 ppm</strong></div>
          </div>
        </aside>

        {error && <div className="p3d-alert" role="status"><span>!</span>{error}<button type="button" onClick={() => setError("")}>×</button></div>}

        {selected && (
          <div className="p3d-detail">
            <button type="button" className="p3d-detail__close" onClick={() => setSelected(null)} aria-label="Đóng">×</button>
            <div className="p3d-detail__code"><span>VỊ TRÍ ĐỖ XE</span><strong>{selected.slotCode}</strong></div>
            <div className="p3d-detail__status"><i style={{ background: STATUS[selected.physicalStatus]?.color || STATUS.UNAVAILABLE.color }} />{STATUS[selected.physicalStatus]?.label || selected.physicalStatus}</div>
            <dl>
              <div><dt>Khu vực</dt><dd>{selected.zone || String(selected.slotCode).split("-")[0]}</dd></div>
              <div><dt>Tầng</dt><dd>{floorLabel}</dd></div>
              <div><dt>Cảm biến</dt><dd>Đang hoạt động</dd></div>
              <div><dt>Mã dữ liệu</dt><dd>#{selected.id}</dd></div>
            </dl>
            <button type="button" onClick={() => changeCamera("overview")}>Trở về toàn cảnh</button>
          </div>
        )}
      </div>
    </section>
  );
}
