import { useCallback, useEffect, useMemo, useRef, useState, useSyncExternalStore } from "react";
import { Link, useSearchParams } from "react-router-dom";
import ParkingCanvas from "../components/ParkingCanvas.jsx";
import { apiRequest } from "../services/api.js";
import { getAuthSession, subscribeAuthSession } from "../services/authSession.js";
import { Feedback, itemsOf, useResource, useMutation, write } from "../components/WorkspaceUI.jsx";
import { sceneStatuses, sceneToDefinition } from "../services/parkingScene.js";
import { canUseStaffView } from "../services/staffDuty.js";
import "../parking-scene.css";

function useScene(lotId, revision, identity) {
  const [state, setState] = useState({ data: null, error: "", loading: false });
  useEffect(() => {
    const controller = new AbortController(); let timer;
    if (!lotId) { setState({ data: null, error: "", loading: false }); return () => controller.abort(); }
    async function load() {
      setState(s => ({ ...s, error: "", loading: true }));
      try {
        const result = await apiRequest(`/v1/parking-lots/${lotId}/scene`, { signal: controller.signal });
        if (!result?.data || result.data.id !== Number(lotId)) throw new Error("Dữ liệu bãi không khớp với bãi đang chọn.");
        if (!controller.signal.aborted) setState({ data: result.data, error: "", loading: false });
      } catch (e) { if (!controller.signal.aborted) setState(s => ({ ...s, error: e.message, loading: false })); }
      finally { if (!controller.signal.aborted) timer = setTimeout(load, 15000); }
    }
    setState({ data: null, error: "", loading: true }); load();
    return () => { controller.abort(); clearTimeout(timer); };
  }, [lotId, revision, identity]);
  return state;
}

