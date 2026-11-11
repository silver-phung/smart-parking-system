import { beforeEach, afterEach, expect, it, vi } from "vitest";
import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import AdminPeople from "./AdminPeople.jsx";
import ManagerStaffAssignments from "./ManagerStaffAssignments.jsx";
import CustomerSessions from "./CustomerSessions.jsx";
import CustomerDashboard from "./CustomerDashboard.jsx";
import Register from "./Register.jsx";
import { setAuthSession, clearAuthSession } from "../services/authSession.js";

let fetcher;
let failWrite;
const user = {
  id: 7,
  username: "staffdemo",
  fullName: "Nhân viên Demo",
  email: "staff@example.com",
  role: "STAFF",
  isActive: true,
  isEmailVerified: true,
  approvalStatus: "APPROVED",
};
const session = {
  id: 10,
  plateNumber: "51A-12345",
  lotName: "Bãi Trung Tâm",
  status: "ACTIVE",
  checkInTime: "2026-10-30T01:00:00Z",
  totalFee: 0,
};
const page = (items) => ({ items, totalCount: items.length, totalPages: 1 });
function mount(element, path = "/") {
  return render(
    <MemoryRouter
      initialEntries={[path]}
      future={{ v7_startTransition: true, v7_relativeSplatPath: true }}
    >
      {element}
    </MemoryRouter>,
  );
}
function auth(role) {
  setAuthSession({ accessToken: "demo-token", userProfile: { id: 1, role } });
}
beforeEach(() => {
  failWrite = false;
  fetcher = vi
    .spyOn(globalThis, "fetch")
    .mockImplementation(async (url, options = {}) => {
      if (options.method === "POST" || options.method === "PATCH")
        return Response.json(
          failWrite
            ? { message: "Không có quyền thao tác" }
            : { data: { id: 7 }, message: "Đã lưu thành công" },
          { status: failWrite ? 403 : 200 },
        );
      let data;
      if (url.includes("/admin/users/7"))
        data = { ...user, activeAssignmentsCount: 1 };
      else if (url.includes("/admin/users?")) data = page([user]);
      else if (url.includes("/staff-assignments/candidates?")) data = page([user, { id: 8, fullName: "Quản lý Demo", username: "managerdemo", role: "MANAGER" }]);
      else if (url.endsWith("/staff-assignments/my-lots")) data = [{ lotId: 1, lotName: "Bãi Trung Tâm", isActive: true }];
      else if (url.includes("/staff-assignments/locations/")) {
        const lot = Number(url.split("/").at(-1));
        data = { floors: [
          { id: lot * 10, name: `Tầng ${lot}A`, zones: [{ id: lot * 100, name: `Khu ${lot}A` }] },
          { id: lot * 10 + 1, name: `Tầng ${lot}B`, zones: [{ id: lot * 100 + 1, name: `Khu ${lot}B` }] },
        ] };
      }
      else if (url.includes("/staff-assignments?"))
        data = page([
          {
            id: 3,
            userId: 7,
            username: user.username,
            userFullName: user.fullName,
            userRole: "STAFF",
            lotName: "Bãi Trung Tâm",
            duty: "GATE",
            isActive: true,
          },
        ]);
      else if (url.includes("/my-sessions?")) data = page([session]);
      else if (url.endsWith("/parking-sessions/10")) data = session;
      else if (url.includes("/floors?")) data = [{ id: 2, name: "Tầng 1" }];
      else if (url.includes("/slots?"))
        data = page([
          {
            id: 9,
            slotCode: "A-01",
            physicalStatus: "EMPTY",
            isAvailable: false,
          },
        ]);
      else if (url.includes("/parking-lots?"))
        data = page([
          {
            id: 1,
            name: "Bãi Trung Tâm",
            isActive: true,
            totalCapacity: 20,
            availableSlotsCount: 10,
          },
        ]);
      else throw new Error(`Unexpected URL ${url}`);
      return Response.json({ data });
    });
});
afterEach(clearAuthSession);
it("loads subsequent lot pages for assignment selection", async () => {
  const fallback = fetcher.getMockImplementation();
  fetcher.mockImplementation(async (url, options) => {
    if (url.includes("/parking-lots?")) {
      const second =
        new URL(url, "http://local").searchParams.get("pageNumber") === "2";
      return Response.json({
        data: {
          items: [
            { id: second ? 2 : 1, name: second ? "Bãi Trang Hai" : "Bãi Một" },
          ],
          totalCount: 2,
          totalPages: 2,
        },
      });
    }
    return fallback(url, options);
  });
  auth("ADMIN");
  mount(<AdminPeople />, "/admin/people?view=assignments");
  await screen.findByRole("option", { name: "Bãi Trang Hai" });
});
it("sends Customer calendar date boundaries in UTC", async () => {
  auth("USER");
  mount(<CustomerSessions />);
  fireEvent.change(screen.getByLabelText("Từ ngày"), {
    target: { value: "2026-10-30" },
  });
  await waitFor(() =>
    expect(
      fetcher.mock.calls.some(([url]) => {
        const value = new URL(url, "http://local").searchParams.get("fromDate");
        return value === new Date("2026-10-30T00:00:00").toISOString();
      }),
    ).toBe(true),
  );
});
const writes = () =>
  fetcher.mock.calls.filter(([, o]) => ["POST", "PATCH"].includes(o.method));
