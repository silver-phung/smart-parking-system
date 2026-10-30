import { useEffect, useRef, useState } from "react";
import { Link } from "react-router-dom";
import { staffError } from "../../services/staffApi.js";

export function StaffIcon({ name, size = 20 }) {
  const paths = {
    entry: (
      <>
        <path d="M3 12h13m-5-5 5 5-5 5" />
        <path d="M16 3h5v18h-5" />
      </>
    ),
    exit: (
      <>
        <path d="M9 12h12m-5-5 5 5-5 5" />
        <path d="M8 3H3v18h5" />
      </>
    ),
    verify: (
      <>
        <rect x="3" y="3" width="18" height="18" rx="4" />
        <path d="m7 12 3 3 7-7" />
      </>
    ),
    sessions: (
      <>
        <path d="M5 7h14l2 5v7h-3v-3H6v3H3v-7l2-5Z" />
        <path d="M7 7V4h10v3M7 12h1m8 0h1" />
      </>
    ),
    dispatches: (
      <>
        <rect x="5" y="4" width="14" height="17" rx="2" />
        <path d="M9 4V2h6v2M9 10h6m-6 5h6" />
      </>
    ),
    refresh: (
      <>
        <path d="M20 7v5h-5M4 17v-5h5" />
        <path d="M6 7a7 7 0 0 1 12-2l2 3M4 16l2 3a7 7 0 0 0 12-2" />
      </>
    ),
    camera: (
      <>
        <path d="M3 7h4l2-3h6l2 3h4v13H3Z" />
        <circle cx="12" cy="13" r="4" />
      </>
    ),
    pin: (
      <>
        <path d="M19 10c0 5-7 11-7 11S5 15 5 10a7 7 0 0 1 14 0Z" />
        <circle cx="12" cy="10" r="2" />
      </>
    ),
    arrow: (
      <>
        <path d="M4 12h16m-6-6 6 6-6 6" />
      </>
    ),
  };
  return (
    <svg
      width={size}
      height={size}
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth="1.7"
      strokeLinecap="round"
      strokeLinejoin="round"
      aria-hidden="true"
    >
      {paths[name] || paths.sessions}
    </svg>
  );
}

export function Notice({ kind = "info", children }) {
  return (
    <div
      className={`staff-notice staff-notice--${kind}`}
      role={kind === "error" ? "alert" : "status"}
    >
      {children}
    </div>
  );
}

export function StaffError({ error }) {
  if (!error) return null;
  return (
    <Notice kind="error">
      {staffError(error)}
      {error.status === 401 && (
        <>
          {" "}
          <Link to="/login">Đăng nhập lại</Link>
        </>
      )}
    </Notice>
  );
}

const statusLabels = {
  ACTIVE: "Đang gửi",
  PAID: "Đã thanh toán",
  COMPLETED: "Đã ra bãi",
  CANCELLED: "Đã hủy",
  PENDING: "Chờ phân ô",
  ASSIGNED: "Chờ xác minh",
  OCCUPIED: "Đã xác minh",
  WRONG_SLOT: "Ghi nhận sai ô",
};

export function StatusBadge({ status }) {
  return (
    <span
      className={`staff-badge staff-badge--${String(status).toLowerCase()}`}
    >
      {statusLabels[status] || status || "Chưa có trạng thái"}
    </span>
  );
}

export const formatTime = (value) =>
  value
    ? new Date(value).toLocaleString("vi-VN", {
        hour: "2-digit",
        minute: "2-digit",
        day: "2-digit",
        month: "2-digit",
      })
    : "—";
export const formatMoney = (value) =>
  new Intl.NumberFormat("vi-VN", { style: "currency", currency: "VND" }).format(
    value ?? 0,
  );

export function SafePhoto({ url, alt, className = "" }) {
  const [failed, setFailed] = useState(false);
  useEffect(() => setFailed(false), [url]);
  if (!url || !/^(https?:\/\/|blob:)/i.test(url) || failed) {
    return (
      <div className={`staff-photo-empty ${className}`}>
        <StaffIcon name="camera" size={28} />
        <span>{url ? "Không tải được ảnh" : "Chưa có ảnh xe"}</span>
      </div>
    );
  }
  return (
    <img
      className={`staff-photo ${className}`}
      src={url}
      alt={alt}
      referrerPolicy="no-referrer"
      onError={() => setFailed(true)}
    />
  );
}

