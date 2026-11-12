import { useEffect, useRef, useState } from "react";
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

const roles = ["STAFF", "MANAGER"];
// Hai loại phân công khi tạo mới. Nhãn GATE_IN/GATE_OUT/BOTH chỉ để hiển thị dữ liệu cũ.
const dutyOptions = {
  GATE: "Trực cổng (xe vào và xe ra)",
  SLOT_VERIFIER: "Trực khu vực (xác minh ô, điều phối)",
};
const duties = {
  ...dutyOptions,
  GATE_IN: "Trực cổng vào",
  GATE_OUT: "Trực cổng ra",
  BOTH: "Trực cổng và khu vực (phân công cũ)",
};
const emptyUser = {
  username: "",
  email: "",
  password: "",
  fullName: "",
  phoneNumber: "",
  role: "STAFF",
};

export function People({ assignmentsOnly = false }) {
  const [params] = useSearchParams();
  const view = assignmentsOnly || params.get("view") === "assignments" ? "assignments" : "users";
  const [revision, refresh] = useState(0);
  const action = useMutation();
  const [page, setPage] = useState(1);
  const [search, setSearch] = useState("");
  const [role, setRole] = useState("");
  const [active, setActive] = useState("");
  const [user, setUser] = useState(emptyUser);
  const [selected, setSelected] = useState(null);
  const [change, setChange] = useState(null);
  const [roleDrafts, setRoleDrafts] = useState({});
  const confirmPanel = useRef(null);
  useEffect(() => {
    if (change) confirmPanel.current?.scrollIntoView?.({ behavior: "smooth", block: "center" });
  }, [change]);
  const [reason, setReason] = useState("");
  const [assignment, setAssignment] = useState({
    userId: "",
    lotId: "",
    floorId: "",
    zoneId: "",
    duty: "",
    notes: "",
  });
  const [staffSearch, setStaffSearch] = useState("");
  const [chosenStaff, setChosenStaff] = useState(null);
  const users = useResource(
    view === "users"
      ? `/v1/admin/users?${query({ page, pageSize: 20, search, role, isActive: active, internalOnly: true })}`
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
    view === "assignments" && !chosenStaff && staffSearch.trim()
      ? `/v1/staff-assignments/candidates?${query({ search: staffSearch, page: 1, pageSize: 100 })}`
      : null,
    revision,
    { allPages: "page" },
  );
  const lots = useResource(
    view === "assignments"
      ? assignmentsOnly ? "/v1/staff-assignments/my-lots" : "/v1/parking-lots?pageNumber=1&pageSize=100&isActive=true"
      : null,
    revision,
    { allPages: "pageNumber" },
  );
  const detail = useResource(
    selected ? `/v1/admin/users/${selected}` : null,
    revision,
  );
  const locations = useResource(
    view === "assignments" && assignment.lotId ? `/v1/staff-assignments/locations/${assignment.lotId}` : null,
    revision,
  );
  const floors = locations.data?.floors || [];
  const selectedFloor = floors.find(f => String(f.id) === assignment.floorId);
  const zones = selectedFloor?.zones || [];
  const requiresZone = assignment.duty === "SLOT_VERIFIER";
  const eligible = itemsOf(candidates.data).filter((u) => u.role === "STAFF");
  const availableLots = assignmentsOnly
    ? itemsOf(lots.data).filter(l => l.isActive).map(l => ({ id: l.lotId, name: l.lotName }))
    : itemsOf(lots.data);
  const chosen = chosenStaff;
  const updated = () => refresh((n) => n + 1);
  const list = view === "users" ? users : assignments;
  useEffect(() => {
    setPage(1);
    setActive("");
    setChange(null);
    setSelected(null);
  }, [view]);
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
        if (change.kind === "role") setRoleDrafts(drafts => {
          const next = { ...drafts }; delete next[change.id]; return next;
        });
        setChange(null);
        updated();
      },
    );
  }
  return (
    <section className="workspace container">
      <header className="workspace-heading">
        <div>
          <span className="tag">{assignmentsOnly ? "Quản lý bãi" : "Quản trị nhân sự"}</span>
          <h1>{view === "assignments" ? "Phân công nhân viên" : "Quản lý tài khoản"}</h1>
          <p>{view === "assignments" ? assignmentsOnly ? "Phân công Staff tại các bãi bạn đang quản lý." : "Phân công Staff theo bãi, tầng, khu vực và nhiệm vụ." : "Tạo và quản lý tài khoản nội bộ cho Manager và Staff."}</p>
        </div>
        <Link className="btn btn--outline" to={assignmentsOnly ? "/manager" : "/admin"}>
          Tổng quan
        </Link>
      </header>
      <Feedback error={action.error} message={action.message} />
      {view === "users" ? (
        <section className="workspace-panel">
          <h2>Tạo tài khoản nội bộ</h2>
          <p className="workspace-note">
            Admin tạo trực tiếp tài khoản Manager và Staff. Tài khoản có thể đăng nhập ngay sau khi tạo.
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
                  <option value="STAFF">Staff</option>
                  <option value="MANAGER">Manager</option>
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
            Chọn Staff, bãi, loại phân công và tầng. Quản lý khu vực cần chọn thêm Zone. Để đổi phân công, thu hồi phân công cũ rồi tạo lại.
          </p>
          <div className="workspace-filters">
            <label>
              Tìm Staff theo tên/email
              <input
                role="combobox"
                aria-autocomplete="list"
                aria-controls="staff-search-results"
                aria-expanded={Boolean(!chosenStaff && staffSearch.trim() && eligible.length)}
                value={staffSearch}
                onChange={(e) => {
                  setStaffSearch(e.target.value);
                  setChosenStaff(null);
                  setAssignment((a) => ({ ...a, userId: "" }));
                }}
                disabled={action.busy}
              />
            </label>
          </div>
          {!chosenStaff && staffSearch.trim() && <ul className="assignment-search-results" id="staff-search-results" role="listbox" aria-label="Kết quả tìm Staff">
            {eligible.map(staff => <li key={staff.id} role="option" aria-selected="false"><button type="button" className="assignment-search-result" disabled={action.busy} onClick={() => {
              setChosenStaff(staff); setStaffSearch(staff.fullName); setAssignment(a => ({ ...a, userId: String(staff.id) }));
            }}><strong>{staff.fullName}</strong><span>{staff.username} · {staff.email}</span></button></li>)}
            {!candidates.loading && !candidates.error && !eligible.length && <li>Không tìm thấy Staff phù hợp.</li>}
          </ul>}
          {chosenStaff && <div className="assignment-selected-staff"><div><strong>{chosenStaff.fullName}</strong><small>{chosenStaff.username} · {chosenStaff.email}</small></div><button type="button" className="btn assignment-cancel" disabled={action.busy} onClick={() => { setChosenStaff(null); setStaffSearch(""); setAssignment(a => ({ ...a, userId: "" })); }}>Hủy</button></div>}
          <Feedback
            loading={candidates.loading || lots.loading || locations.loading}
            error={candidates.error || lots.error || locations.error}
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
                    floorId: Number(assignment.floorId),
                    zoneId: requiresZone ? Number(assignment.zoneId) : null,
                    duty: assignment.duty,
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
                Bãi đỗ
                <select
                  required
                  value={assignment.lotId}
                  onChange={(e) => {
                    setAssignment({ ...assignment, lotId: e.target.value, floorId: "", zoneId: "" });
                    setPage(1);
                  }}
                >
                  <option value="">Chọn bãi</option>
                  {availableLots.map((l) => (
                    <option key={l.id} value={l.id}>
                      {l.name}
                    </option>
                  ))}
                </select>
              </label>
              <label>
                Phân công
                <select
                  required
                  disabled={!assignment.lotId}
                  value={assignment.duty}
                  onChange={(e) =>
                    setAssignment({ ...assignment, duty: e.target.value, floorId: "", zoneId: "" })
                  }
                >
                  <option value="">Chọn phân công</option>
                  {Object.entries(dutyOptions).map(([key, label]) => (
                    <option key={key} value={key}>
                      {label}
                    </option>
                  ))}
                </select>
              </label>
              {assignment.duty && <label>Tầng<select required value={assignment.floorId} disabled={!assignment.lotId || locations.loading || Boolean(locations.error)} onChange={e => setAssignment({ ...assignment, floorId: e.target.value, zoneId: "" })}>
                <option value="">Chọn tầng</option>{floors.map(f => <option key={f.id} value={f.id}>{f.name}</option>)}
              </select></label>}
              {requiresZone && assignment.floorId && <label>Khu vực (Zone)<select required value={assignment.zoneId} disabled={!selectedFloor || locations.loading || Boolean(locations.error)} onChange={e => setAssignment({ ...assignment, zoneId: e.target.value })}>
                <option value="">Chọn khu vực</option>{zones.map(z => <option key={z.id} value={z.id}>{z.name}</option>)}
              </select></label>}
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
                  !selectedFloor || (requiresZone && !zones.some(z => String(z.id) === assignment.zoneId)) ||
                  !assignment.duty ||
                  Boolean(candidates.error || lots.error || locations.error) || locations.loading ||
                  !availableLots.some(l => String(l.id) === assignment.lotId) ||
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
        <section ref={confirmPanel} role="region" aria-labelledby="account-change-title" className="workspace-panel workspace-confirm">
          <h2 id="account-change-title">
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
            {change.kind === "revoke"
              ? "Staff sẽ ngừng có quyền theo phân công này sau khi thu hồi."
              : "Thay đổi vai trò có thể thu hồi phân công hiện tại của tài khoản."}
          </p>
          <form className="workspace-form" onSubmit={confirmChange}>
            <label>
              {change.kind === "revoke" ? "Lý do" : "Ghi chú thay đổi (không bắt buộc)"}
              <textarea
                required={change.kind === "revoke"}
                maxLength={change.kind === "revoke" ? 255 : 500}
                value={reason}
                onChange={(e) => setReason(e.target.value)}
                disabled={action.busy}
              />
            </label>
            <div className="workspace-actions">
              <button
                className="btn btn--primary"
                disabled={action.busy || (change.kind === "revoke" && !reason.trim())}
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
                  {[...roles, "ADMIN"].map((r) => (
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
                  : ["Nhân sự", "Bãi / Phân công", "Thời gian", "Thao tác"]
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
                        {roles.includes(row.role) ? <div className="account-role-editor"><select
                          aria-label={`Vai trò ${row.username}`}
                          value={roleDrafts[row.id] ?? row.role}
                          disabled={action.busy}
                          onChange={(e) => {
                            setRoleDrafts(drafts => ({ ...drafts, [row.id]: e.target.value }));
                          }}
                        >
                          {roles.map((r) => (
                            <option key={r}>{r}</option>
                          ))}
                        </select><button type="button" className="btn btn--outline" aria-label={`Lưu vai trò ${row.username}`} disabled={action.busy || !roleDrafts[row.id] || roleDrafts[row.id] === row.role} onClick={() => {
                          setReason(""); setChange({ kind: "role", id: row.id, label: row.username, value: roleDrafts[row.id] });
                        }}>Lưu vai trò</button></div> : <strong>{row.role}</strong>}
                      </td>
                      <td>
                        {row.isActive ? "Hoạt động" : "Đã khóa"}
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
                        <small>{row.floorName || "Chưa chọn tầng"}{row.zoneName ? ` · ${row.zoneName}` : ""}</small>
                        <small>{duties[row.duty] || (row.duty === "GATE" ? "Trực cổng (phân công cũ)" : "Cần chọn lại phân công")}</small>
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
