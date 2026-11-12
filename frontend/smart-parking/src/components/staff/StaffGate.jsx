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
          "BÃ£i chÆ°a sáºµn sÃ ng nháº­n thÃªm xe. HÃ£y kiá»ƒm tra láº¡i sá»©c chá»©a.",
        );
      const plate = validatePlate(capture.plate);
      if (!checked)
        throw new Error("Vui lÃ²ng Ä‘á»‘i chiáº¿u biá»ƒn sá»‘ vá»›i xe táº¡i cá»•ng.");

      const imageUrl = await prepareCapture(capture, setCapture, lot.lotId);
      const result = await staffApi.checkIn(
        lot.lotId,
        plate,
        imageUrl,
        capture.recognition?.purpose === "ENTRY" ? capture.recognition.id : null,
      );
      if (!result?.data?.id)
        throw new Error(
          "ChÆ°a xÃ¡c nháº­n Ä‘Æ°á»£c káº¿t quáº£ nháº­n xe. HÃ£y tra cá»©u phiÃªn trÆ°á»›c khi thá»­ láº¡i.",
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
            <h2>Nháº­n xe vÃ o bÃ£i</h2>
            <p>Ghi nháº­n xe vÃ£ng lai vÃ  chuyá»ƒn sang chá» xÃ¡c minh Ã´.</p>
          </div>
        </div>
        {!lot.isActive ? (
          <Notice kind="warning">
            BÃ£i Ä‘ang Ä‘Ã³ng nháº­n xe. Báº¡n váº«n cÃ³ thá»ƒ tra cá»©u vÃ  lÃ m thá»§ tá»¥c xe ra.
          </Notice>
        ) : !capacity ? (
          <Notice kind="warning">
            ChÆ°a xÃ¡c Ä‘á»‹nh Ä‘Æ°á»£c sá»©c chá»©a. Báº¥m â€œLÃ m má»›iâ€ Ä‘á»ƒ kiá»ƒm tra trÆ°á»›c khi
            nháº­n xe.
          </Notice>
        ) : (
          capacity.availableForWalkIn <= 0 && (
            <Notice kind="warning">
              BÃ£i Ä‘Ã£ háº¿t chá»— nháº­n thÃªm xe vÃ£ng lai.
            </Notice>
          )
        )}
        <StaffError error={action.error} />
        <form onSubmit={submit} aria-label="Nháº­n xe vÃ o bÃ£i">
          <PlateCapture
            lotId={lot.lotId}
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
            <span>ÄÃ£ Ä‘á»‘i chiáº¿u biá»ƒn sá»‘ vá»›i xe thá»±c táº¿ táº¡i cá»•ng.</span>
          </label>
          <button
            className="staff-button staff-button--primary staff-submit"
            disabled={busy || !accepting}
            type="submit"
          >
            <StaffIcon name="entry" />
            {action.busy ? "Äang ghi nháº­nâ€¦" : "XÃ¡c nháº­n nháº­n xe"}
          </button>
        </form>
      </section>
      <aside className="staff-side-stack">
        {created ? (
          <section className="staff-panel staff-panel--success">
            <Notice kind="success">
              ÄÃ£ nháº­n xe thÃ nh cÃ´ng. PhiÃªn #{created.id} Ä‘ang chá» xÃ¡c minh vá»‹
              trÃ­.
            </Notice>
            <SessionInfo session={created} />
          </section>
        ) : (
          <section className="staff-panel staff-guide">
            <span className="staff-eyebrow">LUá»’NG XE VÃ€O</span>
            <h2>Tá»« cá»•ng Ä‘áº¿n Ä‘Ãºng Ã´ Ä‘á»—</h2>
            <ol>
              <li>
                <span>01</span>
                <div>
                  <strong>Kiá»ƒm tra xe vÃ  biá»ƒn sá»‘</strong>
                  <p>DÃ¹ng áº£nh nháº­n diá»‡n hoáº·c nháº­p biá»ƒn sá»‘ Ä‘Ã£ Ä‘á»‘i chiáº¿u.</p>
                </div>
              </li>
              <li>
                <span>02</span>
                <div>
                  <strong>Ghi nháº­n xe vÃ o bÃ£i</strong>
                  <p>
                    Má»™t lÆ°á»£t xe cÃ³ má»™t phiÃªn. KhÃ´ng nháº­n láº¡i xe Ä‘ang cÃ³ phiÃªn
                    má»Ÿ.
                  </p>
                </div>
              </li>
              <li>
                <span>03</span>
                <div>
                  <strong>NhÃ¢n viÃªn táº¡i Ã´ xÃ¡c minh</strong>
                  <p>Xe xuáº¥t hiá»‡n trong hÃ ng chá» Ä‘á»ƒ ghÃ©p vá»›i vá»‹ trÃ­ thá»±c táº¿.</p>
                </div>
              </li>
            </ol>
          </section>
        )}
        <div className="staff-note-card">
          <StaffIcon name="verify" />
          <p>
            Nháº­n xe á»Ÿ cá»•ng chÆ°a cÃ³ nghÄ©a xe Ä‘Ã£ Ä‘á»— vÃ o Ã´. Vá»‹ trÃ­ Ä‘Æ°á»£c kiá»ƒm tra á»Ÿ
            khu â€œXÃ¡c minh Ã´â€.
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
  const lookupFromRecognition = async (recognition) => {
    const plate = recognition?.plateNumber;
    if (!plate) return;
    setMode("plate");
    setQuery(plate);
    setLookupError(null);
    setLooking(true);
    onBusyChange(true);
    try {
      if (!recognition.matchedParkingSessionId)
        throw new Error("AI chÆ°a tÃ¬m tháº¥y duy nháº¥t má»™t phiÃªn xe khá»›p. Tra cá»©u biá»ƒn sá»‘ thá»§ cÃ´ng Ä‘á»ƒ Ä‘á»‘i chiáº¿u tiáº¿p.");
      const found = await staffApi.findSession(lot.lotId, plate, "plate");
      if (found.id !== recognition.matchedParkingSessionId)
        throw new Error("PhiÃªn tra cá»©u khÃ´ng khá»›p vá»›i phiÃªn do AI xÃ¡c Ä‘á»‹nh. HÃ£y kiá»ƒm tra láº¡i áº£nh vÃ  xe.");
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
        throw new Error("XÃ¡c nháº­n Ä‘Ã£ Ä‘á»‘i chiáº¿u xe vÃ  xe thá»±c sá»± qua cá»•ng ra.");
      const plate = validatePlate(capture.plate);
      if (capture.recognition?.purpose !== "EXIT")
        throw new Error("Hãy nhận diện ảnh xe ra bằng AI trước khi xác nhận checkout.");
      if (!platesMatch(plate, session.plateNumber) && !capture.note.trim())
        throw new Error("Biển số không khớp với session. Kiểm tra xe và nhập lý do xác nhận/sửa biển số.");
      const imageUrl = await prepareCapture(capture, setCapture, lot.lotId);
      const result = await staffApi.checkOut(
        session.id,
        lot.lotId,
        plate,
        imageUrl,
        capture.recognition?.purpose === "EXIT" ? capture.recognition.id : null,
        capture.note,
      );
      if (!result?.data?.id)
        throw new Error(
          "ChÆ°a xÃ¡c nháº­n Ä‘Æ°á»£c káº¿t quáº£ xe ra. HÃ£y tra cá»©u láº¡i phiÃªn trÆ°á»›c khi gá»­i láº¡i.",
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
            <h2>Tra cá»©u xe táº¡i cá»•ng ra</h2>
            <p>
              TÃ¬m Ä‘Ãºng phiÃªn trong bÃ£i {lot.lotName} trÆ°á»›c khi Ä‘á»‘i chiáº¿u xe.
            </p>
          </div>
        </div>
        <form className="staff-search-form" onSubmit={lookup}>
          <label className="staff-field">
            Tra cá»©u theo
            <select
              value={mode}
              disabled={busy || looking}
              onChange={(event) => {
                setMode(event.target.value);
                setQuery("");
                clearLookup();
              }}
            >
              <option value="plate">Biá»ƒn sá»‘ xe</option>
              <option value="id">MÃ£ phiÃªn</option>
            </select>
          </label>
          <label className="staff-field">
            {mode === "id" ? "MÃ£ phiÃªn" : "Biá»ƒn sá»‘ cáº§n tÃ¬m"}
            <input
              value={query}
              onChange={(event) => {
                setQuery(event.target.value);
                clearLookup();
              }}
              placeholder={mode === "id" ? "VÃ­ dá»¥: 1024" : "VÃ­ dá»¥: 51F-123.45"}
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
            {looking ? "Äang tÃ¬mâ€¦" : "TÃ¬m phiÃªn xe"}
          </button>
        </form>
        <StaffError error={lookupError} />
      </section>
      {looking && <Notice>Äang táº£i thÃ´ng tin phiÃªn xeâ€¦</Notice>}
      {session && !looking && (
        <div className="staff-work-grid">
          <section className="staff-panel">
            <div className="staff-panel-heading">
              <h2>ThÃ´ng tin lÃºc xe vÃ o</h2>
            </div>
            <SessionInfo session={session} />
            {session.status === "COMPLETED" && (
              <Notice kind="success">
                ÄÃ£ ghi nháº­n xe ra bÃ£i lÃºc{" "}
                {new Date(session.checkOutTime).toLocaleTimeString("vi-VN")}.
                PhiÃªn Ä‘Ã£ káº¿t thÃºc.
              </Notice>
            )}
            {session.status === "CANCELLED" && (
              <Notice kind="warning">
                PhiÃªn Ä‘Ã£ há»§y, khÃ´ng thá»ƒ thá»±c hiá»‡n thá»§ tá»¥c xe ra.
              </Notice>
            )}
          </section>
          {["ACTIVE", "PAID"].includes(session.status) && (
            <section className="staff-panel">
              <div className="staff-panel-heading">
                <h2>Äá»‘i chiáº¿u vÃ  xÃ¡c nháº­n xe ra</h2>
              </div>
              <StaffError error={action.error} />
              <form onSubmit={submit} aria-label="XÃ¡c nháº­n xe ra">
                <PlateCapture
                  title="áº¢nh táº¡i cá»•ng ra"
                  plateLabel="Biá»ƒn sá»‘ thá»±c táº¿ táº¡i cá»•ng ra"
                  lotId={lot.lotId}
                  purpose="EXIT"
                  onRecognition={lookupFromRecognition}
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
                  <span>
                    ÄÃ£ Ä‘á»‘i chiáº¿u xe, biá»ƒn sá»‘ vÃ  xÃ¡c nháº­n xe thá»±c sá»± qua cá»•ng ra.
                  </span>
                </label>
                <button
                  type="submit"
                  className="staff-button staff-button--primary staff-submit"
                  disabled={busy}
                >
                  <StaffIcon name="exit" />
                  {action.busy ? "Äang ghi nháº­nâ€¦" : "XÃ¡c nháº­n xe Ä‘Ã£ ra bÃ£i"}
                </button>
              </form>
            </section>
          )}
        </div>
      )}
      {!session && !looking && (
        <div className="staff-empty">
          <StaffIcon name="exit" size={38} />
          <h3>Äá»‘i chiáº¿u Ä‘Ãºng xe, Ä‘Ãºng lÆ°á»£t gá»­i</h3>
          <p>
            Nháº­p biá»ƒn sá»‘ hoáº·c mÃ£ phiÃªn, hoáº·c chá»¥p áº£nh Ä‘á»ƒ AI tÃ¬m phiÃªn xe táº¡i cá»•ng.
          </p>
        </div>
      )}
    </div>
  );
}
