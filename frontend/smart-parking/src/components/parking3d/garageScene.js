import * as THREE from "three";
import { OrbitControls } from "three/addons/controls/OrbitControls.js";

const WORLD = { width: 104, depth: 72, ceiling: 5.2 };
const STATUS_COLORS = {
  EMPTY: 0x36d399,
  OCCUPIED: 0xef4444,
  MAINTENANCE: 0xf59e0b,
  UNAVAILABLE: 0x64748b,
};
const CAR_COLORS = [0x2563eb, 0xe11d48, 0x0f172a, 0xe2e8f0, 0xf59e0b, 0x0f766e, 0x7c3aed];

function canvasTexture(width, height, paint, repeat) {
  const canvas = document.createElement("canvas");
  canvas.width = width;
  canvas.height = height;
  const context = canvas.getContext("2d");
  paint(context, width, height);
  const texture = new THREE.CanvasTexture(canvas);
  texture.colorSpace = THREE.SRGBColorSpace;
  texture.anisotropy = 8;
  if (repeat) {
    texture.wrapS = texture.wrapT = THREE.RepeatWrapping;
    texture.repeat.set(repeat[0], repeat[1]);
  }
  return texture;
}

function concreteTexture() {
  return canvasTexture(256, 256, (ctx, width, height) => {
    ctx.fillStyle = "#777b7d";
    ctx.fillRect(0, 0, width, height);
    for (let i = 0; i < 6500; i += 1) {
      const shade = 85 + Math.floor(Math.random() * 55);
      ctx.fillStyle = `rgba(${shade},${shade},${shade},${Math.random() * 0.08})`;
      const size = Math.random() * 2.2;
      ctx.fillRect(Math.random() * width, Math.random() * height, size, size);
    }
    ctx.strokeStyle = "rgba(30,34,36,.24)";
    ctx.lineWidth = 2;
    ctx.strokeRect(0, 0, width, height);
    ctx.beginPath();
    ctx.moveTo(width / 2, 0);
    ctx.lineTo(width / 2, height);
    ctx.moveTo(0, height / 2);
    ctx.lineTo(width, height / 2);
    ctx.stroke();
  }, [10, 7]);
}

function asphaltTexture() {
  return canvasTexture(256, 256, (ctx, width, height) => {
    ctx.fillStyle = "#252a2d";
    ctx.fillRect(0, 0, width, height);
    for (let i = 0; i < 9000; i += 1) {
      const value = 40 + Math.floor(Math.random() * 50);
      ctx.fillStyle = `rgba(${value},${value},${value},${0.08 + Math.random() * 0.16})`;
      const radius = Math.random() * 1.2;
      ctx.fillRect(Math.random() * width, Math.random() * height, radius, radius);
    }
  }, [16, 11]);
}

function hazardTexture() {
  return canvasTexture(256, 256, (ctx, width, height) => {
    ctx.fillStyle = "#f5c400";
    ctx.fillRect(0, 0, width, height);
    ctx.strokeStyle = "#171717";
    ctx.lineWidth = 28;
    for (let offset = -height; offset < width + height; offset += 54) {
      ctx.beginPath();
      ctx.moveTo(offset, height);
      ctx.lineTo(offset + height, 0);
      ctx.stroke();
    }
  }, [1.4, 1.4]);
}

function labelTexture(text, options = {}) {
  const { background = "rgba(10,15,19,.88)", color = "#ffffff", accent = "#f5c400" } = options;
  return canvasTexture(512, 180, (ctx, width, height) => {
    ctx.fillStyle = background;
    ctx.roundRect(3, 3, width - 6, height - 6, 22);
    ctx.fill();
    ctx.strokeStyle = accent;
    ctx.lineWidth = 8;
    ctx.stroke();
    ctx.fillStyle = color;
    ctx.font = "700 72px Arial";
    ctx.textAlign = "center";
    ctx.textBaseline = "middle";
    ctx.fillText(text, width / 2, height / 2 + 3);
  });
}

function addBox(parent, size, position, material, name = "") {
  const mesh = new THREE.Mesh(new THREE.BoxGeometry(...size), material);
  mesh.position.set(...position);
  mesh.castShadow = true;
  mesh.receiveShadow = true;
  mesh.name = name;
  parent.add(mesh);
  return mesh;
}

function addCylinder(parent, radius, height, position, material, rotation = [0, 0, 0]) {
  const mesh = new THREE.Mesh(new THREE.CylinderGeometry(radius, radius, height, 18), material);
  mesh.position.set(...position);
  mesh.rotation.set(...rotation);
  mesh.castShadow = true;
  mesh.receiveShadow = true;
  parent.add(mesh);
  return mesh;
}

function addPlane(parent, size, position, material, rotation = [-Math.PI / 2, 0, 0]) {
  const mesh = new THREE.Mesh(new THREE.PlaneGeometry(...size), material);
  mesh.position.set(...position);
  mesh.rotation.set(...rotation);
  mesh.receiveShadow = true;
  parent.add(mesh);
  return mesh;
}

