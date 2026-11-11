import { apiRequest } from "./api.js";

export const operationsApi = async (lotId, path, body, method, signal) => {
  const response = await apiRequest(`/v1/parking-lots/${lotId}/operations/${path}`, {
    method: method || (body ? "POST" : "GET"),
    ...(body ? { body: JSON.stringify(body) } : {}), signal,
  });
  return response.data;
};
