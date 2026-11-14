import { useEffect, useMemo, useState } from "react";
import { apiRequest } from "../services/api.js";
import { getAuthSession } from "../services/authSession.js";
import { operationsApiBaseConfigured, operationsRequest } from "../services/operationsApi.js";
import { Feedback, itemsOf } from "../components/WorkspaceUI.jsx";
import "../operations.css";

const shifts = [
  ["MORNING", "Ca sáng · 05:00–13:00"],
  ["AFTERNOON", "Ca chiều · 13:00–21:00"],
  ["NIGHT", "Ca đêm · 21:00–05:00"],
];
const shiftLabels = Object.fromEntries(shifts);
const statusLabels = {
  AWAITING_CONFIRMATIONS: "Chờ hai bên xác nhận",
  COMPLETED: "Đã bàn giao",
  DISPUTED: "Lệch tiền · cần xử lý",
  REJECTED: "Bị từ chối",
};
const localDate = () => {
  const now = new Date();
  return `${now.getFullYear()}-${String(now.getMonth() + 1).padStart(2, "0")}-${String(now.getDate()).padStart(2, "0")}`;
};
const money = (value) => value == null
  ? "Chưa kiểm đếm"
  : new Intl.NumberFormat("vi-VN", { style: "currency", currency: "VND", maximumFractionDigits: 0 }).format(value);

function actorId() {
  const id = Number(getAuthSession()?.userProfile?.id);
  return Number.isInteger(id) ? id : null;
}

function HandoverCard({ item, currentActorId, busy, run }) {
  const [amount, setAmount] = useState("");
  const [reason, setReason] = useState("");
  const isOutgoing = currentActorId === item.outgoingStaffId;
  const isIncoming = currentActorId === item.incomingStaffId;
  const canRespond = item.status === "AWAITING_CONFIRMATIONS" && (isOutgoing || isIncoming);
  const ownAmount = isOutgoing ? item.outgoingCountedAmount : item.incomingCountedAmount;
  const variance = item.variance;
  return (
    <article className="workspace-panel handover-card">
      <div className="handover-card__header">
        <div>
          <h3>{shiftLabels[item.fromShiftCode]} → {shiftLabels[item.toShiftCode]}</h3>
          <p>{item.businessDate} · Mã {item.id}</p>
        </div>
        <span className={`handover-status handover-status--${item.status.toLowerCase()}`}>
          {statusLabels[item.status] || item.status}
        </span>
      </div>
      <dl className="handover-amounts">
        <div><dt>Người giao · #{item.outgoingStaffId}</dt><dd>{money(item.outgoingCountedAmount)}</dd></div>
        <div><dt>Người nhận · #{item.incomingStaffId}</dt><dd>{money(item.incomingCountedAmount)}</dd></div>
        {variance != null && <div><dt>Chênh lệch (nhận − giao)</dt><dd>{money(variance)}</dd></div>}
      </dl>
      {item.disputeReason && <p className="handover-dispute">{item.disputeReason}</p>}
      {canRespond && ownAmount == null && (
        <div className="handover-response">
          <label>
            {isOutgoing ? "Tiền mặt bạn đã kiểm đếm khi giao" : "Tiền mặt bạn đã kiểm đếm khi nhận"}
            <input type="number" min="0" step="1" value={amount} onChange={(e) => setAmount(e.target.value)} />
          </label>
          <div className="workspace-actions">
            <button className="btn btn--primary" disabled={busy || amount === ""} onClick={() => run(async () => {
              await operationsRequest(`/handovers/${item.id}/confirm`, {
                method: "POST", body: JSON.stringify({ countedAmount: Number(amount) }),
              });
              setAmount("");
            })}>Xác nhận số tiền</button>
            <input aria-label="Lý do từ chối" placeholder="Lý do nếu không thể bàn giao" value={reason} onChange={(e) => setReason(e.target.value)} />
            <button className="btn btn--outline" disabled={busy || reason.trim().length < 3} onClick={() => run(async () => {
              await operationsRequest(`/handovers/${item.id}/dispute`, {
                method: "POST", body: JSON.stringify({ reason }),
              });
              setReason("");
            })}>Báo có vấn đề</button>
          </div>
        </div>
      )}
      {canRespond && ownAmount != null && <p className="workspace-note">Bạn đã xác nhận số tiền; đang chờ bên còn lại.</p>}
    </article>
  );
}

