import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import {
  fireEvent,
  render,
  screen,
  waitFor,
  within,
} from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router-dom";
import StaffWorkspace from "./StaffWorkspace.jsx";
import { clearAuthSession, setAuthSession } from "../services/authSession.js";

let model;
let fetchMock;
const page = (items) => ({
  items,
  totalCount: items.length,
  totalPages: items.length ? 1 : 0,
  pageNumber: 1,
  pageSize: 100,
});
const result = (data, message = "Đã cập nhật dữ liệu.") =>
  Response.json({ data, message });
const requests = (suffix) =>
  fetchMock.mock.calls.filter(
    ([url, options]) => options.method === "POST" && url.endsWith(suffix),
  );
const primary = {
  id: 10,
  lotId: 1,
  lotName: "Bãi Trung Tâm",
  plateNumber: "51F-123.45",
  status: "ACTIVE",
  currentSlotId: null,
  slotCode: null,
  checkInTime: "2026-10-30T01:00:00Z",
  totalFee: 0,
};

function mount(view = "entry", extra = "") {
  return render(
    <MemoryRouter
      initialEntries={[`/staff?view=${view}${extra}`]}
      future={{ v7_startTransition: true, v7_relativeSplatPath: true }}
    >
      <StaffWorkspace />
    </MemoryRouter>,
  );
}

beforeEach(() => {
  model = {
    sessions: [{ ...primary }],
    dispatches: [],
    failUpload: false,
    denyCheckIn: false,
    slots: [
      {
        id: 101,
        floorId: 1,
        lotId: 1,
        slotCode: "S-01",
        physicalStatus: "EMPTY",
        isAvailable: true,
        isActive: true,
      },
      {
        id: 102,
        floorId: 1,
        lotId: 1,
        slotCode: "S-02",
        physicalStatus: "OCCUPIED",
        isAvailable: false,
        isActive: true,
      },
      {
        id: 103,
        floorId: 1,
        lotId: 1,
        slotCode: "S-03",
        physicalStatus: "MAINTENANCE",
        isAvailable: false,
        isActive: true,
      },
    ],
  };
  setAuthSession({
    accessToken: "staff-token",
    userProfile: { id: 3, username: "staff", role: "STAFF" },
  });
  fetchMock = vi.fn(async (input, options = {}) => {
    const url = new URL(input, "http://localhost");
    const path = url.pathname;
    const body =
      typeof options.body === "string" ? JSON.parse(options.body) : {};
    if (path.endsWith("/staff-assignments/my-lots"))
      return result([
        { lotId: 1, lotName: "Bãi Trung Tâm", isActive: true },
        { lotId: 2, lotName: "Bãi Phía Đông", isActive: true },
      ]);
    if (path.endsWith("/capacity"))
      return result({
        availableForWalkIn: 3,
        activeSessionsCount: model.sessions.length,
        unassignedSessionsCount: 1,
        physicalOccupiedSlotsCount: 1,
      });
    if (path.endsWith("/parking-sessions/active"))
      return result(
        page(
          model.sessions.filter(
            (s) =>
              ["ACTIVE", "PAID"].includes(s.status) &&
              s.lotId === Number(url.searchParams.get("lotId")),
          ),
        ),
      );
    if (/\/parking-sessions\/\d+$/.test(path))
      return result(
        model.sessions.find((s) => s.id === Number(path.split("/").at(-1))),
      );
    if (path.includes("/by-plate/"))
      return result(
        model.sessions.find(
          (s) => s.plateNumber === decodeURIComponent(path.split("/").at(-2)),
        ),
      );
    if (path.endsWith("/floors"))
      return result([{ id: 1, name: "Tầng B1", isActive: true }]);
    if (path.endsWith("/slots")) return result(page(model.slots));
    if (/\/parking-slots\/\d+$/.test(path))
      return result(
        model.slots.find((s) => s.id === Number(path.split("/").at(-1))),
      );
    if (path.endsWith("/unmatched-occupied-slots"))
      return result([{ slotId: 102, slotCode: "S-02", floorNumber: -1 }]);
    if (path.endsWith("/staff/detect"))
      return result({
        id: 501,
        plateNumber: "51F12345",
        rawPlateNumber: "51F-123.45",
        confidence: 0.92,
        status: "PENDING_CONFIRMATION",
      });
    if (path.endsWith("/detections/501/confirm"))
      return result({
        id: 501,
        confirmedPlateNumber: body.plateNumber,
        confirmedByStaffUserId: 3,
      });
    if (path.endsWith("/images"))
      return model.failUpload
        ? Response.json(
            { message: "Dịch vụ lưu ảnh đang gián đoạn." },
            { status: 503 },
          )
        : result({ id: 88, url: "https://images.example.invalid/entry.jpg" });
    if (path.endsWith("/check-in")) {
      if (model.denyCheckIn)
        return Response.json(
          { message: "Bạn không có quyền nghiệp vụ cổng tại bãi này." },
          { status: 403 },
        );
      const session = {
        ...primary,
        id: 20,
        plateNumber: body.plateNumber,
        lotId: body.lotId,
        checkInImageUrl: body.checkInImageUrl,
      };
      model.sessions.push(session);
      return result(session, "Đã nhận xe.");
    }
    if (path.endsWith("/check-out")) {
      model.sessions[0].status = "COMPLETED";
      model.sessions[0].checkOutTime = "2026-10-30T02:00:00Z";
      return result(model.sessions[0]);
    }
    if (path.endsWith("/verify-slot")) {
      model.sessions[0].currentSlotId = body.slotId;
      model.sessions[0].slotCode = "S-01";
      model.dispatches.push({
        id: 77,
        sessionId: 10,
        plateNumber: primary.plateNumber,
        assignedSlotCode: "S-01",
        actualSlotCode: "S-01",
        status: "OCCUPIED",
      });
      return result(model.sessions[0], "Đã xác minh xe tại S-01.");
    }
    if (path.endsWith("/report-wrong-slot"))
      return result(model.sessions[0], "Đã ghi nhận vị trí thực tế.");
    if (path.endsWith("/cancel")) {
      model.dispatches[0].status = "CANCELLED";
      return result(model.dispatches[0], "Đã hủy điều phối.");
    }
    if (path.endsWith("/parking-dispatches") && options.method === "POST")
      return result({ id: 77, ...body }, "Đã phân ô.");
    if (path.endsWith("/parking-dispatches"))
      return result(
        page(
          model.dispatches.filter(
            (d) =>
              !url.searchParams.get("status") ||
              d.status === url.searchParams.get("status"),
          ),
        ),
      );
    throw new Error(`Unmocked API: ${path}`);
  });
  vi.stubGlobal("fetch", fetchMock);
});
afterEach(() => {
  clearAuthSession();
  vi.unstubAllGlobals();
});

