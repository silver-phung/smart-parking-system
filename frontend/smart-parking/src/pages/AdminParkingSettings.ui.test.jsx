import { beforeEach, afterEach, expect, it, vi } from "vitest";
import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router-dom";
import AdminParkingSettings from "./AdminParkingSettings.jsx";
import { setAuthSession, clearAuthSession } from "../services/authSession.js";

let fetchMock;
let rejectPolicy;
beforeEach(() => {
  rejectPolicy = false;
  setAuthSession({ accessToken: "test-admin-token", userProfile: { id: 1, username: "admin", role: "ADMIN" } });
  fetchMock = vi.fn(async (input, options = {}) => {
    const path = new URL(input, "http://localhost").pathname;
    const result = data => Response.json({ data });
    if (path.endsWith("/parking-lots")) return result({ items: [{ id: 1, name: "Bãi A" }, { id: 2, name: "Bãi B" }], totalPages: 1 });
    if (path.endsWith("/zones")) return result([{ id: 1, code: "VL", name: "Vãng lai", type: "TRANSIENT", layout: "INSIDE", isActive: true, floorIds: [] }]);
    if (path.endsWith("/layout")) return result({ floors: [], slots: [] });
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
function mount() { render(<MemoryRouter future={{ v7_startTransition: true, v7_relativeSplatPath: true }}><AdminParkingSettings /></MemoryRouter>); }
it("saves explicitly blank policy parameters as an unapproved draft", async () => {
  const user = userEvent.setup(); mount();
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
  const field = await screen.findByLabelText("Hạn giữ chỗ (phút)");
  await waitFor(() => expect(field.closest("fieldset").disabled).toBe(false));
  await user.type(field, "7");
  await user.selectOptions(screen.getByLabelText("Bãi cần cấu hình"), "2");
  expect(screen.getByLabelText("Hạn giữ chỗ (phút)").value).toBe("");
});
