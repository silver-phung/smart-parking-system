import test from "node:test";
import assert from "node:assert/strict";
import { INTERNAL_ROLES, PARKING_LOTS, createFloorModel } from "./garageScene.js";

test("keeps 100 physical positions while honoring configured lot capacity", () => {
  const fpt = PARKING_LOTS.find((lot) => lot.id === "fpt-q9");
  const b1 = createFloorModel(fpt, "B1");
  const b2 = createFloorModel(fpt, "B2");

  assert.equal(b1.slots.length, 100);
  assert.equal(b2.slots.length, 100);
  assert.equal(b1.counts.configured, 32);
  assert.equal(b2.counts.configured, 32);
  assert.equal(b1.counts.configured + b2.counts.configured, fpt.totalSlots);
});

test("uses floor-row-number codes and reserves B1 configured bays for monthly parking", () => {
  const landmark = PARKING_LOTS.find((lot) => lot.id === "landmark-81");
  const b1 = createFloorModel(landmark, "B1");

  assert.equal(b1.slots[0].code, "B1-A01");
  assert.equal(b1.slots[74].code, "B1-C25");
  assert.equal(b1.slots[99].code, "B1-D25");
  assert.ok(b1.slots.filter((slot) => slot.active).every((slot) => slot.zone === "MONTHLY"));
  assert.ok(b1.slots.filter((slot) => slot.active).every((slot) => ["C", "D"].includes(slot.row)));
});

test("assigns lower underground floors to visitors and preserves 82/112 totals", () => {
  const landmark = PARKING_LOTS.find((lot) => lot.id === "landmark-81");
  const aeon = PARKING_LOTS.find((lot) => lot.id === "aeon-mega-hub");
  const landmarkTotal = landmark.floors.reduce((sum, floor) => sum + createFloorModel(landmark, floor).counts.configured, 0);
  const aeonTotal = aeon.floors.reduce((sum, floor) => sum + createFloorModel(aeon, floor).counts.configured, 0);

  assert.equal(landmarkTotal, 82);
  assert.equal(aeonTotal, 112);
  assert.ok(createFloorModel(aeon, "B3").slots.filter((slot) => slot.active).every((slot) => slot.zone === "VISITOR"));
});

test("keeps sensitive vehicle details limited to operational actors", () => {
  assert.deepEqual([...INTERNAL_ROLES].sort(), ["ADMIN", "MANAGER", "STAFF"]);
  assert.equal(INTERNAL_ROLES.has("GUEST"), false);
  assert.equal(INTERNAL_ROLES.has("USER"), false);
});

test("treats B1 as the underground top and the last T floor as the high-rise top", () => {
  const fpt = PARKING_LOTS.find((lot) => lot.id === "fpt-q9");
  const saigon = PARKING_LOTS.find((lot) => lot.id === "saigon-centre-tower");

  assert.equal(createFloorModel(fpt, "B1").isTopFloor, true);
  assert.equal(createFloorModel(fpt, "B2").isTopFloor, false);
  assert.equal(createFloorModel(saigon, "T1").isBottomFloor, true);
  assert.equal(createFloorModel(saigon, "T3").isTopFloor, true);
});
