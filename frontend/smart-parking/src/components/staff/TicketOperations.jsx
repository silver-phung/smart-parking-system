import { useCallback, useRef, useState } from "react";
import { operationsApi } from "../../services/operationsApi.js";
import { apiRequest } from "../../services/api.js";
import { validatePhoto } from "../../services/staffApi.js";
import { Notice, StaffError, useStaffAction, useStaffQuery, SafePhoto } from "./StaffUI.jsx";
import StaffVerification from "./StaffVerification.jsx";

const Field = ({ label, children }) => <label className="staff-field">{label}{children}</label>;

export function TicketEntry({ lot, onChanged, onBusyChange }) {
  const load = useCallback(signal => operationsApi(lot.lotId, "zones", null, null, signal), [lot.lotId]);
  const zones = useStaffQuery(load);
  const [zoneId, setZone] = useState("");
  const [ticket, setTicket] = useState("");
  const [file, setFile] = useState(null);
  const [imageId, setImageId] = useState(null);
  const [reason, setReason] = useState("");
  const [created, setCreated] = useState(null);
  const request = useRef(null);
  const action = useStaffAction(onBusyChange);
  const submit = e => {
    e.preventDefault();
    action.run(async () => {
      // Keep the exact request across uncertain network failures. Editing only resumes after lookup/reset.
      if (!request.current) {
        let savedId = imageId;
        if (file && !savedId) {
          const body = new FormData(); body.append("file", file);
          const result = await apiRequest("/v1/images", { method: "POST", body });
          savedId = result.data.id; setImageId(savedId);
        }
        request.current = { eventId: crypto.randomUUID(), zoneId: Number(zoneId), ticketCode: ticket,
          imageId: savedId, manualReason: reason || null };
      }
      const result = await operationsApi(lot.lotId, "entries", request.current);
      setCreated(result); onChanged();
    });
  };
  const reset = () => { request.current = null; setCreated(null); setTicket(""); setFile(null); setImageId(null); setReason(""); action.setError(null); };
  const locked = action.busy || Boolean(request.current);
  return <section className="staff-panel">
    <h2>Cấp vé và tiếp nhận xe</h2>
    <p>Nhân viên tại ô sẽ đối chiếu xe thực tế và ghi nhận biển số chính thức.</p>
    <StaffError error={zones.error || action.error} />
    {zones.error && <button className="staff-button" onClick={zones.reload}>Tải lại khu</button>}
    {created ? <>
      <Notice kind="success">Đã cấp vé {created.ticketCode}. Lượt #{created.id} đang chờ xác minh.</Notice>
      <p>Ô sẽ chuyển sang Có xe sau khi nhân viên xác minh.</p>
      <button className="staff-button staff-button--primary" onClick={reset}>Tiếp nhận xe tiếp theo</button>
    </> : <form onSubmit={submit}>
      <fieldset disabled={locked || !lot.isActive} className="operations-fields">
        <Field label="Khu bình thường"><select required value={zoneId} onChange={e => setZone(e.target.value)}>
          <option value="">Chọn khu</option>{zones.data?.filter(z => z.type === "TRANSIENT" && z.isActive).map(z => <option key={z.id} value={z.id}>{z.code} · {z.name}</option>)}
        </select></Field>
        <Field label="Mã vé"><input required maxLength={40} value={ticket} onChange={e => setTicket(e.target.value)} autoComplete="off" /></Field>
        <Field label="Ảnh xe"><input type="file" accept="image/jpeg,image/png,image/webp" capture="environment" onChange={e => {
          const next = e.target.files?.[0]; e.target.value = "";
          if (!next) return;
          try { validatePhoto(next); setFile(next); setImageId(null); action.setError(null); }
          catch (error) { action.setError(error); }
        }} /></Field>
        {file && <p>{file.name}</p>}
        <Field label="Lý do tiếp nhận"><textarea required maxLength={500} value={reason} onChange={e => setReason(e.target.value)} /></Field>
      </fieldset>
      <button type="submit" className="staff-button staff-button--primary" disabled={action.busy || !lot.isActive || zones.loading || Boolean(zones.error)}>
        {action.busy ? "Đang tiếp nhận…" : request.current ? "Gửi lại cùng lượt xe" : "Cấp vé và đưa vào hàng chờ"}
      </button>
      {request.current && <Notice kind="warning">Yêu cầu đã gửi. Tra vé để đối chiếu; gửi lại sử dụng cùng mã sự kiện và nội dung.</Notice>}
      {request.current && <button className="staff-button" type="button" disabled={action.busy} onClick={reset}>Đã đối chiếu vé, nhập lượt khác</button>}
    </form>}
  </section>;
}

