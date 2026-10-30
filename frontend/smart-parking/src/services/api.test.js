import assert from "node:assert/strict";
import { afterEach, test } from "node:test";
import { apiRequest } from "./api.js";
import {
  clearAuthSession,
  getAuthSession,
  setAuthSession,
} from "./authSession.js";

const originalFetch = globalThis.fetch;
afterEach(() => {
  globalThis.fetch = originalFetch;
  clearAuthSession();
});

test("shows ASP.NET field validation errors to the user", async () => {
  globalThis.fetch = async () =>
    Response.json(
      {
        title: "One or more validation errors occurred.",
        errors: {
          Username: ["Tên đăng nhập không được để trống"],
          Password: ["Mật khẩu không được để trống"],
        },
      },
      { status: 400 },
    );

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
    return Response.json(
      { data: { username: "existing_user" } },
      { status: 200 },
    );
  };

  const response = await apiRequest("/v1/auth/login", {
    method: "POST",
    body: "{}",
    skipAuth: true,
  });
  assert.equal(response.data.username, "existing_user");
});

test("keeps the server's account-state error for an inactive login", async () => {
  globalThis.fetch = async () =>
    Response.json(
      {
        message:
          "Tài khoản chưa được kích hoạt. Vui lòng liên hệ quản trị viên.",
      },
      { status: 403 },
    );

  await assert.rejects(apiRequest("/v1/auth/login"), {
    message: "Tài khoản chưa được kích hoạt. Vui lòng liên hệ quản trị viên.",
    status: 403,
  });
});

test("reports a server failure even when the error response is not JSON", async () => {
  globalThis.fetch = async () =>
    new Response("Internal Server Error", { status: 500 });
  await assert.rejects(apiRequest("/v1/auth/login"), {
    message: "Máy chủ đang gặp sự cố. Vui lòng thử lại sau.",
    status: 500,
  });
});

test("reports an unreachable backend", async () => {
  globalThis.fetch = async () => {
    throw new TypeError("Failed to fetch");
  };
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

const signedIn = {
  accessToken: "old-access",
  refreshToken: "refresh",
  userProfile: { id: 1, role: "ADMIN" },
};
const refreshed = { ...signedIn, accessToken: "new-access" };
test("refreshes an expired access token and retries the original request once", async () => {
  setAuthSession(signedIn);
  const calls = [];
  globalThis.fetch = async (url, options) => {
    calls.push(url);
    if (url.endsWith("/auth/refresh")) {
      assert.equal(options.headers.Authorization, undefined);
      assert.deepEqual(JSON.parse(options.body), { refreshToken: "refresh" });
      return Response.json({ data: refreshed });
    }
    return options.headers.Authorization === "Bearer old-access"
      ? new Response(null, { status: 401 })
      : Response.json({ data: "ok" });
  };
  assert.equal((await apiRequest("/v1/users/me")).data, "ok");
  assert.deepEqual(calls, [
    "/api/v1/users/me",
    "/api/v1/auth/refresh",
    "/api/v1/users/me",
  ]);
  assert.equal(getAuthSession().accessToken, "new-access");
});
test("shares a single token refresh between concurrent failing requests", async () => {
  setAuthSession(signedIn);
  let refreshes = 0;
  globalThis.fetch = async (url, options) => {
    if (url.endsWith("/auth/refresh")) {
      refreshes++;
      await new Promise((resolve) => setImmediate(resolve));
      return Response.json({ data: refreshed });
    }
    return options.headers.Authorization === "Bearer old-access"
      ? new Response(null, { status: 401 })
      : Response.json({ data: "ok" });
  };
  const results = await Promise.all([
    apiRequest("/v1/users/me"),
    apiRequest("/v1/admin/users"),
  ]);
  assert.equal(refreshes, 1);
  assert.equal(results.length, 2);
});
test("clears the session when refresh is rejected by the server", async () => {
  setAuthSession(signedIn);
  let calls = 0;
  globalThis.fetch = async () => {
    calls++;
    return new Response(null, { status: 401 });
  };
  await assert.rejects(apiRequest("/v1/users/me"), { status: 401 });
  assert.equal(calls, 2);
  assert.equal(getAuthSession(), null);
});
test("retains the session after a temporary refresh server failure", async () => {
  setAuthSession(signedIn);
  globalThis.fetch = async (url) =>
    new Response(null, { status: url.endsWith("/auth/refresh") ? 503 : 401 });
  await assert.rejects(apiRequest("/v1/users/me"), { status: 503 });
  assert.equal(getAuthSession().refreshToken, "refresh");
});
test("does not revive a session if logout completes during refresh", async () => {
  setAuthSession(signedIn);
  let release, started;
  const ready = new Promise((resolve) => {
    started = resolve;
  });
  globalThis.fetch = async (url) => {
    if (url.endsWith("/auth/refresh")) {
      started();
      return new Promise((resolve) => {
        release = () => resolve(Response.json({ data: refreshed }));
      });
    }
    return new Response(null, { status: 401 });
  };
  const request = apiRequest("/v1/users/me");
  const rejected = assert.rejects(request, { status: 401 });
  await ready;
  clearAuthSession();
  release();
  await rejected;
  assert.equal(getAuthSession(), null);
});
test("does not overwrite a newly signed-in account with a late refresh", async () => {
  setAuthSession(signedIn);
  let release, started;
  const ready = new Promise((resolve) => {
    started = resolve;
  });
  globalThis.fetch = async (url) => {
    if (url.endsWith("/auth/refresh")) {
      started();
      return new Promise((resolve) => {
        release = () => resolve(Response.json({ data: refreshed }));
      });
    }
    return new Response(null, { status: 401 });
  };
  const request = apiRequest("/v1/users/me");
  const rejected = assert.rejects(request, { status: 401 });
  await ready;
  setAuthSession({
    ...signedIn,
    accessToken: "other-account",
    userProfile: { id: 2, role: "USER" },
  });
  release();
  await rejected;
  assert.equal(getAuthSession().userProfile.id, 2);
});
test("stops after one retry when the new access token is also rejected", async () => {
  setAuthSession(signedIn);
  let calls = 0;
  globalThis.fetch = async (url) => {
    calls++;
    return url.endsWith("/auth/refresh")
      ? Response.json({ data: refreshed })
      : new Response(null, { status: 401 });
  };
  await assert.rejects(apiRequest("/v1/users/me"), { status: 401 });
  assert.equal(calls, 3);
  assert.equal(getAuthSession(), null);
});
test("preserves multipart data without a JSON header when retrying", async () => {
  setAuthSession(signedIn);
  const form = new FormData();
  form.append("file", new Blob(["image"]), "photo.png");
  globalThis.fetch = async (url, options) => {
    if (url.endsWith("/auth/refresh"))
      return Response.json({ data: refreshed });
    assert.equal(options.body, form);
    assert.equal(options.headers["Content-Type"], undefined);
    return options.headers.Authorization === "Bearer old-access"
      ? new Response(null, { status: 401 })
      : Response.json({ data: "uploaded" });
  };
  assert.equal(
    (await apiRequest("/v1/images", { method: "POST", body: form })).data,
    "uploaded",
  );
});