export default function CashHandovers() {
  const role = getAuthSession()?.userProfile?.role;
  const manager = role === "MANAGER";
  const currentId = actorId();
  const [lots, setLots] = useState([]);
  const [lotId, setLotId] = useState("");
  const [staff, setStaff] = useState([]);
  const [items, setItems] = useState([]);
  const [businessDate, setBusinessDate] = useState(localDate());
  const [fromShiftCode, setFromShiftCode] = useState("MORNING");
  const [outgoingStaffId, setOutgoingStaffId] = useState("");
  const [incomingStaffId, setIncomingStaffId] = useState("");
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const [notice, setNotice] = useState("");
  const [revision, setRevision] = useState(0);

  const toShiftCode = useMemo(() => ({ MORNING: "AFTERNOON", AFTERNOON: "NIGHT", NIGHT: "MORNING" })[fromShiftCode], [fromShiftCode]);

  useEffect(() => {
    let active = true;
    apiRequest("/v1/staff-assignments/my-lots").then((result) => {
      if (!active) return;
      const assignedLots = itemsOf(result?.data);
      setLots(assignedLots);
      setLotId((current) => current || String(assignedLots[0]?.lotId || ""));
    }).catch((e) => { if (active) setError(e.message); }).finally(() => { if (active) setLoading(false); });
    return () => { active = false; };
  }, []);

  useEffect(() => {
    if (!lotId) return;
    let active = true;
    setError("");
    const jobs = [
      operationsRequest(`/handovers?lotId=${lotId}&page=1&pageSize=50`),
    ];
    if (manager)
      jobs.push(apiRequest(`/v1/staff-assignments?LotId=${lotId}&IsActive=true&Page=1&PageSize=100`));
    Promise.all(jobs).then(([handoverResult, staffResult]) => {
      if (!active) return;
      setItems(itemsOf(handoverResult?.data?.items));
      const candidates = itemsOf(staffResult?.data?.items).filter((person) => person.userRole === "STAFF");
      setStaff(candidates);
      if (!outgoingStaffId && candidates.length) setOutgoingStaffId(String(candidates[0].userId));
      if (!incomingStaffId && candidates.length > 1) setIncomingStaffId(String(candidates[1].userId));
    }).catch((e) => { if (active) setError(e.message); });
    return () => { active = false; };
  }, [lotId, manager, revision]);

  async function run(operation, successMessage) {
    setBusy(true); setError(""); setNotice("");
    try {
      await operation();
      setNotice(successMessage || "Đã cập nhật handover.");
      setRevision((n) => n + 1);
    } catch (e) { setError(e.message); }
    finally { setBusy(false); }
  }

  if (!operationsApiBaseConfigured())
    return <section className="workspace-panel"><h2>Chưa cấu hình Operations API</h2><p>Thiết lập VITE_OPERATIONS_API_BASE_URL và chạy Operations Service để dùng handover.</p></section>;

  return (
    <section className="workspace container">
      <header className="workspace-heading handover-page-heading">
        <div><h1>Bàn giao ca</h1><p>Hai bên tự kiểm đếm tiền mặt; khớp số mới hoàn tất bàn giao.</p></div>
        <label>Bãi
          <select value={lotId} disabled={loading || busy} onChange={(e) => setLotId(e.target.value)}>
            {lots.map((lot) => <option key={lot.lotId} value={lot.lotId}>{lot.lotName}</option>)}
          </select>
        </label>
      </header>
      <Feedback loading={loading} error={error} message={notice} />

      {manager && lotId && (
        <section className="workspace-panel">
          <h2>Tạo bàn giao mới</h2>
          <form className="workspace-form handover-create-form" onSubmit={(e) => {
            e.preventDefault();
            run(async () => {
              await operationsRequest("/handovers", { method: "POST", body: JSON.stringify({
                lotId: Number(lotId), businessDate, fromShiftCode, toShiftCode,
                outgoingStaffId: Number(outgoingStaffId), incomingStaffId: Number(incomingStaffId),
              }) });
            }, "Đã tạo handover; chờ người giao và nhận xác nhận.");
          }}>
            <label>Ngày nghiệp vụ<input type="date" required value={businessDate} onChange={(e) => setBusinessDate(e.target.value)} /></label>
            <label>Ca giao<select value={fromShiftCode} onChange={(e) => setFromShiftCode(e.target.value)}>{shifts.map(([code, label]) => <option key={code} value={code}>{label}</option>)}</select></label>
            <label>Ca nhận<input readOnly value={shiftLabels[toShiftCode]} /></label>
            <label>Nhân viên giao<select required value={outgoingStaffId} onChange={(e) => setOutgoingStaffId(e.target.value)}><option value="">Chọn nhân viên</option>{staff.map((person) => <option key={person.userId} value={person.userId}>{person.userFullName} · #{person.userId}</option>)}</select></label>
            <label>Nhân viên nhận<select required value={incomingStaffId} onChange={(e) => setIncomingStaffId(e.target.value)}><option value="">Chọn nhân viên</option>{staff.map((person) => <option key={person.userId} value={person.userId} disabled={String(person.userId) === outgoingStaffId}>{person.userFullName} · #{person.userId}</option>)}</select></label>
            <button className="btn btn--primary" disabled={busy || !staff.length || !outgoingStaffId || !incomingStaffId || outgoingStaffId === incomingStaffId}>Tạo bàn giao</button>
          </form>
        </section>
      )}

      <div className="handover-list-heading"><h2>{manager ? "Lịch sử bàn giao tại bãi" : "Bàn giao cần bạn xác nhận"}</h2><button className="btn btn--outline" disabled={busy || loading} onClick={() => setRevision((n) => n + 1)}>Làm mới</button></div>
      {!loading && !error && items.length === 0 && <section className="workspace-panel workspace-empty">Chưa có handover trong bãi này.</section>}
      <div className="handover-list">{items.map((item) => <HandoverCard key={item.id} item={item} currentActorId={currentId} busy={busy} run={run} />)}</div>
    </section>
  );
}
