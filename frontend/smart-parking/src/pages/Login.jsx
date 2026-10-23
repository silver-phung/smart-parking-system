import { Component } from "react";
import { Link, useNavigate } from "react-router-dom";
import AuthLayout from "../components/AuthLayout.jsx";
import { apiRequest } from "../services/api.js";

class LoginForm extends Component {
  state = { username: "", password: "", remember: false, error: "", loading: false };

  onChange = (event) => {
    const { name, type, checked, value } = event.target;
    this.setState({ [name]: type === "checkbox" ? checked : value, error: "" });
  };

  onSubmit = async (event) => {
    event.preventDefault();
    this.setState({ loading: true, error: "" });
    try {
      await apiRequest("/auth/login", {
        method: "POST",
        body: JSON.stringify({
          username: this.state.username,
          password: this.state.password,
          remember: this.state.remember,
        }),
      });
      this.props.navigate("/");
    } catch (error) {
      this.setState({ error: error.message });
    } finally {
      this.setState({ loading: false });
    }
  };

  render() {
    return (
      <AuthLayout>
        <div className="auth-form-wrap">
          <p className="auth-content__eyebrow">CHÀO MỪNG BẠN TRỞ LẠI</p>
          <h1>Đăng nhập</h1>
          <p className="auth-content__description">
            Đăng nhập để tiếp tục hành trình đỗ xe thuận tiện hơn.
          </p>

          <form className="auth-form" onSubmit={this.onSubmit}>
            <label htmlFor="login-username">Tên đăng nhập hoặc email</label>
            <input
              id="login-username"
              type="text"
              name="username"
              placeholder="Tên đăng nhập hoặc email"
              autoComplete="username"
              value={this.state.username}
              onChange={this.onChange}
              disabled={this.state.loading}
              required
            />

            <label htmlFor="login-password">Mật khẩu</label>
            <input
              id="login-password"
              type="password"
              name="password"
              placeholder="Nhập mật khẩu của bạn"
              autoComplete="current-password"
              value={this.state.password}
              onChange={this.onChange}
              disabled={this.state.loading}
              required
            />

            <div className="auth-form__options">
              <label className="auth-checkbox">
                <input
                  type="checkbox"
                  name="remember"
                  checked={this.state.remember}
                  onChange={this.onChange}
                  disabled={this.state.loading}
                />
                <span>Ghi nhớ đăng nhập</span>
              </label>
            </div>

            {this.state.error && (
              <p className="auth-form__message auth-form__message--error" role="alert">
                {this.state.error}
              </p>
            )}
            <button type="submit" className="auth-submit" disabled={this.state.loading}>
              {this.state.loading ? "Đang đăng nhập..." : "Đăng nhập"}
              {!this.state.loading && <span aria-hidden="true">→</span>}
            </button>
          </form>

          <p className="auth-switch">
            Chưa có tài khoản? <Link to="/register">Tạo tài khoản</Link>
          </p>
          <p className="auth-terms">
            Bằng việc tiếp tục, bạn đồng ý với các điều khoản sử dụng của chúng tôi.
          </p>
        </div>
      </AuthLayout>
    );
  }
}

export default function Login() {
  const navigate = useNavigate();
  return <LoginForm navigate={navigate} />;
}
