import { useCallback, useEffect, useRef, useState } from "react";
import { useSearchParams } from "react-router-dom";
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
  { id: "floors", name: "Tầng", icon: "floors", description: "Thêm, sửa, xóa tầng, gắn khu và quản lý trạng thái khai thác." },
  { id: "slots", name: "Ô đỗ", icon: "slots", description: "Thêm, sửa, xóa ô đỗ (slot), đổi trạng thái và cấu hình loại nhiên liệu." },
  { id: "tickets", name: "Xử lý vé", icon: "tickets", description: "Tra cứu, thay vé mất hoặc thu hồi vé sau đối chiếu." },
];
const zoneTypes = { TRANSIENT: "Bình thường", FIXED: "Vé tháng" };
const slotStatuses = { EMPTY: "Trống", OCCUPIED: "Có xe", RESERVED: "Đang giữ chỗ", MAINTENANCE: "Bảo trì" };
const emptyZone = () => ({ code: "", name: "", type: "TRANSIENT", layout: "INSIDE", isActive: true, floorIds: [], newFloor: null });
const emptySlotDraft = (defaultFloorId = "") => ({
  floorId: defaultFloorId,
  slotCode: "",
  physicalStatus: "EMPTY",
  positionX: "",
  positionY: "",
  positionZ: "",
  isActive: true,
});

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
  const [ticket, setTicket] = useState({ code: "", action: "REPLACE", replacementCode: "", reason: "" });
  const [ticketResult, setTicketResult] = useState(null);
  const [floorDraft, setFloorDraft] = useState({ name: "", floorNumber: "", zoneId: "" });
  const [editingFloor, setEditingFloor] = useState(null);
  const [createdFloor, setCreatedFloor] = useState(null);
  const [slotDraft, setSlotDraft] = useState(() => emptySlotDraft(""));
  const [editingSlot, setEditingSlot] = useState(null);
  const [slotFloorFilter, setSlotFloorFilter] = useState("");

  useEffect(() => { setMessage(""); action.setError(null); }, [view.id, action.setError]);
  const busy = action.busy || query.loading || Boolean(query.error);
  const perform = work => action.run(async () => { setMessage(""); await work(); setMessage("Đã lưu vào máy chủ."); query.reload(); });

  const allFloors = query.data?.layout?.floors || [];
  const allSlots = query.data?.layout?.slots || [];
  const filteredSlots = slotFloorFilter
    ? allSlots.filter(s => String(s.floorId) === String(slotFloorFilter))
    : allSlots;

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

      {view.id === "floors" && <section className="staff-panel"><div><h2>Quản lý tầng</h2><p className="operations-settings-hint">Thêm, sửa, xóa tầng và gắn tầng với khu vực. Tầng đang có xe hoặc lịch sử phiên không được xóa.</p></div>
        <div className="operations-settings-columns">
        <div><h3>Danh sách tầng ({allFloors.length})</h3>
        {!query.loading && !query.error && !allFloors.length && <Notice>Chưa có tầng trong bãi này.</Notice>}
        {allFloors.map(f => {
          const floorSlotCount = allSlots.filter(s => s.floorId === f.id).length;
          return <div key={f.id} className="operations-floor" style={{ display: "grid", gap: "8px" }}>
            <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", flexWrap: "wrap", gap: "6px" }}>
              <strong>{f.name} · Tầng số {f.floorNumber ?? "—"} ({floorSlotCount} ô)</strong>
              <span style={{ fontSize: "12px", color: f.isActive ? "#15803d" : "#64748b", fontWeight: 600 }}>
                {f.isActive ? "Đang hoạt động" : "Ngừng dùng"}
              </span>
            </div>
            <Field label="Khu sở hữu"><select value={f.zoneId || ""} disabled={busy} onChange={e => perform(() => operationsApi(lot.id, `floors/${f.id}/zone`, { zoneId: Number(e.target.value) }, "PUT"))}><option value="" disabled>Chưa gắn khu</option>{query.data.zones.map(z => <option key={z.id} value={z.id}>{z.code} · {z.name}</option>)}</select></Field>
            <div className="operations-settings-actions" style={{ display: "flex", gap: "8px", flexWrap: "wrap" }}>
              <button type="button" className="staff-button staff-button--secondary" disabled={busy} onClick={() => {
                setEditingFloor(f);
                setFloorDraft({ name: f.name, floorNumber: String(f.floorNumber ?? 1), zoneId: String(f.zoneId || "") });
              }}>Sửa tầng</button>
              <button type="button" className="staff-button" disabled={busy} onClick={() => perform(() => apiRequest(`/v1/parking-floors/${f.id}`, { method: "PUT", body: JSON.stringify({ floorNumber: f.floorNumber, name: f.name, totalSlots: floorSlotCount, isActive: !f.isActive }) }))}>{f.isActive ? "Ngừng dùng tầng" : "Mở lại tầng"}</button>
              <button type="button" className="staff-button staff-button--secondary" disabled={busy || floorSlotCount > 0} title={floorSlotCount > 0 ? "Chỉ xóa được tầng chưa có ô đỗ" : "Xóa tầng"} onClick={() => perform(() => apiRequest(`/v1/parking-floors/${f.id}`, { method: "DELETE" }))}>Xóa tầng</button>
            </div>
          </div>;
        })}</div>
        <form onSubmit={e => { e.preventDefault(); perform(async () => {
          if (editingFloor) {
            const slotCount = allSlots.filter(s => s.floorId === editingFloor.id).length;
            await apiRequest(`/v1/parking-floors/${editingFloor.id}`, {
              method: "PUT",
              body: JSON.stringify({
                name: floorDraft.name.trim(),
                floorNumber: Number(floorDraft.floorNumber),
                totalSlots: slotCount,
                isActive: editingFloor.isActive,
              }),
            });
            if (floorDraft.zoneId && Number(floorDraft.zoneId) !== editingFloor.zoneId) {
              await operationsApi(lot.id, `floors/${editingFloor.id}/zone`, { zoneId: Number(floorDraft.zoneId) }, "PUT");
            }
            setEditingFloor(null);
            setFloorDraft({ name: "", floorNumber: "", zoneId: "" });
            return;
          }
          let floor = createdFloor;
          const chosenZoneId = floorDraft.zoneId ? Number(floorDraft.zoneId) : (query.data?.zones?.find(z => z.isActive)?.id || null);
          if (!floor) {
            const result = await apiRequest(`/v1/parking-lots/${lot.id}/floors`, {
              method: "POST",
              body: JSON.stringify({
                zoneId: chosenZoneId,
                name: floorDraft.name.trim(),
                floorNumber: Number(floorDraft.floorNumber),
                totalSlots: 0,
                isActive: true,
              }),
            });
            floor = result.data;
            setCreatedFloor(floor);
          }
          if (chosenZoneId) {
            await operationsApi(lot.id, `floors/${floor.id}/zone`, { zoneId: chosenZoneId }, "PUT");
          }
          setCreatedFloor(null);
          setFloorDraft({ name: "", floorNumber: "", zoneId: "" });
        }); }}><fieldset disabled={busy} className="operations-fields"><h3>{editingFloor ? `Sửa tầng ${editingFloor.name}` : "Thêm tầng"}</h3>
          <Field label="Tên tầng"><input required disabled={Boolean(createdFloor)} value={floorDraft.name} onChange={e => setFloorDraft({ ...floorDraft, name: e.target.value })} /></Field>
          <Field label="Số tầng"><input type="number" min={-10} max={100} required disabled={Boolean(createdFloor)} value={floorDraft.floorNumber} onChange={e => setFloorDraft({ ...floorDraft, floorNumber: e.target.value })} /></Field>
          <Field label="Khu của tầng"><select value={floorDraft.zoneId} onChange={e => setFloorDraft({ ...floorDraft, zoneId: e.target.value })}><option value="">Tự động gắn khu mặc định</option>{query.data?.zones.filter(z => z.isActive).map(z => <option key={z.id} value={z.id}>{z.code} · {z.name}</option>)}</select></Field>
          <div className="operations-settings-actions">
            <button className="staff-button staff-button--primary">{editingFloor ? "Lưu cập nhật tầng" : createdFloor ? "Gắn khu cho tầng vừa tạo" : "Tạo tầng và gắn khu"}</button>
            {editingFloor && <button type="button" className="staff-button staff-button--secondary" onClick={() => { setEditingFloor(null); setFloorDraft({ name: "", floorNumber: "", zoneId: "" }); }}>Hủy sửa</button>}
          </div>
        </fieldset></form></div>
      </section>}

      {view.id === "slots" && <section className="staff-panel"><div><h2>Quản lý ô đỗ</h2><p className="operations-settings-hint">Thêm, sửa, xóa ô đỗ (slot), cập nhật trạng thái vật lý và cấu hình loại nhiên liệu phù hợp.</p></div>
        <div className="operations-settings-columns">
        <form onSubmit={e => { e.preventDefault(); perform(async () => {
          const body = {
            slotCode: slotDraft.slotCode.trim(),
            isActive: slotDraft.isActive !== false,
          };
          for (const key of ["positionX", "positionY", "positionZ"]) {
            body[key] = slotDraft[key] === "" || slotDraft[key] == null ? null : Number(slotDraft[key]);
          }
          if (editingSlot) {
            await apiRequest(`/v1/parking-slots/${editingSlot.id}`, {
              method: "PUT",
              body: JSON.stringify(body),
            });
            setEditingSlot(null);
            setSlotDraft(emptySlotDraft(slotDraft.floorId));
          } else {
            body.physicalStatus = slotDraft.physicalStatus || "EMPTY";
            await apiRequest(`/v1/parking-floors/${slotDraft.floorId}/slots`, {
              method: "POST",
              body: JSON.stringify(body),
            });
            setSlotDraft({ ...emptySlotDraft(slotDraft.floorId), floorId: slotDraft.floorId });
          }
        }); }}><fieldset disabled={busy} className="operations-fields"><h3>{editingSlot ? `Sửa ô ${editingSlot.slotCode}` : "Thêm ô"}</h3>
          {!editingSlot && (
            <Field label="Tầng"><select required value={slotDraft.floorId} onChange={e => setSlotDraft({ ...slotDraft, floorId: e.target.value })}><option value="">Chọn tầng</option>{allFloors.map(f => <option key={f.id} value={f.id}>{f.name}</option>)}</select></Field>
          )}
          <Field label="Mã ô"><input required maxLength={20} value={slotDraft.slotCode} onChange={e => setSlotDraft({ ...slotDraft, slotCode: e.target.value })} /></Field>
          {!editingSlot && (
            <Field label="Trạng thái ban đầu">
              <select value={slotDraft.physicalStatus || "EMPTY"} onChange={e => setSlotDraft({ ...slotDraft, physicalStatus: e.target.value })}>
                {Object.entries(slotStatuses).map(([val, label]) => <option key={val} value={val}>{label}</option>)}
              </select>
            </Field>
          )}
          <div className="operations-form-grid">{["X", "Y", "Z"].map(axis => <Field key={axis} label={`Tọa độ ${axis}`}><input type="number" step="0.01" value={slotDraft[`position${axis}`] ?? ""} onChange={e => setSlotDraft({ ...slotDraft, [`position${axis}`]: e.target.value })} /></Field>)}</div>
          <label className="staff-check"><input type="checkbox" checked={slotDraft.isActive !== false} onChange={e => setSlotDraft({ ...slotDraft, isActive: e.target.checked })} />Đang sử dụng</label>
          <div className="operations-settings-actions">
            <button className="staff-button staff-button--primary">{editingSlot ? "Lưu ô đỗ" : "Tạo ô"}</button>
            {editingSlot && <button type="button" className="staff-button staff-button--secondary" onClick={() => { setEditingSlot(null); setSlotDraft(emptySlotDraft("")); }}>Hủy sửa</button>}
          </div>
        </fieldset></form>
        <div>
          <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", gap: "10px", flexWrap: "wrap", marginBottom: "10px" }}>
            <h3 style={{ margin: 0 }}>Danh sách ô đỗ ({filteredSlots.length})</h3>
            <select aria-label="Lọc theo tầng" value={slotFloorFilter} onChange={e => setSlotFloorFilter(e.target.value)} style={{ padding: "6px 10px", borderRadius: "6px", border: "1px solid #cbd5e1" }}>
              <option value="">Tất cả các tầng</option>
              {allFloors.map(f => <option key={f.id} value={f.id}>{f.name}</option>)}
            </select>
          </div>
          {!query.loading && !query.error && !filteredSlots.length && <Notice>Chưa có ô đỗ trong bãi/tầng này.</Notice>}
          <div className="operations-slot-grid" style={{ display: "grid", gap: "10px", maxHeight: "480px", overflowY: "auto" }}>
            {filteredSlots.map(s => (
              <div key={s.id} style={{ padding: "10px 12px", borderRadius: "8px", border: "1px solid #e2e8f0", background: "#fff", display: "grid", gap: "8px" }}>
                <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center" }}>
                  <strong>{s.floorName} · {s.slotCode}</strong>
                  <div style={{ display: "flex", gap: "6px" }}>
                    <button type="button" className="staff-button staff-button--secondary" disabled={busy} onClick={() => {
                      setEditingSlot(s);
                      setSlotDraft({
                        floorId: String(s.floorId || ""),
                        slotCode: s.slotCode,
                        physicalStatus: s.physicalStatus || "EMPTY",
                        positionX: s.positionX ?? "",
                        positionY: s.positionY ?? "",
                        positionZ: s.positionZ ?? "",
                        isActive: s.isActive !== false,
                      });
                    }}>Sửa</button>
                    <button type="button" className="staff-button staff-button--secondary" disabled={busy || s.physicalStatus === "OCCUPIED"} onClick={() => perform(() => apiRequest(`/v1/parking-slots/${s.id}`, { method: "DELETE" }))}>Xóa</button>
                  </div>
                </div>
                <div className="operations-form-grid">
                  <Field label="Trạng thái">
                    <select
                      disabled={busy || s.physicalStatus === "OCCUPIED"}
                      value={s.physicalStatus || "EMPTY"}
                      onChange={e => perform(() => apiRequest(`/v1/parking-slots/${s.id}/status`, {
                        method: "PATCH",
                        body: JSON.stringify({ physicalStatus: e.target.value, reason: "Cập nhật từ Quản lý bãi xe" }),
                      }))}
                    >
                      {Object.entries(slotStatuses).map(([val, label]) => <option key={val} value={val}>{label}</option>)}
                    </select>
                  </Field>
                  <Field label={`${s.floorName} · ${s.slotCode}`}>
                    <select disabled={busy} value={s.allowedFuelType || "ANY"} onChange={e => perform(() => operationsApi(lot.id, `slots/${s.id}/fuel`, { allowedFuelType: e.target.value }, "PUT"))}>
                      <option value="ANY">Mọi loại</option>
                      <option value="PETROL">Xăng/dầu</option>
                      <option value="ELECTRIC">Điện</option>
                      <option value="HYBRID">Hybrid</option>
                    </select>
                  </Field>
                </div>
              </div>
            ))}
          </div>
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
  const [showLotModal, setShowLotModal] = useState(false);
  const [editingLot, setEditingLot] = useState(null);
  const [lotForm, setLotForm] = useState({ name: "", address: "", totalCapacity: 100, isActive: true });
  const [lotModalBusy, setLotModalBusy] = useState(false);
  const [lotModalError, setLotModalError] = useState("");
  const [lotActionMessage, setLotActionMessage] = useState("");

  const lot = lots.data?.find(l => String(l.id) === selected) || lots.data?.[0];
  const openView = id => setParams(previous => { const next = new URLSearchParams(previous); next.set("view", id); return next; });

  const openCreateLotModal = () => {
    setEditingLot(null);
    setLotForm({ name: "", address: "", totalCapacity: 100, isActive: true });
    setLotModalError("");
    setShowLotModal(true);
  };

  const openEditLotModal = () => {
    if (!lot) return;
    setEditingLot(lot);
    setLotForm({
      name: lot.name || "",
      address: lot.address || "",
      totalCapacity: lot.configuredCapacity ?? lot.totalCapacity ?? 100,
      isActive: lot.isActive !== false,
    });
    setLotModalError("");
    setShowLotModal(true);
  };

  const handleSaveLot = async (e) => {
    e.preventDefault();
    setLotModalBusy(true);
    setLotModalError("");
    setLotActionMessage("");
    try {
      if (editingLot) {
        await apiRequest(`/v1/parking-lots/${editingLot.id}`, {
          method: "PUT",
          body: JSON.stringify({
            name: lotForm.name.trim(),
            address: lotForm.address.trim() || null,
            totalCapacity: Number(lotForm.totalCapacity) || 0,
            isActive: Boolean(lotForm.isActive),
          }),
        });
        setShowLotModal(false);
        await lots.reload();
        setLotActionMessage(`Đã cập nhật bãi đỗ "${lotForm.name.trim()}".`);
      } else {
        const res = await apiRequest("/v1/parking-lots", {
          method: "POST",
          body: JSON.stringify({
            name: lotForm.name.trim(),
            address: lotForm.address.trim() || null,
            totalCapacity: Number(lotForm.totalCapacity) || 0,
            isActive: Boolean(lotForm.isActive),
          }),
        });
        if (res?.data?.id) {
          try {
            await apiRequest(`/v1/parking-lots/${res.data.id}/floors`, {
              method: "POST",
              body: JSON.stringify({
                name: "Tầng 1",
                floorNumber: 1,
                totalSlots: 0,
                isActive: true,
              }),
            });
            await operationsApi(res.data.id, "policies", {
              zoneId: null,
              effectiveFrom: new Date(Date.now() - 60000).toISOString(),
              approve: true,
              decisionReference: "Quy định vận hành chuẩn mặc định khi khởi tạo bãi đỗ",
              holdMinutes: 15,
              earlyArrivalMinutes: 15,
              lateArrivalMinutes: 10,
              cancellationNoticeMinutes: 30,
              depositAmount: 0,
              nearFullThreshold: 5,
              feeUnitMinutes: 60,
              feeRounding: "CEILING",
              noShowAction: "EXPIRE",
              cancellationAction: "ALLOW",
              rateSelection: "CHECK_IN",
            }, "POST");
          } catch {
            // Ignore optional bootstrap errors
          }
        }
        setShowLotModal(false);
        await lots.reload();
        if (res?.data?.id) {
          select(String(res.data.id));
        }
        setLotActionMessage(`Đã tạo bãi đỗ "${lotForm.name.trim()}".`);
      }
    } catch (err) {
      setLotModalError(err.message || "Không thể lưu thông tin bãi đỗ.");
    } finally {
      setLotModalBusy(false);
    }
  };

  const handleDeleteLot = async () => {
    if (!lot) return;
    setLotModalError("");
    setLotActionMessage("");
    setLotModalBusy(true);
    try {
      await apiRequest(`/v1/parking-lots/${lot.id}`, { method: "DELETE" });
      setSelectedAfterDelete();
      await lots.reload();
      setLotActionMessage(`Đã xóa bãi đỗ "${lot.name}".`);
    } catch (err) {
      setLotModalError(err.message || "Không thể xóa bãi đỗ đang có tầng hoặc lịch sử phiên gửi xe.");
    } finally {
      setLotModalBusy(false);
    }
  };

  const setSelectedAfterDelete = () => {
    const remaining = (lots.data || []).filter(l => l.id !== lot?.id);
    select(remaining[0] ? String(remaining[0].id) : "");
  };

  return <div className="staff-workspace operations-settings container">
    <StaffError error={lots.error || (!showLotModal ? lotModalError : "")} />{lots.error && <button className="staff-button" onClick={lots.reload}>Tải lại bãi</button>}
    {lotActionMessage && <Notice kind="success">{lotActionMessage}</Notice>}
    {lots.loading && <Notice>Đang tải bãi…</Notice>}
    {!lots.loading && !lots.error && !lots.data?.length && (
      <div className="workspace-panel workspace-empty" style={{ textAlign: "center", padding: "32px 16px" }}>
        <h2>Chưa có bãi đỗ</h2>
        <p style={{ margin: "12px 0 20px" }}>Hệ thống chưa có bãi đỗ nào. Hãy tạo bãi đỗ đầu tiên để bắt đầu quản lý tầng, khu vực và slot.</p>
        <button type="button" className="btn btn--primary" onClick={openCreateLotModal}>+ Thêm bãi đỗ mới</button>
      </div>
    )}
    {lot && <>
      <section className="staff-context-bar" aria-label="Bãi đang cấu hình"><div className="staff-context-select" style={{ flexWrap: "wrap", gap: "10px" }}><StaffIcon name="pin" size={23} />
        <label htmlFor="settings-lot">Bãi cần cấu hình<select id="settings-lot" disabled={busy || lotModalBusy} value={lot.id} onChange={e => select(e.target.value)}>{lots.data.map(l => <option key={l.id} value={l.id}>{l.name}</option>)}</select></label>
        <div style={{ display: "flex", gap: "8px", flexWrap: "wrap", marginLeft: "8px" }}>
          <button type="button" className="btn btn--primary" style={{ whiteSpace: "nowrap" }} disabled={busy || lotModalBusy} onClick={openCreateLotModal}>+ Thêm bãi đỗ</button>
          <button type="button" className="btn btn--outline" style={{ whiteSpace: "nowrap", background: "#fff" }} disabled={busy || lotModalBusy} onClick={openEditLotModal}>✎ Sửa bãi đỗ</button>
          <button type="button" className="btn btn--outline" style={{ whiteSpace: "nowrap", background: "#fff", color: "#b91c1c" }} disabled={busy || lotModalBusy} onClick={handleDeleteLot}>🗑 Xóa bãi</button>
        </div>
      </div></section>
      <div className="staff-tabs" role="tablist" aria-label="Quản lý bãi xe">
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

    {showLotModal && (
      <div className="modal-backdrop" role="dialog" aria-modal="true" aria-labelledby="modal-title-lot" style={{ position: "fixed", inset: 0, background: "rgba(16,36,59,0.6)", zIndex: 100, display: "flex", alignItems: "center", justifyContent: "center", padding: "16px" }}>
        <div className="workspace-panel" style={{ width: "100%", maxWidth: "480px", background: "#fff", borderRadius: "14px", boxShadow: "0 20px 50px rgba(0,0,0,0.3)" }}>
          <h2 id="modal-title-lot" style={{ marginTop: 0 }}>{editingLot ? `Sửa bãi đỗ: ${editingLot.name}` : "Thêm bãi đỗ xe mới"}</h2>
          <p className="operations-settings-hint" style={{ margin: "4px 0 16px" }}>{editingLot ? "Cập nhật thông tin tòa nhà / bãi đỗ xe." : "Khai báo tòa nhà / bãi đỗ xe mới trong hệ thống."}</p>
          {lotModalError && <p className="feedback feedback--error" role="alert" style={{ color: "#d14343", margin: "8px 0" }}>{lotModalError}</p>}
          <form onSubmit={handleSaveLot}>
            <fieldset disabled={lotModalBusy} style={{ border: 0, padding: 0, margin: 0, display: "grid", gap: "12px" }}>
              <Field label="Tên bãi đỗ xe (bắt buộc)">
                <input required minLength={2} maxLength={100} placeholder="Ví dụ: Tòa nhà Landmark 81..." value={lotForm.name} onChange={e => setLotForm({ ...lotForm, name: e.target.value })} />
              </Field>
              <Field label="Địa chỉ">
                <input maxLength={255} placeholder="Ví dụ: 720A Điện Biên Phủ, P. 22, Bình Thạnh..." value={lotForm.address} onChange={e => setLotForm({ ...lotForm, address: e.target.value })} />
              </Field>
              <Field label="Sức chứa dự kiến">
                <input type="number" min={0} max={100000} value={lotForm.totalCapacity} onChange={e => setLotForm({ ...lotForm, totalCapacity: e.target.value })} />
              </Field>
              <label className="staff-check">
                <input type="checkbox" checked={lotForm.isActive} onChange={e => setLotForm({ ...lotForm, isActive: e.target.checked })} />
                Đang hoạt động (cho phép tiếp nhận xe)
              </label>
              <div style={{ display: "flex", gap: "10px", justifyContent: "flex-end", marginTop: "14px" }}>
                <button type="button" className="btn btn--outline" disabled={lotModalBusy} onClick={() => { setShowLotModal(false); setLotModalError(""); }}>Hủy</button>
                <button type="submit" className="btn btn--primary" disabled={lotModalBusy || !lotForm.name.trim()}>{lotModalBusy ? "Đang lưu…" : editingLot ? "Lưu cập nhật" : "Tạo bãi đỗ"}</button>
              </div>
            </fieldset>
          </form>
        </div>
      </div>
    )}
  </div>;
}
export default function AdminParkingSettings() { return <RequireRole role="ADMIN"><Content /></RequireRole>; }