function makeTextSign(text, width = 4.6, height = 1.55, options) {
  const material = new THREE.MeshBasicMaterial({ map: labelTexture(text, options), transparent: true });
  return new THREE.Mesh(new THREE.PlaneGeometry(width, height), material);
}

function createCar(color = CAR_COLORS[0]) {
  const group = new THREE.Group();
  const paint = new THREE.MeshStandardMaterial({ color, roughness: 0.28, metalness: 0.52 });
  const dark = new THREE.MeshStandardMaterial({ color: 0x080b0e, roughness: 0.2, metalness: 0.35 });
  const glass = new THREE.MeshStandardMaterial({ color: 0x172b3a, roughness: 0.12, metalness: 0.6 });
  addBox(group, [2.05, 0.55, 4.25], [0, 0.65, 0], paint);
  const cabin = addBox(group, [1.75, 0.62, 2.15], [0, 1.18, 0.05], glass);
  cabin.geometry.translate(0, 0, 0);
  [-0.92, 0.92].forEach((x) => [-1.35, 1.35].forEach((z) => {
    const wheel = addCylinder(group, 0.36, 0.24, [x, 0.48, z], dark, [0, 0, Math.PI / 2]);
    wheel.castShadow = true;
  }));
  const lamp = new THREE.MeshStandardMaterial({ color: 0xfff5c2, emissive: 0xffd36b, emissiveIntensity: 2.5 });
  [-0.62, 0.62].forEach((x) => addBox(group, [0.3, 0.18, 0.08], [x, 0.7, -2.15], lamp));
  group.scale.setScalar(0.88);
  return group;
}

function createCone() {
  const group = new THREE.Group();
  const orange = new THREE.MeshStandardMaterial({ color: 0xf97316, roughness: 0.65 });
  const white = new THREE.MeshStandardMaterial({ color: 0xf8fafc, roughness: 0.5 });
  addBox(group, [0.75, 0.12, 0.75], [0, 0.06, 0], new THREE.MeshStandardMaterial({ color: 0x141414 }));
  const cone = new THREE.Mesh(new THREE.ConeGeometry(0.31, 1.05, 18), orange);
  cone.position.y = 0.62;
  cone.castShadow = true;
  group.add(cone);
  addCylinder(group, 0.225, 0.18, [0, 0.52, 0], white);
  return group;
}

function createArrow(material) {
  const shape = new THREE.Shape();
  shape.moveTo(-0.55, -1.4);
  shape.lineTo(0.55, -1.4);
  shape.lineTo(0.55, 0.25);
  shape.lineTo(1.25, 0.25);
  shape.lineTo(0, 1.55);
  shape.lineTo(-1.25, 0.25);
  shape.lineTo(-0.55, 0.25);
  shape.closePath();
  const mesh = new THREE.Mesh(new THREE.ShapeGeometry(shape), material);
  mesh.rotation.x = -Math.PI / 2;
  mesh.position.y = 0.025;
  return mesh;
}

function createBooth(label = "BOOTH CHECK-IN") {
  const group = new THREE.Group();
  const white = new THREE.MeshStandardMaterial({ color: 0xd8dde0, roughness: 0.58, metalness: 0.15 });
  const frame = new THREE.MeshStandardMaterial({ color: 0x11181e, roughness: 0.35, metalness: 0.5 });
  const glass = new THREE.MeshStandardMaterial({ color: 0x143649, roughness: 0.08, metalness: 0.2, transparent: true, opacity: 0.76 });
  addBox(group, [3.3, 3.05, 4.1], [0, 1.55, 0], white);
  addBox(group, [3.7, 0.28, 4.5], [0, 3.14, 0], frame);
  addBox(group, [2.5, 1.5, 0.08], [0, 2.05, -2.08], glass);
  addBox(group, [0.08, 1.5, 2.65], [-1.66, 2.05, 0], glass);
  addBox(group, [1.1, 0.42, 0.24], [0, 3.4, -1.75], new THREE.MeshStandardMaterial({ color: 0xf5c400, emissive: 0xf5c400, emissiveIntensity: 1.2 }));
  const sign = makeTextSign(label, 2.75, 0.72, { background: "#0d2533", accent: "#38bdf8" });
  sign.position.set(0, 2.8, -2.25);
  group.add(sign);
  return group;
}

