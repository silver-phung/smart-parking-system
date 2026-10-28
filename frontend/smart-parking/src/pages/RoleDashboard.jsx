import { useCallback, useEffect, useState } from "react";
import RequireRole from "../components/RequireRole.jsx";
import { apiRequest } from "../services/api.js";

const roleDetails = {
  ADMIN: {
    title: "Quản trị hệ thống",
    subtitle: "Tổng quan toàn hệ thống, bãi đỗ và phân công nhân sự.",
    scope: "Toàn hệ thống",
    features: [
      { title: "Quản lý bãi, tầng và ô đỗ", api: true, ui: false },
      { title: "Mở hoặc đóng tiếp nhận tại bãi", api: true, ui: false },
      { title: "Phân công Manager và Staff vào bãi", api: true, ui: false },
      { title: "Quản lý tài khoản và phân quyền", api: false, ui: false },
      { title: "Bảng giá, gói tháng, vé và thẻ", api: false, ui: false },
      { title: "Báo cáo doanh thu và lịch sử toàn hệ thống", api: false, ui: false },
    ],
  },
  MANAGER: {
    title: "Điều hành bãi đỗ",
    subtitle: "Quản lý vận hành trong phạm vi bãi được Admin phân công.",
    scope: "Chỉ các bãi được Admin phân công",
    features: [
      { title: "Theo dõi bãi được phân công", api: true, ui: true },
      { title: "Xem phiên đỗ đang hoạt động", api: true, ui: true },
      { title: "Mở hoặc đóng tiếp nhận tại bãi", api: true, ui: false },
      { title: "Cập nhật trạng thái bảo trì ô đỗ", api: true, ui: false },
      { title: "Cấp và quản lý tài khoản STAFF", api: false, ui: false },
      { title: "Quản lý đặt chỗ và thẻ tháng", api: false, ui: false },
      { title: "Thu tiền, hoàn tiền và báo cáo", api: false, ui: false },
    ],
  },
  STAFF: {
    title: "Vận hành bãi đỗ",
    subtitle: "Theo dõi bãi được giao và các phiên xe đang gửi.",
    scope: "Chỉ các bãi được Admin phân công",
    features: [
      { title: "Theo dõi bãi được phân công", api: true, ui: true },
      { title: "Tra cứu phiên đỗ đang hoạt động", api: true, ui: true },
      { title: "Ghi nhận xe vào và xe ra", api: true, ui: false },
      { title: "Điều phối xe vào ô đỗ", api: true, ui: false },
      { title: "Quản lý vé, thẻ và đặt chỗ", api: false, ui: false },
      { title: "Tính phí và ghi nhận thanh toán", api: false, ui: false },
    ],
  },
};

function getItems(response) {
  const data = response?.data;
  return Array.isArray(data) ? data : data?.items || [];
}

function getData(response) {
  return response?.data ?? null;
}

function getErrorMessage(reason) {
  return reason instanceof Error ? reason.message : String(reason);
}

