const STORAGE_KEY = "smart-parking.auth.v1";
const roles = ["ADMIN", "MANAGER", "STAFF", "USER"];
export function isValidAuthSession(session) {
  return Boolean(
    session &&
      typeof session.accessToken === "string" &&
      session.accessToken &&
      typeof session.refreshToken === "string" &&
      session.refreshToken &&
      roles.includes(session.userProfile?.role),
  );
}
function restoreSession() {
  try {
    const stored = globalThis.sessionStorage?.getItem(STORAGE_KEY);
    if (!stored) return null;
    const session = JSON.parse(stored);
    if (isValidAuthSession(session)) return session;
  } catch {
    /* Storage may be disabled or contain an invalid record. */
  }
  try {
    globalThis.sessionStorage?.removeItem(STORAGE_KEY);
  } catch {
    /* Memory fallback. */
  }
  return null;
}
function persistSession(session) {
  try {
    if (isValidAuthSession(session)) {
      globalThis.sessionStorage?.setItem(
        STORAGE_KEY,
        JSON.stringify({
          accessToken: session.accessToken,
          refreshToken: session.refreshToken,
          userProfile: session.userProfile,
        }),
      );
    } else globalThis.sessionStorage?.removeItem(STORAGE_KEY);
  } catch {
    /* In-memory login remains usable when storage is blocked. */
  }
}
let currentSession = restoreSession();
// Changes only when the account signs in/out, so late refreshes cannot revive it.
let generation = 0;
const listeners = new Set();

export function getAuthSession() {
  return currentSession;
}
export function getAuthGeneration() {
  return generation;
}

export function subscribeAuthSession(listener) {
  listeners.add(listener);
  return () => listeners.delete(listener);
}

export function setAuthSession(session) {
  generation++;
  currentSession = session;
  persistSession(session);
  listeners.forEach((listener) => listener());
}

export function replaceRefreshedSession(session, expectedGeneration) {
  if (generation !== expectedGeneration || !currentSession) return false;
  currentSession = session;
  persistSession(session);
  listeners.forEach((listener) => listener());
  return true;
}

export function updateAuthProfile(userProfile) {
  if (!currentSession) return;
  currentSession = { ...currentSession, userProfile };
  persistSession(currentSession);
  listeners.forEach((listener) => listener());
}

export function clearAuthSession(expectedGeneration) {
  if (
    typeof expectedGeneration === "number" &&
    expectedGeneration !== generation
  )
    return;
  generation++;
  currentSession = null;
  persistSession(null);
  listeners.forEach((listener) => listener());
}
