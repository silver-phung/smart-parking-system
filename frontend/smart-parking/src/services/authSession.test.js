import assert from "node:assert/strict";
import { afterEach, beforeEach, test } from "node:test";

let records;
let reload = 0;
const load = () => import(`./authSession.js?reload=${++reload}`);
const session = {
  accessToken: "access",
  refreshToken: "refresh",
  userProfile: { id: 1, role: "ADMIN", fullName: "Admin" },
};
beforeEach(() => {
  records = new Map();
  globalThis.sessionStorage = {
    getItem: (key) => records.get(key) ?? null,
    setItem: (key, value) => records.set(key, value),
    removeItem: (key) => records.delete(key),
  };
});
afterEach(() => {
  delete globalThis.sessionStorage;
});
test("restores a signed-in account after a full module reload (F5)", async () => {
  const first = await load();
  first.setAuthSession(session);
  const afterReload = await load();
  assert.deepEqual(afterReload.getAuthSession(), session);
});
test("does not restore an explicitly logged-out account after reload", async () => {
  const first = await load();
  first.setAuthSession(session);
  first.clearAuthSession();
  assert.equal((await load()).getAuthSession(), null);
  assert.equal(records.size, 0);
});
test("persists profile edits alongside the existing tokens", async () => {
  const first = await load();
  first.setAuthSession(session);
  first.updateAuthProfile({ ...session.userProfile, fullName: "Changed" });
  const restored = (await load()).getAuthSession();
  assert.equal(restored.userProfile.fullName, "Changed");
  assert.equal(restored.refreshToken, "refresh");
});
test("ignores malformed or incomplete saved sessions", async () => {
  const first = await load();
  first.setAuthSession(session);
  const key = [...records.keys()][0];
  records.set(key, "{broken JSON");
  assert.equal((await load()).getAuthSession(), null);
  records.set(key, JSON.stringify({ accessToken: "token" }));
  assert.equal((await load()).getAuthSession(), null);
  records.set(
    key,
    JSON.stringify({ ...session, userProfile: { role: "UNKNOWN" } }),
  );
  assert.equal((await load()).getAuthSession(), null);
});
test("keeps login and logout usable when browser storage is unavailable", async () => {
  globalThis.sessionStorage = {
    getItem() {
      throw new Error("blocked");
    },
    setItem() {
      throw new Error("blocked");
    },
    removeItem() {
      throw new Error("blocked");
    },
  };
  const first = await load();
  first.setAuthSession(session);
  assert.equal(first.getAuthSession().accessToken, "access");
  first.clearAuthSession();
  assert.equal(first.getAuthSession(), null);
});
