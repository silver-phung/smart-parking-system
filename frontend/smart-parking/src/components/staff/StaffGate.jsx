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

export function StaffEntry({ lot, capacity, onChanged, onBusyChange }) {
  const [capture, setCapture] = useState(emptyCapture);
  const [captureBusy, setCaptureBusy] = useState(false);
  const [checked, setChecked] = useState(false);
  const [created, setCreated] = useState(null);
  const action = useStaffAction(onBusyChange);
  const busy = action.busy || captureBusy;
  const accepting = lot.isActive && capacity && capacity.availableForWalkIn > 0;
  const submit = (event) => {
    event.preventDefault();
    action.run(async () => {
      setCreated(null);
      if (!accepting)
        throw new Error(
          "Bãi chưa sẵn sàng nhận thêm xe. Hãy kiểm tra lại sức chứa.",
        );
      const plate = validatePlate(capture.plate);
      if (!checked)
        throw new Error("Vui lòng đối chiếu biển số với xe tại cổng.");
      const imageUrl = await prepareCapture(capture, setCapture);
      const result = await staffApi.checkIn(lot.lotId, plate, imageUrl);
      if (!result?.data?.id)
        throw new Error(
          "Chưa xác nhận được kết quả nhận xe. Hãy tra cứu phiên trước khi thử lại.",
        );
      setCreated(result.data);
      setCapture(emptyCapture());
      setChecked(false);
      onChanged();
    });
  };
  return (
    <div className="staff-work-grid">
      <section className="staff-panel">
        <div className="staff-panel-heading">
          <span className="staff-icon-tile">
            <StaffIcon name="entry" size={24} />
          </span>
          <div>
            <h2>Nhận xe vào bãi</h2>
            <p>Ghi nhận xe vãng lai và chuyển sang chờ xác minh ô.</p>
          </div>
        </div>
        {!lot.isActive ? (
          <Notice kind="warning">
            Bãi đang đóng nhận xe. Bạn vẫn có thể tra cứu và làm thủ tục xe ra.
          </Notice>
        ) : !capacity ? (
          <Notice kind="warning">
            Chưa xác định được sức chứa. Bấm “Làm mới” để kiểm tra trước khi
            nhận xe.
          </Notice>
        ) : (
          capacity.availableForWalkIn <= 0 && (
            <Notice kind="warning">
              Bãi đã hết chỗ nhận thêm xe vãng lai.
            </Notice>
          )
        )}
        <StaffError error={action.error} />
        <form onSubmit={submit} aria-label="Nhận xe vào bãi">
          <PlateCapture
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
              disabled={busy}
              required
            />
            <span>Đã đối chiếu biển số với xe thực tế tại cổng.</span>
          </label>
          <button
            className="staff-button staff-button--primary staff-submit"
            disabled={busy || !accepting}
            type="submit"
          >
            <StaffIcon name="entry" />
            {action.busy ? "Đang ghi nhận…" : "Xác nhận nhận xe"}
          </button>
        </form>
      </section>
      <aside className="staff-side-stack">
        {created ? (
          <section className="staff-panel staff-panel--success">
            <Notice kind="success">
              Đã nhận xe thành công. Phiên #{created.id} đang chờ xác minh vị
              trí.
            </Notice>
            <SessionInfo session={created} />
          </section>
        ) : (
          <section className="staff-panel staff-guide">
            <span className="staff-eyebrow">LUỒNG XE VÀO</span>
            <h2>Từ cổng đến đúng ô đỗ</h2>
            <ol>
              <li>
                <span>01</span>
                <div>
                  <strong>Kiểm tra xe và biển số</strong>
                  <p>Dùng ảnh nhận diện hoặc nhập biển số đã đối chiếu.</p>
                </div>
              </li>
              <li>
                <span>02</span>
                <div>
                  <strong>Ghi nhận xe vào bãi</strong>
                  <p>
                    Một lượt xe có một phiên. Không nhận lại xe đang có phiên
                    mở.
                  </p>
                </div>
              </li>
              <li>
                <span>03</span>
                <div>
                  <strong>Nhân viên tại ô xác minh</strong>
                  <p>Xe xuất hiện trong hàng chờ để ghép với vị trí thực tế.</p>
                </div>
              </li>
            </ol>
          </section>
        )}
        <div className="staff-note-card">
          <StaffIcon name="verify" />
          <p>
            Nhận xe ở cổng chưa có nghĩa xe đã đỗ vào ô. Vị trí được kiểm tra ở
            khu “Xác minh ô”.
          </p>
        </div>
      </aside>
    </div>
  );
}

export function StaffExit({
  lot,
  revision,
  initialSessionId,
  onChanged,
  onBusyChange,
}) {
  const [mode, setMode] = useState("plate");
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
      const imageUrl = await prepareCapture(capture, setCapture);
      const result = await staffApi.checkOut(
        session.id,
        lot.lotId,
        capture.plate,
        imageUrl,
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
              <option value="id">Mã phiên</option>
            </select>
          </label>
          <label className="staff-field">
            {mode === "id" ? "Mã phiên" : "Biển số cần tìm"}
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
                <PlateCapture
                  title="Ảnh tại cổng ra"
                  plateLabel="Biển số thực tế tại cổng ra"
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
