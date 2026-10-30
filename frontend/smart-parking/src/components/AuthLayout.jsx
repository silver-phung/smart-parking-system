import { NavLink } from "react-router-dom";

export default function AuthLayout({ title, description, children }) {
  return (
    <section className="auth-page">
      <div className="auth-modal" role="dialog" aria-labelledby="auth-title">
        <nav className="auth-tabs" aria-label="Đăng nhập hoặc đăng ký">
          <NavLink to="/login" className="auth-tabs__item">
            Đăng nhập
          </NavLink>
          <NavLink to="/register" className="auth-tabs__item">
            Đăng ký
          </NavLink>
        </nav>
        <div className="auth-form-wrap">
          <h1 id="auth-title">{title}</h1>
          {description && <p className="auth-content__description">{description}</p>}
          {children}
        </div>
      </div>
    </section>
  );
}
