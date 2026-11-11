/**
 * SmartPark 3D Render - bộ dựng TẦNG / BÃI (floor + lot builders) - Layout v3
 * Phụ thuộc: parking-3d-render-kit.js (vật liệu, tiện ích, xe)
 *
 * CẬP NHẬT LAYOUT v3:
 *  - Tối ưu diện tích hầm: xoá thang máy trung tâm, giữ 2 phòng thang máy vách kính sát tường
 *    (khu Tây & khu Bắc), 4 dãy đỗ xe A-D liền mạch có chặn lùi bánh xe (wheel-stop).
 *  - Khu Check-in B1: bảng xanh đầu dốc ghi "XE THÁNG | Rẽ phải ➔"; Booth Check-in dời sang
 *    bên phải làn xe, máy quẹt thẻ đặt nằm ngang dọc làn xe, bỏ bảng xanh trên nóc booth,
 *    đặt bảng LED báo số slot trống ngay tại Booth Check-in.
 *  - Khu Check-out B1: có bảng chỉ dẫn lối ra dọc các lối đi trong bãi; Booth Check-out đặt
 *    nằm ngang như Booth Check-in tại đầu ra Lối đi 3, chặn đường đi cắt ngang trong bãi.
 *  - Thang máy: có điểm đứng ngoài cửa kính và điểm đứng TRONG phòng kính; chỉ khi bước vào
 *    bên trong cánh cửa kính mới hiện nút bấm ▲ Lên / ▼ Xuống tầng.
 *  - Dốc liên tầng B1-B2-B3-B4: thiết kế dốc chữ U gập đôi (vòng cung 180°) tiết kiệm diện tích
 *    (như hình 14): làn ngoài xuống tầng dưới, làn trong lên tầng trên, bảng cam hiển thị rõ
 *    "Làn xe xuống B2 | Làn xe lên B1", sửa triệt để lỗi hộp đèn trắng lơ lửng giữa dốc,
 *    và bố trí Booth tại cửa vào/ra B2 kèm luồng di chuyển 1 chiều mạch lạc trong B2.
 */
