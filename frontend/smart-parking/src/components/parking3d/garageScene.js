import * as THREE from "three";

export const ROW_LETTERS = ["A", "B", "C", "D"];
export const ROW_Z = [-20, -7, 7, 20];
export const AISLE_Z = [-13.5, 0, 13.5];
export const INTERNAL_ROLES = new Set(["ADMIN", "MANAGER", "STAFF"]);

export const PARKING_LOTS = [
  {
    id: "fpt-q9",
    name: "Hầm ngầm FPT Q9 · 2 tầng",
    shortName: "FPT Q9",
    kind: "UNDERGROUND",
    floors: ["B1", "B2"],
    capacities: { B1: 32, B2: 32 },
    totalSlots: 64,
  },
  {
    id: "landmark-81",
    name: "Vincom Landmark 81 · 3 tầng hầm",
    shortName: "Landmark 81",
    kind: "UNDERGROUND",
    floors: ["B1", "B2", "B3"],
    capacities: { B1: 28, B2: 27, B3: 27 },
    totalSlots: 82,
  },
  {
    id: "aeon-mega-hub",
    name: "Aeon Mall Mega Hub · 4 tầng hầm",
    shortName: "Aeon Mega Hub",
    kind: "UNDERGROUND",
    floors: ["B1", "B2", "B3", "B4"],
    capacities: { B1: 28, B2: 28, B3: 28, B4: 28 },
    totalSlots: 112,
  },
  {
    id: "vinhomes-tower",
    name: "Vinhomes Central Park · 2 tầng nổi",
    shortName: "Vinhomes Tower",
    kind: "HIGH_RISE",
    floors: ["T1", "T2"],
    capacities: { T1: 100, T2: 100 },
    totalSlots: 200,
  },
  {
    id: "saigon-centre-tower",
    name: "Saigon Centre Parking · 3 tầng nổi",
    shortName: "Saigon Centre",
    kind: "HIGH_RISE",
    floors: ["T1", "T2", "T3"],
    capacities: { T1: 100, T2: 100, T3: 100 },
    totalSlots: 300,
  },
];

const PALETTE = {
  concrete: 0x303943,
  concreteLight: 0x6f7b86,
  asphalt: 0x161d24,
  line: 0xe9f2f8,
  monthly: 0x15b89a,
  visitor: 0x2f8de4,
  inactive: 0x4a5560,
  occupied: 0xe14c58,
  reserved: 0xf4b942,
  maintenance: 0xf58a36,
};

function distributeCapacity(capacity, eligibleRows) {
  const quotas = [0, 0, 0, 0];
  eligibleRows.forEach((row, index) => {
    quotas[row] = Math.floor(capacity / eligibleRows.length) +
      (index < capacity % eligibleRows.length ? 1 : 0);
  });
  return quotas;
}

