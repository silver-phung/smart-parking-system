export const dashboardPaths = {
  ADMIN: "/admin",
  MANAGER: "/manager",
  STAFF: "/staff",
  USER: "/customer",
};
export const dashboardPath = (role) => dashboardPaths[role] || "/";
export const roleNavigation = {
  ADMIN: [
    ["/admin", "Tổng quan"],
    ["/admin/parking-settings", "Quản lý bãi xe"],
    ["/admin/people", "Tài khoản"],
    ["/admin/people?view=assignments", "Phân công"],
    ["/parking", "3D"],
    ["/admin/parking-settings?view=tickets", "Xử lý vé"],
    ["/admin/policies", "Chính sách"],
  ],
  MANAGER: [
    ["/manager", "Tổng quan"],
    ["/parking", "3D"],
    ["/parking/slots", "Quản lý ô đỗ"],
    ["/manager?view=sessions", "Xe đang gửi"],
    ["/manager/assignments", "Phân công"],
    ["/handovers", "Bàn giao ca"],
  ],
  STAFF: [
    ["/parking", "3D"],
    ["/staff?view=entry", "Xe vào vãng lai"],
    ["/staff?view=bookings", "Xe đặt qua web"],
    ["/staff?view=exit", "Xe ra"],
    ["/handovers", "Bàn giao ca"],
  ],
  USER: [
    ["/parking", "3D"],
    ["/customer", "Bãi đỗ"],
    ["/customer/sessions", "Phiên gửi xe"],
    ["/customer/profile", "Hồ sơ & xe"],
  ],
};
export function staffLocation(view, step) {
  const legacy = {
    verify: { view: "entry", step: "verify" },
    dispatches: { view: "entry", step: "dispatches" },
    sessions: { view: "exit", step: "sessions" },
  };
  if (legacy[view]) return legacy[view];
  if (view === "bookings") return { view, step: "" };
  if (view === "exit")
    return { view, step: step === "sessions" ? "sessions" : "gate" };
  return {
    view: "entry",
    step: ["verify", "dispatches"].includes(step) ? step : "gate",
  };
}

export function navIsActive(target, pathname, search) {
  const [path, query = ""] = target.split("?");
  if (path !== pathname) return false;
  const current = new URLSearchParams(search).get("view");
  const expected = new URLSearchParams(query).get("view");
  if (path === "/staff") return (current || "entry") === expected;
  if (path === "/manager")
    return (current || "overview") === (expected || "overview");
  if (path === "/admin/people")
    return (current || "users") === (expected || "users");
  if (path === "/admin/parking-settings")
    return (current || "floors") === (expected || "floors");
  return true;
}