it("creates an internal account without retaining the password", async () => {
  auth("ADMIN");
  mount(<AdminPeople />);
  fireEvent.change(screen.getByLabelText("Tên đăng nhập"), {
    target: { value: "staffnew" },
  });
  fireEvent.change(screen.getByLabelText("Họ và tên"), {
    target: { value: "Staff Mới" },
  });
  fireEvent.change(screen.getByLabelText("Email"), {
    target: { value: "new@example.com" },
  });
  fireEvent.change(screen.getByLabelText("Mật khẩu ban đầu"), {
    target: { value: "Password123!" },
  });
  fireEvent.click(
    screen.getByRole("button", { name: "Tạo tài khoản", exact: true }),
  );
  await screen.findByText("Đã lưu thành công");
  expect(JSON.parse(writes()[0][1].body)).toMatchObject({
    username: "staffnew",
    role: "STAFF",
    isActive: true,
  });
  expect(screen.getByLabelText("Mật khẩu ban đầu").value).toBe("");
});
it("preserves a failed account draft and shows the server permission error", async () => {
  failWrite = true;
  auth("ADMIN");
  mount(<AdminPeople />);
  fireEvent.change(screen.getByLabelText("Tên đăng nhập"), {
    target: { value: "draft" },
  });
  fireEvent.submit(
    screen
      .getByRole("button", { name: "Tạo tài khoản", exact: true })
      .closest("form"),
  );
  await screen.findByRole("alert");
  expect(screen.getByLabelText("Tên đăng nhập").value).toBe("draft");
});
it("confirms role changes with a reason before sending a PATCH", async () => {
  let storedRole = "STAFF";
  const fallback = fetcher.getMockImplementation();
  fetcher.mockImplementation(async (url, options = {}) => {
    if (url.endsWith("/admin/users/7/role") && options.method === "PATCH") {
      storedRole = JSON.parse(options.body).role;
      return Response.json({ data: { ...user, role: storedRole }, message: "Đã lưu thành công" });
    }
    if (url.includes("/admin/users?")) return Response.json({ data: page([{ ...user, role: storedRole }]) });
    return fallback(url, options);
  });
  auth("ADMIN");
  mount(<AdminPeople />);
  fireEvent.change(await screen.findByLabelText("Vai trò staffdemo"), {
    target: { value: "MANAGER" },
  });
  expect(screen.getByLabelText("Vai trò staffdemo").value).toBe("MANAGER");
  expect(writes()).toHaveLength(0);
  fireEvent.click(screen.getByRole("button", { name: "Lưu vai trò staffdemo" }));
  fireEvent.change(screen.getByLabelText("Ghi chú thay đổi (không bắt buộc)"), {
    target: { value: "Phụ trách bãi demo" },
  });
  fireEvent.click(screen.getByRole("button", { name: "Xác nhận thay đổi" }));
  await screen.findByText("Đã lưu thành công");
  expect(writes()[0][0]).toBe("/api/v1/admin/users/7/role");
  expect(JSON.parse(writes()[0][1].body)).toEqual({
    role: "MANAGER",
    reason: "Phụ trách bãi demo",
  });
  await waitFor(() => expect(screen.getByLabelText("Vai trò staffdemo").value).toBe("MANAGER"));
  expect(screen.getByRole("button", { name: "Lưu vai trò staffdemo" }).disabled).toBe(true);
});
it("offers only Staff/Manager account roles and omits account approval information", async () => {
  auth("ADMIN"); mount(<AdminPeople />);
  await screen.findByLabelText("Vai trò staffdemo");
  for (const label of ["Vai trò", "Vai trò staffdemo"]) {
    expect([...screen.getByLabelText(label, { exact: true }).options].map(o => o.value)).toEqual(["STAFF", "MANAGER"]);
  }
  expect(screen.queryByRole("option", { name: "USER", exact: true })).toBeNull();
  expect(screen.queryByText("APPROVED")).toBeNull();
  expect(fetcher.mock.calls.some(([url]) => url.includes("internalOnly=true"))).toBe(true);
  fireEvent.click(screen.getByRole("button", { name: "Nhân viên Demo" }));
  await screen.findByRole("heading", { name: "Chi tiết tài khoản" });
  expect(screen.queryByText("Phê duyệt")).toBeNull();
});
it("allows role changes without an approval reason and shows server failures for retry", async () => {
  failWrite = true; auth("ADMIN"); mount(<AdminPeople />);
  const roleSelect = await screen.findByLabelText("Vai trò staffdemo");
  fireEvent.change(roleSelect, { target: { value: "MANAGER" } });
  fireEvent.click(screen.getByRole("button", { name: "Lưu vai trò staffdemo" }));
  fireEvent.click(screen.getByRole("button", { name: "Xác nhận thay đổi" }));
  await screen.findByRole("alert");
  expect(roleSelect.value).toBe("MANAGER");
  expect(screen.getByRole("region", { name: "Xác nhận đổi vai trò" })).toBeTruthy();
  failWrite = false;
  fireEvent.click(screen.getByRole("button", { name: "Xác nhận thay đổi" }));
  await screen.findByText("Đã lưu thành công");
  expect(writes()).toHaveLength(2);
  expect(JSON.parse(writes()[1][1].body)).toEqual({ role: "MANAGER", reason: "" });
});
it("creates an explicit SLOT_VERIFIER duty assignment", async () => {
  auth("ADMIN");
  mount(<AdminPeople />, "/admin/people?view=assignments");
  fireEvent.change(screen.getByLabelText("Tìm Staff theo tên/email"), { target: { value: "staff" } });
  fireEvent.click(await screen.findByRole("button", { name: /Nhân viên Demo/ }));
  expect(screen.queryByRole("combobox", { name: "Nhân sự" })).toBeNull();
  fireEvent.change(screen.getByLabelText("Bãi đỗ"), { target: { value: "1" } });
  fireEvent.change(screen.getByLabelText("Phân công", { exact: true }), { target: { value: "SLOT_VERIFIER" } });
  await screen.findByRole("option", { name: "Tầng 1A" });
  fireEvent.change(screen.getByLabelText("Tầng"), { target: { value: "10" } });
  fireEvent.change(screen.getByLabelText("Khu vực (Zone)"), { target: { value: "100" } });
  await waitFor(() =>
    expect(screen.getByRole("button", { name: "Lưu phân công" }).disabled).toBe(
      false,
    ),
  );
  fireEvent.click(screen.getByRole("button", { name: "Lưu phân công" }));
  await screen.findByText("Đã lưu thành công");
  expect(JSON.parse(writes()[0][1].body)).toMatchObject({
    userId: 7,
    lotId: 1, floorId: 10, zoneId: 100,
    duty: "SLOT_VERIFIER",
  });
});
it("lets Manager assign only Staff at managed lots without calling account-management APIs", async () => {
  auth("MANAGER");
  mount(<ManagerStaffAssignments />, "/manager/assignments");
  fireEvent.change(screen.getByLabelText("Tìm Staff theo tên/email"), { target: { value: "staff" } });
  fireEvent.click(await screen.findByRole("button", { name: /Nhân viên Demo/ }));
  expect(screen.queryByRole("combobox", { name: "Nhân sự" })).toBeNull();
  expect(screen.queryByRole("button", { name: "Tài khoản", exact: true })).toBeNull();
  expect(screen.queryByRole("heading", { name: "Tạo tài khoản nội bộ" })).toBeNull();
  fireEvent.change(screen.getByLabelText("Bãi đỗ"), { target: { value: "1" } });
  fireEvent.change(screen.getByLabelText("Phân công", { exact: true }), { target: { value: "GATE_IN" } });
  await screen.findByRole("option", { name: "Tầng 1A" });
  fireEvent.change(screen.getByLabelText("Tầng"), { target: { value: "10" } });
  expect(screen.queryByLabelText("Khu vực (Zone)")).toBeNull();
  await waitFor(() => expect(screen.getByRole("button", { name: "Lưu phân công" }).disabled).toBe(false));
  fireEvent.click(screen.getByRole("button", { name: "Lưu phân công" }));
  await screen.findByText("Đã lưu thành công");
  expect(JSON.parse(writes()[0][1].body)).toMatchObject({ userId: 7, lotId: 1, floorId: 10, zoneId: null, duty: "GATE_IN" });
  expect(fetcher.mock.calls.some(([url]) => url.includes("/admin/users"))).toBe(false);
});
it("requires confirmation for revoking an assignment", async () => {
  auth("ADMIN");
  mount(<AdminPeople />, "/admin/people?view=assignments");
  fireEvent.click(
    await screen.findByRole("button", { name: "Thu hồi", exact: true }),
  );
  expect(writes()).toHaveLength(0);
  fireEvent.change(screen.getByLabelText("Lý do"), {
    target: { value: "Đổi nhiệm vụ" },
  });
  fireEvent.click(screen.getByRole("button", { name: "Xác nhận thay đổi" }));
  await screen.findByText("Đã lưu thành công");
  expect(writes()[0][0]).toBe("/api/v1/staff-assignments/3/revoke");
});

