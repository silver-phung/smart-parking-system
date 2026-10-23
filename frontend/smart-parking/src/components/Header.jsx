import { Link, NavLink } from "react-router-dom";
import AnchorLink from "./AnchorLink.jsx";

export function Logo({ small = false }) {
  return (
    <span className={small ? "logo logo--small" : "logo"}>
      <span className="logo__mark">P</span>
      <span className="logo__text">SMART-PARKING</span>
    </span>
  );
}

export default function Header() {
  return (
    <header className="header">
      <div className="container header__inner">
        <Link to="/" aria-label="Smart-Parking - trang chủ">
          <Logo />
        </Link>

        <nav className="header__nav" aria-label="Điều hướng chính">
          <AnchorLink id="gioi-thieu">Giới thiệu</AnchorLink>
          <AnchorLink id="cach-hoat-dong">Cách thức hoạt động</AnchorLink>
          <AnchorLink id="he-thong">Hệ thống</AnchorLink>
        </nav>

        <div className="header__actions">
          <NavLink to="/login" className="btn btn--outline">
            Đăng nhập
          </NavLink>
          <NavLink to="/register" className="btn btn--primary">
            Đăng ký
          </NavLink>
        </div>
      </div>
    </header>
  );
}
