import { useCallback, useEffect, useRef, useState } from "react";
import { Link, useSearchParams } from "react-router-dom";
import RequireRole from "../components/RequireRole.jsx";
import { apiRequest } from "../services/api.js";
import { operationsApi } from "../services/operationsApi.js";
import { collectPages } from "../services/staffApi.js";
import { Notice, StaffError, StaffIcon, useStaffAction, useStaffQuery } from "../components/staff/StaffUI.jsx";
import "../staff.css";
import "../operations.css";

const Field = ({ label, children }) => <label className="staff-field">{label}{children}</label>;
const views = [
  { id: "zones", name: "Khu bãi", icon: "pin", description: "Quản lý khu bình thường và khu dành cho xe có vé tháng." },
  { id: "policies", name: "Chính sách", icon: "dispatches", description: "Thiết lập quy định vận hành và xem lịch sử phiên bản." },
  { id: "floors", name: "Tầng", icon: "floors", description: "Thêm tầng, gắn khu và quản lý trạng thái khai thác." },
  { id: "slots", name: "Ô đỗ", icon: "slots", description: "Thêm ô đỗ và cấu hình loại nhiên liệu phù hợp." },
  { id: "tickets", name: "Xử lý vé", icon: "tickets", description: "Tra cứu, thay vé mất hoặc thu hồi vé sau đối chiếu." },
];
const zoneTypes = { TRANSIENT: "Bình thường", FIXED: "Vé tháng" };
const emptyZone = () => ({ code: "", name: "", type: "TRANSIENT", layout: "INSIDE", isActive: true, floorIds: [], newFloor: null });
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
function LotSettings({ lot, view, onBusyChange }) {
  const load = useCallback(async signal => {
    const [zones, policies, layout] = await Promise.all(["zones", "policies", "layout"].map(path => operationsApi(lot.id, path, null, null, signal)));
    return { zones, policies, layout };
  }, [lot.id]);
  const query = useStaffQuery(load);
  const action = useStaffAction(onBusyChange);
  const [message, setMessage] = useState("");
  const [zone, setZone] = useState(emptyZone);
  const [editId, setEdit] = useState(null);
  const [policy, setPolicy] = useState({ zoneId: "", effectiveFrom: "", decisionReference: "", approve: false });
  const [ticket, setTicket] = useState({ code: "", action: "REPLACE", replacementCode: "", reason: "" });
  const [ticketResult, setTicketResult] = useState(null);
  const [floorDraft, setFloorDraft] = useState({ name: "", floorNumber: "", zoneId: "" });
  const [createdFloor, setCreatedFloor] = useState(null);
  const [slotDraft, setSlotDraft] = useState({ floorId: "", slotCode: "", positionX: "", positionY: "", positionZ: "" });
  useEffect(() => { setMessage(""); action.setError(null); }, [view.id, action.setError]);
  const busy = action.busy || query.loading || Boolean(query.error);
  const perform = work => action.run(async () => { setMessage(""); await work(); setMessage("Đã lưu vào máy chủ."); query.reload(); });
  const patchPolicy = (key, value) => setPolicy(p => ({ ...p, [key]: value }));
  return <>
    <div className="operations-settings-toolbar">
      <p>{view.description}</p>
      <button type="button" className="staff-button staff-button--secondary" disabled={action.busy || query.loading} onClick={query.reload}>
        <StaffIcon name="refresh" />{query.loading ? "Đang tải…" : "Tải lại cấu hình"}
      </button>
    </div>
    <StaffError error={query.error || action.error} />
    {message && <Notice kind="success">{message}</Notice>}
    <div className="operations-settings-panels">
      {view.id === "zones" && <section className="staff-panel"><div><h2>Khu bãi</h2><p className="operations-settings-hint">Đóng khu để ngừng nhận xe mới. Các lượt xe đang gửi vẫn được giữ nguyên.</p></div>
        <div className="operations-settings-columns">
        <div><h3>Danh sách khu</h3>
          {!query.loading && !query.error && !query.data?.zones.length && <Notice>Chưa có khu. Thêm khu bình thường hoặc khu vé tháng để bắt đầu.</Notice>}
          <div className="operations-session-list">{query.data?.zones.map(z => <button type="button" className={`operations-session ${editId === z.id ? "is-selected" : ""}`} aria-pressed={editId === z.id} disabled={action.busy} key={z.id} onClick={() => { setEdit(z.id); setZone({ code: z.code, name: z.name, type: z.type, layout: z.layout || "INSIDE", isActive: z.isActive, floorIds: z.floorIds, newFloor: null }); }}>
            <strong>{z.code} · {z.name}</strong><span>{zoneTypes[z.type] || z.type} · {z.isActive ? "Đang khai thác" : "Đã đóng"}</span><small>{z.floorIds.length ? `Tầng: ${z.floorIds.map(id => query.data.layout.floors.find(f => f.id === id)?.name || `#${id}`).join(", ")}` : "Chưa gắn tầng"} · Chọn để chỉnh sửa</small>
          </button>)}</div>
        </div>
        <form onSubmit={e => { e.preventDefault(); perform(async () => {
          await operationsApi(lot.id, `zones${editId ? `/${editId}` : ""}`, {
            ...zone, newFloor: zone.newFloor ? { ...zone.newFloor, floorNumber: Number(zone.newFloor.floorNumber) } : null,
          }, editId ? "PUT" : "POST");
          setEdit(null); setZone(emptyZone());
        }); }}><fieldset className="operations-fields" disabled={busy}>
          <h3>{editId ? `Sửa khu #${editId}` : "Thêm khu"}</h3>
          <div className="operations-form-grid">
            <Field label="Mã khu"><input required maxLength={30} value={zone.code} onChange={e => setZone({ ...zone, code: e.target.value })} /></Field>
            <Field label="Tên khu"><input required maxLength={100} value={zone.name} onChange={e => setZone({ ...zone, name: e.target.value })} /></Field>
          </div>
          <Field label="Loại khu"><select value={zone.type} onChange={e => setZone({ ...zone, type: e.target.value })}><option value="TRANSIENT">Bình thường</option><option value="FIXED">Vé tháng</option></select></Field>
          <fieldset className="operations-zone-floors"><legend>Tầng của khu</legend>
            <p className="operations-settings-hint">Chọn ít nhất một tầng chưa gắn khu hoặc tạo tầng mới.</p>
            <div className="operations-zone-floor-options">{query.data?.layout.floors.map(f => <label key={f.id} className="staff-check">
              <input type="checkbox" checked={zone.floorIds.includes(f.id)} disabled={Boolean(f.zoneId)} onChange={e => setZone({ ...zone, floorIds: e.target.checked ? [...zone.floorIds, f.id] : zone.floorIds.filter(id => id !== f.id) })} />
              {f.name}{f.zoneId && f.zoneId !== editId ? ` · Đã thuộc khu ${query.data.zones.find(z => z.id === f.zoneId)?.code || f.zoneId}` : ""}
            </label>)}</div>
            <label className="staff-check"><input type="checkbox" checked={Boolean(zone.newFloor)} onChange={e => setZone({ ...zone, newFloor: e.target.checked ? { name: "", floorNumber: "" } : null })} />Tạo tầng mới cho khu</label>
            {zone.newFloor && <div className="operations-form-grid">
              <Field label="Tên tầng mới"><input required maxLength={50} value={zone.newFloor.name} onChange={e => setZone({ ...zone, newFloor: { ...zone.newFloor, name: e.target.value } })} /></Field>
              <Field label="Số tầng mới"><input type="number" min={-10} max={100} required value={zone.newFloor.floorNumber} onChange={e => setZone({ ...zone, newFloor: { ...zone.newFloor, floorNumber: e.target.value } })} /></Field>
            </div>}
          </fieldset>
          <label className="staff-check"><input type="checkbox" checked={zone.isActive} onChange={e => setZone({ ...zone, isActive: e.target.checked })} />Đang khai thác</label>
          <div className="operations-settings-actions"><button className="staff-button staff-button--primary" disabled={!zone.floorIds.length && !zone.newFloor}>Lưu khu</button>
          {editId && <button type="button" className="staff-button staff-button--secondary" onClick={() => { setEdit(null); setZone(emptyZone()); }}>Chuyển sang thêm khu</button>}</div>
        </fieldset></form>
        </div>
      </section>}
      {view.id === "policies" && <section className="staff-panel"><div><h2>Chính sách vận hành</h2>
        <p className="operations-settings-hint">Lưu nháp khi chưa chốt đủ tham số. Phiên gửi cũ giữ nguyên chính sách đã áp dụng.</p></div>
        <div className="operations-settings-columns operations-settings-columns--policy">
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
          <div className="operations-form-grid">{choices.map(([key, label, options]) => <Field key={key} label={label}><select value={policy[key] || ""} onChange={e => patchPolicy(key, e.target.value)}><option value="">Chưa chốt</option>{options.map(([value, name]) => <option key={value} value={value}>{name}</option>)}</select></Field>)}</div>
          <p>Mốc trả ô: sau xác nhận xe thực sự ra bãi.</p>
          <Field label="Nguồn quyết định / ghi chú"><textarea maxLength={500} value={policy.decisionReference} onChange={e => patchPolicy("decisionReference", e.target.value)} /></Field>
          <label className="staff-check"><input type="checkbox" checked={policy.approve} onChange={e => patchPolicy("approve", e.target.checked)} />Tôi xác nhận quyết định đã được duyệt, cho áp dụng từ thời điểm trên.</label>
          <button className="staff-button staff-button--primary">{policy.approve ? "Tạo phiên bản được duyệt" : "Lưu phiên bản nháp"}</button>
        </fieldset></form>
        <div className="operations-policy-history"><h3>Lịch sử phiên bản</h3>
          {!query.loading && !query.error && !query.data?.policies.length && <Notice>Chưa có phiên bản chính sách.</Notice>}
          {query.data?.policies.map(p => <details key={p.id}><summary>#{p.id} · {p.approvedAt ? "Đã duyệt" : "Nháp"} · {new Date(p.effectiveFrom).toLocaleString("vi-VN")}</summary><p>{p.decisionReference || "Chưa có nguồn quyết định"}</p><dl>{numeric.map(([key, label]) => <div key={key}><dt>{label}</dt><dd>{p[key] ?? "Chưa chốt"}</dd></div>)}</dl></details>)}
        </div></div>
      </section>}
      {view.id === "floors" && <section className="staff-panel"><div><h2>Quản lý tầng</h2><p className="operations-settings-hint">Gắn mỗi tầng với một khu. Tầng có xe hoặc lịch sử phiên không được chuyển khu.</p></div>
        <div className="operations-settings-columns">
        <div><h3>Danh sách tầng</h3>
        {!query.loading && !query.error && !query.data?.layout.floors.length && <Notice>Chưa có tầng trong bãi này.</Notice>}
        {query.data?.layout.floors.map(f => <div key={f.id} className="operations-floor"><strong>{f.name} · #{f.id}</strong>
          <Field label="Khu sở hữu"><select value={f.zoneId || ""} disabled={busy} onChange={e => perform(() => operationsApi(lot.id, `floors/${f.id}/zone`, { zoneId: Number(e.target.value) }, "PUT"))}><option value="" disabled>Chưa gắn khu</option>{query.data.zones.map(z => <option key={z.id} value={z.id}>{z.code}</option>)}</select></Field>
          <button className="staff-button" disabled={busy} onClick={() => perform(() => apiRequest(`/v1/parking-floors/${f.id}`, { method: "PUT", body: JSON.stringify({ floorNumber: f.floorNumber, name: f.name, totalSlots: query.data.layout.slots.filter(s => s.floorId === f.id).length, isActive: !f.isActive }) }))}>{f.isActive ? "Ngừng dùng tầng" : "Mở lại tầng"}</button>
        </div>)}</div>
        <form onSubmit={e => { e.preventDefault(); perform(async () => {
          let floor = createdFloor;
          if (!floor) { const result = await apiRequest(`/v1/parking-lots/${lot.id}/floors`, { method: "POST", body: JSON.stringify({ zoneId: Number(floorDraft.zoneId), name: floorDraft.name, floorNumber: Number(floorDraft.floorNumber), totalSlots: 0, isActive: true }) }); floor = result.data; setCreatedFloor(floor); }
          await operationsApi(lot.id, `floors/${floor.id}/zone`, { zoneId: Number(floorDraft.zoneId) }, "PUT"); setCreatedFloor(null); setFloorDraft({ name: "", floorNumber: "", zoneId: "" });
        }); }}><fieldset disabled={busy} className="operations-fields"><h3>Thêm tầng</h3>
          <Field label="Tên tầng"><input required disabled={Boolean(createdFloor)} value={floorDraft.name} onChange={e => setFloorDraft({ ...floorDraft, name: e.target.value })} /></Field>
          <Field label="Số tầng"><input type="number" min={-10} max={100} required disabled={Boolean(createdFloor)} value={floorDraft.floorNumber} onChange={e => setFloorDraft({ ...floorDraft, floorNumber: e.target.value })} /></Field>
          <Field label="Khu của tầng"><select required value={floorDraft.zoneId} onChange={e => setFloorDraft({ ...floorDraft, zoneId: e.target.value })}><option value="">Chọn khu</option>{query.data?.zones.filter(z => z.isActive).map(z => <option key={z.id} value={z.id}>{z.code}</option>)}</select></Field>
          <button className="staff-button">{createdFloor ? "Gắn khu cho tầng vừa tạo" : "Tạo tầng và gắn khu"}</button>
        </fieldset></form></div>
      </section>}
      {view.id === "slots" && <section className="staff-panel"><div><h2>Quản lý ô đỗ</h2><p className="operations-settings-hint">Thêm ô vào tầng đã gắn khu và chọn loại nhiên liệu được phép.</p></div>
        <div className="operations-settings-columns">
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
        <div><h3>Tương thích nhiên liệu</h3>
          {!query.loading && !query.error && !query.data?.layout.slots.length && <Notice>Chưa có ô đỗ trong bãi này.</Notice>}
          <div className="operations-slot-grid">{query.data?.layout.slots.map(s => <Field key={s.id} label={`${s.floorName} · ${s.slotCode}`}><select disabled={busy} value={s.allowedFuelType} onChange={e => perform(() => operationsApi(lot.id, `slots/${s.id}/fuel`, { allowedFuelType: e.target.value }, "PUT"))}><option value="ANY">Mọi loại</option><option value="PETROL">Xăng/dầu</option><option value="ELECTRIC">Điện</option><option value="HYBRID">Hybrid</option></select></Field>)}</div>
        </div></div>
      </section>}
      {view.id === "tickets" && <section className="staff-panel"><div><h2>Xử lý vé mất hoặc thu hồi</h2><p className="operations-settings-hint">Tra cứu vé trước khi thay hoặc thu hồi. Ghi rõ lý do và kết quả đối chiếu.</p></div>
        <div className="operations-settings-columns"><div>
        <form onSubmit={e => { e.preventDefault(); action.run(async () => setTicketResult(await operationsApi(lot.id, `tickets/${encodeURIComponent(ticket.code.trim())}`))); }}><Field label="Mã vé"><input required disabled={busy} value={ticket.code} onChange={e => { setTicket({ ...ticket, code: e.target.value }); setTicketResult(null); }} /></Field><button className="staff-button" disabled={busy}>Tra vé</button></form>
        {ticketResult && <p>Vé {ticketResult.ticketCode} · {ticketResult.ticketStatus} · Lượt #{ticketResult.id} · {ticketResult.plateNumber || "Chưa xác minh"}</p>}
        </div>
        <form onSubmit={e => { e.preventDefault(); perform(async () => { await operationsApi(lot.id, `tickets/${encodeURIComponent(ticket.code.trim())}/change`, ticket); setTicketResult(null); }); }}><fieldset className="operations-fields" disabled={busy || !ticketResult}>
          <Field label="Thao tác"><select value={ticket.action} onChange={e => setTicket({ ...ticket, action: e.target.value })}><option value="REPLACE">Thay vé sau đối chiếu</option><option value="REVOKE">Thu hồi vé</option></select></Field>
          {ticket.action === "REPLACE" && <Field label="Mã vé thay thế"><input required maxLength={40} value={ticket.replacementCode} onChange={e => setTicket({ ...ticket, replacementCode: e.target.value })} /></Field>}
          <Field label="Lý do và kết quả đối chiếu"><textarea required maxLength={500} value={ticket.reason} onChange={e => setTicket({ ...ticket, reason: e.target.value })} /></Field>
          <button className="staff-button staff-button--primary">Ghi nhận xử lý vé</button>
        </fieldset></form></div>
      </section>}
    </div>
  </>;
}
function Content() {
  const [params, setParams] = useSearchParams();
  const view = views.find(item => item.id === params.get("view")) || views[0];
  const tabRefs = useRef({});
  const load = useCallback(signal => collectPages(page => apiRequest(`/v1/parking-lots?pageNumber=${page}&pageSize=100`, { signal }).then(r => r.data)), []);
  const lots = useStaffQuery(load);
  const [selected, select] = useState("");
  const [busy, setBusy] = useState(false);
  const lot = lots.data?.find(l => String(l.id) === selected) || lots.data?.[0];
  const openView = id => setParams(previous => { const next = new URLSearchParams(previous); next.set("view", id); return next; });
  return <div className="staff-workspace operations-settings container"><header className="staff-workspace-heading"><div><Link className="operations-settings-back" to="/admin">← Tổng quan</Link><h1>Khu & chính sách</h1><p>Chọn bãi và mục cần quản lý.</p></div></header>
    <StaffError error={lots.error} />{lots.error && <button className="staff-button" onClick={lots.reload}>Tải lại bãi</button>}
    {lots.loading && <Notice>Đang tải bãi…</Notice>}
    {!lots.loading && !lots.error && !lots.data?.length && <Notice>Chưa có bãi. Khai báo bãi trước khi cấu hình khu.</Notice>}
    {lot && <>
      <section className="staff-context-bar" aria-label="Bãi đang cấu hình"><div className="staff-context-select"><StaffIcon name="pin" size={23} />
        <label htmlFor="settings-lot">Bãi cần cấu hình<select id="settings-lot" disabled={busy} value={lot.id} onChange={e => select(e.target.value)}>{lots.data.map(l => <option key={l.id} value={l.id}>{l.name}</option>)}</select></label>
      </div></section>
      <div className="staff-tabs" role="tablist" aria-label="Quản lý khu và chính sách">
        {views.map((item, index) => <button key={item.id} type="button" role="tab" id={`settings-tab-${item.id}`} aria-controls={`settings-panel-${item.id}`} aria-selected={view.id === item.id} tabIndex={view.id === item.id ? 0 : -1} disabled={busy}
          ref={node => { tabRefs.current[item.id] = node; }} onClick={() => openView(item.id)} onKeyDown={event => {
            const nextIndex = event.key === "ArrowRight" ? (index + 1) % views.length : event.key === "ArrowLeft" ? (index + views.length - 1) % views.length : event.key === "Home" ? 0 : event.key === "End" ? views.length - 1 : null;
            if (nextIndex === null || busy) return;
            event.preventDefault(); openView(views[nextIndex].id); tabRefs.current[views[nextIndex].id]?.focus();
          }}><StaffIcon name={item.icon} /><span>{item.name}</span></button>)}
      </div>
      <section className="operations-settings-view" role="tabpanel" id={`settings-panel-${view.id}`} aria-labelledby={`settings-tab-${view.id}`} tabIndex={0}>
        <LotSettings lot={lot} view={view} key={lot.id} onBusyChange={setBusy} />
      </section>
    </>}
  </div>;
}
export default function AdminParkingSettings() { return <RequireRole role="ADMIN"><Content /></RequireRole>; }
