import { useState } from "react";
import { apiRequest } from "../services/api.js";
import { Feedback, itemsOf, useResource, useMutation, write } from "../components/WorkspaceUI.jsx";

const statuses = { EMPTY: "Trống", OCCUPIED: "Có xe", RESERVED: "Đang giữ chỗ", MAINTENANCE: "Bảo trì" };
const emptyDraft = () => ({ slotCode: "", positionX: "", positionY: "", positionZ: "", isActive: true, physicalStatus: "EMPTY" });

function FloorSlots({ floor, revision, action, refresh }) {
  const slots = useResource(`/v1/parking-floors/${floor.id}/slots?pageNumber=1&pageSize=100`, revision, { allPages: "pageNumber" });
  const [draft, setDraft] = useState(emptyDraft);
  const [editing, setEditing] = useState(null);
  const [pending, setPending] = useState(null);
  const [reason, setReason] = useState("");
  const rows = itemsOf(slots.data);
  const isLocked = slot => slot.isProtected || (slot.physicalStatus === "EMPTY" && slot.isActive && !slot.isAvailable);
  const reset = () => { setEditing(null); setDraft(emptyDraft()); };
  const patch = (key, value) => setDraft(d => ({ ...d, [key]: value }));
  return <>
    <section className="workspace-panel">
      <div><h2>{editing ? `Sửa ô ${editing.slotCode}` : "Thêm ô đỗ"}</h2><p className="workspace-note">Chỉ sửa, xóa hoặc đổi trạng thái ô chưa có xe thực tế và chưa được giữ chỗ/điều phối.</p></div>
      <form className="workspace-form" onSubmit={event => {
        event.preventDefault();
        const body = { slotCode: draft.slotCode.trim(), isActive: draft.isActive };
        for (const key of ["positionX", "positionY", "positionZ"]) body[key] = draft[key] === "" ? null : Number(draft[key]);
        if (!editing) body.physicalStatus = draft.physicalStatus;
        action.run(() => write(editing ? `/v1/parking-slots/${editing.id}` : `/v1/parking-floors/${floor.id}/slots`, body, editing ? "PUT" : "POST"), () => { reset(); refresh(); });
      }}><fieldset disabled={action.busy || slots.loading || Boolean(slots.error)}>
        <label>Mã ô<input required maxLength={20} value={draft.slotCode} onChange={e => patch("slotCode", e.target.value)} /></label>
        {!editing && <label>Trạng thái ban đầu<select value={draft.physicalStatus} onChange={e => patch("physicalStatus", e.target.value)}>{Object.entries(statuses).map(([value, label]) => <option key={value} value={value}>{label}</option>)}</select></label>}
        {["X", "Y", "Z"].map(axis => <label key={axis}>Tọa độ {axis}<input type="number" step="0.01" value={draft[`position${axis}`]} onChange={e => patch(`position${axis}`, e.target.value)} /></label>)}
        <label className="staff-check"><input type="checkbox" checked={draft.isActive} onChange={e => patch("isActive", e.target.checked)} />Đang sử dụng</label>
        <div className="workspace-actions"><button className="btn btn--primary">{editing ? "Lưu ô đỗ" : "Thêm ô đỗ"}</button>{editing && <button type="button" className="btn btn--outline" onClick={reset}>Hủy sửa</button>}</div>
      </fieldset></form>
    </section>
    {pending && <section className="workspace-panel workspace-confirm"><h2>{pending.kind === "delete" ? "Xác nhận xóa ô" : "Xác nhận đổi trạng thái"} {pending.slot.slotCode}</h2>
      <p>{pending.kind === "delete" ? "Ô đã có lịch sử phiên hoặc điều phối sẽ không được xóa." : `Chuyển sang: ${statuses[pending.status]}.`}</p>
      <form className="workspace-form" onSubmit={event => {
        event.preventDefault();
        action.run(() => pending.kind === "delete" ? apiRequest(`/v1/parking-slots/${pending.slot.id}`, { method: "DELETE" }) : write(`/v1/parking-slots/${pending.slot.id}/status`, { physicalStatus: pending.status, reason: reason.trim() }, "PATCH"), () => { setPending(null); reset(); refresh(); });
      }}>
        {pending.kind === "status" && <label>Lý do đổi trạng thái<textarea required maxLength={255} value={reason} disabled={action.busy} onChange={e => setReason(e.target.value)} /></label>}
        <div className="workspace-actions"><button className="btn btn--primary" disabled={action.busy || (pending.kind === "status" && !reason.trim())}>Xác nhận</button><button type="button" className="btn btn--outline" disabled={action.busy} onClick={() => setPending(null)}>Hủy</button></div>
      </form>
    </section>}
    <section className="workspace-panel"><div className="workspace-heading"><div><h2>Ô đỗ · {floor.name}</h2><p>{rows.length} ô · {rows.filter(s => s.isAvailable).length} ô còn trống</p></div><button className="btn btn--outline" disabled={action.busy || slots.loading} onClick={refresh}>Tải lại ô đỗ</button></div>
      <Feedback loading={slots.loading} error={slots.error} />
      {!slots.loading && !slots.error && !rows.length && <p>Chưa có ô đỗ trong tầng này.</p>}
      <div className="workspace-table"><table><thead><tr><th>Mã ô</th><th>Trạng thái</th><th>Sử dụng</th><th>Thao tác</th></tr></thead><tbody>{rows.map(slot => {
        const locked = isLocked(slot);
        return <tr key={slot.id}><td><strong>{slot.slotCode}</strong>{locked && <small>Đang có xe hoặc đã được giữ chỗ · Đã khóa thao tác</small>}</td>
          <td><select aria-label={`Trạng thái ô ${slot.slotCode}`} value={slot.physicalStatus} disabled={locked || action.busy || Boolean(editing) || Boolean(pending)} onChange={e => { setReason(""); setPending({ kind: "status", slot, status: e.target.value }); }}>
            {slot.physicalStatus === "UNAVAILABLE" && <option value="UNAVAILABLE" disabled>Không khả dụng</option>}{Object.entries(statuses).map(([value, label]) => <option key={value} value={value}>{label}</option>)}
          </select></td><td>{slot.isActive ? "Đang sử dụng" : "Ngừng sử dụng"}</td>
          <td><div className="workspace-actions"><button className="btn btn--outline" aria-label={`Sửa ô ${slot.slotCode}`} disabled={locked || action.busy || Boolean(pending)} onClick={() => { setEditing(slot); setDraft({ ...emptyDraft(), ...slot, positionX: slot.positionX ?? "", positionY: slot.positionY ?? "", positionZ: slot.positionZ ?? "" }); }}>Sửa</button>
            <button className="btn btn--outline" aria-label={`Xóa ô ${slot.slotCode}`} disabled={locked || action.busy || Boolean(editing) || Boolean(pending)} onClick={() => setPending({ kind: "delete", slot })}>Xóa</button></div></td>
        </tr>;
      })}</tbody></table></div>
    </section>
  </>;
}

