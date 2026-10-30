import { useEffect, useRef, useState } from "react";
import { apiRequest } from "../services/api.js";
import "../workspace.css";

export function useResource(path, revision = 0, options = {}) {
  const [state, setState] = useState({ loading: true, data: null, error: "" });
  const skipAuth = Boolean(options.skipAuth);
  const allPages = options.allPages || "";
  useEffect(() => {
    const controller = new AbortController();
    setState({ loading: Boolean(path), data: null, error: "" });
    async function load() {
      const response = await apiRequest(path, {
        signal: controller.signal,
        skipAuth,
      });
      if (allPages && response?.data?.totalPages > 1) {
        const url = new URL(path, "http://local");
        const items = [...response.data.items];
        for (let page = 2; page <= response.data.totalPages; page++) {
          url.searchParams.set(allPages, page);
          const next = await apiRequest(url.pathname + url.search, {
            signal: controller.signal,
            skipAuth,
          });
          items.push(...next.data.items);
        }
        response.data.items = items;
      }
      return response;
    }
    if (path)
      load()
        .then((response) => {
          if (!controller.signal.aborted)
            setState({ loading: false, data: response?.data, error: "" });
        })
        .catch((error) => {
          if (!controller.signal.aborted)
            setState({ loading: false, data: null, error: error.message });
        });
    return () => controller.abort();
  }, [path, revision, skipAuth, allPages]);
  return state;
}

export function useMutation() {
  const locked = useRef(false);
  const [busy, setBusy] = useState(false);
  const [message, setMessage] = useState("");
  const [error, setError] = useState("");
  async function run(action, done) {
    if (locked.current) return;
    locked.current = true;
    setBusy(true);
    setError("");
    setMessage("");
    try {
      const response = await action();
      setMessage(response?.message || "Đã lưu thay đổi.");
      done?.(response);
    } catch (e) {
      setError(e.message);
    } finally {
      locked.current = false;
      setBusy(false);
    }
  }
  return { busy, message, error, run };
}

export const itemsOf = (data) =>
  Array.isArray(data) ? data : data?.items || [];
export const query = (values) =>
  new URLSearchParams(
    Object.entries(values).filter(([, value]) => value !== "" && value != null),
  ).toString();
export const write = (path, body, method = "POST") =>
  apiRequest(path, { method, body: JSON.stringify(body) });
export const dateLabel = (value) =>
  value ? new Date(value).toLocaleString("vi-VN") : "—";
export const statusLabel = (value) =>
  ({
    ACTIVE: "Đang gửi",
    PAID: "Đã thanh toán",
    COMPLETED: "Đã kết thúc",
    CANCELLED: "Đã hủy",
  })[value] || value;
export function Feedback({ loading, error, message }) {
  return (
    <>
      {loading && <p role="status">Đang tải dữ liệu…</p>}
      {error && (
        <p className="workspace-error" role="alert">
          {error}
        </p>
      )}
      {message && (
        <p className="workspace-success" role="status">
          {message}
        </p>
      )}
    </>
  );
}
export function Pager({ page, data, onChange, disabled }) {
  const pages = data?.totalPages || 1;
  return (
    <div className="workspace-pager">
      <button
        className="btn btn--outline"
        disabled={disabled || page <= 1}
        onClick={() => onChange(page - 1)}
      >
        Trang trước
      </button>
      <span>
        Trang {page} / {pages} · {data?.totalCount ?? itemsOf(data).length} kết
        quả
      </span>
      <button
        className="btn btn--outline"
        disabled={disabled || page >= pages}
        onClick={() => onChange(page + 1)}
      >
        Trang sau
      </button>
    </div>
  );
}
