import { beforeEach, afterEach, it, expect, vi } from "vitest";
import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import { MemoryRouter, Routes, Route } from "react-router-dom";
import ManagerWorkspace from "./ManagerWorkspace.jsx";
import Header from "../components/Header.jsx";
import RequireRole from "../components/RequireRole.jsx";
import { navIsActive } from "../services/navigation.js";
import { setAuthSession, clearAuthSession } from "../services/authSession.js";
let fetcher, closed, deny, occupied;
const page = (items) => ({ items, totalPages: 1, totalCount: items.length });
beforeEach(() => {
  closed = false;
  deny = false;
  occupied = false;
  setAuthSession({
    accessToken: "test",
    userProfile: { role: "MANAGER", fullName: "Quản lý demo" },
  });
  fetcher = vi
    .spyOn(globalThis, "fetch")
    .mockImplementation(async (url, options = {}) => {
      if (options.method === "PATCH") {
        if (deny)
          return Response.json(
            { message: "Bạn không được phân công bãi này" },
            { status: 403 },
          );
        if (url.includes("parking-lots"))
          closed = !JSON.parse(options.body).isActive;
        return Response.json({ message: "Đã cập nhật" });
      }
      let data;
      if (url.includes("my-lots"))
        data = [
          { lotId: 1, lotName: "Bãi A", isActive: !closed },
          { lotId: 2, lotName: "Bãi B", isActive: true },
        ];
      else if (url.includes("/capacity"))
        data = {
          totalCapacity: 50,
          availableForWalkIn: 20,
          unassignedSessionsCount: 1,
        };
      else if (url.includes("/floors?")) data = [{ id: 3, name: "Tầng 1" }];
      else if (url.includes("/slots?"))
        data = page([
          {
            id: 5,
            slotCode: "A-01",
            physicalStatus: "EMPTY",
            isAvailable: true,
            isActive: true,
          },
          {
            id: 6,
            slotCode: "A-02",
            physicalStatus: "OCCUPIED",
            isAvailable: false,
            isActive: true,
          },
        ]);
      else if (url.includes("/parking-slots/5"))
        data = {
          id: 5,
          lotId: 1,
          physicalStatus: occupied ? "OCCUPIED" : "EMPTY",
        };
      else if (url.includes("/parking-sessions/active"))
        data = page([
          { id: 10, plateNumber: "51A-12345", status: "ACTIVE", lotId: 1 },
        ]);
      else if (url.includes("/parking-sessions/10"))
        data = {
          id: 10,
          plateNumber: "51A-12345",
          status: "ACTIVE",
          lotName: "Bãi A",
        };
      else if (url.includes("/parking-lots/"))
        data = { id: 1, name: "Bãi A", isActive: !closed };
      else throw new Error(url);
      return Response.json({ data });
    });
});
afterEach(clearAuthSession);
function mount(view = "overview") {
  return render(
    <MemoryRouter
      initialEntries={[`/manager?view=${view}`]}
      future={{ v7_startTransition: true, v7_relativeSplatPath: true }}
    >
      <ManagerWorkspace />
    </MemoryRouter>,
  );
}
const writes = () =>
  fetcher.mock.calls.filter(([, options]) => options.method === "PATCH");
