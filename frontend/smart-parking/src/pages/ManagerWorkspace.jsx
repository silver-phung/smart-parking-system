import { useState } from "react";
import { useSearchParams } from "react-router-dom";
import RequireRole from "../components/RequireRole.jsx";
import { apiRequest } from "../services/api.js";
import { staffApi } from "../services/staffApi.js";
import {
  Feedback,
  Pager,
  dateLabel,
  itemsOf,
  statusLabel,
  useResource,
  useMutation,
  write,
} from "../components/WorkspaceUI.jsx";

const labels = {
  EMPTY: "Trống",
  OCCUPIED: "Có xe",
  MAINTENANCE: "Bảo trì",
  UNAVAILABLE: "Ngừng sử dụng",
};
function LotWorkspace({ lotId, revision, refresh, busy, action, view }) {
  const [floorId, setFloorId] = useState("");
  const [page, setPage] = useState(1);
  const [selected, setSelected] = useState(null);
  const [change, setChange] = useState(null);
  const [reason, setReason] = useState("");
  const lot = useResource(`/v1/parking-lots/${lotId}`, revision);
  const capacity = useResource(`/v1/parking-lots/${lotId}/capacity`, revision);
  const floors = useResource(
    view === "slots"
      ? `/v1/parking-lots/${lotId}/floors?isActiveOnly=true`
      : null,
    revision,
  );
  const currentFloor =
    itemsOf(floors.data).find((f) => String(f.id) === floorId) ||
    itemsOf(floors.data)[0];
  const slots = useResource(
    view === "slots" && currentFloor
      ? `/v1/parking-floors/${currentFloor.id}/slots?pageNumber=${page}&pageSize=20&isActive=true`
      : null,
    revision,
  );
  const sessions = useResource(
    `/v1/parking-sessions/active?lotId=${lotId}&page=${view === "sessions" ? page : 1}&pageSize=20`,
    revision,
  );
  const session = useResource(
    selected ? `/v1/parking-sessions/${selected}` : null,
    revision,
  );
  const c = capacity.data;
  const selectChange = (value) => {
    setReason("");
    setChange(value);
  };
  function save(e) {
    e.preventDefault();
    action.run(
      async () => {
        if (change.kind === "slot") {
          const [fresh, activeSessions] = await Promise.all([
            apiRequest(`/v1/parking-slots/${change.id}`),
            staffApi.activeSessions(lotId),
          ]);
          if (
            fresh?.data?.lotId !== lotId ||
            fresh.data.physicalStatus === "OCCUPIED" ||
            fresh.data.currentSessionId ||
            (fresh.data.physicalStatus === "EMPTY" &&
              fresh.data.isAvailable === false) ||
            activeSessions.some((s) => s.currentSlotId === change.id)
          )
            throw new Error(
              "Ô đã có xe hoặc thay đổi phạm vi. Hãy làm mới và kiểm tra lại.",
            );
        }
        return write(
          change.kind === "lot"
            ? `/v1/parking-lots/${lotId}/status`
            : `/v1/parking-slots/${change.id}/status`,
          change.kind === "lot"
            ? { isActive: change.value, reason }
            : { physicalStatus: change.value, reason },
          "PATCH",
        );
      },
      () => {
        setChange(null);
        refresh();
      },
    );
  }
  return (
    <>
      <Feedback loading={lot.loading} error={lot.error || capacity.error} />
      <div className="manager-metrics" aria-label="Tình trạng bãi">
        <article>
          <span>Tiếp nhận</span>
          <strong>
            {lot.data ? (lot.data.isActive ? "Đang mở" : "Đang đóng") : "—"}
          </strong>
        </article>
        <article>
          <span>Chỗ nhận xe vãng lai</span>
          <strong>{c?.availableForWalkIn ?? "—"}</strong>
        </article>
        <article>
          <span>Phiên đang hoạt động</span>
          <strong>{sessions.data?.totalCount ?? "—"}</strong>
        </article>
        <article>
          <span>Chờ xác minh ô</span>
          <strong>{c?.unassignedSessionsCount ?? "—"}</strong>
        </article>
      </div>
      {view === "overview" && (
        <section className="workspace-panel">
          <div className="workspace-heading">
            <div>
              <h2>{lot.data?.name || "Thông tin bãi"}</h2>
              <p>{lot.data?.address}</p>
            </div>
            <button
              className="btn btn--outline"
              disabled={busy || !lot.data}
              onClick={() =>
                selectChange({
                  kind: "lot",
                  value: !lot.data.isActive,
                  label: lot.data.name,
                })
              }
            >
              {lot.data?.isActive ? "Đóng tiếp nhận" : "Mở tiếp nhận"}
            </button>
          </div>
          <dl className="workspace-detail">
            <div>
              <dt>Tổng sức chứa</dt>
              <dd>{c?.totalCapacity ?? "—"}</dd>
            </div>
            <div>
              <dt>Ô cảm biến báo có xe</dt>
              <dd>{c?.physicalOccupiedSlotsCount ?? "—"}</dd>
            </div>
          </dl>
          <p className="workspace-note">
            Đóng tiếp nhận ngăn xe vào mới. Các phiên đang gửi vẫn cần được theo
            dõi và xử lý tại cổng.
          </p>
          {c?.discrepancyNotes && (
            <p className="workspace-note">
              {Array.isArray(c.discrepancyNotes)
                ? c.discrepancyNotes.join(" · ")
                : c.discrepancyNotes}
            </p>
          )}
        </section>
      )}
      {view === "slots" && (
        <section className="workspace-panel">
          <h2>Trạng thái ô đỗ</h2>
          <p className="workspace-note">
            Chọn ô để bảo trì, ngừng sử dụng hoặc đưa lại vào phục vụ. Ô có xe
            hoặc không khả dụng do phiên/điều phối cần được đối soát trước.
          </p>
          <Feedback
            loading={floors.loading || slots.loading}
            error={floors.error || slots.error}
          />
          <div className="workspace-filters">
            <label>
              Tầng
              <select
                value={currentFloor?.id || ""}
                disabled={busy || floors.loading}
                onChange={(e) => {
                  setFloorId(e.target.value);
                  setPage(1);
                  setChange(null);
                }}
              >
                <option value="" disabled>
                  Chọn tầng
                </option>
                {itemsOf(floors.data).map((f) => (
                  <option key={f.id} value={f.id}>
                    {f.name}
                  </option>
                ))}
              </select>
            </label>
          </div>
          {!floors.loading && !floors.error && !currentFloor && (
            <p className="workspace-empty">Chưa có tầng hoạt động.</p>
          )}
          <div className="workspace-table">
            <table>
              <thead>
                <tr>
                  <th>Ô đỗ</th>
                  <th>Trạng thái</th>
                  <th>Khả dụng</th>
                  <th>Cập nhật</th>
                </tr>
              </thead>
              <tbody>
                {itemsOf(slots.data).map((s) => {
                  const blocked =
                    s.physicalStatus === "OCCUPIED" ||
                    (s.physicalStatus === "EMPTY" && !s.isAvailable);
                  return (
                    <tr key={s.id}>
                      <td>
                        <strong>{s.slotCode}</strong>
                      </td>
                      <td>{labels[s.physicalStatus]}</td>
                      <td>{s.isAvailable ? "Có" : "Không"}</td>
                      <td>
                        <select
                          aria-label={`Trạng thái ${s.slotCode}`}
                          value={s.physicalStatus}
                          disabled={busy || blocked || !s.isActive}
                          onChange={(e) =>
                            selectChange({
                              kind: "slot",
                              id: s.id,
                              label: s.slotCode,
                              value: e.target.value,
                            })
                          }
                        >
                          <option value="OCCUPIED" disabled>
                            Có xe
                          </option>
                          {["EMPTY", "MAINTENANCE", "UNAVAILABLE"].map(
                            (value) => (
                              <option key={value} value={value}>
                                {labels[value]}
                              </option>
                            ),
                          )}
                        </select>
                        {blocked && (
                          <small>
                            Đối soát phiên và điều phối trước khi thay đổi.
                          </small>
                        )}
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
          {currentFloor &&
            !slots.loading &&
            !slots.error &&
            !itemsOf(slots.data).length && (
              <p className="workspace-empty">Tầng này chưa có ô hoạt động.</p>
            )}
          <Pager
            page={page}
            data={slots.data}
            onChange={setPage}
            disabled={busy || slots.loading}
          />
        </section>
      )}
      {view === "sessions" && (
        <section className="workspace-panel">
          <h2>Xe đang gửi tại bãi</h2>
          <Feedback loading={sessions.loading} error={sessions.error} />
          <div className="workspace-table">
            <table>
              <thead>
                <tr>
                  <th>Phiên / Biển số</th>
                  <th>Ô hiện tại</th>
                  <th>Giờ vào</th>
                  <th>Trạng thái</th>
                  <th>Chi tiết</th>
                </tr>
              </thead>
              <tbody>
                {itemsOf(sessions.data).map((s) => (
                  <tr key={s.id}>
                    <td>
                      #{s.id}
                      <small>{s.plateNumber}</small>
                    </td>
                    <td>{s.slotCode || "Chờ xác minh"}</td>
                    <td>{dateLabel(s.checkInTime)}</td>
                    <td>{statusLabel(s.status)}</td>
                    <td>
                      <button
                        className="btn btn--outline"
                        onClick={() => setSelected(s.id)}
                      >
                        Xem phiên #{s.id}
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          {!sessions.loading &&
            !sessions.error &&
            !itemsOf(sessions.data).length && (
              <p className="workspace-empty">Không có xe đang gửi tại bãi.</p>
            )}
          <Pager
            page={page}
            data={sessions.data}
            onChange={setPage}
            disabled={busy || sessions.loading}
          />
        </section>
      )}
      {change && (
        <section className="workspace-panel workspace-confirm">
          <h2>Xác nhận thay đổi</h2>
          <p>
            {change.label} →{" "}
            {change.kind === "lot"
              ? change.value
                ? "Mở tiếp nhận"
                : "Đóng tiếp nhận"
              : labels[change.value]}
          </p>
          <form className="workspace-form" onSubmit={save}>
            <label>
              Lý do
              <textarea
                required
                maxLength={255}
                disabled={busy}
                value={reason}
                onChange={(e) => setReason(e.target.value)}
              />
            </label>
            <div className="workspace-actions">
              <button
                className="btn btn--primary"
                disabled={busy || !reason.trim()}
              >
                Xác nhận thay đổi
              </button>
              <button
                type="button"
                className="btn btn--outline"
                disabled={busy}
                onClick={() => setChange(null)}
              >
                Hủy
              </button>
            </div>
          </form>
        </section>
      )}
      {selected && (
        <section className="workspace-panel">
          <div className="workspace-heading">
            <h2>Phiên #{selected}</h2>
            <button
              className="btn btn--outline"
              onClick={() => setSelected(null)}
            >
              Đóng chi tiết
            </button>
          </div>
          <Feedback loading={session.loading} error={session.error} />
          {session.data && (
            <dl className="workspace-detail">
              {Object.entries({
                "Biển số": session.data.plateNumber,
                Bãi: session.data.lotName,
                "Ô đỗ": session.data.slotCode || "Chờ xác minh",
                "Giờ vào": dateLabel(session.data.checkInTime),
                "Trạng thái": statusLabel(session.data.status),
              }).map(([k, v]) => (
                <div key={k}>
                  <dt>{k}</dt>
                  <dd>{v}</dd>
                </div>
              ))}
            </dl>
          )}
        </section>
      )}
    </>
  );
}
function ManagerContent() {
  const [params, setParams] = useSearchParams();
  const view = ["overview", "slots", "sessions"].includes(params.get("view"))
    ? params.get("view")
    : "overview";
  const [revision, setRevision] = useState(0);
  const [selectedLot, setSelectedLot] = useState("");
  const action = useMutation();
  const lots = useResource("/v1/staff-assignments/my-lots", revision);
  const assigned = itemsOf(lots.data);
  const lot =
    assigned.find((l) => String(l.lotId) === selectedLot) || assigned[0];
  return (
    <section className="workspace container">
      <header className="workspace-heading">
        <div>
          <span className="tag">Quản lý bãi</span>
          <h1>Điều hành bãi đỗ</h1>
          <p>
            {assigned.length} bãi được phân công · Theo dõi xe và quản lý trạng
            thái tiếp nhận.
          </p>
        </div>
        <button
          className="btn btn--outline"
          disabled={action.busy || lots.loading}
          onClick={() => setRevision((n) => n + 1)}
        >
          Làm mới dữ liệu
        </button>
      </header>
      <Feedback
        loading={lots.loading}
        error={lots.error || action.error}
        message={action.message}
      />
      {lots.error && (
        <button
          className="btn btn--outline"
          onClick={() => setRevision((n) => n + 1)}
        >
          Tải lại bãi
        </button>
      )}
      {!lots.loading && !lots.error && !assigned.length && (
        <section className="workspace-panel workspace-empty">
          <h2>Chưa có bãi được phân công</h2>
          <p>Liên hệ Admin để được phân công bãi trước khi vận hành.</p>
        </section>
      )}
      {lot && (
        <>
          <div className="workspace-panel">
            <div className="workspace-filters">
              <label>
                Bãi đang quản lý
                <select
                  value={lot.lotId}
                  disabled={action.busy}
                  onChange={(e) => setSelectedLot(e.target.value)}
                >
                  {assigned.map((l) => (
                    <option value={l.lotId} key={l.lotId}>
                      {l.lotName}
                      {!l.isActive ? " · Đóng tiếp nhận" : ""}
                    </option>
                  ))}
                </select>
              </label>
            </div>
            <nav className="workspace-tabs" aria-label="Điều hành bãi">
              {[
                ["overview", "Tổng quan"],
                ["slots", "Ô đỗ / bảo trì"],
                ["sessions", "Xe đang gửi"],
              ].map(([key, label]) => (
                <button
                  className="btn btn--outline"
                  key={key}
                  disabled={action.busy}
                  aria-pressed={view === key}
                  onClick={() => setParams({ view: key })}
                >
                  {label}
                </button>
              ))}
            </nav>
          </div>
          <LotWorkspace
            key={`${lot.lotId}-${view}`}
            lotId={lot.lotId}
            revision={revision}
            refresh={() => setRevision((n) => n + 1)}
            action={action}
            busy={action.busy}
            view={view}
          />
        </>
      )}
    </section>
  );
}
export default function ManagerWorkspace() {
  return (
    <RequireRole role="MANAGER">
      <ManagerContent />
    </RequireRole>
  );
}
