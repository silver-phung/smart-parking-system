import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import CustomerDashboard from "./CustomerDashboard.jsx";
import { clearAuthSession, setAuthSession } from "../services/authSession.js";

const lot = { id: 1, name: "Bãi Trung Tâm", address: "Quận 1", totalCapacity: 30 };

function result(data, message = "Thành công.") {
  return Response.json({ data, message });
}

describe("CustomerDashboard booking", () => {
  let fetchMock;
  let model;

  beforeEach(() => {
    model = { vehicles: [], bookings: [], reserved: 0 };
    setAuthSession({
      accessToken: "user-token",
      refreshToken: "user-refresh-token",
      userProfile: { id: 7, username: "customer", role: "USER" },
    });
    fetchMock = vi.fn(async (input, options = {}) => {
      const url = new URL(input, "http://localhost");
      if (url.pathname === "/api/v1/parking-lots") {
        return result({
          items: [lot],
          totalCount: 1,
          totalPages: 1,
          pageNumber: 1,
          pageSize: 100,
        });
      }
      if (url.pathname === "/api/v1/parking-lots/1/floors") {
        return result([{ id: 11, name: "Tầng B1" }]);
      }
      if (url.pathname === "/api/v1/parking-floors/11/slots") {
        return result({ items: [], totalPages: 1, totalCount: 0 });
      }
      if (url.pathname === "/api/v1/parking-lots/1/capacity") {
        return result({
          lotId: 1,
          totalCapacity: 30,
          availableForBooking: 30 - model.reserved,
          pendingBookingsCount: model.reserved,
        });
      }
      if (url.pathname === "/api/v1/bookings/my") {
        return result({
          items: model.bookings,
          totalCount: model.bookings.length,
          totalPages: 1,
          pageNumber: 1,
          pageSize: 20,
        });
      }
      if (url.pathname === "/api/v1/users/me") {
        return result({
          fullName: "Nguyễn An",
          email: "an@example.com",
          phoneNumber: "0901234567",
        });
      }
      if (url.pathname === "/api/v1/vehicles") {
        return result({
          items: model.vehicles,
          totalCount: model.vehicles.length,
          totalPages: 1,
          pageNumber: 1,
          pageSize: 100,
        });
      }
      if (/^\/api\/v1\/bookings\/\d+\/cancel$/.test(url.pathname)) {
        const bookingId = Number(url.pathname.split("/")[4]);
        const booking = model.bookings.find((item) => item.id === bookingId);
        if (!booking)
          return Response.json(
            { message: "Không tìm thấy lượt đặt chỗ." },
            { status: 404 },
          );
        booking.status = "CANCELLED";
        model.reserved = Math.max(0, model.reserved - 1);
        return result(booking, "Hủy lượt đặt chỗ thành công.");
      }
      if (url.pathname === "/api/v1/bookings" && options.method === "POST") {
        const request = JSON.parse(options.body);
        model.reserved += 1;
        model.bookings.push({
          id: 28,
          lotId: 1,
          lotName: lot.name,
          lotAddress: lot.address,
          plateNumber: request.plateNumber,
          status: "PENDING",
          startTime: request.startTime,
          endTime: request.endTime,
          createdAt: new Date().toISOString(),
        });
        return result({ id: 28 }, "Đặt chỗ thành công.");
      }
      throw new Error(`Unexpected request: ${url.pathname}`);
    });
    vi.stubGlobal("fetch", fetchMock);
  });

  afterEach(() => {
    vi.unstubAllGlobals();
    clearAuthSession();
  });

  it("opens a booking form and submits the selected lot and vehicle details", async () => {
    render(
      <MemoryRouter
        future={{ v7_startTransition: true, v7_relativeSplatPath: true }}
      >
        <CustomerDashboard />
      </MemoryRouter>,
    );

    const openBookingButton = await screen.findByRole("button", {
      name: "Đặt chỗ tại bãi này",
    });
    fireEvent.click(openBookingButton);
    const startTimeInput = await screen.findByLabelText("Thời gian bắt đầu");
    expect(
      screen.queryByLabelText("Thời gian kết thúc (không bắt buộc)"),
    ).toBeNull();

    await screen.findByDisplayValue("Nguyễn An");
    fireEvent.change(screen.getByLabelText("Biển số xe"), {
      target: { value: "51F-123.45" },
    });
    fireEvent.change(screen.getByLabelText("Thời gian bắt đầu"), {
      target: { value: "2099-01-02T10:00" },
    });
    fireEvent.change(screen.getByLabelText("Ghi chú (không bắt buộc)"), {
      target: { value: "Gần lối vào" },
    });
    fireEvent.click(screen.getByRole("button", { name: "Xác nhận đặt chỗ" }));

    await screen.findByRole("status");
    const request = fetchMock.mock.calls.find(
      ([input, options]) =>
        new URL(input, "http://localhost").pathname === "/api/v1/bookings" &&
        options.method === "POST",
    );
    expect(JSON.parse(request[1].body)).toEqual({
      lotId: 1,
      plateNumber: "51F-123.45",
      startTime: new Date("2099-01-02T10:00").toISOString(),
      notes: "Gần lối vào",
    });
    expect(screen.getByRole("status").textContent).toContain(
      "Mã đặt chỗ #28.",
    );
    expect(await screen.findByText("51F-123.45")).toBeTruthy();
    expect(screen.getByText("Chờ xe đến")).toBeTruthy();
    await screen.findByText("29");
    expect(
      screen.getByText(/Đã tính cả 1 chỗ đang được booking giữ/),
    ).toBeTruthy();
  });

  it("automatically selects the only registered vehicle and submits its id", async () => {
    model.vehicles = [
      {
        id: 45,
        brand: "Honda",
        model: "Civic",
        color: "Trắng",
        plateNumber: "51A-456.78",
        isActive: true,
      },
    ];
    render(
      <MemoryRouter
        future={{ v7_startTransition: true, v7_relativeSplatPath: true }}
      >
        <CustomerDashboard />
      </MemoryRouter>,
    );

    fireEvent.click(
      await screen.findByRole("button", { name: "Đặt chỗ tại bãi này" }),
    );

    expect(await screen.findByDisplayValue("Nguyễn An")).toBeTruthy();
    expect(screen.getByDisplayValue("an@example.com")).toBeTruthy();
    expect(await screen.findByText("51A-456.78")).toBeTruthy();
    expect(screen.queryByLabelText("Biển số xe")).toBeNull();
    fireEvent.change(screen.getByLabelText("Thời gian bắt đầu"), {
      target: { value: "2099-01-02T10:00" },
    });
    fireEvent.click(screen.getByRole("button", { name: "Xác nhận đặt chỗ" }));

    await screen.findByRole("status");
    const request = fetchMock.mock.calls.find(
      ([input, options]) =>
        new URL(input, "http://localhost").pathname === "/api/v1/bookings" &&
        options.method === "POST",
    );
    expect(JSON.parse(request[1].body).vehicleId).toBe(45);
  });

  it("lets a customer choose among registered vehicles and shows each plate", async () => {
    model.vehicles = [
      { id: 45, brand: "Honda", model: "Civic", plateNumber: "51A-456.78", isActive: true },
      { id: 46, brand: "Toyota", model: "Vios", plateNumber: "59B-123.45", isActive: true },
    ];
    render(
      <MemoryRouter
        future={{ v7_startTransition: true, v7_relativeSplatPath: true }}
      >
        <CustomerDashboard />
      </MemoryRouter>,
    );

    fireEvent.click(
      await screen.findByRole("button", { name: "Đặt chỗ tại bãi này" }),
    );
    fireEvent.click(
      await screen.findByRole("button", { name: "Chọn xe của bạn" }),
    );
    fireEvent.click(
      screen.getByRole("option", { name: /Toyota Vios.*59B-123\.45/ }),
    );

    expect(screen.getAllByText("59B-123.45").length).toBeGreaterThan(0);
    fireEvent.change(screen.getByLabelText("Thời gian bắt đầu"), {
      target: { value: "2099-01-02T10:00" },
    });
    fireEvent.click(screen.getByRole("button", { name: "Xác nhận đặt chỗ" }));
    await screen.findByRole("status");

    const request = fetchMock.mock.calls.find(
      ([input, options]) =>
        new URL(input, "http://localhost").pathname === "/api/v1/bookings" &&
        options.method === "POST",
    );
    expect(JSON.parse(request[1].body).vehicleId).toBe(46);
  });

  it("lets a customer cancel a pending booking and refreshes the held capacity", async () => {
    model.bookings = [
      {
        id: 33,
        lotId: 1,
        lotName: lot.name,
        plateNumber: "60A-95678",
        status: "PENDING",
        startTime: "2099-01-02T10:00:00Z",
        createdAt: "2099-01-01T10:00:00Z",
      },
      {
        id: 34,
        lotId: 1,
        lotName: lot.name,
        plateNumber: "51A-12345",
        status: "CHECKED_IN",
        startTime: "2099-01-02T11:00:00Z",
        createdAt: "2099-01-01T10:00:00Z",
      },
    ];
    model.reserved = 1;
    render(
      <MemoryRouter
        future={{ v7_startTransition: true, v7_relativeSplatPath: true }}
      >
        <CustomerDashboard />
      </MemoryRouter>,
    );

    const cancelButton = await screen.findByRole("button", { name: "Hủy chỗ" });
    expect(screen.getAllByRole("button", { name: "Hủy chỗ" })).toHaveLength(1);
    fireEvent.click(cancelButton);

    expect(await screen.findByText("Đã hủy")).toBeTruthy();
    expect(screen.queryByRole("button", { name: "Hủy chỗ" })).toBeNull();
    const cancelRequest = fetchMock.mock.calls.find(
      ([input, options]) =>
        new URL(input, "http://localhost").pathname ===
          "/api/v1/bookings/33/cancel" && options.method === "POST",
    );
    expect(JSON.parse(cancelRequest[1].body)).toEqual({});
    await waitFor(() =>
      expect(
        fetchMock.mock.calls.filter(
          ([input]) =>
            new URL(input, "http://localhost").pathname ===
            "/api/v1/parking-lots/1/capacity",
        ).length,
      ).toBeGreaterThan(1),
    );
    expect(model.reserved).toBe(0);
  });
});
