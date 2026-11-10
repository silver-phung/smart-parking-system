import { useCallback, useState } from "react";
import { useSearchParams } from "react-router-dom";
import RequireRole from "../components/RequireRole.jsx";
import { StaffEntry, StaffExit } from "../components/staff/StaffGate.jsx";
import StaffBookings from "../components/staff/StaffBookings.jsx";
import {
  StaffDispatches,
  StaffSessions,
} from "../components/staff/StaffLists.jsx";
import StaffVerification from "../components/staff/StaffVerification.jsx";
import {
  Notice,
  StaffError,
  StaffIcon,
  useStaffQuery,
} from "../components/staff/StaffUI.jsx";
import { staffApi } from "../services/staffApi.js";
import { staffLocation } from "../services/navigation.js";
import "../staff.css";

const views = [
  { id: "entry", name: "Xe vào vãng lai", description: "Nhận xe, xác minh ô và theo dõi điều phối" },
  { id: "bookings", name: "Xe đặt qua web", description: "Xác nhận xe đã đặt và vị trí thực tế" },
  { id: "exit", name: "Xe ra", description: "Đối chiếu tại cổng ra" },
];

function StaffContent() {
  const [params, setParams] = useSearchParams();
  const { view: selectedView, step } = staffLocation(params.get("view"), params.get("step"));
  const view = views.find((item) => item.id === selectedView) || views[0];
  const [selectedLotId, setSelectedLotId] = useState("");
  const [revision, setRevision] = useState(0);
  const [busy, setBusy] = useState(false);
  const [newBookingsCount, setNewBookingsCount] = useState(0);
  const [confirmedSession, setConfirmedSession] = useState(null);
  const loadLots = useCallback(async (signal) => {
    const result = await staffApi.assignedLots(signal);
    if (!Array.isArray(result))
      throw new Error(
        "Chưa tải được danh sách bãi được phân công. Vui lòng thử lại.",
      );
    return result;
  }, []);
  const lots = useStaffQuery(loadLots);
  const lot =
    lots.data?.find((item) => String(item.lotId) === selectedLotId) ||
    lots.data?.[0];
  const lotId = lot?.lotId;
  const loadCapacity = useCallback(
    async (signal) =>
      lotId
        ? {
            lotId,
            value: await staffApi.capacity(lotId, signal),
            time: new Date(),
          }
        : null,
    [lotId, revision],
  );
  const capacity = useStaffQuery(loadCapacity);
  const refresh = () => setRevision((value) => value + 1);
  const open = (nextView, sessionId) => {
    const target = staffLocation(nextView);
    setParams({
      view: target.view,
      ...(target.step && target.step !== "gate" ? { step: target.step } : {}),
      ...(sessionId ? { sessionId: String(sessionId) } : {}),
    });
  };
  const openStep = (nextStep) =>
    setParams({ view: view.id, ...(nextStep !== "gate" ? { step: nextStep } : {}) });
  const currentCapacity = capacity.data?.lotId === lotId ? capacity.data : null;
  const count = (key) =>
    capacity.loading || !currentCapacity
      ? "—"
      : (currentCapacity.value?.[key] ?? "—");
  const props = { lot, revision, onChanged: refresh, onBusyChange: setBusy };

  return (
    <div className="staff-workspace container">
      <header className="staff-workspace-heading">
        <div>
          <span className="staff-eyebrow">SMART PARKING / NHÂN VIÊN</span>
          <h1>Trạm vận hành</h1>
          <p>Tiếp nhận tại cổng. Xác minh tại ô. Theo dõi cùng một lượt xe.</p>
        </div>
        <span className="staff-shift-label">
          <i /> Vận hành tại bãi
        </span>
      </header>
      <StaffError error={lots.error} />
      {lots.error && (
        <button
          className="staff-button staff-button--secondary"
          onClick={lots.reload}
        >
          Tải lại bãi được phân công
        </button>
      )}
      {lots.loading && <Notice>Đang tải các bãi được phân công…</Notice>}
      {!lots.loading && !lots.error && !lots.data?.length && (
        <section className="staff-panel staff-empty">
          <StaffIcon name="pin" size={38} />
          <h2>Chưa có bãi được phân công</h2>
          <p>
            Liên hệ Admin để được phân công bãi và nhiệm vụ trước khi tiếp nhận
            xe.
          </p>
        </section>
      )}
      {lot && (
        <>
          <section className="staff-context-bar" aria-label="Bãi đang làm việc">
            <div className="staff-context-select">
              <StaffIcon name="pin" size={23} />
              <label htmlFor="staff-lot">
                Bãi đang làm việc
                <select
                  id="staff-lot"
                  value={lotId}
                  disabled={busy}
                  onChange={(event) => {
                    setSelectedLotId(event.target.value);
                    setNewBookingsCount(0);
                    setParams({ view: view.id });
                  }}
                >
                  {lots.data.map((item) => (
                    <option key={item.lotId} value={item.lotId}>
                      {item.lotName}
                      {!item.isActive ? " · Đóng nhận xe" : ""}
                    </option>
                  ))}
                </select>
              </label>
            </div>
            <div className="staff-context-meta">
              <span
                className={`staff-lot-state ${lot.isActive ? "is-open" : ""}`}
              >
                <i />
                {lot.isActive ? "Đang nhận xe" : "Đóng nhận xe"}
              </span>
              <button
                className="staff-button staff-button--refresh"
                disabled={busy || capacity.loading}
                onClick={refresh}
              >
                <StaffIcon name="refresh" />
                {capacity.loading ? "Đang tải…" : "Làm mới"}
              </button>
            </div>
          </section>
          <div className="staff-metrics" aria-label="Tình trạng bãi">
            <article>
              <span>Nhận thêm xe vãng lai</span>
              <strong>
                {count("availableForWalkIn")}
                <small>chỗ</small>
              </strong>
            </article>
            <article>
              <span>Phiên đang hoạt động</span>
              <strong>
                {count("activeSessionsCount")}
                <small>xe</small>
              </strong>
            </article>
            <article>
              <span>Chờ đối soát vị trí</span>
              <strong>
                {count("unassignedSessionsCount")}
                <small>xe</small>
              </strong>
            </article>
            <article>
              <span>Ô cảm biến báo có xe</span>
              <strong>
                {count("physicalOccupiedSlotsCount")}
                <small>ô</small>
              </strong>
            </article>
          </div>
          <StaffError error={capacity.error} />
          <div className="staff-workspace-meta">
            <span>{view.description}</span>
            <span>
              {currentCapacity
                ? `Cập nhật lúc ${currentCapacity.time.toLocaleTimeString("vi-VN", { hour: "2-digit", minute: "2-digit" })}`
                : "Đang kiểm tra dữ liệu bãi"}
            </span>
          </div>
          {view.id !== "bookings" && newBookingsCount > 0 && (
            <Notice kind="warning">
              Có {newBookingsCount} đặt chỗ mới.{" "}
              <button type="button" className="staff-text-button" disabled={busy} onClick={() => open("bookings")}>
                Xem xe đặt qua web
              </button>
            </Notice>
          )}
          <section
            className="staff-view"
            id="staff-panel-bookings"
            aria-label="Xe đặt qua web"
            hidden={view.id !== "bookings"}
          >
            <StaffBookings
              key={lotId}
              lotId={lotId}
              lotIsActive={lot.isActive}
              revision={revision}
              onChanged={refresh}
              onBusyChange={setBusy}
              onNewBookingsChange={setNewBookingsCount}
              onConfirmed={(session) => {
                setConfirmedSession(session);
                open("exit", session.id);
              }}
            />
          </section>
          {view.id !== "bookings" && <section
            className="staff-view"
            id={`staff-panel-${view.id}`}
            aria-label={view.name}
            key={`${lotId}-${view.id}-${step}-${params.get("sessionId") || ""}`}
          >
            {view.id === "exit" && step === "gate" &&
              confirmedSession?.id === Number(params.get("sessionId")) && (
                <Notice kind="success">
                  Đã xác nhận xe {confirmedSession.plateNumber} đỗ tại ô{" "}
                  {confirmedSession.slotCode}. Phiên #{confirmedSession.id} đang
                  hoạt động; chỉ xác nhận xe ra sau khi thanh toán hoàn tất và
                  xe thực sự đến cổng.
                </Notice>
              )}
            {view.id !== "bookings" && (
              <div className="staff-flow-nav" role="group" aria-label={`Các bước ${view.name}`}>
                {(view.id === "entry"
                  ? [["gate", "Nhận xe tại cổng"], ["verify", "Xác minh ô"], ["dispatches", "Theo dõi điều phối"]]
                  : [["gate", "Xác nhận xe ra"], ["sessions", "Tra cứu xe đang gửi"]]
                ).map(([id, label]) => (
                  <button
                    key={id}
                    type="button"
                    aria-pressed={step === id}
                    disabled={busy}
                    onClick={() => openStep(id)}
                  >
                    {label}
                  </button>
                ))}
              </div>
            )}
            {view.id === "entry" && step === "gate" && (
              <StaffEntry {...props} capacity={currentCapacity?.value} />
            )}
            {view.id === "exit" && step === "gate" && (
              <StaffExit
                {...props}
                initialSessionId={params.get("sessionId")}
              />
            )}
            {view.id === "entry" && step === "verify" && (
              <StaffVerification
                {...props}
                initialSessionId={params.get("sessionId")}
              />
            )}
            {view.id === "exit" && step === "sessions" && (
              <StaffSessions {...props} onOpen={open} />
            )}
            {view.id === "entry" && step === "dispatches" && (
              <StaffDispatches {...props} onOpen={open} />
            )}
          </section>}
          <p className="staff-permission-note">
            Thao tác được kiểm tra theo nhiệm vụ và bãi được phân công. Nếu bị
            từ chối quyền, liên hệ quản lý để kiểm tra phân công.
          </p>
        </>
      )}
    </div>
  );
}

export default function StaffWorkspace() {
  return (
    <RequireRole role="STAFF">
      <StaffContent />
    </RequireRole>
  );
}
