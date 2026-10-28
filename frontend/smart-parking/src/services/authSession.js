let currentSession = null;
const listeners = new Set();

export function getAuthSession() {
  return currentSession;
}

export function subscribeAuthSession(listener) {
  listeners.add(listener);
  return () => listeners.delete(listener);
}

export function setAuthSession(session) {
  currentSession = session;
  listeners.forEach((listener) => listener());
}

export function updateAuthProfile(userProfile) {
  if (!currentSession) return;
  currentSession = { ...currentSession, userProfile };
  listeners.forEach((listener) => listener());
}

export function clearAuthSession() {
  currentSession = null;
  listeners.forEach((listener) => listener());
}