export function createFloorModel(lot, floorId) {
  const floorIndex = lot.floors.indexOf(floorId);
  const capacity = lot.capacities[floorId] ?? 100;
  const isUndergroundB1 = lot.kind === "UNDERGROUND" && floorId === "B1";
  const eligibleRows = isUndergroundB1 ? [2, 3] : [0, 1, 2, 3];
  const quotas = distributeCapacity(capacity, eligibleRows);
  const slots = [];

  ROW_LETTERS.forEach((rowLetter, rowIndex) => {
    for (let column = 1; column <= 25; column += 1) {
      const active = column <= quotas[rowIndex];
      const key = floorIndex * 100 + rowIndex * 25 + column;
      const zone = isUndergroundB1
        ? "MONTHLY"
        : lot.kind === "HIGH_RISE"
          ? (rowIndex >= 2 ? "MONTHLY" : "VISITOR")
          : "VISITOR";
      let status = "UNAVAILABLE";
      if (active) {
        status = key % 23 === 0
          ? "MAINTENANCE"
          : key % 17 === 0
            ? "RESERVED"
            : key % 6 === 0
              ? "OCCUPIED"
              : "AVAILABLE";
      }
      slots.push({
        code: `${floorId}-${rowLetter}${String(column).padStart(2, "0")}`,
        floorId,
        row: rowLetter,
        rowIndex,
        column,
        active,
        zone,
        status,
        x: -48 + (column - 1) * 4,
        z: ROW_Z[rowIndex],
        plate: status === "OCCUPIED" ? `51${rowLetter}-${String(12000 + key).slice(-5)}` : null,
        vehicleModel: status === "OCCUPIED" ? ["VinFast VF 8", "Toyota Vios", "Kia Seltos", "Mazda CX-5"][key % 4] : null,
      });
    }
  });

  const counts = slots.reduce((result, slot) => {
    if (slot.active) result.configured += 1;
    if (slot.status === "AVAILABLE") result.available += 1;
    if (slot.status === "OCCUPIED") result.occupied += 1;
    if (slot.status === "RESERVED") result.reserved += 1;
    if (slot.status === "MAINTENANCE") result.maintenance += 1;
    return result;
  }, { physical: 100, configured: 0, available: 0, occupied: 0, reserved: 0, maintenance: 0 });

  return {
    lot,
    floorId,
    floorIndex,
    isTopFloor: lot.kind === "UNDERGROUND" ? floorIndex === 0 : floorIndex === lot.floors.length - 1,
    isBottomFloor: lot.kind === "UNDERGROUND" ? floorIndex === lot.floors.length - 1 : floorIndex === 0,
    isUndergroundB1,
    slots,
    counts,
  };
}

function material(color, options = {}) {
  return new THREE.MeshStandardMaterial({ color, roughness: 0.78, metalness: 0.08, ...options });
}

function box(parent, size, position, meshMaterial, options = {}) {
  const mesh = new THREE.Mesh(new THREE.BoxGeometry(...size), meshMaterial);
  mesh.position.set(...position);
  if (options.rotation) mesh.rotation.set(...options.rotation);
  mesh.castShadow = options.castShadow ?? true;
  mesh.receiveShadow = options.receiveShadow ?? true;
  if (options.name) mesh.name = options.name;
  parent.add(mesh);
  return mesh;
}

function cylinder(parent, radius, height, position, meshMaterial, rotation = [0, 0, 0], segments = 20) {
  const mesh = new THREE.Mesh(new THREE.CylinderGeometry(radius, radius, height, segments), meshMaterial);
  mesh.position.set(...position);
  mesh.rotation.set(...rotation);
  mesh.castShadow = true;
  parent.add(mesh);
  return mesh;
}

export function createTextSprite(text, color = "#e8f4ff", background = "rgba(5,16,28,.88)", width = 512, height = 128) {
  const canvas = document.createElement("canvas");
  canvas.width = width;
  canvas.height = height;
  const context = canvas.getContext("2d");
  context.fillStyle = background;
  context.fillRect(0, 0, width, height);
  context.strokeStyle = color;
  context.lineWidth = 5;
  context.strokeRect(4, 4, width - 8, height - 8);
  context.fillStyle = color;
  context.font = `700 ${Math.round(height * 0.34)}px "Arial", sans-serif`;
  context.textAlign = "center";
  context.textBaseline = "middle";
  context.fillText(text, width / 2, height / 2);
  const texture = new THREE.CanvasTexture(canvas);
  texture.colorSpace = THREE.SRGBColorSpace;
  const sprite = new THREE.Sprite(new THREE.SpriteMaterial({ map: texture, transparent: true, depthWrite: false }));
  sprite.scale.set(7.4, 1.85, 1);
  return sprite;
}