(function (root) {
  'use strict';
  var I = root.__P3R_INTERNAL__;
  var U = I.util, M = I.M, FH = I.C.FH, CLEAR = I.C.CLEAR, SLAB = I.C.SLAB;
  var T;

  /* ------------------------------------------------------------------ */
  /* HÌNH HỌC BỐ CỤC GỌN (mét) - ĐÃ XOÁ THANG MÁY GIỮA & LỐI ĐI CUỐI BÃI */
  /* ------------------------------------------------------------------ */
  var SW = 2.6, SL = 5.0;
  var WM = 8.8, XSR = 8 * SW, ECL = 0.8, COR = 8.6;              // Bỏ lối đi dọc cuối khu để xe (ECL = 0.8m sát đầu dãy xe)
  var HX = (WM + XSR + ECL + COR) / 2, HZ = 23.0;                // HX = 19.5m, HZ = 23.0m
  var XS0 = -HX + WM, XE = XS0 + XSR;                            // dãy slot: [-10.7 .. +10.1]
  var BX = -HX + 5.8;                                            // làn trục Tây (xe tháng sau check-in)
  var XEC = XE - 1.3;                                            // điểm cuối trong từng lối đi 1-2-3 (không nối dọc trên B1)
  var XR = XE + ECL;                                             // gờ đỏ ngăn cách sát cuối khu để xe (x = +10.9)
  var XY = XR + 2.2, XB = XR + 4.3, XG = XR + 6.4;               // làn vàng (13.1), cọc tiêu (15.2), làn xanh lá (17.3)

  // Dốc chữ U gập đôi (tiết kiệm diện tích theo hình 14):
  //   - Làn TRÁI (TRONG, sát tường ngăn giữa z = -14.8): hướng TỪ TRÊN XUỐNG (ZD1 = -17.2 -> ZD2 = -12.4)
  //   - Làn PHẢI (NGOÀI, sát tường bao ngoài):           hướng TỪ DƯỚI LÊN   (ZU2 = -8.6  -> ZU1 = -21.0)
  var RL = 12.5;                                                 // chiều dài đoạn dốc nghiêng (12.5m)
  var Z_RAMP_C = -14.8;                                          // tâm vòng cung chữ U
  var R_OUT = 8.2, R_DIV = 4.3, R_IN = 0.6;                      // bán kính ngoài, vạch phân làn giữa, trụ tâm
  var ZD1 = -17.2, ZU1 = -21.0;                                  // nhánh Nam: Trong (-17.2) từ trên xuống, Ngoài (-21.0) từ dưới lên
  var ZD2 = -12.4, ZU2 = -8.6;                                   // nhánh Bắc: Trong (-12.4) từ trên xuống, Ngoài (-8.6) từ dưới lên
  var Z_ENTRY = -18.2;                                           // làn xe vào từ dốc mặt đất ở phía Tây B1
  var XT = HX + RL + R_OUT;                                      // mép Đông ngoài cùng của vòng cung chữ U
  var ZEG = 16.5, ZEY = 20.5;                                    // làn ra mặt đất xanh lá / vàng

  var ROWS = [
    { id: 'A', zc: -12.5, face: 1 },
    { id: 'B', zc: -1.5,  face: 1 },
    { id: 'C', zc: 9.5,   face: -1 },
    { id: 'D', zc: 20.5,  face: -1 }
  ];
  var AZ = [-7.0, 4.0, 15.0];                                    // 3 lối đi chính (Lối đi 1, 2, 3)
  var COLX = [XS0 + SW * 2, XS0 + SW * 4, XS0 + SW * 6];         // 3 hàng cột gọn giữa các ô đỗ

  var LAYOUT = { HX: HX, HZ: HZ, XT: XT, RL: RL, BX: BX, XR: XR, XG: XG, XY: XY, XEC: XEC };
  I.layout = LAYOUT;

  function slotX(i, n) {
    var span = n * SW, start = XS0 + (XSR - span) / 2;
    return start + (i - 0.5) * SW;
  }

  /* ---------- vật liệu bổ sung ---------- */
  function extraMaterials() {
    if (M.paintInst) return;
    M.paintInst = new T.MeshBasicMaterial({ color: 0xffffff, polygonOffset: true, polygonOffsetFactor: -2, polygonOffsetUnits: -2 });
    M.sepInst = new T.MeshStandardMaterial({ color: 0xffffff, roughness: 0.65 });
    M.pad = new T.MeshBasicMaterial({ color: 0xffffff, transparent: true, opacity: 0.22, depthWrite: false, polygonOffset: true, polygonOffsetFactor: -1, polygonOffsetUnits: -1 });
    M.fin = new T.MeshStandardMaterial({ color: 0xd9dadc, roughness: 0.8 });
    M.tile = new T.MeshStandardMaterial({ color: 0xd8d3c8, roughness: 0.45 });
    M.kerbRed = new T.MeshStandardMaterial({ color: 0xc62828, roughness: 0.65 });
    M.bollard = new T.MeshStandardMaterial({ color: 0xffffff, roughness: 0.5 });
  }

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
    var p = new T.Mesh(new T.PlaneGeometry(w || 1.15, l || 2.3), colorArrowMat(color));
    p.rotation.set(-Math.PI / 2, 0, 0);
    var h = new T.Group(); h.add(p); h.rotation.y = rotY; h.position.set(x, y + 0.014, z);
    parent.add(h);
  }
  /** Chữ sơn trên sàn */
  function floorText(parent, x, z, rotY, w, l, lines, bg, fg) {
    var tex = U.signTex(lines, bg, fg, 512, Math.round(512 * l / w));
    var m = new T.MeshBasicMaterial({ map: tex, transparent: true, polygonOffset: true, polygonOffsetFactor: -4, polygonOffsetUnits: -4 });
    var p = new T.Mesh(new T.PlaneGeometry(w, l), m);
    p.rotation.set(-Math.PI / 2, 0, 0);
    var h = new T.Group(); h.add(p); h.rotation.y = rotY; h.position.set(x, 0.016, z);
    parent.add(h);
  }
  var ROT_E = -Math.PI / 2, ROT_W = Math.PI / 2, ROT_N = Math.PI, ROT_S = 0;

  /** tam giác tường chắn hào dốc */
  function triWall(parent, xa, xb, yBa, yBb, yTop, z) {
    var sh = new T.Shape();
    sh.moveTo(xa, yBa); sh.lineTo(xb, yBb); sh.lineTo(xb, yTop); sh.lineTo(xa, yTop); sh.closePath();
    var g = new T.ShapeGeometry(sh), uv = g.attributes.uv;
    for (var i = 0; i < uv.count; i++) uv.setXY(i, uv.getX(i) / 4, uv.getY(i) / 4);
    var m = new T.Mesh(g, M.conc); m.position.z = z; parent.add(m);
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
  /*  PHÒNG THANG MÁY RIÊNG (vách kính + điểm đứng TRONG & NGOÀI cửa)    */
  /* ================================================================== */
  function buildLobby(fl, o) {
    var g = new T.Group();
    g.position.set(o.cx, 0, o.cz);
    g.rotation.y = Math.atan2(o.fx, o.fz);           // local +z hướng ra cửa kính
    var W = 5.0, H = CLEAR, D = 4.6;
    b3(g, W, H, 1.5, M.plaster, 0, H / 2, 0.75);      // khối giếng thang
    b3(g, 0.2, H, D, M.plaster, -W / 2 + 0.1, H / 2, D / 2);
    b3(g, 0.2, H, D, M.plaster, W / 2 - 0.1, H / 2, D / 2);
    b3(g, W, 0.14, D + 0.1, M.plaster, 0, H - 0.07, D / 2);
    b3(g, 0.7, H, 0.2, M.plaster, -W / 2 + 0.45, H / 2, D - 0.1);
    b3(g, 0.7, H, 0.2, M.plaster, W / 2 - 0.45, H / 2, D - 0.1);
    var cab = new T.Mesh(new T.PlaneGeometry(2.0, 2.3), M.cabin); cab.position.set(0, 1.2, 1.51); g.add(cab);
    var slide = [];
    function slider(mesh, base, dist) { g.add(mesh); slide.push({ mesh: mesh, base: base, dir: base < 0 ? -1 : 1, dist: dist }); }
    [-0.5, 0.5].forEach(function (x) { slider(U.box(null, 1.0, 2.3, 0.05, M.steel, x, 1.2, 1.56), x, 0.95); });
    b3(g, 2.2, 0.14, 0.2, M.frame, 0, 2.42, 1.6);
    b3(g, 0.16, 0.36, 0.05, M.dark, 1.65, 1.15, 1.54);
    b3(g, 0.07, 0.07, 0.02, M.green, 1.65, 1.25, 1.57);
    b3(g, 0.07, 0.07, 0.02, M.tail, 1.65, 1.07, 1.57);
    var tile = new T.Mesh(new T.PlaneGeometry(W - 0.3, D - 1.5), M.tile); tile.rotation.x = -Math.PI / 2; tile.position.set(0, 0.012, 1.5 + (D - 1.5) / 2); g.add(tile);
    b3(g, 1.8, 0.04, 0.3, M.lamp, 0, H - 0.17, 3.0);
    function pane(w, h, x, y, z) { var m = new T.Mesh(new T.PlaneGeometry(w, h), M.glass); m.position.set(x, y, z); return m; }
    g.add(pane(0.85, 3.9, -1.32, 1.95, D - 0.02)); g.add(pane(0.85, 3.9, 1.32, 1.95, D - 0.02));
    g.add(pane(1.8, 1.4, 0, 3.25, D - 0.02));
    [-0.45, 0.45].forEach(function (x) {
      var door = new T.Group(); door.add(pane(0.9, 2.5, 0, 1.25, 0)); b3(door, 0.9, 0.05, 0.05, M.frame, 0, 2.5, 0); b3(door, 0.05, 2.5, 0.05, M.frame, x < 0 ? -0.44 : 0.44, 1.25, 0);
      door.position.set(x, 0, D - 0.04);
      slider(door, x, 0.9);
    });
    [-1.8, 0, 1.8].forEach(function (x) { b3(g, 0.06, 3.95, 0.06, M.frame, x, 1.975, D - 0.03); });
    b3(g, 3.6, 0.06, 0.06, M.frame, 0, 3.9, D - 0.03);
    var dm = new T.Mesh(new T.PlaneGeometry(1.4, 1.4), M.paintGreen); dm.rotation.x = -Math.PI / 2; dm.position.set(0, 0.02, D + 0.75); g.add(dm);
    fl.group.add(g);

    var s = Math.sin(g.rotation.y), c = Math.cos(g.rotation.y);
    // pos: điểm đứng BÊN TRONG cánh cửa kính (z_local = 3.0 < D = 4.6)
    var inPos = new T.Vector3(o.cx + s * 3.0, fl.y, o.cz + c * 3.0);
    // outPos: điểm đứng BÊN NGOÀI cánh cửa kính (trên thảm xanh z_local = 5.4 > D = 4.6)
    var outPos = new T.Vector3(o.cx + s * 5.4, fl.y, o.cz + c * 5.4);
    fl.lobbies.push({
      id: o.id, name: o.name, pos: inPos, outPos: outPos,
      slide: slide, open: 0, fx: o.fx, fz: o.fz, lane: o.lane, floorIdx: fl.k, group: g
    });
  }

  /* ================================================================== */
  /*  BOOTH QUẸT THẺ + BARRIER (Đảo gọn, Máy quẹt thẻ đặt nằm ngang)     */
  /* ================================================================== */
  function buildGate(parent, fl, o) {
    var g = new T.Group(); g.position.set(o.x, 0, o.z);
    g.rotation.y = Math.atan2(-o.dir[1], o.dir[0]);       // local +x = hướng xe chạy
    var sg = o.side || 1;                                 // sg = +1: đặt bên PHẢI làn xe; sg = -1: bên TRÁI làn xe

    // Đảo bệ gọn (dài 3.4m dọc làn xe để không chắn ngang đường giao cắt)
    b3(g, 3.4, 0.16, 1.15, M.conc, 0, 0.08, 2.25 * sg);
    b3(g, 3.4, 0.04, 1.18, M.hazard, 0, 0.17, 2.25 * sg);

    // Trụ Barrier đỏ-trắng ở giữa đảo (x = 0)
    b3(g, 0.40, 1.05, 0.36, M.red, 0, 0.70, 1.92 * sg);
    b3(g, 0.42, 0.06, 0.38, M.dark, 0, 1.25, 1.92 * sg);
    var redM = new T.MeshBasicMaterial({ color: 0xff2a2a }), grM = new T.MeshBasicMaterial({ color: 0x0b3d1d });
    b3(g, 0.1, 0.1, 0.02, redM, -0.21, 1.08, 1.92 * sg);
    b3(g, 0.1, 0.1, 0.02, grM,  -0.21, 0.92, 1.92 * sg);
    var lift = new T.Group(); lift.position.set(0, 1.10, 1.72 * sg); g.add(lift);
    var arm = U.box(lift, 0.10, 0.11, 3.2, M.barrier, 0, 0, -1.6 * sg);

    // Máy quẹt thẻ (Card Reader) đặt NẰM NGANG dọc theo chiều xe chạy, mặt hướng thẳng vào cửa sổ tài xế (-z * sg)
    var rd = new T.Group(); rd.position.set(-1.12, 0, 1.92 * sg); g.add(rd);
    b3(rd, 0.56, 1.12, 0.24, M.dark, 0, 0.72, 0);         // thân ngang (rộng 0.56m dọc làn, mỏng 0.24m)
    b3(rd, 0.60, 0.06, 0.28, M.steel, 0, 1.30, 0);
    var scr = new T.Mesh(
      new T.PlaneGeometry(0.44, 0.22),
      U.signMat(U.signTex([o.readerLine || 'QUẸT THẺ', 'TAP CARD HERE'], '#0a1a2a', '#6fe3ff', 320, 160))
    );
    scr.position.set(0, 1.06, -0.125 * sg);
    scr.rotation.y = sg > 0 ? Math.PI : 0;                // quay mặt màn hình về phía làn xe
    rd.add(scr);
    b3(rd, 0.28, 0.05, 0.02, M.green, 0, 0.82, -0.125 * sg);
    b3(rd, 0.22, 0.02, 0.14, M.cabin, 0, 1.34, 0);

    // Cabin Booth bảo vệ nằm gọn phía sau trụ barrier trên cùng đảo bên phải
    var b = new T.Group(); b.position.set(0.95, 0, 2.32 * sg); g.add(b);
    b3(b, 1.25, 1.0, 0.95, M.plaster, 0, 0.5, 0);
    var gl = new T.Mesh(new T.BoxGeometry(1.22, 0.98, 0.92), M.glass); gl.position.set(0, 1.49, 0); b.add(gl);
    [[-0.58, -0.44], [0.58, -0.44], [-0.58, 0.44], [0.58, 0.44]].forEach(function (c) { b3(b, 0.05, 1.0, 0.05, M.frame, c[0], 1.5, c[1]); });
    b3(b, 1.45, 0.10, 1.15, M.dark, 0, 2.04, 0);
    b3(b, 0.7, 0.04, 0.5, M.lamp, 0, 1.97, 0);

    // Chỉ dựng bảng trên nóc booth khi o.showRoofSign = true
    if (o.showRoofSign && o.label) {
      U.cyl(b, 0.04, 0.9, M.steel, 0, 2.50, 0);
      var tx = U.signTex([o.label, o.label2 || ''], o.color || '#0b5cad', '#ffffff', 560, 170);
      var faceFront = o.roofSignFront !== false;
      var l1 = new T.Mesh(new T.PlaneGeometry(2.4, 0.76), U.signMat(tx));
      l1.position.set(faceFront ? -0.05 : 0, 2.92, 0);
      l1.rotation.y = -Math.PI / 2;
      b.add(l1);
      var l2 = new T.Mesh(new T.PlaneGeometry(2.4, 0.76), U.signMat(tx));
      l2.position.set(faceFront ? 0.05 : 0, 2.92, 0);
      l2.rotation.y = Math.PI / 2;
      b.add(l2);
    }

    parent.add(g);
    var gate = {
      kind: o.kind, group: g, lift: lift, open: false, forced: false, redM: redM, grM: grM,
      openAngle: sg * Math.PI / 2, pos: new T.Vector3(o.x, fl.y, o.z), label: o.label || o.kind
    };
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
        c.fillStyle = s.zone === 'MONTHLY' ? '#8ec5ff' : '#e2e8f0';
        c.fillText(s.code, (i % cols) * cw + cw / 2, Math.floor(i / cols) * ch + ch / 2);
      });
    });
    var pos = [], uv = [], idx = [];
    fl.slots.forEach(function (s, i) {
      var f = s.face, w = 1.5, h = 0.6, cx = s.x, cz = s.zc + f * (SL / 2 + 0.68);
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
      var f = s.face, back = s.zc - f * SL / 2;
      var z = back + f * 0.1, y = 2.05;
      poles.push({ x: s.x, y: 1.02, z: back + f * 0.03, sx: 0.06, sy: 2.04, sz: 0.06, color: 0x3a3f47 });
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
          vehicle: occ ? (R() < 0.15 ? 'SUV 7 chỗ' : 'Sedan 4-5 chỗ') : null, carColor: 0,
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

    /* ---- sàn sảnh chính ---- */
    U.floorRect(g, -HX, HX, -HZ, HZ, 0);

    /* ---- tường bao sảnh (mở cửa tại các đầu dốc) ---- */
    wallLine(ctx, 'x', -HZ, -HX, HX, [], true);
    wallLine(ctx, 'x',  HZ, -HX, HX, [], true);
    wallLine(ctx, 'z', -HX, -HZ, HZ, monthly ? [{ c: Z_ENTRY, w: 5.2, h: 3.8 }] : [], true);

    var eGaps = [];
    // Cửa nhánh Nam (dốc xuống từ tầng k sang k+1)
    if (k < n - 1) eGaps.push({ c: (ZD1 + ZU1) / 2, w: 8.2, h: 3.8 });
    // Cửa nhánh Bắc (dốc từ tầng k-1 xuống tầng k)
    if (k > 0) eGaps.push({ c: (ZD2 + ZU2) / 2, w: 8.2, h: 3.8 });
    // Cửa dốc ra mặt đất ở tầng đầu tiên (B1 / T1)
    if (monthly) eGaps.push({ c: (ZEG + ZEY) / 2, w: 8.4, h: 3.8 });
    wallLine(ctx, 'z', HX, -HZ, HZ, eGaps, true);

    if (ctx.fins.length) U.inst(g, U.unitBox(), M.fin, ctx.fins);

    /* ---- trần sảnh ---- */
    var cMesh = U.texPlane(HX * 2, HZ * 2, M.ceil, HX / 2, HZ / 2);
    cMesh.rotation.x = Math.PI / 2; cMesh.position.y = CLEAR - 0.01; ceil.add(cMesh);

    /* ---- vạch ô đỗ, tấm nền, gờ phân cách + chặn lùi bánh xe (wheel-stop) ---- */
    var lines = [], pads = [], seps = [], stops = [], cones = [];
    var lineCol = monthly ? 0x4aa3ff : 0xf2c200;
    ROWS.forEach(function (row, ri) {
      var cnt = def.counts[k][ri];
      if (!cnt) return;
      var f = row.face, zBack = row.zc - f * SL / 2;
      var xL = slotX(1, cnt) - SW / 2, xR = slotX(cnt, cnt) + SW / 2;
      lines.push({ x: (xL + xR) / 2, y: 0.01, z: zBack, sx: xR - xL, sy: 1, sz: 0.12, color: lineCol });
      for (var i = 0; i <= cnt; i++) {
        var x = xL + i * SW;
        lines.push({ x: x, y: 0.01, z: row.zc, sx: 0.11, sy: 1, sz: SL, color: lineCol });
        if (i > 0 && i < cnt) {
          seps.push({ x: x, y: 0.07, z: row.zc - f * 0.25, sx: 0.14, sy: 0.14, sz: SL - 0.9, color: 0x9aa0a6 });
        }
      }
    });
    fl.slots.forEach(function (s) {
      var col = s.status === 'OCCUPIED' ? 0xd9534f : (s.status === 'EMPTY' ? 0x2eb872 : 0xf0ad4e);
      pads.push({ x: s.x, y: 0.006, z: s.zc, sx: SW - 0.34, sy: 1, sz: SL - 0.34, color: col });
      // Chặn lùi bánh xe (wheel-stop vàng đen kiểu hầm CGTrader)
      var zStop = s.zc - s.face * (SL / 2 - 0.55);
      stops.push({ x: s.x, y: 0.06, z: zStop, sx: 1.5, sy: 0.12, sz: 0.22, color: 0xf6c700 });
      if (s.status === 'MAINTENANCE') {
        [-0.55, 0.55].forEach(function (dx) { cones.push({ x: s.x + dx, y: 0.35, z: s.zc + s.face * 1.4, sx: 1, sy: 1, sz: 1 }); });
      }
    });

    /* ---- vạch tim đường các lối đi ---- */
    AZ.forEach(function (z) {
      for (var x = BX + 2; x <= XEC - 2; x += 4.2) {
        lines.push({ x: x, y: 0.01, z: z, sx: 1.8, sy: 1, sz: 0.12, color: 0xd8dee6 });
      }
    });

    /* ================================================================== */
    /*  PHÂN LUỒNG TẦNG B1 (XE THÁNG + LÀN BYPASS VÃNG LAI + 2 LANE RA)    */
    /* ================================================================== */
    if (monthly) {
      // 1. Gờ đỏ ngăn cách hoàn toàn làn bypass xe vãng lai (phía Nam) với khu đỗ xe tháng:
      //    Chừa đúng cửa rẽ phải vào Booth Check-in tại x = BX (rộng 4.4m)
      var openL = BX - 2.2, openR = BX + 2.2;
      b3(g, openL - (-HX), 0.26, 0.42, M.kerbRed, (-HX + openL) / 2, 0.13, -15.6);
      b3(g, XR - openR,    0.26, 0.42, M.kerbRed, (openR + XR) / 2,  0.13, -15.6);
      lines.push({ x: (openR + XR) / 2, y: 0.012, z: -16.1, sx: XR - openR, sy: 1, sz: 0.18, color: 0xff3b3b });

      // 2. Gờ đỏ dọc biên Đông (x = XR = sát cuối các dãy đỗ xe, đã bỏ lối đi dọc cuối bãi):
      //    Chặn liền mạch từ z = -15.6 lên tới z = 12.3 (mép Nam Lối đi 3) + khoá ngang đầu Nam làn vàng (XR -> XB),
      //    mở cửa tại Lối đi 3 (z = 12.3 .. 16.5) để xe ra làn vàng XY qua Booth Check-out, rồi nối tiếp từ z = 19.9 tới HZ.
      b3(g, 0.42, 0.26, 12.3 - (-15.6), M.kerbRed, XR, 0.13, (-15.6 + 12.3) / 2);
      b3(g, XB - XR, 0.26, 0.42,        M.kerbRed, (XR + XB) / 2, 0.13, 12.3);
      b3(g, 0.42, 0.26, HZ - 19.9,      M.kerbRed, XR, 0.13, (19.9 + HZ) / 2);

      // 3. Booth CHECK-IN Xe Tháng:
      //    Đặt bên PHẢI làn rẽ vào (side = +1 -> phía Đông của BX),
      //    máy quẹt thẻ đặt nằm ngang dọc làn xe, gắn bảng LED báo số Slot trống ngay tại Booth Check-in!
      var free0 = lot.free[0], tot0 = lot.slots[0].length;
      buildGate(g, fl, {
        kind: 'checkin', x: BX, z: -13.2, dir: [0, 1], side: 1,
        showRoofSign: true,
        label: 'TẦNG ' + fid + ' · XE THÁNG',
        label2: 'Còn trống ' + free0 + '/' + tot0 + ' chỗ',
        color: free0 > 5 ? '#0b8a43' : (free0 > 0 ? '#c46a00' : '#b83227'),
        readerLine: 'CHECK-IN THÁNG'
      });

      // 4. Booth CHECK-OUT Xe Tháng:
      //    Dời sang chiều ngang trên làn ra xe tháng (XY, dir = [0, 1], side = +1 tương tự Booth Check-in):
      //    Đảo booth nằm dọc sát biên Đông dãy D (x = XR), thanh chắn barrier vươn ngang qua làn vàng XY!
      buildGate(g, fl, {
        kind: 'checkout-m', x: XY + 0.1, z: 18.2, dir: [0, 1], side: 1,
        showRoofSign: false, readerLine: 'CHECK-OUT THÁNG'
      });

      // 6. Hàng cọc tiêu cố định (bollards) chia đôi 2 làn sau khi Check-out:
      //    Làn trái (XY, mũi tên VÀNG - xe tháng) và Làn phải (XG, mũi tên XANH LÁ - xe vãng lai từ B2 lên)
      var bol = [], bBases = [];
      for (var bz = 13.0; bz <= 21.2; bz += 1.65) {
        bBases.push({ x: XB, y: 0.04, z: bz, sx: 0.32, sy: 0.08, sz: 0.32, color: 0x1c2026 });
        bol.push({ x: XB, y: 0.42, z: bz, sx: 1, sy: 1, sz: 1 });
        lines.push({ x: XB, y: 0.012, z: bz, sx: 0.16, sy: 1, sz: 0.9, color: 0xf2c200 });
      }
      for (var bx = XB + 1.4; bx <= HX - 0.4; bx += 1.6) {
        bBases.push({ x: bx, y: 0.04, z: 18.5, sx: 0.32, sy: 0.08, sz: 0.32, color: 0x1c2026 });
        bol.push({ x: bx, y: 0.42, z: 18.5, sx: 1, sy: 1, sz: 1 });
      }
      U.inst(g, U.getGeo('bollard', function () { return new T.CylinderGeometry(0.09, 0.13, 0.76, 12); }), M.cone, bol);
      U.inst(g, U.unitBox(), M.sepInst, bBases);

      // Vạch dẫn hướng vàng & xanh lá
      lines.push({ x: XY - 1.5, y: 0.012, z: 17.5, sx: 0.14, sy: 1, sz: 6.0, color: 0xf2c200 });
      lines.push({ x: XG + 1.5, y: 0.012, z: 0.5,  sx: 0.14, sy: 1, sz: 34.0, color: 0x2fcf6a });

      // Mũi tên XANH DƯƠNG: làn bypass xe vãng lai đi thẳng vào làn TRONG (ZD1 = -17.2) để xuống dốc B2
      //   (tách biệt hoàn toàn khỏi mũi tên VÀNG ở khúc rẽ Check-in đầu tiên, bỏ bảng Vãng lai dưới đất)
      [-HX + 1.5, BX - 1.6, -6.2, 1.5, 8.5, 15.0].forEach(function (ax) {
        arrowC(g, ax, 0, ZD1 - 0.8, ROT_E, '#2d8cff', 1.15, 2.3);
      });
      // Mũi tên VÀNG: nhánh rẽ phải vào Check-in xe tháng (đặt riêng ở phần đường rẽ vào BX)
      arrowC(g, BX, 0, -15.4, ROT_N, '#f2c200', 1.1, 2.2);
      arrowC(g, BX, 0, -10.2, ROT_N, '#f2c200', 1.1, 2.2);
      floorText(g, BX, -9.8, ROT_N, 3.0, 1.3, ['XE THÁNG'], '#0b5cad', '#ffffff');

      // Mũi tên VÀNG: xe tháng từ Lối đi 3 qua Check-out đi làn trái (XY) ra dốc mặt đất
      arrowC(g, XE - 2.0, 0, 15.0, ROT_E, '#f2c200', 1.1, 2.2);
      arrowC(g, XY, 0, 15.5, ROT_N, '#f2c200', 1.15, 2.3);
      arrowC(g, XY, 0, 18.4, ROT_N, '#f2c200', 1.15, 2.3);
      arrowC(g, XY + 1.5, 0, ZEY, ROT_E, '#f2c200', 1.15, 2.3);
      arrowC(g, HX - 1.5, 0, ZEY, ROT_E, '#f2c200', 1.15, 2.3);

      // Mũi tên XANH LÁ: xe vãng lai từ làn NGOÀI dốc B2 (ZU1 = -21.0) đi thẳng làn phải (XG) ra dốc mặt đất
      arrowC(g, HX - 1.8, 0, ZU1, ROT_W, '#2fcf6a', 1.15, 2.3);
      for (var gz = -13; gz <= 13; gz += 6.5) arrowC(g, XG, 0, gz, ROT_N, '#2fcf6a', 1.15, 2.3);
      arrowC(g, XG + 1.2, 0, ZEG, ROT_E, '#2fcf6a', 1.15, 2.3);

      // Mũi tên trắng chỉ hướng trong khu đỗ xe tháng (trục Tây BX dẫn lên Lối đi 3 -> Check-out)
      [0, 5, 11].forEach(function (z) { U.floorArrow(g, BX, 0, z, ROT_N); });
      [-4, 4].forEach(function (x) {
        U.floorArrow(g, x, 0, AZ[0], ROT_W);
        U.floorArrow(g, x, 0, AZ[1], ROT_W);
        U.floorArrow(g, x, 0, AZ[2], ROT_E);
      });
    } else {
      /* ================================================================== */
      /*  PHÂN LUỒNG CÁC TẦNG VÃNG LAI (B2, B3, B4):                        */
      /*   - Làn TRÁI / TRONG (ZD2 = -12.4): từ Trên Xuống vào B2           */
      /*   - Làn PHẢI / NGOÀI (ZU2 = -8.6):  từ Dưới Lên B1 (có Booth B2)   */
      /* ================================================================== */
      // Gờ đỏ phân cách giữa làn Phải/Ngoài đi lên (ZU2 = -8.6) và làn Trái/Trong đi xuống (ZD2 = -12.4)
      b3(g, 8.2, 0.24, 0.36, M.kerbRed, HX - 4.1, 0.12, -10.5);

      // Tại B2 (k === 1): đặt Booth CHECK-OUT Vãng lai ở làn BÊN PHẢI / NGOÀI (ZU2 = -8.6),
      //   side = 1 để cabin booth & máy quẹt thẻ nằm gọn ở lề phải (z = -6.35), thanh chắn vươn qua làn phải!
      if (k === 1) {
        buildGate(g, fl, {
          kind: 'checkout-v', x: HX - 4.2, z: ZU2, dir: [1, 0], side: 1,
          showRoofSign: true, label: 'CHECK-OUT VÃNG LAI', label2: 'QUẸT THẺ LÊN ' + def.ids[0],
          color: '#b83227', readerLine: 'CHECK-OUT VÃNG LAI'
        });
      }

      // Mũi tên XANH DƯƠNG: luồng xe từ Trên Xuống theo làn TRÁI / TRONG (ZD2 = -12.4, hướng Tây -x vào B2)
      arrowC(g, HX - 2.2, 0, ZD2, ROT_W, '#2d8cff', 1.15, 2.3);
      arrowC(g, XEC,      0, ZD2, ROT_W, '#2d8cff', 1.15, 2.3);
      arrowC(g, -4.0,     0, -15.5, ROT_W, '#2d8cff', 1.1, 2.2);
      if (k < n - 1) {
        // Nhánh rẽ xuống tầng tiếp theo (k + 1) ở làn TRONG nhánh Nam (ZD1 = -17.2)
        arrowC(g, XG, 0, -15.2, ROT_S, '#2d8cff', 1.1, 2.2);
        arrowC(g, HX - 2.5, 0, ZD1, ROT_E, '#2d8cff', 1.15, 2.3);
        floorText(g, HX - 6.5, ZD1, ROT_E, 4.0, 1.4, ['XUỐNG ' + def.ids[k + 1] + ' ➔'], '#0b5cad', '#ffffff');
        // Xe từ tầng dưới (k + 1) đi lên tại làn NGOÀI nhánh Nam (ZU1 = -21.0)
        arrowC(g, HX - 2.2, 0, ZU1, ROT_W, '#2fcf6a', 1.1, 2.2);
      }

      // Mũi tên XANH LÁ: luồng xe đi từ Dưới Lên theo làn PHẢI / NGOÀI (ZU2 = -8.6, hướng Đông +x lên B1)
      arrowC(g, XEC - 2.5, 0, ZU2, ROT_E, '#2fcf6a', 1.15, 2.3);
      arrowC(g, HX - 1.5,  0, ZU2, ROT_E, '#2fcf6a', 1.15, 2.3);

      // Mũi tên trắng lưu thông trong các lối đi B2+
      [-5, 4].forEach(function (x) {
        U.floorArrow(g, x, 0, AZ[0], ROT_W);
        U.floorArrow(g, x, 0, AZ[1], ROT_E);
        U.floorArrow(g, x, 0, AZ[2], ROT_W);
      });
      [-2, 9].forEach(function (z) {
        U.floorArrow(g, BX, 0, z, ROT_N);
        U.floorArrow(g, XG, 0, z, ROT_S);
      });
    }

    U.inst(g, U.flatQuad(), M.paintInst, lines);
    U.inst(g, U.flatQuad(), M.pad, pads);
    U.inst(g, U.unitBox(), M.sepInst, seps);
    U.inst(g, U.unitBox(), M.hazard, stops);
    if (cones.length) U.inst(g, U.getGeo('cone', function () { var c = new T.CylinderGeometry(0.05, 0.22, 0.7, 10); return c; }), M.cone, cones);

    /* ---- vạch đi bộ tới 2 phòng thang máy ---- */
    for (var s1 = -1.6; s1 <= 1.6; s1 += 0.45) {
      b3(g, 0.24, 0.01, 1.4, M.paintWhite, BX + s1, 0.012, 16.8);
      b3(g, 1.4, 0.01, 0.24, M.paintWhite, -HX + 5.2, 0.012, 4.0 + s1);
    }

    /* ---- cột bê tông sơn 2 tông màu + ốp góc cao su vàng-đen + bình PCCC ---- */
    var cTop = [], cBot = [], cBand = [], cBase = [], fBox = [];
    var rowZs = [-12.5, -1.5, 9.5, 20.5];
    COLX.forEach(function (cx, ci) {
      rowZs.forEach(function (rz, ri) {
        var cz = rz + (ri < 2 ? -SL / 2 : SL / 2);
        // Phần trên xám bê tông, dải trắng ở cao độ 1.65m, phần dưới xanh rêu đậm (giống mẫu CGTrader Image 5)
        cTop.push({ x: cx, y: 1.7 + (CLEAR - 1.7) / 2, z: cz, sx: 0.58, sy: CLEAR - 1.7, sz: 0.58 });
        cBand.push({ x: cx, y: 1.62, z: cz, sx: 0.60, sy: 0.16, sz: 0.60, color: 0xdfe5ec });
        cBot.push({ x: cx, y: 0.82, z: cz, sx: 0.59, sy: 1.44, sz: 0.59 });
        cBase.push({ x: cx, y: 0.35, z: cz, sx: 0.63, sy: 0.70, sz: 0.63 });
        if ((ci + ri) % 2 === 0) {
          var fz = cz + (ri < 2 ? 0.35 : -0.35);
          fBox.push({ x: cx, y: 1.25, z: fz, sx: 0.28, sy: 0.48, sz: 0.12, color: 0xc62828 });
        }
      });
    });
    U.inst(g, U.unitBox(), M.conc, cTop);
    U.inst(g, U.unitBox(), M.sepInst, cBand);
    U.inst(g, U.unitBox(), M.pillarCol, cBot);
    U.inst(g, U.unitBox(), M.hazard, cBase);
    U.inst(g, U.unitBox(), M.sepInst, fBox);

    /* ---- dầm bê tông, ống thông gió tôn mạ kẽm vuông (HVAC loop) & đèn máng đôi ---- */
    var beams = [], ducts = [], lamps = [], lBodies = [];
    [-15.0, -4.0, 7.0, 18.0].forEach(function (bz) {
      beams.push({ x: -1.5, y: CLEAR - 0.22, z: bz, sx: HX * 2 - 2, sy: 0.42, sz: 0.38 });
    });
    // Hệ thống ống gió vuông tôn mạ kẽm chạy vòng trên trần (như hình mẫu CGTrader Image 5)
    AZ.forEach(function (az) {
      ducts.push({ x: -2.0, y: CLEAR - 0.52, z: az - 2.3, sx: XE - BX + 6, sy: 0.34, sz: 0.58 });
      U.cyl(ceil, 0.045, HX * 2 - 4, M.pipeRed,  0, CLEAR - 0.36, az + 2.3, 'x');
      U.cyl(ceil, 0.038, HX * 2 - 4, M.pipeBlue, 0, CLEAR - 0.46, az + 2.42, 'x');
      // Đèn máng huỳnh quang đôi bố trí tránh vị trí bảng treo trần
      [-10.5, -3.5, 3.5].forEach(function (lx) {
        lBodies.push({ x: lx, y: CLEAR - 0.06, z: az, sx: 1.5, sy: 0.06, sz: 0.28, color: 0x2b3038 });
        lamps.push({ x: lx, y: CLEAR - 0.10, z: az - 0.06, sx: 1.4, sy: 0.03, sz: 0.06 });
        lamps.push({ x: lx, y: CLEAR - 0.10, z: az + 0.06, sx: 1.4, sy: 0.03, sz: 0.06 });
      });
    });
    // Ống gió nối dọc 2 đầu
    ducts.push({ x: BX + 1.5, y: CLEAR - 0.52, z: 4.0, sx: 0.58, sy: 0.34, sz: 22.0 });
    ducts.push({ x: XE - 1.0, y: CLEAR - 0.52, z: 4.0, sx: 0.58, sy: 0.34, sz: 22.0 });

    U.inst(ceil, U.unitBox(), M.beam, beams);
    U.inst(ceil, U.unitBox(), M.duct, ducts);
    U.inst(ceil, U.unitBox(), M.sepInst, lBodies);
    U.inst(ceil, U.unitBox(), M.lamp, lamps);

    /* ---- 2 phòng thang máy riêng sát tường (ĐÃ XOÁ THANG MÁY GIỮA BÃI) ---- */
    // 1. Phòng thang máy khu Bắc (nằm gọn ở góc Tây-Bắc đầu làn BX, không chiếm chỗ dãy D)
    buildLobby(fl, { id: 0, name: 'Thang máy khu Bắc', cx: BX, cz: HZ, fx: 0, fz: -1, lane: [BX, 15.0] });
    // 2. Phòng thang máy khu Tây (sát tường Tây, mở ra Lối đi 2)
    buildLobby(fl, { id: 1, name: 'Thang máy khu Tây', cx: -HX, cz: 4.0, fx: 1, fz: 0, lane: [BX, 4.0] });

    /* ---- cửa thoát hiểm ---- */
    emergencyDoor(fl, -HX, 15.0, 1, 0);
    emergencyDoor(fl,  HX, 4.0, -1, 0);

    /* ---- bảng chỉ dẫn treo trần ---- */
    addSigns(lot, fl);

    /* ---- nhãn mã slot, bảng IoT (B1) và xe 3D ---- */
    labelAtlas(fl);
    if (monthly) iotBoards(fl);
    U.buildCars(g, fl.slots, 0);

    return fl;
  }

  /* ================================================================== */
  /*  HỆ THỐNG BIỂN BÁO / BẢNG SỐ SLOT TRỐNG                              */
  /* ================================================================== */
  function addSigns(lot, fl) {
    var k = fl.k, n = lot.n, ids = lot.def.ids, free = lot.free;
    var BLUE = '#0b5cad', GREEN = '#0b8a43', AMB = '#c46a00', RED = '#b83227', DARK = '#0f172a';
    function S(lines, bg, w, h, fg) { return U.signTex(lines, bg, fg || '#ffffff', w || 512, h || 160); }
    function avail(i) { return free[i] > 5 ? GREEN : (free[i] > 0 ? AMB : RED); }
    function availLine(i) { return 'Còn trống ' + free[i] + '/' + lot.slots[i].length + ' chỗ'; }
    function hang(x, z, w, h, tp, tn, ax, d) { U.hangSign(fl.group, x, z, w, h, tp, tn, ax, d); }

    // Bảng chỉ dẫn đến 2 phòng thang máy
    var elN = S(['🛗 THANG MÁY KHU BẮC ▲'], BLUE, 460, 130);
    var elW = S(['◀ 🛗 THANG MÁY KHU TÂY'], BLUE, 460, 130);
    hang(BX, 11.5, 2.5, 0.68, elN, elN, 'z', 0.45);
    hang(-7.5, 4.0, 2.5, 0.68, elW, elW, 'x', 0.45);

    if (k === 0) {
      // 1. Điểm vừa xuống dốc vào bãi B1: bảng xanh CHỈ GHI "XE THÁNG | Rẽ phải ➔" đúng hướng rẽ phải, hạ cao độ vừa tầm mắt
      hang(-HX + 5.2, ZD1 + 0.4, 3.0, 0.86, null, S(['XE THÁNG', 'Rẽ phải ➔'], BLUE, 520, 160), 'x', 0.72);

      // 2. Bảng hướng dẫn Xe Vãng Lai đi thẳng xuống B2 (đặt lệch khỏi đèn trần để không bị cắt)
      hang(-HX + 12.5, ZD1, 3.3, 0.88, null, S(['XE VÃNG LAI ➔', 'Đi thẳng · xuống ' + (ids[1] || 'B2')], GREEN, 560, 160), 'x', 0.68);

      // 3. Bảng trên làn bypass vãng lai báo số slot trống tầng B2 trước khi vào dốc
      hang(1.5, ZD1, 3.2, 0.85, null, S(['LÀN VÃNG LAI ➔ ' + (ids[1] || 'B2'), 'Không vào khu xe tháng'], GREEN, 560, 155), 'x', 0.65);
      if (n > 1) {
        hang(HX - 8.5, ZD1, 3.3, 0.90, null, S(['XUỐNG ' + ids[1] + ' ➔', ids[1] + ': ' + availLine(1)], avail(1), 560, 165), 'x', 0.65);
      }

      // 4. Bảng chỉ dẫn hướng các xe DI CHUYỂN RA KHỎI BÃI khi đang ở trong khu Xe Tháng
      hang(BX, -2.5, 2.9, 0.78, null, S(['HƯỚNG RA BÃI ▲', 'Đi thẳng tới Lối đi 3 ➔'], GREEN, 520, 145), 'z', 0.42);
      hang(0.0, AZ[0], 2.9, 0.78, S(['◀ RA TRỤC TÂY · LÊN LỐI ĐI 3'], GREEN, 520, 145), null, 'x', 0.42);
      hang(0.0, AZ[1], 2.9, 0.78, S(['◀ RA TRỤC TÂY · LÊN LỐI ĐI 3'], GREEN, 520, 145), null, 'x', 0.42);
      hang(0.0, AZ[2], 3.1, 0.82, null, S(['CHECK-OUT XE THÁNG ➔', 'Quẹt thẻ · Ra mặt đất'], AMB, 540, 150), 'x', 0.40);

      // 5. Bảng màu xanh lá (Lối ra Mặt đất): chỉ giữ đúng 4 chữ "LỐI RA MẶT ĐẤT" và đổi mũi tên hướng lên trên (▲)
      hang(HX - 3.2, 19.5, 3.0, 0.82, null, S(['▲ LỐI RA MẶT ĐẤT'], GREEN, 500, 135), 'x', 0.68);
    } else {
      // Các tầng vãng lai (B2, B3, B4):
      // Ngay cửa vào từ dốc chữ U (làn Trái/Trong ZD2 = -12.4): mặt hướng dốc báo số chỗ trống, mặt hướng B2 chỉ dẫn làn lên/xuống
      hang(HX - 2.5, -10.5, 3.4, 0.88,
        S(['TẦNG ' + ids[k] + ' · VÃNG LAI', availLine(k)], avail(k), 580, 165),
        S(['◀ TRÁI: XUỐNG ' + ids[k], 'PHẢI: LÊN ' + ids[k - 1] + ' ▶'], AMB, 600, 165),
        'x', 0.55);
      if (k < n - 1) {
        hang(XG, -14.8, 3.3, 0.92,
          S(['XUỐNG ' + ids[k + 1] + ' ▼', ids[k + 1] + ': ' + availLine(k + 1)], avail(k + 1), 580, 165),
          null, 'z', 0.35);
      }
      // Bảng chỉ dẫn lối ra lên mặt đất trong các lối đi B2+ (không treo chắn trước bảng Booth Check-out)
      hang(0.0, AZ[1], 2.9, 0.78, null, S(['LỐI RA (LÊN ' + ids[0] + ') ➔', k === 1 ? 'Qua Booth Check-out B2' : 'Lên dốc ' + ids[k - 1]], RED, 520, 145), 'x', 0.42);
      if (k > 1) {
        hang(XEC - 2.0, ZU2, 3.1, 0.85, null, S(['LÊN TẦNG ' + ids[k - 1] + ' ➔', 'Đi làn phải (ngoài)'], RED, 540, 150), 'x', 0.38);
      }
    }
  }

  /* ================================================================== */
  /*  DỐC CHỮ U GẬP ĐÔI (U-TURN SWITCHBACK RAMP - THEO HÌNH 14):          */
  /*   - Dốc bên TRÁI (TRONG): hướng TỪ TRÊN XUỐNG (B1 -> B2)            */
  /*   - Dốc bên PHẢI (NGOÀI): hướng TỪ DƯỚI LÊN   (B2 -> B1)            */
  /* ================================================================== */
  function addRamp(lot, j) {
    var fl = lot.floors[j];
    var yTop = 0;                                         // cao độ sàn tầng j (tương đối = 0)
    var yMid = (lot.ys[j + 1] - lot.ys[j]) / 2;           // cao độ chiếu nghỉ chữ U (-FH/2 với hầm)
    var yBot = lot.ys[j + 1] - lot.ys[j];                 // cao độ sàn tầng j+1 (-FH với hầm)
    var Lh = Math.sqrt(RL * RL + yMid * yMid);            // chiều dài mặt dốc mỗi nhánh
    var slope = Math.atan2(yMid, RL);                     // góc nghiêng nửa tầng

    // 1. Nhánh Nam (z = -19.1, rộng 7.6m): nối tầng j (yTop tại x=HX) với chiếu nghỉ chữ U (yMid tại x=HX+RL)
    //    - Làn TRONG / TRÁI (ZD1 = -17.2, local z = +1.9): TỪ TRÊN XUỐNG tầng j+1 (mũi tên xanh dương, hướng +x)
    //    - Làn NGOÀI / PHẢI (ZU1 = -21.0, local z = -1.9): TỪ DƯỚI LÊN tầng j   (mũi tên xanh lá, hướng -x)
    var rSouth = new T.Group();
    rSouth.position.set(HX + RL / 2, (yTop + yMid) / 2, -19.1);
    rSouth.rotation.z = slope;
    U.box(rSouth, Lh, 0.45, 7.6, M.slab, 0, -0.23, 0);
    var topS = U.texPlane(Lh, 7.6, M.floor, Lh / 4, 2); topS.rotation.x = -Math.PI / 2; topS.position.y = 0.004; rSouth.add(topS);
    U.box(rSouth, Lh, 0.14, 0.24, M.paintYellow, 0, 0.07, 0);
    U.box(rSouth, Lh, 0.18, 0.28, M.hazard, 0, 0.09,  3.66);
    U.box(rSouth, Lh, 0.18, 0.28, M.hazard, 0, 0.09, -3.66);
    [-3.2, 3.2].forEach(function (lx) {
      arrowC(rSouth, lx, 0,  1.9, ROT_E, '#2d8cff', 1.15, 2.2);          // làn Trong (Trái): từ Trên Xuống
      arrowC(rSouth, lx, 0, -1.9, ROT_W, '#2fcf6a', 1.15, 2.2);          // làn Ngoài (Phải): từ Dưới Lên
    });
    fl.group.add(rSouth);

    // 2. Nhánh Bắc (z = -10.5, rộng 7.6m): nối chiếu nghỉ chữ U (yMid tại x=HX+RL) với tầng j+1 (yBot tại x=HX)
    //    - Làn TRONG / TRÁI (ZD2 = -12.4, local z = -1.9): TỪ TRÊN XUỐNG vào tầng j+1 (mũi tên xanh dương, hướng -x)
    //    - Làn NGOÀI / PHẢI (ZU2 =  -8.6, local z = +1.9): TỪ DƯỚI LÊN từ tầng j+1    (mũi tên xanh lá, hướng +x)
    var rNorth = new T.Group();
    rNorth.position.set(HX + RL / 2, (yMid + yBot) / 2, -10.5);
    rNorth.rotation.z = -slope;
    U.box(rNorth, Lh, 0.45, 7.6, M.slab, 0, -0.23, 0);
    var topN = U.texPlane(Lh, 7.6, M.floor, Lh / 4, 2); topN.rotation.x = -Math.PI / 2; topN.position.y = 0.004; rNorth.add(topN);
    U.box(rNorth, Lh, 0.14, 0.24, M.paintYellow, 0, 0.07, 0);
    U.box(rNorth, Lh, 0.18, 0.28, M.hazard, 0, 0.09,  3.66);
    U.box(rNorth, Lh, 0.18, 0.28, M.hazard, 0, 0.09, -3.66);
    [-3.2, 3.2].forEach(function (lx) {
      arrowC(rNorth, lx, 0, -1.9, ROT_W, '#2d8cff', 1.15, 2.2);          // làn Trong (Trái): từ Trên Xuống B2
      arrowC(rNorth, lx, 0,  1.9, ROT_E, '#2fcf6a', 1.15, 2.2);          // làn Ngoài (Phải): từ Dưới Lên B1
    });
    fl.group.add(rNorth);

    // 3. Chiếu nghỉ vòng cung chữ U 180° (bán nguyệt tâm tại x = HX + RL, z = Z_RAMP_C = -14.8, cao độ yMid)
    var uTurn = new T.Group();
    uTurn.position.set(HX + RL, yMid, Z_RAMP_C);
    var semiSh = new T.Shape();
    semiSh.absarc(0, 0, R_OUT, -Math.PI / 2, Math.PI / 2, false);
    semiSh.lineTo(0, -R_OUT);
    var semiSlab = new T.ExtrudeGeometry(semiSh, { depth: 0.45, bevelEnabled: false, curveSegments: 28 });
    semiSlab.rotateX(Math.PI / 2);
    var mSlab = new T.Mesh(semiSlab, M.slab); mSlab.position.y = 0; uTurn.add(mSlab);
    var semiTop = new T.ShapeGeometry(semiSh, 28);
    semiTop.rotateX(Math.PI / 2);
    var mTop = new T.Mesh(semiTop, M.floor); mTop.position.y = 0.01; uTurn.add(mTop);

    // Vạch & gờ cong phân cách 2 làn trên chiếu nghỉ chữ U (bán kính R_DIV = 4.3m)
    var divRing = new T.RingGeometry(R_DIV - 0.14, R_DIV + 0.14, 28, 1, -Math.PI / 2, Math.PI);
    divRing.rotateX(-Math.PI / 2);
    var mDiv = new T.Mesh(divRing, M.paintYellow); mDiv.position.y = 0.02; uTurn.add(mDiv);

    // Mũi tên dẫn hướng trên vòng cung chữ U:
    //   - Vòng TRONG (bán kính nhỏ): từ Trên Xuống (nối ZD1 = -17.2 sang ZD2 = -12.4, hướng +z = ROT_N)
    //   - Vòng NGOÀI (bán kính lớn): từ Dưới Lên   (nối ZU2 = -8.6  sang ZU1 = -21.0, hướng -z = ROT_S)
    arrowC(uTurn, (R_IN  + R_DIV) / 2, 0.01, 0, ROT_N, '#2d8cff', 1.0, 1.9);   // vòng Trong: từ Trên Xuống
    arrowC(uTurn, (R_DIV + R_OUT) / 2, 0.01, 0, ROT_S, '#2fcf6a', 1.1, 2.1);   // vòng Ngoài: từ Dưới Lên

    // Tường chắn cong bao quanh chiếu nghỉ chữ U (lan can dưới 1.4m luôn hiện; phần tường trên + trần gắn vào ceil để cắt khi xem Toàn cảnh)
    var wallArc = new T.CylinderGeometry(R_OUT, R_OUT, 1.4, 28, 1, true, 0, Math.PI);
    var mArc = new T.Mesh(wallArc, M.wall); mArc.position.y = 0.7; uTurn.add(mArc);
    var hazArc = new T.CylinderGeometry(R_OUT - 0.04, R_OUT - 0.04, 0.24, 28, 1, true, 0, Math.PI);
    var mHaz = new T.Mesh(hazArc, M.hazard); mHaz.position.y = 0.18; uTurn.add(mHaz);
    if (lot.under) {
      var upH = CLEAR + FH / 2 - 1.4;
      var upArc = new T.Mesh(new T.CylinderGeometry(R_OUT, R_OUT, upH, 28, 1, true, 0, Math.PI), M.wall);
      upArc.position.set(HX + RL, yMid + 1.4 + upH / 2, Z_RAMP_C);
      fl.ceil.add(upArc);
      var mCap = new T.Mesh(semiTop.clone(), M.ceil);
      mCap.position.set(HX + RL, yMid + CLEAR + FH / 2 - 0.02, Z_RAMP_C);
      fl.ceil.add(mCap);
    }

    fl.group.add(uTurn);

    // 4. Tường ngăn giữa 2 nhánh dốc (z = -14.8, từ x = HX đến HX + RL) + đầu trụ tròn
    U.box(fl.group, RL, 1.5, 0.9, M.conc, HX + RL / 2, yMid + 0.1, Z_RAMP_C);
    U.cyl(fl.group, 0.45, 1.5, M.hazard, HX + RL, yMid + 0.1, Z_RAMP_C);
    // Tường chắn 2 bên ngoài dốc & trần hầm dốc (nếu là hầm ngầm)
    U.box(fl.group, RL, 1.4, 0.3, M.conc, HX + RL / 2, (yTop + yMid) / 2 + 0.5, -22.95);
    U.box(fl.group, RL, 1.4, 0.3, M.conc, HX + RL / 2, (yMid + yBot) / 2 + 0.5, -6.65);
    if (lot.under) {
      var sideH = CLEAR + FH / 2;
      U.box(fl.ceil, RL, sideH, 0.3, M.wall, HX + RL / 2, yMid + sideH / 2, -22.95);
      U.box(fl.ceil, RL, sideH, 0.3, M.wall, HX + RL / 2, yBot + sideH / 2, -6.65);
      U.box(fl.ceil, RL + 0.4, 0.3, 16.6, M.slab, HX + RL / 2, CLEAR + 0.1, Z_RAMP_C);
    }

    // 5. Bảng màu cam ngay đầu dốc: hiển thị rõ "Làn xe xuống B2 | Làn xe lên B1" theo đúng yêu cầu!
    var fDown = lot.def.ids[j + 1], fUp = lot.def.ids[j];
    var rt = U.signTex(
      ['DỐC ' + fUp + ' ⇄ ' + fDown, 'Làn xe xuống ' + fDown + '  |  Làn xe lên ' + fUp],
      '#c46a00', '#ffffff', 880, 180
    );
    U.hangSign(fl.group, HX - 0.4, -19.1, 3.8, 0.88, rt, rt, 'x', 0.65);
  }

  function addPortals(lot) {
    var g0 = lot.floors[0].group, ceil0 = lot.floors[0].ceil, under = lot.under, y0 = 0;
    var RL_PORTAL = 20.0;
    if (under) {
      // Hào vào (đơn) phía Tây và hào ra (2 làn vàng/xanh lá) phía Đông, từ mặt đất xuống B1
      var gy = y0 + FH, zE0 = Z_ENTRY - 2.1, zE1 = Z_ENTRY + 2.1;
      var rg = new T.Group(); rg.position.set(-HX - RL_PORTAL / 2, y0 + FH / 2, Z_ENTRY); rg.rotation.z = -Math.atan2(FH, RL_PORTAL);
      var Lr = Math.sqrt(RL_PORTAL * RL_PORTAL + FH * FH);
      U.box(rg, Lr, 0.5, 4.2, M.slab, 0, -0.25, 0);
      var t1 = U.texPlane(Lr, 4.2, M.floor, Lr / 4, 1); t1.rotation.x = -Math.PI / 2; t1.position.y = 0.004; rg.add(t1);
      [-5, 5].forEach(function (lx) { arrowC(rg, lx, 0, 0, ROT_E, '#2d8cff', 1.2, 2.4); });
      g0.add(rg);
      triWall(g0, -HX - RL_PORTAL, -HX, gy, y0, gy, zE0); triWall(g0, -HX - RL_PORTAL, -HX, gy, y0, gy, zE1);

      var rx = new T.Group(); rx.position.set(HX + RL_PORTAL / 2, y0 + FH / 2, 18.5); rx.rotation.z = Math.atan2(FH, RL_PORTAL);
      U.box(rx, Lr, 0.5, 8.0, M.slab, 0, -0.25, 0);
      var t2 = U.texPlane(Lr, 8.0, M.floor, Lr / 4, 2); t2.rotation.x = -Math.PI / 2; t2.position.y = 0.004; rx.add(t2);
      [-5, 5].forEach(function (lx) {
        arrowC(rx, lx, 0, -2, ROT_E, '#2fcf6a', 1.2, 2.4);
        arrowC(rx, lx, 0,  2, ROT_E, '#f2c200', 1.2, 2.4);
      });
      g0.add(rx);
      triWall(g0, HX, HX + RL_PORTAL, y0, gy, gy, 14.5); triWall(g0, HX, HX + RL_PORTAL, y0, gy, gy, 22.5);

      // Cọc tiêu phân làn trên dốc ra mặt đất
      var bol = [];
      for (var i = 0; i <= 9; i++) {
        var bx = HX + RL_PORTAL * i / 9;
        bol.push({ x: bx, y: y0 + FH * i / 9 + 0.35, z: 18.5, sx: 1, sy: 1, sz: 1 });
      }
      U.inst(g0, U.getGeo('bollard', function () { return new T.CylinderGeometry(0.1, 0.15, 0.72, 10); }), M.cone, bol);

      // Mặt đất có khoét hào, đường phố (ẩn khi xem Toàn cảnh)
      var sh = new T.Shape(); sh.moveTo(-220, -220); sh.lineTo(220, -220); sh.lineTo(220, 220); sh.lineTo(-220, 220); sh.closePath();
      [[-HX - RL_PORTAL, -HX, zE0, zE1], [HX, HX + RL_PORTAL, 14.5, 22.5]].forEach(function (h) {
        var p = new T.Path(); p.moveTo(h[0], -h[2]); p.lineTo(h[1], -h[2]); p.lineTo(h[1], -h[3]); p.lineTo(h[0], -h[3]); p.closePath(); sh.holes.push(p);
      });
      var gg = new T.ShapeGeometry(sh); gg.rotateX(-Math.PI / 2);
      var gm = new T.Mesh(gg, M.ground); gm.position.y = gy - 0.03; ceil0.add(gm);
      [[-HX - RL_PORTAL - 12, Z_ENTRY], [HX + RL_PORTAL + 12, 18.5]].forEach(function (r) {
        var rd = new T.Mesh(new T.PlaneGeometry(24, 8.2), M.road); rd.rotation.x = -Math.PI / 2; rd.position.set(r[0], gy - 0.02, r[1]); ceil0.add(rd);
        var sky = new T.Mesh(new T.PlaneGeometry(12, 7), M.sky); sky.rotation.y = Math.PI / 2; sky.position.set(r[0] + (r[0] < 0 ? -12 : 12), gy + 2.5, r[1]); ceil0.add(sky);
      });
    } else {
      [[-HX - 8, Z_ENTRY, 4.2], [HX + 8, 18.5, 8.0]].forEach(function (r) {
        U.box(g0, 16, 0.1, r[2], M.road, r[0], -0.05, r[1]);
      });
      var bol2 = [];
      for (var q = 0; q <= 8; q++) bol2.push({ x: HX + 16 * q / 8, y: 0.35, z: 18.5, sx: 1, sy: 1, sz: 1 });
      U.inst(g0, U.getGeo('bollard', function () { return new T.CylinderGeometry(0.1, 0.15, 0.72, 10); }), M.cone, bol2);
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
    if (x < -HX) return lot.under ? 'Dốc vào hầm ' + lot.def.ids[0] : 'Đường vào bãi ' + lot.def.ids[0];
    if (x > HX) {
      if (z > 10) return 'Dốc ra mặt đất (làn vàng · xanh lá)';
      if (x > HX + RL - 0.5) return 'Chiếu nghỉ vòng cung chữ U';
      return 'Dốc chữ U liên tầng';
    }
    if (k === 0) {
      if (z < -15.6) return 'Làn xe VÃNG LAI (đi thẳng xuống ' + (lot.def.ids[1] || 'B2') + ')';
      if (x > XR) return x < XB ? 'Làn ra xe THÁNG (mũi tên vàng)' : 'Làn ra xe VÃNG LAI (mũi tên xanh lá)';
      return 'Khu xe THÁNG · ' + rowName(z);
    }
    if (z < -10.5 && x > XEC) return 'Cửa ra Check-out / Lên ' + lot.def.ids[k - 1];
    if (z >= -10.5 && z < -6.0 && x > XEC) return 'Cửa vào tầng ' + lot.def.ids[k];
    return 'Khu VÃNG LAI (' + lot.def.ids[k] + ') · ' + rowName(z);
  }
  function rowName(z) {
    return z < -15 ? 'Làn phía Nam' : z < -10 ? 'Dãy A' : z < -4 ? 'Lối đi 1' : z < 1 ? 'Dãy B' : z < 7 ? 'Lối đi 2' : z < 12 ? 'Dãy C' : z < 18 ? 'Lối đi 3' : 'Dãy D';
  }

  /* ================================================================== */
  /*  ĐỒ THỊ ĐIỂM DI CHUYỂN (Street View)                                 */
  /* ================================================================== */
  function buildGraph(lot) {
    var n = lot.n, nodes = lot.nodes, under = lot.under;
    function addNode(x, y, z, fl, type) {
      for (var i = 0; i < nodes.length; i++) {
        var q = nodes[i];
        if (Math.abs(q.x - x) < 1.6 && Math.abs(q.z - z) < 1.6 && Math.abs(q.y - y) < 0.7) {
          if (type === 'core') q.type = 'core';
          return q;
        }
      }
      var nd = { id: nodes.length, x: x, y: y, z: z, floorIdx: fl, type: type || 'lane', nb: [] };
      nodes.push(nd); return nd;
    }
    function link(a, b) { if (a !== b && a.nb.indexOf(b.id) < 0) { a.nb.push(b.id); b.nb.push(a.id); } }
    function path(fl, pts, flOf) {
      var prev = null;
      for (var i = 0; i < pts.length; i++) {
        var p = pts[i], q = pts[i - 1], seg = [];
        if (q) {
          var dx = p[0] - q[0], dz = p[1] - q[1], len = Math.sqrt(dx * dx + dz * dz), steps = Math.ceil(len / 7.0);
          for (var s = 1; s < steps; s++) {
            var t = s / steps;
            seg.push([q[0] + dx * t, q[1] + dz * t, q.length > 2 ? q[2] + (p[2] - q[2]) * t : undefined]);
          }
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

    lot.floors.forEach(function (fl) {
      var k = fl.k, y = fl.y, mon = k === 0;
      // 3 lối đi chính (Lối đi 1, 2, 3): trên tầng B1 đã bỏ lối đi dọc ở cuối khu để xe (chỉ Lối đi 3 thông qua Booth Check-out sang XY)
      AZ.forEach(function (z, ai) {
        var pts = [[BX, z], [-1.0, z], [XEC, z]];
        if (mon && ai === 2) {
          pts.push([XY, z]);
        } else if (!mon) {
          pts.push([XG, z]);
        }
        path(k, pts);
      });

      // Trục Tây (BX) nối 3 lối đi; trên B1 KHÔNG nối dọc ở cuối khu để xe (XEC) theo yêu cầu
      path(k, [[BX, AZ[0]], [BX, AZ[1]], [BX, AZ[2]]]);
      if (!mon) {
        path(k, [[XEC, AZ[0]], [XEC, AZ[1]], [XEC, AZ[2]]]);
      }

      // Phòng thang máy: tạo điểm đứng NGOÀI cửa kính (outNode) nối vào điểm đứng TRONG phòng kính (inNode, type='core')
      fl.lobbies.forEach(function (lb) {
        var outNd = addNode(lb.outPos.x, y, lb.outPos.z, k, 'lobby_out');
        var inNd  = addNode(lb.pos.x,    y, lb.pos.z,    k, 'core');
        var laneNd = addNode(lb.lane[0], y, lb.lane[1], k);
        link(laneNd, outNd);
        link(outNd, inNd);
        lb.outNodeId = outNd.id;
        lb.nodeId = inNd.id;
        lb.yaw = Math.atan2(-lb.fx, -lb.fz);
      });

      if (mon) {
        // Nhánh rẽ phải từ làn vào qua Booth Check-in vào khu xe tháng
        path(k, [[BX, ZD1], [BX, -11.0], [BX, AZ[0]]]);
        // Làn bypass vãng lai phía Nam đi thẳng vào làn TRONG (ZD1 = -17.2) xuống dốc B2
        path(k, [[-HX, Z_ENTRY], [BX, ZD1], [-2.0, ZD1], [XEC, ZD1], [XG, ZD1], [HX, ZD1]]);
        // Làn ra xe tháng (mũi tên vàng XY) và làn ra xe vãng lai (mũi tên xanh lá XG nối từ làn NGOÀI ZU1 = -21.0)
        path(k, [[XY, AZ[2]], [XY, ZEY], [HX, ZEY]]);
        path(k, [[HX, ZU1], [XG, ZU1], [XG, -7.0], [XG, 4.0], [XG, ZEG], [HX, ZEG]]);
      } else {
        // Tầng vãng lai (B2, B3, B4):
        // 1. Cửa vào từ làn TRÁI / TRONG của dốc chữ U (ZD2 = -12.4, từ Trên Xuống)
        path(k, [[HX, ZD2], [XG, ZD2], [XEC, ZD2], [-2.0, -15.5], [BX, -15.5], [BX, AZ[0]]]);
        // 2. Trục Đông (XG) & trục gom Đông (XEC) dẫn xe từ các lối đi ra làn PHẢI / NGOÀI (ZU2 = -8.6, từ Dưới Lên) qua Booth Check-out B2
        path(k, [[XG, AZ[2]], [XG, AZ[1]], [XG, ZU2]]);
        path(k, [[XEC, AZ[0]], [XEC, ZU2], [XG, ZU2], [HX, ZU2]]);
        if (k < n - 1) {
          path(k, [[XG, ZD2], [XG, ZD1], [HX, ZD1]]);
          path(k, [[HX, ZU1], [XG, ZU1], [XG, ZD2]]);
        }
      }
    });

    // Hào vào / ra mặt đất ở tầng đầu
    var RL_PORTAL = 20.0;
    var gy = lot.ys[0] + (under ? FH : 0);
    path(0, under ? [[-HX - RL_PORTAL, Z_ENTRY, gy], [-HX - RL_PORTAL / 2, Z_ENTRY, lot.ys[0] + FH / 2], [-HX, Z_ENTRY, lot.ys[0]]] : [[-HX - 12, Z_ENTRY], [-HX, Z_ENTRY]]);
    path(0, under ? [[HX, ZEG, lot.ys[0]], [HX + RL_PORTAL / 2, ZEG, lot.ys[0] + FH / 2], [HX + RL_PORTAL, ZEG, gy]] : [[HX, ZEG], [HX + 14, ZEG]]);
    path(0, under ? [[HX, ZEY, lot.ys[0]], [HX + RL_PORTAL, ZEY, gy]] : [[HX, ZEY], [HX + 14, ZEY]]);

    // Đồ thị điểm di chuyển trên dốc chữ U liên tầng (nối tầng j với tầng j+1)
    for (var j = 0; j < n - 1; j++) {
      var yTop = lot.ys[j], yMid = (lot.ys[j] + lot.ys[j + 1]) / 2, yBot = lot.ys[j + 1];
      // Làn TRÁI / TRONG (từ Trên Xuống): (HX, ZD1, yTop) -> vòng trong chữ U -> (HX, ZD2, yBot)
      path(0, [
        [HX, ZD1, yTop],
        [HX + RL * 0.6, ZD1, yTop + (yMid - yTop) * 0.6],
        [HX + RL + 2.4, ZD1 + 1.0, yMid],
        [HX + RL + 2.4, ZD2 - 1.0, yMid],
        [HX + RL * 0.6, ZD2, yMid + (yBot - yMid) * 0.4],
        [HX, ZD2, yBot]
      ], function (c) { return c[2] > yMid - 0.1 ? j : j + 1; });

      // Làn PHẢI / NGOÀI (từ Dưới Lên): (HX, ZU2, yBot) -> vòng ngoài chữ U -> (HX, ZU1, yTop)
      path(0, [
        [HX, ZU2, yBot],
        [HX + RL * 0.6, ZU2, yBot + (yMid - yBot) * 0.6],
        [HX + RL + 5.8, ZU2 - 2.0, yMid],
        [HX + RL + 5.8, ZU1 + 2.0, yMid],
        [HX + RL * 0.6, ZU1, yMid + (yTop - yMid) * 0.4],
        [HX, ZU1, yTop]
      ], function (c) { return c[2] < yMid + 0.1 ? j + 1 : j; });
    }

    // Điểm tham chiếu nhảy nhanh (POIs)
    function nearest(k, x, z) {
      var b = null, bd = 1e9;
      nodes.forEach(function (q) {
        if (q.floorIdx !== k) return;
        var d = (q.x - x) * (q.x - x) + (q.z - z) * (q.z - z);
        if (d < bd) { bd = d; b = q; }
      });
      return b;
    }
    lot.pois = {
      entry:    { node: nearest(0, -HX, Z_ENTRY),   yaw: -Math.PI / 2 },
      checkin:  { node: nearest(0, BX, ZD1),        yaw: Math.PI },
      checkout: { node: nearest(0, XY, AZ[2]),      yaw: Math.PI - 0.16 },
      ramp:     { node: nearest(1, XEC, ZU2),       yaw: -Math.PI / 2 }
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
