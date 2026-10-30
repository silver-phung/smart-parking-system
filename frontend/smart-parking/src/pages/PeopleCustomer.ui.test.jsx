import { beforeEach, afterEach, expect, it, vi } from "vitest";
import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import AdminPeople from "./AdminPeople.jsx";
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
  auth("ADMIN");
  mount(<AdminPeople />);
  fireEvent.change(await screen.findByLabelText("Vai trò staffdemo"), {
    target: { value: "MANAGER" },
  });
  expect(writes()).toHaveLength(0);
  fireEvent.change(screen.getByLabelText("Lý do"), {
    target: { value: "Phụ trách bãi demo" },
  });
  fireEvent.click(screen.getByRole("button", { name: "Xác nhận thay đổi" }));
  await screen.findByText("Đã lưu thành công");
  expect(writes()[0][0]).toBe("/api/v1/admin/users/7/role");
  expect(JSON.parse(writes()[0][1].body)).toEqual({
    role: "MANAGER",
    reason: "Phụ trách bãi demo",
  });
});
it("creates an explicit SLOT_VERIFIER duty assignment", async () => {
  auth("ADMIN");
  mount(<AdminPeople />, "/admin/people?view=assignments");
  await screen.findByRole("option", { name: /Nhân viên Demo/ });
  fireEvent.change(screen.getByLabelText("Nhân sự"), {
    target: { value: "7" },
  });
  fireEvent.change(screen.getByLabelText("Bãi đỗ"), { target: { value: "1" } });
  fireEvent.change(screen.getByLabelText("Nhiệm vụ"), {
    target: { value: "SLOT_VERIFIER" },
  });
  await waitFor(() =>
    expect(screen.getByRole("button", { name: "Lưu phân công" }).disabled).toBe(
      false,
    ),
  );
  fireEvent.click(screen.getByRole("button", { name: "Lưu phân công" }));
  await screen.findByText("Đã lưu thành công");
  expect(JSON.parse(writes()[0][1].body)).toMatchObject({
    userId: 7,
    lotId: 1,
    duty: "SLOT_VERIFIER",
  });
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
  auth("ADMIN");
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