it("shows the assigned lot count and can close then reopen the same lot", async () => {
  mount();
  await screen.findByText(/2 bãi được phân công/);
  fireEvent.click(
    await screen.findByRole("button", { name: "Đóng tiếp nhận" }),
  );
  expect(writes()).toHaveLength(0);
  fireEvent.change(screen.getByLabelText("Lý do"), {
    target: { value: "Bảo trì lối vào" },
  });
  fireEvent.click(screen.getByRole("button", { name: "Xác nhận thay đổi" }));
  await screen.findByRole("button", { name: "Mở tiếp nhận" });
  expect(JSON.parse(writes()[0][1].body)).toEqual({
    isActive: false,
    reason: "Bảo trì lối vào",
  });
  fireEvent.click(screen.getByRole("button", { name: "Mở tiếp nhận" }));
  fireEvent.change(screen.getByLabelText("Lý do"), {
    target: { value: "Hoàn tất bảo trì" },
  });
  fireEvent.click(screen.getByRole("button", { name: "Xác nhận thay đổi" }));
  await screen.findByRole("button", { name: "Đóng tiếp nhận" });
  expect(writes()).toHaveLength(2);
});
it("changes slot maintenance status with a reason and never offers occupied changes", async () => {
  mount("slots");
  fireEvent.change(await screen.findByLabelText("Trạng thái A-01"), {
    target: { value: "MAINTENANCE" },
  });
  expect(screen.getByLabelText("Trạng thái A-02").disabled).toBe(true);
  fireEvent.change(screen.getByLabelText("Lý do"), {
    target: { value: "Sửa cảm biến" },
  });
  fireEvent.click(screen.getByRole("button", { name: "Xác nhận thay đổi" }));
  await screen.findByText("Đã cập nhật");
  expect(writes()[0][0]).toBe("/api/v1/parking-slots/5/status");
  expect(JSON.parse(writes()[0][1].body)).toEqual({
    physicalStatus: "MAINTENANCE",
    reason: "Sửa cảm biến",
  });
});
it("rechecks the slot and blocks a newly occupied position", async () => {
  mount("slots");
  fireEvent.change(await screen.findByLabelText("Trạng thái A-01"), {
    target: { value: "MAINTENANCE" },
  });
  occupied = true;
  fireEvent.change(screen.getByLabelText("Lý do"), {
    target: { value: "Sửa cảm biến" },
  });
  fireEvent.click(screen.getByRole("button", { name: "Xác nhận thay đổi" }));
  await screen.findByRole("alert");
  expect(writes()).toHaveLength(0);
});
it("retains the reason when the server rejects the Manager scope", async () => {
  deny = true;
  mount();
  fireEvent.click(
    await screen.findByRole("button", { name: "Đóng tiếp nhận" }),
  );
  fireEvent.change(screen.getByLabelText("Lý do"), {
    target: { value: "Đóng ca" },
  });
  fireEvent.click(screen.getByRole("button", { name: "Xác nhận thay đổi" }));
  await screen.findByRole("alert");
  expect(screen.getByLabelText("Lý do").value).toBe("Đóng ca");
});
it("scopes sessions to the selected lot and resets details when switching lots", async () => {
  mount("sessions");
  fireEvent.click(await screen.findByRole("button", { name: "Xem phiên #10" }));
  await screen.findByRole("heading", { name: "Phiên #10" });
  fireEvent.change(screen.getByLabelText("Bãi đang quản lý"), {
    target: { value: "2" },
  });
  await waitFor(() =>
    expect(
      fetcher.mock.calls.some(([url]) => url.includes("active?lotId=2")),
    ).toBe(true),
  );
  expect(screen.queryByRole("heading", { name: "Phiên #10" })).toBeNull();
});
it("uses active navigation for query tabs and exposes only Manager links", () => {
  render(
    <MemoryRouter initialEntries={["/manager?view=slots"]}>
      <Header />
    </MemoryRouter>,
  );
  expect(
    screen
      .getByRole("link", { name: "Ô đỗ / bảo trì" })
      .getAttribute("aria-current"),
  ).toBe("page");
  expect(screen.queryByRole("link", { name: "Tài khoản" })).toBeNull();
  expect(
    navIsActive("/admin/people", "/admin/people", "?view=assignments"),
  ).toBe(false);
  expect(navIsActive("/staff?view=entry", "/staff", "")).toBe(true);
});
it("redirects a wrong-role page back to that account's workspace", async () => {
  render(
    <MemoryRouter initialEntries={["/admin"]}>
      <Routes>
        <Route
          path="/admin"
          element={
            <RequireRole role="ADMIN">
              <p>Admin secret</p>
            </RequireRole>
          }
        />
        <Route path="/manager" element={<p>Manager home</p>} />
      </Routes>
    </MemoryRouter>,
  );
  await screen.findByText("Manager home");
  expect(screen.queryByText("Admin secret")).toBeNull();
});
