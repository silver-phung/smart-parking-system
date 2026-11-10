/**
 * ==========================================================
 * SmartPark 3D Render (Three.js) - Hầm / Bãi đỗ xe cao tầng
 *
 * Dựng 3D thủ tục (procedural) theo các mẫu tham khảo:
 *   - Hầm ngầm Sketchfab "Underground Parking Lot": cột bê tông có chân sọc vàng-đen,
 *     dầm trần, ống gió/PCCC, đèn tuýp, vạch sơn, mũi tên chỉ hướng.
 *   - Booth check-in / check-out: trụ quẹt thẻ + thanh chắn (barrier) đỏ-trắng.
 *   - CGTrader "Parking Garage": vạch kẻ, lối thoát hiểm, bình chữa cháy, quạt thông gió,
 *     gờ giảm tốc, vạch qua đường cho người đi bộ.
 *   - Cụm thang máy sát tường + tiền sảnh kính trong suốt + bảng treo trần chỉ đường.
 *   - Bãi cao tầng (multi-storey): lan can, lam đứng mặt tiền, cột chu vi, ramp xe lên xuống.
 *
 * Bố cục 1 tầng: 100 slot = 4 hàng (A-D) x 25 slot, 3 lối đi xen giữa.
 * Mã slot: [Tầng]-[Hàng][Số], ví dụ B1-A01, B2-C20, L3-D25.
 * Khu XE THÁNG: slot 01-12 (48 slot) | Khu VÃNG LAI: slot 13-25 (52 slot),
 * ngăn cách bằng barrier ở từng lối đi.
 *
 * DỮ LIỆU: đây là bản DEMO dựng sẵn (xe/biển số sinh giả lập cố định bằng seed),
 * độc lập với viewer nối API (parking-3d-viewer.js).
 * ==========================================================
 */
