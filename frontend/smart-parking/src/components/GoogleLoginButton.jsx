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
      mount.replaceChildren();
    };
  }, [attempt]);

  return (
    <div className="auth-google">
      <div className="auth-google__divider"><span>hoặc</span></div>
      <div className="auth-google__button" ref={target} inert={disabled ? "" : undefined} />
      {state !== "ready" && (
        <button type="button" className="auth-google__placeholder" disabled={state !== "unavailable" || disabled}
          onClick={() => retry(value => value + 1)}>
          {state === "unavailable" ? "Thử tải lại đăng nhập Google" : "Đăng nhập bằng Google"}
        </button>
      )}
      {state === "unconfigured" && <small>Đăng nhập Google chưa được cấu hình.</small>}
    </div>
  );
}