it.each(["GATE_IN", "GATE_OUT"])("clears a previous zone and submits %s with only a floor", async (duty) => {
  auth("ADMIN"); mount(<AdminPeople />, "/admin/people?view=assignments");
  fireEvent.change(screen.getByLabelText("Tìm Staff theo tên/email"), { target: { value: "staff" } });
  fireEvent.click(await screen.findByRole("button", { name: /Nhân viên Demo/ }));
  fireEvent.change(screen.getByLabelText("Bãi đỗ"), { target: { value: "1" } });
  fireEvent.change(screen.getByLabelText("Phân công", { exact: true }), { target: { value: "SLOT_VERIFIER" } });
  await screen.findByRole("option", { name: "Tầng 1A" });
  fireEvent.change(screen.getByLabelText("Tầng"), { target: { value: "10" } });
  fireEvent.change(screen.getByLabelText("Khu vực (Zone)"), { target: { value: "100" } });
  fireEvent.change(screen.getByLabelText("Phân công", { exact: true }), { target: { value: duty } });
  expect(screen.queryByLabelText("Khu vực (Zone)")).toBeNull();
  expect(screen.getByLabelText("Tầng").value).toBe("");
  expect(screen.getByRole("button", { name: "Lưu phân công" }).disabled).toBe(true);
  fireEvent.change(screen.getByLabelText("Tầng"), { target: { value: "10" } });
  fireEvent.click(screen.getByRole("button", { name: "Lưu phân công" }));
  await screen.findByText("Đã lưu thành công");
  expect(JSON.parse(writes()[0][1].body)).toMatchObject({ duty, floorId: 10, zoneId: null });
});