export default function ParkingScene() {
  const session = useSyncExternalStore(subscribeAuthSession, getAuthSession, getAuthSession);
  const role = session?.userProfile.role;
  const [params, setParams] = useSearchParams();
  const [lotChoice, setLotChoice] = useState(params.get("lotId") || "");
  const [floorChoice, setFloorChoice] = useState(params.get("floorId") || "");
  const [revision, setRevision] = useState(0);
  const [selectedId, setSelectedId] = useState(null);
  const [filter, setFilter] = useState("");
  const [search, setSearch] = useState("");
  const [nextStatus, setNextStatus] = useState("");
  const [reason, setReason] = useState("");
  const viewer = useRef(null);
  const action = useMutation();
  const assigned = role === "STAFF" || role === "MANAGER";
  const lots = useResource(assigned ? "/v1/staff-assignments/my-lots" : "/v1/parking-lots?pageNumber=1&pageSize=100", role || "GUEST", { allPages: "pageNumber" });
  const rawLots = itemsOf(lots.data);
  const choices = rawLots.map(l => ({ id: l.lotId ?? l.id, name: l.lotName ?? l.name, duty: l.duty }));
  const lot = choices.find(l => String(l.id) === lotChoice) || choices[0];
  const staffDuty = lot?.duty;
  const canGate = role === "STAFF" && canUseStaffView(staffDuty, "entry");
  const canArea = role === "STAFF" && canUseStaffView(staffDuty, "verify");
  const state = useScene(lot?.id, revision, `${session?.userProfile.id || "guest"}:${role || ""}`);
  const scene = state.data?.id === lot?.id ? state.data : null;
  const definition = useMemo(() => sceneToDefinition(scene), [JSON.stringify(scene?.floors), scene?.id, scene?.name]);
  const floors = definition?.liveFloors || [];
  const floor = floors.find(f => String(f.id) === floorChoice) || floors[0];
  const slot = floor?.slots.find(s => s.id === selectedId);
  const refresh = () => setRevision(r => r + 1);
  const pick = useCallback(s => { setSelectedId(s.id); setNextStatus(""); setReason(""); }, []);
  const pickFloor = useCallback(id => setFloorChoice(current => current === id ? current : id), []);
  const rows = (floor?.slots || []).filter(s => (!filter || s.status === filter) && `${s.code} ${s.plate || ""}`.toLocaleLowerCase().includes(search.trim().toLocaleLowerCase()));
  const selectFloor = id => { setFloorChoice(id); setSelectedId(null); setNextStatus(""); setParams({ lotId: String(lot.id), floorId: id }, { replace: true }); };
  const focus = s => { pick(s); viewer.current?.findCarByPlate(s.code); };
  const counts = (floor?.slots || []).reduce((out, s) => ({ ...out, [s.status]: (out[s.status] || 0) + 1 }), {});
  const context = `lotId=${lot?.id || ""}&floorId=${floor?.id || ""}`;
  return <section className="parking-scene-page container">
    <div className="parking-toolbar"><label>Bãi đỗ<select aria-label="Bãi đỗ" disabled={action.busy || lots.loading} value={lot?.id || ""} onChange={e => { setLotChoice(e.target.value); setFloorChoice(""); setSelectedId(null); setNextStatus(""); setParams({ lotId: e.target.value }, { replace: true }); }}>
      {!choices.length && <option value="">Chưa có bãi</option>}{choices.map(l => <option key={l.id} value={l.id}>{l.name}</option>)}</select></label>
      <label>Tầng<select aria-label="Tầng" disabled={action.busy || !floor} value={floor?.id || ""} onChange={e => selectFloor(e.target.value)}>{!floor && <option value="">Chưa có tầng</option>}{floors.map(f => <option key={f.id} value={f.id}>{f.name}</option>)}</select></label>
      <div className="parking-sync"><i className={state.error ? "is-stale" : ""} /><div><strong>{state.error ? "Chưa đồng bộ" : state.loading ? "Đang đồng bộ…" : scene ? "Dữ liệu hệ thống" : "Đang tải bãi"}</strong><small>{scene ? `Cập nhật ${new Date(scene.generatedAtUtc).toLocaleTimeString("vi-VN")}` : "Tự cập nhật mỗi 15 giây"}</small></div></div>
      <button className="btn btn--outline" disabled={state.loading || action.busy} onClick={refresh}>↻ Làm mới</button>
    </div>
    <Feedback error={lots.error || state.error || action.error} message={action.message} />
    {!lots.loading && !lots.error && !choices.length && <div className="workspace-panel workspace-empty">{assigned ? "Bạn chưa được phân công bãi đỗ. Liên hệ người quản lý để được phân công." : "Hệ thống chưa có bãi đỗ đang hoạt động."}</div>}
    {scene && !floor && <div className="workspace-panel workspace-empty"><h2>Bãi chưa có tầng</h2><p>Thêm tầng và ô đỗ để hiển thị mô hình bãi.</p>{scene.canManage && <Link to="/admin/parking-settings">Mở cấu hình bãi</Link>}</div>}
    {scene && floor && <>
      <div className="parking-summary">{[["Ô tại tầng", floor.slots.length, "total"], ["Trống", counts.EMPTY || 0, "empty"], ["Có xe", counts.OCCUPIED || 0, "occupied"], ["Giữ chỗ", counts.RESERVED || 0, "reserved"], ["Bảo trì / ngừng", (counts.MAINTENANCE || 0) + (counts.UNAVAILABLE || 0), "maintenance"]].map(([label, value, color]) => <div className={`parking-stat parking-stat--${color}`} key={label}><span>{label}</span><strong>{value}</strong></div>)}</div>
      <div className="parking-workbench"><div className="parking-viewport-panel"><div className="parking-panel-title"><div><strong>{scene.name} <span>/ {floor.name}</span></strong><small>{floor.zoneName} · {floor.kind === "MONTHLY" ? "Khu vé tháng" : floor.kind === "VISITOR" ? "Khu bình thường" : "Chưa phân loại khu"}</small></div><span className="parking-live-badge">{scene.isActive ? "Đang hoạt động" : "Bãi đã đóng"}</span></div>
        <ParkingCanvas definition={definition} floorId={floor.id} role={role} onSelect={pick} onFloor={pickFloor} viewerRef={viewer} />
        <div className="parking-view-note">Kéo để xoay · Cuộn để thu phóng · Chọn ô để xem chi tiết. Bố trí mô hình được dựng tự động từ danh sách ô của bãi.</div>
      </div><aside className="parking-inspector" aria-label="Chi tiết và thao tác ô đỗ">
          <div className="parking-inspector-title"><h2>{slot ? slot.code : "Danh sách ô đỗ"}</h2>{slot && <button className="parking-clear" onClick={() => { setSelectedId(null); setNextStatus(""); }}>Bỏ chọn</button>}</div>
          {slot && <div className="parking-slot-detail"><span className={`slot-status slot-status--${slot.status.toLowerCase()}`}>{sceneStatuses[slot.status] || slot.status}</span><dl><div><dt>Tầng</dt><dd>{floor.name}</dd></div><div><dt>Khu vực</dt><dd>{slot.zoneName}</dd></div>{slot.plate && <div><dt>Biển số</dt><dd>{slot.plate}</dd></div>}<div><dt>Tiếp nhận</dt><dd>{slot.isAvailable ? "Có thể sử dụng" : "Chưa khả dụng"}</dd></div></dl>
            {slot.isProtected && <p className="parking-protection">Ô đang gắn với xe hoặc điều phối. Thao tác chỉnh sửa được khóa.</p>}
            {scene.canManage && <><Link className="btn btn--outline" to={`/parking/slots?${context}`}>Quản lý ô tại tầng</Link>{!slot.isProtected && <div className="parking-status-edit"><label>Đổi trạng thái<select aria-label="Đổi trạng thái ô" value={nextStatus} disabled={action.busy || Boolean(state.error)} onChange={e => setNextStatus(e.target.value)}><option value="">Chọn trạng thái mới</option>{Object.entries(sceneStatuses).filter(([k]) => k !== "UNAVAILABLE" && k !== slot.status).map(([k, v]) => <option key={k} value={k}>{v}</option>)}</select></label>{nextStatus && <form onSubmit={e => { e.preventDefault(); action.run(() => write(`/v1/parking-slots/${slot.id}/status`, { physicalStatus: nextStatus, reason: reason.trim() || null }, "PATCH"), () => { setNextStatus(""); refresh(); }); }}><label>Ghi chú<input value={reason} maxLength={255} disabled={action.busy} onChange={e => setReason(e.target.value)} /></label><button className="btn btn--primary" disabled={action.busy || Boolean(state.error)}>Xác nhận đổi trạng thái</button></form>}</div>}</>}
            {role === "STAFF" && canArea && floor.canVerify && <div style={{ display: "grid", gap: "6px" }}>
              <Link className="btn btn--outline" to={`/staff?view=verify&${context}${slot.sessionId ? `&sessionId=${slot.sessionId}` : ""}`}>🔍 Xác minh ô này</Link>
              {slot.status === "OCCUPIED" && <Link className="btn btn--outline" to={`/staff?view=dispatches&${context}${slot.sessionId ? `&sessionId=${slot.sessionId}` : ""}`}>📋 Báo điều phối / sai ô</Link>}
            </div>}
            {role === "STAFF" && canGate && scene.canExit && slot.sessionId && <Link className="btn btn--outline" to={`/staff?view=exit&${context}&sessionId=${slot.sessionId}`}>🚪 Đối chiếu xe ra</Link>}
            {role === "USER" && <div style={{ display: "grid", gap: "6px" }}>
              {slot.isMine ? <Link className="btn btn--primary" to="/customer/sessions">🚗 Xem phiên gửi xe của tôi</Link>
                : slot.status === "EMPTY" && scene.isActive ? <Link className="btn btn--primary" to={`/customer?${context}`}>🅿 Đặt chỗ tại bãi này</Link> : null}
            </div>}
            {!session && <Link className="btn btn--outline" to="/login">Đăng nhập để đặt chỗ</Link>}
          </div>}
          <label className="parking-slot-search">Tìm ô / biển số<input type="search" value={search} placeholder="Nhập mã ô hoặc biển số" onChange={e => setSearch(e.target.value)} /></label>
          <select aria-label="Lọc trạng thái ô" value={filter} onChange={e => setFilter(e.target.value)}><option value="">Tất cả trạng thái</option>{Object.entries(sceneStatuses).map(([k, v]) => <option key={k} value={k}>{v}</option>)}</select>
          <div className="parking-slot-list">{rows.map(s => <button key={s.id} className={`parking-slot-row ${selectedId === s.id ? "is-selected" : ""}`} onClick={() => focus(s)}><span><strong>{s.code}</strong>{s.plate && <small>{s.plate}{s.isMine ? " · Xe của tôi" : ""}</small>}</span><span className={`slot-status slot-status--${s.status.toLowerCase()}`}>{sceneStatuses[s.status] || s.status}</span></button>)}{!rows.length && <p className="workspace-empty">{floor.slots.length ? "Không tìm thấy ô phù hợp." : "Tầng chưa có ô đỗ."}</p>}</div>
        </aside></div>
      <div className="parking-quick-actions">
        <div>
          <strong>Vận hành tại {scene.name}</strong>
          <span>{scene.capacity.availableCapacity} chỗ có thể nhận thêm · {scene.capacity.pendingBookingsCount} booking đang giữ sức chứa</span>
        </div>
        <div style={{ display: "flex", gap: "8px", flexWrap: "wrap", alignItems: "center" }}>
          {role === "ADMIN" && <>
            <Link className="btn btn--outline" to={`/admin/parking-settings?view=slots&${context}`}>Quản lý bãi xe</Link>
            <Link className="btn btn--outline" to={`/admin/policies?${context}`}>Chính sách</Link>
            <Link className="btn btn--outline" to="/admin/people?view=assignments">Phân công ca trực</Link>
          </>}
          {role === "MANAGER" && <>
            <Link className="btn btn--outline" to={`/parking/slots?${context}`}>Quản lý ô đỗ</Link>
            <Link className="btn btn--outline" to={`/manager?view=sessions&lotId=${lot?.id}`}>Xe đang gửi</Link>
            <Link className="btn btn--outline" to="/manager/assignments">Phân công nhân viên</Link>
          </>}
          {role === "STAFF" && canGate && <>
            <Link className="btn btn--primary" to={`/staff?view=entry&${context}`}>Tiếp nhận xe vào</Link>
            <Link className="btn btn--outline" to={`/staff?view=exit&${context}`}>Đối chiếu xe ra</Link>
          </>}
          {role === "STAFF" && canArea && <>
            <Link className="btn btn--primary" to={`/staff?view=verify&${context}`}>Xác minh ô thực tế</Link>
            <Link className="btn btn--outline" to={`/staff?view=bookings&${context}`}>Xe đặt qua web</Link>
            <Link className="btn btn--outline" to={`/staff?view=dispatches&${context}`}>Điều phối bãi</Link>
          </>}
          {role === "USER" && <>
            <Link className="btn btn--primary" to={`/customer?${context}`}>Đặt chỗ đỗ xe</Link>
            <Link className="btn btn--outline" to="/customer/sessions">Phiên xe của tôi</Link>
          </>}
          {!session && <>
            <Link className="btn btn--primary" to="/login">Đăng nhập</Link>
            <Link className="btn btn--outline" to="/register">Đăng ký tài khoản</Link>
          </>}
        </div>
      </div>
    </>}
  </section>;
}
