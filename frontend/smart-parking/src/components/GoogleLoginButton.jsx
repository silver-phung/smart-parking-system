import { useEffect, useRef, useState } from "react";
import { apiRequest } from "../services/api.js";

let googleScript;
function loadGoogle() {
  if (window.google?.accounts?.id) return Promise.resolve(window.google.accounts.id);
  if (!googleScript) {
    googleScript = new Promise((resolve, reject) => {
      const script = document.createElement("script");
      script.src = "https://accounts.google.com/gsi/client";
      script.async = true;
      script.onload = () => {
        if (window.google?.accounts?.id) resolve(window.google.accounts.id);
        else reject(new Error("Chưa tải được dịch vụ đăng nhập Google."));
      };
      script.onerror = () => {
        script.remove();
        reject(new Error("Chưa tải được dịch vụ đăng nhập Google."));
      };
      document.head.append(script);
    }).catch(error => {
      googleScript = undefined;
      throw error;
    });
  }
  return googleScript;
}

export default function GoogleLoginButton({ onCredential, disabled }) {
  const target = useRef(null);
  const callback = useRef(onCredential);
  const [state, setState] = useState("loading");
  const [nonce, setNonce] = useState("");
  const [showDevPicker, setShowDevPicker] = useState(false);
  const [devEmail, setDevEmail] = useState("dphamtung123@gmail.com");
  const [attempt, retry] = useState(0);
  callback.current = onCredential;

  useEffect(() => {
    const controller = new AbortController();
    const mount = target.current;
    async function render() {
      try {
        const response = await apiRequest("/v1/auth/google/challenge", {
          skipAuth: true,
          signal: controller.signal,
        });
        if (controller.signal.aborted) return;
        if (!response?.data?.enabled) {
          setState("unconfigured");
          return;
        }

        setNonce(response.data.nonce || "");
        if (response.data.allowDevLogin) {
          setState("dev-ready");
          return;
        }

        const google = await loadGoogle();
        if (controller.signal.aborted) return;
        google.initialize({
          client_id: response.data.clientId,
          nonce: response.data.nonce,
          callback: async ({ credential }) => {
            if (controller.signal.aborted) return;
            const succeeded = await callback.current(credential, response.data.nonce);
            if (!succeeded && !controller.signal.aborted) retry(value => value + 1);
          },
        });
        google.renderButton(mount, {
          theme: "outline",
          size: "large",
          text: "signin_with",
          locale: "vi",
          width: Math.floor(Math.min(mount.getBoundingClientRect().width, 400)),
        });
        setState("ready");
      } catch {
        if (!controller.signal.aborted) setState("unavailable");
      }
    }
    setState("loading");
    render();
    return () => {
      controller.abort();
      if (mount) mount.replaceChildren();
    };
  }, [attempt]);

  const handleDevSignIn = async (emailToUse) => {
    const cleanEmail = (emailToUse || devEmail).trim().toLowerCase();
    if (!cleanEmail) return;
    const succeeded = await callback.current(`dev:${cleanEmail}`, nonce);
    if (!succeeded) retry(value => value + 1);
  };

  return (
    <div className="auth-google">
      <div className="auth-google__divider"><span>hoặc</span></div>
      <div className="auth-google__button" ref={target} inert={disabled ? "" : undefined} style={state === "dev-ready" ? { display: "none" } : undefined} />
      {state === "dev-ready" && (
        <div style={{ width: "100%" }}>
          <button
            type="button"
            disabled={disabled}
            onClick={() => setShowDevPicker(v => !v)}
            style={{
              width: "100%",
              display: "flex",
              alignItems: "center",
              justifyContent: "center",
              gap: "10px",
              padding: "10px 16px",
              borderRadius: "8px",
              border: "1px solid #dadce0",
              background: "#fff",
              color: "#3c4043",
              fontSize: "14px",
              fontWeight: 500,
              cursor: disabled ? "not-allowed" : "pointer",
              boxShadow: "0 1px 2px rgba(0,0,0,0.05)"
            }}
          >
            <svg width="18" height="18" viewBox="0 0 48 48" aria-hidden="true">
              <path fill="#EA4335" d="M24 9.5c3.54 0 6.71 1.22 9.21 3.6l6.85-6.85C35.9 2.38 30.47 0 24 0 14.62 0 6.51 5.38 2.56 13.22l7.98 6.19C12.43 13.72 17.74 9.5 24 9.5z"/>
              <path fill="#4285F4" d="M46.98 24.55c0-1.57-.15-3.09-.38-4.55H24v9.02h12.94c-.58 2.96-2.26 5.48-4.78 7.18l7.73 6c4.51-4.18 7.09-10.36 7.09-17.65z"/>
              <path fill="#FBBC05" d="M10.53 28.59c-.48-1.45-.76-2.99-.76-4.59s.27-3.14.76-4.59l-7.98-6.19C.92 16.46 0 20.12 0 24c0 3.88.92 7.54 2.56 10.78l7.97-6.19z"/>
              <path fill="#34A853" d="M24 48c6.48 0 11.93-2.13 15.89-5.81l-7.73-6c-2.15 1.45-4.92 2.3-8.16 2.3-6.26 0-11.57-4.22-13.47-9.91l-7.98 6.19C6.51 42.62 14.62 48 24 48z"/>
            </svg>
            <span>Đăng nhập bằng Google</span>
          </button>
          {showDevPicker && (
            <div style={{
              marginTop: "10px",
              padding: "14px",
              borderRadius: "10px",
              border: "1px solid #e2e8f0",
              background: "#f8fafc",
              display: "flex",
              flexDirection: "column",
              gap: "10px"
            }}>
              <div style={{ fontSize: "13px", fontWeight: 600, color: "#1e293b" }}>
                Chọn hoặc nhập tài khoản Gmail để đăng nhập:
              </div>
              <button
                type="button"
                disabled={disabled}
                onClick={() => handleDevSignIn("dphamtung123@gmail.com")}
                style={{
                  display: "flex",
                  alignItems: "center",
                  gap: "10px",
                  padding: "8px 12px",
                  borderRadius: "8px",
                  border: "1px solid #cbd5e1",
                  background: "#fff",
                  cursor: disabled ? "not-allowed" : "pointer",
                  textAlign: "left"
                }}
              >
                <span style={{
                  width: "28px",
                  height: "28px",
                  borderRadius: "50%",
                  background: "#2563eb",
                  color: "#fff",
                  display: "inline-flex",
                  alignItems: "center",
                  justifyContent: "center",
                  fontWeight: 700,
                  fontSize: "13px"
                }}>D</span>
                <div>
                  <div style={{ fontSize: "13px", fontWeight: 600, color: "#0f172a" }}>dphamtung123@gmail.com</div>
                  <div style={{ fontSize: "11px", color: "#64748b" }}>Bấm để đăng nhập ngay</div>
                </div>
              </button>
              <div style={{ display: "flex", gap: "8px" }}>
                <input
                  type="email"
                  value={devEmail}
                  disabled={disabled}
                  onChange={e => setDevEmail(e.target.value)}
                  placeholder="Nhập địa chỉ @gmail.com"
                  style={{
                    flex: 1,
                    padding: "8px 10px",
                    borderRadius: "6px",
                    border: "1px solid #cbd5e1",
                    fontSize: "13px"
                  }}
                />
                <button
                  type="button"
                  disabled={disabled || !devEmail.trim().toLowerCase().endsWith("@gmail.com")}
                  onClick={() => handleDevSignIn(devEmail)}
                  style={{
                    padding: "8px 14px",
                    borderRadius: "6px",
                    border: "none",
                    background: "#2563eb",
                    color: "#fff",
                    fontSize: "13px",
                    fontWeight: 600,
                    cursor: "pointer"
                  }}
                >
                  Tiếp tục
                </button>
              </div>
            </div>
          )}
        </div>
      )}
      {state !== "ready" && state !== "dev-ready" && (
        <button type="button" className="auth-google__placeholder" disabled={state !== "unavailable" || disabled}
          onClick={() => retry(value => value + 1)}>
          {state === "unavailable" ? "Thử tải lại đăng nhập Google" : "Đăng nhập bằng Google"}
        </button>
      )}
      {state === "unconfigured" && <small>Đăng nhập Google chưa được cấu hình.</small>}
    </div>
  );
}
