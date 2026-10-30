import { useState } from "react";
import { Link, useSearchParams } from "react-router-dom";
import RequireRole from "../components/RequireRole.jsx";
import {
  Feedback,
  Pager,
  dateLabel,
  itemsOf,
  query,
  useMutation,
  useResource,
  write,
} from "../components/WorkspaceUI.jsx";

const roles = ["USER", "STAFF", "MANAGER", "ADMIN"];
const duties = {
  GATE: "Trực cổng",
  SLOT_VERIFIER: "Xác minh ô",
  BOTH: "Cổng và xác minh ô",
};
const emptyUser = {
  username: "",
  email: "",
  password: "",
  fullName: "",
  phoneNumber: "",
  role: "STAFF",
};

function People() {
  const [params, setParams] = useSearchParams();
  const view = params.get("view") === "assignments" ? "assignments" : "users";
  const [revision, refresh] = useState(0);
  const action = useMutation();
  const [page, setPage] = useState(1);
  const [search, setSearch] = useState("");
  const [role, setRole] = useState("");
  const [active, setActive] = useState("");
  const [user, setUser] = useState(emptyUser);
  const [selected, setSelected] = useState(null);
  const [change, setChange] = useState(null);
  const [reason, setReason] = useState("");
  const [assignment, setAssignment] = useState({
    userId: "",
    lotId: "",
    duty: "",
    notes: "",
  });
  const [staffSearch, setStaffSearch] = useState("");
  const users = useResource(
    view === "users"
      ? `/v1/admin/users?${query({ page, pageSize: 20, search, role, isActive: active })}`
      : null,
    revision,
  );
  const assignments = useResource(
    view === "assignments"
      ? `/v1/staff-assignments?${query({ page, pageSize: 20, isActive: active, lotId: assignment.lotId })}`
      : null,
    revision,
  );
  const candidates = useResource(
    view === "assignments"
      ? `/v1/admin/users?${query({ search: staffSearch, page: 1, pageSize: 100, isActive: true })}`
      : null,
    revision,
    { allPages: "page" },
  );
  const lots = useResource(
    view === "assignments"
      ? "/v1/parking-lots?pageNumber=1&pageSize=100&isActive=true"
      : null,
    revision,
    { allPages: "pageNumber" },
  );
  const detail = useResource(
    selected ? `/v1/admin/users/${selected}` : null,
    revision,
  );
  const eligible = itemsOf(candidates.data).filter((u) =>
    ["STAFF", "MANAGER"].includes(u.role),
  );
  const chosen = eligible.find((u) => String(u.id) === assignment.userId);
  const updated = () => refresh((n) => n + 1);
  const list = view === "users" ? users : assignments;
  function switchView(next) {
    setParams({ view: next });
    setPage(1);
    setActive("");
    setChange(null);
    setSelected(null);
  }
  function confirmChange(event) {
    event.preventDefault();
    action.run(
      () =>
        change.kind === "revoke"
          ? write(`/v1/staff-assignments/${change.id}/revoke`, { reason })
          : write(
              `/v1/admin/users/${change.id}/${change.kind}`,
              {
                [change.kind === "role" ? "role" : "isActive"]: change.value,
                reason,
              },
              "PATCH",
            ),
      () => {
        setChange(null);
        updated();
      },
    );
  }
  return (
    <section className="workspace container">
      <header className="workspace-heading">
        <div>
          <span className="tag">Quản trị nhân sự</span>
          <h1>Tài khoản & phân công</h1>
          <p>Chuẩn bị tài khoản và quyền vận hành tại từng bãi đỗ.</p>
        </div>
        <Link className="btn btn--outline" to="/admin">
          Tổng quan
        </Link>
      </header>
      <nav className="workspace-tabs" aria-label="Quản trị nhân sự">
        <button
          disabled={action.busy}
          className="btn btn--outline"
          onClick={() => switchView("users")}
          aria-pressed={view === "users"}
        >
          Tài khoản
        </button>
        <button
          disabled={action.busy}
          className="btn btn--outline"
          onClick={() => switchView("assignments")}
          aria-pressed={view === "assignments"}
        >
          Phân công bãi / duty
        </button>
      </nav>
      <Feedback error={action.error} message={action.message} />
      {view === "users" ? (
        <section className="workspace-panel">
          <h2>Tạo tài khoản nội bộ</h2>
          <p className="workspace-note">
            Tài khoản nội bộ được xác minh và phê duyệt ngay. Khách hàng tự đăng
            ký qua email OTP.
          </p>
          <form
            className="workspace-form"
            onSubmit={(e) => {
              e.preventDefault();
              action.run(
                () =>
                  write("/v1/admin/users", {
                    ...user,
                    username: user.username.trim(),
                    email: user.email.trim(),
                    fullName: user.fullName.trim(),
                    phoneNumber: user.phoneNumber || null,
                    isActive: true,
                  }),
                (response) => {
                  setUser(emptyUser);
                  setSelected(response.data?.id);
                  updated();
                },
              );
            }}
          >
            <fieldset disabled={action.busy}>
              <label>
                Tên đăng nhập
                <input
                  required
                  pattern="[a-zA-Z0-9_.]{3,50}"
                  title="3–50 ký tự chữ, số, chấm hoặc gạch dưới"
                  value={user.username}
                  onChange={(e) =>
                    setUser({ ...user, username: e.target.value })
                  }
                  autoComplete="off"
                />
              </label>
              <label>
                Họ và tên
                <input
                  required
                  maxLength={100}
                  value={user.fullName}
                  onChange={(e) =>
                    setUser({ ...user, fullName: e.target.value })
                  }
                />
              </label>
              <label>
                Email
                <input
                  required
                  type="email"
                  maxLength={255}
                  value={user.email}
                  onChange={(e) => setUser({ ...user, email: e.target.value })}
                  autoComplete="off"
                />
              </label>
              <label>
                Mật khẩu ban đầu
                <input
                  required
                  type="password"
                  minLength={8}
                  maxLength={100}
                  autoComplete="new-password"
                  value={user.password}
                  onChange={(e) =>
                    setUser({ ...user, password: e.target.value })
                  }
                />
              </label>
              <label>
                Số điện thoại
                <input
                  type="tel"
                  maxLength={20}
                  value={user.phoneNumber}
                  onChange={(e) =>
                    setUser({ ...user, phoneNumber: e.target.value })
                  }
                />
              </label>
              <label>
                Vai trò
                <select
                  value={user.role}
                  onChange={(e) => setUser({ ...user, role: e.target.value })}
                >
                  <option>STAFF</option>
                  <option>MANAGER</option>
                </select>
              </label>
              <button className="btn btn--primary" disabled={action.busy}>
                Tạo tài khoản
              </button>
            </fieldset>
          </form>
        </section>
      ) : (
        <section className="workspace-panel">
          <h2>Phân công mới</h2>
          <p className="workspace-note">
            Chọn nhiệm vụ cho Staff. Manager điều hành cả bãi. Để đổi duty, thu
            hồi phân công cũ rồi tạo lại.
          </p>
          <div className="workspace-filters">
            <label>
              Tìm nhân sự theo tên/email
              <input
                value={staffSearch}
                onChange={(e) => {
                  setStaffSearch(e.target.value);
                  setAssignment((a) => ({ ...a, userId: "" }));
                }}
                disabled={action.busy}
              />
            </label>
          </div>
          <Feedback
            loading={candidates.loading || lots.loading}
            error={candidates.error || lots.error}
          />
          <form
            className="workspace-form"
            onSubmit={(e) => {
              e.preventDefault();
              action.run(
                () =>
                  write("/v1/staff-assignments", {
                    userId: Number(assignment.userId),
                    lotId: Number(assignment.lotId),
                    duty: chosen?.role === "MANAGER" ? "BOTH" : assignment.duty,
                    notes: assignment.notes || null,
                  }),
                () => {
                  setAssignment((a) => ({ ...a, notes: "" }));
                  updated();
                },
              );
            }}
          >
            <fieldset disabled={action.busy}>
              <label>
                Nhân sự
                <select
                  required
                  value={assignment.userId}
                  onChange={(e) =>
                    setAssignment({
                      ...assignment,
                      userId: e.target.value,
                      duty: "",
                    })
                  }
                >
                  <option value="">Chọn Staff / Manager</option>
                  {eligible.map((u) => (
                    <option key={u.id} value={u.id}>
                      {u.fullName} · {u.username} · {u.role}
                    </option>
                  ))}
                </select>
              </label>
              <label>
                Bãi đỗ
                <select
                  required
                  value={assignment.lotId}
                  onChange={(e) => {
                    setAssignment({ ...assignment, lotId: e.target.value });
                    setPage(1);
                  }}
                >
                  <option value="">Chọn bãi</option>
                  {itemsOf(lots.data).map((l) => (
                    <option key={l.id} value={l.id}>
                      {l.name}
                    </option>
                  ))}
                </select>
              </label>
              <label>
                Nhiệm vụ
                <select
                  required
                  disabled={chosen?.role === "MANAGER"}
                  value={chosen?.role === "MANAGER" ? "BOTH" : assignment.duty}
                  onChange={(e) =>
                    setAssignment({ ...assignment, duty: e.target.value })
                  }
                >
                  <option value="">Chọn nhiệm vụ</option>
                  {Object.entries(duties).map(([key, label]) => (
                    <option key={key} value={key}>
                      {label}
                    </option>
                  ))}
                </select>
              </label>
              <label>
                Ghi chú
                <textarea
                  maxLength={500}
                  value={assignment.notes}
                  onChange={(e) =>
                    setAssignment({ ...assignment, notes: e.target.value })
                  }
                />
              </label>
              <button
                className="btn btn--primary"
                disabled={
                  action.busy ||
                  !chosen ||
                  !assignment.lotId ||
                  candidates.loading ||
                  lots.loading
                }
              >
                Lưu phân công
              </button>
            </fieldset>
          </form>
        </section>
      )}
      {change && (
        <section className="workspace-panel workspace-confirm">
          <h2>
            Xác nhận{" "}
            {change.kind === "role"
              ? "đổi vai trò"
              : change.kind === "status"
                ? "đổi trạng thái"
                : "thu hồi phân công"}
          </h2>
          <p>
            {change.label}
            {change.kind === "role"
              ? ` → ${change.value}`
              : change.kind === "status"
                ? change.value
                  ? " → Mở tài khoản"
                  : " → Khóa tài khoản"
                : ""}
          </p>
          <p className="workspace-note">
            Thay đổi vai trò có thể thu hồi phân công hiện tại của tài khoản.
          </p>
          <form className="workspace-form" onSubmit={confirmChange}>
            <label>
              Lý do
              <textarea
                required
                maxLength={change.kind === "revoke" ? 255 : 500}
                value={reason}
                onChange={(e) => setReason(e.target.value)}
                disabled={action.busy}
              />
            </label>
            <div className="workspace-actions">
              <button
                className="btn btn--primary"
                disabled={action.busy || !reason.trim()}
              >
                Xác nhận thay đổi
              </button>
              <button
                type="button"
                className="btn btn--outline"
                disabled={action.busy}
                onClick={() => setChange(null)}
              >
                Hủy
              </button>
            </div>
          </form>
        </section>
      )}
      <section className="workspace-panel">
        <div className="workspace-heading">
          <h2>
            {view === "users" ? "Danh sách tài khoản" : "Lịch sử phân công"}
          </h2>
          <button
            className="btn btn--outline"
            disabled={action.busy || list.loading}
            onClick={updated}
          >
            Làm mới
          </button>
        </div>
        <div className="workspace-filters">
          {view === "users" && (
            <>
              <label>
                Tìm tài khoản
                <input
                  value={search}
                  disabled={action.busy}
                  onChange={(e) => {
                    setSearch(e.target.value);
                    setPage(1);
                  }}
                />
              </label>
              <label>
                Lọc vai trò
                <select
                  value={role}
                  disabled={action.busy}
                  onChange={(e) => {
                    setRole(e.target.value);
                    setPage(1);
                  }}
                >
                  <option value="">Tất cả vai trò</option>
                  {roles.map((r) => (
                    <option key={r}>{r}</option>
                  ))}
                </select>
              </label>
            </>
          )}
          <label>
            Trạng thái
            <select
              value={active}
              disabled={action.busy}
              onChange={(e) => {
                setActive(e.target.value);
                setPage(1);
              }}
            >
              <option value="">Tất cả</option>
              <option value="true">Đang hoạt động</option>
              <option value="false">Ngừng hoạt động</option>
            </select>
          </label>
        </div>
        <Feedback loading={list.loading} error={list.error} />
        {!list.loading && !list.error && !itemsOf(list.data).length && (
          <p className="workspace-empty">Không có kết quả phù hợp.</p>
        )}
        <div className="workspace-table">
          <table>
            <thead>
              <tr>
                {(view === "users"
                  ? ["Tài khoản", "Vai trò", "Trạng thái", "Thao tác"]
                  : ["Nhân sự", "Bãi / Nhiệm vụ", "Thời gian", "Thao tác"]
                ).map((h) => (
                  <th key={h}>{h}</th>
                ))}
              </tr>
            </thead>
            <tbody>
              {itemsOf(list.data).map((row) => (
                <tr key={row.id}>
                  {view === "users" ? (
                    <>
                      <td>
                        <button
                          className="btn btn--outline"
                          onClick={() => setSelected(row.id)}
                        >
                          {row.fullName}
                        </button>
                        <small>
                          {row.username} · {row.email}
                        </small>
                      </td>
                      <td>
                        <select
                          aria-label={`Vai trò ${row.username}`}
                          value={row.role}
                          disabled={action.busy}
                          onChange={(e) => {
                            setReason("");
                            setChange({
                              kind: "role",
                              id: row.id,
                              label: row.username,
                              value: e.target.value,
                            });
                          }}
                        >
                          {roles.map((r) => (
                            <option key={r}>{r}</option>
                          ))}
                        </select>
                      </td>
                      <td>
                        {row.isActive ? "Hoạt động" : "Đã khóa"}
                        <small>
                          {row.isEmailVerified
                            ? "Email đã xác minh"
                            : "Chưa xác minh email"}{" "}
                          · {row.approvalStatus}
                        </small>
                      </td>
                      <td>
                        <button
                          className="btn btn--outline"
                          disabled={action.busy}
                          onClick={() => {
                            setReason("");
                            setChange({
                              kind: "status",
                              id: row.id,
                              label: row.username,
                              value: !row.isActive,
                            });
                          }}
                        >
                          {row.isActive ? "Khóa" : "Mở khóa"}
                        </button>
                      </td>
                    </>
                  ) : (
                    <>
                      <td>
                        {row.userFullName}
                        <small>
                          {row.username} · {row.userRole}
                        </small>
                      </td>
                      <td>
                        {row.lotName}
                        <small>{duties[row.duty] || row.duty}</small>
                      </td>
                      <td>
                        {dateLabel(row.assignedAt)}
                        <small>
                          {row.isActive
                            ? "Đang phân công"
                            : `Thu hồi: ${dateLabel(row.revokedAt)}`}
                        </small>
                        <small>{row.revocationReason || row.notes}</small>
                      </td>
                      <td>
                        <button
                          className="btn btn--outline"
                          disabled={action.busy || !row.isActive}
                          onClick={() => {
                            setReason("");
                            setChange({
                              kind: "revoke",
                              id: row.id,
                              label: `${row.username} · ${row.lotName} · ${duties[row.duty]}`,
                            });
                          }}
                        >
                          Thu hồi
                        </button>
                      </td>
                    </>
                  )}
                </tr>
              ))}
            </tbody>
          </table>
        </div>
        <Pager
          page={page}
          data={list.data}
          onChange={setPage}
          disabled={action.busy || list.loading}
        />
      </section>
      {selected && (
        <section className="workspace-panel">
          <div className="workspace-heading">
            <h2>Chi tiết tài khoản</h2>
            <button
              className="btn btn--outline"
              onClick={() => setSelected(null)}
            >
              Đóng chi tiết
            </button>
          </div>
          <Feedback loading={detail.loading} error={detail.error} />
          {detail.data && (
            <dl className="workspace-detail">
              {Object.entries({
                "Họ tên": detail.data.fullName,
                Email: detail.data.email,
                "Vai trò": detail.data.role,
                "Phê duyệt": detail.data.approvalStatus,
                "Ngày tạo": dateLabel(detail.data.createdAt),
                "Số phân công đang hoạt động":
                  detail.data.activeAssignmentsCount,
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
    </section>
  );
}
export default function AdminPeople() {
  return (
    <RequireRole role="ADMIN">
      <People />
    </RequireRole>
  );
}