export function TicketVerification({ lot, revision, onChanged, onBusyChange, initialSessionId }) {
  const load = useCallback(async signal => ({
    sessions: await operationsApi(lot.lotId, "sessions", null, null, signal),
    layout: await operationsApi(lot.lotId, "layout", null, null, signal),
  }), [lot.lotId, revision]);
  const query = useStaffQuery(load);
  const [selected, select] = useState(null);
  const [plate, setPlate] = useState("");
  const [fuel, setFuel] = useState("");
  const [slotId, setSlot] = useState("");
  const [reason, setReason] = useState("");
  const [observed, setObserved] = useState(false);
  const [oldEmpty, setOldEmpty] = useState(false);
  const [evidence, setEvidence] = useState(null);
  const action = useStaffAction(onBusyChange);
  const choose = session => {
    select(session); setPlate(session.temporaryPlateNumber || session.plateNumber || "");
    setFuel(session.fuelType || ""); setSlot(""); setReason(""); setObserved(false); setOldEmpty(false); setEvidence(null); action.setError(null);
  };
  const session = query.data?.sessions.find(s => s.id === selected?.id && s.revision === selected.revision);
  const move = session?.verificationStatus === "VERIFIED";
  const submit = e => { e.preventDefault(); action.run(async () => {
    if (!session) throw new Error("Lượt đã thay đổi. Chọn lại trong danh sách.");
    await operationsApi(lot.lotId, `sessions/${session.id}/${move ? "move" : "verify"}`, move
      ? { actualSlotId: Number(slotId), expectedRevision: session.revision, reason, oldSlotObservedEmpty: oldEmpty, vehicleObservedAtDestination: observed }
      : { actualSlotId: Number(slotId), expectedRevision: session.revision, plateNumber: plate, fuelType: fuel, reason, vehicleObserved: observed });
    select(null); setEvidence(null); onChanged(); query.reload();
  }); };
  return <><div className="staff-work-grid">
    <section className="staff-panel"><h2>Hàng chờ và xe đã xác minh</h2>
      <StaffError error={query.error} />
      <button className="staff-button" disabled={action.busy || query.loading} onClick={query.reload}>Tải lại danh sách</button>
      {query.loading && <Notice>Đang tải dữ liệu…</Notice>}
      {!query.loading && !query.error && !query.data?.sessions.length && <p>Không có lượt xe đang mở.</p>}
      <div className="operations-session-list">{!query.error && query.data?.sessions.filter(s => s.eventId).map(s => <button className={`operations-session ${s.id === selected?.id ? "is-selected" : ""}`} disabled={action.busy} key={s.id} onClick={() => choose(s)}>
        <strong>{s.ticketCode} · #{s.id}</strong><span>{s.plateNumber || s.temporaryPlateNumber || "Chưa đọc được biển số"}</span>
        <small>{s.verificationStatus === "WAITING_VERIFICATION" ? "Chờ xác minh" : `Đã đỗ · ${s.slotCode}`}</small>
      </button>)}</div>
    </section>
    <section className="staff-panel"><h2>{move ? "Chuyển xe đã đỗ" : "Xác minh xe tại ô"}</h2>
      <StaffError error={action.error} />
      {!session ? <p>Chọn lượt gửi từ danh sách hiện tại.</p> : <form onSubmit={submit}>
        <fieldset disabled={action.busy || query.loading || Boolean(query.error)} className="operations-fields">
          <p>Vé {session.ticketCode} · Lượt #{session.id} · Khu #{session.zoneId}</p>
          {session.imageId ? <><button className="staff-button" type="button" onClick={() => action.run(async () => setEvidence(await operationsApi(lot.lotId, `sessions/${session.id}/evidence`)))}>Xem ảnh lúc vào</button>
            {evidence && <SafePhoto url={evidence.url} alt="Xe lúc vào bãi" />}</> : <Notice kind="warning">Lượt này chưa có ảnh. Đối chiếu trực tiếp xe và vé trước khi xác nhận.</Notice>}
          {!move && <><Field label="Biển số đã đối chiếu"><input required maxLength={20} value={plate} onChange={e => setPlate(e.target.value)} /></Field>
            <Field label="Nhiên liệu đã xác nhận"><select required value={fuel} onChange={e => setFuel(e.target.value)}><option value="">Chọn nhiên liệu</option><option value="PETROL">Xăng/dầu</option><option value="ELECTRIC">Điện</option><option value="HYBRID">Hybrid</option></select></Field></>}
          <Field label="Ô thực tế"><select required value={slotId} onChange={e => setSlot(e.target.value)}><option value="">Chọn ô đã đối chiếu</option>{query.data?.layout.slots.filter(s => s.physicalStatus === "EMPTY" && s.isActive && s.floorActive && s.zoneId === session.zoneId && (s.allowedFuelType === "ANY" || s.allowedFuelType === (move ? session.fuelType : fuel))).map(s => <option key={s.id} value={s.id}>{s.floorName} · {s.slotCode}</option>)}</select></Field>
          <Field label="Lý do nhập tay, sửa biển số hoặc chuyển ô"><textarea required={move || !session.temporaryPlateNumber} value={reason} maxLength={500} onChange={e => setReason(e.target.value)} /></Field>
          <label className="staff-check"><input type="checkbox" required checked={observed} onChange={e => setObserved(e.target.checked)} />Đã đối chiếu xe thực tế, vé và ô được chọn.</label>
          {move && <label className="staff-check"><input type="checkbox" required checked={oldEmpty} onChange={e => setOldEmpty(e.target.checked)} />Đã kiểm tra xe rời ô cũ, ô cũ trống.</label>}
          <button className="staff-button staff-button--primary">{action.busy ? "Đang lưu…" : move ? "Xác nhận chuyển ô" : "Xác minh và ghi nhận Có xe"}</button>
        </fieldset>
      </form>}
    </section>
  </div>
    {query.data?.sessions.some(s => !s.eventId) && <section aria-label="Lượt gửi trước nâng cấp">
      <h2>Xác minh lượt gửi trước nâng cấp</h2>
      <StaffVerification lot={lot} revision={revision} onChanged={onChanged} onBusyChange={onBusyChange} initialSessionId={initialSessionId} />
    </section>}
  </>;
}

