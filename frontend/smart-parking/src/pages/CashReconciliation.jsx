import { useEffect, useMemo, useState } from "react";
import { apiRequest } from "../services/api.js";
import { getAuthSession } from "../services/authSession.js";
import { operationsApiBaseConfigured, operationsRequest } from "../services/operationsApi.js";
import { Feedback, itemsOf } from "../components/WorkspaceUI.jsx";
import "../operations.css";

const labels = { MORNING: "Ca sáng", AFTERNOON: "Ca chiều", NIGHT: "Ca đêm", PENDING_REVIEW: "Chờ Manager khác duyệt", APPROVED: "Đã duyệt" };
const money = (v) => new Intl.NumberFormat("vi-VN", { style: "currency", currency: "VND", maximumFractionDigits: 0 }).format(Number(v || 0));
const isoNow = () => new Date().toISOString().slice(0, 16);

export default function CashReconciliation() {
  const [lots, setLots] = useState([]), [lotId, setLotId] = useState("");
  const [shifts, setShifts] = useState([]), [items, setItems] = useState([]);
  const [shiftId, setShiftId] = useState("");
  const [form, setForm] = useState({ openingCash: "0", cashReceipts: "0", cashRefunds: "0", cashDisbursements: "0", countedClosingCash: "", paymentSummaryReference: "", paymentSummaryCapturedAtUtc: isoNow(), varianceReason: "" });
  const [approvalNotes, setApprovalNotes] = useState({});
  const [loading, setLoading] = useState(true), [busy, setBusy] = useState(false), [error, setError] = useState(""), [notice, setNotice] = useState(""), [revision, setRevision] = useState(0);
  const existingShiftIds = useMemo(() => new Set(items.map((x) => x.shiftId)), [items]);
  const eligible = shifts.filter((x) => x.status === "COMPLETED" && !existingShiftIds.has(x.id));
  const expected = Number(form.openingCash || 0) + Number(form.cashReceipts || 0) - Number(form.cashRefunds || 0) - Number(form.cashDisbursements || 0);
  const variance = Number(form.countedClosingCash || 0) - expected;
  const currentManagerId = Number(getAuthSession()?.userProfile?.id);

  useEffect(() => {
    let active = true;
    apiRequest("/v1/staff-assignments/my-lots").then((r) => {
      if (!active) return;
      const values = itemsOf(r?.data); setLots(values); setLotId((v) => v || String(values[0]?.lotId || ""));
    }).catch((e) => active && setError(e.message)).finally(() => active && setLoading(false));
    return () => { active = false; };
  }, []);
  useEffect(() => {
    if (!lotId) return;
    let active = true; setError("");
    Promise.all([operationsRequest(`/reconciliations?lotId=${lotId}`), operationsRequest(`/shifts?lotId=${lotId}&page=1&pageSize=100`)]).then(([r, s]) => {
      if (!active) return;
      const rows = itemsOf(r?.data?.items); setItems(rows); setShifts(itemsOf(s?.data?.items));
      setShiftId((current) => rows.some((x) => x.shiftId === current) ? "" : "");
    }).catch((e) => active && setError(e.message));
    return () => { active = false; };
  }, [lotId, revision]);

  async function run(action, message) {
    setBusy(true); setError(""); setNotice("");
    try { await action(); setNotice(message); setRevision((x) => x + 1); }
    catch (e) { setError(e.message); } finally { setBusy(false); }
  }
  function update(key, value) { setForm((f) => ({ ...f, [key]: value })); }

  if (!operationsApiBaseConfigured()) return <section className="workspace-panel"><h2>Chưa cấu hình Operations API</h2><p>Thiết lập VITE_OPERATIONS_API_BASE_URL để dùng đối soát ca.</p></section>;
  return <section className="workspace container">
    <header className="workspace-heading handover-page-heading"><div><h1>Đối soát tiền mặt theo ca</h1><p>Số liệu thu, hoàn và chi được chép từ báo cáo Payment; hệ thống tính số dự kiến và chênh lệch.</p></div><label>Bãi<select value={lotId} disabled={busy || loading} onChange={(e) => setLotId(e.target.value)}>{lots.map((x) => <option key={x.lotId} value={x.lotId}>{x.lotName}</option>)}</select></label></header>
    <Feedback loading={loading} error={error} message={notice} />
    {lotId && <section className="workspace-panel"><h2>Lập phiếu đối soát</h2><p className="workspace-note">Nhập mã báo cáo/export Payment và thời điểm chụp. Payment hiện chưa có API tổng hợp tự động theo ca.</p>
      {eligible.length === 0 ? <p>Chưa có ca hoàn tất nào đang chờ đối soát.</p> : <form className="workspace-form handover-create-form" onSubmit={(e) => { e.preventDefault(); run(async () => {
        await operationsRequest("/reconciliations", { method: "POST", body: JSON.stringify({ shiftId, openingCash: Number(form.openingCash), cashReceipts: Number(form.cashReceipts), cashRefunds: Number(form.cashRefunds), cashDisbursements: Number(form.cashDisbursements), countedClosingCash: Number(form.countedClosingCash), paymentSummaryReference: form.paymentSummaryReference, paymentSummaryCapturedAtUtc: new Date(form.paymentSummaryCapturedAtUtc).toISOString(), varianceReason: form.varianceReason || null }) });
        setForm((f) => ({ ...f, countedClosingCash: "", paymentSummaryReference: "", varianceReason: "" }));
      }, "Đã gửi phiếu đối soát chờ Manager khác duyệt."); }}>
        <label>Ca đã hoàn tất<select required value={shiftId} onChange={(e) => setShiftId(e.target.value)}><option value="">Chọn ca</option>{eligible.map((x) => <option key={x.id} value={x.id}>{labels[x.code]} · {x.businessDate}</option>)}</select></label>
        <label>Tiền đầu ca<input type="number" min="0" step="1" required value={form.openingCash} onChange={(e) => update("openingCash", e.target.value)} /></label>
        <label>Thu tiền mặt theo Payment<input type="number" min="0" step="1" required value={form.cashReceipts} onChange={(e) => update("cashReceipts", e.target.value)} /></label>
        <label>Hoàn tiền mặt<input type="number" min="0" step="1" required value={form.cashRefunds} onChange={(e) => update("cashRefunds", e.target.value)} /></label>
        <label>Khoản chi tiền mặt đã duyệt<input type="number" min="0" step="1" required value={form.cashDisbursements} onChange={(e) => update("cashDisbursements", e.target.value)} /></label>
        <label>Tiền cuối ca kiểm đếm<input type="number" min="0" step="1" required value={form.countedClosingCash} onChange={(e) => update("countedClosingCash", e.target.value)} /></label>
        <label>Mã báo cáo / export Payment<input required minLength="3" maxLength="200" value={form.paymentSummaryReference} onChange={(e) => update("paymentSummaryReference", e.target.value)} /></label>
        <label>Thời điểm chụp báo cáo<input type="datetime-local" required value={form.paymentSummaryCapturedAtUtc} onChange={(e) => update("paymentSummaryCapturedAtUtc", e.target.value)} /></label>
        <label>Lý do chênh lệch{variance !== 0 && <span> · bắt buộc</span>}<input maxLength="500" value={form.varianceReason} onChange={(e) => update("varianceReason", e.target.value)} /></label>
        <div className="workspace-note">Dự kiến: <strong>{money(expected)}</strong> · Chênh lệch: <strong>{money(variance)}</strong></div>
        <button className="btn btn--primary" disabled={busy || !shiftId || form.countedClosingCash === "" || expected < 0 || (variance !== 0 && !form.varianceReason.trim())}>Gửi duyệt</button>
      </form>}
    </section>}
    <div className="handover-list-heading"><h2>Lịch sử và phiếu chờ duyệt</h2><button className="btn btn--outline" disabled={busy || loading} onClick={() => setRevision((x) => x + 1)}>Làm mới</button></div>
    {items.map((x) => <article className="workspace-panel handover-card" key={x.id}><div className="handover-card__header"><div><h3>{labels[x.shiftCode]} · {x.businessDate}</h3><p>Báo cáo Payment: {x.paymentSummaryReference} · chụp {new Date(x.paymentSummaryCapturedAtUtc).toLocaleString("vi-VN")}</p></div><span className="handover-status">{labels[x.status]}</span></div>
      <dl className="handover-amounts"><div><dt>Đầu ca</dt><dd>{money(x.openingCash)}</dd></div><div><dt>Thu</dt><dd>{money(x.cashReceipts)}</dd></div><div><dt>Hoàn</dt><dd>{money(x.cashRefunds)}</dd></div><div><dt>Chi</dt><dd>{money(x.cashDisbursements)}</dd></div><div><dt>Dự kiến</dt><dd>{money(x.expectedClosingCash)}</dd></div><div><dt>Đếm thực tế</dt><dd>{money(x.countedClosingCash)}</dd></div><div><dt>Chênh lệch</dt><dd>{money(x.variance)}</dd></div></dl>
      {x.varianceReason && <p className="handover-dispute">Lý do: {x.varianceReason}</p>}{x.approvalNote && <p className="workspace-note">Ghi chú duyệt: {x.approvalNote}</p>}
      {x.status === "PENDING_REVIEW" && x.submittedByManagerId !== currentManagerId && <div className="workspace-actions"><input aria-label="Ghi chú duyệt" placeholder={x.variance !== 0 ? "Ghi chú duyệt bắt buộc" : "Ghi chú duyệt"} value={approvalNotes[x.id] || ""} onChange={(e) => setApprovalNotes((v) => ({ ...v, [x.id]: e.target.value }))} /><button className="btn btn--primary" disabled={busy || (x.variance !== 0 && !(approvalNotes[x.id] || "").trim())} onClick={() => run(() => operationsRequest(`/reconciliations/${x.id}/approve`, { method: "POST", body: JSON.stringify({ approvalNote: approvalNotes[x.id] || null }) }), "Đã duyệt phiếu đối soát.")}>Duyệt (Manager khác)</button></div>}
    </article>)}
  </section>;
}
