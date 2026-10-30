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

  it("collects every page before checking whether an active session owns a slot", async () => {
    const load = vi.fn(async (page) => ({
      items: [page],
      totalCount: 3,
      totalPages: 3,
    }));
    expect(await collectPages(load)).toEqual([1, 2, 3]);
    expect(load).toHaveBeenCalledTimes(3);
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