function createRamp(label, direction = "down", accent = "#38bdf8") {
  const group = new THREE.Group();
  const concrete = new THREE.MeshStandardMaterial({ color: 0x555d61, roughness: 0.9 });
  const edge = new THREE.MeshStandardMaterial({ color: 0xf5c400, roughness: 0.62 });
  const white = new THREE.MeshStandardMaterial({ color: 0xf8fafc, roughness: 0.55 });
  const slope = direction === "down" ? -0.07 : 0.07;
  const deck = addBox(group, [13.5, 0.34, 6.1], [0, 0.55, 0], concrete);
  deck.rotation.z = slope;
  [-3.15, 3.15].forEach((z) => {
    const guard = addBox(group, [13.8, 0.72, 0.28], [0, 0.82, z], edge);
    guard.rotation.z = slope;
  });
  const centerLine = addBox(group, [11.8, 0.045, 0.16], [0, 0.82, 0], white);
  centerLine.rotation.z = slope;
  const arrow = createArrow(white);
  arrow.position.set(direction === "down" ? 2.2 : -2.2, 0.9, 0);
  arrow.rotation.z = direction === "down" ? -Math.PI / 2 : Math.PI / 2;
  group.add(arrow);
  const sign = makeTextSign(label, 5.8, 1.2, { background: "#111827", accent });
  sign.position.set(0, 2.75, 0);
  sign.rotation.y = Math.PI / 2;
  group.add(sign);
  return group;
}

function createBarrier(label) {
  const group = new THREE.Group();
  const orange = new THREE.MeshStandardMaterial({ color: 0xf97316, roughness: 0.48, metalness: 0.18 });
  addBox(group, [0.85, 1.35, 0.85], [0, 0.68, 0], orange);
  const arm = addBox(group, [0.18, 0.18, 5.1], [0, 1.28, -2.55], new THREE.MeshStandardMaterial({ color: 0xf8fafc, roughness: 0.45 }));
  arm.rotation.x = 0.02;
  for (let i = 0; i < 5; i += 1) {
    addBox(group, [0.2, 0.205, 0.42], [0, 1.28, -0.55 - i * 0.88], new THREE.MeshStandardMaterial({ color: 0xef4444 }));
  }
  const sign = makeTextSign(label, 2.2, 0.76, { background: "#0f172a", accent: "#f97316" });
  sign.position.set(0, 2.05, 0);
  sign.rotation.y = Math.PI / 2;
  group.add(sign);
  return group;
}

function createFireCabinet() {
  const group = new THREE.Group();
  const red = new THREE.MeshStandardMaterial({ color: 0xb91c1c, roughness: 0.32, metalness: 0.4 });
  addBox(group, [1.35, 1.55, 0.34], [0, 1.42, 0], red);
  addBox(group, [0.06, 1.12, 0.03], [0.54, 1.42, -0.19], new THREE.MeshStandardMaterial({ color: 0xfca5a5 }));
  const sign = makeTextSign("PCCC", 0.92, 0.46, { background: "#7f1d1d", accent: "#ffffff" });
  sign.position.set(0, 1.45, -0.19);
  group.add(sign);
  return group;
}

function createDoor(label, accent = "#22c55e") {
  const group = new THREE.Group();
  addBox(group, [2.4, 3.25, 0.24], [0, 1.62, 0], new THREE.MeshStandardMaterial({ color: 0xe7e7e4, roughness: 0.72 }));
  addCylinder(group, 0.09, 0.18, [0.78, 1.55, -0.2], new THREE.MeshStandardMaterial({ color: 0x63686c, metalness: 0.75 }), [Math.PI / 2, 0, 0]);
  const sign = makeTextSign(label, 2.8, 0.82, { background: "#14532d", accent });
  sign.position.set(0, 3.75, -0.16);
  group.add(sign);
  return group;
}

function createElevatorCore() {
  const group = new THREE.Group();
  const wall = new THREE.MeshStandardMaterial({ color: 0x30383d, roughness: 0.78 });
  const metal = new THREE.MeshStandardMaterial({ color: 0xc4cdd1, roughness: 0.24, metalness: 0.82 });
  const frame = new THREE.MeshStandardMaterial({ color: 0x11171b, roughness: 0.34, metalness: 0.62 });
  addBox(group, [10.5, 4.8, 7.8], [0, 2.4, 0], wall);
  [-2.45, 2.45].forEach((x) => {
    addBox(group, [3.12, 3.58, 0.12], [x, 1.79, -3.97], frame);
    addBox(group, [2.78, 3.3, 0.16], [x, 1.66, -4.05], metal);
    addBox(group, [0.075, 3.3, 0.2], [x, 1.66, -4.15], frame);
    addBox(group, [0.14, 0.34, 0.22], [x + 1.65, 1.35, -4.12], new THREE.MeshStandardMaterial({ color: 0xf5c400, emissive: 0xf5c400, emissiveIntensity: 1.2 }));
  });
  const header = makeTextSign("THANG MÁY", 5.6, 1.05, { background: "#15212a", accent: "#f5c400" });
  header.position.set(0, 4.02, -4.11);
  group.add(header);
  const canopy = addBox(group, [12.2, 0.32, 3.2], [0, 4.5, -4.6], new THREE.MeshStandardMaterial({ color: 0x252c31, metalness: 0.48 }));
  canopy.castShadow = true;
  return group;
}