export function SessionInfo({ session, image = true }) {
  return (
    <div className="staff-session-info">
      <div className="staff-session-title">
        <div>
          <span className="staff-eyebrow">PHIÊN #{session.id}</span>
          <h3 className="staff-plate">{session.plateNumber}</h3>
        </div>
        <StatusBadge status={session.status} />
      </div>
      <dl className="staff-facts">
        <div>
          <dt>Bãi đỗ</dt>
          <dd>{session.lotName || `Bãi #${session.lotId}`}</dd>
        </div>
        <div>
          <dt>Vị trí</dt>
          <dd>{session.slotCode || "Chưa ghép ô"}</dd>
        </div>
        <div>
          <dt>Giờ vào</dt>
          <dd>{formatTime(session.checkInTime)}</dd>
        </div>
        <div>
          <dt>Giờ ra</dt>
          <dd>{formatTime(session.checkOutTime)}</dd>
        </div>
        <div>
          <dt>Phí đã ghi nhận</dt>
          <dd>
            {session.status === "ACTIVE" && !session.totalFee
              ? "Chưa tính phí"
              : formatMoney(session.totalFee)}
          </dd>
        </div>
        <div>
          <dt>Thanh toán lúc</dt>
          <dd>{formatTime(session.paidAt)}</dd>
        </div>
      </dl>
      {image && (
        <div className="staff-session-images">
          <figure>
            <SafePhoto
              url={session.checkInImageUrl}
              alt={`Ảnh vào của xe ${session.plateNumber}`}
            />
            <figcaption>Ảnh lúc vào</figcaption>
          </figure>
          {session.checkOutImageUrl && (
            <figure>
              <SafePhoto
                url={session.checkOutImageUrl}
                alt={`Ảnh ra của xe ${session.plateNumber}`}
              />
              <figcaption>Ảnh lúc ra</figcaption>
            </figure>
          )}
        </div>
      )}
    </div>
  );
}

export function Pagination({ page, totalPages, totalCount, onChange, busy }) {
  if (!totalCount) return null;
  return (
    <nav className="staff-pagination" aria-label="Phân trang">
      <span>
        {totalCount} kết quả · Trang {page}/{Math.max(1, totalPages)}
      </span>
      <div>
        <button
          className="staff-button staff-button--quiet"
          disabled={busy || page <= 1}
          onClick={() => onChange(page - 1)}
        >
          Trước
        </button>
        <button
          className="staff-button staff-button--quiet"
          disabled={busy || page >= totalPages}
          onClick={() => onChange(page + 1)}
        >
          Sau
        </button>
      </div>
    </nav>
  );
}

export function useStaffQuery(loader) {
  const [state, setState] = useState({
    data: null,
    loading: true,
    error: null,
  });
  const [version, setVersion] = useState(0);
  useEffect(() => {
    const controller = new AbortController();
    setState({ data: null, loading: true, error: null });
    Promise.resolve()
      .then(() => loader(controller.signal))
      .then(
        (data) => {
          if (!controller.signal.aborted)
            setState({ data, loading: false, error: null });
        },
        (error) => {
          if (!controller.signal.aborted)
            setState({ data: null, loading: false, error });
        },
      );
    return () => controller.abort();
  }, [loader, version]);
  return { ...state, reload: () => setVersion((n) => n + 1) };
}

export function useStaffAction(onBusyChange) {
  const inProgress = useRef(false);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState(null);
  async function run(action) {
    if (inProgress.current) return;
    inProgress.current = true;
    setBusy(true);
    setError(null);
    onBusyChange?.(true);
    try {
      return await action();
    } catch (err) {
      setError(err);
    } finally {
      inProgress.current = false;
      setBusy(false);
      onBusyChange?.(false);
    }
  }
  return { busy, error, setError, run };
}
