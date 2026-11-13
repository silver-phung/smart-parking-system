import { useSearchParams } from "react-router-dom";
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
  const [params] = useSearchParams();
  const [lotId, setLotId] = useState(params.get("lotId") || "");
  const [floorId, setFloorId] = useState(params.get("floorId") || "");
  const [revision, setRevision] = useState(0);
  const [floorRevision, setFloorRevision] = useState(0);
  const [showAddFloor, setShowAddFloor] = useState(false);
  const [newFloorName, setNewFloorName] = useState("Tầng 1");
  const [newFloorNumber, setNewFloorNumber] = useState(1);
  const action = useMutation();
  const lots = useResource(role === "MANAGER" ? "/v1/staff-assignments/my-lots" : "/v1/parking-lots?pageNumber=1&pageSize=100", 0, { allPages: "pageNumber" });
  const availableLots = itemsOf(lots.data).map(l => role === "MANAGER" ? { id: l.lotId, name: l.lotName } : l);
  const lot = availableLots.find(l => String(l.id) === lotId) || availableLots.find(l => (l.totalFloors || l.physicalSlotsCount) > 0) || availableLots[0];
  const floors = useResource(lot ? `/v1/parking-lots/${lot.id}/floors?all=1` : null, floorRevision);
  const floorList = itemsOf(floors.data);
  const floor = floorList.find(f => String(f.id) === floorId) || floorList[0];
  const refresh = () => setRevision(r => r + 1);
  return <section className="workspace container">
    <Feedback loading={lots.loading || floors.loading} error={lots.error || floors.error || action.error} message={action.message} />
    {!lots.loading && !lots.error && !availableLots.length && <section className="workspace-panel">Chưa có bãi được phép quản lý.</section>}
    {lot && <section className="workspace-panel">
      <div className="workspace-heading">
        <h2>Chọn bãi và tầng</h2>
        <div className="workspace-actions">
          {floor && <a className="btn btn--outline" href={`/app/parking?lotId=${lot.id}&floorId=${floor.id}`}>Xem tầng này bằng 3D</a>}
          <button type="button" className="btn btn--outline" disabled={action.busy} onClick={() => {
            const nextNum = floorList.length ? Math.max(...floorList.map(f => Number(f.floorNumber) || 0)) + 1 : 1;
            setNewFloorNumber(nextNum);
            setNewFloorName(`Tầng ${nextNum}`);
            setShowAddFloor(v => !v);
          }}>{showAddFloor ? "Đóng tạo tầng" : "+ Thêm tầng"}</button>
        </div>
      </div>
      <div className="workspace-filters">
        <label>Bãi đỗ<select disabled={action.busy} value={lot.id} onChange={e => { setLotId(e.target.value); setFloorId(""); setShowAddFloor(false); }} >{availableLots.map(l => <option key={l.id} value={l.id}>{l.name}</option>)}</select></label>
        <label>Tầng<select value={floor?.id || ""} disabled={action.busy || floors.loading || !floor} onChange={e => setFloorId(e.target.value)}>{!floor && <option value="">Chưa có tầng</option>}{floorList.map(f => <option key={f.id} value={f.id}>{f.name}</option>)}</select></label>
      </div>
    </section>}
    {lot && !floors.loading && !floors.error && (!floor || showAddFloor) && (
      <section className="workspace-panel">
        <div>
          <h2>{!floor ? `Bãi "${lot.name}" chưa có tầng nào — Tạo tầng đầu tiên` : `Thêm tầng mới cho bãi "${lot.name}"`}</h2>
          <p className="workspace-note">Tạo tầng để bắt đầu thêm và quản lý các ô đỗ xe trong bãi này. Hệ thống sẽ tự động gắn khu mặc định nếu bãi chưa có khu.</p>
        </div>
        <form className="workspace-form" onSubmit={event => {
          event.preventDefault();
          action.run(
            () => write(`/v1/parking-lots/${lot.id}/floors`, {
              name: newFloorName.trim(),
              floorNumber: Number(newFloorNumber) || 1,
              totalSlots: 0,
              isActive: true
            }, "POST"),
            res => {
              setShowAddFloor(false);
              if (res?.data?.id) setFloorId(String(res.data.id));
              setFloorRevision(r => r + 1);
            }
          );
        }}>
          <fieldset disabled={action.busy}>
            <label>Tên tầng<input required maxLength={50} value={newFloorName} onChange={e => setNewFloorName(e.target.value)} placeholder="VD: Tầng 1, Tầng G, Hầm B1" /></label>
            <label>Số thứ tự tầng<input type="number" required min={-10} max={100} value={newFloorNumber} onChange={e => setNewFloorNumber(e.target.value)} /></label>
            <div className="workspace-actions">
              <button className="btn btn--primary" type="submit">Tạo tầng ngay</button>
              {floor && <button type="button" className="btn btn--outline" onClick={() => setShowAddFloor(false)}>Hủy</button>}
            </div>
          </fieldset>
        </form>
      </section>
    )}
    {lot && floor && !floors.loading && !floors.error && <FloorSlots key={`${lot.id}-${floor.id}`} floor={floor} revision={revision} action={action} refresh={refresh} />}
  </section>;
}