function addArchitecture(scene, layers, floorContext) {
  const concrete = new THREE.MeshStandardMaterial({ map: concreteTexture(), color: 0xffffff, roughness: 0.92, metalness: 0.02 });
  const asphalt = new THREE.MeshStandardMaterial({ map: asphaltTexture(), color: 0xe2e5e6, roughness: 0.94 });
  const darkConcrete = new THREE.MeshStandardMaterial({ color: 0x656c70, roughness: 0.9 });
  const white = new THREE.MeshStandardMaterial({ color: 0xf4f3ea, roughness: 0.62 });
  const yellow = new THREE.MeshStandardMaterial({ color: 0xf5c400, roughness: 0.58 });
  const hazard = new THREE.MeshStandardMaterial({ map: hazardTexture(), roughness: 0.66 });

  const floor = new THREE.Group();
  floor.name = "floor";
  addBox(floor, [WORLD.width, 0.34, WORLD.depth], [0, -0.17, 0], asphalt);
  addBox(floor, [WORLD.width, 0.12, 1.15], [0, 0.02, 0], new THREE.MeshStandardMaterial({ color: 0x30363a, roughness: 0.8 }));
  scene.add(floor);

  const structure = new THREE.Group();
  structure.name = "structure";
  addBox(structure, [WORLD.width, WORLD.ceiling, 0.55], [0, WORLD.ceiling / 2, -WORLD.depth / 2], concrete);
  addBox(structure, [WORLD.width, WORLD.ceiling, 0.55], [0, WORLD.ceiling / 2, WORLD.depth / 2], concrete);
  addBox(structure, [0.55, WORLD.ceiling, WORLD.depth], [-WORLD.width / 2, WORLD.ceiling / 2, 0], concrete);
  addBox(structure, [0.55, WORLD.ceiling, WORLD.depth], [WORLD.width / 2, WORLD.ceiling / 2, 0], concrete);
  [-36, -18, 0, 18, 36].forEach((x) => [-21, 0, 21].forEach((z) => {
    addBox(structure, [2.2, WORLD.ceiling, 2.2], [x, WORLD.ceiling / 2, z], darkConcrete);
    const wrap = addBox(structure, [2.28, 1.55, 2.28], [x, 0.92, z], hazard);
    wrap.userData.kind = "column";
  }));
  scene.add(structure);

  const ceiling = new THREE.Group();
  ceiling.name = "ceiling";
  addBox(ceiling, [WORLD.width, 0.34, WORLD.depth], [0, WORLD.ceiling, 0], darkConcrete);
  [-24, 0, 24].forEach((z) => addBox(ceiling, [WORLD.width, 0.58, 0.85], [0, WORLD.ceiling - 0.28, z], darkConcrete));
  [-36, -18, 0, 18, 36].forEach((x) => addBox(ceiling, [0.82, 0.55, WORLD.depth], [x, WORLD.ceiling - 0.28, 0], darkConcrete));
  scene.add(ceiling);
  layers.ceiling = ceiling;

  const markings = new THREE.Group();
  markings.name = "markings";
  const line = (w, d, x, z, color = white) => addBox(markings, [w, 0.035, d], [x, 0.035, z], color);
  line(86, 0.18, 4, -5.2, yellow);
  line(86, 0.18, 4, 5.2, yellow);
  for (let x = -38; x <= 40; x += 13) {
    const arrowA = createArrow(white);
    arrowA.position.set(x, 0.04, -2.45);
    arrowA.rotation.z = -Math.PI / 2;
    markings.add(arrowA);
    const arrowB = createArrow(white);
    arrowB.position.set(x + 6, 0.04, 2.45);
    arrowB.rotation.z = Math.PI / 2;
    markings.add(arrowB);
  }
  scene.add(markings);

  const entrance = new THREE.Group();
  entrance.name = "entrance";
  const floorNumber = floorContext?.floorNumber || 1;
  const floorCount = floorContext?.floorCount || Math.max(floorNumber, 2);
  const isB1 = floorNumber === 1;
  const divider = new THREE.MeshStandardMaterial({ color: 0xf5c400, roughness: 0.5, metalness: 0.12 });
  if (isB1) {
    const groundArrival = createRamp("MẶT ĐẤT → B1", "down", "#38bdf8");
    groundArrival.position.set(-43, 0, 3.8);
    entrance.add(groundArrival);

    const booth = createBooth("CHECK-IN XE THÁNG");
    booth.position.set(-28.5, 0, -10.5);
    entrance.add(booth);
    const monthlyBarrier = createBarrier("THẺ THÁNG");
    monthlyBarrier.position.set(-23.5, 0, -6.2);
    monthlyBarrier.rotation.y = Math.PI / 2;
    entrance.add(monthlyBarrier);

    const visitorRamp = createRamp("VÃNG LAI ↓ B2", "down", "#22c55e");
    visitorRamp.position.set(-25.5, 0, 4.2);
    entrance.add(visitorRamp);

    const guide = makeTextSign("THÁNG ←   |   VÃNG LAI ↓ B2", 12.8, 1.85, { background: "#0f172a", accent: "#f5c400" });
    guide.position.set(-35, 4.05, -1.8);
    guide.rotation.y = Math.PI / 2;
    entrance.add(guide);

    for (let x = -15; x <= 25; x += 4) addBox(entrance, [2.5, 0.42, 0.32], [x, 0.22, 8.8], divider);

    const monthlyCheckout = createBooth("CHECK-OUT XE THÁNG");
    monthlyCheckout.scale.setScalar(0.82);
    monthlyCheckout.position.set(24.5, 0, 13.5);
    entrance.add(monthlyCheckout);

    const fromB2 = createRamp("B2 ↑ B1", "up", "#22c55e");
    fromB2.position.set(39, 0, -5.2);
    entrance.add(fromB2);
    const toGround = createRamp("B1 ↑ MẶT ĐẤT · 2 LÀN", "up", "#f5c400");
    toGround.position.set(39, 0, 5.2);
    entrance.add(toGround);
  } else {
    const upRamp = createRamp(`${floorContext?.code || `B${floorNumber}`} ↑ B${floorNumber - 1}`, "up", "#22c55e");
    upRamp.position.set(39, 0, 6.1);
    entrance.add(upRamp);
    if (floorNumber < floorCount) {
      const downRamp = createRamp(`${floorContext?.code || `B${floorNumber}`} ↓ B${floorNumber + 1}`, "down", "#38bdf8");
      downRamp.position.set(39, 0, -6.1);
      entrance.add(downRamp);
    }
    const endSign = makeTextSign(floorNumber < floorCount ? `CUỐI TUYẾN · ↑ B${floorNumber - 1}  |  ↓ B${floorNumber + 1}` : `CUỐI TUYẾN · ↑ B${floorNumber - 1}`, 11.5, 1.65, { background: "#0f172a", accent: "#f5c400" });
    endSign.position.set(30.5, 3.75, 0);
    endSign.rotation.y = -Math.PI / 2;
    entrance.add(endSign);
    if (floorNumber === 2) {
      const visitorCheckout = createBooth("CHECK-OUT VÃNG LAI");
      visitorCheckout.scale.setScalar(0.82);
      visitorCheckout.position.set(23.5, 0, 11.8);
      entrance.add(visitorCheckout);
      const checkoutBarrier = createBarrier("QUẸT THẺ RA");
      checkoutBarrier.scale.setScalar(0.86);
      checkoutBarrier.position.set(29, 0, 6.1);
      checkoutBarrier.rotation.y = -Math.PI / 2;
      entrance.add(checkoutBarrier);
    }
  }
  scene.add(entrance);
  layers.entrance = entrance;

  const facilities = new THREE.Group();
  facilities.name = "facilities";
  const elevator = createElevatorCore();
  elevator.position.set(45, 0, 12);
  facilities.add(elevator);
  const emergencyDoor = createDoor("LỐI THOÁT", "#86efac");
  emergencyDoor.position.set(51.55, 0, -21);
  emergencyDoor.rotation.y = -Math.PI / 2;
  facilities.add(emergencyDoor);
  const stairs = new THREE.Group();
  for (let i = 0; i < 8; i += 1) addBox(stairs, [5.5, 0.25 + i * 0.22, 0.75], [0, (0.25 + i * 0.22) / 2, i * 0.72], concrete);
  stairs.position.set(45.5, 0, -30.8);
  stairs.rotation.y = Math.PI / 2;
  facilities.add(stairs);
  [[-24, -35.65, 0], [10, -35.65, 0], [33, 35.65, Math.PI]].forEach(([x, z, ry]) => {
    const cabinet = createFireCabinet();
    cabinet.position.set(x, 0, z);
    cabinet.rotation.y = ry;
    facilities.add(cabinet);
  });
  scene.add(facilities);
  layers.facilities = facilities;

  const mep = new THREE.Group();
  mep.name = "mep";
  const redPipe = new THREE.MeshStandardMaterial({ color: 0xb91c1c, roughness: 0.36, metalness: 0.48 });
  const pipe = new THREE.MeshStandardMaterial({ color: 0x333b40, roughness: 0.42, metalness: 0.72 });
  const duct = new THREE.MeshStandardMaterial({ color: 0x92999d, roughness: 0.34, metalness: 0.65 });
  [-29, 29].forEach((z) => addCylinder(mep, 0.11, WORLD.width - 7, [0, 4.43, z], redPipe, [0, 0, Math.PI / 2]));
  [-17, 17].forEach((z) => {
    addBox(mep, [WORLD.width - 16, 0.58, 1.3], [0, 4.28, z], duct);
    for (let x = -38; x <= 38; x += 12) addBox(mep, [2.7, 0.12, 1.36], [x, 3.93, z], pipe);
  });
  [-9, 9].forEach((z) => addCylinder(mep, 0.18, WORLD.width - 12, [0, 4.05, z], pipe, [0, 0, Math.PI / 2]));
  for (let x = -42; x <= 42; x += 12) [-29, -9, 9, 29].forEach((z) => {
    addCylinder(mep, 0.035, 0.5, [x, 4.13, z], redPipe);
    addCylinder(mep, 0.13, 0.14, [x, 3.83, z], new THREE.MeshStandardMaterial({ color: 0xa7adb0, metalness: 0.8 }));
  });
  scene.add(mep);
  layers.mep = mep;

  const lights = new THREE.Group();
  lights.name = "lights";
  const lightMaterial = new THREE.MeshStandardMaterial({ color: 0xffffff, emissive: 0xe8f4ff, emissiveIntensity: 4.8 });
  for (let x = -42; x <= 42; x += 12) [-25, -13, 0, 13, 25].forEach((z) => {
    addBox(lights, [3.8, 0.12, 0.42], [x, 4.55, z], lightMaterial);
    if (Math.abs(z) <= 13 && x % 24 === 6) {
      const point = new THREE.PointLight(0xd8ecff, 18, 18, 2.2);
      point.position.set(x, 4.2, z);
      lights.add(point);
    }
  });
  scene.add(lights);
  layers.lights = lights;
}

