import { useEffect, useRef, useState } from "react";
import {
  platesMatch,
  staffApi,
  validatePlate,
} from "../../services/staffApi.js";
import PlateCapture, { emptyCapture, prepareCapture } from "./PlateCapture.jsx";
import {
  Notice,
  SessionInfo,
  StaffError,
  StaffIcon,
  useStaffAction,
} from "./StaffUI.jsx";

export function StaffExit({
  lot,
  revision,
  initialSessionId,
  onChanged,
  onBusyChange,
}) {
  const [mode, setMode] = useState("plate");
  const [exitTicketCode, setExitTicketCode] = useState("");
  const [query, setQuery] = useState("");
  const [session, setSession] = useState(null);
  const lookupId = useRef(initialSessionId || null);
  const [lookupError, setLookupError] = useState(null);
  const [looking, setLooking] = useState(false);
  const [capture, setCapture] = useState(emptyCapture);
  const [captureBusy, setCaptureBusy] = useState(false);
  const [checked, setChecked] = useState(false);
  const action = useStaffAction(onBusyChange);
  const busy = action.busy || captureBusy;
  const isTicketed = Boolean(session?.verificationStatus && session.verificationStatus !== "LEGACY");
  useEffect(() => {
    lookupId.current = initialSessionId || null;
  }, [initialSessionId]);
  useEffect(() => {
    if (!lookupId.current) return;
    const controller = new AbortController();
    setLooking(true);
    setLookupError(null);
    staffApi
      .findSession(lot.lotId, String(lookupId.current), "id", controller.signal)
      .then(
        (result) => {
          if (!controller.signal.aborted) setSession(result);
        },
        (error) => {
          if (!controller.signal.aborted) setLookupError(error);
        },
      )
      .finally(() => {
        if (!controller.signal.aborted) setLooking(false);
      });
    return () => controller.abort();
  }, [initialSessionId, lot.lotId, revision]);
  const clearLookup = () => {
    setExitTicketCode("");
    setSession(null);
    lookupId.current = null;
    setCapture(emptyCapture());
    setChecked(false);
    setLookupError(null);
    action.setError(null);
  };
  const lookup = async (event) => {
    event.preventDefault();
    if (looking) return;
    clearLookup();
    setLooking(true);
    onBusyChange(true);
    try {
      const found = await staffApi.findSession(lot.lotId, query, mode);
      setExitTicketCode(found.ticketCode || "");
      lookupId.current = found.id;
      setSession(found);
    } catch (error) {
      setLookupError(error);
    } finally {
      setLooking(false);
      onBusyChange(false);
    }
  };
  const submit = (event) => {
    event.preventDefault();
    action.run(async () => {
      if (!checked)
        throw new Error("Xác nhận đã đối chiếu xe và xe thực sự qua cổng ra.");
      validatePlate(capture.plate);
      if (!platesMatch(capture.plate, session.plateNumber))
        throw new Error(
          "Biển số tại cổng ra không khớp với phiên xe. Hãy kiểm tra lại.",
        );
      const exitCapture = isTicketed ? { ...capture, recognition: null } : capture;
      const imageUrl = await prepareCapture(exitCapture, setCapture);
      const result = await staffApi.checkOut(
        session.id,
        lot.lotId,
        capture.plate,
        imageUrl,
        exitTicketCode,
      );
      if (!result?.data?.id)
        throw new Error(
          "Chưa xác nhận được kết quả xe ra. Hãy tra cứu lại phiên trước khi gửi lại.",
        );
      setSession(result.data);
      setCapture(emptyCapture());
      setChecked(false);
      onChanged();
    });
  };
  return (
    <div className="staff-side-stack">
      <section className="staff-panel">
        <div className="staff-panel-heading">
          <span className="staff-icon-tile">
            <StaffIcon name="exit" size={24} />
          </span>
          <div>
            <h2>Tra cứu xe tại cổng ra</h2>
            <p>
              Tìm đúng phiên trong bãi {lot.lotName} trước khi đối chiếu xe.
            </p>
          </div>
        </div>
        <form className="staff-search-form" onSubmit={lookup}>
          <label className="staff-field">
            Tra cứu theo
            <select
              value={mode}
              disabled={busy || looking}
              onChange={(event) => {
                setMode(event.target.value);
                setQuery("");
                clearLookup();
              }}
            >
              <option value="plate">Biển số xe</option>
              <option value="ticket">Mã vé</option>
              <option value="id">Mã phiên</option>
            </select>
          </label>
          <label className="staff-field">
            {mode === "id" ? "Mã phiên" : mode === "ticket" ? "Mã vé" : "Biển số cần tìm"}
            <input
              value={query}
              onChange={(event) => {
                setQuery(event.target.value);
                clearLookup();
              }}
              placeholder={mode === "id" ? "Ví dụ: 1024" : "Ví dụ: 51F-123.45"}
              disabled={busy || looking}
              required
              autoComplete="off"
            />
          </label>
          <button
            type="submit"
            className="staff-button staff-button--primary"
            disabled={looking || busy}
          >
            {looking ? "Đang tìm…" : "Tìm phiên xe"}
          </button>
        </form>
        <StaffError error={lookupError} />
      </section>
      {looking && <Notice>Đang tải thông tin phiên xe…</Notice>}
      {session && !looking && (
        <div className="staff-work-grid">
          <section className="staff-panel">
            <div className="staff-panel-heading">
              <h2>Thông tin lúc xe vào</h2>
            </div>
            <SessionInfo session={session} />
            {session.status === "ACTIVE" && (
              <Notice kind="warning">
                <strong>Chưa xác nhận thanh toán.</strong> Phiên cần hoàn tất
                tính phí và thanh toán trước khi cho xe ra. Chức năng thu phí
                chưa sẵn sàng trên màn hình này.
              </Notice>
            )}
            {session.status === "COMPLETED" && (
              <Notice kind="success">
                Đã ghi nhận xe ra bãi lúc{" "}
                {new Date(session.checkOutTime).toLocaleTimeString("vi-VN")}.
                Phiên đã kết thúc.
              </Notice>
            )}
            {session.status === "CANCELLED" && (
              <Notice kind="warning">
                Phiên đã hủy, không thể thực hiện thủ tục xe ra.
              </Notice>
            )}
          </section>
          {["ACTIVE", "PAID"].includes(session.status) && (
            <section className="staff-panel">
              <div className="staff-panel-heading">
                <h2>Đối chiếu và xác nhận xe ra</h2>
              </div>
              <StaffError error={action.error} />
              <form onSubmit={submit} aria-label="Xác nhận xe ra">
                {isTicketed && (
                  <label className="staff-field">Mã vé đã đối chiếu
                    <input required maxLength={40} disabled={busy} value={exitTicketCode} onChange={event => setExitTicketCode(event.target.value)} />
                  </label>
                )}
                <PlateCapture
                  title="Ảnh tại cổng ra"
                  plateLabel="Biển số thực tế tại cổng ra"
                  enableRecognition={!isTicketed}
                  value={capture}
                  onChange={setCapture}
                  disabled={busy}
                  onWorking={(value) => {
                    setCaptureBusy(value);
                    onBusyChange(value);
                  }}
                />
                <label className="staff-check">
                  <input
                    type="checkbox"
                    checked={checked}
                    onChange={(event) => setChecked(event.target.checked)}
                    disabled={busy || session.status !== "PAID"}
                    required
                  />
                  <span>
                    Đã đối chiếu xe, biển số và xác nhận xe thực sự qua cổng ra.
                  </span>
                </label>
                <button
                  type="submit"
                  className="staff-button staff-button--primary staff-submit"
                  disabled={busy || session.status !== "PAID"}
                >
                  <StaffIcon name="exit" />
                  {action.busy ? "Đang ghi nhận…" : "Xác nhận xe đã ra bãi"}
                </button>
              </form>
            </section>
          )}
        </div>
      )}
      {!session && !looking && (
        <div className="staff-empty">
          <StaffIcon name="exit" size={38} />
          <h3>Đối chiếu đúng xe, đúng lượt gửi</h3>
          <p>
            Nhập biển số hoặc mã phiên để xem ảnh vào, vị trí đỗ và trạng thái
            thanh toán.
          </p>
        </div>
      )}
    </div>
  );
}