export default function ParkingSlotManagement({ role }) {
  const [lotId, setLotId] = useState("");
  const [floorId, setFloorId] = useState("");
  const [revision, setRevision] = useState(0);
  const action = useMutation();
  const lots = useResource(role === "MANAGER" ? "/v1/staff-assignments/my-lots" : "/v1/parking-lots?pageNumber=1&pageSize=100", 0, { allPages: "pageNumber" });
  const availableLots = itemsOf(lots.data).map(l => role === "MANAGER" ? { id: l.lotId, name: l.lotName } : l);
  const lot = availableLots.find(l => String(l.id) === lotId) || availableLots[0];
  const floors = useResource(lot ? `/v1/parking-lots/${lot.id}/floors?isActiveOnly=false` : null);
  const floor = itemsOf(floors.data).find(f => String(f.id) === floorId) || itemsOf(floors.data)[0];
  const refresh = () => setRevision(r => r + 1);
  return <section className="workspace container"><header className="workspace-heading"><div><span className="tag">{role === "ADMIN" ? "Quản trị bãi" : "Quản lý bãi"}</span><h1>Quản lý ô đỗ</h1><p>Thêm, sửa, xóa và cập nhật trạng thái ô tại bãi được phép quản lý.</p></div></header>
    <Feedback loading={lots.loading || floors.loading} error={lots.error || floors.error || action.error} message={action.message} />
    {!lots.loading && !lots.error && !availableLots.length && <section className="workspace-panel">Chưa có bãi được phép quản lý.</section>}
    {lot && <section className="workspace-panel"><h2>Chọn bãi và tầng</h2>{floor && <a className="btn btn--outline" href={`/3d-viewer.html?lotId=${lot.id}&floorId=${floor.id}`}>Xem tầng này bằng 3D</a>}<div className="workspace-filters">
      <label>Bãi đỗ<select disabled={action.busy} value={lot.id} onChange={e => { setLotId(e.target.value); setFloorId(""); }} >{availableLots.map(l => <option key={l.id} value={l.id}>{l.name}</option>)}</select></label>
      <label>Tầng<select value={floor?.id || ""} disabled={action.busy || floors.loading || !floor} onChange={e => setFloorId(e.target.value)}>{!floor && <option value="">Chưa có tầng</option>}{itemsOf(floors.data).map(f => <option key={f.id} value={f.id}>{f.name}</option>)}</select></label>
    </div></section>}
    {lot && floor && !floors.loading && !floors.error && <FloorSlots key={`${lot.id}-${floor.id}`} floor={floor} revision={revision} action={action} refresh={refresh} />}
  </section>;
}
