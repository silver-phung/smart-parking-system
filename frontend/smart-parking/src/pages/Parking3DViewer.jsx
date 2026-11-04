import { useCallback, useMemo, useRef, useState, useSyncExternalStore } from "react";
import ParkingGarageViewport from "../components/parking3d/ParkingGarageViewport.jsx";
import {
  INTERNAL_ROLES,
  PARKING_LOTS,
  createFloorModel,
} from "../components/parking3d/garageScene.js";
import { getAuthSession, subscribeAuthSession } from "../services/authSession.js";

const VIEW_MODES = [
  ["WALK", "Đi bộ", "Bấm điểm sáng để di chuyển"],
  ["OVERVIEW", "Toàn cảnh 3D", "Quan sát toàn bộ luồng xe"],
  ["TOP", "Mặt bằng", "Kiểm tra 4 dãy và 3 lối"],
];

function floorPurpose(model) {
  if (model.lot.kind === "HIGH_RISE") {
    return "Tầng nổi hỗn hợp · 50 vị trí gửi tháng và 50 vị trí vãng lai";
  }
  if (model.floorId === "B1") {
    return "Tầng gửi tháng · khách vãng lai đi trong làn riêng để xuống B2";
  }
  if (model.floorId === "B2") {
    return "Tầng khách vãng lai · có điểm check-out trước khi lên B1";
  }
  return "Tầng khách vãng lai · dốc xuống đặt ở cuối hành trình tìm chỗ";
}

