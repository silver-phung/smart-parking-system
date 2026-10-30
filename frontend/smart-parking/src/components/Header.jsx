import { useState, useSyncExternalStore } from "react";
import { Link, useLocation } from "react-router-dom";
import AnchorLink from "./AnchorLink.jsx";
import { apiRequest } from "../services/api.js";
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
export default function Header() {
  const { pathname, search } = useLocation();
  const session = useSyncExternalStore(
    subscribeAuthSession,
    getAuthSession,
    getAuthSession,
  );
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const role = session?.userProfile.role;
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
    <header className="header">
      <div className="container header__inner">
        <Link
          className="header__brand"
          to="/"
          aria-label="Smart-Parking - trang chủ"
        >
          <Logo />
        </Link>
        <div className="header__actions">
          {session ? (
            <>
              <span className="header__identity">
                {session.userProfile.fullName || session.userProfile.username}
                <small>{role}</small>
              </span>
              <Link className="btn btn--outline" to={dashboardPath(role)}>
                Khu vực của tôi
              </Link>
              <button
                className="btn btn--outline"
                onClick={logout}
                disabled={busy}
              >
                {busy ? "Đang đăng xuất…" : "Đăng xuất"}
              </button>
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
            (roleNavigation[role] || []).map(([target, label]) => (
              <Link
                key={target}
                to={target}
                className={
                  navIsActive(target, pathname, search) ? "is-active" : ""
                }
                aria-current={
                  navIsActive(target, pathname, search) ? "page" : undefined
                }
              >
                {label}
              </Link>
            ))
          ) : (
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
    </header>
  );
}