function addFloorLabel(parent, text, x, z, color = "#dff6ff") {
  const canvas = document.createElement("canvas");
  canvas.width = 768;
  canvas.height = 160;
  const context = canvas.getContext("2d");
  context.fillStyle = "rgba(6,20,34,.82)";
  context.fillRect(0, 0, canvas.width, canvas.height);
  context.strokeStyle = color;
  context.lineWidth = 6;
  context.strokeRect(4, 4, canvas.width - 8, canvas.height - 8);
  context.fillStyle = color;
  context.font = "700 54px Arial, sans-serif";
  context.textAlign = "center";
  context.textBaseline = "middle";
  context.fillText(text, canvas.width / 2, canvas.height / 2);
  const texture = new THREE.CanvasTexture(canvas);
  texture.colorSpace = THREE.SRGBColorSpace;
  const plane = new THREE.Mesh(
    new THREE.PlaneGeometry(12, 2.5),
    new THREE.MeshBasicMaterial({ map: texture, transparent: true, side: THREE.DoubleSide }),
  );
  plane.position.set(x, 0.09, z);
  plane.rotation.x = -Math.PI / 2;
  parent.add(plane);
  return plane;
}

function createCar(slot, showInternalDetails) {
  const car = new THREE.Group();
  const colors = [0xdfe7ee, 0x2f78c4, 0xb92f3a, 0x252d35, 0x8c9a6a];
  const paint = material(colors[(slot.rowIndex * 25 + slot.column) % colors.length], { roughness: 0.34, metalness: 0.58 });
  box(car, [2.05, 0.58, 4.35], [0, 0.62, 0], paint);
  box(car, [1.72, 0.62, 2.15], [0, 1.18, 0.15], material(0x152536, { roughness: 0.16, metalness: 0.65, transparent: true, opacity: 0.9 }));
  const tireMaterial = material(0x070a0d, { roughness: 0.95 });
  const rimMaterial = material(0xb9c5ce, { roughness: 0.25, metalness: 0.8 });
  [[-1.05, -1.35], [1.05, -1.35], [-1.05, 1.35], [1.05, 1.35]].forEach(([x, z]) => {
    const wheel = cylinder(car, 0.34, 0.25, [x, 0.38, z], tireMaterial, [0, 0, Math.PI / 2], 18);
    cylinder(wheel, 0.18, 0.27, [0, 0, 0], rimMaterial, [0, 0, 0], 12);
  });
  box(car, [0.36, 0.15, 0.08], [-0.65, 0.66, 2.2], material(0xf7fbff, { emissive: 0xffffff, emissiveIntensity: 0.4 }));
  box(car, [0.36, 0.15, 0.08], [0.65, 0.66, 2.2], material(0xf7fbff, { emissive: 0xffffff, emissiveIntensity: 0.4 }));
  box(car, [0.4, 0.16, 0.08], [-0.65, 0.66, -2.2], material(0xe53b48, { emissive: 0xa30810, emissiveIntensity: 0.5 }));
  box(car, [0.4, 0.16, 0.08], [0.65, 0.66, -2.2], material(0xe53b48, { emissive: 0xa30810, emissiveIntensity: 0.5 }));
  if (showInternalDetails) {
    const plate = createTextSprite(slot.plate, "#111827", "rgba(255,255,255,.96)", 300, 90);
    plate.position.set(0, 0.58, 2.28);
    plate.scale.set(1.25, 0.38, 1);
    plate.visible = false;
    plate.userData.isVehicleDetail = true;
    car.add(plate);
  }
  car.userData.vehicle = slot;
  return car;
}

function createBarrier(parent, x, z, rotationY, label) {
  const group = new THREE.Group();
  group.position.set(x, 0, z);
  group.rotation.y = rotationY;
  const orange = material(0xf97316, { roughness: 0.4, metalness: 0.28 });
  box(group, [0.75, 1.45, 0.85], [0, 0.73, 0], orange);
  const arm = new THREE.Group();
  arm.position.set(0.42, 1.22, 0);
  for (let index = 0; index < 8; index += 1) {
    box(arm, [0.65, 0.15, 0.17], [0.34 + index * 0.64, 0, 0], material(index % 2 ? 0xd92736 : 0xf8fafc));
  }
  group.add(arm);
  const sign = createTextSprite(label, "#ffffff", "rgba(14,24,34,.94)", 440, 110);
  sign.position.set(0, 2.35, 0);
  sign.scale.set(4.4, 1.1, 1);
  group.add(sign);
  parent.add(group);
  return group;
}

