import { getAuthSession } from "./authSession.js";

export async function apiRequest(path, options = {}) {
  let response;
  try {
    const accessToken = options.skipAuth
      ? null
      : getAuthSession()?.accessToken;
    const { skipAuth, ...requestOptions } = options;
    response = await fetch(`/api${path}`, {
      ...requestOptions,
      credentials: "same-origin",
      headers: {
        ...(requestOptions.body ? { "Content-Type": "application/json" } : {}),
        ...(accessToken ? { Authorization: `Bearer ${accessToken}` } : {}),
        ...requestOptions.headers,
      },
    });
  } catch {
    throw new Error("Không thể kết nối máy chủ. Hãy kiểm tra backend đang chạy.");
  }

  if (response.status === 204) {
    return null;
  }

  const data = await response.json().catch(() => ({}));
  if (!response.ok) {
    const message = data.message || data.error
      || (response.status === 404
        ? "API này chưa được triển khai trong backend."
        : "Yêu cầu không thành công. Vui lòng thử lại.");
    throw new Error(message);
  }

  return data;
}
