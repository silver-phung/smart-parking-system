import { useCallback, useEffect, useMemo, useState } from "react";
import { apiRequest } from "../services/api.js";
import RequireRole from "../components/RequireRole.jsx";
import Viewer3D from "./Viewer3D.jsx";

function getItems(response) {
  const data = response?.data;
  return Array.isArray(data) ? data : data?.items || [];
}

function getErrorMessage(reason) {
  return reason instanceof Error ? reason.message : String(reason);
}

function formatNumber(value) {
  return new Intl.NumberFormat("vi-VN").format(value);
}

function AdminDashboardContent() {
  const [lots, setLots] = useState([]);
  const [lotCount, setLotCount] = useState(0);
  const [assignmentCount, setAssignmentCount] = useState(0);
  const [sessions, setSessions] = useState([]);
  const [sessionCount, setSessionCount] = useState(0);
  const [loading, setLoading] = useState(true);
  const [lotsError, setLotsError] = useState("");
  const [assignmentsError, setAssignmentsError] = useState("");
  const [sessionsError, setSessionsError] = useState("");
  const [lastUpdated, setLastUpdated] = useState(null);

  const refresh = useCallback(async () => {
    setLoading(true);
    setLotsError("");
    setAssignmentsError("");
    setSessionsError("");

    const [lotsResult, assignmentsResult, sessionsResult] = await Promise.allSettled([
      apiRequest("/v1/parking-lots?pageNumber=1&pageSize=100"),
      apiRequest("/v1/staff-assignments?page=1&pageSize=100"),
      apiRequest("/v1/parking-sessions/active?page=1&pageSize=100"),
    ]);

    if (lotsResult.status === "fulfilled") {
      const resultLots = getItems(lotsResult.value);
      setLots(resultLots);
      setLotCount(lotsResult.value?.data?.totalCount ?? resultLots.length);
    } else {
      setLots([]);
      setLotCount(0);
      setLotsError(getErrorMessage(lotsResult.reason));
    }

    if (assignmentsResult.status === "fulfilled") {
      const resultAssignments = getItems(assignmentsResult.value);
      setAssignmentCount(
        assignmentsResult.value?.data?.totalCount ?? resultAssignments.length
      );
    } else {
      setAssignmentCount(0);
      setAssignmentsError(getErrorMessage(assignmentsResult.reason));
    }

    if (sessionsResult.status === "fulfilled") {
      const resultSessions = getItems(sessionsResult.value);
      setSessions(resultSessions);
      setSessionCount(
        sessionsResult.value?.data?.totalCount ?? resultSessions.length
      );
    } else {
      setSessions([]);
      setSessionCount(0);
      setSessionsError(getErrorMessage(sessionsResult.reason));
    }

    setLastUpdated(new Date());
    setLoading(false);
  }, []);

  useEffect(() => {
    void refresh();
  }, [refresh]);

  const summary = useMemo(() => {
    const capacity = lots.reduce((sum, lot) => sum + (lot.totalCapacity || 0), 0);
    const occupied = lots.reduce((sum, lot) => sum + (lot.occupiedSlotsCount || 0), 0);
    const available = lots.reduce((sum, lot) => sum + (lot.availableSlotsCount || 0), 0);

    return {
      capacity,
      occupied,
      available,
      openLots: lots.filter((lot) => lot.isActive).length,
      occupancy: capacity > 0 ? Math.min(100, Math.round((occupied / capacity) * 100)) : 0,
    };
  }, [lots]);

  return (
    <section className="admin-dashboard container" id="overview">
      <header className="admin-dashboard__header">
        <div>
          <span className="admin-dashboard__eyebrow">BẢNG ĐIỀU KHIỂN ADMIN</span>
          <h1>Tổng quan vận hành</h1>
          <p>Theo dõi tình trạng các bãi đỗ và hoạt động đang diễn ra trên toàn hệ thống.</p>
        </div>
        <button
          type="button"
          className="btn btn--outline"
          onClick={() => void refresh()}
          disabled={loading}
        >
          {loading ? "Đang cập nhật..." : "Làm mới dữ liệu"}
        </button>
      </header>

      <div className="admin-dashboard__meta">
        <span>Phạm vi: toàn hệ thống</span>
        {lastUpdated && <span>Cập nhật lúc {lastUpdated.toLocaleTimeString("vi-VN")}</span>}
      </div>

      {lotsError && <p className="customer-message customer-message--error" role="alert">{lotsError}</p>}
      {assignmentsError && <p className="customer-message customer-message--error" role="alert">{assignmentsError}</p>}
      {sessionsError && <p className="customer-message customer-message--error" role="alert">{sessionsError}</p>}

      <div className="admin-dashboard__metrics" aria-label="Chỉ số vận hành">
        <article className="admin-dashboard__metric">
          <span>Tổng số bãi</span>
          <strong>{loading ? "—" : formatNumber(lotCount)}</strong>
          <small>{loading ? "Đang tải" : `${summary.openLots} bãi đang hoạt động`}</small>
        </article>
        <article className="admin-dashboard__metric">
          <span>Tổng sức chứa</span>
          <strong>{loading ? "—" : formatNumber(summary.capacity)}</strong>
          <small>Chỗ đỗ trên các bãi đã tải</small>
        </article>
        <article className="admin-dashboard__metric">
          <span>Đang có xe</span>
          <strong>{loading ? "—" : formatNumber(summary.occupied)}</strong>
          <small>{loading ? "—" : `${summary.occupancy}% công suất hiện tại`}</small>
        </article>
        <article className="admin-dashboard__metric">
          <span>Chỗ còn trống</span>
          <strong>{loading ? "—" : formatNumber(summary.available)}</strong>
          <small>Số liệu hiện tại từ API bãi đỗ</small>
        </article>
        <article className="admin-dashboard__metric">
          <span>Phiên đang hoạt động</span>
          <strong>{loading ? "—" : formatNumber(sessionCount)}</strong>
          <small>Xe đang gửi tại các bãi</small>
        </article>
        <article className="admin-dashboard__metric">
          <span>Phân công nhân sự</span>
          <strong>{loading ? "—" : formatNumber(assignmentCount)}</strong>
          <small>Danh sách phân công từ API</small>
        </article>
      </div>

      <div className="admin-dashboard__columns">
        <section className="admin-dashboard__panel admin-dashboard__panel--wide" id="parking-lots">
          <div className="admin-dashboard__panel-heading">
            <div>
              <h2>Tiến độ và công suất bãi đỗ</h2>
              <p>Tỷ lệ xe đang đỗ so với tổng sức chứa hiện tại của từng bãi.</p>
            </div>
            {lotCount > lots.length && (
              <span className="admin-dashboard__hint">
                Đang hiển thị {lots.length}/{lotCount} bãi
              </span>
            )}
          </div>

          {!loading && !lotsError && lots.length === 0 && (
            <p className="customer-message">Chưa có dữ liệu bãi đỗ.</p>
          )}

          <div className="admin-dashboard__lot-list">
            {lots.map((lot) => {
              const capacity = lot.totalCapacity || 0;
              const occupied = lot.occupiedSlotsCount || 0;
              const percent = capacity > 0 ? Math.min(100, Math.round((occupied / capacity) * 100)) : 0;

              return (
                <article className="admin-dashboard__lot" key={lot.id}>
                  <div className="admin-dashboard__lot-heading">
                    <div>
                      <h3>{lot.name}</h3>
                      <p>{lot.address || "Chưa cập nhật địa chỉ"}</p>
                    </div>
                    <span className={lot.isActive ? "admin-dashboard__status is-open" : "admin-dashboard__status"}>
                      {lot.isActive ? "Đang mở" : "Đang đóng"}
                    </span>
                  </div>
                  <div
                    className="admin-dashboard__progress"
                    role="progressbar"
                    aria-label={`Công suất ${lot.name}`}
                    aria-valuemin="0"
                    aria-valuemax="100"
                    aria-valuenow={percent}
                  >
                    <span style={{ width: `${percent}%` }} />
                  </div>
                  <div className="admin-dashboard__lot-meta">
                    <span>{formatNumber(occupied)} đang đỗ · {percent}% công suất</span>
                    <span>{formatNumber(lot.availableSlotsCount || 0)} trống / {formatNumber(capacity)} chỗ</span>
                  </div>
                </article>
              );
            })}
          </div>
        </section>

        <section className="admin-dashboard__panel" id="finance">
          <div className="admin-dashboard__panel-heading">
            <div>
              <h2>Tài chính và hóa đơn</h2>
              <p>Doanh thu, chi phí và xuất hóa đơn.</p>
            </div>
          </div>
          <div className="admin-dashboard__unavailable">
            <strong>Chưa có dữ liệu tài chính</strong>
            <p>
              Backend hiện chưa cung cấp API hóa đơn, doanh thu hoặc chi tiêu.
              Các số liệu này sẽ không được ước tính từ phiên đỗ.
            </p>
            <button type="button" className="btn btn--outline" disabled>
              Xuất hóa đơn · Chưa có API
            </button>
          </div>
        </section>

        <section className="admin-dashboard__panel" id="trends">
          <div className="admin-dashboard__panel-heading">
            <div>
              <h2>Biến động theo ngày / tháng</h2>
              <p>Số lượt xe và doanh thu theo thời gian.</p>
            </div>
          </div>
          <div className="admin-dashboard__chart-empty">
            <span className="admin-dashboard__chart-placeholder" aria-hidden="true">API BÁO CÁO</span>
            <strong>Chưa thể tạo biểu đồ lịch sử</strong>
            <p>
              API hiện chỉ trả về phiên đang hoạt động, chưa có lịch sử phiên hoặc báo cáo tổng hợp theo ngày/tháng.
            </p>
          </div>
        </section>
      </div>

      <section className="admin-dashboard__panel" id="active-sessions">
        <div className="admin-dashboard__panel-heading">
          <div>
            <h2>Xe đang gửi tại bãi</h2>
            <p>
              {sessionCount} phiên đang hoạt động.
              {sessionCount > sessions.length && ` Hiển thị ${sessions.length} phiên gần nhất.`}
            </p>
          </div>
        </div>
        {!loading && !sessionsError && sessions.length === 0 && (
          <p className="customer-message">Hiện không có phiên đỗ xe đang hoạt động.</p>
        )}
        {sessions.length > 0 && (
          <div className="admin-dashboard__table-wrap">
            <table className="admin-dashboard__table">
              <thead>
                <tr>
                  <th>Mã phiên</th>
                  <th>Biển số</th>
                  <th>Bãi đỗ</th>
                  <th>Vị trí</th>
                  <th>Trạng thái</th>
                  <th>Giờ vào</th>
                </tr>
              </thead>
              <tbody>
                {sessions.map((session) => (
                  <tr key={session.id}>
                    <td>#{session.id}</td>
                    <td>{session.plateNumber || "—"}</td>
                    <td>{session.lotName || "—"}</td>
                    <td>{session.slotCode || "Chưa gán"}</td>
                    <td>{session.status || "—"}</td>
                    <td>
                      {session.checkInTime
                        ? new Date(session.checkInTime).toLocaleString("vi-VN")
                        : "—"}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </section>
      <Viewer3D embedded />
    </section>
  );
}

export default function AdminDashboard() {
  return (
    <RequireRole role="ADMIN">
      <AdminDashboardContent />
    </RequireRole>
  );
}
