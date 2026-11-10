/**
 * SmartPark 3D Render - bộ dựng TẦNG / BÃI (floor + lot builders) - Layout v2
 * Phụ thuộc: parking-3d-render-kit.js (vật liệu, tiện ích, xe)
 *
 * NGUYÊN TẮC LAYOUT (theo sơ đồ của nhóm):
 *  - Tầng ĐẦU TIÊN (B1 hoặc T1): chỉ dành cho xe THÁNG.
 *      Mọi xe đi vào bằng 1 dốc duy nhất ở phía Tây. Booth CHECK-IN nằm phía trong làn xe:
 *      xe tháng rẽ vào quẹt thẻ; xe vãng lai chạy tiếp theo làn riêng (viền đỏ, mũi tên xanh dương)
 *      dọc cạnh Nam để tới dốc xuống tầng 2. Xe tháng ra bằng booth CHECK-OUT phía Đông, vào làn
 *      vàng; xe vãng lai từ B2 lên đi làn xanh lá; 2 làn ngăn bằng cọc tiêu rồi dùng chung dốc lên mặt đất.
 *  - Các tầng sau (B2, B3.. / T2, T3..): xe VÃNG LAI, không quẹt thẻ lại. Các tầng xếp CHỒNG song
 *      song, dốc kiểu "zig-zag" (switchback) đặt sát nhau ở phía Đông: xuống xong là tới ngay dốc
 *      kế tiếp, có bảng số slot trống. Booth CHECK-OUT xe vãng lai đặt tại B2 (T2) trên đường lên.
 *  - Thang máy nằm trong phòng riêng, vách kín + cửa kính tự động, ngăn cách khỏi khu đỗ xe.
 */
