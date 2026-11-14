import { getAuthSession } from "./authSession.js";
import { apiRequest } from "./api.js";

const baseUrl = import.meta.env.VITE_OPERATIONS_API_BASE_URL ||
  (import.meta.env.DEV ? "http://localhost:5174" : "");

export async function operationsRequest(path, options = {}) {
  if (!baseUrl)
    throw new Error("Operations API chưa được cấu hình cho môi trường này.");
  const token = getAuthSession()?.accessToken;
  const response = await fetch(`${baseUrl}/api/v1/operations${path}`, {
    ...options,
    headers: {
      ...(options.body ? { "Content-Type": "application/json" } : {}),
      ...options.headers,
      ...(token ? { Authorization: `Bearer ${token}` } : {}),
    },
  });
  const body = await response.json().catch(() => ({}));
  if (!response.ok)
    throw Object.assign(new Error(body.message || "Không thể thực hiện thao tác handover."), {
      status: response.status,
    });
  return body;
}

export function operationsApiBaseConfigured() {
  return Boolean(baseUrl);
}

// Preserve the Core-backed Staff Operations adapter already used for tickets.
export const operationsApi = async (lotId, path, body, method, signal) => {
  const response = await apiRequest(`/v1/parking-lots/${lotId}/operations/${path}`, {
    method: method || (body ? "POST" : "GET"),
    ...(body ? { body: JSON.stringify(body) } : {}),
    signal,
  });
  return response.data;
};
