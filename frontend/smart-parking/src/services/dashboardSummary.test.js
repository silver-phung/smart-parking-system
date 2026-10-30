import assert from "node:assert/strict";
import { test } from "node:test";
import { calculateDashboardSummary } from "./dashboardSummary.js";

test("Dashboard Summary: strictly aggregates physical slots for deployed capacity and isolates configured capacity", () => {
  const lots = [
    {
      id: 2,
      name: "Lot 2 (Digitized)",
      totalCapacity: 1,
      physicalSlotsCount: 1,
      configuredCapacity: 10,
      occupiedSlotsCount: 0,
      availableSlotsCount: 1,
      isActive: true,
    },
    {
      id: 4,
      name: "Lot 4 (Unconfigured Test)",
      totalCapacity: 0,
      physicalSlotsCount: 0,
      configuredCapacity: 100000,
      occupiedSlotsCount: 0,
      availableSlotsCount: 0,
      isActive: true,
    },
  ];

  const summary = calculateDashboardSummary(lots);

  // Deployed physical capacity must be 1, NOT 100001
  assert.equal(summary.physicalSlots, 1);
  assert.equal(summary.configuredCapacity, 100010);
  assert.equal(summary.capacity, 1);
  assert.equal(summary.available, 1);
  assert.equal(summary.occupied, 0);
  assert.equal(summary.openLots, 2);
  assert.equal(summary.occupancy, 0);
});

test("Dashboard Summary: deactivating Lot 4 completely excludes it from active totals", () => {
  const lots = [
    {
      id: 2,
      name: "Lot 2 (Digitized)",
      totalCapacity: 1,
      physicalSlotsCount: 1,
      configuredCapacity: 10,
      occupiedSlotsCount: 0,
      availableSlotsCount: 1,
      isActive: true,
    },
    {
      id: 4,
      name: "Lot 4 (Deactivated)",
      totalCapacity: 0,
      physicalSlotsCount: 0,
      configuredCapacity: 100000,
      occupiedSlotsCount: 0,
      availableSlotsCount: 0,
      isActive: false, // Closed
    },
  ];

  const summary = calculateDashboardSummary(lots);

  // Lot 4 is completely excluded
  assert.equal(summary.physicalSlots, 1);
  assert.equal(summary.configuredCapacity, 10);
  assert.equal(summary.capacity, 1);
  assert.equal(summary.available, 1);
  assert.equal(summary.occupied, 0);
  assert.equal(summary.openLots, 1);
});

test("Dashboard Summary: handles empty lots gracefully", () => {
  const summary = calculateDashboardSummary([]);
  assert.equal(summary.physicalSlots, 0);
  assert.equal(summary.configuredCapacity, 0);
  assert.equal(summary.capacity, 0);
  assert.equal(summary.available, 0);
  assert.equal(summary.occupied, 0);
  assert.equal(summary.openLots, 0);
  assert.equal(summary.occupancy, 0);
});
