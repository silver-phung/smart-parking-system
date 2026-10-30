import { useEffect, useState } from "react";
import { Link, useNavigate } from "react-router-dom";
import AuthLayout from "../components/AuthLayout.jsx";
import { apiRequest } from "../services/api.js";

const RESEND_COOLDOWN_SECONDS = 60;
const EMAIL_PATTERN = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

export default function Register() {
  const navigate = useNavigate();
  const [form, setForm] = useState({ email: "", password: "", confirmPassword: "", otp: "" });
  const [otpSent, setOtpSent] = useState(false);
  const [cooldown, setCooldown] = useState(0);
  const [sending, setSending] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState("");
  const [info, setInfo] = useState("");

  useEffect(() => {
    if (cooldown <= 0) return undefined;
    const timer = setTimeout(() => setCooldown((value) => value - 1), 1000);
    return () => clearTimeout(timer);
  }, [cooldown]);

  const onChange = (event) => {
    const { name, value } = event.target;
    setForm((current) => ({ ...current, [name]: name === "otp" ? value.replace(/\D/g, "").slice(0, 6) : value }));
    setError("");
  };

  const validateAccount = () => {
    if (!EMAIL_PATTERN.test(form.email.trim())) return "Địa chỉ email không đúng định dạng.";
    if (form.password.length < 8) return "Mật khẩu phải có tối thiểu 8 ký tự.";
    if (form.password !== form.confirmPassword) return "Mật khẩu nhập lại không khớp.";
    return "";
  };

  const sendOtp = async () => {
    const problem = validateAccount();
    if (problem) {
      setError(problem);
      return;
    }
    setSending(true);
    setError("");
    setInfo("");
    const email = form.email.trim();
    try {
      const response = otpSent
        ? await apiRequest("/v1/auth/resend-verification", {
            method: "POST",
            body: JSON.stringify({ email }),
            skipAuth: true,
          })
        : await apiRequest("/v1/auth/register", {
            method: "POST",
            body: JSON.stringify({ email, password: form.password }),
            skipAuth: true,
          });
      setOtpSent(true);
      setCooldown(RESEND_COOLDOWN_SECONDS);
      setInfo(response?.message || "Mã OTP đã được gửi tới email của bạn.");
    } catch (requestError) {
      setError(requestError.message);
    } finally {
      setSending(false);
    }
  };

  const onSubmit = async (event) => {
    event.preventDefault();
    if (!otpSent) {
      setError("Vui lòng bấm “Gửi mã” để nhận OTP qua email.");
      return;
    }
    if (!/^\d{6}$/.test(form.otp)) {
      setError("Mã OTP gồm 6 chữ số.");
      return;
    }
    setSubmitting(true);
    setError("");
    try {
      const response = await apiRequest("/v1/auth/verify-email", {
        method: "POST",
        body: JSON.stringify({ email: form.email.trim(), code: form.otp }),
        skipAuth: true,
      });
      navigate("/login", {
        replace: true,
        state: { notice: response?.message || "Đăng ký thành công. Bạn có thể đăng nhập." },
      });
    } catch (requestError) {
      setError(requestError.message);
    } finally {
      setSubmitting(false);
    }
  };

  const busy = sending || submitting;

  return (
    <AuthLayout title="Đăng ký" description="Tạo tài khoản bằng email, xác minh bằng mã OTP.">
      <form className="auth-form auth-form--compact" onSubmit={onSubmit} noValidate>
        <label htmlFor="register-email">Email</label>
        <input
          id="register-email"
          type="email"
          name="email"
          placeholder="ban@example.com"
          autoComplete="email"
          value={form.email}
          onChange={onChange}
          disabled={busy || otpSent}
          required
        />

        <div className="auth-form__row">
          <div>
            <label htmlFor="register-password">Mật khẩu</label>
            <input
              id="register-password"
              type="password"
              name="password"
              placeholder="Tối thiểu 8 ký tự"
              autoComplete="new-password"
              value={form.password}
              onChange={onChange}
              disabled={busy || otpSent}
              required
            />
          </div>
          <div>
            <label htmlFor="register-confirm">Nhập lại mật khẩu</label>
            <input
              id="register-confirm"
              type="password"
              name="confirmPassword"
              placeholder="Nhập lại mật khẩu"
              autoComplete="new-password"
              value={form.confirmPassword}
              onChange={onChange}
              disabled={busy || otpSent}
              required
            />
          </div>
        </div>

        <label htmlFor="register-otp">Mã OTP</label>
        <div className="auth-form__inline">
          <input
            id="register-otp"
            type="text"
            name="otp"
            inputMode="numeric"
            placeholder="6 chữ số"
            autoComplete="one-time-code"
            value={form.otp}
            onChange={onChange}
            disabled={busy || !otpSent}
          />
          <button
            type="button"
            className="auth-secondary"
            onClick={sendOtp}
            disabled={busy || cooldown > 0}
          >
            {sending ? "Đang gửi..." : cooldown > 0 ? `Gửi lại (${cooldown}s)` : otpSent ? "Gửi lại" : "Gửi mã"}
          </button>
        </div>

        {info && !error && (
          <p className="auth-form__message auth-form__message--success" role="status">{info}</p>
        )}
        {error && (
          <p className="auth-form__message auth-form__message--error" role="alert">{error}</p>
        )}

        <button type="submit" className="auth-submit" disabled={busy}>
          {submitting ? "Đang xác minh..." : "Đăng ký"}
          {!submitting && <span aria-hidden="true">→</span>}
        </button>
      </form>
      <p className="auth-switch">
        Đã có tài khoản? <Link to="/login">Đăng nhập</Link>
      </p>
    </AuthLayout>
  );
}