export default function Parking3DViewer() {
  const session = useSyncExternalStore(subscribeAuthSession, getAuthSession, getAuthSession);
  const actor = session?.userProfile?.role || "GUEST";
  const [lotId, setLotId] = useState(PARKING_LOTS[0].id);
  const lot = useMemo(() => PARKING_LOTS.find((item) => item.id === lotId) || PARKING_LOTS[0], [lotId]);
  const [floorId, setFloorId] = useState(PARKING_LOTS[0].floors[0]);
  const model = useMemo(() => createFloorModel(lot, floorId), [floorId, lot]);
  const [viewMode, setViewMode] = useState("WALK");
  const [nearVehicle, setNearVehicle] = useState(null);
  const [nearElevator, setNearElevator] = useState(false);
  const [ready, setReady] = useState(false);
  const viewportRef = useRef(null);
  const showInternalDetails = INTERNAL_ROLES.has(actor);
  const upOffset = lot.kind === "UNDERGROUND" ? -1 : 1;
  const downOffset = -upOffset;
  const canGoUp = Boolean(lot.floors[model.floorIndex + upOffset]);
  const canGoDown = Boolean(lot.floors[model.floorIndex + downOffset]);

  const handleNearVehicle = useCallback((vehicle) => setNearVehicle(vehicle), []);
  const handleElevatorProximity = useCallback((isNear) => setNearElevator(isNear), []);
  const handleReady = useCallback(() => setReady(true), []);

  function changeLot(nextLotId) {
    const nextLot = PARKING_LOTS.find((item) => item.id === nextLotId) || PARKING_LOTS[0];
    setReady(false);
    setLotId(nextLot.id);
    setFloorId(nextLot.floors[0]);
    setViewMode("WALK");
    setNearVehicle(null);
    setNearElevator(false);
  }

  function changeFloor(nextFloor) {
    if (!lot.floors.includes(nextFloor) || nextFloor === floorId) return;
    setReady(false);
    setFloorId(nextFloor);
    setViewMode("WALK");
    setNearVehicle(null);
    setNearElevator(false);
  }

  function moveFloor(offset) {
    const next = lot.floors[model.floorIndex + offset];
    if (next) changeFloor(next);
  }

  return (
    <section className="parking3d-page">
      <div className="parking3d-shell">
        <header className="parking3d-toolbar">
          <div className="parking3d-title">
            <span className="parking3d-kicker">MÔ PHỎNG LUỒNG XE · ACTOR {actor}</span>
            <h1>Sơ đồ bãi đỗ xe 3D</h1>
            <p>{floorPurpose(model)}</p>
          </div>
          <div className="parking3d-selectors" aria-label="Chọn bãi và tầng">
            <label>
              <span>Bãi đỗ xe</span>
              <select value={lotId} onChange={(event) => changeLot(event.target.value)}>
                {PARKING_LOTS.map((item) => (
                  <option key={item.id} value={item.id}>{item.name}</option>
                ))}
              </select>
            </label>
            <label>
              <span>Tầng đang xem</span>
              <select value={floorId} onChange={(event) => changeFloor(event.target.value)}>
                {lot.floors.map((floor) => (
                  <option key={floor} value={floor}>Tầng {floor}</option>
                ))}
              </select>
            </label>
          </div>
        </header>

        <div className="parking3d-stage">
          <ParkingGarageViewport
            ref={viewportRef}
            model={model}
            actor={actor}
            viewMode={viewMode}
            onNearVehicle={handleNearVehicle}
            onElevatorProximity={handleElevatorProximity}
            onReady={handleReady}
          />

          {!ready && <div className="parking3d-loading">Đang dựng tầng {floorId}…</div>}

          <aside className="parking3d-summary" aria-label="Thống kê tầng">
            <strong>{lot.shortName} · {floorId}</strong>
            <span>{model.counts.physical} vị trí hình học</span>
            <span>{model.counts.configured} ô theo cấu hình</span>
            <div className="parking3d-summary__grid">
              <span><i className="is-free" />{model.counts.available} trống</span>
              <span><i className="is-busy" />{model.counts.occupied} có xe</span>
              <span><i className="is-reserved" />{model.counts.reserved} đã giữ</span>
              <span><i className="is-maintenance" />{model.counts.maintenance} bảo trì</span>
            </div>
          </aside>

          <div className="parking3d-flow-card">
            <span>LUỒNG TẦNG {floorId}</span>
            {model.isUndergroundB1 ? (
              <p>Xuống B1 → Thẻ tháng rẽ vào booth → Vãng lai đi thẳng xuống B2 → Hai luồng dùng chung dốc lên mặt đất.</p>
            ) : model.floorId === "B2" && model.lot.kind === "UNDERGROUND" ? (
              <p>Khách vãng lai tìm chỗ → Check-out tại B2 → Lên B1 → Đi thẳng lên mặt đất.</p>
            ) : (
              <p>Đi hết tuyến tìm chỗ → Nếu hết chỗ, dùng dốc cuối đường để sang tầng kế tiếp.</p>
            )}
          </div>

          {nearVehicle && (
            <aside className="parking3d-vehicle-card" aria-live="polite">
              <button type="button" aria-label="Đóng thông tin xe" onClick={() => setNearVehicle(null)}>×</button>
              <span>XE Ở GẦN</span>
              <strong>{nearVehicle.code}</strong>
              <dl>
                <div><dt>Khu vực</dt><dd>{nearVehicle.zone === "MONTHLY" ? "Gửi tháng" : "Vãng lai"}</dd></div>
                <div><dt>Trạng thái</dt><dd>{nearVehicle.status === "OCCUPIED" ? "Đang có xe" : nearVehicle.status}</dd></div>
                {showInternalDetails && <div><dt>Biển số</dt><dd>{nearVehicle.plate}</dd></div>}
                {showInternalDetails && <div><dt>Mẫu xe</dt><dd>{nearVehicle.vehicleModel}</dd></div>}
              </dl>
              {!showInternalDetails && <small>Biển số chỉ hiển thị cho Staff, Manager và Admin.</small>}
            </aside>
          )}

          {nearElevator && viewMode === "WALK" && (
            <aside className="parking3d-elevator-panel" aria-live="polite">
              <span>🛗 BẢNG GỌI THANG MÁY</span>
              <strong>Tầng {floorId}</strong>
              <div>
                <button type="button" disabled={!canGoUp} onClick={() => moveFloor(upOffset)}>▲ Lên</button>
                <button type="button" disabled={!canGoDown} onClick={() => moveFloor(downOffset)}>▼ Xuống</button>
              </div>
            </aside>
          )}

          <div className="parking3d-view-controls" role="group" aria-label="Chọn góc nhìn">
            {VIEW_MODES.map(([mode, label, description]) => (
              <button
                key={mode}
                type="button"
                className={viewMode === mode ? "is-active" : ""}
                onClick={() => setViewMode(mode)}
                title={description}
              >
                {label}
              </button>
            ))}
            <button type="button" className="parking3d-elevator-shortcut" onClick={() => {
              setViewMode("WALK");
              viewportRef.current?.walkToElevator();
            }}>
              Tìm thang máy
            </button>
          </div>

          <div className="parking3d-help">
            <span>◉ Bấm điểm sáng để đi</span>
            <span>🖱 Kéo để xoay</span>
            <span>⌁ Cuộn để zoom</span>
            <span>Thông tin xe hiện trong phạm vi 11 m</span>
          </div>
        </div>

        <div className="parking3d-legend">
          <span><i className="zone-monthly" /> Khu gửi tháng</span>
          <span><i className="zone-visitor" /> Khu vãng lai</span>
          <span><i className="state-occupied" /> Có xe</span>
          <span><i className="state-reserved" /> Đã giữ</span>
          <span><i className="state-maintenance" /> Bảo trì</span>
          <span><i className="state-inactive" /> Chưa cấu hình</span>
        </div>
      </div>
    </section>
  );
}
