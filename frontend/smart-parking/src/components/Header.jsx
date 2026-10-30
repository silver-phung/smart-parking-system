import { useState, useSyncExternalStore } from "react";
import { Link, NavLink, useLocation } from "react-router-dom";
import AnchorLink from "./AnchorLink.jsx";
import { apiRequest } from "../services/api.js";
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

const dashboardPaths = {
  ADMIN: "/admin",
  MANAGER: "/manager",
  STAFF: "/staff",
};

export default function Header() {
  const { pathname } = useLocation();
  const [logoutError, setLogoutError] = useState("");
  const [loggingOut, setLoggingOut] = useState(false);
  const session = useSyncExternalStore(
    subscribeAuthSession,
    getAuthSession,
    getAuthSession,
  );

  const logout = async () => {
    setLoggingOut(true);
    setLogoutError("");
    try {
      await apiRequest("/v1/auth/logout", {
        method: "POST",
        body: JSON.stringify({ refreshToken: session.refreshToken }),
      });
      clearAuthSession();
    } catch (error) {
      setLogoutError(error.message);
    } finally {
      setLoggingOut(false);
    }
  };

  return (
    <header
      className={`header${session?.userProfile.role === "STAFF" ? " header--staff" : ""}`}
    >
      <div className="container header__inner">
        <Link to="/" aria-label="Smart-Parking - trang chủ">
          <Logo />
        </Link>

        <nav className="header__nav" aria-label="Điều hướng chính">
          {session?.userProfile.role === "ADMIN" ? (
            <>
              <Link to="/admin#overview">Tổng quan</Link>
              <Link to="/admin/people">Tài khoản</Link>
              <Link to="/admin/people?view=assignments">Phân công</Link>
              <Link to="/admin#parking-lots">Bãi đỗ</Link>
              <Link to="/admin#active-sessions">Xe đang gửi</Link>
              <Link to="/admin#finance">Tài chính</Link>
            </>
          ) : session?.userProfile.role === "STAFF" ? (
            <>
              <Link to="/parking">Xem bãi đỗ</Link>
              <Link to="/staff?view=entry">Xe vào</Link>
              <Link to="/staff?view=exit">Xe ra</Link>
              <Link to="/staff?view=verify">Xác minh ô</Link>
            </>
          ) : (
            <>
              <Link to="/parking">Xem bãi đỗ</Link>
              <AnchorLink id="gioi-thieu">Giới thiệu</AnchorLink>
              <AnchorLink id="cach-hoat-dong">Cách thức hoạt động</AnchorLink>
              <AnchorLink id="he-thong">Hệ thống</AnchorLink>
            </>
          )}
        </nav>

        <div className="header__actions">
          {session ? (
            <div className="header__session" key={pathname}>
              <span>
                {session.userProfile.fullName || session.userProfile.username} ·{" "}
                {session.userProfile.role}
              </span>
              {session.userProfile.role === "USER" ? (
                <>
                  <NavLink to="/customer" className="btn btn--outline">
                    Bãi đỗ
                  </NavLink>
                  <NavLink to="/customer/profile" className="btn btn--outline">
                    Hồ sơ
                  </NavLink>
                  <NavLink to="/customer/sessions" className="btn btn--outline">
                    Phiên của tôi
                  </NavLink>
                </>
              ) : dashboardPaths[session.userProfile.role] ? (
                <NavLink
                  to={dashboardPaths[session.userProfile.role]}
                  className="btn btn--outline"
                >
                  Bảng điều khiển
                </NavLink>
              ) : null}
              <button
                type="button"
                className="btn btn--outline"
                onClick={logout}
                disabled={loggingOut}
              >
                {loggingOut ? "Đang đăng xuất..." : "Đăng xuất"}
              </button>
              {logoutError && (
                <span className="header__session-error" role="alert">
                  {logoutError}
                </span>
              )}
            </div>
          ) : (
            <>
              <NavLink to="/login" className="btn btn--primary">
                Đăng nhập
              </NavLink>
            </>
          )}
        </div>
      </div>
    </header>
  );
}
