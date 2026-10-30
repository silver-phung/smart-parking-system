import { useCallback, useEffect, useRef, useState } from "react";
import {
  plateKey,
  slotIsSelectable,
  staffApi,
} from "../../services/staffApi.js";
import {
  formatTime,
  Notice,
  SafePhoto,
  StaffError,
  StaffIcon,
  StatusBadge,
  useStaffAction,
  useStaffQuery,
} from "./StaffUI.jsx";

const physicalLabels = {
  EMPTY: "Cảm biến: trống",
  OCCUPIED: "Cảm biến: có xe",
  MAINTENANCE: "Bảo trì",
  UNAVAILABLE: "Không khả dụng",
};

export default function StaffVerification({
  lot,
  revision,
  initialSessionId,
  onChanged,
  onBusyChange,
}) {
  const loader = useCallback(
    async (signal) => {
      const [sessions, layout, dispatches, unmatchedSlots] = await Promise.all([
        staffApi.activeSessions(lot.lotId, signal),
        staffApi.lotSlots(lot.lotId, signal),
        staffApi.openDispatches(lot.lotId, signal),
        staffApi.unmatchedSlots(lot.lotId, signal),
      ]);
      return { sessions, ...layout, dispatches, unmatchedSlots };
    },
    [lot.lotId, revision],
  );
  const resource = useStaffQuery(loader);
  const [selectedId, setSelectedId] = useState(initialSessionId || "");
  const [queueFilter, setQueueFilter] = useState("waiting");
  const [search, setSearch] = useState("");
  const [mode, setMode] = useState("verify");
  const [slotId, setSlotId] = useState("");
  const [floorId, setFloorId] = useState("");
  const [notes, setNotes] = useState("");
  const [checked, setChecked] = useState(false);
  const [message, setMessage] = useState("");
  const panelRef = useRef(null);
  const action = useStaffAction(onBusyChange);
  useEffect(() => {
    if (initialSessionId) {
      setSelectedId(String(initialSessionId));
      setQueueFilter("all");
    }
  }, [initialSessionId]);

  const data = resource.data;
  const session = data?.sessions.find(
    (item) => String(item.id) === String(selectedId),
  );
  const dispatch = data?.dispatches.find(
    (item) => item.sessionId === session?.id,
  );
  const slot = data?.slots.find((item) => String(item.id) === String(slotId));
  const queue = (data?.sessions || []).filter((item) => {
    if (item.status !== "ACTIVE") return false;
    const open = data.dispatches.find((row) => row.sessionId === item.id);
    const pending = !item.currentSlotId || Boolean(open);
    if (queueFilter === "waiting" && (!pending || open?.status === "ASSIGNED"))
      return false;
    if (queueFilter === "assigned" && open?.status !== "ASSIGNED") return false;
    return (
      !search ||
      plateKey(item.plateNumber).includes(plateKey(search)) ||
      String(item.id).includes(search.trim())
    );
  });
  const chooseSession = (item) => {
    setSelectedId(String(item.id));
    setSlotId(item.currentSlotId ? String(item.currentSlotId) : "");
    setFloorId("");
    setNotes("");
    setChecked(false);
    setMode("verify");
    setMessage("");
    action.setError(null);
  };
  const submit = (event) => {
    event.preventDefault();
    action.run(async () => {
      if (!session || session.status !== "ACTIVE")
        throw new Error("Chọn một phiên đang gửi để thực hiện xác minh.");
      if (!slot || !slotIsSelectable(slot, session, data.sessions, mode))
        throw new Error("Chọn ô hợp lệ, chưa được xe khác sử dụng.");
      if (!checked)
        throw new Error("Vui lòng xác nhận đã kiểm tra xe và vị trí thực tế.");
      const result = await staffApi.slotAction({
        mode,
        session,
        slotId: Number(slotId),
        notes: notes.trim(),
        dispatch,
      });
      setMessage(
        result.message || `Đã cập nhật vị trí xe ${session.plateNumber}.`,
      );
      setSelectedId("");
      setSlotId("");
      setNotes("");
      setChecked(false);
      onChanged();
    });
  };

  return (
    <div className="staff-side-stack">
      {message && <Notice kind="success">{message}</Notice>}
      <StaffError error={resource.error} />
      {resource.error && (
        <button
          className="staff-button staff-button--secondary"
          onClick={resource.reload}
        >
          Tải lại hàng chờ
        </button>
      )}
      {resource.loading && (
        <Notice>Đang tải xe chờ, danh sách ô và điều phối…</Notice>
      )}
      {data && (
        <>
          <div className="staff-verification-grid">
            <section className="staff-panel staff-queue">
              <div className="staff-panel-heading">
                <div>
                  <span className="staff-eyebrow">TẠI BÃI</span>
                  <h2>Hàng xe cần xử lý</h2>
                </div>
                <span className="staff-count">{queue.length}</span>
              </div>
              <label className="staff-field">
                Danh sách
                <select
                  value={queueFilter}
                  disabled={action.busy}
                  onChange={(event) => setQueueFilter(event.target.value)}
                >
                  <option value="waiting">Chờ phân / ghép ô</option>
                  <option value="assigned">Đã phân, chờ xác minh</option>
                  <option value="all">Tất cả xe đang gửi</option>
                </select>
              </label>
              <label className="staff-field">
                Tìm xe
                <input
                  type="search"
                  value={search}
                  onChange={(event) => setSearch(event.target.value)}
                  placeholder="Biển số hoặc mã phiên"
                  disabled={action.busy}
                />
              </label>
              <div className="staff-queue-list" aria-label="Hàng xe cần xử lý">
                {queue.length === 0 && (
                  <p className="staff-empty-small">
                    Không có xe phù hợp trong danh sách này.
                  </p>
                )}
                {queue.map((item) => {
                  const open = data.dispatches.find(
                    (row) => row.sessionId === item.id,
                  );
                  return (
                    <button
                      type="button"
                      className={`staff-queue-item ${String(selectedId) === String(item.id) ? "is-selected" : ""}`}
                      key={item.id}
                      disabled={action.busy}
                      onClick={() => chooseSession(item)}
                      aria-pressed={String(selectedId) === String(item.id)}
                    >
                      <span className="staff-queue-item-top">
                        <strong>{item.plateNumber}</strong>
                        <StaffIcon name="arrow" size={16} />
                      </span>
                      <span>
                        #{item.id} · Vào {formatTime(item.checkInTime)}
                      </span>
                      <span className="staff-queue-position">
                        {open
                          ? `Đã phân: ${open.assignedSlotCode}`
                          : item.slotCode
                            ? `Vị trí: ${item.slotCode}`
                            : "Chưa ghép ô"}
                      </span>
                    </button>
                  );
                })}
              </div>
            </section>
            <section className="staff-panel" ref={panelRef}>
              <div className="staff-panel-heading">
                <span className="staff-icon-tile">
                  <StaffIcon name="verify" size={24} />
                </span>
                <div>
                  <h2>Đối chiếu xe và vị trí</h2>
                  <p>Kiểm tra tại ô trước khi xác nhận.</p>
                </div>
              </div>
              {!session ? (
                <div className="staff-empty">
                  <StaffIcon name="verify" size={38} />
                  <h3>Chọn một xe để bắt đầu</h3>
                  <p>
                    Chọn xe ở danh sách bên cạnh để phân ô, xác minh hoặc ghi
                    nhận đỗ sai ô.
                  </p>
                </div>
              ) : session.status !== "ACTIVE" ? (
                <Notice kind="warning">
                  Phiên này không còn ở trạng thái có thể xác minh. Hãy chọn xe
                  khác hoặc làm mới.
                </Notice>
              ) : (
                <>
                  <div className="staff-verification-vehicle">
                    <SafePhoto
                      url={session.checkInImageUrl}
                      alt={`Ảnh vào của xe ${session.plateNumber}`}
                    />
                    <div>
                      <span className="staff-eyebrow">PHIÊN #{session.id}</span>
                      <h3 className="staff-plate">{session.plateNumber}</h3>
                      <p>Vào lúc {formatTime(session.checkInTime)}</p>
                      <StatusBadge
                        status={dispatch?.status || session.status}
                      />
                      {session.slotCode && (
                        <p>
                          Ô đang liên kết: <strong>{session.slotCode}</strong>
                        </p>
                      )}
                    </div>
                  </div>
                  <StaffError error={action.error} />
                  <form onSubmit={submit} aria-label="Xác minh vị trí xe">
                    <fieldset
                      className="staff-action-picker"
                      disabled={action.busy}
                    >
                      <legend>Thao tác cần thực hiện</legend>
                      {[
                        ["verify", "Xác minh tại ô"],
                        [
                          "assign",
                          dispatch ? "Đổi ô được phân" : "Phân ô mục tiêu",
                        ],
                        ["wrong", "Báo đỗ sai ô"],
                      ].map(([value, label]) => (
                        <label key={value}>
                          <input
                            type="radio"
                            name="slot-operation"
                            value={value}
                            checked={mode === value}
                            onChange={() => {
                              setMode(value);
                              setSlotId("");
                              setChecked(false);
                              action.setError(null);
                            }}
                          />
                          <span>{label}</span>
                        </label>
                      ))}
                    </fieldset>
                    <label className="staff-field">
                      Tầng
                      <select
                        value={floorId}
                        disabled={action.busy}
                        onChange={(event) => setFloorId(event.target.value)}
                      >
                        <option value="">Tất cả tầng</option>
                        {data.floors.map((floor) => (
                          <option key={floor.id} value={floor.id}>
                            {floor.name}
                          </option>
                        ))}
                      </select>
                    </label>
                    <div className="staff-slot-legend">
                      <span>
                        <i className="is-empty" /> Trống
                      </span>
                      <span>
                        <i className="is-occupied" /> Có xe
                      </span>
                      <span>
                        <i className="is-blocked" /> Không thể chọn
                      </span>
                    </div>
                    <div
                      className="staff-slot-picker"
                      aria-label="Chọn ô đỗ thực tế"
                    >
                      {data.slots
                        .filter(
                          (item) =>
                            !floorId || String(item.floorId) === floorId,
                        )
                        .map((item) => {
                          const available = slotIsSelectable(
                            item,
                            session,
                            data.sessions,
                            mode,
                          );
                          const linked = data.sessions.find(
                            (other) =>
                              other.currentSlotId === item.id &&
                              other.id !== session.id,
                          );
                          return (
                            <button
                              type="button"
                              key={item.id}
                              className={`staff-slot-choice staff-slot-choice--${String(item.physicalStatus).toLowerCase()} ${String(slotId) === String(item.id) ? "is-selected" : ""}`}
                              disabled={action.busy || !available}
                              aria-pressed={String(slotId) === String(item.id)}
                              onClick={() => {
                                setSlotId(String(item.id));
                                setChecked(false);
                              }}
                            >
                              <strong>{item.slotCode}</strong>
                              <span>{item.floorName}</span>
                              <small>
                                {linked
                                  ? `Đang gắn xe ${linked.plateNumber}`
                                  : !available
                                    ? "Chưa thể chọn"
                                    : physicalLabels[item.physicalStatus]}
                              </small>
                            </button>
                          );
                        })}
                    </div>
                    {data.slots.length === 0 && (
                      <Notice kind="warning">
                        Bãi chưa có ô hoạt động để lựa chọn. Liên hệ quản lý
                        bãi.
                      </Notice>
                    )}
                    {slot && (
                      <Notice
                        kind={
                          mode !== "assign" && slot.physicalStatus === "EMPTY"
                            ? "warning"
                            : "info"
                        }
                      >
                        Đã chọn <strong>{slot.slotCode}</strong> ·{" "}
                        {slot.floorName}.{" "}
                        {mode === "assign"
                          ? "Đây là ô mục tiêu, xe vẫn cần được xác minh tại vị trí thực tế."
                          : slot.physicalStatus === "EMPTY"
                            ? "Cảm biến đang báo trống. Kiểm tra lại xe tại ô trước khi xác nhận."
                            : "Đối chiếu biển số và xe tại vị trí này."}
                      </Notice>
                    )}
                    <label className="staff-field">
                      {mode === "wrong" ? "Lý do đỗ sai ô" : "Ghi chú"}
                      <textarea
                        value={notes}
                        onChange={(event) => setNotes(event.target.value)}
                        maxLength={mode === "assign" ? 255 : 500}
                        required={mode === "wrong"}
                        disabled={action.busy}
                        placeholder={
                          mode === "wrong"
                            ? "Mô tả vị trí thực tế và lý do điều chỉnh"
                            : "Thông tin bàn giao cho nhân viên tại bãi"
                        }
                      />
                    </label>
                    <label className="staff-check">
                      <input
                        type="checkbox"
                        checked={checked}
                        onChange={(event) => setChecked(event.target.checked)}
                        required
                        disabled={action.busy}
                      />
                      <span>
                        {mode === "assign"
                          ? "Đã kiểm tra ô mục tiêu phù hợp và chưa được xe khác sử dụng."
                          : "Đã đối chiếu xe, biển số và vị trí thực tế tại ô."}
                      </span>
                    </label>
                    <button
                      type="submit"
                      className="staff-button staff-button--primary staff-submit"
                      disabled={
                        action.busy ||
                        !slot ||
                        !checked ||
                        !slotIsSelectable(slot, session, data.sessions, mode)
                      }
                    >
                      <StaffIcon name="verify" />
                      {action.busy
                        ? "Đang cập nhật…"
                        : mode === "assign"
                          ? "Xác nhận phân ô"
                          : mode === "wrong"
                            ? "Ghi nhận xe đỗ sai ô"
                            : "Xác nhận xe tại ô"}
                    </button>
                  </form>
                </>
              )}
            </section>
          </div>
          <section className="staff-panel">
            <div className="staff-panel-heading">
              <div>
                <h2>Ô có xe chưa ghép phiên</h2>
                <p>Đối chiếu xe tại ô với một phiên đang gửi trước khi ghép.</p>
              </div>
              <span className="staff-count">
                {data.unmatchedSlots?.length || 0}
              </span>
            </div>
            {!data.unmatchedSlots?.length ? (
              <p className="staff-empty-small">
                Không có ô cần đối soát trong dữ liệu hiện tại.
              </p>
            ) : (
              <div className="staff-unmatched-grid">
                {data.unmatchedSlots.map((item) => (
                  <article key={item.slotId}>
                    <strong>{item.slotCode}</strong>
                    <span>Tầng {item.floorNumber} · Cảm biến báo có xe</span>
                    <button
                      className="staff-button staff-button--quiet"
                      disabled={action.busy || !session}
                      onClick={() => {
                        setSlotId(String(item.slotId));
                        setFloorId("");
                        setMode("verify");
                        setChecked(false);
                        panelRef.current?.scrollIntoView({
                          behavior: "smooth",
                          block: "start",
                        });
                      }}
                    >
                      {session ? "Đối soát ô này" : "Chọn xe ở hàng chờ trước"}
                    </button>
                  </article>
                ))}
              </div>
            )}
          </section>
        </>
      )}
    </div>
  );
}
