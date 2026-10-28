import { useState } from "react";
import { Link, useNavigate } from "react-router-dom";
import AuthLayout from "../components/AuthLayout.jsx";
import { apiRequest } from "../services/api.js";

export default function Register() {
  const navigate = useNavigate();
  const [form, setForm] = useState({
    name: "",
    email: "",
    plate: "",
    password: "",
  });
  const [error, setError] = useState("");
  const [loading, setLoading] = useState(false);

  const onChange = (event) => {
    setForm((current) => ({ ...current, [event.target.name]: event.target.value }));
    setError("");
  };

  const onSubmit = async (event) => {
    event.preventDefault();
    setLoading(true);
    setError("");
    try {
      await apiRequest("/auth/register", {
        method: "POST",
        body: JSON.stringify(form),
      });
      navigate("/");
    } catch (requestError) {
      setError(requestError.message);
    } finally {
      setLoading(false);
    }
  };

  return (
    <AuthLayout>
      <div className="auth-form-wrap">
        <p className="auth-content__eyebrow">BẮT ĐẦU NGAY HÔM NAY</p>
        <h1>Tạo tài khoản</h1>
        <p className="auth-content__description">
          Đăng ký miễn phí để đặt chỗ đỗ xe chỉ với vài thao tác.
        </p>

        <form className="auth-form" onSubmit={onSubmit}>
          <label htmlFor="register-name">Họ và tên</label>
          <input
            id="register-name"
            name="name"
            placeholder="Nguyễn Văn A"
            autoComplete="name"
            value={form.name}
            onChange={onChange}
            disabled={loading}
            required
          />

          <label htmlFor="register-email">Email</label>
          <input
            id="register-email"
            type="email"
            name="email"
            placeholder="you@example.com"
            autoComplete="email"
            value={form.email}
            onChange={onChange}
            disabled={loading}
            required
          />

          <label htmlFor="register-plate">
            Biển số xe <span className="auth-form__optional">(không bắt buộc)</span>
          </label>
          <input
            id="register-plate"
            name="plate"
            placeholder="Ví dụ: 30A-123.45"
            autoComplete="off"
            value={form.plate}
            onChange={onChange}
            disabled={loading}
          />

          <label htmlFor="register-password">Mật khẩu</label>
          <input
            id="register-password"
            type="password"
            name="password"
            placeholder="Tối thiểu 6 ký tự"
            autoComplete="new-password"
            minLength={6}
            value={form.password}
            onChange={onChange}
            disabled={loading}
            required
          />

          {error && (
            <p className="auth-form__message auth-form__message--error" role="alert">
              {error}
            </p>
          )}
          <button type="submit" className="auth-submit" disabled={loading}>
            {loading ? "Đang tạo tài khoản..." : "Tạo tài khoản"}
            {!loading && <span aria-hidden="true">→</span>}
          </button>
        </form>

        <p className="auth-switch">
          Đã có tài khoản? <Link to="/login">Đăng nhập</Link>
        </p>
        <p className="auth-terms">
          Bằng việc tiếp tục, bạn đồng ý với các điều khoản sử dụng của chúng tôi.
        </p>
      </div>
    </AuthLayout>
  );
}