function DashboardContent({ role }) {
  const details = roleDetails[role];
  const [lots, setLots] = useState([]);
  const [lotCount, setLotCount] = useState(0);
  const [assignments, setAssignments] = useState([]);
  const [assignmentCount, setAssignmentCount] = useState(0);
  const [sessions, setSessions] = useState([]);
  const [sessionCount, setSessionCount] = useState(0);
  const [loading, setLoading] = useState(true);
  const [lotsError, setLotsError] = useState("");
  const [assignmentsError, setAssignmentsError] = useState("");
  const [sessionsError, setSessionsError] = useState("");
  const [lastUpdated, setLastUpdated] = useState(null);

  const loadDashboard = useCallback(async () => {
    setLoading(true);
    setLotsError("");
    setAssignmentsError("");
    setSessionsError("");

    const lotsRequest = role === "ADMIN"
      ? apiRequest("/v1/parking-lots?pageNumber=1&pageSize=100")
      : apiRequest("/v1/staff-assignments/my-lots");
    const assignmentsRequest = role === "ADMIN"
      ? apiRequest("/v1/staff-assignments?page=1&pageSize=100")
      : Promise.resolve(null);
    const [lotsResult, assignmentsResult, sessionsResult] = await Promise.allSettled([
      lotsRequest,
      assignmentsRequest,
      apiRequest("/v1/parking-sessions/active?page=1&pageSize=100"),
    ]);

    if (lotsResult.status === "fulfilled") {
      if (role === "ADMIN") {
        setLots(getItems(lotsResult.value));
        setLotCount(lotsResult.value?.data?.totalCount ?? getItems(lotsResult.value).length);
      } else {
        const assignedLots = getItems(lotsResult.value);
        setAssignments(assignedLots);
        setAssignmentCount(assignedLots.length);
        const detailResults = await Promise.allSettled(
          assignedLots.map((lot) => apiRequest(`/v1/parking-lots/${lot.lotId}`))
        );
        const lotDetails = detailResults
          .filter((result) => result.status === "fulfilled")
          .map((result) => getData(result.value))
          .filter(Boolean);
        setLots(lotDetails);
        const failures = detailResults.filter((result) => result.status === "rejected");
        if (failures.length > 0) {
          setLotsError("Không thể tải chi tiết một số bãi được phân công.");
        }
      }
    } else {
      setLots([]);
      setLotCount(0);
      setLotsError(getErrorMessage(lotsResult.reason));
    }

    if (role === "ADMIN" && assignmentsResult.status === "fulfilled") {
      setAssignments(getItems(assignmentsResult.value));
      setAssignmentCount(
        assignmentsResult.value?.data?.totalCount ?? getItems(assignmentsResult.value).length
      );
    } else if (assignmentsResult.status === "rejected") {
      setAssignmentsError(getErrorMessage(assignmentsResult.reason));
    }

    if (sessionsResult.status === "fulfilled") {
      setSessions(getItems(sessionsResult.value));
      setSessionCount(
        sessionsResult.value?.data?.totalCount ?? getItems(sessionsResult.value).length
      );
    } else {
      setSessions([]);
      setSessionCount(0);
      setSessionsError(getErrorMessage(sessionsResult.reason));
    }

    setLastUpdated(new Date());
    setLoading(false);
  }, [role]);

  useEffect(() => {
    void loadDashboard();
  }, [loadDashboard]);

  const assignedLotIds = new Set(assignments.map((lot) => String(lot.lotId)));
  const activeLots = lots.filter((lot) => lot.isActive).length;
  const totalCapacity = lots.reduce((total, lot) => total + (lot.totalCapacity || 0), 0);
  const availableSlots = lots.reduce((total, lot) => total + (lot.availableSlotsCount || 0), 0);
  const occupiedSlots = lots.reduce((total, lot) => total + (lot.occupiedSlotsCount || 0), 0);
  const roleLabel = role === "ADMIN" ? "Quản trị viên" : role === "MANAGER" ? "Quản lý bãi" : "Nhân viên bãi";

  return (
    <section className="customer-page container">
      <div className="customer-page__heading role-dashboard__heading">
        <div>
          <span className="tag">{roleLabel} · {details.scope}</span>
          <h1>{details.title}</h1>
          <p className="muted">{details.subtitle}</p>
        </div>
        <button
          type="button"
          className="btn btn--outline"
          onClick={() => void loadDashboard()}
          disabled={loading}
        >
          {loading ? "Đang cập nhật..." : "Làm mới dữ liệu"}
        </button>
      </div>

      <div className="role-dashboard__stats" aria-label="Tổng quan vận hành">
        <div><strong>{loading ? "—" : lotCount}</strong><span>Bãi trong phạm vi</span></div>
        <div><strong>{loading ? "—" : activeLots}</strong><span>Bãi đang mở</span></div>
        <div><strong>{loading ? "—" : availableSlots}</strong><span>Chỗ trống</span></div>
        <div><strong>{loading ? "—" : occupiedSlots}</strong><span>Đang có xe</span></div>
        <div><strong>{loading ? "—" : sessionCount}</strong><span>Phiên đang hoạt động</span></div>
        {role === "ADMIN" && (
          <div><strong>{loading ? "—" : assignmentCount}</strong><span>Phân công nhân sự</span></div>
        )}
      </div>

      {lastUpdated && (
        <p className="role-dashboard__updated">
          Dữ liệu cập nhật lúc {lastUpdated.toLocaleTimeString("vi-VN")}
        </p>
      )}
      {lotsError && <p className="customer-message customer-message--error" role="alert">{lotsError}</p>}
      {assignmentsError && <p className="customer-message customer-message--error" role="alert">{assignmentsError}</p>}
      {sessionsError && <p className="customer-message customer-message--error" role="alert">{sessionsError}</p>}

      <div className="customer-card">
        <div className="customer-card__heading">
          <div>
            <h2>{role === "ADMIN" ? "Tổng quan bãi đỗ" : "Bãi được phân công"}</h2>
            <p className="muted">
              {role === "ADMIN"
                ? "Số liệu được tải từ API quản lý bãi, không gồm dữ liệu giả."
                : "Chỉ hiển thị bãi thuộc phạm vi tài khoản được phân công."}
            </p>
          </div>
        </div>

        {!loading && !lotsError && lots.length === 0 && (
          <p className="customer-message">
            {role === "ADMIN"
              ? "Chưa có dữ liệu bãi đỗ."
              : "Tài khoản hiện chưa được phân công bãi nào."}
          </p>
        )}

        {lots.length > 0 && (
          <div className="role-dashboard__lot-grid">
            {lots.map((lot) => (
              <article className="role-dashboard__lot" key={lot.id || lot.lotId}>
                <div className="role-dashboard__lot-heading">
                  <div>
                    <h3>{lot.name || lot.lotName}</h3>
                    <p>{lot.address || lot.lotAddress || "Chưa có địa chỉ"}</p>
                  </div>
                  <span className={lot.isActive ? "role-dashboard__badge is-active" : "role-dashboard__badge"}>
                    {lot.isActive ? "Đang mở" : "Đang đóng"}
                  </span>
                </div>
                <div className="role-dashboard__lot-stats">
                  <span>{lot.availableSlotsCount ?? "—"} chỗ trống</span>
                  <span>{lot.occupiedSlotsCount ?? "—"} đang có xe</span>
                  <span>{lot.totalCapacity ?? "—"} sức chứa</span>
                  {lot.totalFloors !== undefined && <span>{lot.totalFloors} tầng</span>}
                </div>
              </article>
            ))}
          </div>
        )}

        <p className="role-dashboard__scope-note">
          Tổng sức chứa trong phạm vi: {loading ? "—" : totalCapacity} chỗ.
          {role !== "ADMIN" && assignedLotIds.size > 0 &&
            ` Đã xác thực ${assignedLotIds.size} bãi được phân công từ API.`}
        </p>
      </div>

      {role === "MANAGER" && (
        <div className="customer-card manager-permissions">
          <div className="customer-card__heading">
            <div>
              <h2>Quyền hạn Manager</h2>
              <p className="muted">
                Quyền của Manager giới hạn theo bãi được Admin phân công và thấp hơn quyền Admin.
              </p>
            </div>
          </div>
          <div className="manager-permissions__grid">
            <div>
              <span className="manager-permissions__label">Được quản lý</span>
              <strong>Vận hành các bãi được giao</strong>
              <p>Xem tình trạng bãi và phiên xe trong phạm vi phân công.</p>
            </div>
            <div>
              <span className="manager-permissions__label">Không có quyền</span>
              <strong>Quản trị toàn hệ thống</strong>
              <p>Không tạo bãi, không phân quyền Manager/Admin và không truy cập bãi ngoài phạm vi.</p>
            </div>
          </div>
        </div>
      )}

      {role === "MANAGER" && (
        <div className="customer-card manager-staff">
          <div className="customer-card__heading">
            <div>
              <h2>Tài khoản nhân viên STAFF</h2>
              <p className="muted">
                Tài khoản nhân viên cần được Admin cấp và gắn với bãi thuộc phạm vi quản lý.
              </p>
            </div>
            <span className="role-dashboard__feature-status">Chưa có API</span>
          </div>
          <p className="customer-message customer-message--notice">
            Tài khoản nhân viên và phân công bãi do Admin quản lý. Vui lòng liên hệ Admin khi cần cấp hoặc thay đổi tài khoản.
          </p>
        </div>
      )}

      <div className="customer-card">
        <div className="customer-card__heading">
          <div>
            <h2>Phiên đỗ đang hoạt động</h2>
            <p className="muted">
              {sessionCount} phiên đang hoạt động trong phạm vi tài khoản.
              {sessionCount > sessions.length && ` Đang hiển thị ${sessions.length} phiên gần nhất.`}
            </p>
          </div>
        </div>
        {!loading && !sessionsError && sessions.length === 0 && (
          <p className="customer-message">Hiện không có phiên đỗ xe đang hoạt động.</p>
        )}
        {sessions.length > 0 && (
          <div className="role-dashboard__table-wrap">
            <table className="role-dashboard__table">
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
      </div>

      <div className="customer-card">
        <div className="customer-card__heading">
          <div>
            <h2>Chức năng theo vai trò</h2>
            <p className="muted">
              Chỉ phần có API và giao diện tương ứng mới được coi là có thể thao tác.
            </p>
          </div>
        </div>
        <div className="role-dashboard__features">
          {details.features.map((feature) => (
            <article className="role-dashboard__feature" key={feature.title}>
              <div>
                <h3>{feature.title}</h3>
                <p>
                  {feature.ui
                    ? "Có thể xem tại dashboard này."
                    : feature.api
                      ? "API đã có; giao diện thao tác chưa được xây dựng."
                      : "Chưa có API trong backend hiện tại."}
                </p>
              </div>
              <span className={`role-dashboard__feature-status ${feature.ui ? "is-ready" : ""}`}>
                {feature.ui ? "Đang dùng" : feature.api ? "Chưa có giao diện" : "Chưa hỗ trợ"}
              </span>
            </article>
          ))}
        </div>
      </div>
    </section>
  );
}

export default function RoleDashboard({ role }) {
  return (
    <RequireRole role={role}>
      <DashboardContent role={role} />
    </RequireRole>
  );
}
