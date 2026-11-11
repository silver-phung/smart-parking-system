import { beforeEach, afterEach, expect, it, vi } from "vitest";
import { fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router-dom";
import AdminParkingSettings from "./AdminParkingSettings.jsx";
import { setAuthSession, clearAuthSession } from "../services/authSession.js";

let fetchMock;
let rejectPolicy;
let rejectZone;
beforeEach(() => {
  rejectPolicy = false;
  rejectZone = false;
  setAuthSession({ accessToken: "test-admin-token", userProfile: { id: 1, username: "admin", role: "ADMIN" } });
  fetchMock = vi.fn(async (input, options = {}) => {
    const path = new URL(input, "http://localhost").pathname;
    const result = data => Response.json({ data });
    if (path.endsWith("/parking-lots")) return result({ items: [{ id: 1, name: "Bãi A" }, { id: 2, name: "Bãi B" }], totalPages: 1 });
    if (path.endsWith("/zones")) return options.method === "POST" ? (rejectZone ? Response.json({ message: "Tên hoặc số tầng đã tồn tại trong bãi." }, { status: 409 }) : result({ id: 3 })) : result([
      { id: 1, code: "BT", name: "Khu A", type: "TRANSIENT", layout: "INSIDE", isActive: true, floorIds: [1] },
      { id: 2, code: "VT", name: "Khu B", type: "FIXED", layout: "EXTERNAL", isActive: true, floorIds: [2] },
    ]);
    if (path.endsWith("/zones/2") && options.method === "PUT") return result({ id: 2 });
    if (path.endsWith("/layout")) return result({ floors: [
      { id: 1, name: "Tầng 1", floorNumber: 1, zoneId: 1, isActive: true },
      { id: 2, name: "Tầng 2", floorNumber: 2, zoneId: 2, isActive: true },
      { id: 3, name: "Tầng 3", floorNumber: 3, zoneId: null, isActive: true },
    ], slots: [] });
    if (path.endsWith("/policies")) {
      if (options.method === "POST") return rejectPolicy
        ? Response.json({ message: "Chỉ duyệt khi có đủ nguồn quyết định." }, { status: 400 })
        : result({ id: 10 });
      return result([]);
    }
    throw new Error(`Unmocked ${path}`);
  });
  vi.stubGlobal("fetch", fetchMock);
});
afterEach(() => { clearAuthSession(); vi.unstubAllGlobals(); });
function mount(path = "/admin/parking-settings") { render(<MemoryRouter initialEntries={[path]} future={{ v7_startTransition: true, v7_relativeSplatPath: true }}><AdminParkingSettings /></MemoryRouter>); }
it("saves explicitly blank policy parameters as an unapproved draft", async () => {
  const user = userEvent.setup(); mount();
  await user.click(await screen.findByRole("tab", { name: "Chính sách" }));
  const date = await screen.findByLabelText("Bắt đầu có hiệu lực (giờ máy của bạn)");
  await waitFor(() => expect(date.closest("fieldset").disabled).toBe(false));
  expect(screen.getByLabelText("Hạn giữ chỗ (phút)").value).toBe("");
  fireEvent.change(date, { target: { value: "2026-11-03T09:00" } });
  await user.click(screen.getByRole("button", { name: "Lưu phiên bản nháp" }));
  await screen.findByText("Đã lưu vào máy chủ.");
  const write = fetchMock.mock.calls.find(([url, o]) => url.endsWith("/policies") && o.method === "POST");
  expect(JSON.parse(write[1].body)).toMatchObject({ approve: false, zoneId: null, holdMinutes: null,
    feeUnitMinutes: null, depositAmount: null, feeRounding: null, decisionReference: "" });
});
it("keeps the draft and displays the server rejection when approval is incomplete", async () => {
  rejectPolicy = true; const user = userEvent.setup(); mount();
  await user.click(await screen.findByRole("tab", { name: "Chính sách" }));
  const date = await screen.findByLabelText("Bắt đầu có hiệu lực (giờ máy của bạn)");
  await waitFor(() => expect(date.closest("fieldset").disabled).toBe(false));
  fireEvent.change(date, { target: { value: "2026-11-03T09:00" } });
  await user.type(screen.getByLabelText("Hạn giữ chỗ (phút)"), "7");
  await user.click(screen.getByRole("checkbox", { name: /Tôi xác nhận quyết định/ }));
  await user.click(screen.getByRole("button", { name: "Tạo phiên bản được duyệt" }));
  await screen.findByText("Chỉ duyệt khi có đủ nguồn quyết định.");
  expect(screen.getByLabelText("Hạn giữ chỗ (phút)").value).toBe("7");
  expect(screen.queryByText("Đã lưu vào máy chủ.")).toBeNull();
});
it("resets policy drafts when another lot is selected", async () => {
  const user = userEvent.setup(); mount();
  await user.click(await screen.findByRole("tab", { name: "Chính sách" }));
  const field = await screen.findByLabelText("Hạn giữ chỗ (phút)");
  await waitFor(() => expect(field.closest("fieldset").disabled).toBe(false));
  await user.type(field, "7");
  await user.selectOptions(screen.getByLabelText("Bãi cần cấu hình"), "2");
  expect(screen.getByLabelText("Hạn giữ chỗ (phút)").value).toBe("");
});

it("shows only the selected settings function and retains drafts between tabs", async () => {
  const user = userEvent.setup(); mount();
  const name = await screen.findByLabelText("Tên khu");
  await waitFor(() => expect(name.closest("fieldset").disabled).toBe(false));
  expect(screen.getAllByRole("tab").map(tab => tab.textContent)).toEqual(["Khu bãi", "Chính sách", "Tầng", "Ô đỗ", "Xử lý vé"]);
  expect(screen.getAllByRole("tabpanel")).toHaveLength(1);
  expect(screen.queryByLabelText("Bố cục")).toBeNull();
  expect(within(screen.getByLabelText("Loại khu")).getAllByRole("option").map(option => option.textContent)).toEqual(["Bình thường", "Vé tháng"]);
  expect(screen.queryByLabelText("Hạn giữ chỗ (phút)")).toBeNull();
  expect(screen.queryByLabelText("Mã vé")).toBeNull();
  await user.type(name, "Khu vé tháng mới");
  for (const [tab, heading] of [["Chính sách", "Chính sách vận hành"], ["Tầng", "Quản lý tầng"], ["Ô đỗ", "Quản lý ô đỗ"], ["Xử lý vé", "Xử lý vé mất hoặc thu hồi"]]) {
    await user.click(screen.getByRole("tab", { name: tab, exact: true }));
    expect(screen.getByRole("heading", { name: heading, level: 2 })).toBeTruthy();
    expect(screen.queryByLabelText("Tên khu")).toBeNull();
  }
  await user.click(screen.getByRole("tab", { name: "Khu bãi" }));
  expect(screen.getByLabelText("Tên khu").value).toBe("Khu vé tháng mới");
});

it("saves monthly zones with the compatible API type and preserves hidden layout on edit", async () => {
  const user = userEvent.setup(); mount();
  const code = await screen.findByLabelText("Mã khu");
  await waitFor(() => expect(code.closest("fieldset").disabled).toBe(false));
  await user.type(code, "VT2");
  await user.type(screen.getByLabelText("Tên khu"), "Khu vé tháng");
  await user.selectOptions(screen.getByLabelText("Loại khu"), "FIXED");
  await user.click(screen.getByRole("checkbox", { name: "Tầng 3" }));
  await user.click(screen.getByRole("button", { name: "Lưu khu" }));
  await screen.findByText("Đã lưu vào máy chủ.");
  const create = fetchMock.mock.calls.find(([url, options]) => url.endsWith("/zones") && options.method === "POST");
  expect(JSON.parse(create[1].body)).toMatchObject({ code: "VT2", name: "Khu vé tháng", type: "FIXED", layout: "INSIDE", floorIds: [3], newFloor: null });

  await user.click(await screen.findByRole("button", { name: /VT · Khu B/ }));
  expect(screen.getByLabelText("Loại khu").value).toBe("FIXED");
  expect(screen.queryByText("EXTERNAL")).toBeNull();
  await user.clear(screen.getByLabelText("Tên khu"));
  await user.type(screen.getByLabelText("Tên khu"), "Khu vé tháng B");
  await user.click(screen.getByRole("button", { name: "Lưu khu" }));
  await waitFor(() => expect(fetchMock.mock.calls.some(([url, options]) => url.endsWith("/zones/2") && options.method === "PUT")).toBe(true));
  const update = fetchMock.mock.calls.find(([url, options]) => url.endsWith("/zones/2") && options.method === "PUT");
  expect(JSON.parse(update[1].body)).toMatchObject({ name: "Khu vé tháng B", type: "FIXED", layout: "EXTERNAL", floorIds: [2] });
});

it("requires a floor and submits a new floor with the zone in one request", async () => {
  const user = userEvent.setup(); mount();
  const code = await screen.findByLabelText("Mã khu");
  await waitFor(() => expect(code.closest("fieldset").disabled).toBe(false));
  expect(screen.getByRole("button", { name: "Lưu khu" }).disabled).toBe(true);
  expect(screen.getByRole("checkbox", { name: /Tầng 1 · Đã thuộc khu BT/ }).disabled).toBe(true);
  expect(screen.getByRole("button", { name: /BT · Khu A/ }).textContent).toContain("Tầng: Tầng 1");
  await user.type(code, "VT3");
  await user.type(screen.getByLabelText("Tên khu"), "Khu vé tháng mới");
  await user.click(screen.getByRole("checkbox", { name: "Tạo tầng mới cho khu" }));
  await user.type(screen.getByLabelText("Tên tầng mới"), "Tầng 4");
  await user.type(screen.getByLabelText("Số tầng mới"), "4");
  await user.click(screen.getByRole("button", { name: "Lưu khu" }));
  await screen.findByText("Đã lưu vào máy chủ.");
  const writes = fetchMock.mock.calls.filter(([, options]) => options.method === "POST");
  expect(writes).toHaveLength(1);
  expect(JSON.parse(writes[0][1].body)).toMatchObject({ code: "VT3", floorIds: [], newFloor: { name: "Tầng 4", floorNumber: 4 } });
});

it("keeps both zone and floor drafts when the server rejects the floor", async () => {
  rejectZone = true;
  const user = userEvent.setup(); mount();
  const code = await screen.findByLabelText("Mã khu");
  await waitFor(() => expect(code.closest("fieldset").disabled).toBe(false));
  await user.type(code, "VT3");
  await user.type(screen.getByLabelText("Tên khu"), "Khu mới");
  await user.click(screen.getByRole("checkbox", { name: "Tạo tầng mới cho khu" }));
  await user.type(screen.getByLabelText("Tên tầng mới"), "Tầng 1");
  await user.type(screen.getByLabelText("Số tầng mới"), "1");
  await user.click(screen.getByRole("button", { name: "Lưu khu" }));
  await screen.findByText("Tên hoặc số tầng đã tồn tại trong bãi.");
  expect(screen.getByLabelText("Tên khu").value).toBe("Khu mới");
  expect(screen.getByLabelText("Tên tầng mới").value).toBe("Tầng 1");
  expect(screen.queryByText("Đã lưu vào máy chủ.")).toBeNull();
});

it("opens linked tabs and supports keyboard navigation", async () => {
  const user = userEvent.setup(); mount("/admin/parking-settings?view=slots");
  const slotsTab = await screen.findByRole("tab", { name: "Ô đỗ" });
  expect(slotsTab.getAttribute("aria-selected")).toBe("true");
  expect(screen.getByRole("tabpanel").getAttribute("aria-labelledby")).toBe(slotsTab.id);
  slotsTab.focus();
  await user.keyboard("{ArrowRight}");
  expect(screen.getByRole("tab", { name: "Xử lý vé" })).toBe(document.activeElement);
  await user.keyboard("{Home}");
  expect(screen.getByRole("tab", { name: "Khu bãi" })).toBe(document.activeElement);
  await user.keyboard("{End}");
  expect(screen.getByRole("tab", { name: "Xử lý vé" }).getAttribute("aria-selected")).toBe("true");
});