function createSlotLayout(slotCount = 32) {
  const slots = [];
  const rows = [
    { z: -31.5, rotation: 0, prefix: "A" },
    { z: -20.2, rotation: Math.PI, prefix: "B" },
    { z: 20.2, rotation: 0, prefix: "C" },
    { z: 31.5, rotation: Math.PI, prefix: "D" },
  ];
  const boundedCount = Math.max(1, Math.min(slotCount, 48));
  const perRow = Math.ceil(boundedCount / rows.length);
  const spacing = Math.min(8.4, 70 / Math.max(perRow - 1, 1));
  const startX = -((perRow - 1) * spacing) / 2;
  rows.forEach((row) => {
    for (let index = 0; index < perRow && slots.length < boundedCount; index += 1) {
      slots.push({
        key: `${row.prefix}-${String(index + 1).padStart(2, "0")}`,
        x: startX + index * spacing,
        z: row.z,
        rotation: row.rotation,
      });
    }
  });
  return slots;
}

function createSlotBase(slot, clickable, slotRoots) {
  const group = new THREE.Group();
  group.position.set(slot.x, 0, slot.z);
  group.rotation.y = slot.rotation;
  const white = new THREE.MeshStandardMaterial({ color: 0xe7e5d9, roughness: 0.65 });
  const lineWidth = 0.11;
  addBox(group, [lineWidth, 0.035, 5.35], [-2.75, 0.035, 0], white);
  addBox(group, [lineWidth, 0.035, 5.35], [2.75, 0.035, 0], white);
  addBox(group, [5.6, 0.035, lineWidth], [0, 0.035, 2.62], white);
  const stopperMaterial = new THREE.MeshStandardMaterial({ color: 0xeab308, roughness: 0.52 });
  [-1.55, 1.55].forEach((x) => addCylinder(group, 0.12, 1.1, [x, 0.14, 1.9], stopperMaterial, [0, 0, Math.PI / 2]));
  const hitArea = addBox(group, [5.45, 0.08, 5.15], [0, 0.02, 0], new THREE.MeshBasicMaterial({ transparent: true, opacity: 0 }));
  hitArea.userData.kind = "slot";
  hitArea.userData.slotKey = slot.key;
  clickable.push(hitArea);
  group.userData.dynamic = new THREE.Group();
  group.add(group.userData.dynamic);
  slotRoots.set(slot.key, group);
  return group;
}

