import { useCallback, useEffect, useState, useSyncExternalStore } from "react";
import { Link } from "react-router-dom";
import AnchorLink from "../components/AnchorLink.jsx";
import { apiRequest } from "../services/api.js";
import { getAuthSession, subscribeAuthSession } from "../services/authSession.js";

const highlights = [
  "Biết chỗ đậu xe còn trống trên web, không phải chạy vòng tìm chỗ",
  "Ra vào bằng thẻ cứng hoặc đặt chỗ trước trên app",
  "Camera ghi lại biển số và màu xe của mỗi lượt vào bãi",
];

const methods = [
  {
    id: "the-cung",
    title: "Quẹt thẻ cứng",
    icon: "card",
    steps: [
      {
        title: "Quẹt thẻ tại cổng vào",
        text: "Đưa thẻ cứng vào đầu đọc để hệ thống ghi nhận lượt vào.",
      },
      {
        title: "Camera lưu thông tin xe",
        text: "Camera ghi lại biển số và màu xe, lưu cùng dữ liệu của thẻ.",
      },
      {
        title: "Vào bãi và đậu xe",
        text: "Cổng mở, bạn đậu xe vào vị trí còn trống. Khi ra, quẹt thẻ lần nữa để đối chiếu xe.",
      },
    ],
  },
  {
    id: "app",
    title: "Đăng ký chỗ đậu từ app",
    icon: "phone",
    steps: [
      {
        title: "Chọn bãi và chỗ trống",
        text: "Mở app, xem sơ đồ bãi và chọn vị trí còn trống.",
      },
      {
        title: "Xác nhận đặt chỗ",
        text: "Nhập biển số xe và xác nhận. Vị trí đã chọn được giữ cho bạn trên hệ thống.",
      },
      {
        title: "Đến bãi và đậu đúng chỗ",
        text: "Camera ghi lại biển số, màu xe ở cổng, rồi bạn đi thẳng tới vị trí đã đặt.",
      },
    ],
  },
];

function Icon({ name }) {
  const common = {
    width: 30,
    height: 30,
    viewBox: "0 0 24 24",
    fill: "none",
    stroke: "currentColor",
    strokeWidth: 1.8,
    strokeLinecap: "round",
    strokeLinejoin: "round",
    "aria-hidden": true,
  };
  if (name === "card")
    return (
      <svg {...common}>
        <rect x="3" y="5" width="18" height="14" rx="2" />
        <line x1="3" y1="10" x2="21" y2="10" />
        <line x1="7" y1="15" x2="11" y2="15" />
      </svg>
    );
  if (name === "phone")
    return (
      <svg {...common}>
        <rect x="7" y="2" width="10" height="20" rx="2" />
        <line x1="11" y1="18" x2="13" y2="18" />
      </svg>
    );
  if (name === "camera")
    return (
      <svg {...common}>
        <path d="M4 8h3l2-3h6l2 3h3v11H4z" />
        <circle cx="12" cy="13" r="3.5" />
      </svg>
    );
  return (
    <svg {...common}>
      <path d="M12 21s-7-6.2-7-11a7 7 0 0 1 14 0c0 4.8-7 11-7 11z" />
      <circle cx="12" cy="10" r="2.5" />
    </svg>
  );
}

function SectionHead({ tag, title, text }) {
  return (
    <div className="section-head">
      <span className="tag">{tag}</span>
      <h2>{title}</h2>
      {text && <p>{text}</p>}
    </div>
  );
}

