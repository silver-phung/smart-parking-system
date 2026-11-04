import { describe, expect, it } from "vitest";
import { createDemoSlots, DEMO_PARKING_LOTS, inferFloorContext } from "./parkingProfiles.js";

describe("parking 3D profiles", () => {
  it("keeps the approved total capacity for every demonstration lot", () => {
    expect(DEMO_PARKING_LOTS.map((lot) => lot.floors.reduce((sum, floor) => sum + floor.slotCount, 0))).toEqual([64, 82, 112]);
  });

  it("reserves B1 for monthly parking and lower floors for visitors", () => {
    DEMO_PARKING_LOTS.forEach((lot) => {
      expect(lot.floors[0].parkingType).toBe("MONTHLY");
      expect(lot.floors.slice(1).every((floor) => floor.parkingType === "VISITOR")).toBe(true);
    });
  });

  it("creates one valid slot record for every configured position", () => {
    const floor = DEMO_PARKING_LOTS[0].floors[0];
    const slots = createDemoSlots(floor);
    expect(slots).toHaveLength(32);
    expect(slots[0].slotCode).toBe("M1-001");
    expect(slots.at(-1).slotCode).toBe("M1-032");
  });

  it("infers an API floor context from its B-number", () => {
    expect(inferFloorContext({ name: "Basement B3" }, "Tầng B3")).toMatchObject({ floorNumber: 3, parkingType: "VISITOR" });
  });
});
