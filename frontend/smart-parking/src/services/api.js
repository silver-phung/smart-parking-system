import {
  clearAuthSession,
  getAuthGeneration,
  getAuthSession,
  isValidAuthSession,
  replaceRefreshedSession,
} from "./authSession.js";

let refreshFlight = null;
const expired = () =>
  Object.assign(
    new Error("Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại."),
    { status: 401 },
  );
async function send(path, options, accessToken) {
  const { skipAuth, ...requestOptions } = options;
  const multipart =
    typeof FormData !== "undefined" && requestOptions.body instanceof FormData;
  try {
    return await fetch(`/api${path}`, {
      ...requestOptions,
      credentials: "same-origin",
      headers: {
        ...(requestOptions.body && !multipart
          ? { "Content-Type": "application/json" }
          : {}),
        ...requestOptions.headers,
        ...(accessToken ? { Authorization: `Bearer ${accessToken}` } : {}),
      },
    });
  } catch (error) {
    if (error.name === "AbortError") throw error;
    throw new Error(
      "Không thể kết nối máy chủ. Hãy kiểm tra backend đang chạy.",
    );
  }
}
async function read(response) {
  if (response.status === 204) return null;
  const data = await response.json().catch(() => ({}));
  if (!response.ok) {
    const validation = data?.errors
      ? Object.values(data.errors)
          .flat()
          .filter((message) => typeof message === "string")
      : [];
    const message =
      data?.message ||
      data?.error ||
      validation.join(" ") ||
      (response.status === 404
        ? "API này chưa được triển khai trong backend."
        : response.status >= 500
          ? "Máy chủ đang gặp sự cố. Vui lòng thử lại sau."
          : "Yêu cầu không thành công. Vui lòng thử lại.");
    throw Object.assign(new Error(message), {
      status: response.status,
      code: data?.code,
    });
  }
  return data;
}
async function refreshSession(session, generation) {
  if (refreshFlight?.generation === generation) return refreshFlight.promise;
  const flight = { generation };
  flight.promise = (async () => {
    const response = await send(
      "/v1/auth/refresh",
      {
        method: "POST",
        body: JSON.stringify({ refreshToken: session.refreshToken }),
      },
      null,
    );
    if ([400, 401, 403].includes(response.status)) {
      clearAuthSession(generation);
      throw expired();
    }
    const result = await read(response);
    if (!isValidAuthSession(result?.data))
      throw Object.assign(
        new Error("Phản hồi làm mới phiên không hợp lệ. Vui lòng thử lại."),
        { status: 502 },
      );
    if (!replaceRefreshedSession(result.data, generation)) throw expired();
    return result.data;
  })();
  refreshFlight = flight;
  try {
    return await flight.promise;
  } finally {
    if (refreshFlight === flight) refreshFlight = null;
  }
}
export async function apiRequest(path, options = {}) {
  const session = options.skipAuth ? null : getAuthSession();
  const generation = getAuthGeneration();
  let response = await send(path, options, session?.accessToken);
  if (response.status === 401 && session && !path.startsWith("/v1/auth/")) {
    options.signal?.throwIfAborted();
    if (generation !== getAuthGeneration()) throw expired();
    let latest = getAuthSession();
    if (latest?.accessToken === session.accessToken) {
      if (!session.refreshToken) {
        clearAuthSession(generation);
        throw expired();
      }
      latest = await refreshSession(session, generation);
    }
    options.signal?.throwIfAborted();
    if (generation !== getAuthGeneration()) throw expired();
    response = await send(path, options, latest?.accessToken);
    if (response.status === 401) {
      clearAuthSession(generation);
      throw expired();
    }
  }
  return read(response);
}