export function TicketLookup({ lot, onBusyChange }) {
  const [code, setCode] = useState("");
  const [result, setResult] = useState(null);
  const [reason, setReason] = useState("");
  const action = useStaffAction(onBusyChange);
  return <section className="staff-panel"><h2>Tra vé và báo mất</h2>
    <StaffError error={action.error} />
    <form onSubmit={e => { e.preventDefault(); setResult(null); action.run(async () => setResult(await operationsApi(lot.lotId, `tickets/${encodeURIComponent(code.trim())}`))); }}>
      <Field label="Mã vé"><input required disabled={action.busy} value={code} onChange={e => { setCode(e.target.value); setResult(null); }} /></Field>
      <button className="staff-button" disabled={action.busy}>Tra vé</button>
    </form>
    {result && <><p>Vé {result.ticketCode} · {result.ticketStatus} · Lượt #{result.id}</p><p>{result.plateNumber || result.temporaryPlateNumber || "Chưa xác minh biển số"} · {result.verificationStatus} · {result.status}</p>
      <Notice>Vé mất/thu hồi không cho xe ra. Quản lý hoặc Admin cần đối chiếu và xử lý thay vé.</Notice>
      <Field label="Lý do báo mất vé"><textarea value={reason} maxLength={500} onChange={e => setReason(e.target.value)} disabled={action.busy} /></Field>
      <button className="staff-button" disabled={action.busy || result.ticketStatus !== "ISSUED" || !reason.trim()} onClick={() => action.run(async () => {
        await operationsApi(lot.lotId, `tickets/${encodeURIComponent(result.ticketCode)}/change`, { action: "LOST", reason });
        setResult(await operationsApi(lot.lotId, `tickets/${encodeURIComponent(result.ticketCode)}`));
      })}>Ghi nhận mất vé</button></>}
  </section>;
}
