import { useEffect, useRef, useState } from "react";
import { Link, useLocation, useNavigate } from "react-router-dom";
import AuthLayout from "../components/AuthLayout.jsx";
import { apiRequest } from "../services/api.js";

export default function VerifyEmail() {
  const { state } = useLocation();
  const navigate = useNavigate();
  const emailInput = useRef(null);
  const [email, setEmail] = useState(state?.email || "");
  const [code, setCode] = useState("");
  const [message, setMessage] = useState(state?.message || "");
  const [error, setError] = useState("");
  const [action, setAction] = useState(null);
  const [resendAvailableAt, setResendAvailableAt] = useState(
    state?.sentAt ? state.sentAt + 60_000 : 0
  );
  const [secondsLeft, setSecondsLeft] = useState(0);

  useEffect(() => {
    const updateCountdown = () => setSecondsLeft(
      Math.max(0, Math.ceil((resendAvailableAt - Date.now()) / 1000))
    );
    updateCountdown();
    if (resendAvailableAt <= Date.now()) return;
    const timer = setInterval(updateCountdown, 1000);
    return () => clearInterval(timer);
  }, [resendAvailableAt]);

  const verify = async (event) => {
    event.preventDefault();
    setAction("verify");
    setError("");
    try {
      const response = await apiRequest("/v1/auth/verify-email", {
        method: "POST",
        body: JSON.stringify({ email: email.trim(), code: code.trim() }),
        skipAuth: true,
      });
      navigate("/login", {
        replace: true,
        state: {
          username: state?.username || "",
          message: response.message || "Xác minh email thành công. Bạn có thể đăng nhập.",
        },
      });
    } catch (requestError) {
      setError(requestError.message);
    } finally {
      setAction(null);
    }
  };

  const resend = async () => {
    if (!emailInput.current.reportValidity()) return;
    setAction("resend");
    setError("");
    setMessage("");
    try {
      const response = await apiRequest("/v1/auth/resend-verification", {
        method: "POST",
        body: JSON.stringify({ email: email.trim() }),
        skipAuth: true,
      });
      setMessage(response.message);
      setResendAvailableAt(Date.now() + 60_000);
    } catch (requestError) {
      setError(requestError.message);
    } finally {
      setAction(null);
    }
  };

  return (
    <AuthLayout>
      <div className="auth-form-wrap">
        <p className="auth-content__eyebrow">HOÀN TẤT ĐĂNG KÝ</p>
        <h1>Xác minh email</h1>
        <p className="auth-content__description">
          Nhập email đăng ký và mã OTP 6 chữ số để kích hoạt tài khoản.
        </p>
        <form className="auth-form" onSubmit={verify}>
          <label htmlFor="verify-email">Email đăng ký</label>
          <input
            ref={emailInput}
            id="verify-email"
            type="email"
            autoComplete="email"
            value={email}
            onChange={(event) => {
              setEmail(event.target.value);
              setCode("");
              setError("");
              setMessage("");
            }}
            disabled={!!action}
            required
          />
          <label htmlFor="verify-code">Mã xác minh</label>
          <input
            id="verify-code"
            inputMode="numeric"
            autoComplete="one-time-code"
            placeholder="Nhập 6 chữ số"
            pattern="[0-9]{6}"
            minLength={6}
            maxLength={6}
            value={code}
            onChange={(event) => {
              setCode(event.target.value);
              setError("");
            }}
            disabled={!!action}
            required
          />
          {message && (
            <p className="auth-form__message auth-form__message--success" role="status">
              {message}
            </p>
          )}
          {error && (
            <p className="auth-form__message auth-form__message--error" role="alert">
              {error}
            </p>
          )}
          <button type="submit" className="auth-submit" disabled={!!action}>
            {action === "verify" ? "Đang xác minh..." : "Xác minh tài khoản"}
          </button>
          <button
            type="button"
            className="auth-resend"
            onClick={resend}
            disabled={!!action || secondsLeft > 0}
          >
            {action === "resend" ? "Đang gửi mã..."
              : secondsLeft > 0 ? `Gửi lại mã sau ${secondsLeft}s` : "Gửi lại mã"}
          </button>
        </form>
        <p className="auth-switch">
          Đã xác minh tài khoản? <Link to="/login">Đăng nhập</Link>
        </p>
      </div>
    </AuthLayout>
  );
}
