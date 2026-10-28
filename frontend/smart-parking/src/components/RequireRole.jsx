import { useSyncExternalStore } from "react";
import { Navigate } from "react-router-dom";
import {
  getAuthSession,
  subscribeAuthSession,
} from "../services/authSession.js";

export default function RequireRole({ role, children }) {
  const session = useSyncExternalStore(
    subscribeAuthSession,
    getAuthSession,
    getAuthSession
  );

  if (!session) return <Navigate to="/login" replace />;
  if (session.userProfile.role !== role) return <Navigate to="/" replace />;

  return children;
}
