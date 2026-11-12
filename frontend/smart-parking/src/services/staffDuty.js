// Staff có hai loại phân công: trực cổng (GATE) và trực khu vực (SLOT_VERIFIER).
// GATE_IN / GATE_OUT / BOTH là giá trị cũ, vẫn được hiểu để không mất quyền của dữ liệu đã có.
export const dutyLabels = {
  GATE: "Trực cổng",
  GATE_IN: "Trực cổng vào",
  GATE_OUT: "Trực cổng ra",
  SLOT_VERIFIER: "Trực khu vực",
  BOTH: "Trực cổng và khu vực",
};

const canEnter = (duty) => ["GATE", "GATE_IN", "BOTH"].includes(duty);
const canExit = (duty) => ["GATE", "GATE_OUT", "BOTH"].includes(duty);
const canArea = (duty) => ["SLOT_VERIFIER", "BOTH"].includes(duty);

// Màn hình Staff nào thuộc loại phân công nào.
const viewRules = {
  entry: canEnter,
  exit: canExit,
  tickets: (duty) => canEnter(duty) || canExit(duty),
  bookings: canArea,
  verify: canArea,
  sessions: canArea,
  dispatches: canArea,
};

// Duty chưa biết (API cũ, dữ liệu thiếu) -> không ẩn gì, backend vẫn là nơi quyết định quyền.
export const canUseStaffView = (duties, viewId) => {
  const list = [].concat(duties || []).filter(Boolean);
  if (!list.length) return true;
  const rule = viewRules[viewId];
  return !rule || list.some(rule);
};

export const staffDutiesOf = (lots) => [...new Set((lots || []).map((l) => l?.duty).filter(Boolean))];

export const dutyKinds = (duties) => {
  const list = [].concat(duties || []).filter(Boolean);
  return { known: list.length > 0, gate: list.some((d) => canEnter(d) || canExit(d)), area: list.some(canArea) };
};

const staffNavItems = [
  ["entry", "/staff?view=entry", "Xe vào vãng lai"],
  ["exit", "/staff?view=exit", "Xe ra"],
  ["tickets", "/staff?view=tickets", "Tra vé"],
  ["bookings", "/staff?view=bookings", "Xe đặt qua web"],
  ["verify", "/staff?view=verify", "Xác minh ô"],
  ["sessions", "/staff?view=sessions", "Xe đang gửi"],
  ["dispatches", "/staff?view=dispatches", "Điều phối"],
];

// Menu của Staff chỉ chứa chức năng thuộc loại phân công của họ.
export const staffNavigationFor = (duties) =>
  staffNavItems.filter(([id]) => canUseStaffView(duties, id)).map(([, target, label]) => [target, label]);

