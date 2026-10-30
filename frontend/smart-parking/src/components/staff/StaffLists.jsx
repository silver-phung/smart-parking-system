import { useCallback, useState } from "react";
import { staffApi } from "../../services/staffApi.js";
import {
  formatTime,
  Notice,
  Pagination,
  SessionInfo,
  StaffError,
  StatusBadge,
  useStaffAction,
  useStaffQuery,
} from "./StaffUI.jsx";

export function StaffSessions({ lot, revision, onOpen }) {
  const [page, setPage] = useState(1);
  const [search, setSearch] = useState("");
  const [filter, setFilter] = useState("");
  const [selectedId, setSelectedId] = useState(null);
  const loader = useCallback(
    (signal) => staffApi.activePage(lot.lotId, page, filter, signal),
    [lot.lotId, page, filter, revision],
  );
  const resource = useStaffQuery(loader);
  const detailLoader = useCallback(
    (signal) => (selectedId ? staffApi.session(selectedId, signal) : null),
    [selectedId, revision],
  );
  const detail = useStaffQuery(detailLoader);
  return (
    <div className="staff-side-stack">
      <section className="staff-panel">
        <div className="staff-panel-heading">
          <div>
            <h2>Xe đang gửi tại bãi</h2>
            <p>Tra cứu phiên đang hoạt động và đã thanh toán, chưa ra bãi.</p>
          </div>
        </div>
        <form
          className="staff-search-form staff-search-form--compact"
          onSubmit={(event) => {
            event.preventDefault();
            setPage(1);
            setFilter(search.trim());
            setSelectedId(null);
          }}
        >
          <label className="staff-field">
            Biển số
            <input
              type="search"
              value={search}
              onChange={(event) => setSearch(event.target.value)}
              placeholder="Tìm theo biển số"
            />
          </label>
          <button
            type="submit"
            className="staff-button staff-button--secondary"
          >
            Tìm kiếm
          </button>
        </form>
        <StaffError error={resource.error} />
        {resource.error && (
          <button
            className="staff-button staff-button--quiet"
            onClick={resource.reload}
          >
            Thử lại
          </button>
        )}
        {resource.loading && <Notice>Đang tải danh sách xe…</Notice>}
        {resource.data && (
          <>
            {!resource.data.items.length ? (
              <div className="staff-empty-small">
                Không có phiên xe phù hợp tại bãi này.
              </div>
            ) : (
              <div className="staff-table-wrap">
                <table className="staff-table">
                  <thead>
                    <tr>
                      <th>Xe / Phiên</th>
                      <th>Giờ vào</th>
                      <th>Vị trí</th>
                      <th>Trạng thái</th>
                      <th>Thao tác</th>
                    </tr>
                  </thead>
                  <tbody>
                    {resource.data.items.map((item) => (
                      <tr key={item.id}>
                        <td data-label="Xe / Phiên">
                          <strong>{item.plateNumber}</strong>
                          <small>#{item.id}</small>
                        </td>
                        <td data-label="Giờ vào">
                          {formatTime(item.checkInTime)}
                        </td>
                        <td data-label="Vị trí">
                          {item.slotCode || "Chưa ghép ô"}
                        </td>
                        <td data-label="Trạng thái">
                          <StatusBadge status={item.status} />
                        </td>
                        <td data-label="Thao tác">
                          <div className="staff-inline-actions">
                            <button
                              type="button"
                              className="staff-text-button"
                              onClick={() => setSelectedId(item.id)}
                            >
                              Chi tiết
                            </button>
                            <button
                              type="button"
                              className="staff-button staff-button--quiet"
                              onClick={() => onOpen("exit", item.id)}
                            >
                              Cổng ra
                            </button>
                          </div>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
            <Pagination
              page={page}
              totalPages={resource.data.totalPages}
              totalCount={resource.data.totalCount}
              onChange={setPage}
              busy={resource.loading}
            />
          </>
        )}
      </section>
      {selectedId && (
        <section className="staff-panel" aria-label="Chi tiết phiên xe">
          <div className="staff-panel-heading">
            <h2>Chi tiết phiên #{selectedId}</h2>
            <button
              className="staff-text-button"
              onClick={() => setSelectedId(null)}
            >
              Đóng chi tiết
            </button>
          </div>
          <StaffError error={detail.error} />
          {detail.loading && <Notice>Đang tải chi tiết phiên…</Notice>}
          {detail.data && (
            <>
              <SessionInfo session={detail.data} />
              <div className="staff-inline-actions">
                {detail.data.status === "ACTIVE" && (
                  <button
                    className="staff-button staff-button--secondary"
                    onClick={() => onOpen("verify", detail.data.id)}
                  >
                    Xác minh vị trí
                  </button>
                )}
                <button
                  className="staff-button staff-button--primary"
                  onClick={() => onOpen("exit", detail.data.id)}
                >
                  Mở tại cổng ra
                </button>
              </div>
            </>
          )}
        </section>
      )}
    </div>
  );
}

export function StaffDispatches({
  lot,
  revision,
  onChanged,
  onOpen,
  onBusyChange,
}) {
  const [page, setPage] = useState(1);
  const [status, setStatus] = useState("");
  const [cancel, setCancel] = useState(null);
  const [reason, setReason] = useState("");
  const [message, setMessage] = useState("");
  const action = useStaffAction(onBusyChange);
  const loader = useCallback(
    (signal) => staffApi.dispatchPage(lot.lotId, page, status, signal),
    [lot.lotId, page, status, revision],
  );
  const resource = useStaffQuery(loader);
  const submitCancel = (event) => {
    event.preventDefault();
    action.run(async () => {
      if (!reason.trim())
        throw new Error(
          "Nhập lý do hủy điều phối để lưu lại thông tin bàn giao.",
        );
      const result = await staffApi.cancelDispatch(cancel.id, reason.trim());
      setMessage(result.message || "Đã hủy điều phối.");
      setCancel(null);
      setReason("");
      onChanged();
    });
  };
  return (
    <section className="staff-panel">
      <div className="staff-panel-heading">
        <div>
          <h2>Theo dõi điều phối</h2>
          <p>Giữ lại kết quả đã xác minh, báo sai ô và hủy để tra cứu.</p>
        </div>
        <label className="staff-field">
          Trạng thái
          <select
            value={status}
            disabled={action.busy}
            onChange={(event) => {
              setStatus(event.target.value);
              setPage(1);
              setCancel(null);
            }}
          >
            <option value="">Tất cả</option>
            <option value="ASSIGNED">Chờ xác minh</option>
            <option value="OCCUPIED">Đã xác minh</option>
            <option value="WRONG_SLOT">Ghi nhận sai ô</option>
            <option value="CANCELLED">Đã hủy</option>
          </select>
        </label>
      </div>
      {message && <Notice kind="success">{message}</Notice>}
      <StaffError error={resource.error} />
      {resource.error && (
        <button
          className="staff-button staff-button--quiet"
          onClick={resource.reload}
        >
          Tải lại điều phối
        </button>
      )}
      {cancel && (
        <form
          className="staff-cancel-form"
          onSubmit={submitCancel}
          aria-label="Hủy điều phối"
        >
          <h3>
            Hủy điều phối #{cancel.id} · {cancel.plateNumber}
          </h3>
          <p>
            Ô được phân: {cancel.assignedSlotCode}. Kiểm tra xe trước khi thu
            hồi điều phối.
          </p>
          <label className="staff-field">
            Lý do hủy
            <textarea
              value={reason}
              onChange={(event) => setReason(event.target.value)}
              required
              maxLength={255}
              disabled={action.busy}
            />
          </label>
          <StaffError error={action.error} />
          <div className="staff-inline-actions">
            <button
              type="submit"
              className="staff-button staff-button--danger"
              disabled={action.busy}
            >
              {action.busy ? "Đang hủy…" : "Xác nhận hủy điều phối"}
            </button>
            <button
              type="button"
              className="staff-button staff-button--quiet"
              disabled={action.busy}
              onClick={() => setCancel(null)}
            >
              Giữ điều phối
            </button>
          </div>
        </form>
      )}
      {resource.loading && <Notice>Đang tải điều phối…</Notice>}
      {resource.data && (
        <>
          {!resource.data.items.length ? (
            <p className="staff-empty-small">
              Chưa có điều phối phù hợp tại bãi này.
            </p>
          ) : (
            <div className="staff-table-wrap">
              <table className="staff-table">
                <thead>
                  <tr>
                    <th>Xe / Điều phối</th>
                    <th>Ô mục tiêu</th>
                    <th>Ô thực tế</th>
                    <th>Trạng thái</th>
                    <th>Thao tác</th>
                  </tr>
                </thead>
                <tbody>
                  {resource.data.items.map((item) => (
                    <tr key={item.id}>
                      <td data-label="Xe / Điều phối">
                        <strong>{item.plateNumber}</strong>
                        <small>
                          Điều phối #{item.id} · Phiên #{item.sessionId}
                        </small>
                      </td>
                      <td data-label="Ô mục tiêu">{item.assignedSlotCode}</td>
                      <td data-label="Ô thực tế">
                        {item.actualSlotCode || "Chưa xác minh"}
                        <small>
                          {formatTime(item.confirmedAt || item.dispatchedAt)}
                        </small>
                        {item.notes && (
                          <small className="staff-row-note">{item.notes}</small>
                        )}
                      </td>
                      <td data-label="Trạng thái">
                        <StatusBadge status={item.status} />
                      </td>
                      <td data-label="Thao tác">
                        {["PENDING", "ASSIGNED"].includes(item.status) ? (
                          <div className="staff-inline-actions">
                            <button
                              type="button"
                              className="staff-button staff-button--quiet"
                              disabled={action.busy}
                              onClick={() => onOpen("verify", item.sessionId)}
                            >
                              Xử lý
                            </button>
                            <button
                              type="button"
                              className="staff-text-button is-danger"
                              disabled={action.busy}
                              onClick={() => {
                                setCancel(item);
                                setReason("");
                                action.setError(null);
                              }}
                            >
                              Hủy
                            </button>
                          </div>
                        ) : (
                          <span className="staff-hint">Đã lưu kết quả</span>
                        )}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
          <Pagination
            page={page}
            totalPages={resource.data.totalPages}
            totalCount={resource.data.totalCount}
            onChange={(value) => {
              setPage(value);
              setCancel(null);
            }}
            busy={action.busy || resource.loading}
          />
        </>
      )}
    </section>
  );
}
