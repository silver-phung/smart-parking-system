import { getAuthSession } from "./authSession.js";

export async function apiRequest(path, options = {}) {
  let response;
  try {
    const accessToken = options.skipAuth
      ? null
      : getAuthSession()?.accessToken;
    const { skipAuth, ...requestOptions } = options;
    const isMultipart = typeof FormData !== "undefined" && requestOptions.body instanceof FormData;
    response = await fetch(`/api${path}`, {
      ...requestOptions,
      credentials: "same-origin",
      headers: {
        ...(requestOptions.body && !isMultipart ? { "Content-Type": "application/json" } : {}),
        ...(accessToken ? { Authorization: `Bearer ${accessToken}` } : {}),
        ...requestOptions.headers,
      },
    });
  } catch (error) {
    if (error.name === "AbortError") throw error;
    throw new Error("Không thể kết nối máy chủ. Hãy kiểm tra backend đang chạy.");
  }

  if (response.status === 204) {
    return null;
  }

  const data = await response.json().catch(() => ({}));
  if (!response.ok) {
    const validationMessages = data?.errors
      ? Object.values(data.errors).flat().filter((message) => typeof message === "string")
      : [];
    const message = data?.message || data?.error || validationMessages.join(" ")
      || (response.status === 404
        ? "API này chưa được triển khai trong backend."
        : response.status >= 500
        ? "Máy chủ đang gặp sự cố. Vui lòng thử lại sau."
        : "Yêu cầu không thành công. Vui lòng thử lại.");
    const error = new Error(message);
    error.status = response.status;
    error.code = data?.code;
    throw error;
  }

  return data;
}
