import { useSyncExternalStore } from "react";
import { Navigate } from "react-router-dom";
import { dashboardPath } from "../services/navigation.js";
import {
  getAuthSession,
  subscribeAuthSession,
} from "../services/authSession.js";

export default function RequireRole({ role, children }) {
  const session = useSyncExternalStore(
    subscribeAuthSession,
    getAuthSession,
    getAuthSession,
  );

  if (!session) return <Navigate to="/login" replace />;
  if (session.userProfile.role !== role)
    return <Navigate to={dashboardPath(session.userProfile.role)} replace />;

  return children;
}
