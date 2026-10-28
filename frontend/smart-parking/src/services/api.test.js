import assert from "node:assert/strict";
import { afterEach, test } from "node:test";
import { apiRequest } from "./api.js";
import { clearAuthSession, setAuthSession } from "./authSession.js";

const originalFetch = globalThis.fetch;
afterEach(() => {
  globalThis.fetch = originalFetch;
  clearAuthSession();
});

test("shows ASP.NET field validation errors to the user", async () => {
  globalThis.fetch = async () => Response.json({
    title: "One or more validation errors occurred.",
    errors: {
      Username: ["Tên đăng nhập không được để trống"],
      Password: ["Mật khẩu không được để trống"],
    },
  }, { status: 400 });

  await assert.rejects(apiRequest("/v1/auth/login"), (error) => {
    assert.equal(error.status, 400);
    assert.match(error.message, /Tên đăng nhập không được để trống/);
    assert.match(error.message, /Mật khẩu không được để trống/);
    return true;
  });
});

test("does not attach an existing account token to public auth requests", async () => {
  setAuthSession({ accessToken: "existing-session" });
  globalThis.fetch = async (url, options) => {
    assert.equal(url, "/api/v1/auth/login");
    assert.equal(options.headers.Authorization, undefined);
    assert.equal(options.headers["Content-Type"], "application/json");
    assert.equal(options.skipAuth, undefined);
    return Response.json({ data: { username: "existing_user" } }, { status: 200 });
  };

  const response = await apiRequest("/v1/auth/login", {
    method: "POST", body: "{}", skipAuth: true,
  });
  assert.equal(response.data.username, "existing_user");
});

test("keeps the server's account-state error for an inactive login", async () => {
  globalThis.fetch = async () => Response.json({
    message: "Tài khoản chưa được kích hoạt. Vui lòng liên hệ quản trị viên.",
  }, { status: 403 });

  await assert.rejects(apiRequest("/v1/auth/login"), {
    message: "Tài khoản chưa được kích hoạt. Vui lòng liên hệ quản trị viên.", status: 403,
  });
});

test("reports a server failure even when the error response is not JSON", async () => {
  globalThis.fetch = async () => new Response("Internal Server Error", { status: 500 });
  await assert.rejects(apiRequest("/v1/auth/login"), {
    message: "Máy chủ đang gặp sự cố. Vui lòng thử lại sau.", status: 500,
  });
});

test("reports an unreachable backend", async () => {
  globalThis.fetch = async () => { throw new TypeError("Failed to fetch"); };
  await assert.rejects(apiRequest("/v1/auth/login"), {
    message: "Không thể kết nối máy chủ. Hãy kiểm tra backend đang chạy.",
  });
});

test("preserves authenticated API calls and empty successful responses", async () => {
  setAuthSession({ accessToken: "current-session" });
  globalThis.fetch = async (url, options) => {
    assert.equal(url, "/api/v1/users/me");
    assert.equal(options.headers.Authorization, "Bearer current-session");
    return new Response(null, { status: 204 });
  };
  assert.equal(await apiRequest("/v1/users/me"), null);
});