it("filters assignment areas by floor and resets descendants when lot or floor changes", async () => {
  auth("ADMIN"); mount(<AdminPeople />, "/admin/people?view=assignments");
  expect(screen.queryByLabelText("Tầng")).toBeNull();
  expect(screen.queryByLabelText("Khu vực (Zone)")).toBeNull();
  expect(screen.queryByRole("option", { name: "Cổng và xác minh ô" })).toBeNull();
  await screen.findByRole("option", { name: "Bãi Trung Tâm" });
  fireEvent.change(screen.getByLabelText("Bãi đỗ"), { target: { value: "1" } });
  fireEvent.change(screen.getByLabelText("Phân công", { exact: true }), { target: { value: "SLOT_VERIFIER" } });
  await screen.findByRole("option", { name: "Tầng 1A" });
  fireEvent.change(screen.getByLabelText("Tầng"), { target: { value: "10" } });
  expect(screen.getByRole("option", { name: "Khu 1A" })).toBeTruthy();
  expect(screen.queryByRole("option", { name: "Khu 1B" })).toBeNull();
  fireEvent.change(screen.getByLabelText("Khu vực (Zone)"), { target: { value: "100" } });
  fireEvent.change(screen.getByLabelText("Tầng"), { target: { value: "11" } });
  expect(screen.getByLabelText("Khu vực (Zone)").value).toBe("");
  expect(screen.getByRole("option", { name: "Khu 1B" })).toBeTruthy();
  fireEvent.change(screen.getByLabelText("Bãi đỗ"), { target: { value: "" } });
  expect(screen.getByLabelText("Tầng").value).toBe("");
  expect(screen.queryByLabelText("Khu vực (Zone)")).toBeNull();
});
it("uses only my-sessions and displays the authorized session detail", async () => {
  auth("USER");
  mount(<CustomerSessions />);
  fireEvent.click(await screen.findByRole("button", { name: "Xem phiên #10" }));
  await screen.findByRole("heading", { name: "Phiên #10" });
  expect(await screen.findByText("Phí ghi nhận")).toBeTruthy();
  expect(fetcher.mock.calls.some(([u]) => u.includes("/my-sessions?"))).toBe(
    true,
  );
  expect(fetcher.mock.calls.some(([u]) => u.includes("/active"))).toBe(false);
});
it("rejects an inverted date range without querying sessions", async () => {
  auth("USER");
  mount(<CustomerSessions />);
  fireEvent.change(screen.getByLabelText("Từ ngày"), {
    target: { value: "2026-10-31" },
  });
  const count = fetcher.mock.calls.length;
  fireEvent.change(screen.getByLabelText("Đến ngày"), {
    target: { value: "2026-10-01" },
  });
  await screen.findByRole("alert");
  expect(fetcher.mock.calls.length).toBe(count);
});
it("shows public parking without a token and refreshes floor/slot data", async () => {
  auth("USER");
  mount(<CustomerDashboard publicView />);
  await screen.findByText("A-01");
  expect(screen.getByText("Không khả dụng")).toBeTruthy();
  expect(
    fetcher.mock.calls.every(([, options]) => !options.headers.Authorization),
  ).toBe(true);
  const count = fetcher.mock.calls.filter(([url]) =>
    url.includes("/slots?"),
  ).length;
  fireEvent.click(screen.getByRole("button", { name: "Làm mới", exact: true }));
  await waitFor(() =>
    expect(
      fetcher.mock.calls.filter(([url]) => url.includes("/slots?")).length,
    ).toBeGreaterThan(count),
  );
});
it("keeps email registration and OTP requests public", async () => {
  auth("ADMIN");
  mount(<Register />);
  fireEvent.change(screen.getByLabelText("Email"), {
    target: { value: "customer@example.com" },
  });
  fireEvent.change(screen.getByLabelText("Mật khẩu", { exact: true }), {
    target: { value: "Password123!" },
  });
  fireEvent.change(screen.getByLabelText("Nhập lại mật khẩu"), {
    target: { value: "Password123!" },
  });
  fireEvent.click(screen.getByRole("button", { name: "Gửi mã", exact: true }));
  await waitFor(() =>
    expect(screen.getByLabelText("Mã OTP").disabled).toBe(false),
  );
  expect(writes()[0][0]).toBe("/api/v1/auth/register");
  expect(writes()[0][1].headers.Authorization).toBeUndefined();
  fireEvent.change(screen.getByLabelText("Mã OTP"), {
    target: { value: "123456" },
  });
  fireEvent.click(screen.getByRole("button", { name: "Đăng ký", exact: true }));
  await waitFor(() => expect(writes()).toHaveLength(2));
  expect(writes()[1][0]).toBe("/api/v1/auth/verify-email");
});

