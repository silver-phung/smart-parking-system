import { useCallback, useEffect, useState } from "react";
import { useSearchParams } from "react-router-dom";
import RequireRole from "../components/RequireRole.jsx";
import StaffBookings from "../components/staff/StaffBookings.jsx";
import { StaffExit } from "../components/staff/StaffExit.jsx";
import {
  StaffDispatches,
  StaffSessions,
} from "../components/staff/StaffLists.jsx";
import { TicketEntry, TicketVerification } from "../components/staff/TicketOperations.jsx";
import "../operations.css";
import {
  Notice,
  StaffError,
  StaffIcon,
  useStaffQuery,
} from "../components/staff/StaffUI.jsx";
import { staffApi } from "../services/staffApi.js";
import { canUseStaffView } from "../services/staffDuty.js";
import "../staff.css";

const views = [
  { id: "bookings", name: "Xe đặt qua web", description: "Xác nhận xe đã đặt và vị trí thực tế" },
  { id: "entry", name: "Xe vào", description: "Tiếp nhận xe vào khu bình thường" },
  { id: "exit", name: "Xe ra", description: "Đối chiếu tại cổng ra" },
  {
    id: "verify",
    name: "Xác minh ô",
    description: "Ghép xe với vị trí thực tế",
  },
  { id: "sessions", name: "Xe đang gửi", description: "Tra cứu phiên tại bãi" },
  {
    id: "dispatches",
    name: "Điều phối",
    description: "Theo dõi kết quả xử lý",
  },
];

function StaffContent() {
  const [params, setParams] = useSearchParams();
  const selectedView = params.get("view") || "entry";
  const [selectedLotId, setSelectedLotId] = useState(params.get("lotId") || "");
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
  // Mỗi Staff chỉ thấy chức năng của loại phân công tại bãi đang chọn (trực cổng hoặc trực khu vực).
  const duty = lot?.duty;
  const visibleViews = views.filter((item) => canUseStaffView(duty, item.id));
  // "Xe đặt qua web" mở từ sidebar/thông báo, không có tab riêng trong dãy tab.
  const view =
    visibleViews.find((item) => item.id === selectedView) ||
    visibleViews[0] ||
    views.find((item) => item.id === "entry");
  useEffect(() => {
    if (lot && view.id !== selectedView) setParams({ view: view.id }, { replace: true });
  }, [lot, view.id, selectedView, setParams]);
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
  const open = (nextView, sessionId) =>
    setParams({
      view: nextView,
      ...(sessionId ? { sessionId: String(sessionId) } : {}),
    });
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
                    setParams({ view: view.id });
                  }}
                >
                  {lots.data.map((item) => (
                    <option key={item.lotId} value={item.lotId}>
                      {item.lotName}
                      {item.duty ? ` (${item.duty === "GATE" || item.duty === "GATE_IN" || item.duty === "GATE_OUT" ? "Trực cổng" : item.duty === "SLOT_VERIFIER" ? "Trực khu vực" : "Toàn quyền"})` : ""}
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
              <span>Chỗ trống khu bình thường</span>
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
          {canUseStaffView(duty, "bookings") && newBookingsCount > 0 && view.id !== "bookings" && <Notice kind="success">Có {newBookingsCount} đặt chỗ mới. <button className="staff-text-button" disabled={busy} onClick={() => open("bookings")}>Xem xe đặt qua web</button></Notice>}
          {canUseStaffView(duty, "bookings") && (
            <section hidden={view.id !== "bookings"} aria-label="Xe đặt qua web">
              <StaffBookings key={lotId} lotId={lotId} lotIsActive={lot.isActive} revision={revision} onChanged={refresh} onBusyChange={setBusy} onNewBookingsChange={setNewBookingsCount} onConfirmed={session => { setConfirmedSession(session); open("exit", session.id); }} />
            </section>
          )}

          <div className="staff-workspace-meta">
            <span>{view.description}</span>
            <span>
              {currentCapacity
                ? `Cập nhật lúc ${currentCapacity.time.toLocaleTimeString("vi-VN", { hour: "2-digit", minute: "2-digit" })}`
                : "Đang kiểm tra dữ liệu bãi"}
            </span>
          </div>
          {view.id !== "bookings" && <section
            className="staff-view"
            id={`staff-panel-${view.id}`}
            aria-label={view.name}
            key={`${lotId}-${view.id}`}
          >
            {view.id === "exit" && confirmedSession?.id === Number(params.get("sessionId")) && <Notice kind="success">Đã xác nhận xe {confirmedSession.plateNumber} đỗ tại ô {confirmedSession.slotCode}. Phiên #{confirmedSession.id} đang hoạt động.</Notice>}
            {view.id === "entry" && (
              <TicketEntry {...props} />
            )}
            {view.id === "exit" && (
              <StaffExit
                {...props}
                initialSessionId={params.get("sessionId")}
              />
            )}
            {view.id === "verify" && (
              <TicketVerification
                {...props}
                initialSessionId={params.get("sessionId")}
              />
            )}
            {view.id === "sessions" && (
              <StaffSessions {...props} onOpen={open} />
            )}
            {view.id === "dispatches" && (
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