function normalizeSlot(raw, index, fallbackKey) {
  const physical = String(raw?.physicalStatus || raw?.status || (index % 7 === 1 ? "OCCUPIED" : index % 19 === 7 ? "MAINTENANCE" : "EMPTY")).toUpperCase();
  return {
    id: raw?.id ?? fallbackKey,
    slotCode: raw?.slotCode || raw?.code || fallbackKey,
    physicalStatus: physical === "EMPTY" && raw?.isAvailable === false ? "UNAVAILABLE" : physical,
    isAvailable: raw?.isAvailable ?? physical === "EMPTY",
    zone: raw?.zone || fallbackKey.split("-")[0],
  };
}

export function createGarageScene({ canvas, floorLabel, floorContext, slots = [], onSelect }) {
  const renderer = new THREE.WebGLRenderer({ canvas, antialias: true, powerPreference: "high-performance" });
  renderer.outputColorSpace = THREE.SRGBColorSpace;
  renderer.toneMapping = THREE.ACESFilmicToneMapping;
  renderer.toneMappingExposure = 1.42;
  renderer.shadowMap.enabled = true;
  renderer.shadowMap.type = THREE.PCFShadowMap;
  renderer.setPixelRatio(Math.min(window.devicePixelRatio || 1, 1.75));

  const scene = new THREE.Scene();
  scene.background = new THREE.Color(0x10171b);
  scene.fog = new THREE.FogExp2(0x1d2529, 0.0065);
  const camera = new THREE.PerspectiveCamera(48, 1, 0.1, 260);
  camera.position.set(55, 43, 54);
  const controls = new OrbitControls(camera, canvas);
  controls.target.set(0, 1.2, 0);
  controls.enableDamping = true;
  controls.dampingFactor = 0.075;
  controls.minDistance = 8;
  controls.maxDistance = 125;
  controls.maxPolarAngle = Math.PI * 0.49;
  controls.update();

  scene.add(new THREE.HemisphereLight(0xd9efff, 0x30383d, 2.8));
  scene.add(new THREE.AmbientLight(0xddeeff, 1.15));
  const sun = new THREE.DirectionalLight(0xf2f8ff, 4.2);
  sun.position.set(-22, 36, 18);
  sun.castShadow = true;
  sun.shadow.mapSize.set(2048, 2048);
  sun.shadow.camera.left = -65;
  sun.shadow.camera.right = 65;
  sun.shadow.camera.top = 50;
  sun.shadow.camera.bottom = -50;
  scene.add(sun);

  const layers = {};
  addArchitecture(scene, layers, floorContext);

  const parking = new THREE.Group();
  parking.name = "parking";
  const clickable = [];
  const slotRoots = new Map();
  const layout = createSlotLayout(Math.max(slots.length, floorContext?.slotCount || 32));
  layout.forEach((slot) => parking.add(createSlotBase(slot, clickable, slotRoots)));
  scene.add(parking);
  layers.parking = parking;

  const zoneSigns = new THREE.Group();
  const zonePrefix = floorContext?.parkingType === "MONTHLY" ? "THÁNG" : "VÃNG LAI";
  [[`${zonePrefix} A`, -34, -35.6], [`${zonePrefix} B`, 23, -35.6], [`${zonePrefix} C`, -34, 35.6], [`${zonePrefix} D`, 23, 35.6]].forEach(([text, x, z]) => {
    const sign = makeTextSign(`${floorLabel} · ${text}`, 6.2, 1.5, { background: "#111827", accent: "#f5c400" });
    sign.position.set(x, 3.25, z > 0 ? z - 0.3 : z + 0.3);
    sign.rotation.y = z > 0 ? Math.PI : 0;
    zoneSigns.add(sign);
  });
  scene.add(zoneSigns);

  const raycaster = new THREE.Raycaster();
  const pointer = new THREE.Vector2();
  let statusFilter = "ALL";
  let activeSlots = [];
  let hovered = null;
  let cameraTween = null;

  function updateSlots(nextSlots = slots, filter = statusFilter) {
    statusFilter = filter;
    activeSlots = layout.map((layoutSlot, index) => normalizeSlot(nextSlots[index], index, layoutSlot.key));
    layout.forEach((layoutSlot, index) => {
      const root = slotRoots.get(layoutSlot.key);
      const dynamic = root.userData.dynamic;
      while (dynamic.children.length) {
        const child = dynamic.children.pop();
        child.traverse((node) => {
          node.geometry?.dispose?.();
          if (node.material?.map) node.material.map.dispose();
          node.material?.dispose?.();
        });
      }
      const data = activeSlots[index];
      root.userData.slotData = data;
      const status = STATUS_COLORS[data.physicalStatus] ? data.physicalStatus : "UNAVAILABLE";
      root.visible = filter === "ALL" || filter === status;
      const indicator = addCylinder(dynamic, 0.18, 0.1, [0, 0.14, -2.13], new THREE.MeshStandardMaterial({
        color: STATUS_COLORS[status], emissive: STATUS_COLORS[status], emissiveIntensity: 2.4,
      }));
      indicator.userData.kind = "slot";
      indicator.userData.slotKey = layoutSlot.key;
      const code = makeTextSign(data.slotCode, 2.7, 0.72, { background: "rgba(17,24,39,.9)", accent: `#${STATUS_COLORS[status].toString(16).padStart(6, "0")}` });
      code.position.set(0, 0.07, 1.15);
      code.rotation.x = -Math.PI / 2;
      dynamic.add(code);
      if (status === "OCCUPIED") {
        const car = createCar(CAR_COLORS[index % CAR_COLORS.length]);
        car.position.set(0, 0, 0.1);
        car.userData.kind = "slot";
        car.userData.slotKey = layoutSlot.key;
        car.traverse((node) => {
          if (node.isMesh) {
            node.userData.kind = "slot";
            node.userData.slotKey = layoutSlot.key;
          }
        });
        dynamic.add(car);
      } else if (status === "MAINTENANCE") {
        [-0.9, 0.9].forEach((x) => {
          const cone = createCone();
          cone.position.set(x, 0, 0);
          dynamic.add(cone);
        });
      } else if (status === "UNAVAILABLE") {
        const crossMaterial = new THREE.MeshStandardMaterial({ color: 0xef4444, emissive: 0x7f1d1d, emissiveIntensity: 0.5 });
        const crossA = addBox(dynamic, [0.28, 0.05, 4.2], [0, 0.07, 0], crossMaterial);
        const crossB = crossA.clone();
        crossA.rotation.y = Math.PI / 4;
        crossB.rotation.y = -Math.PI / 4;
        dynamic.add(crossB);
      }
    });
  }

  updateSlots(slots);

  function pick(event, click = false) {
    const rect = canvas.getBoundingClientRect();
    pointer.x = ((event.clientX - rect.left) / rect.width) * 2 - 1;
    pointer.y = -((event.clientY - rect.top) / rect.height) * 2 + 1;
    raycaster.setFromCamera(pointer, camera);
    const intersections = raycaster.intersectObjects(clickable, false);
    const target = intersections.find((entry) => entry.object.visible)?.object || null;
    canvas.style.cursor = target ? "pointer" : "grab";
    if (hovered && hovered !== target && hovered.material?.emissive) hovered.material.emissiveIntensity = hovered.userData.previousIntensity ?? hovered.material.emissiveIntensity;
    hovered = target;
    if (target?.material?.emissive) {
      target.userData.previousIntensity ??= target.material.emissiveIntensity;
      target.material.emissiveIntensity = Math.max(3.8, target.material.emissiveIntensity);
    }
    if (click && target) {
      const root = slotRoots.get(target.userData.slotKey);
      if (!root) return;
      onSelect?.(root.userData.slotData);
      const world = root.getWorldPosition(new THREE.Vector3());
      cameraTween = {
        start: performance.now(),
        duration: 750,
        fromPosition: camera.position.clone(),
        fromTarget: controls.target.clone(),
        toPosition: world.clone().add(new THREE.Vector3(7, 8, 9)),
        toTarget: world.clone().add(new THREE.Vector3(0, 0.8, 0)),
      };
    }
  }
  const onMove = (event) => pick(event, false);
  const onClick = (event) => pick(event, true);
  canvas.addEventListener("pointermove", onMove);
  canvas.addEventListener("click", onClick);

  let frame = 0;
  function animate(time) {
    frame = requestAnimationFrame(animate);
    if (cameraTween) {
      const raw = Math.min(1, (time - cameraTween.start) / cameraTween.duration);
      const eased = 1 - Math.pow(1 - raw, 3);
      camera.position.lerpVectors(cameraTween.fromPosition, cameraTween.toPosition, eased);
      controls.target.lerpVectors(cameraTween.fromTarget, cameraTween.toTarget, eased);
      if (raw >= 1) cameraTween = null;
    }
    controls.update();
    renderer.render(scene, camera);
  }
  animate(performance.now());

  function resize(width, height) {
    renderer.setSize(width, height, false);
    camera.aspect = width / Math.max(height, 1);
    camera.updateProjectionMatrix();
  }
  function setCamera(mode) {
    const views = {
      overview: { position: [55, 43, 54], target: [0, 1.2, 0] },
      top: { position: [0, 91, 0.01], target: [0, 0, 0] },
      drive: { position: [-30, 2.35, -5.2], target: [24, 1.05, -5.2] },
      entrance: floorContext?.floorNumber === 1
        ? { position: [-8, 3.15, 14], target: [-30, 1.2, 0] }
        : { position: [14, 3.15, -14], target: [39, 1.2, 0] },
      elevator: { position: [37.5, 3.15, -4], target: [45, 1.75, 8] },
    };
    const view = views[mode] || views.overview;
    cameraTween = {
      start: performance.now(), duration: 900,
      fromPosition: camera.position.clone(), fromTarget: controls.target.clone(),
      toPosition: new THREE.Vector3(...view.position), toTarget: new THREE.Vector3(...view.target),
    };
  }
  function setLayer(name, visible) {
    if (layers[name]) layers[name].visible = visible;
  }
  function destroy() {
    cancelAnimationFrame(frame);
    canvas.removeEventListener("pointermove", onMove);
    canvas.removeEventListener("click", onClick);
    controls.dispose();
    scene.traverse((object) => {
      object.geometry?.dispose?.();
      if (Array.isArray(object.material)) object.material.forEach((material) => { material.map?.dispose?.(); material.dispose?.(); });
      else { object.material?.map?.dispose?.(); object.material?.dispose?.(); }
    });
    renderer.dispose();
  }
  return { resize, setCamera, setLayer, updateSlots, destroy };
}

export { STATUS_COLORS };