function ParkingIllustration() {
  const cols = [30, 130, 230, 330, 430];
  const cars = [
    { x: 42, y: 40, fill: "#1B5FD6", glass: "#0F3E96" },
    { x: 242, y: 40, fill: "#C2410C", glass: "#7C2A07" },
    { x: 342, y: 40, fill: "#0E1A2B", glass: "#33445C" },
    { x: 442, y: 40, fill: "#F5B700", glass: "#9A7200" },
    { x: 142, y: 280, fill: "#5B6B80", glass: "#2F3C4D" },
    { x: 342, y: 280, fill: "#C2410C", glass: "#7C2A07" },
  ];
  const free = [
    { x: 130, y: 24 },
    { x: 30, y: 264 },
    { x: 230, y: 264 },
    { x: 430, y: 264 },
  ];
  return (
    <svg
      viewBox="0 0 560 420"
      role="img"
      aria-label="Minh họa bãi đỗ xe nhìn từ trên xuống, hai hàng chỗ đậu, một số chỗ đã có xe và bốn chỗ còn trống"
      className="lot"
    >
      <rect width="560" height="420" fill="#DDE5EF" />
      <rect y="172" width="560" height="76" fill="#B8C4D4" />
      <line x1="20" y1="210" x2="540" y2="210" stroke="#fff" strokeWidth="4" strokeDasharray="22 16" />
      <g fill="none" stroke="#fff" strokeWidth="3">
        {[24, 264].map((y) =>
          cols.map((x) => <rect key={`${x}-${y}`} x={x} y={y} width="84" height="132" rx="4" />)
        )}
      </g>
      {cars.map((c) => (
        <g key={`${c.x}-${c.y}`}>
          <rect x={c.x} y={c.y} width="60" height="100" rx="16" fill={c.fill} />
          <rect x={c.x + 7} y={c.y + 22} width="46" height="22" rx="6" fill={c.glass} />
        </g>
      ))}
      {free.map((f) => (
        <g key={`${f.x}-${f.y}`}>
          <rect
            x={f.x}
            y={f.y}
            width="84"
            height="132"
            rx="4"
            fill="#fff"
            fillOpacity="0.55"
            stroke="#1B5FD6"
            strokeWidth="3"
            strokeDasharray="8 6"
          />
          <text
            x={f.x + 42}
            y={f.y + 80}
            textAnchor="middle"
            fontFamily="Be Vietnam Pro, sans-serif"
            fontSize="44"
            fontWeight="800"
            fill="#1B5FD6"
          >
            P
          </text>
        </g>
      ))}
    </svg>
  );
}

