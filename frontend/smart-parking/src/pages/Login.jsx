import { Component } from "react";
import { Link, useNavigate } from "react-router-dom";
import AuthLayout from "../components/AuthLayout.jsx";
import { apiRequest } from "../services/api.js";
import { setAuthSession } from "../services/authSession.js";

const demoAccounts = {
  ADMIN: "admin",
  MANAGER: "manager",
  STAFF: "staff",
  USER: "user",
};

class LoginForm extends Component {
  state = {
    role: "USER",
    username: demoAccounts.USER,
    password: "",
    error: "",
    loading: false,
  };

  onChange = (event) => {
    const { name, value } = event.target;
    this.setState((current) => ({
      [name]: value,
      ...(name === "role" ? { username: demoAccounts[value] } : {}),
      error: "",
    }));
  };

  onSubmit = async (event) => {
    event.preventDefault();
    this.setState({ loading: true, error: "" });
    try {
      const response = await apiRequest("/v1/auth/login", {
        method: "POST",
        body: JSON.stringify({
          username: this.state.username,
          password: this.state.password,
        }),
        skipAuth: true,
      });
      if (!response?.data?.accessToken || !response.data.refreshToken || !response.data.userProfile) {
        throw new Error("Phản hồi đăng nhập từ máy chủ không hợp lệ.");
      }
      setAuthSession(response.data);
      const dashboardPaths = {
        ADMIN: "/admin",
        MANAGER: "/manager",
        STAFF: "/staff",
        USER: "/customer",
      };
      this.props.navigate(dashboardPaths[response.data.userProfile.role] || "/");
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
            <label htmlFor="login-role">Tài khoản thử nghiệm</label>
            <select
              id="login-role"
              name="role"
              value={this.state.role}
              onChange={this.onChange}
              disabled={this.state.loading}
            >
              <option value="ADMIN">Quản trị viên (admin)</option>
              <option value="MANAGER">Quản lý (manager)</option>
              <option value="STAFF">Nhân viên (staff)</option>
              <option value="USER">Người dùng (user)</option>
            </select>

            <label htmlFor="login-username">Tên đăng nhập</label>
            <input
              id="login-username"
              type="text"
              name="username"
              placeholder="Tên đăng nhập"
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

          <p className="auth-switch">Đăng nhập bằng username và mật khẩu tài khoản thử nghiệm.</p>
          <p className="auth-switch">
            Đăng ký tài khoản mới? <Link to="/register">Tạo tài khoản</Link>
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
