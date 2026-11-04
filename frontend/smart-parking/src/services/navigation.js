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
    ["/admin/people", "Tài khoản"],
    ["/admin/people?view=assignments", "Phân công"],
    ["/parking", "Xem bãi đỗ"],
  ],
  MANAGER: [
    ["/manager", "Tổng quan"],
    ["/manager?view=slots", "Ô đỗ / bảo trì"],
    ["/manager?view=sessions", "Xe đang gửi"],
  ],
  STAFF: [
    ["/staff?view=entry", "Xe vào"],
    ["/staff?view=exit", "Xe ra"],
    ["/staff?view=verify", "Xác minh ô"],
    ["/staff?view=sessions", "Xe đang gửi"],
    ["/staff?view=dispatches", "Điều phối"],
  ],
  USER: [
    ["/customer", "Bãi đỗ"],
    ["/customer/sessions", "Phiên gửi xe"],
    ["/customer/profile", "Hồ sơ & xe"],
  ],
};
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
  return true;
}