function createBooth(parent, x, z, title, color = "#38bdf8") {
  const booth = new THREE.Group();
  booth.position.set(x, 0, z);
  box(booth, [3.6, 2.8, 3.6], [0, 1.4, 0], material(0x253442, { roughness: 0.62 }));
  box(booth, [2.8, 1.2, 0.1], [0, 1.65, -1.84], material(0x7dd3fc, { transparent: true, opacity: 0.45, metalness: 0.62 }));
  box(booth, [0.1, 1.2, 2.8], [1.84, 1.65, 0], material(0x7dd3fc, { transparent: true, opacity: 0.45, metalness: 0.62 }));
  const sign = createTextSprite(title, color, "rgba(5,20,32,.96)", 500, 120);
  sign.position.set(0, 3.35, 0);
  sign.scale.set(5.2, 1.25, 1);
  booth.add(sign);
  parent.add(booth);
  return booth;
}

function createRamp(parent, { x, z, label, direction = "DOWN", width = 5.2, rotationY = 0 }) {
  const group = new THREE.Group();
  group.position.set(x, 0, z);
  group.rotation.y = rotationY;
  const angle = direction === "DOWN" ? 0.19 : -0.19;
  const surface = box(group, [18, 0.42, width], [0, 1.8, 0], material(0x303a44), { rotation: [0, 0, angle] });
  surface.userData.ramp = direction;
  [-width / 2 - 0.2, width / 2 + 0.2].forEach((railZ) => {
    box(group, [18, 0.35, 0.18], [0, 2.28, railZ], material(0xf4c430), { rotation: [0, 0, angle] });
  });
  const sign = createTextSprite(label, "#facc15", "rgba(9,20,30,.92)", 460, 110);
  sign.position.set(-2, 4.6, 0);
  sign.scale.set(5.4, 1.3, 1);
  group.add(sign);
  parent.add(group);
  return group;
}

function createElevatorRoom(parent, x, z, height, label) {
  const room = new THREE.Group();
  room.position.set(x, 0, z);
  const frameMaterial = material(0x18232e, { metalness: 0.65, roughness: 0.28 });
  const glassMaterial = material(0x9bdcf7, { transparent: true, opacity: 0.24, metalness: 0.5, roughness: 0.08, side: THREE.DoubleSide });
  box(room, [8, height - 0.45, 0.35], [0, (height - 0.45) / 2, 2.9], material(PALETTE.concrete));
  box(room, [0.28, height - 0.8, 5.8], [-4, (height - 0.8) / 2, 0], frameMaterial);
  box(room, [0.28, height - 0.8, 5.8], [4, (height - 0.8) / 2, 0], frameMaterial);
  box(room, [8, height - 0.9, 0.1], [0, (height - 0.9) / 2, -2.9], glassMaterial);
  [-3.8, 0, 3.8].forEach((postX) => box(room, [0.16, height - 0.9, 0.18], [postX, (height - 0.9) / 2, -2.82], frameMaterial));
  box(room, [2.7, 3.1, 0.2], [0, 1.55, 2.7], material(0x111820, { metalness: 0.7, roughness: 0.24 }));
  const floorScreen = createTextSprite(`🛗 ${label}`, "#38bdf8", "rgba(3,10,18,.96)", 320, 100);
  floorScreen.position.set(0, 3.55, 2.55);
  floorScreen.scale.set(2.7, 0.82, 1);
  room.add(floorScreen);
  const anchor = new THREE.Mesh(new THREE.CylinderGeometry(1.05, 1.05, 0.06, 32), new THREE.MeshBasicMaterial({ color: 0x38bdf8, transparent: true, opacity: 0.45 }));
  anchor.position.set(0, 0.07, -4.1);
  anchor.userData.elevatorAnchor = { x, z: z - 4.1 };
  room.add(anchor);
  const ceilingSign = createTextSprite("THANG MÁY →", "#7dd3fc", "rgba(4,18,30,.92)", 420, 100);
  ceilingSign.position.set(-1.2, height - 0.75, -5.6);
  ceilingSign.scale.set(4.6, 1.1, 1);
  room.add(ceilingSign);
  parent.add(room);
  return { room, anchor };
}