(function (root) {
  'use strict';

  /* ------------------------------------------------------------------ */
  /* HẰNG SỐ CHUNG (mét) - kích thước bố cục chi tiết nằm trong floor.js */
  /* ------------------------------------------------------------------ */
  var FH = 4.8;          // cao độ giữa 2 tầng
  var CLEAR = 4.2;       // chiều cao thông thủy (đã nâng trần)
  var SLAB = 0.6;        // dày sàn
  var EYE = 1.65;

  /* ------------------------------------------------------------------ */
  /* DANH SÁCH BÃI MẪU                                                   */
  /*  - ids[0] luôn là TẦNG ĐẦU TIÊN (B1 hoặc T1): có lối vào, check-in   */
  /*    xe tháng, check-out xe tháng và lối ra. Hầm: B1 là tầng cao nhất; */
  /*    cao tầng: T1 là tầng trệt, các tầng sau nằm phía trên.            */
  /*  - counts: số slot của 4 dãy A-D ở từng tầng.                         */
  /*  - Tầng đầu = chỉ xe THÁNG, các tầng sau = xe VÃNG LAI.               */
  /* ------------------------------------------------------------------ */
  var LOTS = [
    { id: 'fptq9', name: 'Hầm ngầm FPT Q9 · 2 tầng (64 slot)', type: 'underground', ids: ['B1', 'B2'], seed: 9,
      counts: [[8, 8, 8, 8], [8, 8, 8, 8]] },
    { id: 'landmark81', name: 'Vincom Landmark 81 · 3 tầng hầm (82 slot)', type: 'underground', ids: ['B1', 'B2', 'B3'], seed: 81,
      counts: [[6, 6, 6, 6], [7, 7, 7, 7], [7, 7, 8, 8]] },
    { id: 'aeon', name: 'Aeon Mall Mega Hub · 4 tầng hầm (112 slot)', type: 'underground', ids: ['B1', 'B2', 'B3', 'B4'], seed: 112,
      counts: [[6, 6, 6, 6], [7, 7, 7, 8], [7, 7, 7, 8], [8, 8, 7, 7]] },
    { id: 'saigoncentre', name: 'Saigon Centre · bãi cao tầng 3 tầng (88 slot)', type: 'multistorey', ids: ['T1', 'T2', 'T3'], seed: 27,
      counts: [[7, 7, 7, 7], [7, 7, 7, 7], [8, 8, 8, 8]] },
    { id: 'crescent', name: 'Crescent Mall · bãi cao tầng 2 tầng (64 slot)', type: 'multistorey', ids: ['T1', 'T2'], seed: 45,
      counts: [[8, 8, 8, 8], [8, 8, 8, 8]] }
  ];

  var T; // alias THREE
  var M = {};          // materials
  var TEXC = {};       // texture cache
  var GEO = {};        // geometry cache

  /* ------------------------------------------------------------------ */
  /* TIỆN ÍCH                                                            */
  /* ------------------------------------------------------------------ */
  function rng(seed) {
    var a = seed >>> 0;
    return function () {
      a = (a + 0x6D2B79F5) | 0;
      var t = Math.imul(a ^ (a >>> 15), 1 | a);
      t = (t + Math.imul(t ^ (t >>> 7), 61 | t)) ^ t;
      return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
    };
  }
  function pad2(n) { return (n < 10 ? '0' : '') + n; }
  function clamp(v, a, b) { return Math.max(a, Math.min(b, v)); }

  function canvasTex(w, h, draw, repeat) {
    var c = document.createElement('canvas');
    c.width = w; c.height = h;
    draw(c.getContext('2d'), w, h);
    var t = new T.CanvasTexture(c);
    t.anisotropy = 4;
    if (repeat) { t.wrapS = t.wrapT = T.RepeatWrapping; }
    return t;
  }
  function noiseFill(ctx, w, h, base, amp, n) {
    ctx.fillStyle = base; ctx.fillRect(0, 0, w, h);
    for (var i = 0; i < n; i++) {
      var v = Math.floor(128 + (Math.random() - 0.5) * amp);
      ctx.fillStyle = 'rgba(' + v + ',' + v + ',' + v + ',0.18)';
      ctx.fillRect(Math.random() * w, Math.random() * h, 2, 2);
    }
  }

  function initMaterials() {
    if (M.floor) return;
    var tAsphalt = canvasTex(256, 256, function (c, w, h) {
      noiseFill(c, w, h, '#5b6068', 120, 9000);
    }, true);
    var tConcrete = canvasTex(256, 256, function (c, w, h) {
      noiseFill(c, w, h, '#b9bbbd', 90, 7000);
      c.strokeStyle = 'rgba(70,72,75,0.35)'; c.lineWidth = 2;
      c.strokeRect(1, 1, w - 2, h - 2);
    }, true);
    var tWall = canvasTex(256, 256, function (c, w, h) {
      noiseFill(c, w, h, '#c4c6c8', 70, 6000);
      c.strokeStyle = 'rgba(60,62,65,0.4)'; c.lineWidth = 2;
      c.beginPath(); c.moveTo(0, 0); c.lineTo(0, h); c.moveTo(0, h - 1); c.lineTo(w, h - 1); c.stroke();
    }, true);
    var tCeil = canvasTex(256, 256, function (c, w, h) {
      noiseFill(c, w, h, '#a3a6a9', 60, 5000);
    }, true);
    var tHazard = canvasTex(128, 128, function (c, w, h) {
      c.fillStyle = '#f6c700'; c.fillRect(0, 0, w, h);
      c.fillStyle = '#16181b';
      for (var i = -h; i < w + h; i += 32) {
        c.beginPath(); c.moveTo(i, h); c.lineTo(i + 16, h); c.lineTo(i + 16 + h, 0); c.lineTo(i + h, 0); c.fill();
      }
    }, true);
    var tBarrier = canvasTex(256, 32, function (c, w, h) {
      for (var i = 0; i < 8; i++) { c.fillStyle = i % 2 ? '#ffffff' : '#d62828'; c.fillRect(i * 32, 0, 32, h); }
    }, true);

    M.floor = new T.MeshStandardMaterial({ map: tAsphalt, roughness: 0.93, metalness: 0 });
    M.slab = new T.MeshStandardMaterial({ map: tCeil, roughness: 0.95, side: T.DoubleSide });
    M.conc = new T.MeshStandardMaterial({ map: tConcrete, roughness: 0.95 });
    M.wall = new T.MeshStandardMaterial({ map: tWall, roughness: 0.95, side: T.DoubleSide });
    M.ceil = new T.MeshStandardMaterial({ map: tCeil, roughness: 0.95, side: T.DoubleSide });
    M.hazard = new T.MeshStandardMaterial({ map: tHazard, roughness: 0.8 });
    M.plaster = new T.MeshStandardMaterial({ color: 0xe6e8ec, roughness: 0.85 });
    M.steel = new T.MeshStandardMaterial({ color: 0xb8c0c8, roughness: 0.35, metalness: 0.8 });
    M.dark = new T.MeshStandardMaterial({ color: 0x20242a, roughness: 0.6, metalness: 0.4 });
    M.duct = new T.MeshStandardMaterial({ color: 0xa8adb3, roughness: 0.55, metalness: 0.5 });
    M.pipeRed = new T.MeshStandardMaterial({ color: 0xc62828, roughness: 0.5, metalness: 0.3 });
    M.pipeBlue = new T.MeshStandardMaterial({ color: 0x1e5bb8, roughness: 0.5, metalness: 0.3 });
    M.tray = new T.MeshStandardMaterial({ color: 0x6b7078, roughness: 0.5, metalness: 0.6 });
    M.lamp = new T.MeshBasicMaterial({ color: 0xfff4d6 });
    M.white = new T.MeshBasicMaterial({ color: 0xf4f4f0 });
    M.yellow = new T.MeshBasicMaterial({ color: 0xf2c200 });
    M.paintWhite = new T.MeshBasicMaterial({ color: 0xe8e8e2, polygonOffset: true, polygonOffsetFactor: -2, polygonOffsetUnits: -2 });
    M.paintYellow = new T.MeshBasicMaterial({ color: 0xf2c200, polygonOffset: true, polygonOffsetFactor: -2, polygonOffsetUnits: -2 });
    M.paintBlue = new T.MeshBasicMaterial({ color: 0x4aa3ff, polygonOffset: true, polygonOffsetFactor: -2, polygonOffsetUnits: -2 });
    M.paintGreen = new T.MeshBasicMaterial({ color: 0x2fcf6a, polygonOffset: true, polygonOffsetFactor: -3, polygonOffsetUnits: -3 });
    M.glass = new T.MeshStandardMaterial({ color: 0x9fd8ff, transparent: true, opacity: 0.2, roughness: 0.05, metalness: 0.2, side: T.DoubleSide, depthWrite: false });
    M.frame = new T.MeshStandardMaterial({ color: 0x8e979f, roughness: 0.4, metalness: 0.8 });
    M.red = new T.MeshStandardMaterial({ color: 0xd32f2f, roughness: 0.5 });
    M.green = new T.MeshBasicMaterial({ color: 0x19c45a });
    M.exitGreen = new T.MeshBasicMaterial({ color: 0x0fa44a });
    M.barrier = new T.MeshStandardMaterial({ map: tBarrier, roughness: 0.5 });
    M.cabin = new T.MeshStandardMaterial({ color: 0xfff1cf, emissive: 0xffe9b5, emissiveIntensity: 0.9, roughness: 0.6 });
    M.glow = new T.MeshBasicMaterial({ color: 0xffffff });
    M.sky = new T.MeshBasicMaterial({ color: 0xcfe8ff, side: T.DoubleSide });
    M.car = new T.MeshStandardMaterial({ color: 0xffffff, roughness: 0.45, metalness: 0.08 });
    M.carGlass = new T.MeshStandardMaterial({ color: 0x1b2430, roughness: 0.25, metalness: 0.25 });
    M.tire = new T.MeshStandardMaterial({ color: 0x15171a, roughness: 0.9 });
    M.tail = new T.MeshBasicMaterial({ color: 0xff3030 });
    M.plate = new T.MeshBasicMaterial({ color: 0xfafafa });
    M.cone = new T.MeshStandardMaterial({ color: 0xff7a1a, roughness: 0.6 });
    M.ground = new T.MeshStandardMaterial({ color: 0x2c3138, roughness: 1 });
    M.road = new T.MeshStandardMaterial({ color: 0x23272d, roughness: 0.95 });
  }

  /* ------------------------------------------------------------------ */
  /* HÌNH HỌC / MESH TIỆN ÍCH                                            */
  /* ------------------------------------------------------------------ */
  function box(parent, w, h, d, mat, x, y, z) {
    var m = new T.Mesh(new T.BoxGeometry(w, h, d), mat);
    m.position.set(x, y, z);
    if (parent) parent.add(m);
    return m;
  }
  function cyl(parent, r, len, mat, x, y, z, axis) {
    var g = new T.CylinderGeometry(r, r, len, 14);
    if (axis === 'x') g.rotateZ(Math.PI / 2);
    else if (axis === 'z') g.rotateX(Math.PI / 2);
    var m = new T.Mesh(g, mat);
    m.position.set(x, y, z);
    if (parent) parent.add(m);
    return m;
  }
  function texPlane(w, h, mat, ru, rv) {
    var g = new T.PlaneGeometry(w, h), uv = g.attributes.uv;
    for (var i = 0; i < uv.count; i++) uv.setXY(i, uv.getX(i) * ru, uv.getY(i) * rv);
    return new T.Mesh(g, mat);
  }
  function floorRect(parent, x0, x1, z0, z1, y, withSlab) {
    var w = x1 - x0, d = z1 - z0;
    var f = texPlane(w, d, M.floor, w / 4, d / 4);
    f.rotation.x = -Math.PI / 2;
    f.position.set((x0 + x1) / 2, y + 0.002, (z0 + z1) / 2);
    parent.add(f);
    if (withSlab !== false) box(parent, w, SLAB, d, M.slab, (x0 + x1) / 2, y - SLAB / 2 - 0.004, (z0 + z1) / 2);
  }
  function wallSeg(parent, ax, az, bx, bz, y0, y1, mat) {
    var dx = bx - ax, dz = bz - az, len = Math.sqrt(dx * dx + dz * dz), h = y1 - y0;
    if (len < 0.01 || h < 0.01) return null;
    var m = texPlane(len, h, mat || M.wall, len / 3, h / 3);
    m.position.set((ax + bx) / 2, (y0 + y1) / 2, (az + bz) / 2);
    m.rotation.y = Math.atan2(-dz, dx);
    parent.add(m);
    return m;
  }
  /** Tường phẳng có ô cửa (dọc theo x hoặc z) */
  function wallWithGap(parent, axis, fixed, from, to, y0, y1, gapC, gapW, gapH, mat) {
    var a = gapC - gapW / 2, b = gapC + gapW / 2;
    function seg(s, e, ya, yb) {
      if (axis === 'x') wallSeg(parent, s, fixed, e, fixed, ya, yb, mat);
      else wallSeg(parent, fixed, s, fixed, e, ya, yb, mat);
    }
    if (gapW <= 0) { seg(from, to, y0, y1); return; }
    seg(from, a, y0, y1); seg(b, to, y0, y1); seg(a, b, y0 + gapH, y1);
  }

  /** Tạo InstancedMesh từ danh sách {x,y,z,sx,sy,sz,ry,rx,color} */
  function inst(parent, geo, mat, items) {
    if (!items.length) return null;
    var im = new T.InstancedMesh(geo, mat, items.length);
    var m = new T.Matrix4(), q = new T.Quaternion(), e = new T.Euler(), p = new T.Vector3(), s = new T.Vector3(), col = new T.Color();
    var hasColor = false;
    items.forEach(function (it, i) {
      e.set(it.rx || 0, it.ry || 0, it.rz || 0);
      q.setFromEuler(e);
      p.set(it.x, it.y, it.z);
      s.set(it.sx == null ? 1 : it.sx, it.sy == null ? 1 : it.sy, it.sz == null ? 1 : it.sz);
      m.compose(p, q, s);
      im.setMatrixAt(i, m);
      hasColor = true; im.setColorAt(i, col.set(it.color != null ? it.color : 0xffffff));
    });
    im.instanceMatrix.needsUpdate = true;
    if (hasColor && im.instanceColor) im.instanceColor.needsUpdate = true;
    im.frustumCulled = false;
    parent.add(im);
    return im;
  }

  function getGeo(key, make) { return GEO[key] || (GEO[key] = make()); }
  function unitBox() { return getGeo('ub', function () { return new T.BoxGeometry(1, 1, 1); }); }
  function flatQuad() { return getGeo('fq', function () { var g = new T.PlaneGeometry(1, 1); g.rotateX(-Math.PI / 2); return g; }); }

  /* ----- Biển báo / chữ ----- */
  function signTex(lines, bg, fg, w, h, border) {
    var key = lines.join('|') + bg + fg + w + h;
    if (TEXC[key]) return TEXC[key];
    var t = canvasTex(w, h, function (c) {
      c.fillStyle = bg; c.fillRect(0, 0, w, h);
      c.strokeStyle = border || fg; c.lineWidth = 6; c.strokeRect(5, 5, w - 10, h - 10);
      c.fillStyle = fg; c.textAlign = 'center'; c.textBaseline = 'middle';
      var n = lines.length, lh = (h - 20) / n;
      lines.forEach(function (ln, i) {
        var size = Math.floor(lh * (n > 1 && i > 0 ? 0.55 : 0.68));
        c.font = 'bold ' + size + 'px Arial, Helvetica, sans-serif';
        c.fillText(ln, w / 2, 10 + lh * (i + 0.5));
      });
    });
    TEXC[key] = t;
    return t;
  }
  function signMat(tex) {
    if (!tex.__p3rMat) tex.__p3rMat = new T.MeshBasicMaterial({ map: tex, side: T.FrontSide });
    return tex.__p3rMat;
  }

  /** Bảng treo trần 2 mặt. axis 'x': pháp tuyến ±x; 'z': pháp tuyến ±z. */
  function hangSign(parent, x, z, w, h, texPos, texNeg, axis, drop) {
    var g = new T.Group();
    var yc = CLEAR - (drop == null ? 0.55 : drop) - h / 2;
    var thick = 0.06;
    box(g, axis === 'x' ? thick : w, h, axis === 'x' ? w : thick, M.dark, 0, yc, 0);
    [-0.35, 0.35].forEach(function (k) {
      var rx = axis === 'x' ? 0 : k * w, rz = axis === 'x' ? k * w : 0;
      cyl(g, 0.012, CLEAR - yc - h / 2 + 0.02, M.steel, rx, (CLEAR + yc + h / 2) / 2, rz);
    });
    function face(tex, nrm) {
      var p = new T.Mesh(new T.PlaneGeometry(w - 0.04, h - 0.04), signMat(tex));
      var off = thick / 2 + 0.004;
      if (axis === 'x') { p.rotation.y = nrm > 0 ? Math.PI / 2 : -Math.PI / 2; p.position.set(nrm * off, yc, 0); }
      else { p.rotation.y = nrm > 0 ? 0 : Math.PI; p.position.set(0, yc, nrm * off); }
      g.add(p);
    }
    if (texPos) face(texPos, 1);
    if (texNeg) face(texNeg, -1);
    g.position.set(x, 0, z);
    parent.add(g);
    return g;
  }

  function arrowTex() {
    if (TEXC.arrow) return TEXC.arrow;
    TEXC.arrow = canvasTex(128, 256, function (c, w, h) {
      c.clearRect(0, 0, w, h); c.fillStyle = '#f0f0ea';
      c.beginPath(); c.moveTo(w / 2, 10); c.lineTo(w - 12, h * 0.5); c.lineTo(w * 0.66, h * 0.5);
      c.lineTo(w * 0.66, h - 10); c.lineTo(w * 0.34, h - 10); c.lineTo(w * 0.34, h * 0.5); c.lineTo(12, h * 0.5);
      c.closePath(); c.fill();
    });
    return TEXC.arrow;
  }
  /** Mũi tên sơn sàn. angle: 0 = -z, -PI/2 = +x, PI/2 = -x, PI = +z */
  function floorArrow(parent, x, y, z, rotY, w, l) {
    var mt = getGeo('arrowMat', function () {
      return new T.MeshBasicMaterial({ map: arrowTex(), transparent: true, polygonOffset: true, polygonOffsetFactor: -3, polygonOffsetUnits: -3, depthWrite: false });
    });
    var p = new T.Mesh(new T.PlaneGeometry(w || 1.1, l || 2.2), mt);
    p.rotation.set(-Math.PI / 2, 0, 0);
    var h = new T.Group(); h.add(p); h.rotation.y = rotY; h.position.set(x, y + 0.012, z);
    parent.add(h);
    return h;
  }

  /* ------------------------------------------------------------------ */
  /* XE Ô TÔ (InstancedMesh)                                             */
  /* ------------------------------------------------------------------ */
  var CAR_COLORS = [0xe8e8ea, 0x1d1f23, 0x9aa1ab, 0xc0392b, 0x2d5fa8, 0x2f7d5b, 0xd4b13a, 0x6b4f3a, 0x3b3f46, 0xf2f2f2];
  function buildCars(parent, slots, y) {
    var occ = slots.filter(function (s) { return s.status === 'OCCUPIED'; });
    if (!occ.length) return;
    var bodyG = getGeo('carBody', function () { var g = new T.BoxGeometry(1.8, 0.62, 4.4); g.translate(0, 0.62, 0); return g; });
    var cabG = getGeo('carCab', function () { var g = new T.BoxGeometry(1.58, 0.55, 2.35); g.translate(0, 1.2, -0.25); return g; });
    var wheelG = getGeo('carWheel', function () { var g = new T.CylinderGeometry(0.34, 0.34, 0.24, 14); g.rotateZ(Math.PI / 2); return g; });
    var tailG = getGeo('carTail', function () { return new T.BoxGeometry(0.42, 0.14, 0.05); });
    var plateG = getGeo('carPlate', function () { return new T.BoxGeometry(0.5, 0.12, 0.03); });
    var bodies = [], cabs = [], wheels = [], tails = [], plates = [];
    occ.forEach(function (s) {
      var ry = s.face > 0 ? Math.PI : 0;
      var sin = Math.sin(ry), cos = Math.cos(ry);
      function toW(lx, lz) { return { x: s.x + lx * cos + lz * sin, z: s.z - lx * sin + lz * cos }; }
      bodies.push({ x: s.x, y: y, z: s.z, ry: ry, color: s.carColor });
      cabs.push({ x: s.x, y: y, z: s.z, ry: ry, color: 0x1b2430 });
      [[-0.88, 1.35], [0.88, 1.35], [-0.88, -1.35], [0.88, -1.35]].forEach(function (wp) {
        var p = toW(wp[0], wp[1]); wheels.push({ x: p.x, y: y + 0.34, z: p.z, ry: ry });
      });
      [-0.6, 0.6].forEach(function (tx) { var p = toW(tx, -2.2); tails.push({ x: p.x, y: y + 0.85, z: p.z, ry: ry }); });
      var pp = toW(0, -2.21); plates.push({ x: pp.x, y: y + 0.6, z: pp.z, ry: ry });
    });
    inst(parent, bodyG, M.car, bodies);
    inst(parent, cabG, M.carGlass, cabs);
    inst(parent, wheelG, M.tire, wheels);
    inst(parent, tailG, M.tail, tails);
    inst(parent, plateG, M.plate, plates);
  }

  root.__P3R_INTERNAL__ = {
    setThree: function (three) { T = three; },
    C: { FH: FH, CLEAR: CLEAR, SLAB: SLAB, EYE: EYE },
    LOTS: LOTS,
    M: M, initMaterials: initMaterials,
    util: { rng: rng, pad2: pad2, clamp: clamp, canvasTex: canvasTex, box: box, cyl: cyl, texPlane: texPlane, floorRect: floorRect, wallSeg: wallSeg, wallWithGap: wallWithGap, inst: inst, getGeo: getGeo, unitBox: unitBox, flatQuad: flatQuad, signTex: signTex, signMat: signMat, hangSign: hangSign, floorArrow: floorArrow, buildCars: buildCars, CAR_COLORS: CAR_COLORS, arrowTex: arrowTex }
  };
})(typeof self !== 'undefined' ? self : this);
