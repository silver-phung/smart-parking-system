import { useEffect, useRef, useState, useSyncExternalStore } from "react";
import { Link, useLocation } from "react-router-dom";
import AnchorLink from "./AnchorLink.jsx";
import { apiRequest } from "../services/api.js";
import { staffApi } from "../services/staffApi.js";
import { staffDutiesOf, staffNavigationFor } from "../services/staffDuty.js";
import {
  dashboardPath,
  navIsActive,
  roleNavigation,
} from "../services/navigation.js";
import {
  clearAuthSession,
  getAuthSession,
  subscribeAuthSession,
} from "../services/authSession.js";
export function Logo({ small = false }) {
  return (
    <span className={small ? "logo logo--small" : "logo"}>
      <span className="logo__mark">P</span>
      <span className="logo__text">SMART-PARKING</span>
    </span>
  );
}
export default function Header({ workspace = false }) {
  const headerRef = useRef(null);
  useEffect(() => {
    const updateHeight = () => {
      const height = headerRef.current?.getBoundingClientRect().height;
      if (height)
        document.documentElement.style.setProperty(
          "--site-header-height",
          `${height}px`,
        );
    };
    updateHeight();
    const observer =
      typeof ResizeObserver !== "undefined"
        ? new ResizeObserver(updateHeight)
        : null;
    if (headerRef.current) observer?.observe(headerRef.current);
    return () => {
      observer?.disconnect();
      document.documentElement.style.removeProperty("--site-header-height");
    };
  }, []);
  const { pathname, search } = useLocation();
  const session = useSyncExternalStore(
    subscribeAuthSession,
    getAuthSession,
    getAuthSession,
  );
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const [profileOpen, setProfileOpen] = useState(false);
  const [accountModalOpen, setAccountModalOpen] = useState(false);
  const [accountDetails, setAccountDetails] = useState(null);
  const [accountLoading, setAccountLoading] = useState(false);
  const profileRef = useRef(null);
  const role = session?.userProfile.role;
  const userId = session?.userProfile.id;
  const displayName = session?.userProfile?.fullName || session?.userProfile?.username || "Tài khoản";
  const avatarInitial = displayName.trim().charAt(0).toUpperCase() || "U";

  useEffect(() => {
    setProfileOpen(false);
  }, [pathname, search]);

  useEffect(() => {
    if (!profileOpen) return undefined;
    const handlePointerDown = (event) => {
      if (profileRef.current && !profileRef.current.contains(event.target)) {
        setProfileOpen(false);
      }
    };
    const handleKeyDown = (event) => {
      if (event.key === "Escape") setProfileOpen(false);
    };
    document.addEventListener("mousedown", handlePointerDown);
    document.addEventListener("keydown", handleKeyDown);
    return () => {
      document.removeEventListener("mousedown", handlePointerDown);
      document.removeEventListener("keydown", handleKeyDown);
    };
  }, [profileOpen]);

  useEffect(() => {
    if (!accountModalOpen || !session) return undefined;
    const controller = new AbortController();
    setAccountLoading(true);
    apiRequest("/v1/users/me", { signal: controller.signal })
      .then((data) => {
        if (!controller.signal.aborted && data) setAccountDetails(data);
      })
      .catch(() => {})
      .finally(() => {
        if (!controller.signal.aborted) setAccountLoading(false);
      });
    const handleKeyDown = (event) => {
      if (event.key === "Escape") setAccountModalOpen(false);
    };
    document.addEventListener("keydown", handleKeyDown);
    return () => {
      controller.abort();
      document.removeEventListener("keydown", handleKeyDown);
    };
  }, [accountModalOpen, session]);
  // Staff chỉ thấy menu thuộc loại phân công của mình (trực cổng hoặc trực khu vực).
  const [staffDuties, setStaffDuties] = useState(null);
  useEffect(() => {
    if (role !== "STAFF") { setStaffDuties(null); return undefined; }
    const controller = new AbortController();
    staffApi.assignedLots(controller.signal)
      .then((lots) => { if (!controller.signal.aborted) setStaffDuties(staffDutiesOf(Array.isArray(lots) ? lots : [])); })
      .catch(() => { if (!controller.signal.aborted) setStaffDuties([]); });
    return () => controller.abort();
  }, [role, userId]);
  const navItems = role === "STAFF"
    ? [["/parking", "3D"], ...(staffDuties === null ? [] : staffNavigationFor(staffDuties))]
    : roleNavigation[role] || [];
  const roleLabel = { ADMIN: "Quản trị viên", MANAGER: "Quản lý bãi", STAFF: "Nhân viên", USER: "Khách hàng" }[role] || "Khách tham quan";
  const activePage = pathname === "/parking" ? "Bãi đỗ 3D" : navItems.find(([target]) => navIsActive(target, pathname, search))?.[1] || (pathname.startsWith("/parking") ? "Bãi đỗ 3D" : "Khu vực làm việc");
  async function logout() {
    setBusy(true);
    setError("");
    try {
      await apiRequest("/v1/auth/logout", {
        method: "POST",
        body: JSON.stringify({ refreshToken: session.refreshToken }),
        skipAuth: true,
      });
      clearAuthSession();
    } catch (e) {
      setError(e.message);
    } finally {
      setBusy(false);
    }
  }
  return (
    <header className={`header ${workspace ? "header--workspace" : ""}`} ref={headerRef}>
      <div className="container header__inner">
        <Link
          className="header__brand"
          to="/"
          aria-label="Smart-Parking - trang chủ"
        >
          <Logo />

        </Link>
        {workspace && <div className="header__context"><small>{roleLabel}</small><strong>{activePage}</strong></div>}
        <div className="header__actions">
          {session ? (
            <>
              {!workspace && (
                <Link className="btn btn--outline" to={dashboardPath(role)}>
                  Khu vực của tôi
                </Link>
              )}
              <div className="header__profile" ref={profileRef}>
                <button
                  type="button"
                  className={`header__profile-trigger ${profileOpen ? "is-open" : ""}`}
                  onClick={() => setProfileOpen((prev) => !prev)}
                  aria-expanded={profileOpen}
                  aria-haspopup="menu"
                >
                  <span className="header__profile-avatar" aria-hidden="true">
                    {avatarInitial}
                  </span>
                  <span className="header__profile-text">
                    <strong>{displayName}</strong>
                    <small>{roleLabel}</small>
                  </span>
                  <span className="header__profile-caret" aria-hidden="true">
                    ▾
                  </span>
                </button>
                {profileOpen && (
                  <div className="header__profile-popup" role="menu" aria-label="Menu tài khoản">
                    <div className="header__profile-summary">
                      <strong>{displayName}</strong>
                      <span className="header__profile-role">{roleLabel}</span>
                    </div>
                    <button
                      type="button"
                      className="header__profile-item"
                      role="menuitem"
                      onClick={() => {
                        setProfileOpen(false);
                        setAccountModalOpen(true);
                      }}
                    >
                      Thông tin cá nhân
                    </button>
                    <button
                      type="button"
                      className="header__profile-logout"
                      role="menuitem"
                      onClick={logout}
                      disabled={busy}
                    >
                      {busy ? "Đang đăng xuất…" : "Đăng xuất"}
                    </button>
                  </div>
                )}
              </div>
            </>
          ) : (
            <>
              <Link className="btn btn--outline" to="/register">
                Đăng ký
              </Link>
              <Link className="btn btn--primary" to="/login">
                Đăng nhập
              </Link>
            </>
          )}
          {error && (
            <p className="header__session-error" role="alert">
              {error}
            </p>
          )}
        </div>
        <nav className="header__nav" aria-label="Điều hướng chính">
          {session ? (
            navItems.map(([target, label]) => (
              <Link
                key={target}
                to={target}
                className={
                  [navIsActive(target, pathname, search) ? "is-active" : "", target === "/parking" ? "nav-3d" : ""].filter(Boolean).join(" ")
                }
                aria-current={
                  navIsActive(target, pathname, search) ? "page" : undefined
                }
              >
                {label}
              </Link>
            ))
          ) : workspace ? <Link to="/parking" className="is-active" aria-current="page">3D</Link> : (
            <>
              <Link
                to="/parking"
                className={pathname === "/parking" ? "is-active" : ""}
              >
                Xem bãi đỗ
              </Link>
              <AnchorLink id="gioi-thieu">Giới thiệu</AnchorLink>
              <AnchorLink id="cach-hoat-dong">Cách thức hoạt động</AnchorLink>
              <AnchorLink id="he-thong">Hệ thống</AnchorLink>
            </>
          )}
        </nav>
      </div>
      {session && accountModalOpen && (
        <div
          className="header__account-backdrop"
          role="dialog"
          aria-modal="true"
          aria-label="Thông tin tài khoản"
          onMouseDown={(e) => {
            if (e.target === e.currentTarget) setAccountModalOpen(false);
          }}
        >
          <div className="header__account-modal">
            <div className="header__account-modal-head">
              <div className="header__account-modal-user">
                <span className="header__profile-avatar header__profile-avatar--large" aria-hidden="true">
                  {avatarInitial}
                </span>
                <div>
                  <h3>Thông tin tài khoản</h3>
                  <span className="header__profile-role">{roleLabel}</span>
                </div>
              </div>
              <button
                type="button"
                className="header__account-close"
                aria-label="Đóng"
                onClick={() => setAccountModalOpen(false)}
              >
                ✕
              </button>
            </div>
            <dl className="header__account-list">
              <div>
                <dt>Họ và tên</dt>
                <dd>{accountDetails?.fullName || session.userProfile.fullName || "Chưa cập nhật"}</dd>
              </div>
              <div>
                <dt>Tên đăng nhập</dt>
                <dd>{accountDetails?.username || session.userProfile.username || "—"}</dd>
              </div>
              <div>
                <dt>Chức vụ</dt>
                <dd>{roleLabel}</dd>
              </div>
              <div>
                <dt>Email</dt>
                <dd>{accountDetails?.email || session.userProfile.email || "Chưa cập nhật"}</dd>
              </div>
              <div>
                <dt>Số điện thoại</dt>
                <dd>{accountDetails?.phone || session.userProfile.phone || "Chưa cập nhật"}</dd>
              </div>
              <div>
                <dt>Trạng thái</dt>
                <dd>
                  {(accountDetails?.status || session.userProfile.status || "ACTIVE") === "ACTIVE"
                    ? "Đang hoạt động"
                    : accountDetails?.status || session.userProfile.status}
                </dd>
              </div>
            </dl>
            {accountLoading && <p className="header__account-loading">Đang đồng bộ thông tin tài khoản…</p>}
            <div className="header__account-modal-foot">
              <button
                type="button"
                className="btn btn--outline"
                onClick={() => setAccountModalOpen(false)}
              >
                Đóng
              </button>
            </div>
          </div>
        </div>
      )}
    </header>
  );
}
