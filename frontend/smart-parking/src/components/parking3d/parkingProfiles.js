export const DEMO_PARKING_LOTS = [
  {
    id: "demo-fpt-q9",
    name: "Hầm ngầm FPT Q9",
    isDemo: true,
    floors: [
      { id: "demo-fpt-q9-b1", name: "Tầng B1 · Xe tháng", code: "B1", floorNumber: 1, floorCount: 2, parkingType: "MONTHLY", slotCount: 32 },
      { id: "demo-fpt-q9-b2", name: "Tầng B2 · Vãng lai", code: "B2", floorNumber: 2, floorCount: 2, parkingType: "VISITOR", slotCount: 32 },
    ],
  },
  {
    id: "demo-landmark-81",
    name: "Vincom Landmark 81",
    isDemo: true,
    floors: [
      { id: "demo-landmark-81-b1", name: "Tầng B1 · Xe tháng", code: "B1", floorNumber: 1, floorCount: 3, parkingType: "MONTHLY", slotCount: 28 },
      { id: "demo-landmark-81-b2", name: "Tầng B2 · Vãng lai", code: "B2", floorNumber: 2, floorCount: 3, parkingType: "VISITOR", slotCount: 27 },
      { id: "demo-landmark-81-b3", name: "Tầng B3 · Vãng lai", code: "B3", floorNumber: 3, floorCount: 3, parkingType: "VISITOR", slotCount: 27 },
    ],
  },
  {
    id: "demo-aeon-mega-hub",
    name: "Aeon Mall Mega Hub",
    isDemo: true,
    floors: [
      { id: "demo-aeon-mega-hub-b1", name: "Tầng B1 · Xe tháng", code: "B1", floorNumber: 1, floorCount: 4, parkingType: "MONTHLY", slotCount: 28 },
      { id: "demo-aeon-mega-hub-b2", name: "Tầng B2 · Vãng lai", code: "B2", floorNumber: 2, floorCount: 4, parkingType: "VISITOR", slotCount: 28 },
      { id: "demo-aeon-mega-hub-b3", name: "Tầng B3 · Vãng lai", code: "B3", floorNumber: 3, floorCount: 4, parkingType: "VISITOR", slotCount: 28 },
      { id: "demo-aeon-mega-hub-b4", name: "Tầng B4 · Vãng lai", code: "B4", floorNumber: 4, floorCount: 4, parkingType: "VISITOR", slotCount: 28 },
    ],
  },
];

export function findDemoLot(lotId) {
  return DEMO_PARKING_LOTS.find((lot) => lot.id === lotId);
}

export function findDemoFloor(floorId) {
  for (const lot of DEMO_PARKING_LOTS) {
    const floor = lot.floors.find((item) => item.id === floorId);
    if (floor) return floor;
  }
  return undefined;
}

export function createDemoSlots(floor) {
  const slotCount = floor?.slotCount || 32;
  const zonePrefix = floor?.parkingType === "MONTHLY" ? "M1" : `V${floor?.floorNumber || 2}`;
  return Array.from({ length: slotCount }, (_, index) => {
    const physicalStatus = index % 7 === 1 || index % 11 === 4
      ? "OCCUPIED"
      : index % 19 === 7
        ? "MAINTENANCE"
        : index % 23 === 13
          ? "UNAVAILABLE"
          : "EMPTY";
    return {
      id: `${floor?.id || "demo-floor"}-${index + 1}`,
      slotCode: `${zonePrefix}-${String(index + 1).padStart(3, "0")}`,
      physicalStatus,
      isAvailable: physicalStatus === "EMPTY",
      zone: floor?.parkingType === "MONTHLY" ? "Khu xe tháng" : `Khu vãng lai ${floor?.code || "B2"}`,
    };
  });
}

export function inferFloorContext(floor, floorLabel = "Tầng B1") {
  if (floor?.floorNumber) return floor;
  const matchedNumber = Number(String(floor?.code || floorLabel).match(/B(\d+)/i)?.[1] || 1);
  return {
    ...floor,
    code: floor?.code || `B${matchedNumber}`,
    floorNumber: matchedNumber,
    floorCount: Math.max(matchedNumber, 2),
    parkingType: matchedNumber === 1 ? "MONTHLY" : "VISITOR",
  };
}
