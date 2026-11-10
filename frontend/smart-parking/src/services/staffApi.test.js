import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { clearAuthSession, setAuthSession } from "./authSession.js";
import { collectPages, slotIsSelectable, staffApi } from "./staffApi.js";

const session = {
  id: 10,
  lotId: 1,
  plateNumber: "51F-123.45",
  status: "ACTIVE",
  currentSlotId: null,
};
const slot = {
  id: 101,
  lotId: 1,
  physicalStatus: "EMPTY",
  isActive: true,
  isAvailable: true,
};
const ok = (data) => Response.json({ data });

beforeEach(() => setAuthSession({ accessToken: "staff-test" }));
afterEach(() => {
  clearAuthSession();
  vi.unstubAllGlobals();
});

describe("staff operation boundaries", () => {
  it("uploads real multipart data with bearer auth and lets the browser set its boundary", async () => {
    const fetch = vi.fn(async () =>
      ok({ url: "https://images.example.invalid/entry.jpg" }),
    );
    vi.stubGlobal("fetch", fetch);
    const photo = new File([new Uint8Array([1, 2])], "car.jpg", {
      type: "image/jpeg",
    });

    await staffApi.upload(photo);
    const [url, options] = fetch.mock.calls[0];
    expect(url).toBe("/api/v1/images");
    expect(options.headers.Authorization).toBe("Bearer staff-test");
    expect(options.headers["Content-Type"]).toBeUndefined();
    expect(options.body.get("file")).toBe(photo);
  });

  it("accepts a database image URL returned after upload", async () => {
    const url = "/api/v1/images/content/0123456789abcdef0123456789abcdef";
    vi.stubGlobal("fetch", vi.fn(async () => ok({ url })));
    const photo = new File(["photo"], "car.jpg", { type: "image/jpeg" });
    expect(await staffApi.upload(photo)).toBe(url);
  });

  it("collects every page before checking whether an active session owns a slot", async () => {
    const load = vi.fn(async (page) => ({
      items: [page],
      totalCount: 3,
      totalPages: 3,
    }));
    expect(await collectPages(load)).toEqual([1, 2, 3]);
    expect(load).toHaveBeenCalledTimes(3);
  });

  it("loads every page of pending bookings for the selected assigned lot", async () => {
    const fetch = vi.fn(async (url) => {
      const pageNumber = Number(new URL(url, "http://localhost").searchParams.get("pageNumber"));
      return ok({
        items: [{ id: 500 + pageNumber, lotId: 1, plateNumber: `51A-888.8${pageNumber}` }],
        totalCount: 2,
        totalPages: 2,
        pageNumber,
        pageSize: 100,
      });
    });
    vi.stubGlobal("fetch", fetch);

    const result = await staffApi.pendingBookings(1);

    expect(result.items.map((booking) => booking.plateNumber)).toEqual([
      "51A-888.81",
      "51A-888.82",
    ]);
    expect(fetch.mock.calls.map(([url]) => url)).toEqual([
      "/api/v1/bookings?lotId=1&status=PENDING&pageNumber=1&pageSize=100",
      "/api/v1/bookings?lotId=1&status=PENDING&pageNumber=2&pageSize=100",
    ]);
  });

  it("confirms a pending booking at the actual parked slot with its captured photo", async () => {
    const fetch = vi.fn(async () => ok({ id: 20, bookingId: 501 }));
    vi.stubGlobal("fetch", fetch);

    await staffApi.confirmBookingParked(
      501,
      "51A-888.88",
      101,
      "https://images.example.invalid/entry.jpg",
    );

    expect(fetch.mock.calls[0][0]).toBe(
      "/api/v1/parking-sessions/bookings/501/confirm-parked",
    );
    expect(JSON.parse(fetch.mock.calls[0][1].body)).toEqual({
      plateNumber: "51A-888.88",
      slotId: 101,
      checkInImageUrl: "https://images.example.invalid/entry.jpg",
    });
  });

  it("excludes a slot held by another open session even if its sensor says EMPTY", () => {
    const other = { ...session, id: 11, currentSlotId: slot.id };
    expect(slotIsSelectable(slot, session, [session, other], "assign")).toBe(
      false,
    );
    expect(slotIsSelectable(slot, session, [session, other], "verify")).toBe(
      false,
    );
    expect(
      slotIsSelectable({ ...slot, lotId: 2 }, session, [session], "verify"),
    ).toBe(false);
    expect(
      slotIsSelectable({ ...slot, physicalStatus: "MAINTENANCE" }, session, [
        session,
      ]),
    ).toBe(false);
    expect(
      slotIsSelectable(
        { ...slot, physicalStatus: "OCCUPIED" },
        session,
        [session],
        "verify",
      ),
    ).toBe(true);
  });

  it("rechecks slot ownership immediately before a write and rejects stale selections", async () => {
    const fetch = vi.fn(async (url) => {
      if (url.includes("/parking-sessions/10")) return ok(session);
      if (url.includes("/parking-slots/101")) return ok(slot);
      return ok({
        items: [session, { ...session, id: 11, currentSlotId: 101 }],
        totalCount: 2,
        totalPages: 1,
      });
    });
    vi.stubGlobal("fetch", fetch);
    await expect(
      staffApi.slotAction({ mode: "assign", session, slotId: 101, notes: "" }),
    ).rejects.toThrow(/xe khác/);
    expect(
      fetch.mock.calls.every(([, options]) => options.method !== "POST"),
    ).toBe(true);
  });

  it("does not change slot assignment if the session has since been paid", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn(async (url) =>
        url.includes("/parking-slots/")
          ? ok(slot)
          : url.includes("/active?")
            ? ok({ items: [], totalPages: 1 })
            : ok({ ...session, status: "PAID" }),
      ),
    );
    await expect(
      staffApi.slotAction({ mode: "assign", session, slotId: 101, notes: "" }),
    ).rejects.toThrow(/đã thay đổi/);
  });

  it("rechecks the observed plate against the current session before verification", async () => {
    const fetch = vi.fn(async (url) =>
      url.includes("/parking-slots/")
        ? ok(slot)
        : url.includes("/active?")
          ? ok({ items: [session], totalPages: 1 })
          : ok({ ...session, plateNumber: "51F-99999" }),
    );
    vi.stubGlobal("fetch", fetch);
    await expect(
      staffApi.slotAction({
        mode: "verify",
        session,
        slotId: 101,
        notes: "",
        observedPlate: "51F12345",
      }),
    ).rejects.toThrow(/Biển số thực tế không khớp/);
    expect(fetch.mock.calls.every(([, options]) => options.method !== "POST")).toBe(true);
  });

  it("refuses unpaid exit without sending a checkout request", async () => {
    const fetch = vi.fn(async () => ok(session));
    vi.stubGlobal("fetch", fetch);
    await expect(staffApi.checkOut(10, 1, "51F12345")).rejects.toThrow(
      /thanh toán/,
    );
    expect(fetch).toHaveBeenCalledTimes(1);
  });

  it("refuses an exit plate mismatch even for a paid session", async () => {
    const fetch = vi.fn(async () => ok({ ...session, status: "PAID" }));
    vi.stubGlobal("fetch", fetch);
    await expect(staffApi.checkOut(10, 1, "51F99999")).rejects.toThrow(
      /không khớp/,
    );
    expect(fetch).toHaveBeenCalledTimes(1);
  });

  it("sends the observed exit plate without any payment exemption", async () => {
    const fetch = vi.fn(async () => ok({ ...session, status: "PAID" }));
    vi.stubGlobal("fetch", fetch);
    await staffApi.checkOut(10, 1, "51f 12345");
    const [url, options] = fetch.mock.calls[1];
    expect(url).toBe("/api/v1/parking-sessions/10/check-out");
    expect(JSON.parse(options.body)).toEqual({
      confirmedPlateNumber: "51F 12345",
    });
  });

  it("rejects lookup by ID when the session belongs to a different selected lot", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn(async () => ok({ ...session, lotId: 2 })),
    );
    await expect(staffApi.findSession(1, "10", "id")).rejects.toThrow(
      /bãi đang chọn/,
    );
  });
});