function createIoTBoard(parent, x, z, showInternalDetails, plate) {
  const group = new THREE.Group();
  group.position.set(x, 0, z);
  box(group, [0.22, 2.7, 0.22], [0, 1.35, 0], material(0x273441, { metalness: 0.72 }));
  box(group, [4.3, 1.55, 0.24], [0, 2.85, 0], material(0x07131e, { emissive: 0x043b2f, emissiveIntensity: 0.7 }));
  const text = createTextSprite(showInternalDetails && plate ? `IOT · ${plate}` : "IOT · ĐÃ XÁC THỰC", "#4ade80", "rgba(2,12,18,.96)", 520, 120);
  text.position.set(0, 2.85, 0.16);
  text.scale.set(4.1, 0.95, 1);
  text.visible = false;
  text.userData.isVehicleDetail = true;
  group.add(text);
  parent.add(group);
  return { group, detail: text };
}

function createNavigationHotspots(parent) {
  const hotspots = [];
  AISLE_Z.forEach((z) => {
    [-46, -30, -14, 2, 18, 34, 48].forEach((x) => {
      const marker = new THREE.Mesh(
        new THREE.RingGeometry(0.55, 0.92, 28),
        new THREE.MeshBasicMaterial({ color: 0x38bdf8, transparent: true, opacity: 0.72, side: THREE.DoubleSide }),
      );
      marker.rotation.x = -Math.PI / 2;
      marker.position.set(x, 0.08, z);
      marker.userData.hotspot = { x, z };
      parent.add(marker);
      hotspots.push(marker);
    });
  });
  return hotspots;
}

function addDirectionalArrow(parent, x, z, reverse = false) {
  const group = new THREE.Group();
  const white = new THREE.MeshBasicMaterial({ color: 0xf8fafc, transparent: true, opacity: 0.86, side: THREE.DoubleSide });
  const stem = new THREE.Mesh(new THREE.PlaneGeometry(3.2, 0.34), white);
  stem.rotation.x = -Math.PI / 2;
  stem.rotation.z = reverse ? Math.PI : 0;
  const head = new THREE.Mesh(new THREE.ConeGeometry(0.72, 1.5, 3), white);
  head.rotation.x = -Math.PI / 2;
  head.rotation.z = reverse ? Math.PI / 2 : -Math.PI / 2;
  head.position.x = reverse ? -2.15 : 2.15;
  group.add(stem, head);
  group.position.set(x, 0.09, z);
  parent.add(group);
}

