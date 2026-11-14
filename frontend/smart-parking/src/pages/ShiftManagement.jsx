import { useEffect, useState } from "react";
import { apiRequest } from "../services/api.js";
import { operationsRequest } from "../services/operationsApi.js";
import { Feedback, itemsOf } from "../components/WorkspaceUI.jsx";
import "../operations.css";

const shifts = [
  ["MORNING", "Ca sáng · 05:00–13:00"],
  ["AFTERNOON", "Ca chiều · 13:00–21:00"],
  ["NIGHT", "Ca đêm · 21:00–05:00"],
];
const shiftLabels = Object.fromEntries(shifts);
const statusLabels = { PLANNED: "Đã lên lịch", IN_PROGRESS: "Đang diễn ra", COMPLETED: "Đã chốt" };
const localDate = () => {
  const now = new Date();
  return `${now.getFullYear()}-${String(now.getMonth() + 1).padStart(2, "0")}-${String(now.getDate()).padStart(2, "0")}`;
};
const dateTime = (value) => value ? new Date(value).toLocaleString("vi-VN", { dateStyle: "short", timeStyle: "short" }) : "—";

export default function ShiftManagement() {
  const [lots, setLots] = useState([]);
  const [lotId, setLotId] = useState("");
  const [date, setDate] = useState(localDate());
  const [code, setCode] = useState("MORNING");
  const [staff, setStaff] = useState([]);
  const [records, setRecords] = useState([]);
  const [drafts, setDrafts] = useState({});
  const [loading, setLoading] = useState(true);
  const [busyId, setBusyId] = useState("");
  const [error, setError] = useState("");
  const [notice, setNotice] = useState("");
  const [revision, setRevision] = useState(0);

  useEffect(() => {
    let active = true;
    apiRequest("/v1/staff-assignments/my-lots").then((result) => {
      if (!active) return;
      const values = itemsOf(result?.data);
      setLots(values);
      setLotId((current) => current || String(values[0]?.lotId || ""));
    }).catch((e) => { if (active) setError(e.message); }).finally(() => { if (active) setLoading(false); });
    return () => { active = false; };
  }, []);

  useEffect(() => {
    if (!lotId) return;
    let active = true;
    setError("");
    Promise.all([
      operationsRequest(`/shifts?lotId=${lotId}&page=1&pageSize=100`),
      apiRequest(`/v1/staff-assignments?LotId=${lotId}&IsActive=true&Page=1&PageSize=100`),
    ]).then(([shiftResult, staffResult]) => {
      if (!active) return;
      const values = itemsOf(shiftResult?.data?.items);
      setRecords(values);
      setStaff(itemsOf(staffResult?.data?.items).filter((x) => x.userRole === "STAFF"));
      setDrafts((current) => {
        const next = { ...current };
        values.forEach((shift) => {
          if (!next[shift.id]) next[shift.id] = { staffId: "", dutyCode: "GATE" };
        });
        return next;
      });
    }).catch((e) => { if (active) setError(e.message); });
    return () => { active = false; };
  }, [lotId, revision]);

  async function run(key, operation, message) {
    setBusyId(key); setError(""); setNotice("");
    try {
      await operation();
      setNotice(message);
      setRevision((n) => n + 1);
    } catch (e) { setError(e.message); }
    finally { setBusyId(""); }
  }

  return (
    <section className="workspace container">
      <header className="workspace-heading handover-page-heading">
        <div><h1>Quản lý ca trực</h1><p>Lập lịch, xếp nhân viên và theo dõi trạng thái ba ca vận hành.</p></div>
        <label>Bãi<select value={lotId} disabled={loading || Boolean(busyId)} onChange={(e) => setLotId(e.target.value)}>{lots.map((lot) => <option key={lot.lotId} value={lot.lotId}>{lot.lotName}</option>)}</select></label>
      </header>
      <Feedback loading={loading} error={error} message={notice} />
      {lotId && <section className="workspace-panel">
        <h2>Lập lịch ca</h2>
        <form className="workspace-form handover-create-form" onSubmit={(e) => {
          e.preventDefault();
          run("create", () => operationsRequest("/shifts", { method: "POST", body: JSON.stringify({ lotId: Number(lotId), businessDate: date, shiftCode: code }) }), "Đã tạo ca theo lịch.");
        }}>
          <label>Ngày bắt đầu ca<input type="date" required value={date} onChange={(e) => setDate(e.target.value)} /></label>
          <label>Khung giờ<select value={code} onChange={(e) => setCode(e.target.value)}>{shifts.map(([value, label]) => <option key={value} value={value}>{label}</option>)}</select></label>
          <div><p className="workspace-note">Ca đêm kết thúc lúc 05:00 ngày hôm sau.</p><button className="btn btn--primary" disabled={Boolean(busyId)}>Tạo ca</button></div>
        </form>
      </section>}

      <div className="handover-list-heading"><h2>Danh sách ca</h2><button className="btn btn--outline" disabled={Boolean(busyId)} onClick={() => setRevision((n) => n + 1)}>Làm mới</button></div>
      {!loading && !error && records.length === 0 && <section className="workspace-panel workspace-empty">Chưa có ca nào cho bãi này.</section>}
      <div className="handover-list">
        {records.map((shift) => {
          const draft = drafts[shift.id] || { staffId: "", dutyCode: "GATE" };
          const assignedIds = new Set(shift.assignments.map((x) => x.staffId));
          return <article className="workspace-panel handover-card" key={shift.id}>
            <div className="handover-card__header"><div><h3>{shiftLabels[shift.code]} · {shift.businessDate}</h3><p>{dateTime(shift.startsAtUtc)} – {dateTime(shift.endsAtUtc)} · Mã {shift.id}</p></div><span className={`handover-status handover-status--${shift.status.toLowerCase()}`}>{statusLabels[shift.status] || shift.status}</span></div>
            <div className="shift-roster"><h4>Nhân viên trực</h4>{shift.assignments.length ? <ul>{shift.assignments.map((assignment) => <li key={assignment.id}>Nhân viên #{assignment.staffId} · {assignment.dutyCode === "GATE" ? "Cổng" : assignment.dutyCode === "SLOT_VERIFIER" ? "Xác minh ô" : "Cổng và xác minh ô"}</li>)}</ul> : <p className="workspace-note">Chưa phân công nhân viên.</p>}</div>
            {shift.status === "PLANNED" && <form className="workspace-form handover-create-form" onSubmit={(e) => {
              e.preventDefault();
              run(shift.id, () => operationsRequest(`/shifts/${shift.id}/assignments`, { method: "POST", body: JSON.stringify({ staffId: Number(draft.staffId), dutyCode: draft.dutyCode }) }), "Đã cập nhật phân công ca.");
            }}>
              <label>Thêm nhân viên<select required value={draft.staffId} onChange={(e) => setDrafts((current) => ({ ...current, [shift.id]: { ...draft, staffId: e.target.value } }))}><option value="">Chọn nhân viên</option>{staff.filter((person) => !assignedIds.has(person.userId)).map((person) => <option key={person.userId} value={person.userId}>{person.userFullName} · #{person.userId}</option>)}</select></label>
              <label>Nhiệm vụ<select value={draft.dutyCode} onChange={(e) => setDrafts((current) => ({ ...current, [shift.id]: { ...draft, dutyCode: e.target.value } }))}><option value="GATE">Trực cổng</option><option value="SLOT_VERIFIER">Xác minh ô</option><option value="BOTH">Cổng và xác minh ô</option></select></label>
              <button className="btn btn--outline" disabled={Boolean(busyId) || !draft.staffId}>Thêm vào ca</button>
            </form>}
            <div className="workspace-actions">
              {shift.status === "PLANNED" && <button className="btn btn--primary" disabled={Boolean(busyId) || !shift.assignments.length} onClick={() => run(shift.id, () => operationsRequest(`/shifts/${shift.id}/start`, { method: "POST" }), "Ca đã bắt đầu.")}>Bắt đầu ca</button>}
              {shift.status === "IN_PROGRESS" && <button className="btn btn--outline" disabled={Boolean(busyId)} onClick={() => run(shift.id, () => operationsRequest(`/shifts/${shift.id}/complete`, { method: "POST" }), "Đã chốt ca.")}>Chốt ca</button>}
            </div>
          </article>;
        })}
      </div>
    </section>
  );
}
