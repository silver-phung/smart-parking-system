import { useCallback, useState } from "react";
import { Link } from "react-router-dom";
import RequireRole from "../components/RequireRole.jsx";
import { apiRequest } from "../services/api.js";
import { operationsApi } from "../services/operationsApi.js";
import { collectPages } from "../services/staffApi.js";
import { Notice, StaffError, useStaffAction, useStaffQuery } from "../components/staff/StaffUI.jsx";
import "../staff.css";
import "../operations.css";

const Field = ({ label, children }) => <label className="staff-field">{label}{children}</label>;
const numeric = [
  ["holdMinutes", "Hạn giữ chỗ (phút)"], ["earlyArrivalMinutes", "Đến sớm (phút)"],
  ["lateArrivalMinutes", "Đến trễ (phút)"], ["cancellationNoticeMinutes", "Báo hủy trước (phút)"],
  ["depositAmount", "Tiền cọc (VNĐ)"], ["nearFullThreshold", "Ngưỡng gần đầy (số chỗ còn)"],
  ["feeUnitMinutes", "Đơn vị tính phí (phút)"],
];
const choices = [
  ["feeRounding", "Làm tròn phí", [["CEILING", "Lên"], ["FLOOR", "Xuống"], ["NEAREST", "Gần nhất"]]],
  ["noShowAction", "Không đến", [["EXPIRE", "Hết hạn đặt"]]],
  ["cancellationAction", "Hủy đặt", [["ALLOW", "Cho phép theo thời hạn"], ["DENY", "Không cho hủy"]]],
  ["rateSelection", "Mốc chọn phiên bản giá", [["CHECK_IN", "Lúc nhận xe"]]],
];
function LotSettings({ lot, onBusyChange }) {
  const load = useCallback(async signal => {
    const [zones, policies, layout] = await Promise.all(["zones", "policies", "layout"].map(path => operationsApi(lot.id, path, null, null, signal)));
    return { zones, policies, layout };
  }, [lot.id]);
  const query = useStaffQuery(load);
  const action = useStaffAction(onBusyChange);
  const [message, setMessage] = useState("");
  const [zone, setZone] = useState({ code: "", name: "", type: "TRANSIENT", layout: "INSIDE", isActive: true });
  const [editId, setEdit] = useState(null);
  const [policy, setPolicy] = useState({ zoneId: "", effectiveFrom: "", decisionReference: "", approve: false });
  const [ticket, setTicket] = useState({ code: "", action: "REPLACE", replacementCode: "", reason: "" });
  const [ticketResult, setTicketResult] = useState(null);
  const [floorDraft, setFloorDraft] = useState({ name: "", floorNumber: "", zoneId: "" });
  const [createdFloor, setCreatedFloor] = useState(null);
  const [slotDraft, setSlotDraft] = useState({ floorId: "", slotCode: "", positionX: "", positionY: "", positionZ: "" });
  const busy = action.busy || query.loading || Boolean(query.error);
  const perform = work => action.run(async () => { setMessage(""); await work(); setMessage("Đã lưu vào máy chủ."); query.reload(); });
  const patchPolicy = (key, value) => setPolicy(p => ({ ...p, [key]: value }));
  return <>
    <StaffError error={query.error || action.error} />
    {message && <Notice kind="success">{message}</Notice>}
    <button className="staff-button" disabled={action.busy || query.loading} onClick={query.reload}>Tải lại cấu hình</button>
    <div className="operations-admin-grid">
      <section className="staff-panel"><h2>Khu bãi</h2><p>Đóng khu ngừng nhận xe mới và giữ nguyên các lượt xe còn bên trong.</p>
        <div className="operations-session-list">{query.data?.zones.map(z => <button className="operations-session" disabled={action.busy} key={z.id} onClick={() => { setEdit(z.id); setZone({ code: z.code, name: z.name, type: z.type, layout: z.layout, isActive: z.isActive }); }}>
          <strong>{z.code} · {z.name}</strong><span>{z.type} · {z.layout} · {z.isActive ? "Hoạt động" : "Đóng"}</span><small>Tầng: {z.floorIds.join(", ") || "Chưa có"}</small>
        </button>)}</div>
        <form onSubmit={e => { e.preventDefault(); perform(async () => {
          await operationsApi(lot.id, `zones${editId ? `/${editId}` : ""}`, zone, editId ? "PUT" : "POST");
          setEdit(null); setZone({ code: "", name: "", type: "TRANSIENT", layout: "INSIDE", isActive: true });
        }); }}><fieldset className="operations-fields" disabled={busy}>
          <h3>{editId ? `Sửa khu #${editId}` : "Thêm khu"}</h3>
          <Field label="Mã khu"><input required maxLength={30} value={zone.code} onChange={e => setZone({ ...zone, code: e.target.value })} /></Field>
          <Field label="Tên khu"><input required maxLength={100} value={zone.name} onChange={e => setZone({ ...zone, name: e.target.value })} /></Field>
          <Field label="Loại khu"><select value={zone.type} onChange={e => setZone({ ...zone, type: e.target.value })}><option value="TRANSIENT">Vãng lai</option><option value="FIXED">Cố định</option></select></Field>
          <Field label="Bố cục"><select value={zone.layout} onChange={e => setZone({ ...zone, layout: e.target.value })}><option value="INSIDE">Bên trong</option><option value="EXTERNAL">Bên ngoài</option></select></Field>
          <label className="staff-check"><input type="checkbox" checked={zone.isActive} onChange={e => setZone({ ...zone, isActive: e.target.checked })} />Đang khai thác</label>
          <button className="staff-button staff-button--primary">Lưu khu</button>
          {editId && <button type="button" className="staff-button" onClick={() => { setEdit(null); setZone({ code: "", name: "", type: "TRANSIENT", layout: "INSIDE", isActive: true }); }}>Chuyển sang thêm khu</button>}
        </fieldset></form>
      </section>
      <section className="staff-panel"><h2>Chính sách có phiên bản</h2>
        <p>Để trống tham số chưa chốt và lưu nháp. Khi duyệt, ghi rõ nguồn quyết định. Phiên cũ giữ nguyên phiên bản đã áp dụng.</p>
        <form onSubmit={e => { e.preventDefault(); perform(async () => {
          const body = { ...policy, zoneId: policy.zoneId ? Number(policy.zoneId) : null,
            effectiveFrom: new Date(policy.effectiveFrom).toISOString() };
          for (const [key] of numeric) body[key] = policy[key] === "" || policy[key] == null ? null : Number(policy[key]);
          for (const [key] of choices) body[key] = policy[key] || null;
          await operationsApi(lot.id, "policies", body); patchPolicy("approve", false);
        }); }}><fieldset className="operations-fields" disabled={busy}>
          <Field label="Phạm vi"><select value={policy.zoneId} onChange={e => patchPolicy("zoneId", e.target.value)}><option value="">Toàn bãi</option>{query.data?.zones.map(z => <option key={z.id} value={z.id}>{z.code}</option>)}</select></Field>
          <Field label="Bắt đầu có hiệu lực (giờ máy của bạn)"><input type="datetime-local" required value={policy.effectiveFrom} onChange={e => patchPolicy("effectiveFrom", e.target.value)} /></Field>
          <div className="operations-form-grid">{numeric.map(([key, label]) => <Field label={label} key={key}><input type="number" min={key === "holdMinutes" || key === "feeUnitMinutes" ? 1 : 0} step={key === "depositAmount" ? "0.01" : 1} value={policy[key] ?? ""} onChange={e => patchPolicy(key, e.target.value)} /></Field>)}</div>
          {choices.map(([key, label, options]) => <Field key={key} label={label}><select value={policy[key] || ""} onChange={e => patchPolicy(key, e.target.value)}><option value="">Chưa chốt</option>{options.map(([value, name]) => <option key={value} value={value}>{name}</option>)}</select></Field>)}
          <p>Mốc trả ô: sau xác nhận xe thực sự ra bãi.</p>
          <Field label="Nguồn quyết định / ghi chú"><textarea maxLength={500} value={policy.decisionReference} onChange={e => patchPolicy("decisionReference", e.target.value)} /></Field>
          <label className="staff-check"><input type="checkbox" checked={policy.approve} onChange={e => patchPolicy("approve", e.target.checked)} />Tôi xác nhận quyết định đã được duyệt, cho áp dụng từ thời điểm trên.</label>
          <button className="staff-button staff-button--primary">{policy.approve ? "Tạo phiên bản được duyệt" : "Lưu phiên bản nháp"}</button>
        </fieldset></form>
        <h3>Lịch sử phiên bản</h3>{query.data?.policies.map(p => <details key={p.id}><summary>#{p.id} · {p.approvedAt ? "Đã duyệt" : "Nháp"} · {new Date(p.effectiveFrom).toLocaleString("vi-VN")}</summary><p>{p.decisionReference || "Chưa có nguồn quyết định"}</p><dl>{numeric.map(([key, label]) => <div key={key}><dt>{label}</dt><dd>{p[key] ?? "Chưa chốt"}</dd></div>)}</dl></details>)}
      </section>
      <section className="staff-panel"><h2>Tầng và ô</h2><p>Quan hệ khu được lấy từ tầng. Không chuyển tầng có xe hoặc lịch sử phiên sang khu khác.</p>
        {query.data?.layout.floors.map(f => <div key={f.id} className="operations-floor"><strong>{f.name} · #{f.id}</strong>
          <Field label="Khu sở hữu"><select value={f.zoneId || ""} disabled={busy} onChange={e => perform(() => operationsApi(lot.id, `floors/${f.id}/zone`, { zoneId: Number(e.target.value) }, "PUT"))}><option value="" disabled>Chưa gắn khu</option>{query.data.zones.map(z => <option key={z.id} value={z.id}>{z.code}</option>)}</select></Field>
          <button className="staff-button" disabled={busy} onClick={() => perform(() => apiRequest(`/v1/parking-floors/${f.id}`, { method: "PUT", body: JSON.stringify({ floorNumber: f.floorNumber, name: f.name, totalSlots: query.data.layout.slots.filter(s => s.floorId === f.id).length, isActive: !f.isActive }) }))}>{f.isActive ? "Ngừng dùng tầng" : "Mở lại tầng"}</button>
        </div>)}
        <form onSubmit={e => { e.preventDefault(); perform(async () => {
          let floor = createdFloor;
          if (!floor) { const result = await apiRequest(`/v1/parking-lots/${lot.id}/floors`, { method: "POST", body: JSON.stringify({ zoneId: Number(floorDraft.zoneId), name: floorDraft.name, floorNumber: Number(floorDraft.floorNumber), totalSlots: 0, isActive: true }) }); floor = result.data; setCreatedFloor(floor); }
          await operationsApi(lot.id, `floors/${floor.id}/zone`, { zoneId: Number(floorDraft.zoneId) }, "PUT"); setCreatedFloor(null); setFloorDraft({ name: "", floorNumber: "", zoneId: "" });
        }); }}><fieldset disabled={busy} className="operations-fields"><h3>Thêm tầng</h3>
          <Field label="Tên tầng"><input required disabled={Boolean(createdFloor)} value={floorDraft.name} onChange={e => setFloorDraft({ ...floorDraft, name: e.target.value })} /></Field>
          <Field label="Số tầng"><input type="number" min={-10} max={100} required disabled={Boolean(createdFloor)} value={floorDraft.floorNumber} onChange={e => setFloorDraft({ ...floorDraft, floorNumber: e.target.value })} /></Field>
          <Field label="Khu của tầng"><select required value={floorDraft.zoneId} onChange={e => setFloorDraft({ ...floorDraft, zoneId: e.target.value })}><option value="">Chọn khu</option>{query.data?.zones.filter(z => z.isActive).map(z => <option key={z.id} value={z.id}>{z.code}</option>)}</select></Field>
          <button className="staff-button">{createdFloor ? "Gắn khu cho tầng vừa tạo" : "Tạo tầng và gắn khu"}</button>
        </fieldset></form>
        <form onSubmit={e => { e.preventDefault(); perform(async () => {
          const body = { slotCode: slotDraft.slotCode, physicalStatus: "EMPTY", isActive: true };
          for (const key of ["positionX", "positionY", "positionZ"]) body[key] = slotDraft[key] === "" ? null : Number(slotDraft[key]);
          await apiRequest(`/v1/parking-floors/${slotDraft.floorId}/slots`, { method: "POST", body: JSON.stringify(body) }); setSlotDraft({ ...slotDraft, slotCode: "" });
        }); }}><fieldset disabled={busy} className="operations-fields"><h3>Thêm ô</h3>
          <Field label="Tầng"><select required value={slotDraft.floorId} onChange={e => setSlotDraft({ ...slotDraft, floorId: e.target.value })}><option value="">Chọn tầng</option>{query.data?.layout.floors.filter(f => f.zoneId).map(f => <option key={f.id} value={f.id}>{f.name}</option>)}</select></Field>
          <Field label="Mã ô"><input required maxLength={20} value={slotDraft.slotCode} onChange={e => setSlotDraft({ ...slotDraft, slotCode: e.target.value })} /></Field>
          <div className="operations-form-grid">{["X", "Y", "Z"].map(axis => <Field key={axis} label={`Tọa độ ${axis}`}><input type="number" step="0.01" value={slotDraft[`position${axis}`]} onChange={e => setSlotDraft({ ...slotDraft, [`position${axis}`]: e.target.value })} /></Field>)}</div>
          <button className="staff-button">Tạo ô</button>
        </fieldset></form>
        <h3>Tương thích nhiên liệu</h3><div className="operations-slot-grid">{query.data?.layout.slots.map(s => <Field key={s.id} label={`${s.floorName} · ${s.slotCode}`}><select disabled={busy} value={s.allowedFuelType} onChange={e => perform(() => operationsApi(lot.id, `slots/${s.id}/fuel`, { allowedFuelType: e.target.value }, "PUT"))}><option value="ANY">Mọi loại</option><option value="PETROL">Xăng/dầu</option><option value="ELECTRIC">Điện</option><option value="HYBRID">Hybrid</option></select></Field>)}</div>
      </section>
      <section className="staff-panel"><h2>Xử lý vé mất hoặc thu hồi</h2>
        <form onSubmit={e => { e.preventDefault(); action.run(async () => setTicketResult(await operationsApi(lot.id, `tickets/${encodeURIComponent(ticket.code.trim())}`))); }}><Field label="Mã vé"><input required disabled={busy} value={ticket.code} onChange={e => { setTicket({ ...ticket, code: e.target.value }); setTicketResult(null); }} /></Field><button className="staff-button" disabled={busy}>Tra vé</button></form>
        {ticketResult && <p>Vé {ticketResult.ticketCode} · {ticketResult.ticketStatus} · Lượt #{ticketResult.id} · {ticketResult.plateNumber || "Chưa xác minh"}</p>}
        <form onSubmit={e => { e.preventDefault(); perform(async () => { await operationsApi(lot.id, `tickets/${encodeURIComponent(ticket.code.trim())}/change`, ticket); setTicketResult(null); }); }}><fieldset className="operations-fields" disabled={busy || !ticketResult}>
          <Field label="Thao tác"><select value={ticket.action} onChange={e => setTicket({ ...ticket, action: e.target.value })}><option value="REPLACE">Thay vé sau đối chiếu</option><option value="REVOKE">Thu hồi vé</option></select></Field>
          {ticket.action === "REPLACE" && <Field label="Mã vé thay thế"><input required maxLength={40} value={ticket.replacementCode} onChange={e => setTicket({ ...ticket, replacementCode: e.target.value })} /></Field>}
          <Field label="Lý do và kết quả đối chiếu"><textarea required maxLength={500} value={ticket.reason} onChange={e => setTicket({ ...ticket, reason: e.target.value })} /></Field>
          <button className="staff-button staff-button--primary">Ghi nhận xử lý vé</button>
        </fieldset></form>
      </section>
    </div>
  </>;
}
function Content() {
  const load = useCallback(signal => collectPages(page => apiRequest(`/v1/parking-lots?pageNumber=${page}&pageSize=100`, { signal }).then(r => r.data)), []);
  const lots = useStaffQuery(load);
  const [selected, select] = useState("");
  const [busy, setBusy] = useState(false);
  const lot = lots.data?.find(l => String(l.id) === selected) || lots.data?.[0];
  return <div className="staff-workspace container"><header><Link to="/admin">← Tổng quan</Link><h1>Khu bãi và chính sách vận hành</h1><p>Quản lý cấu trúc, tham số có hiệu lực và lịch sử vé tại từng bãi.</p></header>
    <StaffError error={lots.error} />{lots.error && <button className="staff-button" onClick={lots.reload}>Tải lại bãi</button>}
    {lots.loading && <Notice>Đang tải bãi…</Notice>}
    {!lots.loading && !lots.error && !lots.data?.length && <Notice>Chưa có bãi. Khai báo bãi trước khi cấu hình khu.</Notice>}
    {lot && <><Field label="Bãi cần cấu hình"><select disabled={busy} value={lot.id} onChange={e => select(e.target.value)}>{lots.data.map(l => <option key={l.id} value={l.id}>{l.name}</option>)}</select></Field><LotSettings lot={lot} key={lot.id} onBusyChange={setBusy} /></>}
  </div>;
}
export default function AdminParkingSettings() { return <RequireRole role="ADMIN"><Content /></RequireRole>; }