it("resends OTP after a reopened registration form receives a conflict", async () => {
  const email = "customer@example.com";
  fetcher
    .mockResolvedValueOnce(Response.json({ message: "Email đã tồn tại" }, { status: 409 }))
    .mockResolvedValueOnce(Response.json({ message: "Nếu email chưa xác minh, mã mới sẽ được gửi." }));

  mount(<Register />);
  fireEvent.change(screen.getByLabelText("Email"), { target: { value: email } });
  fireEvent.change(screen.getByLabelText("Mật khẩu", { exact: true }), {
    target: { value: "Password123!" },
  });
  fireEvent.change(screen.getByLabelText("Nhập lại mật khẩu"), {
    target: { value: "Password123!" },
  });
  fireEvent.click(screen.getByRole("button", { name: "Gửi mã", exact: true }));

  expect((await screen.findByRole("status")).textContent).toContain("Nếu email chưa xác minh");
  expect(screen.getByLabelText("Mã OTP").disabled).toBe(false);
  expect(screen.queryByRole("alert")).toBeNull();
  expect(writes().map(([url]) => url)).toEqual([
    "/api/v1/auth/register",
    "/api/v1/auth/resend-verification",
  ]);
  expect(JSON.parse(writes()[1][1].body)).toEqual({ email });
});