describe("Staff workspace", () => {
  it("receives a walk-in vehicle without assigning a physical slot", async () => {
    const user = userEvent.setup();
    mount();
    await screen.findByRole("option", { name: "Bãi Trung Tâm" });
    await waitFor(() =>
      expect(
        screen.getByRole("button", { name: "Xác nhận nhận xe" }).disabled,
      ).toBe(false),
    );
    await user.type(screen.getByLabelText("Biển số xe"), "51a-99999");
    await user.click(screen.getByRole("checkbox"));
    await user.click(screen.getByRole("button", { name: "Xác nhận nhận xe" }));
    await screen.findByText(/Đã nhận xe thành công/);
    expect(JSON.parse(requests("/check-in")[0][1].body)).toEqual({
      lotId: 1,
      plateNumber: "51A-99999",
    });
    expect(screen.getByLabelText("Biển số xe").value).toBe("");
  });

  it("shows backend duty rejection and keeps the entry draft for correction", async () => {
    model.denyCheckIn = true;
    const user = userEvent.setup();
    mount();
    await waitFor(() =>
      expect(
        screen.getByRole("button", { name: "Xác nhận nhận xe" }).disabled,
      ).toBe(false),
    );
    await user.type(screen.getByLabelText("Biển số xe"), "51A99999");
    await user.click(screen.getByRole("checkbox"));
    await user.click(screen.getByRole("button", { name: "Xác nhận nhận xe" }));
    await screen.findByText(/không có quyền nghiệp vụ cổng/);
    expect(screen.getByLabelText("Biển số xe").value).toBe("51A99999");
    expect(screen.queryByText(/Đã nhận xe thành công/)).toBeNull();
  });

  it("confirms an OCR correction with a reason and attaches the uploaded image", async () => {
    const user = userEvent.setup();
    mount();
    await screen.findByLabelText("Biển số xe");
    await user.upload(
      screen.getByLabelText(/Chọn \/ chụp ảnh/),
      new File(["jpeg"], "entry.jpg", { type: "image/jpeg" }),
    );
    await user.click(screen.getByRole("button", { name: "Nhận diện biển số" }));
    await waitFor(() =>
      expect(screen.getByLabelText("Biển số xe").value).toBe("51F12345"),
    );
    await user.clear(screen.getByLabelText("Biển số xe"));
    await user.type(screen.getByLabelText("Biển số xe"), "51F12346");
    await user.type(
      screen.getByLabelText("Lý do sửa biển số AI"),
      "Đối chiếu số cuối trên xe.",
    );
    await user.click(screen.getByRole("checkbox"));
    await user.click(screen.getByRole("button", { name: "Xác nhận nhận xe" }));
    await screen.findByText(/Đã nhận xe thành công/);
    expect(JSON.parse(requests("/confirm")[0][1].body)).toEqual({
      plateNumber: "51F12346",
      note: "Đối chiếu số cuối trên xe.",
    });
    expect(JSON.parse(requests("/check-in")[0][1].body).checkInImageUrl).toBe(
      "https://images.example.invalid/entry.jpg",
    );
  });

  it("does not report a successful entry when photo persistence fails", async () => {
    model.failUpload = true;
    const user = userEvent.setup();
    mount();
    await screen.findByLabelText("Biển số xe");
    await user.upload(
      screen.getByLabelText(/Chọn \/ chụp ảnh/),
      new File(["jpeg"], "entry.jpg", { type: "image/jpeg" }),
    );
    await user.type(screen.getByLabelText("Biển số xe"), "51F12345");
    await user.click(screen.getByRole("checkbox"));
    await user.click(screen.getByRole("button", { name: "Xác nhận nhận xe" }));
    await screen.findByText("Dịch vụ lưu ảnh đang gián đoạn.");
    expect(requests("/check-in")).toHaveLength(0);
    expect(screen.queryByText(/Đã nhận xe thành công/)).toBeNull();
  });

  it("clears the old OCR plate when a different photo is chosen", async () => {
    const user = userEvent.setup();
    mount();
    await screen.findByLabelText("Biển số xe");
    await user.upload(
      screen.getByLabelText(/Chọn \/ chụp ảnh/),
      new File(["one"], "one.jpg", { type: "image/jpeg" }),
    );
    await user.click(screen.getByRole("button", { name: "Nhận diện biển số" }));
    await waitFor(() =>
      expect(screen.getByLabelText("Biển số xe").value).toBe("51F12345"),
    );
    await user.upload(
      screen.getByLabelText("Đổi ảnh"),
      new File(["two"], "two.jpg", { type: "image/jpeg" }),
    );
    expect(screen.getByLabelText("Biển số xe").value).toBe("");
    expect(screen.queryByText(/AI đọc được/)).toBeNull();
  });

  it("blocks exit while payment is unconfirmed", async () => {
    mount("exit", "&sessionId=10");
    await screen.findByText("Chưa xác nhận thanh toán.");
    expect(
      screen.getByRole("button", { name: "Xác nhận xe đã ra bãi" }).disabled,
    ).toBe(true);
    expect(requests("/check-out")).toHaveLength(0);
  });

  it("reloads the displayed exit session when payment changes and Staff refreshes", async () => {
    const user = userEvent.setup();
    mount("exit", "&sessionId=10");
    await screen.findByText("Chưa xác nhận thanh toán.");
    model.sessions[0].status = "PAID";
    model.sessions[0].totalFee = 15000;
    await user.click(
      screen.getByRole("button", { name: "Làm mới", exact: true }),
    );
    await waitFor(() =>
      expect(
        screen.getByRole("button", { name: "Xác nhận xe đã ra bãi" }).disabled,
      ).toBe(false),
    );
    expect(screen.queryByText("Chưa xác nhận thanh toán.")).toBeNull();
    expect(requests("/check-out")).toHaveLength(0);
  });

  it("compares the observed exit plate and completes a paid session", async () => {
    model.sessions[0].status = "PAID";
    model.sessions[0].totalFee = 15000;
    const user = userEvent.setup();
    mount("exit", "&sessionId=10");
    const input = await screen.findByLabelText("Biển số thực tế tại cổng ra");
    expect(input.value).toBe("");
    await user.type(input, "51F99999");
    await user.click(screen.getByRole("checkbox"));
    await user.click(
      screen.getByRole("button", { name: "Xác nhận xe đã ra bãi" }),
    );
    await screen.findByText(/Biển số tại cổng ra không khớp/);
    expect(requests("/check-out")).toHaveLength(0);
    await user.clear(input);
    await user.type(input, "51F12345");
    await user.click(
      screen.getByRole("button", { name: "Xác nhận xe đã ra bãi" }),
    );
    await screen.findByText(/Phiên đã kết thúc/);
    expect(JSON.parse(requests("/check-out")[0][1].body)).toEqual({
      confirmedPlateNumber: "51F12345",
    });
  });

  it("disables an EMPTY slot linked to another active session", async () => {
    model.sessions.push({
      ...primary,
      id: 11,
      plateNumber: "51F99999",
      currentSlotId: 101,
    });
    mount("verify", "&sessionId=10");
    const choice = await screen.findByRole("button", {
      name: /S-01.*51F99999/,
    });
    expect(choice.disabled).toBe(true);
    expect(screen.getByRole("button", { name: /S-03/ }).disabled).toBe(true);
  });

  it("verifies the actual slot and retains the result in dispatch history", async () => {
    const user = userEvent.setup();
    mount("verify", "&sessionId=10");
    await user.click(
      await screen.findByRole("button", { name: /S-01.*Cảm biến: trống/ }),
    );
    await user.click(screen.getByRole("checkbox"));
    await user.click(screen.getByRole("button", { name: "Xác nhận xe tại ô" }));
    await screen.findByText("Đã xác minh xe tại S-01.");
    expect(JSON.parse(requests("/verify-slot")[0][1].body)).toEqual({
      slotId: 101,
      notes: "",
    });
    await user.click(screen.getByRole("tab", { name: "Điều phối" }));
    await screen.findByText("Đã xác minh", { selector: ".staff-badge" });
    expect(screen.getByText("Đã lưu kết quả")).toBeTruthy();
  });

  it("requires a reason when reporting the actual wrong slot", async () => {
    const user = userEvent.setup();
    mount("verify", "&sessionId=10");
    await screen.findByRole("radio", { name: "Báo đỗ sai ô" });
    await user.click(screen.getByRole("radio", { name: "Báo đỗ sai ô" }));
    await user.click(
      screen.getByRole("button", { name: /S-02.*Cảm biến: có xe/ }),
    );
    const reason = screen.getByLabelText("Lý do đỗ sai ô");
    expect(reason.required).toBe(true);
    await user.type(reason, "Xe thực tế nằm ở S-02.");
    await user.click(screen.getByRole("checkbox"));
    await user.click(
      screen.getByRole("button", { name: "Ghi nhận xe đỗ sai ô" }),
    );
    await screen.findByText("Đã ghi nhận vị trí thực tế.");
    expect(JSON.parse(requests("/report-wrong-slot")[0][1].body)).toEqual({
      actualSlotId: 102,
      reason: "Xe thực tế nằm ở S-02.",
    });
  });

  it("cancels an assignment only after an explicit reason and confirmation", async () => {
    model.dispatches.push({
      id: 77,
      sessionId: 10,
      plateNumber: primary.plateNumber,
      assignedSlotCode: "S-01",
      status: "ASSIGNED",
    });
    const user = userEvent.setup();
    mount("dispatches");
    await user.click(
      await screen.findByRole("button", { name: "Hủy", exact: true }),
    );
    expect(requests("/cancel")).toHaveLength(0);
    const form = screen.getByRole("form", { name: "Hủy điều phối" });
    await user.type(
      within(form).getByLabelText("Lý do hủy"),
      "Điều chỉnh luồng xe.",
    );
    await user.click(
      within(form).getByRole("button", { name: "Xác nhận hủy điều phối" }),
    );
    await screen.findByText("Đã hủy điều phối.");
    expect(JSON.parse(requests("/cancel")[0][1].body)).toEqual({
      reason: "Điều chỉnh luồng xe.",
    });
    expect(
      await screen.findByText("Đã hủy", { selector: ".staff-badge" }),
    ).toBeTruthy();
  });

  it("locks lot switching while a mutation is pending and prevents duplicate submission", async () => {
    let resolveWrite;
    const original = fetchMock.getMockImplementation();
    fetchMock.mockImplementation((url, options) =>
      url.endsWith("/check-in")
        ? new Promise((resolve) => {
            resolveWrite = resolve;
          })
        : original(url, options),
    );
    const user = userEvent.setup();
    mount();
    await waitFor(() =>
      expect(
        screen.getByRole("button", { name: "Xác nhận nhận xe" }).disabled,
      ).toBe(false),
    );
    await user.type(screen.getByLabelText("Biển số xe"), "51F12345");
    await user.click(screen.getByRole("checkbox"));
    const form = screen.getByRole("form", { name: "Nhận xe vào bãi" });
    fireEvent.submit(form);
    fireEvent.submit(form);
    await waitFor(() => expect(requests("/check-in")).toHaveLength(1));
    expect(
      screen.getByRole("combobox", { name: "Bãi đang làm việc" }).disabled,
    ).toBe(true);
    expect(screen.getByRole("tab", { name: "Xe ra" }).disabled).toBe(true);
    resolveWrite(result({ ...primary, id: 20 }));
    await screen.findByText(/Đã nhận xe thành công/);
    expect(
      screen.getByRole("combobox", { name: "Bãi đang làm việc" }).disabled,
    ).toBe(false);
  });

  it("clears the entry draft when switching to a different assigned lot", async () => {
    const user = userEvent.setup();
    mount();
    await screen.findByLabelText("Biển số xe");
    await user.type(screen.getByLabelText("Biển số xe"), "51F12345");
    await user.selectOptions(
      screen.getByRole("combobox", { name: "Bãi đang làm việc" }),
      "2",
    );
    expect(screen.getByLabelText("Biển số xe").value).toBe("");
    expect(requests("/check-in")).toHaveLength(0);
  });
});