export default function Home() {
  const session = useSyncExternalStore(
    subscribeAuthSession,
    getAuthSession,
    getAuthSession
  );
  const [systemStatus, setSystemStatus] = useState({
    health: null,
    info: null,
    healthError: "",
    infoError: "",
    loading: true,
    checkedAt: null,
  });
  const loadSystemStatus = useCallback(async () => {
    setSystemStatus((current) => ({ ...current, loading: true }));
    const [healthResult, infoResult] = await Promise.allSettled([
      apiRequest("/system/health"),
      apiRequest("/system/info"),
    ]);

    setSystemStatus({
      health: healthResult.status === "fulfilled" ? healthResult.value : null,
      info: infoResult.status === "fulfilled" ? infoResult.value : null,
      healthError: healthResult.status === "rejected" ? healthResult.reason.message : "",
      infoError: infoResult.status === "rejected" ? infoResult.reason.message : "",
      loading: false,
      checkedAt: new Date(),
    });
  }, []);

  useEffect(() => {
    void loadSystemStatus();
    const refreshTimer = window.setInterval(loadSystemStatus, 30000);
    return () => window.clearInterval(refreshTimer);
  }, [loadSystemStatus]);

  const healthOnline = systemStatus.health?.status === "Healthy";
  const infoOnline = systemStatus.info?.status === "Online";
  const apiOnline = healthOnline && infoOnline;
  const apiPartiallyOnline = healthOnline || infoOnline;
  const apiStatusLabel = apiOnline
    ? "Đang hoạt động"
    : apiPartiallyOnline
      ? "Hoạt động một phần"
      : "Chưa kết nối";
  const formatTimestamp = (value) => {
    if (!value) return "Chưa có dữ liệu";
    const date = new Date(value);
    return Number.isNaN(date.getTime()) ? "Chưa có dữ liệu" : date.toLocaleString("vi-VN");
  };

  return (
    <>
      {/* ---------- Giới thiệu ---------- */}
      <section id="gioi-thieu" className="container hero">
        <div className="hero__text">
          <span className="tag">Giới thiệu</span>
          <h1>Smart-Parking là gì?</h1>
          <p className="lead">
            Smart-Parking (bãi đỗ xe thông minh) là hệ thống quản lý bãi đỗ xe kết hợp thẻ
            cứng, ứng dụng di động và camera. Camera ghi lại biển số và màu xe của từng lượt
            ra vào, còn app giúp tài xế xem chỗ trống và đăng ký chỗ đậu trước.
          </p>
          <ul className="bullets">
            {highlights.map((h) => (
              <li key={h}>{h}</li>
            ))}
          </ul>
          <div className="hero__cta">
            <AnchorLink id="cach-hoat-dong" className="btn btn--primary btn--lg">
              Xem cách thức hoạt động
            </AnchorLink>
            {session?.userProfile.role === "USER" ? (
              <Link to="/customer" className="btn btn--dark-outline btn--lg">
                Đăng ký bãi đỗ / chọn chỗ đỗ
              </Link>
            ) : !session ? (
              <Link to="/login" className="btn btn--dark-outline btn--lg">
                Đăng nhập để chọn bãi đỗ
              </Link>
            ) : null}
          </div>
        </div>
        <div className="hero__art">
          <ParkingIllustration />
        </div>
      </section>

      {/* ---------- Cách thức hoạt động ---------- */}
      <section id="cach-hoat-dong" className="band band--white">
        <div className="container section">
          <SectionHead
            tag="Cách thức hoạt động"
            title="Hai cách để gửi xe"
            text="Bạn có thể quẹt thẻ cứng ngay tại cổng hoặc đăng ký chỗ đậu trước bằng ứng dụng. Cả hai cách đều được camera ghi lại biển số và màu xe."
          />
          <div className="grid-2">
            {methods.map((m) => (
              <article key={m.id} className="card card--soft">
                <div className="card__head">
                  <span className="icon-box icon-box--blue">
                    <Icon name={m.icon} />
                  </span>
                  <h3>{m.title}</h3>
                </div>
                <ol className="steps">
                  {m.steps.map((s, i) => (
                    <li key={s.title}>
                      <span className="steps__num">{i + 1}</span>
                      <div>
                        <strong>{s.title}</strong>
                        <div className="muted">{s.text}</div>
                      </div>
                    </li>
                  ))}
                </ol>
              </article>
            ))}
          </div>
        </div>
      </section>

      {/* ---------- Hệ thống ---------- */}
      <section id="he-thong" className="container section">
        <SectionHead tag="Hệ thống" title="Các thành phần của Smart-Parking" />
        <div className="grid-2">
          <article className="card card--dark">
            <span className="icon-box icon-box--yellow">
              <Icon name="camera" />
            </span>
            <h3>Camera lưu biển số và màu xe</h3>
            <p>
              Camera đặt tại cổng ra vào chỉ làm một việc: ghi lại biển số xe và màu xe của mỗi
              lượt vào, ra. Dữ liệu này được lưu cùng thẻ hoặc lượt đặt chỗ trên app để đối chiếu
              khi xe rời bãi.
            </p>
            <div className="chips-row">
              <div className="chip-box">
                <small>Lưu lại</small>
                <strong>Biển số xe</strong>
              </div>
              <div className="chip-box">
                <small>Lưu lại</small>
                <strong>Màu xe</strong>
              </div>
            </div>
          </article>

          <article className="card card--white">
            <span className="icon-box icon-box--blue">
              <Icon name="pin" />
            </span>
            <h3>Trạng thái API hệ thống</h3>
            <p className="muted">
              Theo dõi tình trạng hoạt động và thông tin môi trường từ các API hiện có của Smart
              Parking.
            </p>
            <div className="api-status-card">
              <div className="api-status-card__heading">
                <span className={`api-status-card__indicator ${apiOnline ? "is-online" : apiPartiallyOnline ? "is-partial" : ""}`} />
                <div>
                  <strong>{systemStatus.info?.service || "Smart Parking API"}</strong>
                  <span>{systemStatus.loading ? "Đang kiểm tra kết nối..." : apiStatusLabel}</span>
                </div>
              </div>
              <dl className="api-status-card__details">
                <div>
                  <dt>Health check</dt>
                  <dd>{systemStatus.health?.status || (systemStatus.healthError ? "Không khả dụng" : "Đang kiểm tra")}</dd>
                </div>
                <div>
                  <dt>API hệ thống</dt>
                  <dd>{systemStatus.info?.status || (systemStatus.infoError ? "Không khả dụng" : "Đang kiểm tra")}</dd>
                </div>
                <div>
                  <dt>Môi trường</dt>
                  <dd>{systemStatus.info?.environment || "Chưa có dữ liệu"}</dd>
                </div>
                <div>
                  <dt>Kiểm tra lần cuối</dt>
                  <dd>{formatTimestamp(systemStatus.checkedAt)}</dd>
                </div>
              </dl>
              {(systemStatus.healthError || systemStatus.infoError) && (
                <div className="api-status-card__error" role="alert">
                  {[systemStatus.healthError, systemStatus.infoError].filter(Boolean).join(" ")}
                </div>
              )}
              <button
                type="button"
                className="api-status-card__refresh"
                onClick={() => void loadSystemStatus()}
                disabled={systemStatus.loading}
              >
                {systemStatus.loading ? "Đang kiểm tra..." : "Kiểm tra lại"}
              </button>
            </div>
          </article>
        </div>
      </section>

      {!session && (
        <section className="container cta-wrap">
          <div className="cta">
            <div>
              <h2>Sẵn sàng đậu xe thông minh hơn?</h2>
              <p>Tạo tài khoản để đặt chỗ từ app và xem lịch sử gửi xe của bạn.</p>
            </div>
            <div className="cta__actions">
              <Link to="/login" className="btn btn--white-outline btn--lg">
                Đăng nhập
              </Link>
              <Link to="/register" className="btn btn--yellow btn--lg">
                Đăng ký
              </Link>
            </div>
          </div>
        </section>
      )}
    </>
  );
}