function addSlot(parent, slot, showInternalDetails, vehicleMeshes) {
  const group = new THREE.Group();
  group.position.set(slot.x, 0, slot.z);
  const zoneColor = slot.zone === "MONTHLY" ? PALETTE.monthly : PALETTE.visitor;
  const padColor = !slot.active
    ? PALETTE.inactive
    : slot.status === "MAINTENANCE"
      ? PALETTE.maintenance
      : slot.status === "RESERVED"
        ? PALETTE.reserved
        : zoneColor;
  const pad = new THREE.Mesh(
    new THREE.PlaneGeometry(3.15, 5.3),
    new THREE.MeshStandardMaterial({ color: padColor, roughness: 0.9, transparent: true, opacity: slot.active ? 0.26 : 0.12, side: THREE.DoubleSide }),
  );
  pad.rotation.x = -Math.PI / 2;
  pad.position.y = 0.025;
  group.add(pad);
  const lineMaterial = material(slot.active ? PALETTE.line : 0x66717a, { emissive: slot.active ? 0x0a1822 : 0x000000, emissiveIntensity: 0.25 });
  box(group, [0.08, 0.04, 5.4], [-1.62, 0.04, 0], lineMaterial, { castShadow: false });
  box(group, [0.08, 0.04, 5.4], [1.62, 0.04, 0], lineMaterial, { castShadow: false });
  box(group, [2.25, 0.2, 0.32], [0, 0.12, slot.rowIndex < 2 ? -2.2 : 2.2], material(0x111820));
  box(group, [0.4, 0.22, 0.34], [-0.62, 0.14, slot.rowIndex < 2 ? -2.2 : 2.2], material(0xfacc15));
  box(group, [0.4, 0.22, 0.34], [0.62, 0.14, slot.rowIndex < 2 ? -2.2 : 2.2], material(0xfacc15));
  const code = createTextSprite(slot.code, slot.active ? "#e8f4ff" : "#8a98a5", "rgba(4,16,26,.78)", 340, 86);
  code.position.set(0, 0.18, slot.rowIndex < 2 ? 1.85 : -1.85);
  code.scale.set(2.15, 0.54, 1);
  group.add(code);
  if (slot.status === "OCCUPIED") {
    const car = createCar(slot, showInternalDetails);
    car.rotation.y = slot.rowIndex < 2 ? Math.PI : 0;
    group.add(car);
    vehicleMeshes.push(car);
  }
  if (slot.status === "MAINTENANCE") {
    const cone = new THREE.Group();
    cylinder(cone, 0.45, 0.12, [0, 0.06, 0], material(0xffffff));
    const coneBody = new THREE.Mesh(new THREE.ConeGeometry(0.32, 1.05, 20), material(0xf97316));
    coneBody.position.y = 0.62;
    cone.add(coneBody);
    group.add(cone);
  }
  if (!slot.active) {
    const crossMaterial = material(0x7b8792, { transparent: true, opacity: 0.48 });
    box(group, [0.13, 0.04, 4.5], [0, 0.05, 0], crossMaterial, { rotation: [0, Math.PI / 5, 0], castShadow: false });
    box(group, [0.13, 0.04, 4.5], [0, 0.05, 0], crossMaterial, { rotation: [0, -Math.PI / 5, 0], castShadow: false });
  }
  parent.add(group);
}

function addInfrastructure(parent, height, underground) {
  const concrete = material(PALETTE.concrete);
  [-50, -30, -10, 10, 30, 50].forEach((x) => {
    [-10, 10].forEach((z) => {
      box(parent, [1.2, height, 1.2], [x, height / 2, z], concrete);
      box(parent, [1.32, 1.25, 1.32], [x, 1.15, z], material(0x1c2128));
      for (let band = 0; band < 5; band += 1) {
        box(parent, [1.35, 0.16, 1.35], [x, 0.65 + band * 0.22, z], material(band % 2 ? 0x111111 : 0xfacc15));
      }
    });
  });
  if (!underground) return;
  [-18, 0, 18].forEach((z) => box(parent, [112, 0.55, 0.8], [0, height - 0.35, z], concrete));
  [-15, 0, 15].forEach((z) => cylinder(parent, 0.34, 108, [0, height - 0.9, z], material(0x8e9ca8, { metalness: 0.72, roughness: 0.32 }), [0, 0, Math.PI / 2], 18));
  [-10, 10].forEach((z) => cylinder(parent, 0.12, 108, [0, height - 0.45, z], material(0xc8202c, { metalness: 0.42 }), [0, 0, Math.PI / 2], 14));
  [-44, -22, 0, 22, 44].forEach((x) => {
    [-15.5, -2.5, 10.5, 23].forEach((z) => {
      box(parent, [5.2, 0.08, 0.45], [x, height - 0.18, z], material(0xf8fafc, { emissive: 0xffffff, emissiveIntensity: 1.1 }), { castShadow: false });
      const light = new THREE.PointLight(0xd7efff, 0.5, 15, 2);
      light.position.set(x, height - 0.35, z);
      parent.add(light);
    });
  });
}

