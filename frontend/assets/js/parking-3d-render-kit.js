/**
 * ==========================================================
 * SmartPark 3D Render (Three.js) - Hầm / Bãi đỗ xe cao tầng
 *
 * Đồ hoạ hầm ngầm & model xe nâng cấp theo mẫu CGTrader:
 *   - Underground Parking Lot Interior: sàn bê tông đánh bóng có khe co giãn,
 *     tường & cột sơn 2 tông (xám bê tông + dải trắng + chân xanh rêu đậm),
 *     hệ thống dầm bê tông + ống thông gió tôn mạ kẽm vuông chạy vòng trên trần,
 *     đèn máng huỳnh quang đôi, chặn lùi bánh xe (wheel-stop), tủ PCCC.
 *   - Model xe ô tô low-poly hiện đại: thân xe vát khí động học (nắp capo dốc,
 *     kính chắn gió nghiêng, cột A/B/C, mâm đúc hợp kim, đèn pha LED, đèn hậu, gương chiếu hậu).
 * ==========================================================
 */
(function (root) {
  'use strict';

  /* ------------------------------------------------------------------ */
  /* HẰNG SỐ CHUNG (mét)                                                 */
  /* ------------------------------------------------------------------ */
  var FH = 4.8;          // cao độ giữa 2 tầng
  var CLEAR = 4.2;       // chiều cao thông thủy
  var SLAB = 0.6;        // dày sàn
  var EYE = 1.65;

  /* ------------------------------------------------------------------ */
  /* DANH SÁCH BÃI MẪU                                                   */
  /* ------------------------------------------------------------------ */
  var LOTS = []; // Definitions are supplied exclusively by the Core scene API.

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
      ctx.fillStyle = 'rgba(' + v + ',' + v + ',' + v + ',0.14)';
      ctx.fillRect(Math.random() * w, Math.random() * h, 2, 2);
    }
  }

  function initMaterials() {
    if (M.floor) return;
    // Sàn bê tông mài bóng có khe co giãn (giống mẫu CGTrader Underground Parking Lot Interior)
    var tAsphalt = canvasTex(512, 512, function (c, w, h) {
      noiseFill(c, w, h, '#484d54', 90, 14000);
      // Vệt bóng / mài mòn tự nhiên trên mặt bê tông
      for (var k = 0; k < 28; k++) {
        var gx = Math.random() * w, gy = Math.random() * h, gr = 35 + Math.random() * 80;
        var g = c.createRadialGradient(gx, gy, 2, gx, gy, gr);
        g.addColorStop(0, k % 2 ? 'rgba(95,103,112,0.18)' : 'rgba(32,35,40,0.22)');
        g.addColorStop(1, 'rgba(72,77,84,0)');
        c.fillStyle = g; c.fillRect(0, 0, w, h);
      }
      // Khe cắt co giãn bê tông (grid 2x2)
      c.strokeStyle = 'rgba(24,27,31,0.65)'; c.lineWidth = 3;
      c.beginPath();
      c.moveTo(0, 1.5); c.lineTo(w, 1.5);
      c.moveTo(0, h / 2); c.lineTo(w, h / 2);
      c.moveTo(1.5, 0); c.lineTo(1.5, h);
      c.moveTo(w / 2, 0); c.lineTo(w / 2, h);
      c.stroke();
      c.strokeStyle = 'rgba(120,128,138,0.22)'; c.lineWidth = 1.5;
      c.beginPath();
      c.moveTo(0, 4); c.lineTo(w, 4);
      c.moveTo(4, 0); c.lineTo(4, h);
      c.stroke();
    }, true);

    // Bê tông cột & dầm
    var tConcrete = canvasTex(256, 256, function (c, w, h) {
      noiseFill(c, w, h, '#6b7179', 70, 6500);
      c.strokeStyle = 'rgba(38,42,46,0.42)'; c.lineWidth = 2;
      c.strokeRect(1, 1, w - 2, h - 2);
    }, true);

    // Tường hầm 2 tông màu (trên bê tông xám, giữa kẻ sọc trắng, dưới xanh rêu/xám đá đậm như mẫu CGTrader)
    var tWall = canvasTex(512, 512, function (c, w, h) {
      noiseFill(c, w, h, '#6e747c', 65, 9000);
      // Chân tường sơn xanh rêu đậm (wainscot)
      c.fillStyle = '#223a35'; c.fillRect(0, Math.floor(h * 0.58), w, Math.ceil(h * 0.42));
      for (var i = 0; i < 3200; i++) {
        var v = Math.floor(40 + Math.random() * 30);
        c.fillStyle = 'rgba(' + v + ',' + (v + 18) + ',' + (v + 14) + ',0.14)';
        c.fillRect(Math.random() * w, h * 0.58 + Math.random() * h * 0.42, 2, 2);
      }
      // Dải sơn trắng phản quang phân tách
      c.fillStyle = '#dfe5ec'; c.fillRect(0, Math.floor(h * 0.53), w, Math.floor(h * 0.055));
      c.fillStyle = '#182421'; c.fillRect(0, Math.floor(h * 0.525), w, 3);
      c.fillRect(0, Math.floor(h * 0.585), w, 3);
      // Chân đế đen sát sàn
      c.fillStyle = '#171a1e'; c.fillRect(0, Math.floor(h * 0.93), w, Math.ceil(h * 0.07));
      // Khe ván khuôn đứng
      c.strokeStyle = 'rgba(30,34,38,0.38)'; c.lineWidth = 2;
      c.beginPath();
      c.moveTo(1, 0); c.lineTo(1, h);
      c.moveTo(w / 2, 0); c.lineTo(w / 2, h);
      c.moveTo(0, Math.floor(h * 0.26)); c.lineTo(w, Math.floor(h * 0.26));
      c.stroke();
    }, true);

    var tCeil = canvasTex(256, 256, function (c, w, h) {
      noiseFill(c, w, h, '#525860', 60, 6000);
      c.strokeStyle = 'rgba(32,36,40,0.38)'; c.lineWidth = 2;
      c.strokeRect(1, 1, w - 2, h - 2);
    }, true);

    var tDuct = canvasTex(256, 128, function (c, w, h) {
      var g = c.createLinearGradient(0, 0, 0, h);
      g.addColorStop(0, '#9aa2ab'); g.addColorStop(0.5, '#b8c0c8'); g.addColorStop(1, '#7f8790');
      c.fillStyle = g; c.fillRect(0, 0, w, h);
      c.strokeStyle = 'rgba(45,50,56,0.55)'; c.lineWidth = 3;
      c.strokeRect(2, 2, w - 4, h - 4);
      c.beginPath(); c.moveTo(w / 2, 0); c.lineTo(w / 2, h); c.stroke();
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

    M.floor = new T.MeshStandardMaterial({ map: tAsphalt, roughness: 0.52, metalness: 0.08 });
    M.slab = new T.MeshStandardMaterial({ map: tCeil, roughness: 0.88, side: T.DoubleSide });
    M.conc = new T.MeshStandardMaterial({ map: tConcrete, roughness: 0.85 });
    M.beam = new T.MeshStandardMaterial({ map: tConcrete, color: 0x8d949c, roughness: 0.82 });
    M.wall = new T.MeshStandardMaterial({ map: tWall, roughness: 0.82, side: T.DoubleSide });
    M.pillarCol = new T.MeshStandardMaterial({ color: 0x223a35, roughness: 0.72 });
    M.ceil = new T.MeshStandardMaterial({ map: tCeil, roughness: 0.9, side: T.DoubleSide });
    M.hazard = new T.MeshStandardMaterial({ map: tHazard, roughness: 0.75 });
    M.plaster = new T.MeshStandardMaterial({ color: 0xdce1e7, roughness: 0.8 });
    M.steel = new T.MeshStandardMaterial({ color: 0xc2cad2, roughness: 0.28, metalness: 0.82 });
    M.dark = new T.MeshStandardMaterial({ color: 0x1c2026, roughness: 0.55, metalness: 0.45 });
    M.duct = new T.MeshStandardMaterial({ map: tDuct, roughness: 0.35, metalness: 0.62 });
    M.pipeRed = new T.MeshStandardMaterial({ color: 0xc62828, roughness: 0.45, metalness: 0.35 });
    M.pipeBlue = new T.MeshStandardMaterial({ color: 0x1e5bb8, roughness: 0.45, metalness: 0.35 });
    M.tray = new T.MeshStandardMaterial({ color: 0x6b7078, roughness: 0.45, metalness: 0.6 });
    M.lamp = new T.MeshBasicMaterial({ color: 0xf4f9ff });
    M.white = new T.MeshBasicMaterial({ color: 0xf4f4f0 });
    M.yellow = new T.MeshBasicMaterial({ color: 0xf2c200 });
    M.paintWhite = new T.MeshBasicMaterial({ color: 0xe8e8e2, polygonOffset: true, polygonOffsetFactor: -2, polygonOffsetUnits: -2 });
    M.paintYellow = new T.MeshBasicMaterial({ color: 0xf2c200, polygonOffset: true, polygonOffsetFactor: -2, polygonOffsetUnits: -2 });
    M.paintBlue = new T.MeshBasicMaterial({ color: 0x4aa3ff, polygonOffset: true, polygonOffsetFactor: -2, polygonOffsetUnits: -2 });
    M.paintGreen = new T.MeshBasicMaterial({ color: 0x2fcf6a, polygonOffset: true, polygonOffsetFactor: -3, polygonOffsetUnits: -3 });
    M.glass = new T.MeshStandardMaterial({ color: 0xa8dcff, transparent: true, opacity: 0.22, roughness: 0.05, metalness: 0.25, side: T.DoubleSide, depthWrite: false });
    M.frame = new T.MeshStandardMaterial({ color: 0x7c858e, roughness: 0.38, metalness: 0.8 });
    M.red = new T.MeshStandardMaterial({ color: 0xd32f2f, roughness: 0.48 });
    M.green = new T.MeshBasicMaterial({ color: 0x19c45a });
    M.exitGreen = new T.MeshBasicMaterial({ color: 0x0fa44a });
    M.barrier = new T.MeshStandardMaterial({ map: tBarrier, roughness: 0.45 });
    M.cabin = new T.MeshStandardMaterial({ color: 0xfff1cf, emissive: 0xffe9b5, emissiveIntensity: 0.9, roughness: 0.6 });
    M.glow = new T.MeshBasicMaterial({ color: 0xffffff });
    M.sky = new T.MeshBasicMaterial({ color: 0xcfe8ff, side: T.DoubleSide });
    M.car = new T.MeshStandardMaterial({ color: 0xffffff, roughness: 0.28, metalness: 0.22 });
    M.carGlass = new T.MeshStandardMaterial({ color: 0x131c27, roughness: 0.14, metalness: 0.38 });
    M.carGrille = new T.MeshStandardMaterial({ color: 0x16191e, roughness: 0.6, metalness: 0.3 });
    M.carRim = new T.MeshStandardMaterial({ color: 0xd8dee6, roughness: 0.25, metalness: 0.82 });
    M.carHead = new T.MeshBasicMaterial({ color: 0xf4f9ff });
    M.tire = new T.MeshStandardMaterial({ color: 0x15171a, roughness: 0.88 });
    M.tail = new T.MeshBasicMaterial({ color: 0xff2828 });
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
    var f = texPlane(w, d, M.floor, w / 5, d / 5);
    f.rotation.x = -Math.PI / 2;
    f.position.set((x0 + x1) / 2, y + 0.002, (z0 + z1) / 2);
    parent.add(f);
    if (withSlab !== false) box(parent, w, SLAB, d, M.slab, (x0 + x1) / 2, y - SLAB / 2 - 0.004, (z0 + z1) / 2);
  }
  function wallSeg(parent, ax, az, bx, bz, y0, y1, mat) {
    var dx = bx - ax, dz = bz - az, len = Math.sqrt(dx * dx + dz * dz), h = y1 - y0;
    if (len < 0.01 || h < 0.01) return null;
    var vRep = (y0 <= 0.05 && y1 >= CLEAR - 0.2) ? 1 : h / 3;
    var m = texPlane(len, h, mat || M.wall, len / 4, vRep);
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
        var size = Math.floor(lh * (n > 1 && i > 0 ? 0.54 : 0.66));
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
    var yc = CLEAR - (drop == null ? 0.62 : drop) - h / 2;
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
  /* XE Ô TÔ 3D LOW-POLY HIỆN ĐẠI (theo mẫu CGTrader Parking Lot)         */
  /* ------------------------------------------------------------------ */
  var CAR_COLORS = [
    0xf2f4f7, // trắng ngọc trai
    0x1b1e24, // đen bóng
    0x8e96a0, // bạc kim loại
    0xc8232c, // đỏ thể thao
    0x1d56a5, // xanh dương đậm
    0x276b4c, // xanh lục bảo
    0xd99b16, // vàng cát / cam vàng
    0x46515e, // xám titan
    0x6d3b2e, // nâu đồng
    0x2c82c9  // xanh dương sáng
  ];

  function profileGeo(pts, width, bevel) {
    var sh = new T.Shape();
    sh.moveTo(pts[0][0], pts[0][1]);
    for (var i = 1; i < pts.length; i++) sh.lineTo(pts[i][0], pts[i][1]);
    sh.closePath();
    var b = bevel == null ? 0.06 : bevel;
    var g = new T.ExtrudeGeometry(sh, {
      depth: width - b * 2,
      bevelEnabled: b > 0,
      bevelThickness: b,
      bevelSize: b * 0.7,
      bevelSegments: 2,
      curveSegments: 4
    });
    // Shape plane (X=z_car, Y=y_car), extrude along Z -> rotate so extrude is along X
    g.rotateY(Math.PI / 2);
    g.computeBoundingBox();
    var cx = (g.boundingBox.min.x + g.boundingBox.max.x) / 2;
    g.translate(-cx, 0, 0);
    g.computeVertexNormals();
    return g;
  }

  function buildCars(parent, slots, y) {
    var occ = slots.filter(function (s) { return s.status === 'OCCUPIED'; });
    if (!occ.length) return;

    // Thân dưới + mui xe + cốp vát khí động học (mũi xe tại +z = +2.22, đuôi xe tại -z = -2.22)
    var bodyG = getGeo('carBodyV2', function () {
      return profileGeo([
        [-2.20, 0.28],
        [-2.14, 0.22],
        [ 2.12, 0.22],
        [ 2.24, 0.30],
        [ 2.22, 0.68],
        [ 2.02, 0.78],
        [ 0.98, 0.88],
        [-1.65, 0.90],
        [-2.16, 0.88],
        [-2.22, 0.66]
      ], 1.84, 0.07);
    });

    // Khung mui + cột A/B/C đồng màu sơn xe
    var roofG = getGeo('carRoofV2', function () {
      return profileGeo([
        [-1.62, 0.86],
        [ 0.92, 0.86],
        [ 0.32, 1.42],
        [-1.08, 1.42]
      ], 1.54, 0.04);
    });

    // Khối kính cabin màu đen xanh (nhô nhẹ ra 2 bên và trước/sau để tạo kính chắn gió + cửa sổ bên)
    var cabG = getGeo('carCabV2', function () {
      return profileGeo([
        [-1.66, 0.88],
        [ 0.96, 0.88],
        [ 0.35, 1.38],
        [-1.12, 1.38]
      ], 1.58, 0.02);
    });

    // Cột B chia cửa trước / cửa sau
    var bPillarG = getGeo('carBPillarV2', function () {
      var g = new T.BoxGeometry(1.60, 0.48, 0.10);
      g.translate(0, 1.13, -0.28);
      return g;
    });

    var wheelG = getGeo('carWheelV2', function () {
      var g = new T.CylinderGeometry(0.35, 0.35, 0.24, 18);
      g.rotateZ(Math.PI / 2);
      return g;
    });
    var rimG = getGeo('carRimV2', function () {
      var g = new T.CylinderGeometry(0.22, 0.22, 0.255, 12);
      g.rotateZ(Math.PI / 2);
      return g;
    });
    var grilleG = getGeo('carGrilleV2', function () { return new T.BoxGeometry(0.96, 0.22, 0.05); });
    var headG = getGeo('carHeadV2', function () { return new T.BoxGeometry(0.34, 0.12, 0.06); });
    var tailG = getGeo('carTailV2', function () { return new T.BoxGeometry(0.46, 0.13, 0.05); });
    var plateG = getGeo('carPlateV2', function () { return new T.BoxGeometry(0.48, 0.12, 0.03); });
    var mirrorG = getGeo('carMirrorV2', function () { return new T.BoxGeometry(0.18, 0.11, 0.14); });

    var bodies = [], roofs = [], cabs = [], bPillars = [], wheels = [], rims = [], grilles = [], heads = [], tails = [], plates = [], mirrors = [];
    occ.forEach(function (s, idx) {
      // Mũi xe hướng ra lối đi: face > 0 là lối đi ở +z (ry = 0), face < 0 là lối đi ở -z (ry = PI)
      var ry = s.face > 0 ? 0 : Math.PI;
      var sin = Math.sin(ry), cos = Math.cos(ry);
      // Cỡ xe biến thiên nhẹ (Sedan / SUV / Crossover)
      var isSuv = (idx + s.idx) % 3 === 0;
      var sy = isSuv ? 1.09 : 1.0;
      function toW(lx, lz) { return { x: s.x + lx * cos + lz * sin, z: s.z - lx * sin + lz * cos }; }

      bodies.push({ x: s.x, y: y, z: s.z, ry: ry, sy: sy, color: s.carColor });
      roofs.push({ x: s.x, y: y, z: s.z, ry: ry, sy: sy, color: s.carColor });
      cabs.push({ x: s.x, y: y, z: s.z, ry: ry, sy: sy, color: 0x131c27 });
      bPillars.push({ x: s.x, y: y, z: s.z, ry: ry, sy: sy, color: s.carColor });

      [[-0.86, 1.34], [0.86, 1.34], [-0.86, -1.34], [0.86, -1.34]].forEach(function (wp) {
        var p = toW(wp[0], wp[1]);
        wheels.push({ x: p.x, y: y + 0.35, z: p.z, ry: ry });
        rims.push({ x: p.x, y: y + 0.35, z: p.z, ry: ry });
      });
      [-0.88, 0.88].forEach(function (mx) {
        var pm = toW(mx, 0.62);
        mirrors.push({ x: pm.x, y: y + 0.94 * sy, z: pm.z, ry: ry, color: s.carColor });
      });
      var pg = toW(0, 2.23);
      grilles.push({ x: pg.x, y: y + 0.48 * sy, z: pg.z, ry: ry });
      [-0.64, 0.64].forEach(function (hx) {
        var ph = toW(hx, 2.21);
        heads.push({ x: ph.x, y: y + 0.62 * sy, z: ph.z, ry: ry });
      });
      [-0.62, 0.62].forEach(function (tx) {
        var pt = toW(tx, -2.21);
        tails.push({ x: pt.x, y: y + 0.72 * sy, z: pt.z, ry: ry });
      });
      var ppRear = toW(0, -2.22), ppFront = toW(0, 2.24);
      plates.push({ x: ppRear.x, y: y + 0.48 * sy, z: ppRear.z, ry: ry });
      plates.push({ x: ppFront.x, y: y + 0.32 * sy, z: ppFront.z, ry: ry });
    });

    inst(parent, bodyG, M.car, bodies);
    inst(parent, roofG, M.car, roofs);
    inst(parent, cabG, M.carGlass, cabs);
    inst(parent, bPillarG, M.car, bPillars);
    inst(parent, mirrorG, M.car, mirrors);
    inst(parent, wheelG, M.tire, wheels);
    inst(parent, rimG, M.carRim, rims);
    inst(parent, grilleG, M.carGrille, grilles);
    inst(parent, headG, M.carHead, heads);
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
