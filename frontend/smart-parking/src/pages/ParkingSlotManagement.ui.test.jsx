import { beforeEach, afterEach, it, expect, vi } from "vitest";
import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import CustomerDashboard from "./CustomerDashboard.jsx";
import { setAuthSession, clearAuthSession } from "../services/authSession.js";

let fetcher;
beforeEach(() => {
  fetcher = vi.spyOn(globalThis, "fetch").mockImplementation(async (url, options = {}) => {
    if (options.method && options.method !== "GET") return Response.json({ message: "Đã cập nhật.", data: { id: 3 } });
    let data;
    if (url.includes("/staff-assignments/my-lots")) data = [{ lotId: 1, lotName: "Bãi quản lý" }];
    else if (url.includes("/parking-lots?")) data = { items: [{ id: 1, name: "Bãi A" }, { id: 2, name: "Bãi B" }], totalPages: 1 };
    else if (url.includes("/floors?")) data = [{ id: 10, name: "Tầng A" }];
    else if (url.includes("/slots?")) data = { items: [
      { id: 1, slotCode: "A1", physicalStatus: "EMPTY", isAvailable: true, isProtected: false, isActive: true },
      { id: 2, slotCode: "A2", physicalStatus: "OCCUPIED", isAvailable: false, isProtected: true, isActive: true },
      { id: 3, slotCode: "A3", physicalStatus: "EMPTY", isAvailable: false, isProtected: true, isActive: true },
    ], totalPages: 1 };
    else throw new Error(`Unexpected ${url}`);
    return Response.json({ data });
  });
});
afterEach(clearAuthSession);
function mount(role = "ADMIN") {
  setAuthSession({ accessToken: "test", userProfile: { id: 1, role } });
  render(<MemoryRouter><CustomerDashboard publicView /></MemoryRouter>);
}

it("shows management for Admin and locks occupied/reserved slots", async () => {
  mount(); await screen.findByRole("button", { name: "Sửa ô A1" });
  expect(screen.getByRole("heading", { name: "Quản lý ô đỗ" })).toBeTruthy();
  expect(screen.queryByText("Đăng nhập để xem phiên của tôi")).toBeNull();
  expect(screen.getByRole("button", { name: "Sửa ô A2" }).disabled).toBe(true);
  expect(screen.getByRole("button", { name: "Xóa ô A3" }).disabled).toBe(true);
  expect(screen.getByLabelText("Trạng thái ô A2").disabled).toBe(true);
  expect(screen.getByLabelText("Trạng thái ô A3").disabled).toBe(true);
  expect(Array.from(screen.getByLabelText("Trạng thái ô A1").options).map(o => o.text)).toEqual(["Trống", "Có xe", "Đang giữ chỗ", "Bảo trì"]);
});

it("creates, edits, confirms status changes and deletes a free slot", async () => {
  mount(); await screen.findByRole("button", { name: "Sửa ô A1" });
  fireEvent.change(screen.getByLabelText("Mã ô"), { target: { value: "A4" } });
  fireEvent.click(screen.getByRole("button", { name: "Thêm ô đỗ", exact: true }));
  await screen.findByText("Đã cập nhật.");
  expect(fetcher.mock.calls.some(([url, o]) => url.endsWith("/parking-floors/10/slots") && o.method === "POST")).toBe(true);
  await waitFor(() => expect(screen.getByRole("button", { name: "Sửa ô A1" }).disabled).toBe(false));
  fireEvent.click(screen.getByRole("button", { name: "Sửa ô A1" }));
  fireEvent.change(screen.getByLabelText("Mã ô"), { target: { value: "A1-NEW" } });
  fireEvent.click(screen.getByRole("button", { name: "Lưu ô đỗ" }));
  await waitFor(() => expect(fetcher.mock.calls.some(([url, o]) => url.endsWith("/parking-slots/1") && o.method === "PUT")).toBe(true));
  await waitFor(() => expect(screen.getByLabelText("Trạng thái ô A1").disabled).toBe(false));
  fireEvent.change(screen.getByLabelText("Trạng thái ô A1"), { target: { value: "RESERVED" } });
  fireEvent.change(screen.getByLabelText("Lý do đổi trạng thái"), { target: { value: "Giữ chỗ thủ công" } });
  fireEvent.click(screen.getByRole("button", { name: "Xác nhận", exact: true }));
  await waitFor(() => expect(fetcher.mock.calls.some(([url, o]) => url.endsWith("/status") && o.method === "PATCH" && JSON.parse(o.body).physicalStatus === "RESERVED")).toBe(true));
  await waitFor(() => expect(screen.getByRole("button", { name: "Xóa ô A1" }).disabled).toBe(false));
  fireEvent.click(screen.getByRole("button", { name: "Xóa ô A1" }));
  fireEvent.click(screen.getByRole("button", { name: "Xác nhận", exact: true }));
  await waitFor(() => expect(fetcher.mock.calls.some(([url, o]) => url.endsWith("/parking-slots/1") && o.method === "DELETE")).toBe(true));
});

it("limits Manager lot choices to assigned lots", async () => {
  mount("MANAGER");
  await screen.findByRole("option", { name: "Bãi quản lý" });
  expect(screen.queryByRole("option", { name: "Bãi B" })).toBeNull();
  expect(fetcher.mock.calls.some(([url]) => url.includes("/parking-lots?"))).toBe(false);
});