function buildTrafficFlow(parent, model, height, showInternalDetails, detailObjects) {
  const { floorId, lot, isTopFloor, isBottomFloor, isUndergroundB1 } = model;
  if (lot.kind === "UNDERGROUND") {
    if (isUndergroundB1) {
      box(parent, [78, 3.1, 0.35], [2, 1.55, 2.2], material(0x4b5660));
      createBarrier(parent, -34, 0.4, Math.PI / 2, "KHU GỬI THÁNG");
      createBooth(parent, -39, 6.1, "BOOT CHECK-IN THẺ THÁNG", "#4ade80");
      const occupiedMonthly = model.slots.find((slot) => slot.zone === "MONTHLY" && slot.plate)?.plate;
      const board = createIoTBoard(parent, -30, 5.3, showInternalDetails, occupiedMonthly);
      detailObjects.push(board.detail);
      createRamp(parent, { x: -46, z: -13.5, label: "TỪ MẶT ĐẤT XUỐNG B1", direction: "DOWN" });
      if (!isBottomFloor) createRamp(parent, { x: -24, z: -13.5, label: "VÃNG LAI ↓ B2", direction: "DOWN" });
      createBooth(parent, 34, 9.4, "CHECK-OUT THẺ THÁNG", "#facc15");
      createRamp(parent, { x: 46, z: 13.5, label: "2 LÀN LÊN MẶT ĐẤT", direction: "UP", width: 8.6 });
      addFloorLabel(parent, "← GỬI THÁNG / VÃNG LAI ĐI THẲNG ↓ B2", -32, -8.2, "#facc15");
      const monthlySign = createTextSprite("KHU ĐỖ XE THÁNG", "#4ade80", "rgba(4,25,28,.92)", 520, 120);
      monthlySign.position.set(-5, height - 0.9, 3.2);
      monthlySign.scale.set(6.2, 1.45, 1);
      parent.add(monthlySign);
    } else {
      createRamp(parent, { x: 46, z: 13.5, label: `LÊN ${lot.floors[model.floorIndex - 1]}`, direction: "UP" });
      if (!isBottomFloor) createRamp(parent, { x: 46, z: -13.5, label: `XUỐNG ${lot.floors[model.floorIndex + 1]}`, direction: "DOWN" });
      if (floorId === "B2") {
        createBooth(parent, 32, 9.4, "CHECK-OUT KHÁCH VÃNG LAI", "#facc15");
        createBarrier(parent, 38, 13.5, 0, "CHECK-OUT");
      }
      addFloorLabel(parent, isBottomFloor ? "HẾT CHỖ → QUAY LẠI DỐC LÊN" : `HẾT CHỖ → XUỐNG ${lot.floors[model.floorIndex + 1]}`, 34, -8.5, "#facc15");
    }
  } else {
    const floorAbove = lot.floors[model.floorIndex + 1];
    const floorBelow = lot.floors[model.floorIndex - 1];
    if (floorAbove) createRamp(parent, { x: 44, z: -13.5, label: `LÊN ${floorAbove}`, direction: "UP" });
    if (floorBelow) createRamp(parent, { x: 44, z: 13.5, label: `XUỐNG ${floorBelow}`, direction: "DOWN" });
  }
  if (isTopFloor) {
    createBarrier(parent, -52, -13.5, Math.PI / 2, "LỐI VÀO");
    createBarrier(parent, 52, 13.5, -Math.PI / 2, "LỐI RA");
    const exitSign = createTextSprite("LỐI RA / EXIT →", "#4ade80", "rgba(3,22,20,.94)", 520, 120);
    exitSign.position.set(30, height - 0.8, 13.5);
    exitSign.scale.set(6, 1.4, 1);
    parent.add(exitSign);
  }
}