(function (root) {
  'use strict';
  var I = root.__P3R_INTERNAL__;
  var U = I.util, M = I.M, FH = I.C.FH, CLEAR = I.C.CLEAR, SLAB = I.C.SLAB;
  var T;

  /* ------------------------------------------------------------------ */
  /* HÌNH HỌC BỐ CỤC (mét)                                               */
  /* ------------------------------------------------------------------ */
  var SW = 2.5, SL = 5, GAPW = 5.6, AIS = 6;
  var WM = 11, XSR = 25.6, ECL = 5, COR = 9.2;
  var HX = (WM + XSR + ECL + COR) / 2, HZ = 23;                 // nửa dài / nửa rộng sảnh
  var XS0 = -HX + WM, GAPL = XS0 + 4 * SW, GAPR = GAPL + GAPW, XE = XS0 + XSR, GX = GAPL + GAPW / 2;
  var XR = XE + ECL, XY = XR + 2.6, XB = XR + 4.8, XG = XR + 7.0;   // lề đỏ, làn vàng, cọc tiêu, làn xanh lá
  var BX = -HX + 8;                                             // làn Nam-Bắc phía Tây (xe tháng sau check-in)
  var XEC = XE + ECL / 2;                                       // làn ngang phía Đông
  var RL = 24, LAND = 8, XT = HX + RL + LAND;                   // dốc, bãi chuyển hướng, mép Đông tháp dốc
  var ROWS = [
    { id: 'A', zc: -12.5, face: 1 }, { id: 'B', zc: -1.5, face: 1 },
    { id: 'C', zc: 9.5, face: -1 }, { id: 'D', zc: 20.5, face: -1 }
  ];
  var AZ = [-7, 4, 15];
  var ZD1 = -20.5, ZU1 = -16.5, ZD2 = -8, ZU2 = -12, ZCW = -0.8, ZCE = -3.2;   // các làn dốc / connector
  var ZEG = 16.5, ZEY = 20.5;                                                   // làn ra xanh lá / vàng
  var XD = HX + RL + 6, XS_ = HX + RL + 3;                                      // nhánh Bắc (xuống) / Nam (lên) ở bãi chuyển hướng
  var XJD = HX - 9, XJS = HX - 5;                                               // quay đầu tại sảnh các tầng chẵn
  var TS = [[-HZ, -14.25], [-14.25, -5.5], [-5.5, 1]];                          // 3 dải của tháp dốc (băng 1, băng 2, connector)
  var COLX = [GAPL - 10, GAPL - 5, GAPL, GAPR, GAPR + 5, GAPR + 10];

  var LAYOUT = { HX: HX, HZ: HZ, XT: XT, RL: RL, BX: BX, XR: XR, XG: XG, XY: XY, GX: GX, XEC: XEC };
  I.layout = LAYOUT;

  function slotX(i, n) {
    var l = Math.ceil(n / 2);
    return i <= l ? GAPL - (l - i + 0.5) * SW : GAPR + (i - l - 0.5) * SW;
  }

  /* ---------- vật liệu bổ sung ---------- */
  function extraMaterials() {
    if (M.paintInst) return;
    M.paintInst = new T.MeshBasicMaterial({ color: 0xffffff, polygonOffset: true, polygonOffsetFactor: -2, polygonOffsetUnits: -2 });
    M.sepInst = new T.MeshStandardMaterial({ color: 0xffffff, roughness: 0.7 });
    M.pad = new T.MeshBasicMaterial({ color: 0xffffff, transparent: true, opacity: 0.3, depthWrite: false, polygonOffset: true, polygonOffsetFactor: -1, polygonOffsetUnits: -1 });
    M.fin = new T.MeshStandardMaterial({ color: 0xd9dadc, roughness: 0.8 });
    M.tile = new T.MeshStandardMaterial({ color: 0xd8d3c8, roughness: 0.5 });
    M.kerbRed = new T.MeshStandardMaterial({ color: 0xc62828, roughness: 0.7 });
    M.bollard = new T.MeshStandardMaterial({ color: 0xffffff, roughness: 0.5 });
  }

  /* ---------- tiện ích dựng ---------- */
  function b3(p, w, h, d, m, x, y, z) { return U.box(p, w, h, d, m, x, y, z); }

  var ARROW_TEX = {};
  function colorArrowMat(color) {
    if (ARROW_TEX[color]) return ARROW_TEX[color];
    var tex = U.canvasTex(128, 256, function (c, w, h) {
      c.clearRect(0, 0, w, h); c.fillStyle = color;
      c.beginPath(); c.moveTo(w / 2, 10); c.lineTo(w - 12, h * 0.5); c.lineTo(w * 0.66, h * 0.5);
      c.lineTo(w * 0.66, h - 10); c.lineTo(w * 0.34, h - 10); c.lineTo(w * 0.34, h * 0.5); c.lineTo(12, h * 0.5);
      c.closePath(); c.fill();
    });
    ARROW_TEX[color] = new T.MeshBasicMaterial({ map: tex, transparent: true, polygonOffset: true, polygonOffsetFactor: -3, polygonOffsetUnits: -3, depthWrite: false });
    return ARROW_TEX[color];
  }
  /** Mũi tên sơn màu trên sàn. rotY: 0 = -z, -PI/2 = +x, PI/2 = -x, PI = +z */
  function arrowC(parent, x, y, z, rotY, color, w, l) {
    var p = new T.Mesh(new T.PlaneGeometry(w || 1.2, l || 2.4), colorArrowMat(color));
    p.rotation.set(-Math.PI / 2, 0, 0);
    var h = new T.Group(); h.add(p); h.rotation.y = rotY; h.position.set(x, y + 0.014, z);
    parent.add(h);
  }
  /** Chữ sơn trên sàn (đọc được từ hướng xe chạy) */
  function floorText(parent, x, z, rotY, w, l, lines, bg, fg) {
    var tex = U.signTex(lines, bg, fg, 512, Math.round(512 * l / w));
    var m = new T.MeshBasicMaterial({ map: tex, transparent: true, polygonOffset: true, polygonOffsetFactor: -4, polygonOffsetUnits: -4 });
    var p = new T.Mesh(new T.PlaneGeometry(w, l), m);
    p.rotation.set(-Math.PI / 2, 0, 0);
    var h = new T.Group(); h.add(p); h.rotation.y = rotY; h.position.set(x, 0.016, z);
    parent.add(h);
  }
  var ROT_E = -Math.PI / 2, ROT_W = Math.PI / 2, ROT_N = Math.PI, ROT_S = 0;   // hướng mũi tên: đi về +x / -x / +z / -z

  /** tam giác tường chắn hào dốc */
  function triWall(parent, xa, xb, yBa, yBb, yTop, z) {
    var sh = new T.Shape();
    sh.moveTo(xa, yBa); sh.lineTo(xb, yBb); sh.lineTo(xb, yTop); sh.lineTo(xa, yTop); sh.closePath();
    var g = new T.ShapeGeometry(sh), uv = g.attributes.uv;
    for (var i = 0; i < uv.count; i++) uv.setXY(i, uv.getX(i) / 3, uv.getY(i) / 3);
    var m = new T.Mesh(g, M.wall); m.position.z = z; parent.add(m);
  }

  /* ---------- tường (đặc / lan can) có ô cửa ---------- */
  function wallLine(ctx, axis, fixed, from, to, gaps, ext) {
    var g = ctx.g, solid = !ext || ctx.under;
    var gs = (gaps || []).slice().sort(function (a, b) { return a.c - b.c; });
    var segs = [], cur = from;
    gs.forEach(function (q) { var a = q.c - q.w / 2, b = q.c + q.w / 2; if (a > cur) segs.push([cur, a]); cur = b; });
    if (cur < to) segs.push([cur, to]);
    function line(s, e, y0, y1) { if (axis === 'x') U.wallSeg(g, s, fixed, e, fixed, y0, y1, M.wall); else U.wallSeg(g, fixed, s, fixed, e, y0, y1, M.wall); }
    segs.forEach(function (sg) {
      if (solid) line(sg[0], sg[1], 0, CLEAR);
      else {
        var len = sg[1] - sg[0], mid = (sg[0] + sg[1]) / 2, PH = 1.1;
        if (axis === 'x') U.box(g, len, PH, 0.3, M.conc, mid, PH / 2, fixed); else U.box(g, 0.3, PH, len, M.conc, fixed, PH / 2, mid);
        for (var f = sg[0] + 0.6; f < sg[1] - 0.3; f += 1.5) {
          var fh = CLEAR - PH;
          ctx.fins.push(axis === 'x' ? { x: f, y: PH + fh / 2, z: fixed, sx: 0.12, sy: fh, sz: 0.34 } : { x: fixed, y: PH + fh / 2, z: f, sx: 0.34, sy: fh, sz: 0.12 });
        }
      }
    });
    if (solid) gs.forEach(function (q) {
      var a = q.c - q.w / 2, b = q.c + q.w / 2;
      line(a, b, q.h || 3.6, CLEAR);
    });
  }

  /* ================================================================== */
  /*  PHÒNG THANG MÁY RIÊNG (vách kín + cửa kính tự động)                */
  /* ================================================================== */
  function buildLobby(fl, o) {
    var g = new T.Group();
    g.position.set(o.cx, 0, o.cz);
    g.rotation.y = Math.atan2(o.fx, o.fz);           // local +z hướng ra cửa
    var W = 5.2, H = CLEAR, D = 5.0;
    b3(g, W, H, 1.6, M.plaster, 0, H / 2, 0.8);        // khối giếng thang
    b3(g, 0.2, H, D, M.plaster, -W / 2 + 0.1, H / 2, D / 2);
    b3(g, 0.2, H, D, M.plaster, W / 2 - 0.1, H / 2, D / 2);
    b3(g, W, 0.14, D + 0.1, M.plaster, 0, H - 0.07, D / 2);
    b3(g, 0.8, H, 0.2, M.plaster, -W / 2 + 0.5, H / 2, D - 0.1);      // cột 2 bên mặt tiền
    b3(g, 0.8, H, 0.2, M.plaster, W / 2 - 0.5, H / 2, D - 0.1);
    var cab = new T.Mesh(new T.PlaneGeometry(2.0, 2.3), M.cabin); cab.position.set(0, 1.2, 1.61); g.add(cab);
    var slide = [];
    function slider(mesh, base, dist) { g.add(mesh); slide.push({ mesh: mesh, base: base, dir: base < 0 ? -1 : 1, dist: dist }); }
    [-0.5, 0.5].forEach(function (x) { slider(U.box(null, 1.0, 2.3, 0.05, M.steel, x, 1.2, 1.66), x, 0.95); });
    b3(g, 2.2, 0.14, 0.2, M.frame, 0, 2.42, 1.7);
    b3(g, 0.16, 0.36, 0.05, M.dark, 1.7, 1.15, 1.64);
    b3(g, 0.07, 0.07, 0.02, M.green, 1.7, 1.25, 1.67);
    b3(g, 0.07, 0.07, 0.02, M.tail, 1.7, 1.07, 1.67);
    var tile = new T.Mesh(new T.PlaneGeometry(W, D - 1.6), M.tile); tile.rotation.x = -Math.PI / 2; tile.position.set(0, 0.012, 1.6 + (D - 1.6) / 2); g.add(tile);
    b3(g, 1.8, 0.04, 0.3, M.lamp, 0, H - 0.17, 3.2);
    // mặt tiền kính: 2 tấm cố định, 2 cánh cửa trượt tự động, ô thoáng
    function pane(w, h, x, y, z) { var m = new T.Mesh(new T.PlaneGeometry(w, h), M.glass); m.position.set(x, y, z); return m; }
    g.add(pane(0.9, 3.9, -1.35, 1.95, D - 0.02)); g.add(pane(0.9, 3.9, 1.35, 1.95, D - 0.02));
    g.add(pane(1.8, 1.7, 0, 3.35, D - 0.02));
    [-0.45, 0.45].forEach(function (x) {
      var door = new T.Group(); door.add(pane(0.9, 2.5, 0, 1.25, 0)); b3(door, 0.9, 0.05, 0.05, M.frame, 0, 2.5, 0); b3(door, 0.05, 2.5, 0.05, M.frame, x < 0 ? -0.44 : 0.44, 1.25, 0);
      door.position.set(x, 0, D - 0.04);
      slider(door, x, 0.9);
    });
    [-1.8, 0, 1.8].forEach(function (x) { b3(g, 0.06, 3.95, 0.06, M.frame, x, 1.975, D - 0.03); });
    b3(g, 3.8, 0.06, 0.06, M.frame, 0, 3.9, D - 0.03);
    var dm = new T.Mesh(new T.PlaneGeometry(1.3, 1.6), M.paintGreen); dm.rotation.x = -Math.PI / 2; dm.position.set(0, 0.02, D + 0.8); g.add(dm);
    fl.group.add(g);
    var s = Math.sin(g.rotation.y), c = Math.cos(g.rotation.y);
    var pos = new T.Vector3(o.cx + s * 3.0, fl.y, o.cz + c * 3.0);
    fl.lobbies.push({ id: o.id, name: o.name, pos: pos, slide: slide, open: 0, fx: o.fx, fz: o.fz, lane: o.lane, floorIdx: fl.k, group: g });
  }

  /* ================================================================== */
  /*  BOOTH QUẸT THẺ + BARRIER                                            */
  /* ================================================================== */
  function buildGate(parent, fl, o) {
    var g = new T.Group(); g.position.set(o.x, 0, o.z);
    g.rotation.y = Math.atan2(-o.dir[1], o.dir[0]);       // local +x = hướng xe chạy
    var sg = o.side || 1;
    b3(g, 5.4, 0.18, 1.0, M.conc, 0, 0.09, 2.15 * sg);
    b3(g, 5.4, 0.04, 1.02, M.hazard, 0, 0.19, 2.15 * sg);
    b3(g, 0.42, 1.05, 0.38, M.red, 0, 0.72, 2.05 * sg);
    b3(g, 0.44, 0.06, 0.4, M.dark, 0, 1.27, 2.05 * sg);
    var redM = new T.MeshBasicMaterial({ color: 0xff2a2a }), grM = new T.MeshBasicMaterial({ color: 0x0b3d1d });
    b3(g, 0.1, 0.1, 0.02, redM, 0, 1.12, 1.85 * sg); b3(g, 0.1, 0.1, 0.02, grM, 0, 0.96, 1.85 * sg);
    var lift = new T.Group(); lift.position.set(0, 1.12, 1.78 * sg); g.add(lift);
    var arm = U.box(lift, 0.1, 0.11, 3.4, M.barrier, 0, 0, -1.7 * sg);
    var rd = new T.Group(); rd.position.set(-1.9, 0, 2.1 * sg); g.add(rd);
    b3(rd, 0.3, 1.1, 0.3, M.dark, 0, 0.73, 0);
    b3(rd, 0.34, 0.06, 0.34, M.steel, 0, 1.31, 0);
    var scr = new T.Mesh(new T.PlaneGeometry(0.26, 0.17), U.signMat(U.signTex([o.readerLine || 'QUẸT THẺ', 'TAP CARD'], '#0a1a2a', '#6fe3ff', 256, 160)));
    scr.position.set(0, 1.1, -0.155 * sg); scr.rotation.y = sg > 0 ? Math.PI : 0; rd.add(scr);
    b3(rd, 0.16, 0.05, 0.02, M.green, 0, 0.85, -0.16 * sg);
    b3(rd, 0.14, 0.02, 0.14, M.cabin, 0, 1.345, 0);
    var b = new T.Group(); b.position.set(2.0, 0, 2.15 * sg); g.add(b);
    b3(b, 1.2, 1.0, 0.9, M.plaster, 0, 0.5, 0);
    var gl = new T.Mesh(new T.BoxGeometry(1.2, 1.0, 0.9), M.glass); gl.position.set(0, 1.5, 0); b.add(gl);
    [[-0.58, -0.43], [0.58, -0.43], [-0.58, 0.43], [0.58, 0.43]].forEach(function (c) { b3(b, 0.05, 1.0, 0.05, M.frame, c[0], 1.5, c[1]); });
    b3(b, 1.5, 0.1, 1.2, M.dark, 0, 2.05, 0);
    b3(b, 0.7, 0.04, 0.5, M.lamp, 0, 1.98, 0);
    U.cyl(b, 0.04, 1.2, M.steel, 0.55, 2.7, 0.35 * sg);
    var tx = U.signTex([o.label, o.label2 || ''], o.color || '#0b5cad', '#ffffff', 512, 160);
    [-Math.PI / 2, Math.PI / 2].forEach(function (ry) {
      var l = new T.Mesh(new T.PlaneGeometry(2.4, 0.7), U.signMat(tx)); l.position.set(0, 3.3, 0.35 * sg); l.rotation.y = ry; b.add(l);
    });
    parent.add(g);
    var gate = { kind: o.kind, group: g, lift: lift, open: false, forced: false, redM: redM, grM: grM, openAngle: sg * Math.PI / 2,
      pos: new T.Vector3(o.x, fl.y, o.z), label: o.label };
    arm.userData.gate = gate; scr.userData.gate = gate;
    gate.pick = [arm, scr];
    fl.gates.push(gate);
    return gate;
  }

  function emergencyDoor(fl, x, z, fx, fz) {
    var g = new T.Group(); g.position.set(x, 0, z); g.rotation.y = Math.atan2(fx, fz);
    b3(g, 1.7, 2.35, 0.14, M.frame, 0, 1.175, 0.07);
    b3(g, 1.4, 2.2, 0.08, M.exitGreen, 0, 1.1, 0.16);
    b3(g, 0.3, 0.05, 0.05, M.steel, 0.5, 1.05, 0.22);
    var s = new T.Mesh(new T.PlaneGeometry(1.1, 0.4), U.signMat(U.signTex(['THOÁT HIỂM', 'EXIT'], '#0b8a43', '#ffffff', 256, 96)));
    s.position.set(0, 2.65, 0.1); g.add(s);
    fl.group.add(g);
  }

  /* ---------- nhãn mã slot trên sàn (atlas) ---------- */
  function labelAtlas(fl) {
    var n = fl.slots.length, cols = 10, rows = Math.ceil(n / cols), cw = 128, ch = 64;
    var tex = U.canvasTex(cols * cw, rows * ch, function (c) {
      c.clearRect(0, 0, cols * cw, rows * ch);
      c.textAlign = 'center'; c.textBaseline = 'middle'; c.font = 'bold 34px Arial, sans-serif';
      fl.slots.forEach(function (s, i) {
        c.fillStyle = s.zone === 'MONTHLY' ? '#8ec5ff' : '#ffffff';
        c.fillText(s.code, (i % cols) * cw + cw / 2, Math.floor(i / cols) * ch + ch / 2);
      });
    });
    var pos = [], uv = [], idx = [];
    fl.slots.forEach(function (s, i) {
      var f = s.face, w = 1.5, h = 0.6, cx = s.x, cz = s.zc + f * (SL / 2 + 0.7);
      var rx = f > 0 ? 1 : -1, uz = f > 0 ? -1 : 1;
      var p = [[cx - rx * w / 2, cz - uz * h / 2], [cx + rx * w / 2, cz - uz * h / 2], [cx + rx * w / 2, cz + uz * h / 2], [cx - rx * w / 2, cz + uz * h / 2]];
      var u0 = (i % cols) / cols, u1 = u0 + 1 / cols, v1 = 1 - Math.floor(i / cols) / rows, v0 = v1 - 1 / rows, b = pos.length / 3;
      p.forEach(function (q) { pos.push(q[0], 0.014, q[1]); });
      uv.push(u0, v0, u1, v0, u1, v1, u0, v1);
      idx.push(b, b + 1, b + 2, b, b + 2, b + 3);
    });
    var geo = new T.BufferGeometry();
    geo.setAttribute('position', new T.Float32BufferAttribute(pos, 3));
    geo.setAttribute('uv', new T.Float32BufferAttribute(uv, 2));
    geo.setIndex(idx);
    fl.group.add(new T.Mesh(geo, new T.MeshBasicMaterial({ map: tex, transparent: true, side: T.DoubleSide, depthWrite: false, polygonOffset: true, polygonOffsetFactor: -3, polygonOffsetUnits: -3 })));
  }

  /* ---------- bảng IoT hiển thị biển số (tầng xe tháng) ---------- */
  function iotBoards(fl) {
    var poles = [], bodies = [];
    fl.slots.forEach(function (s) {
      if (s.zone !== 'MONTHLY') return;
      var f = s.face, back = s.zc - f * SL / 2, wall = (s.row === 'A' && false);
      var z = back + f * 0.1, y = 2.0;
      poles.push({ x: s.x, y: 1.0, z: back + f * 0.03, sx: 0.06, sy: 2.0, sz: 0.06, color: 0x3a3f47 });
      bodies.push({ x: s.x, y: y, z: z, sx: 0.84, sy: 0.4, sz: 0.06, color: 0x16191e });
      var occ = s.status === 'OCCUPIED';
      var tex = U.canvasTex(192, 96, function (c, w, h) {
        c.fillStyle = '#05080c'; c.fillRect(0, 0, w, h);
        c.textAlign = 'center'; c.textBaseline = 'middle';
        c.fillStyle = '#9fb3c8'; c.font = 'bold 20px Arial'; c.fillText(s.code, w / 2, 20);
        if (occ) { c.fillStyle = '#ffd34d'; c.font = 'bold 30px Arial'; c.fillText(s.plate, w / 2, 58); c.fillStyle = '#ff5050'; c.fillRect(8, h - 14, w - 16, 6); }
        else { c.fillStyle = '#35e07a'; c.font = 'bold 34px Arial'; c.fillText('TRỐNG', w / 2, 58); c.fillRect(8, h - 14, w - 16, 6); }
      });
      var scr = new T.Mesh(new T.PlaneGeometry(0.78, 0.34), new T.MeshBasicMaterial({ map: tex }));
      scr.position.set(s.x, y, z + f * 0.034); scr.rotation.y = f > 0 ? 0 : Math.PI;
      fl.group.add(scr);
    });
    U.inst(fl.group, U.unitBox(), M.sepInst, poles);
    U.inst(fl.group, U.unitBox(), M.sepInst, bodies);
  }

  /* ================================================================== */
  /*  DỮ LIỆU SLOT DEMO                                                   */
  /* ================================================================== */
  function makeSlots(lot, k) {
    var def = lot.def, fid = def.ids[k], y = lot.ys[k], kind = k === 0 ? 'MONTHLY' : 'VISITOR';
    var R = U.rng(def.seed * 100 + k * 7 + 3), out = [];
    var provinces = ['51A', '51F', '51G', '59A', '59C', '50A', '51K', '52A', '60A', '61A'];
    ROWS.forEach(function (row, ri) {
      var n = def.counts[k][ri];
      for (var i = 1; i <= n; i++) {
        var r = R(), occ = r < 0.55, mnt = !occ && r > 0.96;
        var plate = occ ? provinces[Math.floor(R() * provinces.length)] + '-' + Math.floor(100 + R() * 899) + '.' + U.pad2(Math.floor(R() * 99)) : null;
        var x = slotX(i, n);
        out.push({
          code: fid + '-' + row.id + U.pad2(i), floor: fid, floorIdx: k, row: row.id, idx: i, zone: kind,
          status: occ ? 'OCCUPIED' : (mnt ? 'MAINTENANCE' : 'EMPTY'), plate: plate,
          vehicle: occ ? (R() < 0.12 ? 'Ô tô điện' : 'Ô tô 4-7 chỗ') : null, carColor: 0,
          x: x, zc: row.zc, z: row.zc - row.face * 0.12, face: row.face, world: new T.Vector3(x, y + 0.9, row.zc)
        });
      }
    });
    out.forEach(function (s, i) { s.carColor = U.CAR_COLORS[(i * 7 + k * 3 + s.idx * 13) % U.CAR_COLORS.length]; });
    return out;
  }
  function freeOf(slots) { return slots.filter(function (s) { return s.status === 'EMPTY'; }).length; }

  /* ================================================================== */
  /*  1 TẦNG                                                              */
  /* ================================================================== */
  function buildFloor(lot, k) {
    var def = lot.def, n = lot.n, under = lot.under, fid = def.ids[k], y = lot.ys[k], monthly = k === 0;
    var g = new T.Group(); g.position.y = y;
    var ceil = new T.Group(); g.add(ceil);
    var fl = { k: k, id: fid, y: y, group: g, ceil: ceil, kind: monthly ? 'MONTHLY' : 'VISITOR', slots: lot.slots[k], lobbies: [], gates: [] };
    var ctx = { g: g, under: under, fins: [] };
    var odd = k % 2 === 1;

    /* ---- sàn sảnh ---- */
    U.floorRect(g, -HX, HX, -HZ, HZ, 0);

    /* ---- sàn tháp dốc phía Đông ---- */
    function adj(parity) { var c = [k - 1, k]; for (var i = 0; i < 2; i++) { var j = c[i]; if (j >= 0 && j < n - 1 && j % 2 === parity) return j; } return -1; }
    [0, 1].forEach(function (b) {
      var j = adj(b); if (j < 0) return;
      var upper = under ? j : j + 1;
      if (k !== upper) U.floorRect(g, HX, HX + RL, TS[b][0], TS[b][1], 0);
      else {                                                     // miệng hố dốc: lan can hai mép
        [TS[b][0] + 0.2, TS[b][1] - 0.2].forEach(function (zz) {
          if (Math.abs(zz) > HZ - 0.5) return;
          U.box(g, XT - HX, 1.0, 0.25, M.conc, (HX + XT) / 2, 0.5, zz);
          U.box(g, XT - HX, 0.12, 0.27, M.hazard, (HX + XT) / 2, 1.04, zz);
        });
      }
    });
    if (odd) {
      TS.forEach(function (s) { U.floorRect(g, HX + RL, XT, s[0], s[1], 0); });
      U.floorRect(g, HX, HX + RL, TS[2][0], TS[2][1], 0);
    }

    /* ---- tường: ngăn sảnh | tháp dốc, chu vi ---- */
    var gapsE = [];                                              // cửa trên tường Đông (x = HX)
    if (!odd) {
      if (k < n - 1) gapsE.push({ c: -18.5, w: 8, h: 3.6 });
      if (k >= 1) gapsE.push({ c: -10, w: 8, h: 3.6 });
    } else gapsE.push({ c: -2, w: 6, h: 3.6 });
    wallLine(ctx, 'z', HX, -HZ, 1, gapsE, false);
    wallLine(ctx, 'z', HX, 1, HZ, monthly ? [{ c: 18.5, w: 8, h: 3.6 }] : [], true);
    wallLine(ctx, 'z', -HX, -HZ, HZ, monthly ? [{ c: ZD1, w: 4.6, h: 3.6 }] : [], true);
    wallLine(ctx, 'x', -HZ, -HX, XT, [], true);
    wallLine(ctx, 'x', HZ, -HX, HX, [], true);
    wallLine(ctx, 'z', XT, -HZ, 1, [], true);
    wallLine(ctx, 'x', 1, HX, XT, [], true);
    if (!under) U.inst(g, U.unitBox(), M.fin, ctx.fins);
    if (under && monthly) {                                      // trần hầm (nhóm ẩn khi xem toàn cảnh)
      U.box(ceil, 2 * HX, SLAB, 2 * HZ, M.ceil, 0, CLEAR + SLAB / 2, 0);
      U.box(ceil, XT - HX, SLAB, 24, M.ceil, (HX + XT) / 2, CLEAR + SLAB / 2, -11);
    }
    if (!under && k === n - 1) { U.box(ceil, 0.01, 0.01, 0.01, M.dark, 0, 0, 0); }

    /* ---- vạch kẻ ô, bánh chặn, vách ngăn modular, đệm trạng thái, xe ---- */
    var lines = [], backs = [], stops = [], seps = [], pads = [], cones = [];
    var tone = monthly ? 0x4aa3ff : 0xf0f0ea;
    ROWS.forEach(function (row, ri) {
      var cnt = def.counts[k][ri], f = row.face, l = Math.ceil(cnt / 2), r = cnt - l;
      function edges(x0, num) {
        for (var i = 0; i <= num; i++) {
          var lx = x0 + i * SW;
          lines.push({ x: lx, y: 0.012, z: row.zc, sx: 0.1, sy: 0.01, sz: SL, color: tone });
          seps.push({ x: lx, y: 0.3, z: row.zc - f * (SL / 2 - 0.65), sx: 0.07, sy: 0.6, sz: 1.3, color: monthly ? 0x7fb8ee : 0xe2d9a8 });
        }
      }
      edges(GAPL - l * SW, l); edges(GAPR, r);
    });
    fl.slots.forEach(function (s) {
      backs.push({ x: s.x, y: 0.012, z: s.zc - s.face * SL / 2, sx: SW, sy: 0.01, sz: 0.1, color: tone });
      stops.push({ x: s.x, y: 0.07, z: s.zc - s.face * (SL / 2 - 0.55), sx: 1.6, sy: 0.14, sz: 0.16, color: 0x9a9da1 });
      var col = s.status === 'OCCUPIED' ? 0xef4444 : (s.status === 'MAINTENANCE' ? 0xf59e0b : 0x10b981);
      pads.push({ x: s.x, y: 0.02, z: s.zc, sx: SW - 0.4, sy: 1, sz: SL - 0.6, color: col });
      if (s.status === 'MAINTENANCE') cones.push({ x: s.x, y: 0.35, z: s.zc, sx: 1, sy: 1, sz: 1 }, { x: s.x - 0.6, y: 0.35, z: s.zc - s.face * 1.5, sx: 1, sy: 1, sz: 1 }, { x: s.x + 0.6, y: 0.35, z: s.zc - s.face * 1.5, sx: 1, sy: 1, sz: 1 });
    });
    U.inst(g, U.unitBox(), M.paintInst, lines);
    U.inst(g, U.unitBox(), M.paintInst, backs);
    U.inst(g, U.unitBox(), M.sepInst, stops);
    U.inst(g, U.unitBox(), M.sepInst, seps);
    U.inst(g, U.flatQuad(), M.pad, pads);
    if (cones.length) U.inst(g, U.getGeo('cone', function () { return new T.ConeGeometry(0.2, 0.7, 10); }), M.cone, cones);
    labelAtlas(fl);
    U.buildCars(g, fl.slots, 0);
    if (monthly) iotBoards(fl);

    /* ---- vạch lối đi, mũi tên, tín hiệu làn ---- */
    var dashes = [];
    AZ.forEach(function (az) { for (var dx = BX + 3; dx < XEC - 1; dx += 4) dashes.push({ x: dx, y: 0.012, z: az, sx: 2, sy: 0.01, sz: 0.12, color: 0xf0f0ea }); });
    U.inst(g, U.unitBox(), M.paintInst, dashes);
    // mũi tên trắng trong khu đỗ
    AZ.forEach(function (az, ai) {
      var rot = ai === 1 ? ROT_W : ROT_E;
      [-10, 6].forEach(function (ax) { if (Math.abs(ax - GX) > 3.5) U.floorArrow(g, ax, 0, az, rot, 1.0, 2.0); });
    });
    [-5, 10].forEach(function (zz) { U.floorArrow(g, BX, 0, zz, ROT_N, 1.0, 2.0); });
    [-3, 8].forEach(function (zz) { U.floorArrow(g, XEC, 0, zz, ROT_N, 1.0, 2.0); });

    /* ---- phòng thang máy riêng (3 phòng: Bắc / Tây / giữa) ---- */
    buildLobby(fl, { id: 0, name: 'Thang máy phía Bắc', cx: GX, cz: HZ, fx: 0, fz: -1, lane: [GX, AZ[2]] });
    buildLobby(fl, { id: 1, name: 'Thang máy phía Tây', cx: -HX, cz: AZ[1], fx: 1, fz: 0, lane: [BX, AZ[1]] });
    buildLobby(fl, { id: 2, name: 'Thang máy giữa bãi', cx: GX, cz: ROWS[1].zc - 2.5, fx: 0, fz: 1, lane: [GX, AZ[1]] });
    emergencyDoor(fl, -HX, 19, 1, 0);
    emergencyDoor(fl, XEC, HZ, 0, -1);

    /* ---- cột bê tông, chân sọc vàng-đen, bình chữa cháy ---- */
    var cols = [], bases = [], exts = [], cctv = [];
    var extTex = U.canvasTex(64, 64, function (c) { c.fillStyle = '#d32f2f'; c.fillRect(0, 0, 64, 64); c.fillStyle = '#fff'; c.fillRect(26, 10, 12, 44); c.fillRect(10, 26, 44, 12); });
    ROWS.forEach(function (row) {
      var f = row.face, zb = row.zc - f * SL / 2 + f * 0.25;
      COLX.forEach(function (cx, ci) {
        if (Math.abs(cx - GX) < 3.2) return;                      // chừa vị trí phòng thang máy
        cols.push({ x: cx, y: CLEAR / 2, z: zb, sx: 0.5, sy: CLEAR, sz: 0.5 });
        bases.push({ x: cx, y: 0.6, z: zb, sx: 0.54, sy: 1.2, sz: 0.54 });
        if (ci % 2 === 0) {
          var dirz = (row.id === 'A' || row.id === 'D') ? f : -f;
          exts.push({ x: cx, y: 1.35, z: zb + dirz * 0.3, sx: 0.28, sy: 0.55, sz: 0.16 });
          var sp = new T.Mesh(new T.PlaneGeometry(0.32, 0.32), new T.MeshBasicMaterial({ map: extTex }));
          sp.position.set(cx, 1.95, zb + dirz * 0.26); sp.rotation.y = dirz > 0 ? 0 : Math.PI; g.add(sp);
        }
        if (ci % 3 === 1 && (row.id === 'B' || row.id === 'C')) cctv.push({ x: cx, y: 3.4, z: zb + (row.id === 'B' ? -1 : 1) * 0.35 });
      });
    });
    U.inst(g, U.unitBox(), M.conc, cols);
    U.inst(g, U.unitBox(), M.hazard, bases);
    U.inst(g, U.unitBox(), M.red, exts);
    var domeG = U.getGeo('dome', function () { return new T.SphereGeometry(0.13, 10, 8, 0, Math.PI * 2, 0, Math.PI / 2); });
    cctv.forEach(function (c) { var m = new T.Mesh(domeG, M.dark); m.position.set(c.x, c.y, c.z); m.rotation.x = Math.PI; g.add(m); });

    /* ---- LÀN THEO TỪNG LOẠI TẦNG ---- */
    var bol = [];
    function bollardLine(x0, z0, x1, z1, step) {
      var len = Math.sqrt((x1 - x0) * (x1 - x0) + (z1 - z0) * (z1 - z0)), cnt = Math.max(1, Math.round(len / step));
      for (var i = 0; i <= cnt; i++) bol.push({ x: x0 + (x1 - x0) * i / cnt, y: 0.35, z: z0 + (z1 - z0) * i / cnt, sx: 1, sy: 1, sz: 1 });
    }
    function kerb(x0, z0, x1, z1) {                                // đường kẻ đỏ ngăn cách (lề bê tông đỏ)
      var len = Math.sqrt((x1 - x0) * (x1 - x0) + (z1 - z0) * (z1 - z0)), mx = (x0 + x1) / 2, mz = (z0 + z1) / 2;
      if (Math.abs(x1 - x0) > Math.abs(z1 - z0)) U.box(g, len, 0.35, 0.4, M.kerbRed, mx, 0.175, mz); else U.box(g, 0.4, 0.35, len, M.kerbRed, mx, 0.175, mz);
    }
    if (monthly) {
      // lề đỏ: ngăn làn vãng lai (Nam) và làn ra (Đông) khỏi khu đỗ xe tháng
      kerb(-HX, -15.6, BX - 2.3, -15.6); kerb(BX + 2.3, -15.6, XR, -15.6);
      kerb(XR, -15.6, XR, 12.2); kerb(XR, 17.8, XR, HZ);
      // cọc tiêu giữa 2 làn (vàng | xanh lá)
      bollardLine(XB, -13, XB, ZEG + 0.5, 2.2);
      // mũi tên màu theo làn
      for (var sx = -HX + 6; sx < HX - 2; sx += 7) arrowC(g, sx, 0, ZD1, ROT_E, '#2d8cff', 1.3, 2.6);
      [-12, -4, 4, 12].forEach(function (zz) { arrowC(g, XG, 0, zz, ROT_N, '#2fcf6a', 1.3, 2.6); });
      [16.2, 18.5].forEach(function (zz) { arrowC(g, XY, 0, zz, ROT_N, '#f2c200', 1.3, 2.6); });
      arrowC(g, BX, 0, -18.5, ROT_N, '#f2c200', 1.2, 2.4);
      arrowC(g, XEC + 0, 0, 15, ROT_E, '#f2c200', 1.0, 2.0);
      floorText(g, -3, ZD1, ROT_E, 6.4, 2.2, ['XE VÃNG LAI ▶ B2'], '#1a5fd0', '#ffffff');
      floorText(g, BX, -12.5, ROT_N, 3.4, 3.0, ['XE', 'THÁNG'], '#c79a00', '#ffffff');
      floorText(g, XG, -14, ROT_N, 3.4, 3.0, ['VÃNG', 'LAI'], '#1e8f4e', '#ffffff');
      floorText(g, XY, 10, ROT_N, 3.4, 3.0, ['XE', 'THÁNG'], '#c79a00', '#ffffff');
      // vạch dừng + tên ô
      U.box(g, 0.4, 0.01, 4, M.paintYellow, XR - 1.2, 0.013, 15);
      U.box(g, 3.6, 0.01, 0.4, M.paintYellow, BX, 0.013, -17.6);
    } else {
      // tầng vãng lai: lề đỏ không cần, làn ngoại vi mở
      for (var vx = BX; vx < HX - 4; vx += 8) arrowC(g, vx, 0, ZD1, ROT_E, '#2d8cff', 1.2, 2.4);
      [-12, -4, 4, 12].forEach(function (zz) { arrowC(g, XG, 0, zz, ROT_N, '#f2f2ea', 1.2, 2.4); });
      [-4, 4, 12].forEach(function (zz) { arrowC(g, XY, 0, zz, ROT_S, '#f2f2ea', 1.2, 2.4); });
      bollardLine(XB, -3, XB, 17, 4.4);
    }
    // phân làn trong tháp dốc / bãi chuyển hướng (cọc tiêu giữa các cặp làn)
    if (odd) {
      bollardLine(HX + RL + 0.5, -18.5, HX + RL + 4, -18.5, 2.2);
      if (k < n - 1 || true) bollardLine(HX + RL + 0.5, -10, HX + RL + 4, -10, 2.2);
      U.box(g, 0.2, 0.01, 7, M.paintYellow, HX + RL + 0.5, 0.013, -8);
      for (var ax = HX + 3; ax < HX + RL; ax += 6) { arrowC(g, ax, 0, ZCW, ROT_W, '#f2f2ea', 1.0, 2.0); arrowC(g, ax, 0, ZCE, ROT_E, '#f2f2ea', 1.0, 2.0); }
      arrowC(g, XD, 0, -14, ROT_N, '#2d8cff', 1.0, 2.0);
      arrowC(g, XS_, 0, -6, ROT_S, '#2fcf6a', 1.0, 2.0);
    } else if (k >= 1) {
      [[HX - 3, ZD2, ROT_W], [XJD, -14, ROT_S], [HX - 3, ZD1, ROT_E]].forEach(function (a) { arrowC(g, a[0], 0, a[1], a[2], '#2d8cff', 1.0, 2.0); });
      [[HX - 3, ZU1, ROT_W], [XJS, -14, ROT_N], [HX - 3, ZU2, ROT_E]].forEach(function (a) { arrowC(g, a[0], 0, a[1], a[2], '#2fcf6a', 1.0, 2.0); });
    }
    if (bol.length) {
      U.inst(g, U.getGeo('bollard', function () { var q = new T.CylinderGeometry(0.1, 0.16, 0.7, 10); return q; }), M.cone, bol);
      var capG = U.getGeo('bollardCap', function () { return new T.CylinderGeometry(0.105, 0.105, 0.12, 10); });
      U.inst(g, capG, M.bollard, bol.map(function (b) { return { x: b.x, y: 0.5, z: b.z, sx: 1, sy: 1, sz: 1 }; }));
    }

    /* ---- trần: dầm, ống, đèn, quạt ---- */
    buildCeiling(fl, ceil, k, odd);
    /* ---- biển báo ---- */
    buildSigns(fl, ceil, lot, k, odd);
    /* ---- booth quẹt thẻ + barrier ---- */
    if (monthly) {
      buildGate(g, fl, { x: BX, z: -16.5, dir: [0, 1], side: -1, kind: 'checkin', label: 'CHECK-IN', label2: 'XE THÁNG · QUẸT THẺ', color: '#0b5cad', readerLine: 'QUẸT THẺ' });
      buildGate(g, fl, { x: XR - 2.0, z: 15, dir: [1, 0], side: -1, kind: 'checkout', label: 'CHECK-OUT', label2: 'XE THÁNG · QUẸT THẺ', color: '#b45309', readerLine: 'QUẸT THẺ RA' });
    }
    if (k === 1) {
      buildGate(g, fl, { x: XS_, z: -14, dir: [0, -1], side: -1, kind: 'visitorOut', label: 'CHECK-OUT', label2: 'XE VÃNG LAI', color: '#a31818', readerLine: 'QUẸT THẺ RA' });
    }
    return fl;
  }

  /* ---------- trần ---------- */
  function buildCeiling(fl, ceil, k, odd) {
    var beams = [], lamps = [];
    COLX.forEach(function (cx) { beams.push({ x: cx, y: CLEAR - 0.25, z: 0, sx: 0.55, sy: 0.5, sz: 2 * HZ }); });
    [-4.2, 7.2].forEach(function (zz) { beams.push({ x: 0, y: CLEAR - 0.25, z: zz, sx: 2 * HX, sy: 0.5, sz: 0.5 }); });
    U.inst(ceil, U.unitBox(), M.conc, beams);
    var zs = [ZD1, ZU1, -12.5, -7, -1.5, 4, 9.5, 15, 20.5];
    for (var lx = -HX + 3; lx < HX; lx += 5) zs.forEach(function (z) { lamps.push({ x: lx, y: CLEAR - 0.56, z: z, sx: 1.5, sy: 0.07, sz: 0.16 }); });
    if (odd) for (var tx = HX + 3; tx < XT; tx += 5) [-20.5, -16.5, -12, -8, -3].forEach(function (z) { lamps.push({ x: tx, y: CLEAR - 0.56, z: z, sx: 1.5, sy: 0.07, sz: 0.16 }); });
    U.inst(ceil, U.unitBox(), M.lamp, lamps);
    [-7, 4, 15].forEach(function (az) {
      U.box(ceil, 2 * HX, 0.5, 0.9, M.duct, 0, CLEAR - 0.8, az - 2.2);
      U.cyl(ceil, 0.06, 2 * HX, M.pipeRed, 0, CLEAR - 0.7, az + 2.4, 'x');
      U.cyl(ceil, 0.05, 2 * HX, M.pipeBlue, 0, CLEAR - 0.72, az + 2.0, 'x');
      U.box(ceil, 2 * HX, 0.05, 0.4, M.tray, 0, CLEAR - 0.6, az + 0.5);
    });
    for (var fx = -HX + 8; fx < HX - 6; fx += 14) {
      [4 + 1.2, -7 + 0.5, 15 - 0.5].forEach(function (fz) {
        U.cyl(ceil, 0.34, 1.8, M.steel, fx, CLEAR - 1.35, fz, 'x');
        U.cyl(ceil, 0.37, 0.12, M.dark, fx - 0.9, CLEAR - 1.35, fz, 'x');
        U.cyl(ceil, 0.37, 0.12, M.dark, fx + 0.9, CLEAR - 1.35, fz, 'x');
        U.box(ceil, 0.05, 0.6, 0.05, M.steel, fx - 0.5, CLEAR - 0.95, fz);
        U.box(ceil, 0.05, 0.6, 0.05, M.steel, fx + 0.5, CLEAR - 0.95, fz);
      });
    }
  }

  /* ---------- biển báo (treo trần) ---------- */
  function S(lines, bg, w, h) { return U.signTex(lines, bg, '#ffffff', w || 512, h || 160); }
  function buildSigns(fl, ceil, lot, k, odd) {
    var ids = lot.def.ids, n = lot.n, free = lot.free;
    function hang(x, z, w, h, pos, neg, axis, drop) { return U.hangSign(ceil, x, z, w, h, pos, neg, axis, drop == null ? 0.3 : drop); }
    var BLUE = '#0b5cad', GREEN = '#0b7a3b', RED = '#a31818', AMB = '#b45309', DARK = '#202833';

    /* thang máy: bảng nhỏ chỉ đường tới 3 phòng thang */
    var tl = S(['◀ THANG MÁY'], BLUE, 360, 110), tr = S(['THANG MÁY ▶'], BLUE, 360, 110), tu = S(['▲ THANG MÁY'], BLUE, 360, 110);
    hang(GX, AZ[2] + 1.6, 1.8, 0.55, null, tu, 'z');                    // phía Bắc: người đi lên hướng +z thấy mặt -z
    hang(GX, AZ[1] - 1.6, 1.8, 0.55, tu, null, 'z');                    // phòng giữa: người đi hướng -z
    hang(BX + 3.5, AZ[1], 1.8, 0.55, tl, tr, 'x');                      // phòng phía Tây
    hang(GX - 8, AZ[2], 1.8, 0.55, null, tu, 'x'); hang(GX + 8, AZ[2], 1.8, 0.55, tu, null, 'x');
    hang(GX - 8, AZ[1], 1.8, 0.55, null, tu, 'x'); hang(GX + 8, AZ[1], 1.8, 0.55, tu, null, 'x');

    var freeNow = freeOf(fl.slots), totalNow = fl.slots.length;
    function avail(i) { var f = free[i]; return (f > 6 ? GREEN : (f > 0 ? AMB : RED)); }
    function availLine(i) { return ids[i] + ' · còn trống ' + free[i] + '/' + lot.slots[i].length; }

    if (k === 0) {
      // ngay sau khi xuống dốc vào hầm: chỉ hướng Tháng / Vãng lai tới booth check-in
      hang(-HX + 4, ZD1, 3.2, 0.95, null, S(['◀ XE THÁNG', 'Rẽ trái · Booth Check-in'], BLUE, 560, 170), 'x', 0.25);
      hang(-HX + 4, ZD1 - 0.0, 0.01, 0.01, null, null, 'x');
      hang(-HX + 14, ZD1, 3.4, 0.95, null, S(['XE VÃNG LAI ▶▶', 'Đi thẳng · xuống ' + (ids[1] || '')], GREEN, 560, 170), 'x', 0.25);
      hang(BX, -13.5, 2.8, 0.85, null, S(['CHỈ XE THÁNG', 'Quẹt thẻ để vào'], RED, 480, 150), 'z');
      hang(-6, ZD1, 3.0, 0.8, null, S(['LÀN VÃNG LAI ▶ ' + (ids[1] || ''), 'Không vào khu xe tháng'], GREEN, 560, 150), 'x');
      hang(HX - 11, ZD1, 3.4, 1.05, null, S(['XUỐNG ' + ids[1] + ' ▶', availLine(1)], avail(1), 560, 170), 'x', 0.25);
      hang(XEC, 15, 3.0, 0.85, null, S(['CHECK-OUT XE THÁNG ▶', 'Quẹt thẻ ra'], AMB, 560, 150), 'x');
      hang((XY + XG) / 2 - 0.2, 3, 3.6, 0.95, S(['◀ XE THÁNG · làn vàng', 'VÃNG LAI từ ' + ids[1] + ' · làn xanh ▶'], DARK, 640, 170), S(['◀ XE THÁNG · làn vàng', 'VÃNG LAI từ ' + ids[1] + ' · làn xanh ▶'], DARK, 640, 170), 'z');
      hang(XG - 0.5, 10, 2.6, 0.85, null, S(['▲ LỐI RA MẶT ĐẤT'], GREEN, 480, 130), 'z');
      hang(HX - 3, ZEG, 2.8, 0.8, null, S(['RA MẶT ĐẤT ▶▶'], GREEN, 480, 130), 'x');
      hang(-HX + 3, 12, 3.0, 0.9, S(['TẦNG ' + ids[0] + ' · XE THÁNG', availLine(0)], avail(0), 560, 160), S(['TẦNG ' + ids[0] + ' · XE THÁNG', availLine(0)], avail(0), 560, 160), 'x');
    } else if (odd) {
      // bãi chuyển hướng (landing): chọn vào tầng này hay xuống tiếp
      hang(XD, -15.5, 3.4, 1.0, null, S(['TẦNG ' + ids[k] + ' · VÃNG LAI', availLine(k) + ' ▲'], avail(k), 600, 170), 'z');
      if (k < n - 1) hang(XD, -11.2, 3.4, 1.0, null, S(['TIẾP TỤC XUỐNG ' + ids[k + 1] + ' ◀', availLine(k + 1)], avail(k + 1), 600, 170), 'z');
      hang(XD + 1.2, -4.2, 3.2, 0.85, S(['VÀO KHU ĐỖ ' + ids[k] + ' ◀'], GREEN, 520, 140), null, 'x');
      hang(XS_, -16.5, 3.0, 0.85, S(['CHECK-OUT XE VÃNG LAI', 'Quẹt thẻ · rồi lên ' + ids[0]], RED, 560, 150), null, 'z');
      hang(HX + 6, ZCW, 2.8, 0.8, S(['KHU ĐỖ VÃNG LAI ' + ids[k] + ' ◀'], GREEN, 520, 130), S(['LỐI RA ' + ids[0] + ' ▶'], RED, 480, 130), 'x');
    } else {
      // tầng chẵn (không phải tầng đầu): quay đầu ngay tại sảnh, bảng số chỗ trống
      hang(HX - 3, ZD2, 3.4, 1.0, S(['TẦNG ' + ids[k] + ' · VÃNG LAI', availLine(k)], avail(k), 600, 170), null, 'x');
      if (k < n - 1) hang(HX - 3, ZD1, 3.4, 1.0, null, S(['XUỐNG ' + ids[k + 1] + ' ▶', availLine(k + 1)], avail(k + 1), 600, 170), 'x');
      hang(HX - 11, ZU1, 3.0, 0.8, S(['◀ LỐI RA · LÊN ' + ids[k - 1]], RED, 520, 130), null, 'x');
    }
    if (k >= 1) {
      hang(-HX + 3, 11.5, 3.4, 0.9, S(['KHU ĐỖ XE VÃNG LAI ' + ids[k], availLine(k)], avail(k), 600, 160), S(['KHU ĐỖ XE VÃNG LAI ' + ids[k], availLine(k)], avail(k), 600, 160), 'x');
      hang(XEC, 15, 2.8, 0.8, null, S(['LỐI RA ▶ ' + (k === 1 ? 'Check-out' : 'Lên ' + ids[k - 1])], RED, 480, 130), 'x');
    }
  }

  /* ================================================================== */
  /*  DỐC (switchback) + HÀO VÀO / RA                                     */
  /* ================================================================== */
  function addRamp(lot, j) {
    var even = j % 2 === 0, fl = lot.floors[j], yBase = fl.y;
    var yH = lot.ys[even ? j : j + 1] - yBase, yL = lot.ys[even ? j + 1 : j] - yBase; // đầu sảnh / đầu bãi chuyển hướng (tương đối theo tầng j)
    var zc = even ? -18.5 : -10, Lr = Math.sqrt(RL * RL + (yL - yH) * (yL - yH));
    var rg = new T.Group();
    rg.position.set(HX + RL / 2, (yH + yL) / 2, zc);
    rg.rotation.z = Math.atan2(yL - yH, RL);
    U.box(rg, Lr, 0.5, 8, M.slab, 0, -0.25, 0);
    var top = U.texPlane(Lr, 8, M.floor, Lr / 4, 2); top.rotation.x = -Math.PI / 2; top.position.y = 0.004; rg.add(top);
    U.box(rg, Lr, 0.01, 0.16, M.paintYellow, 0, 0.012, 0);
    U.box(rg, Lr, 0.18, 0.3, M.hazard, 0, 0.09, 3.85);
    U.box(rg, Lr, 0.18, 0.3, M.hazard, 0, 0.09, -3.85);
    var deepE = even, deepZ = even ? -2 : 2, shZ = -deepZ;                          // làn xuống (sâu) / làn lên (nông)
    [-6, 6].forEach(function (lx) {
      arrowC(rg, lx, 0, deepZ, deepE ? ROT_E : ROT_W, '#2d8cff', 1.2, 2.4);
      arrowC(rg, lx, 0, shZ, deepE ? ROT_W : ROT_E, '#2fcf6a', 1.2, 2.4);
    });
    fl.group.add(rg);
    // đèn trần trong dốc (gắn vào ceil để ẩn khi xem Toàn cảnh)
    for (var lx = HX + 3; lx < HX + RL; lx += 6) {
      var t = (lx - HX) / RL, yy = yH + (yL - yH) * t;
      U.box(fl.ceil, 1.2, 0.07, 0.16, M.lamp, lx, yy + CLEAR - 0.5, zc - 2);
      U.box(fl.ceil, 1.2, 0.07, 0.16, M.lamp, lx, yy + CLEAR - 0.5, zc + 2);
    }
    var rt = U.signTex(['DỐC ' + (even ? 'XUỐNG' : 'XUỐNG (vòng về sảnh)') + ' ' + lot.def.ids[j + 1], '▼ ' + (even ? 'Làn Nam' : 'Làn Bắc') + ' · ▲ làn còn lại'], '#c46a00', '#fff', 512, 160);
    U.hangSign(fl.group, HX + 3, zc, 3.0, 0.8, rt, rt, 'x', 0.3);
  }

  function addPortals(lot) {
    var g0 = lot.floors[0].group, ceil0 = lot.floors[0].ceil, under = lot.under, y0 = 0;
    if (under) {
      // hào vào (đơn) phía Tây và hào ra (2 làn vàng/xanh lá) phía Đông, từ mặt đất xuống B1
      var gy = y0 + FH, zE0 = -22.5, zE1 = -18.5;
      var rg = new T.Group(); rg.position.set(-HX - RL / 2, y0 + FH / 2, -20.5); rg.rotation.z = -Math.atan2(FH, RL);
      var Lr = Math.sqrt(RL * RL + FH * FH);
      U.box(rg, Lr, 0.5, 4, M.slab, 0, -0.25, 0);
      var t1 = U.texPlane(Lr, 4, M.floor, Lr / 4, 1); t1.rotation.x = -Math.PI / 2; t1.position.y = 0.004; rg.add(t1);
      [-6, 6].forEach(function (lx) { arrowC(rg, lx, 0, 0, ROT_E, '#2d8cff', 1.2, 2.4); });
      g0.add(rg);
      triWall(g0, -HX - RL, -HX, gy, y0, gy, zE0); triWall(g0, -HX - RL, -HX, gy, y0, gy, zE1);
      var rx = new T.Group(); rx.position.set(HX + RL / 2, y0 + FH / 2, 18.5); rx.rotation.z = Math.atan2(FH, RL);
      U.box(rx, Lr, 0.5, 8, M.slab, 0, -0.25, 0);
      var t2 = U.texPlane(Lr, 8, M.floor, Lr / 4, 2); t2.rotation.x = -Math.PI / 2; t2.position.y = 0.004; rx.add(t2);
      [-6, 6].forEach(function (lx) { arrowC(rx, lx, 0, -2, ROT_E, '#2fcf6a', 1.2, 2.4); arrowC(rx, lx, 0, 2, ROT_E, '#f2c200', 1.2, 2.4); });
      g0.add(rx);
      triWall(g0, HX, HX + RL, y0, gy, gy, 14.5); triWall(g0, HX, HX + RL, y0, gy, gy, 22.5);
      // cọc tiêu phân làn trên dốc ra
      var bol = [];
      for (var i = 0; i <= 10; i++) { var bx = HX + RL * i / 10; bol.push({ x: bx, y: y0 + FH * i / 10 + 0.35, z: 18.5, sx: 1, sy: 1, sz: 1 }); }
      U.inst(g0, U.getGeo('bollard', function () { return new T.CylinderGeometry(0.1, 0.16, 0.7, 10); }), M.cone, bol);
      // mặt đất có khoét hào, đường phố và ánh sáng ngoài trời (ẩn khi xem Toàn cảnh)
      var sh = new T.Shape(); sh.moveTo(-220, -220); sh.lineTo(220, -220); sh.lineTo(220, 220); sh.lineTo(-220, 220); sh.closePath();
      [[-HX - RL, -HX, -22.5, -18.5], [HX, HX + RL, 14.5, 22.5]].forEach(function (h) {
        var p = new T.Path(); p.moveTo(h[0], -h[2]); p.lineTo(h[1], -h[2]); p.lineTo(h[1], -h[3]); p.lineTo(h[0], -h[3]); p.closePath(); sh.holes.push(p);
      });
      var gg = new T.ShapeGeometry(sh); gg.rotateX(-Math.PI / 2);
      var gm = new T.Mesh(gg, M.ground); gm.position.y = gy - 0.03; ceil0.add(gm);
      [[-HX - RL - 14, -20.5], [HX + RL + 14, 18.5]].forEach(function (r) {
        var rd = new T.Mesh(new T.PlaneGeometry(28, 8.2), M.road); rd.rotation.x = -Math.PI / 2; rd.position.set(r[0], gy - 0.02, r[1]); ceil0.add(rd);
        var sky = new T.Mesh(new T.PlaneGeometry(12, 7), M.sky); sky.rotation.y = Math.PI / 2; sky.position.set(r[0] + (r[0] < 0 ? -14 : 14), gy + 2.5, r[1]); ceil0.add(sky);
      });
    } else {
      [[-HX - 8, -20.5, 4], [HX + 8, 18.5, 8]].forEach(function (r) {
        U.box(g0, 16, 0.1, r[2], M.road, r[0], -0.05, r[1]);
      });
      var bol2 = [];
      for (var q = 0; q <= 8; q++) bol2.push({ x: HX + 16 * q / 8, y: 0.35, z: 18.5, sx: 1, sy: 1, sz: 1 });
      U.inst(g0, U.getGeo('bollard', function () { return new T.CylinderGeometry(0.1, 0.16, 0.7, 10); }), M.cone, bol2);
      var gp = new T.Mesh(new T.PlaneGeometry(600, 600), M.ground); gp.rotation.x = -Math.PI / 2; gp.position.y = -SLAB - 0.04; lot.group.add(gp);
      var rd2 = new T.Mesh(new T.PlaneGeometry(600, 12), M.road); rd2.rotation.x = -Math.PI / 2; rd2.position.set(0, -SLAB - 0.03, HZ + 16); lot.group.add(rd2);
    }
  }

  /* ================================================================== */
  /*  1 BÃI                                                               */
  /* ================================================================== */
  function buildLot(def, THREE) {
    T = THREE; I.setThree(THREE); I.initMaterials(); extraMaterials();
    var n = def.ids.length, under = def.type === 'underground';
    var lot = { def: def, n: n, under: under, dir: under ? -1 : 1, group: new T.Group(), ys: [], floors: [], nodes: [], cores: [], gates: [], slots: [], free: [], layout: LAYOUT };
    for (var k = 0; k < n; k++) lot.ys.push((under ? -1 : 1) * k * FH);
    for (var k0 = 0; k0 < n; k0++) { lot.slots.push(makeSlots(lot, k0)); lot.free.push(freeOf(lot.slots[k0])); }
    for (var k2 = 0; k2 < n; k2++) {
      var fl = buildFloor(lot, k2);
      lot.floors.push(fl); lot.group.add(fl.group);
      fl.gates.forEach(function (gt) { lot.gates.push(gt); });
      fl.lobbies.forEach(function (lb) { lot.cores.push(lb); });
    }
    for (var j = 0; j < n - 1; j++) addRamp(lot, j);
    addPortals(lot);
    buildGraph(lot);
    lot.describe = function (x, z, k) { return describe(lot, x, z, k); };
    return lot;
  }

  /* ---------- mô tả vị trí đang đứng ---------- */
  function describe(lot, x, z, k) {
    if (x < -HX) return lot.under ? 'Dốc vào hầm' : 'Đường vào bãi';
    if (x > XT - 0.5) return 'Bãi chuyển hướng';
    if (x > HX) {
      if (z > 10) return 'Dốc ra mặt đất (làn vàng · xanh lá)';
      if (x > HX + RL) return 'Bãi chuyển hướng dốc';
      return 'Tháp dốc xe lên/xuống';
    }
    if (k === 0) {
      if (z < -15.8) return 'Làn xe VÃNG LAI (xuống ' + lot.def.ids[1] + ')';
      if (x > XR) return x < XB ? 'Làn ra xe THÁNG (vàng)' : 'Làn ra xe VÃNG LAI (xanh lá)';
      return 'Khu xe THÁNG · ' + rowName(z);
    }
    if (x > HX - 9.5 && z < -3) return 'Khu dốc / quay đầu ' + lot.def.ids[k];
    if (x > XR) return 'Làn trục Đông ' + lot.def.ids[k];
    return 'Khu VÃNG LAI · ' + rowName(z);
  }
  function rowName(z) { return z < -15 ? 'Làn xe' : z < -10 ? 'Dãy A' : z < -4 ? 'Lối đi 1' : z < 1 ? 'Dãy B' : z < 7 ? 'Lối đi 2' : z < 12 ? 'Dãy C' : z < 18 ? 'Lối đi 3' : 'Dãy D'; }

  /* ================================================================== */
  /*  ĐỒ THỊ ĐIỂM DI CHUYỂN (Street View)                                 */
  /* ================================================================== */
  function buildGraph(lot) {
    var n = lot.n, nodes = lot.nodes, under = lot.under;
    function addNode(x, y, z, fl, type) {
      for (var i = 0; i < nodes.length; i++) {
        var q = nodes[i];
        if (Math.abs(q.x - x) < 1.8 && Math.abs(q.z - z) < 1.8 && Math.abs(q.y - y) < 0.7) return q;
      }
      var nd = { id: nodes.length, x: x, y: y, z: z, floorIdx: fl, type: type || 'lane', nb: [] };
      nodes.push(nd); return nd;
    }
    function link(a, b) { if (a !== b && a.nb.indexOf(b.id) < 0) { a.nb.push(b.id); b.nb.push(a.id); } }
    /** pts: [[x,z]] hoặc [[x,z,y]]; fl: tầng (số) hoặc hàm(t) */
    function path(fl, pts, flOf) {
      var prev = null;
      for (var i = 0; i < pts.length; i++) {
        var p = pts[i], q = pts[i - 1], seg = [];
        if (q) {
          var dx = p[0] - q[0], dz = p[1] - q[1], len = Math.sqrt(dx * dx + dz * dz), steps = Math.ceil(len / 7.5);
          for (var s = 1; s < steps; s++) { var t = s / steps; seg.push([q[0] + dx * t, q[1] + dz * t, q.length > 2 ? q[2] + (p[2] - q[2]) * t : undefined]); }
        }
        seg.push(p);
        seg.forEach(function (c) {
          var f = typeof flOf === 'function' ? flOf(c) : fl;
          var yy = c[2] != null ? c[2] : lot.ys[f];
          var nd = addNode(c[0], yy, c[1], f);
          if (prev) link(prev, nd);
          prev = nd;
        });
      }
    }
    var ZW = AZ;
    lot.floors.forEach(function (fl) {
      var k = fl.k, y = fl.y, odd = k % 2 === 1, mon = k === 0;
      var east = mon ? XY : XG;
      // khu đỗ: 3 lối đi + làn Tây + làn ngang Đông
      ZW.forEach(function (z, ai) {
        var pts = [[BX, z], [GX, z], [XEC, z]];
        if (!mon) pts.push([XG, z]); else if (ai === 2) { pts.push([XR, z]); pts.push([XY, z]); }
        path(k, pts);
      });
      path(k, [[BX, ZD1], [BX, ZU1], [BX, ZW[0]], [BX, ZW[1]], [BX, ZW[2]], [BX, 20.5]]);
      path(k, [[XEC, ZW[0]], [XEC, ZW[1]], [XEC, ZW[2]], [XEC, 20.5]]);
      // phòng thang máy
      fl.lobbies.forEach(function (lb) {
        var nd = addNode(lb.pos.x, y, lb.pos.z, k, 'core');
        var lane = addNode(lb.lane[0], y, lb.lane[1], k);
        link(nd, lane); lb.nodeId = nd.id;
        lb.yaw = Math.atan2(-lb.fx, -lb.fz);
      });
      if (mon) {
        // làn vãng lai phía Nam, làn xanh lá + vàng phía Đông
        path(k, [[-HX, ZD1], [BX, ZD1], [GX, ZD1], [XEC, ZD1], [XG, ZD1], [HX, ZD1]]);
        path(k, [[HX, ZU1], [XG, ZU1], [XG, -7], [XG, 4], [XG, ZEG], [HX, ZEG]]);
        path(k, [[XY, 15], [XY, ZEY], [HX, ZEY]]);
      } else {
        path(k, [[BX, ZD1], [GX, ZD1], [XEC, ZD1], [XG, ZD1]]);
        path(k, [[XG, ZD1], [XG, ZU1], [XG, ZU2], [XG, ZD2], [XG, 4], [XG, 15], [XG, 20.5]]);
        if (odd) {
          path(k, [[XD, ZD1], [XD, ZD2], [XD, ZCW], [HX, ZCW], [XG, ZCW]]);
          path(k, [[XG, ZCE], [HX, ZCE], [XS_, ZCE], [XS_, ZU2], [XS_, -14], [XS_, ZU1]]);
          path(k, [[HX + RL, ZD1], [XD, ZD1]]);
          path(k, [[HX + RL, ZU1], [XS_, ZU1]]);
          if (k < n - 1) { path(k, [[XD, ZD2], [HX + RL, ZD2]]); path(k, [[HX + RL, ZU2], [XS_, ZU2]]); }
        } else {
          path(k, [[HX, ZD2], [XG, ZD2], [XJD, ZD2], [XJD, ZD1], [HX, ZD1]]);
          path(k, [[HX, ZU1], [XG, ZU1], [XJS, ZU1], [XJS, ZU2], [HX, ZU2]]);
        }
      }
    });
    // hào vào / ra và dốc giữa các tầng
    var gy = lot.ys[0] + (under ? FH : 0);
    path(0, under ? [[-HX - RL, ZD1, gy], [-HX - RL / 2, ZD1, lot.ys[0] + FH / 2], [-HX, ZD1, lot.ys[0]]] : [[-HX - 12, ZD1], [-HX, ZD1]]);
    path(0, under ? [[HX, ZEG, lot.ys[0]], [HX + RL / 2, ZEG, lot.ys[0] + FH / 2], [HX + RL, ZEG, gy]] : [[HX, ZEG], [HX + 14, ZEG]]);
    path(0, under ? [[HX, ZEY, lot.ys[0]], [HX + RL, ZEY, gy]] : [[HX, ZEY], [HX + 14, ZEY]]);
    for (var j = 0; j < n - 1; j++) {
      var even = j % 2 === 0, yH = lot.ys[even ? j : j + 1], yL = lot.ys[even ? j + 1 : j];
      var fH = even ? j : j + 1, fL = even ? j + 1 : j;
      [even ? ZD1 : ZD2, even ? ZU1 : ZU2].forEach(function (z) {
        var pts = [[HX, z, yH]];
        [0.34, 0.67].forEach(function (t) { pts.push([HX + RL * t, z, yH + (yL - yH) * t]); });
        pts.push([HX + RL, z, yL]);
        path(0, pts, function (c) { return (c[0] - HX) / RL < 0.5 ? fH : fL; });
      });
    }
    // điểm tham chiếu
    function nearest(k, x, z) { var b = null, bd = 1e9; nodes.forEach(function (q) { if (q.floorIdx !== k) return; var d = (q.x - x) * (q.x - x) + (q.z - z) * (q.z - z); if (d < bd) { bd = d; b = q; } }); return b; }
    lot.pois = {
      entry: { node: nearest(0, -HX - RL, ZD1), yaw: -Math.PI / 2 },
      checkin: { node: nearest(0, BX, -13.5), yaw: Math.PI },
      checkout: { node: nearest(0, XR - 4.5, 15), yaw: -Math.PI / 2 },
      ramp: { node: nearest(0, HX - 5, ZD1), yaw: -Math.PI / 2 }
    };
    lot.entryNode = lot.pois.entry.node;
  }
  function nearestNode(lot, floorIdx, x, z) {
    var best = null, bd = 1e12;
    lot.nodes.forEach(function (nd) {
      if (floorIdx != null && nd.floorIdx !== floorIdx) return;
      var d = (nd.x - x) * (nd.x - x) + (nd.z - z) * (nd.z - z);
      if (d < bd) { bd = d; best = nd; }
    });
    return best;
  }

  I.buildLot = buildLot;
  I.nearestNode = nearestNode;
})(typeof self !== 'undefined' ? self : this);