export function buildGarageScene(scene, model, actor = "GUEST") {
  const root = new THREE.Group();
  root.name = "parking-garage-root";
  const underground = model.lot.kind === "UNDERGROUND";
  const height = underground ? 7.2 : 8.1;
  const showInternalDetails = INTERNAL_ROLES.has(actor);
  const asphalt = material(PALETTE.asphalt, { roughness: 0.96 });
  const vehicleMeshes = [];
  const detailObjects = [];
  const elevatorAnchors = [];
  let ceiling = null;

  const floor = box(root, [116, 0.55, 55], [0, -0.28, 0], asphalt, { castShadow: false });
  floor.userData.walkSurface = true;
  if (underground) {
    ceiling = box(root, [116, 0.55, 55], [0, height, 0], material(0x232c34), { castShadow: false });
    box(root, [116, height, 0.55], [0, height / 2, -27.2], material(PALETTE.concrete));
    box(root, [116, height, 0.55], [0, height / 2, 27.2], material(PALETTE.concrete));
    box(root, [0.55, height, 55], [-57.7, height / 2, 0], material(PALETTE.concrete));
    box(root, [0.55, height, 55], [57.7, height / 2, 0], material(PALETTE.concrete));
    scene.background = new THREE.Color(0x071019);
    scene.fog = new THREE.FogExp2(0x071019, 0.0055);
  } else {
    scene.background = new THREE.Color(0x9fc1d9);
    scene.fog = new THREE.FogExp2(0x9fc1d9, 0.0024);
    [-27, 27].forEach((z) => {
      box(root, [116, 1.2, 0.3], [0, 1.1, z], material(0x465867, { metalness: 0.52 }));
      for (let x = -56; x <= 56; x += 8) box(root, [0.18, 2.5, 0.18], [x, 1.25, z], material(0xb7c8d4, { metalness: 0.76 }));
    });
    ceiling = box(root, [116, 0.45, 55], [0, height, 0], material(0x8f9ca8, { transparent: true, opacity: 0.24 }), { castShadow: false });
  }

  addInfrastructure(root, height, underground);
  model.slots.forEach((slot) => addSlot(root, slot, showInternalDetails, vehicleMeshes));
  AISLE_Z.forEach((z, index) => {
    addDirectionalArrow(root, -22, z, index % 2 === 1);
    addDirectionalArrow(root, 14, z, index % 2 === 0);
  });
  buildTrafficFlow(root, model, height, showInternalDetails, detailObjects);

  const elevatorPositions = underground
    ? [[-45, 23.6], [0, 23.6], [45, 23.6]]
    : [[-44, 23.2], [0, 23.2], [44, 23.2]];
  elevatorPositions.forEach(([x, z]) => {
    const elevator = createElevatorRoom(root, x, z, height, model.floorId);
    elevatorAnchors.push(elevator.anchor);
  });

  const hotspots = createNavigationHotspots(root);
  scene.add(root);
  return { root, height, hotspots, elevatorAnchors, vehicleMeshes, detailObjects, floor, ceiling };
}

export function disposeGarageScene(sceneResult) {
  if (!sceneResult?.root) return;
  sceneResult.root.traverse((object) => {
    object.geometry?.dispose?.();
    if (Array.isArray(object.material)) object.material.forEach((item) => {
      item.map?.dispose?.();
      item.dispose?.();
    });
    else {
      object.material?.map?.dispose?.();
      object.material?.dispose?.();
    }
  });
  sceneResult.root.parent?.remove(sceneResult.root);
}
